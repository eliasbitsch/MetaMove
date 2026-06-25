"""Joint feedback relay — real robot /joint_states -> Unity model mirror.

Bidirectional digital twin: the Unity GoFa model should always show the REAL
robot's actual pose, regardless of who commands it (waypoint playback OR manual
grab-IK). The robot's true state arrives as sensor_msgs/JointState on
/joint_states (published by the Windows EGM bridge from EgmRobot feedback,
~50 Hz). The Unity model driver (JointAnglesSubscriber) consumes a
std_msgs/Float64MultiArray of 6 joint positions (rad) and already carries the
verified per-axis signFlip/offset config.

This node bridges the two: it picks joint_1..joint_6 (in URDF order) out of
/joint_states by name and republishes them as Float64MultiArray on
/robot/joint_feedback. Point the model's JointAnglesSubscriber at that topic and
the twin mirrors the live robot — no signFlip re-entry, no command-path coupling.

Standalone (run via python3, no colcon entry point needed):
  python3 .../metamove_bridge/joint_feedback_relay.py
"""
from __future__ import annotations

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, QoSReliabilityPolicy, QoSDurabilityPolicy
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']


class JointFeedbackRelay(Node):
    def __init__(self) -> None:
        super().__init__('joint_feedback_relay')
        self.declare_parameter('in_topic', '/joint_states')
        self.declare_parameter('out_topic', '/robot/joint_feedback')

        # /joint_states is sensor data (BEST_EFFORT). Match it so we actually
        # receive from the EGM bridge / fake_jsp.
        sub_qos = QoSProfile(depth=10,
                             reliability=QoSReliabilityPolicy.BEST_EFFORT,
                             durability=QoSDurabilityPolicy.VOLATILE)
        self.create_subscription(JointState,
                                 self.get_parameter('in_topic').value,
                                 self._on_state, sub_qos)
        self.pub = self.create_publisher(
            Float64MultiArray, self.get_parameter('out_topic').value, 10)
        self._n = 0
        self.get_logger().info(
            f"joint_feedback_relay up — {self.get_parameter('in_topic').value} "
            f"-> {self.get_parameter('out_topic').value} (live twin mirror)")

    def _on_state(self, msg: JointState) -> None:
        if not msg.name or not msg.position:
            return
        out = [0.0] * 6
        for i, jn in enumerate(JOINT_NAMES):
            try:
                idx = list(msg.name).index(jn)
            except ValueError:
                continue
            if idx < len(msg.position):
                out[i] = float(msg.position[idx])
        self.pub.publish(Float64MultiArray(data=out))
        self._n += 1
        if self._n % 100 == 0:
            self.get_logger().info(
                f'mirroring real pose: {[round(x, 3) for x in out]}')


def main() -> None:
    rclpy.init()
    node = JointFeedbackRelay()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
