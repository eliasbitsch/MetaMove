"""Closed-loop joint-state feedback for offline MoveIt-Servo testing.

Subscribes /servo_node/commands (Float64MultiArray, joint positions in rad)
and republishes as sensor_msgs/JointState on /joint_states. This closes the
loop so MoveIt-Servo's pose-tracking has a current state to plan from when no
real robot is connected.

Pair with:
  - MoveIt-Servo in POSE mode
  - IKTargetPosePublisher publishing to /servo_node/pose_target_cmds
  - Unity JointAnglesSubscriber on /servo_node/commands to render the result
"""
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray


JOINT_NAMES = ["joint_1", "joint_2", "joint_3", "joint_4", "joint_5", "joint_6"]


class JointStateLoopback(Node):
    def __init__(self):
        super().__init__("joint_state_loopback")
        self.create_subscription(Float64MultiArray, "/servo_node/commands", self._on_cmd, 10)
        self.pub = self.create_publisher(JointState, "/joint_states", 10)
        # GoFa lab standard home pose: J5=30° (URDF convention, TCP tilted).
        # Sign here is URDF-axis sign, not Unity-rig sign — at +30 Servo is happy,
        # at -30 it trips a joint-bound stop.
        import math
        self._latest = [v * math.pi / 180.0 for v in [0.0, 0.0, 0.0, 0.0, 30.0, 0.0]]
        self.create_timer(1.0 / 50.0, self._tick)
        self.get_logger().info("joint-state loopback: /servo_node/commands -> /joint_states at 50 Hz")

    def _on_cmd(self, msg: Float64MultiArray):
        # Intentionally NOT echoing commands back into /joint_states. Closing the
        # loop that way causes Servo's IK to chase its own output and quickly
        # diverge to joint bounds. Servo sees a stable home pose; Unity renders
        # the commands directly (JointAnglesSubscriber on /servo_node/commands).
        pass

    def _tick(self):
        msg = JointState()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.name = JOINT_NAMES
        msg.position = self._latest
        self.pub.publish(msg)


def main():
    rclpy.init()
    n = JointStateLoopback()
    try:
        rclpy.spin(n)
    except KeyboardInterrupt:
        pass
    finally:
        n.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
