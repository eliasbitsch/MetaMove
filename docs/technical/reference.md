# Reference

Consolidated reference for the ROS topics, parameters, and tunables described in the
subsystem chapters.

## ROS topics

| Topic | Message | Direction | Notes |
|-------|---------|-----------|-------|
| `/quest/min_distance` | `std_msgs/Float32` | Quest → ROS | Nearest human-to-robot distance (m), ~20 Hz. |
| `/quest/scaling_enabled` | `std_msgs/Bool` | Quest → ROS | AUTO/MANUAL toggle for the speed scaler. |
| `/robot/speed_factor` | `std_msgs/Float32` | ROS → Quest | Heartbeat of the applied speed factor (0..1). |
| `/metamove/ik_target` | `geometry_msgs/PoseStamped` | Quest → ROS | IK target TCP pose in `base_link`, 50 Hz, gated by grab. |
| `/servo_node/commands` | `std_msgs/Float64MultiArray` | ROS → bridge | Six joint positions (rad). |
| `/servo_node/delta_twist_cmds` | `geometry_msgs/TwistStamped` | ROS → Servo | Cartesian velocity command. |
| `/joint_states` | `sensor_msgs/JointState` | bridge → ROS | Live robot state; seed for IK and slew base. |
| `/diagnostics`, `/tf`, `/egm/*` | — | bridge/MoveIt → dashboard | Telemetry consumed by the dashboard. |

## ROS parameters

| Node | Parameter | Default | Meaning |
|------|-----------|---------|---------|
| `distance_speed_scaler` | `d_near` | 0.6 m | Freeze threshold. |
| `distance_speed_scaler` | `d_far` | 2.0 m | Full-speed threshold. |
| `distance_speed_scaler` | `ema_alpha` | 0.3 | EMA low-pass strength. |
| `distance_speed_scaler` | `up_rate` | 0.6 /s | Maximum ramp-up rate. |
| `distance_speed_scaler` | `stale_timeout` | 1.5 s | Freeze/pause on stale distance. |
| `jtc_servo_relay` | `live_speed` | 1.0 | Continuous speed multiplier (0..1). |
| `moveit_ik_relay` | `target_timeout` | 0.3 s | Drop stale grab targets. |
| `moveit_ik_relay` | `joint_state_timeout` | 0.5 s | Refuse to command without fresh seed. |
| `moveit_ik_relay` | `max_joint_speed` | 0.3 rad/s | Per-joint slew clamp. |
| `pose_to_twist_node` | `linear_gain` | 1.5 /s | Position P-gain. |
| `pose_to_twist_node` | `angular_gain` | 1.5 /s | Orientation P-gain. |
| `pose_to_twist_node` | `max_linear` | 0.25 m/s | Linear velocity clamp. |
| `pose_to_twist_node` | `max_angular` | 1.0 rad/s | Angular velocity clamp. |

## Network ports

| Port | Protocol | Use |
|------|----------|-----|
| 6511 | UDP | EGM joint path (`UDPUC`). |
| 6515 | UDP | EGM pose path (`ROB_1`). |
| 9090 | WebSocket | `rosbridge` (operator consoles, dashboard). |
| 10000 | TCP | ROS-TCP-Endpoint (Quest client). |
| 443 | HTTPS | RWS (controller web services). |
| 8080 / 8443 | HTTP/HTTPS | Web dashboard. |

## Glossary

EGM
: Externally Guided Motion — ABB's low-latency external motion interface over UDP.

RWS
: Robot Web Services — the controller's HTTP/REST interface.

UDPUC
: UDP Unicast Communication device on the controller used by EGM; binds to a specific
  remote address/port.

TCP (robotics)
: Tool Center Point — the controlled point at the robot's end-effector (distinct from TCP
  the network protocol).

FLU
: Forward-Left-Up — the ROS right-handed Z-up convention; Unity is left-handed Y-up, so a
  frame conversion is applied when publishing poses.

Servo
: MoveIt Servo — real-time Cartesian/joint jogging with collision and singularity handling.
