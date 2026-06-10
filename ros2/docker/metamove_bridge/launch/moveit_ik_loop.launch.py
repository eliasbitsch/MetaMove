"""
Phase-1 IK loop v3 — Unity pose target → MoveIt /compute_ik → joint commands.

Brings up:
  - metamove_servo.launch.py    (move_group + servo_node + RViz + ros_tcp_endpoint)
  - fake_joint_state_publisher  (closes /servo_node/commands → /joint_states loop)
  - moveit_ik_relay             (replaces broken servo_node — direct /compute_ik call)

This bypasses moveit_servo's known PSM-bootstrap deadlock in jazzy by calling
MoveIt's /compute_ik service directly. The IK solver itself (KDL) is identical
to what Servo uses internally; only the streaming wrapper is replaced.
"""
from launch import LaunchDescription
from launch.actions import IncludeLaunchDescription
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import PathJoinSubstitution
from launch_ros.actions import Node
from launch_ros.substitutions import FindPackageShare


def generate_launch_description():
    return LaunchDescription([
        IncludeLaunchDescription(
            PythonLaunchDescriptionSource([
                PathJoinSubstitution([
                    FindPackageShare('abb_crb15000_moveit'),
                    'launch', 'metamove_servo.launch.py',
                ]),
            ]),
        ),
        Node(
            package='metamove_bridge',
            executable='fake_joint_state_publisher',
            name='fake_jsp',
            output='screen',
        ),
        Node(
            package='metamove_bridge',
            executable='moveit_ik_relay',
            name='moveit_ik_relay',
            output='screen',
        ),
    ])
