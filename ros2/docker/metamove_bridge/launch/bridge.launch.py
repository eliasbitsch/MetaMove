"""Launch MetaMove bridge node + rosbridge_websocket for Jarvis tool-calls."""
from launch import LaunchDescription
from launch.actions import DeclareLaunchArgument
from launch.substitutions import LaunchConfiguration, EnvironmentVariable
from launch_ros.actions import Node


def generate_launch_description():
    return LaunchDescription([
        DeclareLaunchArgument('rws_ip',   default_value=EnvironmentVariable('METAMOVE_RWS_IP', default_value='192.168.125.1')),
        DeclareLaunchArgument('rws_port', default_value=EnvironmentVariable('METAMOVE_RWS_PORT', default_value='443')),
        DeclareLaunchArgument('rws_user',     default_value='Default User'),
        DeclareLaunchArgument('rws_password', default_value='robotics'),
        DeclareLaunchArgument('scenario',     default_value='chess'),
        DeclareLaunchArgument('poll_hz',      default_value='2.0'),
        DeclareLaunchArgument('rosbridge_port', default_value='9090',
                              description='WS port for external clients (Jarvis, roslibjs)'),

        Node(
            package='metamove_bridge',
            executable='bridge_node',
            name='metamove_bridge',
            output='screen',
            emulate_tty=True,
            parameters=[{
                'rws_ip':       LaunchConfiguration('rws_ip'),
                'rws_port':     LaunchConfiguration('rws_port'),
                'rws_user':     LaunchConfiguration('rws_user'),
                'rws_password': LaunchConfiguration('rws_password'),
                'scenario':     LaunchConfiguration('scenario'),
                'poll_hz':      LaunchConfiguration('poll_hz'),
            }],
        ),

        # rosbridge_websocket — lets Python/JS clients call ROS services over WS
        # without installing rclpy. Used by Jarvis for tool-call routing.
        Node(
            package='rosbridge_server',
            executable='rosbridge_websocket',
            name='rosbridge_websocket',
            output='screen',
            parameters=[{
                'port': LaunchConfiguration('rosbridge_port'),
                'address': '0.0.0.0',
            }],
        ),
    ])
