#!/usr/bin/env bash
# E2E smoke test: publish a small +Z twist into moveit_servo, wait, then publish stop.
# Run inside the ros2 container while Unity Editor is in Play mode and VC is running.
#
#   wsl docker exec -it <ros2-container> bash /mnt/c/git/MetaMove/ros2/test/e2e_twist_send.sh
set -eo pipefail
source /opt/ros/jazzy/setup.bash

echo "=== switch_command_type=TWIST ==="
timeout 5 ros2 service call /servo_node/switch_command_type \
    moveit_msgs/srv/ServoCommandType '{command_type: 1}' 2>&1 | tail -3

echo "=== pause_servo=false (start) ==="
timeout 5 ros2 service call /servo_node/pause_servo \
    std_srvs/srv/SetBool '{data: false}' 2>&1 | tail -3

echo "=== publish stamped +Z twist via rclpy for 3s ==="
python3 - <<'PY'
import rclpy, time
from geometry_msgs.msg import TwistStamped
rclpy.init()
n = rclpy.create_node('e2e_twist_sender')
pub = n.create_publisher(TwistStamped, '/servo_node/delta_twist_cmds', 10)
end = time.time() + 3
while time.time() < end:
    msg = TwistStamped()
    msg.header.stamp = n.get_clock().now().to_msg()
    msg.header.frame_id = 'base_link'
    msg.twist.linear.z = 0.05    # 5 cm/s up
    pub.publish(msg)
    rclpy.spin_once(n, timeout_sec=0.02)
print("done — twist publish finished")
PY

echo "=== check /servo_node/commands rate ==="
timeout 4 ros2 topic hz /servo_node/commands -w 30 2>&1 | tail -3 || echo "(no commands seen — servo not active or Unity not subscribed)"
