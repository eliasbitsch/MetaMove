# Installation & Setup

This chapter covers hardware and software requirements, dependencies, and the
bring-up sequence for the full Quest → ROS 2 → GoFa pipeline. You can run individual
subsystems standalone (e.g. the dashboard against a simulated robot) — each section
notes its own prerequisites.

## Hardware requirements

| Component | Requirement |
|-----------|-------------|
| Robot | ABB GoFa CRB 15000 (5 kg / 950 mm), RobotWare 7.x with the **EGM** and **Externally Guided Motion** options. |
| Robot controller | OmniCore, reachable over Ethernet; RWS (HTTPS) enabled. |
| Headset | Meta Quest 3 (passthrough + hand tracking), developer mode enabled. |
| Workstation | Windows 11 PC on the robot LAN; dedicated wired NIC for the robot subnet. |
| Power | Use a sufficiently rated PSU for the workstation. A 130 W supply browns out under combined load — prefer a USB-A→USB-C cable for `adb` to avoid USB-PD draw on the laptop. |
| Network | Isolated robot LAN (e.g. `10.x` / a dedicated `/24`). The controller, the Windows EGM bridge, and the workstation must share that subnet. |

## Software requirements

| Layer | Software |
|-------|----------|
| ROS 2 | **Jazzy** (run in Docker on **WSL2**, not Docker Desktop). |
| Motion | MoveIt 2 (+ MoveIt Servo), `ros2_control`, `joint_trajectory_controller`. |
| Bridge | `rosbridge_suite` (WebSocket on `:9090`), ROS-TCP-Endpoint (`:10000`). |
| Unity | Unity 6 LTS with Meta XR SDK (v85), ROS-TCP-Connector. |
| EGM bridge | Python 3.11+ on Windows (`roslibpy`, `numpy`). |
| Dashboard | Python 3.11+ (`fastapi`, `uvicorn`, `rclpy`); optional PostgreSQL. |
| Docs | Python 3.10+ with `sphinx`, `myst-parser`, `furo`, `rinohtype` (this document). |

## Network configuration

The pipeline depends on a correctly configured robot subnet. Key points:

- Give the Windows EGM bridge a **fixed IP on the robot subnet** and bind the bridge
  socket to that address — **never bind to `0.0.0.0`** on a multi-homed host. The
  controller's UDP unicast device (`UDPUC`) silently discards correction packets that
  arrive from an unexpected source IP.
- Configure the controller `UDPUC` device `RemoteAddress` to the bridge IP and the
  matching port (`:6511` for joint, `:6515` for the pose/ROB_1 path).
- On WSL2, forward the rosbridge (`9090`) and ROS-TCP (`10000`) ports with
  `netsh portproxy` so the Quest can reach the ROS graph through the Windows host.
- Identify the **physical** robot NIC (not a loopback/virtual adapter) before assigning
  the static IP.

```{note}
RWS access as the default controller user is limited: writing `PERS` variables works,
but program execution, configuration changes, and file upload do not. Use the FlexPendant
for config changes, or load RAPID modules via the documented RWS 2.0 `loadmod` / PP-reset
recipe.
```

## Bring-up sequence (real robot)

1. **Network** — power the controller, verify the workstation can ping it and the
   assigned bridge IP exists on the robot NIC.
2. **RAPID** — ensure the EGM RAPID module (`MetaJointMain` / equivalent) is the active
   task and the program pointer is set to its main routine. Motors on.
3. **ROS 2** — start the Docker stack in WSL2 (MoveIt, controllers, `rosbridge`,
   ROS-TCP-Endpoint, and the `metamove_bridge` nodes).
4. **EGM bridge (Windows)** — start `egm_bridge_servo.py`, bound to the bridge IP,
   pointing at the controller `UDPUC` port. Confirm `/joint_states` goes live.
5. **MoveIt Servo** — activate servo (`activate_servo.py`) and confirm
   `/servo_node/status` is nominal.
6. **Quest app** — launch the MetaMove app; it connects via ROS-TCP-Connector. Calibrate
   the QR/world anchor so the virtual robot overlays the real one.
7. **Safety check** — verify distance scaling: walking toward the robot must reduce speed
   and freeze it; retreating must ramp it back up.

```{warning}
Always validate new RAPID/EGM features on a RobotStudio Virtual Controller first, then on
the real robot. Keep acceleration limits conservative (e.g. accel ~ 0.10) during bring-up.
```

## Subsystem-local setup

### ROS 2 bridge package

The `metamove_bridge` package (`ros2/docker/metamove_bridge/`) is a standard
`ament_python` package. Inside the ROS 2 container:

```bash
cd /ros2_ws
colcon build --packages-select metamove_bridge
source install/setup.bash
ros2 launch metamove_bridge bringup.launch.py   # or run individual nodes
```

### EGM bridge & operator consoles (Windows)

```powershell
cd bridge\egm-bridge
python -m venv .venv ; .\.venv\Scripts\Activate.ps1
pip install roslibpy numpy
python egm_bridge_servo.py --rosbridge-host <ROBOT_LAN_IP>
```

The console tools (`control_console.py`, `teleop_console.py`, `playback_console.py`,
`relay_speed_console.py`, `rescue_jog.py`, …) connect to `rosbridge` on `:9090`. See
{doc}`03_dashboard_hmi` for what each one does.

### Web dashboard

```bash
cd MRE2-GOFA_Dashboard/backend
pip install -r requirements.txt
uvicorn app:app --host 0.0.0.0 --port 8080
```

Or use the slim container under `deploy/dashboard/` to join an existing ROS 2 graph.
The dashboard auto-discovers ROS topics at runtime.

### Unity Quest client

1. Open `unity-quest/` in Unity 6 LTS with the Meta XR SDK and ROS-TCP-Connector
   installed (see `Packages/manifest.json`).
2. Set the ROS-TCP-Connector endpoint to the Windows host IP / port `10000`.
3. Build to Android (Quest), deploy with `adb install`. Use a USB-A→USB-C cable to
   avoid the USB-PD brownout issue noted above.

## Building this documentation

The documentation source lives in `docs/technical/`. To build it yourself:

```bash
cd docs/technical
pip install -r requirements.txt

# HTML (browsable)
sphinx-build -b html . _build/html

# PDF (pure-Python via rinohtype — no LaTeX toolchain required)
sphinx-build -b rinoh . _build/pdf
# -> _build/pdf/MetaMove-Technical-Documentation.pdf
```

`requirements.txt` pins `sphinx`, `myst-parser`, `furo`, and `rinohtype`.
