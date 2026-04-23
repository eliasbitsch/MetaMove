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
    ])
