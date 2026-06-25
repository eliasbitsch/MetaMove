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

        # Freshness gate: if no grab target arrives for target_timeout s, this node
        # goes SILENT so the playback relay (jtc_servo_relay) owns /servo_node/commands
        # in AUTO mode. In MANUAL the Unity IKTargetPosePublisher streams ~50 Hz.
        self.declare_parameter('target_timeout', 0.3)
        # Safety slew on the REAL robot: cap each joint's commanded speed so a fast
        # hand move or a near-singularity IK jump can't snap the GoFa.
        self.declare_parameter('max_joint_speed', 0.3)   # rad/s per joint (raise once trusted)
        # FAIL-SAFE: never command unless we have a FRESH actual robot pose to seed
        # the slew from. Without it the seed is the stale default -> the real robot
        # would jump from its true pose to the default on the first command.
        self.declare_parameter('joint_state_timeout', 0.5)
        self._target_time = 0.0
        self._joint_state_time = 0.0
        self._last_cmd: list[float] | None = None
        self._active = False
        self._tick_dt = 1.0 / 50.0
        self._in_flight = False

        # Match MoveIt's standard sensor_data QoS for /joint_states.
        self.create_subscription(JointState, '/joint_states',
                                  self._on_joint_state,
                                  QoSProfile(depth=1,
                                             reliability=QoSReliabilityPolicy.BEST_EFFORT,
                                             durability=QoSDurabilityPolicy.VOLATILE))

        # Unity publishes RELIABLE/VOLATILE (default ROS-TCP-Connector).
        self.create_subscription(PoseStamped, '/metamove/ik_target',
                                  self._on_target, 10)

        # Must match jtc_servo_relay: rosbridge's subscription (feeding the Windows
        # EGM bridge) requests TRANSIENT_LOCAL durability — a volatile publisher is
        # silently incompatible (QoS warning, no data reaches the real robot).
        cmd_qos = QoSProfile(depth=10,
                             reliability=QoSReliabilityPolicy.RELIABLE,
                             durability=QoSDurabilityPolicy.TRANSIENT_LOCAL)
        self.cmd_pub = self.create_publisher(Float64MultiArray,
                                              '/servo_node/commands', cmd_qos)

        self.ik_cli = self.create_client(GetPositionIK, '/compute_ik')
        self.get_logger().info('Waiting for /compute_ik service...')
        while not self.ik_cli.wait_for_service(timeout_sec=2.0):
            self.get_logger().info('Still waiting for /compute_ik...')

        self.create_timer(self._tick_dt, self._tick)  # 50 Hz IK rate
        self.get_logger().info('MoveIt IK relay ready (50 Hz, freshness-gated + slew-limited).')

    def _on_joint_state(self, msg: JointState) -> None:
        # Cache latest joint positions for IK seed + slew base.
        for i, name in enumerate(msg.name):
            if name in JOINT_NAMES:
                idx = JOINT_NAMES.index(name)
                if idx < len(msg.position):
                    self._latest_state[idx] = msg.position[idx]
        self._joint_state_time = self.get_clock().now().nanoseconds * 1e-9

    def _on_target(self, msg: PoseStamped) -> None:
        self._latest_target = msg
        self._target_time = self.get_clock().now().nanoseconds * 1e-9

    def _tick(self) -> None:
        now = self.get_clock().now().nanoseconds * 1e-9
        fresh = (self._latest_target is not None
                 and (now - self._target_time)
                 < float(self.get_parameter('target_timeout').value))
        if not fresh:
            # No live grab target → go silent so the playback relay owns the bus (AUTO).
            self._active = False
            self._last_cmd = None
            return
        # FAIL-SAFE: refuse to command without a fresh ACTUAL robot pose to seed from.
        if (now - self._joint_state_time) >= float(self.get_parameter('joint_state_timeout').value):
            if self._active:
                self.get_logger().warn(
                    'IK idle: /joint_states stale — no real robot pose, refusing to command.')
            self._active = False
            self._last_cmd = None
            return
        if self._in_flight:
            return

        req = GetPositionIK.Request()
        req.ik_request = PositionIKRequest()
        req.ik_request.group_name = 'manipulator'
        req.ik_request.pose_stamped = self._latest_target
        req.ik_request.timeout.sec = 0
        req.ik_request.timeout.nanosec = 50_000_000  # 50 ms
        req.ik_request.avoid_collisions = True

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
        target = [0.0] * 6
        for i, name in enumerate(sol.name):
            if name in JOINT_NAMES:
                idx = JOINT_NAMES.index(name)
                if i < len(sol.position):
                    target[idx] = sol.position[i]

        # Re-check freshness: the target may have gone stale during the async IK call.
        now = self.get_clock().now().nanoseconds * 1e-9
        if (now - self._target_time) >= float(self.get_parameter('target_timeout').value):
            return

        # Seed the slew from the ACTUAL robot pose on (re)activation, so switching
        # AUTO->MANUAL never jumps from a stale command into the live grab.
        if not self._active or self._last_cmd is None:
            self._last_cmd = list(self._latest_state)
            self._active = True
            self.get_logger().info(
                f'IK ACTIVE — seed from /joint_states {[round(x, 3) for x in self._last_cmd]}')

        # Safety slew: bound each joint's per-tick change (rad/s -> rad/tick).
        step = float(self.get_parameter('max_joint_speed').value) * self._tick_dt
        cmd = []
        for i in range(6):
            d = max(-step, min(step, target[i] - self._last_cmd[i]))
            cmd.append(self._last_cmd[i] + d)
        self._last_cmd = cmd

        msg = Float64MultiArray()
        msg.data = cmd
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
