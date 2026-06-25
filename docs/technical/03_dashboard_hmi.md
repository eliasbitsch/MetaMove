# Dashboard and HMI

MetaMove exposes three operator surfaces: a **web dashboard** for monitoring and
supervisory control, an **in-headset HMI** for immersive in-world control, and a set of
**operator console** scripts for scripted or recovery operations. All three talk to the
same ROS 2 graph.

## Web dashboard (MRE2-GOFA_Dashboard)

```{note}
The web dashboard is maintained as a separate component (git submodule
`MRE2-GOFA_Dashboard`). It is summarised here because it is part of the operating system
of MetaMove.
```

### Technology stack

- **Backend** — FastAPI (Python) joining the ROS 2 graph through `rclpy`. It exposes a
  REST API plus a WebSocket stream, drives `FollowJointTrajectory` actions and MoveIt
  Servo twist commands, and persists history to PostgreSQL (or SQLite).
- **Frontend** — HTML/JS/CSS. `index.html` is the monitoring dashboard;
  `hmi.html` is a tablet-oriented GoFa control surface with a tiled layout. A WebSocket
  client streams live data.
- **Deployment** — containerised via `deploy/dashboard/dashboard.Dockerfile`; a slim image
  that joins an existing ROS 2 graph. Served on `:8080` (HTTP) or behind Nginx on `:8443`.

### What it shows and controls

| Surface | Capability |
|---------|------------|
| Live dashboard (`/`) | Joint states, TCP pose, EGM state, 3D digital twin. |
| GoFa HMI (`/hmi`) | Tiled control: speed, HRC safety, status, maintenance. Axial jog (J1–J6) via hold-to-press; linear/TCP jog via MoveIt Servo; speed gauges and payload; safety zones; maintenance trends, event timeline, toasts; developer view (topic freshness, packet flow, JSON preview). |

### Connection to the robot

The backend subscribes to `/joint_states`, `/tf`, `/diagnostics`, and `/egm/*`, and sends
commands via the `FollowJointTrajectory` action and `/servo_node/delta_twist_cmds`. It
auto-discovers ROS topics at runtime and supports both EGM (UDP) and RWS (HTTPS) backends.

Representative API endpoints:

```text
GET  /api/snapshot
GET  /api/history/summary?window=1h|24h|7d|30d|90d
GET  /api/history/series
POST /api/ingest
GET  /api/hmi/state
POST /api/hmi/jog/start    * /api/hmi/jog/heartbeat    * /api/hmi/jog/stop
POST /api/hmi/tcp/start    * /api/hmi/tcp/heartbeat
POST /api/hmi/home
```

The jog/TCP endpoints use a **start → heartbeat → stop** pattern: motion continues only
while heartbeats arrive, so a dropped connection stops the robot (dead-man behaviour).

## In-headset HMI (Unity Quest 3)

The immersive HMI lives in `unity-quest/Assets/MetaMove/Scripts/UI/`, with `Hud.unity` as
the main scene. It is organised in three layers:

1. **L1 — Home**: a hand-anchored radial menu (palm-up gesture) with eight wedges for
   quick actions (Status, Paths, Position, Safety, Ghost toggle, Envelope toggle, HUD
   toggle, Robot info).
2. **L2 — Floating panels**: world-positioned panels spawned on demand.
3. **L3 — Physical fixtures**: permanent 3D objects (E-Stop mushroom, pedestal, floor
   grid).

### Core UI scripts

