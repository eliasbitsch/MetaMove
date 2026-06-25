"""Standalone RViz with the MoveIt MotionPlanning display, for visually checking
the robot motion (shares /robot_description + /joint_states with the running stack).

Renders to Windows via WSLg (DISPLAY=:0, /tmp/.X11-unix mounted in the container).

Run inside the metamove-ros2 container (with the playback or IK stack already up):
  ros2 launch /opt/metamove_ws/src/metamove_bridge/launch/metamove_rviz.launch.py
"""
import os
from launch import LaunchDescription
from launch_ros.actions import Node
from moveit_configs_utils import MoveItConfigsBuilder


def generate_launch_description():
    moveit_config = (
        MoveItConfigsBuilder("abb_crb15000_5_95", package_name="abb_crb15000_moveit")
        .robot_description(file_path="config/abb_crb15000_5_95.urdf.xacro")
        .robot_description_semantic(file_path="config/abb_crb15000_5_95.srdf")
        .planning_pipelines(pipelines=["ompl"])
        .to_moveit_configs()
    )

    pkg_src = "/opt/metamove_ws/src/abb_gofa_custom/abb_crb15000_moveit"
    rviz_cfg = os.path.join(pkg_src, "config", "moveit.rviz")
    args = ["-d", rviz_cfg] if os.path.exists(rviz_cfg) else []

    rviz = Node(
        package="rviz2", executable="rviz2",
        arguments=args,
        parameters=[
            moveit_config.robot_description,
            moveit_config.robot_description_semantic,
            moveit_config.robot_description_kinematics,
        ],
        output="screen",
    )
    return LaunchDescription([rviz])
