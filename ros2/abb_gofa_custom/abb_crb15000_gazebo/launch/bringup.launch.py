"""Headless Gazebo Sim + ros2_control + MoveIt-Servo for the GoFa-5/0.95.

Pipeline (Phantom-IK closed-loop):
    Unity Sphere (grabbed)
        |  IKTargetPosePublisher
        v
    /servo_node/pose_target_cmds
        |  MoveIt-Servo (POSE mode, with this URDF + SRDF)
        v
    /joint_group_position_controller/commands  (Float64MultiArray)
        |  position_controllers/JointGroupPositionController
        v
    Gazebo Sim (headless physics)
        |  joint_state_broadcaster
        v
    /joint_states  ->  Unity JointAnglesSubscriber + Servo current-state
    Closed loop.

Run inside the metamove-ros2 container:
    ros2 launch abb_crb15000_gazebo bringup.launch.py
"""
import os
from pathlib import Path
from launch import LaunchDescription
from launch.actions import IncludeLaunchDescription, ExecuteProcess, RegisterEventHandler, TimerAction
from launch.event_handlers import OnProcessExit
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch_ros.actions import Node
from ament_index_python.packages import get_package_share_directory
import xacro


def generate_launch_description():
    pkg_share = get_package_share_directory("abb_crb15000_gazebo")
    moveit_share = get_package_share_directory("abb_crb15000_moveit")

    urdf_xacro = os.path.join(pkg_share, "urdf", "abb_crb15000.urdf.xacro")
    controllers_yaml = os.path.join(pkg_share, "config", "gazebo_controllers.yaml")
    servo_yaml = os.path.join(moveit_share, "config", "servo_crb15000.yaml")
    srdf_path = os.path.join(moveit_share, "config", "abb_crb15000_5_95.srdf")
    kinematics_yaml = os.path.join(moveit_share, "config", "kinematics.yaml")

    robot_description_xml = xacro.process_file(
        urdf_xacro, mappings={"controllers_file": controllers_yaml}
    ).toxml()
    robot_description = {"robot_description": robot_description_xml}
    with open(srdf_path) as f:
        srdf_text = f.read()
    robot_description_semantic = {"robot_description_semantic": srdf_text}

    rsp = Node(
        package="robot_state_publisher",
        executable="robot_state_publisher",
        output="screen",
        parameters=[robot_description, {"use_sim_time": True}],
    )

    gz_sim = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            os.path.join(get_package_share_directory("ros_gz_sim"), "launch", "gz_sim.launch.py")
        ),
        launch_arguments={"gz_args": "-r -s --headless-rendering empty.sdf"}.items(),
    )

    spawn = Node(
        package="ros_gz_sim",
        executable="create",
        arguments=["-topic", "robot_description", "-name", "gofa"],
        output="screen",
    )

    # Bridge Gazebo's clock so ROS2 nodes with use_sim_time can synchronize.
    # Without this the controller_manager waits forever for sim time.
    clock_bridge = Node(
        package="ros_gz_bridge",
        executable="parameter_bridge",
        arguments=["/clock@rosgraph_msgs/msg/Clock[gz.msgs.Clock"],
        output="screen",
    )

    load_jsb = ExecuteProcess(
        cmd=["ros2", "control", "load_controller", "--set-state", "active", "joint_state_broadcaster"],
        output="screen",
    )
    load_jgpc = ExecuteProcess(
        cmd=["ros2", "control", "load_controller", "--set-state", "active", "joint_group_position_controller"],
        output="screen",
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
            {"use_sim_time": True},
        ],
        remappings=[
            ("/servo_node/commands", "/joint_group_position_controller/commands"),
        ],
        output="screen",
    )

    return LaunchDescription([
        rsp,
        gz_sim,
        clock_bridge,
        TimerAction(period=4.0, actions=[spawn]),
        RegisterEventHandler(event_handler=OnProcessExit(target_action=spawn, on_exit=[load_jsb])),
        RegisterEventHandler(event_handler=OnProcessExit(target_action=load_jsb, on_exit=[load_jgpc])),
        TimerAction(period=10.0, actions=[servo_node]),
    ])
