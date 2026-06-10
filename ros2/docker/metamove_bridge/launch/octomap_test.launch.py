"""Octomap pipeline sanity test — no Quest, no robot, just synthetic cloud.

Launches:
  * fake_cloud_publisher — rotating point cube on /cloud_in @ 10 Hz
  * octomap_server       — voxelises /cloud_in into /octomap_full + /octomap_binary
  * static TF map -> gofa_base_link (RViz fixed frame convenience)

Open RViz, set Fixed Frame = map, add an OccupancyGrid display on
/occupied_cells_vis_array (or MarkerArray). Voxels should appear and rotate.
"""
from launch import LaunchDescription
from launch_ros.actions import Node


def generate_launch_description():
    return LaunchDescription([
        Node(
            package='tf2_ros',
            executable='static_transform_publisher',
            name='map_to_gofa_base',
            arguments=['0', '0', '0', '0', '0', '0', 'map', 'gofa_base_link'],
        ),

        Node(
            package='metamove_bridge',
            executable='fake_cloud_publisher',
            name='fake_cloud_publisher',
            output='screen',
            emulate_tty=True,
            parameters=[{
                'frame_id': 'gofa_base_link',
                'topic': '/cloud_in',
                'rate_hz': 10.0,
                'offset_x': 1.0,
                'offset_z': 0.4,
                'cube_side': 0.6,
                'point_step': 0.04,
                'rotate': True,
            }],
        ),

        Node(
            package='octomap_server',
            executable='octomap_server_node',
            name='octomap_server',
            output='screen',
            emulate_tty=True,
            remappings=[('cloud_in', '/cloud_in')],
            parameters=[{
                'frame_id': 'map',
                'base_frame_id': 'gofa_base_link',
                'resolution': 0.02,
                'sensor_model.max_range': 3.0,
                'sensor_model.hit': 0.7,
                'sensor_model.miss': 0.4,
                'sensor_model.min': 0.12,
                'sensor_model.max': 0.97,
                'filter_ground': False,
                'latch': False,
            }],
        ),
    ])
