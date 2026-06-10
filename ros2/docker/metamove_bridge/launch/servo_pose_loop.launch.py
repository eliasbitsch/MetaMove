"""
Phase-1 IK loop (sim, no real robot).

Brings up:
  - metamove_servo (rviz + move_group + moveit_servo + rosbridge + ros_tcp_endpoint)
  - pose_to_twist_node       (Unity ik_target → /servo_node/delta_twist_cmds)
  - fake_joint_state_publisher  (/servo_node/commands → /joint_states, closes loop)

Once running, grab the IK target ball in Unity → MoveIt Servo computes
joints → Unity GoFa visual follows. No real robot is touched.

Phase-2 swaps `fake_joint_state_publisher` for the real EGM-fed JSP.
"""
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument, IncludeLaunchDescription
from launch.launch_description_sources import PythonLaunchDescriptionSource
from launch.substitutions import LaunchConfiguration, PathJoinSubstitution
from launch_ros.actions import Node
from launch_ros.substitutions import FindPackageShare


def generate_launch_description():
    return LaunchDescription([
        DeclareLaunchArgument('publish_hz', default_value='100.0'),
        DeclareLaunchArgument('linear_gain', default_value='5.0'),
        DeclareLaunchArgument('angular_gain', default_value='1.5'),
        DeclareLaunchArgument('max_linear', default_value='0.6'),

        # Existing servo + rviz + move_group + bridges.
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
            executable='pose_to_twist_node',
            name='pose_to_twist',
            output='screen',
            parameters=[{
                'input_topic': '/metamove/ik_target',
                'output_topic': '/servo_node/delta_twist_cmds',
                'base_frame': 'base_link',
                'ee_frame': 'tool0',
                'publish_hz': LaunchConfiguration('publish_hz'),
                'linear_gain': LaunchConfiguration('linear_gain'),
                'angular_gain': LaunchConfiguration('angular_gain'),
                'max_linear': LaunchConfiguration('max_linear'),
            }],
        ),

        Node(
            package='metamove_bridge',
            executable='fake_joint_state_publisher',
            name='fake_jsp',
            output='screen',
        ),
    ])
