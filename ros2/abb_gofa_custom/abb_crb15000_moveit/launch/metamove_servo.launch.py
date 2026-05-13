"""
MetaMove servo launch — RViz + MoveIt + moveit_servo + rosbridge_websocket.

Architektur:
  Servo (Container) -> /servo_node/commands (Float64MultiArray joint positions)
    -> rosbridge_websocket :9090 -> Windows EGM bridge -> EgmSensor -> Controller
  Controller -> EgmRobot feedback -> Windows bridge -> rosbridge -> /joint_states

Kein ros2_control hardware — Joint-States kommen via rosbridge vom Windows-Bridge.
"""
import os
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import IncludeLaunchDescription
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch_ros.actions import Node
from moveit_configs_utils import MoveItConfigsBuilder


def generate_launch_description():
    moveit_config = (
        MoveItConfigsBuilder("abb_crb15000_5_95",
                             package_name="abb_crb15000_moveit")
        .robot_description(file_path="config/abb_crb15000_5_95.urdf.xacro")
        .robot_description_semantic(file_path="config/abb_crb15000_5_95.srdf")
        .trajectory_execution(file_path="config/moveit_controllers.yaml")
        .planning_pipelines(pipelines=["ompl"])
        .to_moveit_configs()
    )

    # Prefer bind-mounted src over installed share so live yaml edits apply
    # without a colcon rebuild during lab iteration.
    pkg_share = get_package_share_directory("abb_crb15000_moveit")
    pkg_src   = "/opt/metamove_ws/src/abb_gofa_custom/abb_crb15000_moveit"
    def _resolve(rel):
        for base in (pkg_src, pkg_share):
            p = os.path.join(base, rel)
            if os.path.exists(p):
                return p
        raise FileNotFoundError(rel)
    servo_yaml = _resolve("config/servo_crb15000.yaml")
    rviz_cfg   = _resolve("config/moveit.rviz")

    rsp = Node(
        package="robot_state_publisher",
        executable="robot_state_publisher",
        parameters=[moveit_config.robot_description],
    )

    move_group = Node(
        package="moveit_ros_move_group",
        executable="move_group",
        output="screen",
        parameters=[moveit_config.to_dict()],
    )

    rviz = Node(
        package="rviz2",
        executable="rviz2",
        arguments=["-d", rviz_cfg],
        parameters=[
            moveit_config.robot_description,
            moveit_config.robot_description_semantic,
            moveit_config.robot_description_kinematics,
        ],
    )

    _yml = __import__("yaml").safe_load(open(servo_yaml)) or {}
    _yml.update({
        "scale": {"linear": 1.0, "rotational": 1.5, "joint": 1.0},
        "override_velocity_scaling_factor": 1.0,
    })
    servo_params = {"moveit_servo": _yml}
    servo_node = Node(
        package="moveit_servo",
        executable="servo_node",
        name="servo_node",
        parameters=[
            servo_params,
            moveit_config.robot_description,
            moveit_config.robot_description_semantic,
            moveit_config.robot_description_kinematics,
        ],
        output="screen",
    )

    rosbridge = Node(
        package="rosbridge_server",
        executable="rosbridge_websocket",
        output="screen",
        parameters=[{"port": 9090, "address": "0.0.0.0"}],
    )

    rosapi = Node(
        package="rosapi",
        executable="rosapi_node",
        output="screen",
    )

    # Unity-side ros-tcp-connector talks to this (binary TCP, NOT rosbridge JSON).
    # Listens on :10000 — Unity ROSConnection.RosIPAddress points to the docker
    # host IP, port stays 10000.
    ros_tcp = Node(
        package="ros_tcp_endpoint",
        executable="default_server_endpoint",
        name="ros_tcp_endpoint",
        parameters=[{"ROS_IP": "0.0.0.0", "ROS_TCP_PORT": 10000}],
        output="screen",
    )

    return LaunchDescription([rsp, move_group, rviz, servo_node, rosbridge, rosapi, ros_tcp])
