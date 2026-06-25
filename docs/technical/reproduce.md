# Reproduction Guide — Build It Yourself

This chapter is a complete, ordered recipe for rebuilding MetaMove from scratch. It is
written so that the **simulation path needs no robot and no headset** — you can stand up
the full ROS 2 motion stack on one Linux/WSL2 machine and exercise the IK loop, the
playback, and the distance speed scaler against a fake joint-state publisher. The
**real-robot path** then adds the RAPID module, the controller `UDPUC` config, the Windows
EGM bridge, and the Quest client.

All exact values below (node names, parameters, topics, ports) are taken from the source
in this repository. Substitute your own LAN addresses where you see `<...>`; the lab's own
values are shown as concrete examples.

```{contents}
:local:
:depth: 1
```

## 0. What you are building

```text
                 +--------- SIMULATION PATH (no hardware) ---------+
Unity (Quest) -> | ROS 2 graph: move_group + relays + fake_jsp     |
   or console    | /metamove/ik_target -> IK -> /servo_node/commands|
                 +-------------------------------------------------+
                                     |  add hardware
                                     v
                 +---------- REAL-ROBOT PATH -----------+
                 | EGM bridge (Windows) <-UDP-> GoFa    |
                 +--------------------------------------+
```

Two interchangeable IK backends exist; pick one:

- `moveit_ik_relay` — calls MoveIt `/compute_ik`, outputs joint positions.
- `pose_to_twist_node` + MoveIt Servo — outputs a Cartesian twist.

## 1. Prerequisites

| Tool | Version / note |
|------|----------------|
| OS | Linux or Windows 11 + **WSL2** (Ubuntu). Run ROS in Docker **in WSL2**, not Docker Desktop. |
| Docker | Engine with Compose v2. |
| ROS 2 | **Jazzy** (provided by the image; you do not install it on the host). |
| Robot description | `abb_crb15000_moveit` MoveIt config package for the GoFa CRB 15000 5/0.95. |
| Unity | **6000.4.0f1** (Unity 6 LTS) — only for the headset client. |
| Robot | ABB GoFa CRB 15000, RobotWare 7.x with the EGM option — only for the real path. |

## 2. Build the ROS 2 image

The image is defined by `ros2/docker/Dockerfile` (base `ros:jazzy-ros-base`). It installs
MoveIt + MoveIt Servo, `ros2_control`, `rosbridge_suite`, `ros-tcp-endpoint`, and
`octomap`, and builds the `metamove_bridge` package into `/opt/metamove_ws`.

```bash
cd MetaMove
docker build -f ros2/docker/Dockerfile -t metamove/ros2:jazzy .
```

Key environment baked into the image (override at `docker run` as needed):

```text
ROS_DOMAIN_ID=42
RMW_IMPLEMENTATION=rmw_fastrtps_cpp
METAMOVE_RWS_IP=192.168.125.1   METAMOVE_RWS_PORT=443   METAMOVE_EGM_PORT=6511
```

The `metamove_bridge` package (`ament_python`) exposes these console scripts
(`ros2 run metamove_bridge <name>`):

```text
bridge_node            moveit_ik_relay        dpp_teach
pose_to_twist_node     jtc_servo_relay        dpp_playback
fake_joint_state_publisher   distance_speed_scaler   dpp_orchestrate
fake_cloud_publisher   jtc_egm_stub           dpp_gui
```

## 3. Simulation path (no robot, no headset)

This proves the whole motion core on one machine. The fake joint-state publisher closes
the loop by integrating `/servo_node/commands` back into `/joint_states`.

### 3a. IK loop

```bash
docker run -it --net host -v "$PWD/ros2:/opt/metamove_ws/src" metamove/ros2:jazzy bash
# inside the container:
source /opt/ros/jazzy/setup.bash && source /opt/metamove_ws/install/setup.bash
ros2 launch metamove_bridge metamove_sim_ik.launch.py
```

This launch starts, in order: `robot_state_publisher` (50 Hz), `move_group` (provides
`/compute_ik`), `rosbridge_websocket` (`:9090`), `ros_tcp_endpoint` (`:10000`),
`fake_joint_state_publisher`, and `moveit_ik_relay`.

Drive it without Unity by publishing a target pose:

```bash
ros2 topic pub -r 50 /metamove/ik_target geometry_msgs/PoseStamped \
  '{header: {frame_id: "base_link"}, pose: {position: {x: 0.4, y: 0.0, z: 0.5},
    orientation: {x: 0, y: 1, z: 0, w: 0}}}'
ros2 topic echo /servo_node/commands     # watch joint commands appear
```

`moveit_ik_relay` (node `moveit_ik_relay`, 50 Hz) gates on target freshness
(`target_timeout=0.3`) and seed freshness (`joint_state_timeout=0.5`), seeds
`/compute_ik` (`group_name="manipulator"`, 50 ms IK timeout, `avoid_collisions=True`) from
`/joint_states`, slew-limits each joint by `max_joint_speed=0.3` rad/s, and publishes
`/servo_node/commands` (`std_msgs/Float64MultiArray`, joints in rad).

