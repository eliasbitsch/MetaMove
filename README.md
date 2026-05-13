# MetaMove

Mixed-reality teleoperation and digital twin for the **ABB GoFa CRB 15000** collaborative robot, driven from a **Meta Quest 3** through hand tracking, with **ROS 2 Jazzy + MoveIt Servo** providing inverse kinematics, collision checking, and planning.

The robot listens on **EGM (Externally Guided Motion)** at 250 Hz. Quest sends pose targets over the LAN, ROS computes the joint commands, a Windows-native Unity bridge translates them to the EGM wire format, and the controller follows. RAPID stays minimal — one mode, one loop.

---

## Architecture

```
  ┌───────────────────────────────────────────────────┐
  │  Meta Quest 3 (Unity AR build)                    │
  │    Hand tracking · Passthrough · Spatial Anchors  │
  └────────────┬──────────────────────────────────────┘
               │ ros-tcp-connector  (PoseStamped / TwistStamped)
               ▼
  ┌───────────────────────────────────────────────────┐
  │  ROS 2 Jazzy   (Docker, in WSL2)                  │
  │    move_group · moveit_servo · rosbridge · rviz   │
  │    inverse kinematics + collision + singularity   │
  └────────────┬──────────────────────────────────────┘
               │ ros-tcp-connector  (Float64MultiArray joint targets)
               ▼
  ┌───────────────────────────────────────────────────┐
  │  Unity Bridge  (Windows native, headless build)   │
  │    EgmClient · JointStatePublisher · ServoSub.    │
  └────────────┬──────────────────────────────────────┘
               │ EGM UDP @ 250 Hz   (protobuf, EgmSensor)
               ▼
  ┌───────────────────────────────────────────────────┐
  │  ABB OmniCore Controller (RW 7.20, real or VC)    │
  │    MetaMoveCore.mod:  EGMActJoint + EGMRunJoint   │
  └───────────────────────────────────────────────────┘
```

Design choices, with rationale:

- **No mode switching in RAPID.** The robot stays in joint-EGM forever. All IK is solved client-side by MoveIt Servo. This avoids the EGMStop / EGMReset / EGMActX dance and the configuration-jump that plagues pose-mode teleop.
- **Hot path stays off WSL.** WSL2 mirrored networking drops UDP from real hardware. The 250 Hz EGM loop runs as a native Windows process (or a Linux server build). Only TCP traffic crosses into WSL.
- **ROS is the brain, Unity is the courier.** Unity owns the AR rendering, hand input, EGM wire format, and last-mile sensor stream. ROS owns planning, IK, monitoring, and rosbag logging.
- **One Unity project, two builds.** Quest3 APK (AR client) and Windows / Linux standalone (headless bridge) share the same C# code through assembly definitions.

---

## Repository layout

| Path | What lives here |
|---|---|
| `unity-quest/` | Unity 6 project: Quest3 AR app, EGM bridge components, MR scene |
| `ros2/` | ROS 2 Jazzy workspace, docker stack, MoveIt config, Servo launch |
| `robotstudio/` | RAPID modules, RobotStudio station, controller backups, helper scripts |
| `ai-services/` | Standalone services — EGM bridge (Python), Gemma VLM, Qwen3 TTS, Jarvis dialog |
| `docs/` | AR markers, documentation, drawings |

---

## Components

### Unity (`unity-quest/`)

Unity 6 project targeting the Quest 3 with Meta XR SDK + URDF Importer + ros-tcp-connector. Hand tracking and passthrough are first-class.

**EGM stack** lives in `Assets/MetaMove/Scripts/Robot/EGM/`:
- `EgmClient.cs` — UDP socket, background RX thread, joint and pose send paths
- `EgmMessages.cs` — minimal protobuf encoder/decoder for `egm.proto`
- `EgmRobotSink.cs` — adapts the client to the gesture pipeline

**ROS bridge** lives in `Assets/MetaMove/Scripts/Robot/Ros/`:
- `RosBridgeBootstrap.cs` — configures the `ROSConnection` singleton
- `JointStatePublisher.cs` — EGM feedback (deg) → `/joint_states` (rad, 50 Hz)
- `ServoCommandSubscriber.cs` — `/servo_node/commands` → `EgmClient.SendJoints` (250 Hz)

Drop the `MetaMoveRosBridge` GameObject in your scene (already present in `Scene_Robot.unity`).

### ROS 2 (`ros2/`)

Docker image based on `ros:jazzy-ros-base` with MoveIt 2, RViz, ros2-control, rosbridge suite, and the Unity ROS-TCP endpoint pre-built into `/opt/ros2_extras`.

Launch the full stack:

```bash
cd ros2
docker compose run --rm ros2 \
  ros2 launch /opt/metamove_ws/src/abb_gofa_custom/abb_crb15000_moveit/launch/metamove_servo.launch.py
```

