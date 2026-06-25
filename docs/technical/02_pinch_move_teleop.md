# Pinch-and-Move End-Effector Control

## Purpose

The operator controls the robot's end-effector with bare hands. A virtual handle sits on
the robot flange; the operator **pinches** it and **drags** it through space. The handle's
world pose becomes an IK target, which is solved to joint angles and streamed to the
controller. The visible handle stays locked to the real flange, so the operator always
grabs "the robot" rather than a free-floating widget.

Two interaction methods exist, both feeding the same world-space IK target:

- **Direct hand-delta grab** (`PhantomGrabRelay`) — the IK target moves by the hand's
  displacement since the moment of grab.
- **Visual-lock grab** (`IKHandleVisualLock`) — Meta's grab interactable moves a handle,
  the script copies its pose to the IK target and snaps the visible mesh back to the TCP.

A third, discrete gesture — **spatial pinch tap** — places a target on a real-world
surface by raycasting from the palm.

## Data flow

```text
Quest hand  --(Meta SDK pinch/grab recogniser)-->  GestureRouter
                                                      |  Pinch -> Teleop
                                                      |  Point -> Jog
                                                      |  Free  -> Command
                                                      v
   +--------------------------------------------------------------+
   | Continuous grab (one of):                                     |
   |   PhantomGrabRelay.LateUpdate                                 |
   |     ikTarget.pos = ee_pos_at_grab + (hand - hand_at_grab)*gain|
   |   IKHandleVisualLock.LateUpdate                               |
   |     ikTarget.pos = grabbed_handle.pos ; handle.pos = tcp.pos  |
   +-------------------------------+------------------------------+
                                   v
   IKTargetPosePublisher.Update  (50 Hz, only while grabbed)
     express target in robotBase frame; convert Unity LH->ROS RH (FLU)
     publish geometry_msgs/PoseStamped -> /metamove/ik_target
                                   |
                                   v ROS-TCP-Connector
   +--------------------------------------------------------------+
   | ROS 2 — one of two relays:                                    |
   |  moveit_ik_relay.py  (position IK):                           |
   |    gate: target fresh? joint_state fresh?                     |
   |    /compute_ik(pose, seed=joint_states)                       |
   |    per-joint slew clamp -> /servo_node/commands (Float64MArray)|
   |  pose_to_twist_node.py  (servo twist):                        |
   |    P-control pos error + quaternion-log rot error             |
   |    -> /servo_node/delta_twist_cmds (TwistStamped)              |
   +-------------------------------+------------------------------+
                                   v
        EGM bridge (Windows) -> UDP -> GoFa controller
```

## Hand tracking & gesture routing

`OVRHandPoseProvider.cs`
: Wraps the Meta hand-tracking rig and exposes palm position, palm normal, per-finger
  curl, and index-pointing direction in world space.

`GestureRouter.cs`
: Central gesture dispatcher. Meta's shape recognisers raise pinch/point begin/end events
  here; `UpdateModeFromGesture()` selects the active mode — **pinch -> Teleop**,
  **point/thumb -> Jog**, **hand free -> Command** — and forwards `OnBegin`/`OnEnd` to the
  grab relays and the spatial-pinch controller.

Pinch detection itself is provided by the Meta SDK shape recognition and wired into the
router via UnityEvent adapters.

## Continuous teleoperation

### Method A — `PhantomGrabRelay.cs` (direct hand-delta)

The visible sphere is a child of `Joint_6` and never moves visually. A separate
world-space `ikTarget` transform is driven by the hand:

```csharp
// on grab begin
_ikStartPos  = transform.position;   // current EE position
_handStartPos = anchorPos;           // hand grab-point at grab start

// each frame while grabbed
Vector3 posDelta  = (anchorPos - _handStartPos) * dragGain;
Vector3 targetPos = _ikStartPos + posDelta;
ikTarget.position = targetPos;       // clamped to maxReachM from the EE
```

The hand anchor is taken, in priority order, from the live Oculus selecting point, an
inspector override, or the camera as a last resort.

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `dragGain` | 1.0 | Multiplier on hand displacement (1.0 = 1:1 motion). |
| `maxReachM` | 0.5 m | Clamp on IK-target distance from the current EE. |

### Method B — `IKHandleVisualLock.cs` (visual lock)

