"""MetaMove on the REAL GoFa - everything ROS-side in one launch, self-restarting.

  Quest (ROS-TCP :10000) -> distance_speed_scaler -> jtc_servo_relay.live_speed
  dpp_playback -> MoveGroup -> jtc_servo_relay -> /servo_node/commands
  preview_confirm (MANUAL: ghost preview, OK) --------^  (moveit_ik_relay: legacy live grab, idle)
  /servo_node/commands -> rosbridge :9090 -> Windows EGM bridge -> GoFa
  GoFa -> EGM bridge -> /joint_states -> joint_feedback_relay -> Quest twin
  robot_status: why the robot is (not) moving -> console + HUD

Unlike metamove_sim_playback.launch.py there is NO fake joint state publisher:
/joint_states comes only from the EGM bridge (two sources trip the robot).
Every node respawns after a crash. All tunables live in config/robot_profile.yaml.

Started by docker compose (service "robot") via tools/metamove_up.ps1, or by hand:
  ros2 launch metamove_bridge metamove_real.launch.py
"""
from launch import LaunchDescription
from launch_ros.actions import Node
from moveit_configs_utils import MoveItConfigsBuilder

PROFILE = "/opt/metamove_ws/src/metamove_bridge/config/robot_profile.yaml"
RESPAWN = {"respawn": True, "respawn_delay": 2.0, "output": "screen"}


def generate_launch_description():
    moveit_config = (
        MoveItConfigsBuilder("abb_crb15000_5_95", package_name="abb_crb15000_moveit")
        .robot_description(file_path="config/abb_crb15000_5_95.urdf.xacro")
        .robot_description_semantic(file_path="config/abb_crb15000_5_95.srdf")
        .trajectory_execution(file_path="config/moveit_controllers.yaml")
        .planning_pipelines(pipelines=["ompl"])
        .to_moveit_configs()
    )

    def ours(executable, name=None):
        return Node(package="metamove_bridge", executable=executable, name=name or executable,
                    parameters=[PROFILE], **RESPAWN)

    return LaunchDescription([
        Node(package="robot_state_publisher", executable="robot_state_publisher",
             parameters=[moveit_config.robot_description, {"publish_frequency": 50.0}], **RESPAWN),
        Node(package="moveit_ros_move_group", executable="move_group",
             parameters=[moveit_config.to_dict()], **RESPAWN),
        Node(package="rosbridge_server", executable="rosbridge_websocket",
             parameters=[{"port": 9090, "address": "0.0.0.0"}], **RESPAWN),
        Node(package="ros_tcp_endpoint", executable="default_server_endpoint", name="ros_tcp_endpoint",
             parameters=[{"ROS_IP": "0.0.0.0", "ROS_TCP_PORT": 10000}], **RESPAWN),
        ours("jtc_servo_relay", "joint_trajectory_controller"),
        ours("dpp_playback"),
        ours("distance_speed_scaler"),
        ours("moveit_ik_relay"),
        ours("joint_feedback_relay"),
        ours("robot_status"),
        ours("preview_confirm"),
    ])
