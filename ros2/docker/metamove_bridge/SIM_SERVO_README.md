# Sim-Servo Path (ROS → Python → RWS → VC)

Bridge from `/servo_node/commands` (MoveIt-Servo output, Float64MultiArray 6 joints in rad) to a RAPID PERS jointtarget via RWS REST. RAPID-side loop reads the PERS and issues `MoveAbsJ`.

No Unity in the closed loop. No EGM/UDP. Works through WSL2 (TCP outbound only).

## Latency expectation

- HTTPS Digest Auth roundtrip: ~10-20 ms
- RWS endpoint server-side: ~5-15 ms
- RAPID scheduler tick: ~10-25 ms
- **Total end-to-end: ~25-60 ms**

Acceptable for scripted demo / MoveIt-planned trajectories / pick-and-place. NOT acceptable for realtime Quest hand teleop — keep EGM-Unity-bridge for that.

## Setup

### Controller side (VC or real GoFa)

1. Load `robotstudio/rapid/MetaMoveCorePers.mod` into `T_ROB1`:
   - In RobotStudio: RAPID tab → right-click `T_ROB1` → "Modul laden" → select the file
2. Set program pointer to `MetaMoveCorePers.main`
3. Motors On
4. Play (status: Running)

### ROS container side

```bash
# In the metamove-ros2 container
ros2 launch metamove_bridge sim_servo.launch.py rws_ip:=192.168.125.1
```

For a VC running on the same Windows host (port-forwarded from container), use:
```bash
ros2 launch metamove_bridge sim_servo.launch.py rws_ip:=host.docker.internal rws_port:=443
```

### Drive with twists (test)

In another terminal in the same container:
```bash
source /opt/metamove_ws/install/setup.bash
ros2 service call /servo_node/start_servo std_srvs/srv/Trigger
ros2 topic pub --rate 30 /servo_node/delta_twist_cmds geometry_msgs/msg/TwistStamped \
  "{header: {frame_id: 'base_link'}, twist: {linear: {z: 0.02}}}"
```

Then Servo computes `/servo_node/commands` and bridge forwards as PERS-writes. RAPID `MoveAbsJ` follows.

## Telemetry

```bash
# Throughput + latency every 1s
ros2 topic echo /metamove/motion_rate

# Example output:
# {hz_in: 30, hz_out: 28, last_latency_ms: 24.7}
#  ↑ ROS subscribed at 30 Hz, RWS write succeeded 28x, last write took 24.7 ms
```

## Tuning

- `servo_max_rate_hz` parameter caps outbound writes (default 30 Hz). If avg latency stays under 30 ms, raise to 50 Hz. If digest auth keeps re-handshaking and you see >100 ms spikes, lower to 20 Hz.
- RAPID `vServo` in MetaMoveCorePers.mod sets max joint speed — adjust based on target hardware.
- `z10` blend zone keeps motion smooth at low rates. For higher precision use `fine` (but expect stop-and-go).

## Limitations

- RWS REST overhead is significant — every joint write does HTTPS Digest auth roundtrip
- If you need >50 Hz sustained, switch to a single persistent HTTP/2 connection or use a different transport
- PERS-variable polling in RAPID isn't truly realtime — there's a 1-2 RAPID-scheduler-cycle delay between PERS write and `MoveAbsJ` start
