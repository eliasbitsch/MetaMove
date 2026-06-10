"""Launch metamove_bridge in sim-servo mode.

Subscribes /servo_node/commands and forwards to RWS PERS variable jTarget in
RAPID/T_ROB1/MetaMoveCorePers. Pair with MetaMoveCorePers.mod loaded on the
target controller.

Usage:
  ros2 launch metamove_bridge sim_servo.launch.py rws_ip:=192.168.125.1
"""
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration
from launch_ros.actions import Node


def generate_launch_description():
    return LaunchDescription([
        DeclareLaunchArgument('rws_ip', default_value='192.168.125.1'),
        DeclareLaunchArgument('rws_port', default_value='443'),
        DeclareLaunchArgument('rws_user', default_value='Default User'),
        DeclareLaunchArgument('rws_password', default_value='robotics'),
        DeclareLaunchArgument('servo_max_rate_hz', default_value='30.0'),
        Node(
            package='metamove_bridge',
            executable='bridge_node',
            name='metamove_bridge',
            output='screen',
            parameters=[{
                'rws_ip': LaunchConfiguration('rws_ip'),
                'rws_port': LaunchConfiguration('rws_port'),
                'rws_user': LaunchConfiguration('rws_user'),
                'rws_password': LaunchConfiguration('rws_password'),
                'servo_bridge': True,
                'servo_module': 'MetaMoveCorePers',
                'servo_var': 'jTarget',
                'servo_max_rate_hz': LaunchConfiguration('servo_max_rate_hz'),
                'poll_hz': 1.0,
            }],
        ),
    ])