This starts:
- `move_group` with the GoFa SRDF and OMPL planning
- `servo_node` with `servo_crb15000.yaml` (twist + joint + pose command modes)
- `rosbridge_websocket` on `:9090` (for roslibpy clients)
- `ros_tcp_endpoint` on `:10000` (for Unity)
- `rviz2` with MoveIt motion planning

### RAPID (`robotstudio/`)

Two modules cover the controller side:

- **`MetaMoveJointStream.mod`** — bare-minimum continuous joint EGM. `EGMSetupUC` + `EGMActJoint` + `EGMRunJoint` with `MaxSpeedDeviation:=1000` and a long `CondTime`. Drop in via RWS, point the production entry point at `main`, hit play.
- **`MetaMoveCore.mod`** — slightly richer dispatcher with pose-mode and joint-mode case branches. Useful when developing against the StateMachine Add-In v2.0.

`ROB_1_udpuc.cfg` is the SIO configuration that registers the UDPUC device pointing at the Unity bridge.

### EGM bridge (`ai-services/egm-bridge/`)

Reference Python bridge used during bring-up before the Unity bridge was finished. Two flavors:

- `egm_bridge_identity.py` — receives `EgmRobot`, echoes the current joints back so the EGM session stays alive without moving the robot.
- `egm_bridge_servo.py` — same plus `rosbridge_websocket` client that publishes `/joint_states` and subscribes to `/servo_node/commands`. Useful when running headless without Unity.

`Dockerfile` + `docker-compose.yml` ship a macvlan-networked container so the bridge appears as its own device on the lab LAN — sidesteps WSL2's UDP-receive bug entirely.

### AI services (`ai-services/`)

Local-first stack used by the Quest app for voice control, scene understanding, and assistive narration. None of these are required for raw teleop but they enable the higher-level demos:

- **`llm-gemma/`** — Gemma 3 VLM behind an HTTP API, fronted by a small Jarvis-style dialog manager
- **`tts-qwen3/`** — Qwen3-TTS with FX presets (helmet, ceiling speaker, neutral) for sub-second TTFA voice output
- **`whisper/`** — local ASR for voice control

---

## Quickstart

This assumes the lab network is `192.168.125.0/24` with the GoFa controller at `192.168.125.1` and a workstation reachable on `192.168.125.99`.

### 1. ROS stack (Linux / WSL)

```bash
cd ros2
docker compose run --rm ros2 \
  ros2 launch /opt/metamove_ws/src/abb_gofa_custom/abb_crb15000_moveit/launch/metamove_servo.launch.py
```

Wait for `Rosbridge WebSocket server started on port 9090` and `ROS-TCP Server` to come up.

### 2. RAPID side

Load `robotstudio/rapid/MetaMoveJointStream.mod` onto the controller via RWS or the FlexPendant, load `ROB_1_udpuc.cfg`, warm-restart, set program pointer to `main`, motors on, play.

### 3. Unity bridge

Open `unity-quest/` in Unity 6. Open `Scenes/Scene_Robot.unity`. On the `MetaMoveRosBridge` GameObject set:
- `RosBridgeBootstrap.rosIPAddress` to your ROS host
- `EgmClient.listenPort` to `6511`

Hit play.

### 4. Quest 3 deployment

Switch the build target to Android, build the AR scene as an APK, install on the Quest. The same scripts run there — the headset publishes its hand pose into ROS and the Unity bridge on the workstation does the EGM translation. When the headset goes offline the bridge holds the last pose and the robot stops safely.

---

## Hardware

- ABB GoFa CRB 15000-5/0.95 with OmniCore controller running RobotWare 7.20 + the EGM (3124-1) option
- Meta Quest 3 (Quest 2 also works with reduced fidelity)
- Windows 10/11 PC for RobotStudio + Unity bridge build
- Optional Linux PC for hosting the headless Unity bridge and ROS in a single box

## Software

- Unity 6 with Meta XR SDK and URDF Importer
- RobotStudio 2024 with the matching RobotWare 7.20 system image
- Docker Desktop or native Docker in WSL2 Ubuntu for the ROS image
- Python 3.12 for the standalone bridges and AI services

---

## Status

- EGM joint streaming proven on the real GoFa at 250 Hz with sub-millisecond bridge RTT
- MoveIt Servo end-to-end on real hardware with cartesian, joint, and pose command modes
- Unity bridge components committed and compiling; full end-to-end VC + ROS + Quest dry run is the next milestone
- Six pick-and-place demo scenarios scoped (chess, stone sort, framing, mug, pins, big stone)

## Acknowledgements

Architecture inspired by [rparak/Unity3D_ABB_CRB_15000_GoFa_EGM](https://github.com/rparak/Unity3D_ABB_CRB_15000_GoFa_EGM), the PickNik [abb_ros2](https://github.com/PickNikRobotics/abb_ros2) driver, and Jakob Hörbst's original GoHolo RAPID modules. EGM protobuf schema from ABB's RobotWare distribution.
