"""
MoveIt-IK relay — Unity-pose-target → joint-positions, bypassing moveit_servo.

Subscribes:
  /metamove/ik_target    geometry_msgs/PoseStamped   (target TCP pose in base_link)
Publishes:
  /servo_node/commands   std_msgs/Float64MultiArray  (6 joint positions in rad)

Calls MoveIt2's /compute_ik service for each incoming target. This bypasses
moveit_servo entirely (which has a known PSM-bootstrap deadlock with sim
joint sources in jazzy), while still using MoveIt's KDL/TracIK kinematics
plugin — so the IK solution itself is the same one Servo would compute.

For real-EGM path: same node, real /joint_states comes from EGM bridge,
solutions still go to /servo_node/commands → Unity → EGM controller.
"""
from __future__ import annotations

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, QoSReliabilityPolicy, QoSDurabilityPolicy
from geometry_msgs.msg import PoseStamped
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray
from moveit_msgs.srv import GetPositionIK
from moveit_msgs.msg import PositionIKRequest, RobotState


JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']


class MoveItIkRelay(Node):
    def __init__(self) -> None:
        super().__init__('moveit_ik_relay')

        self._latest_state = [0.0, 0.0, -0.7854, 0.0, -0.7854, 0.0]
        self._latest_target: PoseStamped | None = None

        # Match MoveIt's standard sensor_data QoS for /joint_states.
        self.create_subscription(JointState, '/joint_states',
                                  self._on_joint_state,
                                  QoSProfile(depth=1,
                                             reliability=QoSReliabilityPolicy.BEST_EFFORT,
                                             durability=QoSDurabilityPolicy.VOLATILE))

        # Unity publishes RELIABLE/VOLATILE (default ROS-TCP-Connector).
        self.create_subscription(PoseStamped, '/metamove/ik_target',
                                  self._on_target, 10)

        self.cmd_pub = self.create_publisher(Float64MultiArray,
                                              '/servo_node/commands', 10)

        self.ik_cli = self.create_client(GetPositionIK, '/compute_ik')
        self.get_logger().info('Waiting for /compute_ik service...')
        while not self.ik_cli.wait_for_service(timeout_sec=2.0):
            self.get_logger().info('Still waiting for /compute_ik...')

        self.create_timer(1.0 / 50.0, self._tick)  # 50 Hz IK rate
        self.get_logger().info('MoveIt IK relay ready (50 Hz).')

        self._in_flight = False

    def _on_joint_state(self, msg: JointState) -> None:
        # Cache latest joint positions for IK seed.
        for i, name in enumerate(msg.name):
            if name in JOINT_NAMES:
                idx = JOINT_NAMES.index(name)
                if idx < len(msg.position):
                    self._latest_state[idx] = msg.position[idx]

    def _on_target(self, msg: PoseStamped) -> None:
        self._latest_target = msg

    def _tick(self) -> None:
        if self._latest_target is None or self._in_flight:
            return

        req = GetPositionIK.Request()
        req.ik_request = PositionIKRequest()
        req.ik_request.group_name = 'manipulator'
        req.ik_request.pose_stamped = self._latest_target
        req.ik_request.timeout.sec = 0
        req.ik_request.timeout.nanosec = 50_000_000  # 50 ms
        req.ik_request.avoid_collisions = False

        seed = RobotState()
        seed.joint_state = JointState()
        seed.joint_state.name = list(JOINT_NAMES)
        seed.joint_state.position = list(self._latest_state)
        req.ik_request.robot_state = seed

        self._in_flight = True
        future = self.ik_cli.call_async(req)
        future.add_done_callback(self._on_ik_response)

    def _on_ik_response(self, future) -> None:
        self._in_flight = False
        try:
            resp = future.result()
        except Exception as e:
            self.get_logger().warn(f'IK call failed: {e}')
            return
        if resp.error_code.val != 1:  # SUCCESS
            return  # silent — happens normally near singularities / out of reach
        sol = resp.solution.joint_state
        out = [0.0] * 6
        for i, name in enumerate(sol.name):
            if name in JOINT_NAMES:
                idx = JOINT_NAMES.index(name)
                if i < len(sol.position):
                    out[idx] = sol.position[i]
        msg = Float64MultiArray()
        msg.data = out
        self.cmd_pub.publish(msg)


def main() -> None:
    rclpy.init()
    node = MoveItIkRelay()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
