"""FollowJointTrajectory action server that replays MoveIt trajectories
through the PROVEN servo/EGM path: interpolated joint positions are published
to /servo_node/commands (radians), which the Windows EGM bridge forwards as
EGM joint corrections.

Unlike jtc_egm_stub.py this owns NO UDP socket — the Windows bridge keeps the
EGM session; we just feed it targets. moveit_servo is paused during execution
(its 50 Hz hold-position publishing would fight the replay) and resumed after.

Run inside the ROS container:
  python3 /opt/metamove_ws/src/metamove_bridge/metamove_bridge/jtc_servo_relay.py

Parameters:
  time_scale   (default 2.0)  — replay N x slower than planned (safety)
  rate_hz      (default 50.0) — interpolation/publish rate
  tolerance_deg(default 2.0)  — final convergence check via /joint_states
"""
from __future__ import annotations

import math
import threading
import time

import rclpy
from rclpy.qos import QoSProfile, DurabilityPolicy, ReliabilityPolicy
from rclpy.action import ActionServer
from rclpy.action.server import GoalResponse, CancelResponse
from rclpy.callback_groups import ReentrantCallbackGroup
from rclpy.executors import MultiThreadedExecutor
from rclpy.node import Node
from control_msgs.action import FollowJointTrajectory
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray
from std_srvs.srv import SetBool, Trigger

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
R2D = 180.0 / math.pi