Meta's `HandGrabInteractable` (or `DistanceHandGrabInteractable`) moves a handle while
grabbed. Each `LateUpdate` the script copies that pose to the IK target and then snaps the
visible handle back to the TCP, so the mesh stays locked to the real flange:

```csharp
if (IsGrabbed) ikTarget.position = transform.position; // hand motion -> IK target
else           ikTarget.position = tcp.position;        // idle: target at EE
transform.position = tcp.position;                       // visible lock to flange
```

### Discrete — `SpatialPinchController.cs` (tap-to-place)

A quick pinch-tap (<= `tapMaxDurationSeconds`, default 0.2 s) raycasts from the palm along
the palm normal up to a configured length and places a waypoint / IK target at the hit
point. Longer pinches fall through to the continuous teleop mode.

## Unity -> ROS publishing

`IKTargetPosePublisher.cs`
: Publishes the IK target at 50 Hz. It expresses the target in the robot base frame,
  converts Unity's left-handed Y-up coordinates to ROS right-handed Z-up (FLU), and
  publishes a `geometry_msgs/PoseStamped` on **`/metamove/ik_target`** with `frame_id =
  base_link`.

Publishing is **gated**: it streams only while the handle is actually grabbed
(`onlyWhenGrabbed`), which prevents the robot from chasing a stale target after release.

| Field | Value |
|-------|-------|
| Topic | `/metamove/ik_target` |
| Message | `geometry_msgs/PoseStamped` |
| Frame | `base_link` |
| Rate | 50 Hz |

## ROS-side relays

### `moveit_ik_relay.py` — position IK

Subscribes `/metamove/ik_target` and `/joint_states`, and calls MoveIt's `/compute_ik`
service. The loop runs at 50 Hz with two **fail-safe gates**:

- **Target freshness** — if the last target is older than `target_timeout`, do nothing.
- **Seed freshness** — if `/joint_states` is older than `joint_state_timeout`, refuse to
  command (no live robot pose -> no motion).

The IK request seeds from the current joint state. The solution is **slew-limited
per joint** (`max_joint_speed * dt`) before publishing to `/servo_node/commands`.

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `target_timeout` | 0.3 s | Drop stale grab targets. |
| `joint_state_timeout` | 0.5 s | Refuse to command without a fresh seed. |
| `max_joint_speed` | 0.3 rad/s | Per-joint slew clamp (~0.006 rad/tick @ 50 Hz). |

### `pose_to_twist_node.py` — servo twist (alternative)

Converts an absolute pose target into a Cartesian velocity twist for MoveIt Servo. It
reads the current EE pose from TF, applies P-control on the position error and a
quaternion-log (axis-angle) term on the orientation error, clamps to maximum linear and
angular speed, and publishes `geometry_msgs/TwistStamped` on
`/servo_node/delta_twist_cmds`.

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `linear_gain` | 1.5 /s | Position P-gain. |
| `angular_gain` | 1.5 /s | Orientation P-gain. |
| `max_linear` | 0.25 m/s | Linear velocity clamp. |
| `max_angular` | 1.0 rad/s | Angular velocity clamp. |
| `deadband_pos_m` | small | Ignore sub-millimetre jitter. |

## Topics & message types

| Topic | Message | Producer -> Consumer (rate) |
|-------|---------|------------------------------|
| `/metamove/ik_target` | `geometry_msgs/PoseStamped` | Unity publisher -> IK/twist relay (50 Hz) |
| `/servo_node/commands` | `std_msgs/Float64MultiArray` | `moveit_ik_relay` -> EGM bridge (50 Hz) |
| `/servo_node/delta_twist_cmds` | `geometry_msgs/TwistStamped` | `pose_to_twist_node` -> MoveIt Servo (100 Hz) |
| `/joint_states` | `sensor_msgs/JointState` | EGM bridge -> relays (seed) |

## Design notes

- The visible handle is always locked to the **real** flange; the operator manipulates a
  separate invisible IK target. This avoids visual drift between command and reality.
- Both relays enforce **seed freshness** — without a live `/joint_states` they will not
  command motion, which prevents commanding into an unknown robot state.
- The position-IK relay and the servo-twist node are interchangeable backends; IK gives
  absolute pose tracking, twist gives smooth velocity control through MoveIt Servo's
  collision/singularity handling.
