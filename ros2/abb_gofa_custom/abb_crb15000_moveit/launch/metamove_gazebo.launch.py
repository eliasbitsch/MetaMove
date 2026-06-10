"""Full offline Phantom-IK demo: Gazebo (headless) + ros2_control + MoveIt-Servo.

Pipeline:
    Unity Sphere (grabbed)
        ↓ IKTargetPosePublisher
    /servo_node/pose_target_cmds
        ↓ MoveIt-Servo (POSE mode)
    /servo_node/commands  (Float64MultiArray, 6 joint positions in rad)
        ↓ joint_group_position_controller
    Gazebo Sim (headless physics)
        ↓ joint_state_broadcaster
    /joint_states
        ↓ Unity JointAnglesSubscriber + Servo's current-state input
    Closed loop.

Run inside the metamove-ros2 container:
    ros2 launch abb_crb15000_moveit metamove_gazebo.launch.py
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


def _pkg_path(rel: str) -> str:
    return str(Path(__file__).resolve().parent.parent / rel)


def generate_launch_description():
    urdf_xacro = _pkg_path("config/abb_crb15000_5_95.gazebo.urdf.xacro")
    controllers_yaml = _pkg_path("config/gazebo_controllers.yaml")
    servo_yaml = _pkg_path("config/servo_crb15000.yaml")
    srdf = _pkg_path("config/abb_crb15000_5_95.srdf")
    kinematics_yaml = _pkg_path("config/kinematics.yaml")
    initial_positions = _pkg_path("config/initial_positions.yaml")

    robot_description_xml = xacro.process_file(
        urdf_xacro,
        mappings={"initial_positions_file": initial_positions,
                  "controllers_file": controllers_yaml},
    ).toxml()
    robot_description = {"robot_description": robot_description_xml}
    with open(srdf) as f:
        srdf_text = f.read()
    robot_description_semantic = {"robot_description_semantic": srdf_text}

    # robot_state_publisher — broadcasts URDF + TF tree
    rsp = Node(
        package="robot_state_publisher",
        executable="robot_state_publisher",
        output="screen",
        parameters=[robot_description, {"use_sim_time": True}],
    )

    # Headless Gazebo Sim with an empty world
    gz_sim = IncludeLaunchDescription(
        PythonLaunchDescriptionSource(
            os.path.join(get_package_share_directory("ros_gz_sim"), "launch", "gz_sim.launch.py")
        ),
        launch_arguments={"gz_args": "-r -s --headless-rendering empty.sdf"}.items(),
    )

    # Spawn robot into Gazebo
    spawn = Node(
        package="ros_gz_sim",
        executable="create",
        arguments=["-topic", "robot_description", "-name", "abb_crb15000_5_95"],
        output="screen",
    )

    # Controllers — spawn after robot is present
    load_jsb = ExecuteProcess(
        cmd=["ros2", "control", "load_controller", "--set-state", "active", "joint_state_broadcaster"],
        output="screen",
    )
    load_jgpc = ExecuteProcess(
        cmd=["ros2", "control", "load_controller", "--set-state", "active", "joint_group_position_controller"],
        output="screen",
    )

    # MoveIt-Servo
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
            # Servo expects to publish to the controller's command topic by default
            # via /servo_node/commands → route to our JointGroupPositionController
            ("/servo_node/commands", "/joint_group_position_controller/commands"),
        ],
        output="screen",
    )

    return LaunchDescription([
        rsp,
        gz_sim,
        TimerAction(period=3.0, actions=[spawn]),
        RegisterEventHandler(event_handler=OnProcessExit(target_action=spawn, on_exit=[load_jsb])),
        RegisterEventHandler(event_handler=OnProcessExit(target_action=load_jsb, on_exit=[load_jgpc])),
        TimerAction(period=8.0, actions=[servo_node]),
    ])
