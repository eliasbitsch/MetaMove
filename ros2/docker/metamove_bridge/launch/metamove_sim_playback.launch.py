"""MetaMove SIM waypoint playback — taught JOINT waypoints, no IK, never under the table.

Chain:
  dpp_playback -> MoveGroup /move_action (plans through each taught joint waypoint)
              -> joint_trajectory_controller (jtc_servo_relay, FollowJointTrajectory)
              -> /servo_node/commands (streamed at live_speed)
              -> Unity JointAnglesSubscriber (robot follows)

Brings up: rsp + move_group + ros_tcp_endpoint + rosbridge + fake_jsp
           + jtc_servo_relay (the MoveIt execution controller)
           + dpp_playback (loops the 5 taught waypoints from dpp_waypoints.yaml)

distance_speed_scaler is layered on top later (sets jtc_servo_relay.live_speed from
the Quest distance). Here live_speed stays 1.0 = full speed so playback is visible.

Run inside the metamove-ros2 container:
  ros2 launch /opt/metamove_ws/src/metamove_bridge/launch/metamove_sim_playback.launch.py
"""
from launch import LaunchDescription
from launch_ros.actions import Node
from moveit_configs_utils import MoveItConfigsBuilder


def generate_launch_description():
    moveit_config = (
        MoveItConfigsBuilder("abb_crb15000_5_95", package_name="abb_crb15000_moveit")
        .robot_description(file_path="config/abb_crb15000_5_95.urdf.xacro")
        .robot_description_semantic(file_path="config/abb_crb15000_5_95.srdf")
        .trajectory_execution(file_path="config/moveit_controllers.yaml")
        .planning_pipelines(pipelines=["ompl"])
        .to_moveit_configs()
    )

    rsp = Node(
        package="robot_state_publisher", executable="robot_state_publisher",
        parameters=[moveit_config.robot_description, {"publish_frequency": 50.0}],
    )
    move_group = Node(
        package="moveit_ros_move_group", executable="move_group",
        output="screen", parameters=[moveit_config.to_dict()],
    )
    rosbridge = Node(
        package="rosbridge_server", executable="rosbridge_websocket",
        output="screen", parameters=[{"port": 9090, "address": "0.0.0.0"}],
    )
    ros_tcp = Node(
        package="ros_tcp_endpoint", executable="default_server_endpoint",
        name="ros_tcp_endpoint",
        parameters=[{"ROS_IP": "0.0.0.0", "ROS_TCP_PORT": 10000}], output="screen",
    )
    fake_jsp = Node(
        package="metamove_bridge", executable="fake_joint_state_publisher",
        name="fake_jsp", output="screen",
    )
    jtc = Node(
        package="metamove_bridge", executable="jtc_servo_relay",
        name="joint_trajectory_controller", output="screen",
        parameters=[{"time_scale": 2.0, "rate_hz": 50.0, "live_speed": 1.0}],
    )
    playback = Node(
        package="metamove_bridge", executable="dpp_playback",
        name="dpp_playback", output="screen",
        parameters=[{
            "waypoints_file": "/opt/metamove_ws/src/metamove_bridge/dpp_waypoints.yaml",
            "velocity_scaling": 0.5,
            "acceleration_scaling": 0.5,
            "dwell_seconds": 0.5,
        }],
    )

    return LaunchDescription([rsp, move_group, rosbridge, ros_tcp, fake_jsp, jtc, playback])
