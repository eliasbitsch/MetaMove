"""
Phase-1 IK loop v2 (sim, no real robot) — using ros2_control + FakeSystem
instead of fake_jsp. Matches the official moveit_servo demo_pose pattern
so MoveIt-Servo's PlanningSceneMonitor actually fires its callbacks.

Brings up:
  - ros2_control_node with FakeSystem mock_components (URDF-defined)
  - joint_state_broadcaster spawner (publishes /joint_states from the mock HW)
  - move_group + RViz + servo_node (via metamove_servo.launch.py)
  - pose_to_twist_node (Unity ik_target → /servo_node/delta_twist_cmds)
  - Tiny relay: /servo_node/commands → manipulator_controller via direct write
    to FakeSystem command_interface (closes the loop)

Replaces fake_joint_state_publisher entirely.
"""
import os
from ament_index_python.packages import get_package_share_directory
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument, IncludeLaunchDescription
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import LaunchConfiguration, PathJoinSubstitution
from launch_ros.actions import Node
from launch_ros.substitutions import FindPackageShare


def generate_launch_description():
    pkg_share = get_package_share_directory("abb_crb15000_moveit")
    ros2_controllers = os.path.join(pkg_share, "config", "ros2_controllers.yaml")

    # ros2_control node — drives the FakeSystem in the URDF, exposes joint_states
    # via joint_state_broadcaster.
    ros2_control_node = Node(
        package="controller_manager",
        executable="ros2_control_node",
        parameters=[ros2_controllers],
        remappings=[("/controller_manager/robot_description", "/robot_description")],
        output="screen",
    )

    # Spawn joint_state_broadcaster — this is what publishes /joint_states
    # at controller_manager.update_rate (100 Hz). Servo's PSM will see updates
    # via this and exit its "Waiting to receive robot state update" loop.
    jsb_spawner = Node(
        package="controller_manager",
        executable="spawner",
        arguments=["joint_state_broadcaster", "--controller-manager-timeout", "60",
                   "--controller-manager", "/controller_manager"],
        output="screen",
    )

    # Existing servo + rviz + move_group + bridges.
    metamove_servo = IncludeLaunchDescription(
        PythonLaunchDescriptionSource([
            PathJoinSubstitution([
                FindPackageShare("abb_crb15000_moveit"),
                "launch", "metamove_servo.launch.py",
            ]),
        ]),
    )

    pose_to_twist = Node(
        package="metamove_bridge",
        executable="pose_to_twist_node",
        name="pose_to_twist",
        output="screen",
        parameters=[{
            "input_topic": "/metamove/ik_target",
            "output_topic": "/servo_node/delta_twist_cmds",
            "base_frame": "base_link",
            "ee_frame": "tool0",
            "publish_hz": 100.0,
            "linear_gain": 5.0,
            "angular_gain": 1.5,
            "max_linear": 0.6,
        }],
    )

    return LaunchDescription([
        ros2_control_node,
        jsb_spawner,
        metamove_servo,
        pose_to_twist,
    ])