| Script | Purpose |
|--------|---------|
| `NearTouchButton.cs` | Pinch / finger-near button with dwell-based firing and visual feedback. |
| `StatusHud.cs` | Curved status HUD (lazy-follow, yaw-only) — battery, link Hz, motors, passthrough. |
| `MainDashboardPanel.cs` | Hub with tabs (Status / Control / Path / Safety / Motors / Body / Voice / System) and a persistent E-Stop row. |
| `FlexPendantPanel.cs` | ABB-style teach-pendant emulation: resizable, wrist-attachable, 12 jog buttons (6 axes × 2), E-Stop. |
| `TelemetryPanel.cs` | Six joint gauges, TCP XYZ / RPY readouts, Hz label. |
| `ConnectionPanel.cs` | IP / port / mode selection (Real / Virtual / Offline), status LEDs (EGM / RWS / ROS / MoveIt), latency. |
| `SafetyPanel.cs` | Zone toggles, speed-cap slider, separation distance, ISO mode, E-Stop, stop-reason log. |
| `PathsPanel.cs` | Waypoint list, path dropdown, add/clear/run, speed slider, loop toggle. |
| `PrecisePositionPanel.cs` | Six XYZ + RPY sliders with a numpad popup for exact entry. |
| `PanelManager.cs` | Singleton spawner; registers panels by ID and opens/closes on demand. |
| `UiModeController.cs` | Switches between Minimal, Control-Center, and FlexPendant UI modes. |

In-world overlays (`ShowPose`, `ShowAngles`, `ShowTorque`, `PathPreviewRenderer`,
`AxisGizmo`, `DistanceRuler`, …) annotate the robot directly in space.

### UI modes

1. **Minimal (default)** — radial menu + on-demand mini-panels + in-world overlays + HUD.
2. **3-panel control center** — three permanent panels around the user (Telemetry, Paths, Safety).
3. **FlexPendant** — a single resizable ABB-style teach pendant (two-hand corner pinch to resize).

All panels use the Meta UI Set components, are grab-translatable (no rotation), and
re-orient to the camera on spawn. The curved HUD sits at ~60 cm with status LEDs, TCP
pose, link Hz, a mode badge, mini torque bars, and safety status.

## Operator console tools (bridge/egm-bridge)

Command-line tools that connect to `rosbridge` (WebSocket, `:9090`) via `roslibpy`. They
are Windows-friendly (use `msvcrt` for key input) and intended for testing, lab runs, and
recovery.

| Tool | Purpose |
|------|---------|
| `control_console.py` | Unified speed control: toggle **MANUAL** (keys 0–9 / `f` freeze) vs **QUEST** mode (distance-based scaling owns `live_speed`). |
| `teleop_console.py` | Keyboard teleop for MoveIt Servo: `w/s/a/d/q/e` linear, `i/k/j/l/u/o` angular, `1–9` speed; publishes `/servo_node/delta_twist_cmds`. |
| `playback_console.py` | Teach-playback control: `p` pause, `r` resume, `1–9/0` speed, space = instant stop; calls `/dpp_playback/pause` and `/resume`. |
| `relay_speed_console.py` | Direct immediate `live_speed` control (`0`/space freeze, `1–9` %, `f` full) for mid-motion adjustment; also home/pause/resume. |
| `rescue_jog.py` | Free the robot from servo collision/singularity braking: `--axis 1-6 --deg ±20` publishes ramped joint steps to `/servo_node/commands`, limited to ±20°/invocation. |
| `activate_servo.py` | Robust MoveIt Servo activation: calls `/servo_node/start_servo`, switches to TWIST command type, monitors `/servo_node/status` (avoids DDS discovery issues). |
| `inject_command.py` | Publishes a raw `Float64MultiArray` to `/servo_node/commands` for testing. |
| `dpp_orchestrate_win.py` | Dynamic-measurement orchestration (teach / playback cycles) on Windows. |
| `egm_bridge_servo.py` | Core EGM/UDP bridge for joint/pose teleoperation. |
| `rws2_loadmod.py`, `rws2_fix_module.py` | RWS utilities to load / fix RAPID modules. |

## Design notes

- The three surfaces are **views onto one ROS graph**, not separate control stacks — the
  dashboard, the headset HMI, and the consoles all read the same telemetry and write to
  the same command topics.
- Jog/TCP control uses a **heartbeat / dead-man** pattern: motion only continues while
  fresh heartbeats arrive, so a lost connection stops the robot.
- `rescue_jog.py` deliberately bypasses servo velocity scaling and is tightly bounded
  (±20° per call) so it can recover a braked robot without becoming a back door around the
  safety layer.