class JtcServoRelay(Node):
    def __init__(self) -> None:
        super().__init__('joint_trajectory_controller')
        self.declare_parameter('time_scale', 2.0)
        self.declare_parameter('rate_hz', 50.0)
        self.declare_parameter('tolerance_deg', 2.0)
        # live_speed (0..1): continuous throttle read EVERY tick. The trajectory
        # is replayed via a time-cursor advanced by period*live_speed, so the
        # distance scaler can change speed mid-motion. 0 = freeze (hold pose),
        # 1 = full planned speed. This is what makes distance scaling continuous
        # instead of only-at-waypoint-boundaries.
        self.declare_parameter('live_speed', 1.0)

        # Reentrant group + MultiThreadedExecutor: the action execute callback
        # BLOCKS for the whole replay. With the default single-threaded
        # executor, /joint_states and service calls starve during execution —
        # verification then compares against a frozen pre-move pose
        # ("tolerance missed" although the robot arrived) and pause_servo
        # never goes out.
        self._cb_group = ReentrantCallbackGroup()
        self._stop_now = threading.Event()

        self._js_lock = threading.Lock()
        self._js: list[float] | None = None
        self.create_subscription(JointState, '/joint_states', self._on_js, 10,
                                 callback_group=self._cb_group)
        self.create_service(Trigger, 'jtc_relay/stop_now', self._on_stop,
                            callback_group=self._cb_group)

        # transient_local: rosbridge's subscription (which feeds the Windows
        # EGM bridge) requests TRANSIENT_LOCAL durability; a volatile
        # publisher would be silently incompatible (QoS warning, no data).
        qos = QoSProfile(depth=10,
                         reliability=ReliabilityPolicy.RELIABLE,
                         durability=DurabilityPolicy.TRANSIENT_LOCAL)
        self._cmd_pub = self.create_publisher(
            Float64MultiArray, '/servo_node/commands', qos)

        self._pause_cli = self.create_client(
            SetBool, '/servo_node/pause_servo',
            callback_group=self._cb_group)

        self._busy = threading.Lock()
        self._server = ActionServer(
            self, FollowJointTrajectory,
            'joint_trajectory_controller/follow_joint_trajectory',
            execute_callback=self._execute,
            goal_callback=self._on_goal,
            cancel_callback=lambda _h: CancelResponse.ACCEPT,
            callback_group=self._cb_group,
        )
        self.get_logger().info(
            'jtc_servo_relay up — executing MoveIt trajectories via '
            '/servo_node/commands -> EGM bridge')

    def _on_stop(self, _req, resp):
        self._stop_now.set()
        self.get_logger().warn('STOP NOW requested — freezing trajectory')
        resp.success = True
        resp.message = 'stopping'
        return resp

    def _on_js(self, msg: JointState) -> None:
        try:
            idx = [list(msg.name).index(n) for n in JOINT_NAMES]
        except ValueError:
            return
        with self._js_lock:
            self._js = [msg.position[i] for i in idx]

    def _on_goal(self, _req) -> GoalResponse:
        if self._busy.locked():
            self.get_logger().warn('goal rejected — busy')
            return GoalResponse.REJECT
        with self._js_lock:
            if self._js is None:
                self.get_logger().warn('goal rejected — no /joint_states yet')
                return GoalResponse.REJECT
        return GoalResponse.ACCEPT

    def _pause_servo(self, pause: bool) -> None:
        if not self._pause_cli.service_is_ready():
            self.get_logger().warn('pause_servo service not ready — skipping')
            return
        req = SetBool.Request()
        req.data = pause
        self._pause_cli.call_async(req)  # fire and forget; replay must not block

    def _execute(self, handle):
        with self._busy:
            return self._run(handle)

    def _run(self, handle):
        result = FollowJointTrajectory.Result()
        traj = handle.request.trajectory
        names = list(traj.joint_names)
        pts = list(traj.points)
        if not pts:
            handle.abort()
            result.error_code = result.INVALID_GOAL
            return result
        try:
            idx = [names.index(n) for n in JOINT_NAMES]
        except ValueError as e:
            self.get_logger().error(f'missing joint: {e}')
            handle.abort()
            result.error_code = result.INVALID_JOINTS
            return result

        scale = float(self.get_parameter('time_scale').value)
        rate = float(self.get_parameter('rate_hz').value)
        tol = float(self.get_parameter('tolerance_deg').value)

        # (time, positions[6]) keyframes, time-scaled
        keys: list[tuple[float, list[float]]] = []
        for p in pts:
            t = (p.time_from_start.sec
                 + p.time_from_start.nanosec * 1e-9) * scale
            keys.append((t, [p.positions[i] for i in idx]))
        total = keys[-1][0]
        self.get_logger().info(
            f'executing {len(keys)} pts over {total:.1f}s (scale x{scale})')

        self._stop_now.clear()
        self._pause_servo(True)
        period = 1.0 / rate
        tc = 0.0          # trajectory-time cursor (s, in stretched timeline)
        ki = 0
        try:
            while True:
                if handle.is_cancel_requested or self._stop_now.is_set():
                    # soft stop: 0.5 s ramp from the last commanded point to
                    # the actual robot pose. An instant freeze is a hard step
                    # backward for the controller and can trip the GoFa's
                    # collaborative supervision at higher speeds.
                    with self._js_lock:
                        js = list(self._js) if self._js else None
                    last_q = locals().get('q') or js
                    if js and last_q:
                        steps = 25  # 0.5 s @ 50 Hz
                        for i in range(1, steps + 1):
                            a = i / steps
                            m = Float64MultiArray()
                            m.data = [x + a * (y - x)
                                      for x, y in zip(last_q, js)]
                            self._cmd_pub.publish(m)
                            time.sleep(0.02)
                    if handle.is_cancel_requested:
                        handle.canceled()
                    else:
                        handle.abort()
                    result.error_code = result.GOAL_TOLERANCE_VIOLATED
                    self.get_logger().info('gestoppt — halte Position')
                    return result
                # advance the cursor by the LIVE speed factor (read each tick)
                sp = float(self.get_parameter('live_speed').value)
                sp = max(0.0, min(1.0, sp))
                tc += period * sp
                if tc >= total:
                    break
                while ki + 1 < len(keys) and keys[ki + 1][0] <= tc:
                    ki += 1
                (t0, q0) = keys[ki]
                (t1, q1) = keys[min(ki + 1, len(keys) - 1)]
                if t1 <= t0:
                    q = list(q1)
                else:
                    a = (tc - t0) / (t1 - t0)
                    q = [x0 + a * (x1 - x0) for x0, x1 in zip(q0, q1)]
                m = Float64MultiArray()
                m.data = q
                self._cmd_pub.publish(m)
                time.sleep(period)

            # final point a few times, then verify
            final = keys[-1][1]
            for _ in range(25):
                m = Float64MultiArray()
                m.data = list(final)
                self._cmd_pub.publish(m)
                time.sleep(period)

            deadline = time.monotonic() + 5.0
            err = 999.0
            while time.monotonic() < deadline:
                with self._js_lock:
                    js = list(self._js) if self._js else None
                if js:
                    err = max(abs(a - b) for a, b in zip(js, final)) * R2D
                    if err < tol:
                        break
                time.sleep(0.05)

            if err < tol:
                self.get_logger().info(f'done, err={err:.2f} deg')
                handle.succeed()
                result.error_code = result.SUCCESSFUL
            else:
                self.get_logger().warn(f'tolerance missed, err={err:.2f} deg')
                handle.abort()
                result.error_code = result.GOAL_TOLERANCE_VIOLATED
            return result
        finally:
            self._pause_servo(False)


def main() -> None:
    rclpy.init()
    node = JtcServoRelay()
    executor = MultiThreadedExecutor(num_threads=4)
    executor.add_node(node)
    try:
        executor.spin()
    except KeyboardInterrupt:
        pass
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