### 3b. Waypoint playback + distance speed scaling

```bash
ros2 launch metamove_bridge metamove_sim_playback.launch.py
```

This starts `jtc_servo_relay` (rclpy node name **`joint_trajectory_controller`**,
`time_scale=2.0`, `rate_hz=50`, `live_speed=1.0`) and `dpp_playback`
(`velocity_scaling=0.5`, `acceleration_scaling=0.5`, `dwell_seconds=0.5`,
`waypoints_file=.../dpp_waypoints.yaml`). Teach waypoints into the YAML with
`ros2 run metamove_bridge dpp_teach`; each entry is `{joints: [j1..j6]}` in radians.

Add the safety layer:

```bash
ros2 run metamove_bridge distance_speed_scaler
# simulate an approaching human:
ros2 topic pub -r 20 /quest/min_distance std_msgs/Float32 '{data: 0.5}'   # < d_near -> freeze
ros2 topic pub -r 20 /quest/min_distance std_msgs/Float32 '{data: 1.3}'   # mid-band -> partial
ros2 topic echo /robot/speed_factor
```

`distance_speed_scaler` (node `distance_speed_scaler`, 20 Hz) maps distance through the
band `d_near=0.6 .. d_far=2.0`, smooths it (`ema_alpha=0.3`), ramps up at `up_rate=0.6`/s,
drops instantly, and writes the `live_speed` parameter of `joint_trajectory_controller`
via `/joint_trajectory_controller/set_parameters`. It also pauses/resumes `dpp_playback`
on stale distance (`stale_timeout=1.5`).

### 3c. Servo-twist backend (alternative to 3a)

If you prefer velocity control through MoveIt Servo, run `pose_to_twist_node` (node
`pose_to_twist`) instead of the IK relay. It reads `/metamove/ik_target`, looks up
`base_link -> tool0` via TF, applies `linear_gain=1.5` / `angular_gain=1.5` with clamps
`max_linear=0.25` m/s, `max_angular=1.0` rad/s, deadbands `0.002` m / `0.01` rad
(`track_orientation=False` by default), and publishes
`/servo_node/delta_twist_cmds` (`geometry_msgs/TwistStamped`, 100 Hz).

## 4. Unity Quest client

### 4a. Project setup

Create a Unity **6000.4.0f1** project and add (see `unity-quest/Packages/manifest.json`):

| Package | Version |
|---------|---------|
| `com.meta.xr.sdk.all` | 85.0.0 |
| `com.unity.robotics.ros-tcp-connector` | git `main` |
| `com.unity.robotics.urdf-importer` | git `main` |
| `com.unity.xr.interaction.toolkit` | 3.4.1 |
| `com.unity.xr.hands` | 1.7.3 |
| `com.unity.render-pipelines.universal` | 17.4.0 |

Open `Assets/MetaMove/Scenes/Scene_Robot.unity` (the teleop scene). `Hud.unity` is the
standalone HUD; `Scene_QRTest.unity` is QR-anchor calibration.

### 4b. ROS connection

Add a `RosBridgeBootstrap` GameObject (`Assets/MetaMove/Scripts/Robot/Ros/RosBridgeBootstrap.cs`).
Set `rosIPAddress` to the host running `ros_tcp_endpoint` (lab default `192.168.125.99`,
or `localhost` when the editor runs on the same PC) and `rosPort = 10000`.

### 4c. Wire the feature scripts (Inspector)

`IKTargetPosePublisher` — `topic="/metamove/ik_target"`, `frameId="base_link"`,
`publishHz=50`, `onlyWhenGrabbed=true`; assign `target` (the IK target ball) and
`robotBase` (the QR-anchored `base_link`). Publishes `geometry_msgs/PoseStamped` after
converting Unity left-handed Y-up to ROS FLU.

`PhantomGrabRelay` (on the end-effector sphere) — assign `grabbable`, `ikTarget`, and a
hand `handAnchor`; `dragGain=1.0`, `maxReachM=0.5`.

`SafetyHud` — assign `robotBase` and `humanPoints` (defaults to `Camera.main`); `useRos=true`,
`distanceTopic="/quest/min_distance"`, `speedTopic="/robot/speed_factor"`, `publishHz=20`,
thresholds `dangerDist=0.6`, `warnDist=1.2`, `speedDistNear=0.6`, `speedDistFar=2.0`.

`ScalingModeToggle` — `topic="/quest/scaling_enabled"`, `scalingEnabled=true`,
`vrButton=OVRInput.Button.Two`; publishes `std_msgs/Bool` on a 2 s heartbeat.

### 4d. Build & deploy

Build to Android and install on the Quest:

