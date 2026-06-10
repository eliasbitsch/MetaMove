"""Fake /joint_states publisher for offline JointStateRenderer testing.

Publishes a 6-DOF JointState in radians at 30 Hz with each joint following its
own sinusoid (different amplitude + period per joint) so you can visually verify
axis mapping, sign, and ordering without a real robot.

Run inside the metamove-ros2 container:
    python3 /mnt/c/git/MetaMove/ros2/docker/fake_joint_publisher.py

Or copy in via stdin if the file system mount path differs.
"""
import math
import time

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray


JOINT_NAMES = ["joint_1", "joint_2", "joint_3", "joint_4", "joint_5", "joint_6"]

# Per-joint (amplitude_deg, period_sec) — primes so you can tell which is which.
WAVEFORM = [
    (30, 6.0),  # J1: ±30°  over 6s
    (20, 5.0),  # J2: ±20°  over 5s
    (15, 4.0),  # J3: ±15°  over 4s
    (45, 7.0),  # J4: ±45°  over 7s
    (30, 3.0),  # J5: ±30°  over 3s
    (60, 8.0),  # J6: ±60°  over 8s
]
DEG2RAD = math.pi / 180


class FakePub(Node):
    def __init__(self):
        super().__init__("fake_joint_publisher")
        # Publish to BOTH topics so either subscriber works:
        #   /joint_states         (sensor_msgs/JointState) — for JointStateRenderer
        #   /servo_node/commands  (std_msgs/Float64MultiArray) — for JointAnglesSubscriber
        self.pub_js = self.create_publisher(JointState, "/joint_states", 10)
        self.pub_cmd = self.create_publisher(Float64MultiArray, "/servo_node/commands", 10)
        self.t0 = time.perf_counter()
        self.create_timer(1.0 / 30.0, self._tick)
        self.get_logger().info("fake publisher: /joint_states + /servo_node/commands at 30 Hz")

    def _tick(self):
        t = time.perf_counter() - self.t0
        positions = []
        for amp, period in WAVEFORM:
            deg = amp * math.sin(2 * math.pi * t / period)
            positions.append(deg * DEG2RAD)

        js = JointState()
        js.header.stamp = self.get_clock().now().to_msg()
        js.name = JOINT_NAMES
        js.position = positions
        self.pub_js.publish(js)

        cmd = Float64MultiArray()
        cmd.data = positions
        self.pub_cmd.publish(cmd)


def main():
    rclpy.init()
    n = FakePub()
    try:
        rclpy.spin(n)
    except KeyboardInterrupt:
        pass
    finally:
        n.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
