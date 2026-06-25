"""MetaMove SIM IK foundation — no Gazebo, no RViz, no moveit_servo.

Brings up the minimal stack for Unity <-> real MoveIt KDL IK:
  - robot_state_publisher (URDF/TF)
  - move_group            (MoveIt, provides /compute_ik = real KDL/TracIK)
  - rosbridge_websocket   (:9090, for control/playback consoles via roslibpy)
  - ros_tcp_endpoint      (:10000, Unity ROS-TCP-Connector)
  - fake_joint_state_publisher  (echoes /servo_node/commands -> /joint_states,
                                 closes the loop so IK has a seed state)
  - moveit_ik_relay       (Unity /metamove/ik_target -> /compute_ik -> joints)

Mode 2 (Cartesian target via real KDL IK). Waypoint playback (Mode 1) and the
distance_speed_scaler are layered on top in a later step.

Run inside the metamove-ros2 container:
  ros2 launch /opt/metamove_ws/src/metamove_bridge/launch/metamove_sim_ik.launch.py
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
        package="robot_state_publisher",
        executable="robot_state_publisher",
        parameters=[moveit_config.robot_description, {"publish_frequency": 50.0}],
    )

    move_group = Node(
        package="moveit_ros_move_group",
        executable="move_group",
        output="screen",
        parameters=[moveit_config.to_dict()],
    )

    rosbridge = Node(
        package="rosbridge_server",
        executable="rosbridge_websocket",
        output="screen",
        parameters=[{"port": 9090, "address": "0.0.0.0"}],
    )

    ros_tcp = Node(
        package="ros_tcp_endpoint",
        executable="default_server_endpoint",
        name="ros_tcp_endpoint",
        parameters=[{"ROS_IP": "0.0.0.0", "ROS_TCP_PORT": 10000}],
        output="screen",
    )

    fake_jsp = Node(
        package="metamove_bridge",
        executable="fake_joint_state_publisher",
        name="fake_jsp",
        output="screen",
    )

    ik_relay = Node(
        package="metamove_bridge",
        executable="moveit_ik_relay",
        name="moveit_ik_relay",
        output="screen",
    )

    return LaunchDescription([rsp, move_group, rosbridge, ros_tcp, fake_jsp, ik_relay])
