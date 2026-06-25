#!/bin/bash
source /opt/ros/jazzy/setup.bash 2>/dev/null
source /opt/metamove_ws/install/setup.bash 2>/dev/null
echo "install/setup.bash: $(test -f /opt/metamove_ws/install/setup.bash && echo yes || echo NO)"
echo "ROS_DISTRO=$ROS_DISTRO  DOMAIN=$ROS_DOMAIN_ID"
for p in ros_gz_sim ros_gz_bridge moveit_servo moveit_ros_move_group abb_crb15000_moveit metamove_bridge ros_tcp_endpoint controller_manager joint_state_broadcaster; do
  printf "%-26s " "$p"
  ros2 pkg prefix "$p" >/dev/null 2>&1 && echo OK || echo MISSING
done
echo "--- gz binary ---"
which gz ign 2>/dev/null || echo "no gz/ign on PATH"
