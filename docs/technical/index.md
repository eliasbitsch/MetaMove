# MetaMove — Technical Documentation

MetaMove is a mixed-reality teleoperation system for an **ABB GoFa CRB 15000**
collaborative robot. An operator wearing a **Meta Quest 3** headset sees the real
robot through passthrough, controls its end-effector with bare-hand gestures, and
is protected by a distance-based speed-scaling safety layer. A web dashboard and an
in-headset HMI provide monitoring and supervisory control.

This document describes the three core subsystems:

1. **Distance-Based Speed Scaling** — the robot slows down and freezes as a human
   approaches, and ramps back up smoothly when they retreat.
2. **Pinch-and-Move End-Effector Control** — the operator pinches a virtual handle
   on the robot flange and drags it through space; the motion is solved to joint
   commands and streamed to the controller.
3. **Dashboard and HMI** — a FastAPI/WebSocket web dashboard plus a layered Unity
   in-headset interface, and a set of operator console tools.

```{toctree}
:maxdepth: 2
:caption: Contents

installation
reproduce
01_distance_speed_scaling
02_pinch_move_teleop
03_dashboard_hmi
reference
```

## System overview

```text
            +--------------------------+
            |   Meta Quest 3 (Unity)   |
            |  hand tracking * HMI *   |
            |  passthrough * safety    |
            +------------+-------------+
                         | ROS-TCP-Connector (TCP 10000)
                         | /metamove/ik_target  * /quest/min_distance
                         v
            +--------------------------+        +-----------------------+
            |   ROS 2 (Jazzy, Docker)  |<------>|   Web Dashboard / HMI |
            |  moveit_ik_relay         |  9090  |  FastAPI + WebSocket  |
            |  distance_speed_scaler   | rosbri |  (MRE2-GOFA_Dashboard)|
            |  jtc_servo_relay         |  dge   +-----------------------+
            |  MoveIt 2 * /compute_ik  |
            +------------+-------------+
                         | /servo_node/commands  (Float64MultiArray, joints rad)
                         v
            +--------------------------+
            |   EGM bridge (Windows)    |
            |  UDP :6511 / :6515        |
            +------------+-------------+
                         | EGM (UDP, 250 Hz)
                         v
            +--------------------------+
            |   ABB GoFa CRB 15000      |
            |  RobotWare 7.x * EGM      |
            +--------------------------+
```

The architecture is intentionally **dual-path**:

- **Real robot** — Quest -> ROS 2 -> EGM bridge (Windows) -> GoFa controller over EGM/UDP.
- **Virtual controller / simulation** — the same ROS 2 graph drives a RobotStudio
  Virtual Controller via RWS where EGM/UDP is not available on the same host.

## Repository layout

| Path | Contents |
|------|----------|
| `unity-quest/` | Unity project for the Quest 3 client (hand tracking, HMI, safety, ROS publishers). |
| `ros2/docker/metamove_bridge/` | ROS 2 Python package: IK relay, speed scaler, trajectory relay, playback. |
| `bridge/egm-bridge/` | Windows EGM/UDP bridge and operator console tools. |
| `MRE2-GOFA_Dashboard/` | Web dashboard (FastAPI backend + HTML/JS frontend). |
| `deploy/dashboard/` | Container deployment glue for the dashboard. |
| `robotstudio/` | RAPID modules and RobotStudio station artifacts. |
| `docs/` | Architecture notes and this technical documentation. |

## How to read this document

Each subsystem chapter follows the same structure: **purpose -> data flow -> key
files -> parameters -> notes**. Topic names, message types, and tunable parameters are
listed verbatim so they can be cross-referenced against the running system. The
{doc}`installation` chapter covers hardware/software requirements and bring-up; the
{doc}`reference` chapter consolidates all ROS topics and parameters.

## Team

MetaMove is developed by:

- **Elias Bitsch**
- **Philip Stix**
- **Viktoriia Ovdiienko**