```bash
adb install -r MetaMove.apk
```

Use a **USB-A -> USB-C** cable for `adb`, not USB-C -> USB-C: USB-PD negotiation can brown
out / crash the laptop on the 130 W supply.

## 5. Real-robot path

```{warning}
Do every new RAPID/EGM step on a RobotStudio Virtual Controller first, then the real robot.
Keep acceleration conservative during bring-up. The robot moves under external command — be
ready on the E-Stop.
```

### 5a. RAPID module

Load the joint-streaming module (`robotstudio/rapid/MetaMoveJointStreamFix.mod`) and make
it the active task with the PP at its main routine. Its EGM cycle is:

```text
EGMSetupUC ROB_1, egmId, "default", "ROB_1" \Joint;
EGMActJoint egmId
    \Tool:=tool0
    \J1:=egmMM \J2:=egmMM \J3:=egmMM
    \J4:=egmMM \J5:=egmMM \J6:=egmMM
    \LpFilter:=20 \SampleRate:=4
    \MaxSpeedDeviation:=1000;
EGMRunJoint egmId, EGM_STOP_HOLD
    \J1 \J2 \J3 \J4 \J5 \J6
    \CondTime:=600 \RampInTime:=0.05;
```

```{note}
Do **not** pass `\PosCorrGain` to `EGMActJoint` on RW 7.20 — it raises elog 40160. Joint
corrections need no gain. `MaxSpeedDeviation` here is the dominant speed cap and overrides
the ROS-side `live_speed`.
```

### 5b. Controller UDPUC device

Configure the EGM unicast device so its `RemoteAddress` is the **bridge's** IP and the port
matches (`6511` joint / `6515` pose):

```text
UDPUC:
  -Name "ROB_1" -Type "UDPUC" \
  -RemoteAddress "192.168.125.150" \
  -RemotePortNumber 6511 -LocalPortNumber 6511
```

Set this on the FlexPendant (the default RWS user cannot write CFG). The single most common
failure is a **source-IP mismatch**: the controller silently drops any correction packet
whose source IP is not the configured `RemoteAddress`.

### 5c. Windows EGM bridge

```powershell
cd bridge\egm-bridge
python -m venv .venv ; .\.venv\Scripts\Activate.ps1
pip install roslibpy numpy
# BIND to the robot-subnet IP — never 0.0.0.0 on a multi-homed host:
python egm_bridge_servo.py --host 192.168.125.150 --port 6511 --rosbridge-host 192.168.125.99
```

The bridge (`egm_bridge_servo.py`) opens a UDP socket bound to `--host:--port`, parses the
controller's `EgmRobot` feedback (joints in degrees, ~250 Hz), publishes `/joint_states`
(deg->rad) at ~50 Hz, subscribes `/servo_node/commands` (rad) over rosbridge, converts
rad->deg, and sends `EgmSensor` correction packets (`MSGTYPE_CORRECTION`). If no command
arrives within 0.5 s it echoes feedback (identity mode) to keep the EGM session alive.

```{note}
If you run the bridge inside a container under WSL2 with mirrored networking, inbound UDP
may never reach the container namespace. Use a **macvlan** network so the container holds a
real address on the robot subnet (e.g. `192.168.125.150/24`), or run the bridge natively on
Windows.
```

### 5d. Activate servo & verify

```powershell
python activate_servo.py --rosbridge-host 192.168.125.99   # start_servo + TWIST mode
```

Verification checklist:

- Bridge stdout shows ~250 packets/s received from the controller.
- `ros2 topic hz /joint_states` ~ 50 Hz.
- `ros2 topic echo /servo_node/status` is nominal (no collision/singularity stop).
- Publishing `/metamove/ik_target` (or jogging in the Quest) moves the robot.
- Walking toward the robot reduces `/robot/speed_factor` and freezes motion; retreating
  ramps it back up.

## 6. Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Robot ignores EGM corrections | Bridge sends from wrong source IP | Bind the socket to the UDPUC `RemoteAddress`; never `0.0.0.0`. |
| `elog 40160` on EGMActJoint | `\PosCorrGain` used in joint mode on RW 7.20 | Remove it — joint corrections take no gain. |
| Container receives 0 UDP packets | WSL2 mirrored-networking bug | Use a macvlan network or run the bridge on Windows. |
| IK relay never commands | No fresh `/joint_states` seed | Start the bridge / fake JSP first; check `joint_state_timeout`. |
| Robot jumps at start | Singular seed | Seed near `[0, 0, -0.785, 0, -0.785, 0]` (the fake JSP default). |
| Speed never reaches 100% | Distance below `d_far`, or `MaxSpeedDeviation` cap | Move past 2.0 m; raise the RAPID `MaxSpeedDeviation` cap. |
| `adb` crashes the laptop | USB-PD draw over USB-C | Use a USB-A -> USB-C cable. |
```
