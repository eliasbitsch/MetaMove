from launch import LaunchDescription
from launch_ros.actions import Node
from moveit_configs_utils import MoveItConfigsBuilder


def generate_launch_description():
    moveit_config = (
        MoveItConfigsBuilder("abb_crb15000_moveit", package_name="abb_crb15000_moveit")
        .robot_description(file_path="config/crb15000_5_95.urdf.xacro")
        .to_moveit_configs()
    )

    return LaunchDescription([
        Node(
            package="goholo_task_primitives",
            executable="pick_place_node",
            output="screen",
            parameters=[
                moveit_config.robot_description,
                moveit_config.robot_description_semantic,
                moveit_config.robot_description_kinematics,
                {"use_sim_time": False},
            ],
        ),
    ])
