"""Slim servo launch — no Unity endpoints (no rosbridge_websocket, no ros_tcp_endpoint).
Use this when driving the robot via metamove_bridge (RWS) instead of Unity (EGM).

Components:
  Servo (Container) -> /servo_node/commands (Float64MultiArray joint positions)
  MoveIt move_group + RViz visualization
"""
import os
from pathlib import Path
from launch import LaunchDescription
from launch_ros.actions import Node
import xacro


def _resolve(rel: str) -> str:
    # Pkg-relative resolve so we don't depend on installed share path lookup.
    return str(Path(__file__).resolve().parent.parent / rel)


def generate_launch_description():
    pkg_dir = Path(__file__).resolve().parent.parent

    urdf_xacro = _resolve("config/abb_crb15000_5_95.urdf.xacro")
    srdf_candidates = [_resolve("config/abb_crb15000_5_95.srdf"), _resolve("config/crb15000.srdf")]
    srdf = next((p for p in srdf_candidates if os.path.exists(p)), srdf_candidates[0])
    kinematics_yaml = _resolve("config/kinematics.yaml")
    joint_limits_yaml = _resolve("config/joint_limits.yaml")
    servo_yaml = _resolve("config/servo_crb15000.yaml")
    rviz_cfg = _resolve("rviz/metamove_servo.rviz") if os.path.exists(_resolve("rviz/metamove_servo.rviz")) else ""

    robot_description = {"robot_description": xacro.process_file(urdf_xacro).toxml()}
    with open(srdf) as f:
        srdf_text = f.read()
    robot_description_semantic = {"robot_description_semantic": srdf_text}

    rsp = Node(
        package="robot_state_publisher",
        executable="robot_state_publisher",
        output="screen",
        parameters=[robot_description],
    )

    move_group = Node(
        package="moveit_ros_move_group",
        executable="move_group",
        output="screen",
        parameters=[
            robot_description,
            robot_description_semantic,
            {"robot_description_kinematics": __import__("yaml").safe_load(open(kinematics_yaml)) or {}},
            {"robot_description_planning": __import__("yaml").safe_load(open(joint_limits_yaml)) or {}},
            {"publish_planning_scene": True, "publish_geometry_updates": True,
             "publish_state_updates": True, "publish_transforms_updates": True},
        ],
    )

    _yml = __import__("yaml").safe_load(open(servo_yaml)) or {}
    servo_params = {"moveit_servo": _yml}
    servo_node = Node(
        package="moveit_servo",
        executable="servo_node",
        name="servo_node",
        parameters=[
            servo_params,
            robot_description,
            robot_description_semantic,
            {"robot_description_kinematics": __import__("yaml").safe_load(open(kinematics_yaml)) or {}},
        ],
        output="screen",
    )

    # No move_group — Servo doesn't need motion planning. /joint_states comes from
    # metamove_bridge (RWS poll).
    return LaunchDescription([rsp, servo_node])
