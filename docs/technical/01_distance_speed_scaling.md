# Distance-Based Speed Scaling

## Purpose

The robot's motion speed is continuously scaled by the distance between the nearest
human and the robot. As the operator approaches, the robot slows down; below a near
threshold it freezes entirely. As they retreat, speed ramps back up smoothly. This is
a collaborative-safety layer that runs **on top of** any commanded motion — teach
playback, pinch teleoperation, or autonomous pick-and-place.

The design has two defining properties:

- **Asymmetric response** — speed ramps **up slowly** (smooth acceleration) but drops
  to a stop **instantly** (reactive safety).
- **Stale-safe** — if the distance signal goes stale (e.g. the headset is removed),
  the system freezes / pauses cleanly rather than coasting.

There are two implementations sharing the same formula: a **local** Unity-only path for
offline demos, and the **ROS** path used with the real robot.

## Data flow

```text
Quest human pose (head + optional hands)
        |
        +-- LOCAL DEMO ----------------------------------------------+
        |   ProximitySpeedController.cs                               |
        |     min_distance(human -> robotBase) -> EMA filter -> slew     |
        |     -> Factor (0..1) -> PickPlaceLoop motion gain             |
        |                                                             |
        +-- ROS PATH ---------------------------------------------+  |
            SafetyHud.cs                                           |  |
              publishes /quest/min_distance  (Float32, ~20 Hz)     |  |
                        |                                          |  |
                        v                                          |  |
            distance_speed_scaler.py  (ROS 2, 50 Hz)               |  |
              subscribe /quest/min_distance                        |  |
              EMA filter + asymmetric slew + stale timeout         |  |
              +- publish /robot/speed_factor   (heartbeat -> HUD)   |  |
              +- set param jtc_servo_relay.live_speed  (0..1)       |  |
                        |                                          |  |
                        v                                          |  |
            jtc_servo_relay.py  (50 Hz)                            |  |
              tc += period * live_speed   (advance time cursor)    |  |
              interpolate keyframes -> /servo_node/commands         |  |
                        |                                          |  |
                        v                                          v  v
            EGM bridge (Windows) -> UDP -> GoFa controller
```

## The scaling law

The distance `d` (metres) is mapped to a raw factor with a linear band between a near
and far threshold:

```text
 band(d) = 0.0                          if d <= d_near       (freeze)
 band(d) = (d - d_near)/(d_far - d_near) if d_near < d < d_far
 band(d) = 1.0                          if d >= d_far        (full speed)
```

The raw factor is then **low-pass filtered** (exponential moving average) and passed
through an **asymmetric slew limiter**:

```text
 filtered = alpha * raw + (1 - alpha) * filtered          # EMA smoothing
 if raw_distance <= d_near: target = 0             # hard safety overrides filter

 # asymmetric slew
 if target > v_out:  v_out = min(target, v_out + up_rate * dt)   # ramp up slowly
 else:               v_out = target                              # drop instantly
```

If no fresh distance arrives within the stale timeout, the output collapses to `0` and,
on the ROS path, the playback node is paused for a clean stop instead of a hard jerk.

### Default parameters

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `d_near` | 0.6 m | At/below this distance the factor is 0 (freeze). |
| `d_far` | 2.0 m | At/above this distance the factor is 1 (full speed). |
| `ema_alpha` (alpha) | 0.3 | Low-pass strength; higher = more responsive, less smooth. |
| `up_rate` | 0.6 / s | Maximum rise of the factor per second (smooth acceleration). |
| `stale_timeout` | 1.5 s | No distance for this long -> freeze / pause. |
| `min_delta` | 0.01 | Skip param writes smaller than this. |
| `max_rate_hz` | 15.0 | Cap on `live_speed` write rate to the relay. |
| scaler tick | 20 Hz | `distance_speed_scaler` timer (`_tick_dt = 0.05`). |
| relay tick | 50 Hz | `jtc_servo_relay` interpolation (`rate_hz`). |

```{note}
The scaler does not own a topic to the relay — it writes the relay's `live_speed`
**parameter** over the `/<relay_node>/set_parameters` service (`relay_node` defaults to
`joint_trajectory_controller`, which is the rclpy node name of `jtc_servo_relay`). On
stale distance or MANUAL mode it also calls `/<playback_node>/pause` and `/resume`
(`std_srvs/Trigger`).
```

Down-ramping is intentionally **not** rate-limited: a reduction in commanded speed is
applied on the next tick.

## Key files

### Unity (C#)

`ProximitySpeedController.cs`
: Local-only controller. Computes the nearest human-to-`robotBase` distance, applies the
  EMA filter and asymmetric slew, and exposes `Factor` (0..1). Consumed directly by
  `PickPlaceLoop`.

`SafetyHud.cs`
: Dual-mode. In **ROS mode** it publishes the nearest distance on `/quest/min_distance`
  (~20 Hz) and subscribes to `/robot/speed_factor` to display what the robot is actually
  doing. In **local demo** mode it reads distance and factor straight from
  `ProximitySpeedController`.

`ScalingModeToggle.cs`
: AUTO/MANUAL switch. A controller button or a near-touch poke publishes
  `/quest/scaling_enabled` (Bool), which toggles `distance_speed_scaler.enabled`. When
  disabled, the manual console owns `live_speed`.

`PickPlaceLoop.cs`
: Local demo consumer — multiplies position and rotation motion by `Factor` each frame.

### ROS 2 (Python)

`distance_speed_scaler.py`
: The authoritative node on the real-robot path. Subscribes `/quest/min_distance`,
  applies the same filter + slew, publishes `/robot/speed_factor` as a heartbeat, and
  continuously sets the `jtc_servo_relay.live_speed` parameter. Handles the stale timeout
  and pauses `/dpp_playback` on stale distance or MANUAL mode.

`jtc_servo_relay.py`
: Reads `live_speed` every 50 Hz tick and advances a trajectory-time cursor by
  `period * live_speed`, then interpolates the keyframes and publishes joint positions to
  `/servo_node/commands`. Because speed is applied to the **time cursor** rather than to
  waypoints, it can slow or stop **mid-motion** smoothly.

`dpp_orchestrate.py`
: Lab orchestrator that sets MoveIt-level `velocity_scaling` per phase
  (e.g. normal 0.15, fast 0.50) for dynamic measurement runs — a coarser, waypoint-level
  scaling distinct from the continuous `live_speed`.

## Where speed is applied

1. **Unity IK-target motion** — `PickPlaceLoop` scales per-frame position/rotation by `Factor`.
2. **Trajectory playback cursor** — `jtc_servo_relay` advances time by `period * live_speed` each tick (continuous, mid-motion).
3. **MoveIt waypoint scaling** — `dpp_playback` `max_velocity_scaling_factor` per waypoint (coarser).
4. **EGM servo loop** — interpolated joint positions are streamed over UDP to the controller.

## Design notes

- The Unity and ROS implementations use an **identical** formula, so the offline demo and
  the real robot behave the same.
- `/robot/speed_factor` is fed back to the headset HUD so the operator always sees the
  speed the robot is actually executing, not just the commanded value.
- The MoveSpeed/MaxSpeedDeviation caps in the RAPID source are a **separate, dominant**
  governance layer — the EGM correction can never exceed the controller-side limits
  regardless of `live_speed`.
