"""Publish fake /quest/head_pose at a few distances to test the scaler."""
import sys
import time
import rclpy
from geometry_msgs.msg import PoseStamped

rclpy.init()
n = rclpy.create_node('fake_head_pub')
pub = n.create_publisher(PoseStamped, '/quest/head_pose', 10)
dists = [float(x) for x in (sys.argv[1:] or ['0.3', '1.3', '2.5'])]
hold = 3.0
for d in dists:
    n.get_logger().info(f'publishing head at {d} m for {hold}s')
    t_end = time.monotonic() + hold
    while time.monotonic() < t_end:
        m = PoseStamped()
        m.header.frame_id = 'quest_world'
        m.pose.position.x = d
        m.pose.orientation.w = 1.0
        pub.publish(m)
        time.sleep(0.05)
n.get_logger().info('done')
rclpy.shutdown()
