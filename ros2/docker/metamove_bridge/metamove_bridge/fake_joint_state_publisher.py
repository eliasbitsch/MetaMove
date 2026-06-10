"""
Fake joint-state publisher — closes the IK loop in Phase-1 (sim, no robot).

Subscribes:
  /servo_node/commands  std_msgs/Float64MultiArray  (6 joint positions in rad)
Publishes:
  /joint_states         sensor_msgs/JointState

Without this node, MoveIt Servo would keep computing twists against a
stale starting joint state because nothing publishes /joint_states in
sim. With this node, every joint command Servo emits is immediately
echoed back as the new joint state, so Servo "sees" the robot moving.

Replace this with the real EGM-fed joint-state publisher in Phase-2.
"""
from __future__ import annotations

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSDurabilityPolicy, QoSProfile, QoSReliabilityPolicy
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray
from moveit_msgs.msg import ServoStatus


# Standard ABB CRB 15000 joint names — must match the SRDF for MoveIt.
DEFAULT_JOINT_NAMES = [
    'joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6',
]


class FakeJsp(Node):
    def __init__(self) -> None:
        super().__init__('fake_joint_state_publisher')

        self.declare_parameter('input_topic', '/servo_node/commands')
        self.declare_parameter('output_topic', '/joint_states')
        self.declare_parameter('joint_names', DEFAULT_JOINT_NAMES)
        # Default ready-pose: avoid the "all zeros" elbow+wrist singularity.
        # joint_3 (elbow) = -45°, joint_5 (wrist pitch) = -45° → arm bent up,
        # wrist clear of flange-aligned-with-forearm singularity.
        self.declare_parameter('initial_positions', [0.0, 0.0, -0.7854, 0.0, -0.7854, 0.0])
        self.declare_parameter('publish_hz', 100.0)

        self.joint_names = list(self.get_parameter('joint_names').value)
        positions = list(self.get_parameter('initial_positions').value)
        self._positions = [float(v) for v in positions]

        # Default ROS2 subscribers use RELIABLE QoS (rclcpp::SystemDefaultsQoS).
        # Unity's ROS-TCP-Connector also subscribes RELIABLE. Publishing RELIABLE
        # is compatible with both RELIABLE and BEST_EFFORT subscribers.
        self.pub = self.create_publisher(JointState, self.get_parameter('output_topic').value,
                                          QoSProfile(depth=10,
                                                     reliability=QoSReliabilityPolicy.RELIABLE,
                                                     durability=QoSDurabilityPolicy.VOLATILE))

        self.create_subscription(Float64MultiArray,
                                 self.get_parameter('input_topic').value,
                                 self._on_cmd, 10)

        # Track Servo status so we can ignore emergency-stop commands which
        # would otherwise pin joints back to zero and trap the loop in a
        # singularity (the all-zeros pose IS a singularity for the GoFa).
        # Default false: don't accept any cmd until we've heard a clean status,
        # otherwise the first emergency-stop zero would overwrite initial_positions
        # before status arrives.
        self._servo_ok = False
        self.create_subscription(ServoStatus, '/servo_node/status', self._on_status, 10)

        publish_hz = float(self.get_parameter('publish_hz').value)
        self.create_timer(1.0 / max(publish_hz, 1.0), self._tick)

        self.get_logger().info(
            f'fake_jsp: echoing {self.get_parameter("input_topic").value} → '
            f'{self.get_parameter("output_topic").value} for joints {self.joint_names}'
        )

    def _on_cmd(self, msg: Float64MultiArray) -> None:
        if msg.data is None or len(msg.data) < len(self.joint_names):
            return
        # Emergency-stop signature: all positions exactly zero. Real solutions
        # for non-home configurations always have at least one non-zero joint.
        # Skipping these prevents the loop from being yanked back to home and
        # re-trapped in a singularity each time servo halts.
        n = len(self.joint_names)
        if all(abs(float(msg.data[i])) < 1e-9 for i in range(n)):
            return
        for i in range(n):
            self._positions[i] = float(msg.data[i])

    def _on_status(self, msg: ServoStatus) -> None:
        # Kept for diagnostics; emergency-stop is now detected by all-zero cmd
        # signature in _on_cmd, which is more robust than enum-watching across
        # MoveIt versions.
        self._servo_ok = (msg.code == 0)

    def _tick(self) -> None:
        msg = JointState()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.name = self.joint_names
        msg.position = self._positions
        # MoveIt's CurrentStateMonitor expects same-length arrays even if zero.
        msg.velocity = [0.0] * len(self.joint_names)
        msg.effort = [0.0] * len(self.joint_names)
        self.pub.publish(msg)


def main() -> None:
    rclpy.init()
    node = FakeJsp()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
