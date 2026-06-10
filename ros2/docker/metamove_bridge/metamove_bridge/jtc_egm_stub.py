"""
Fake joint_trajectory_controller backed by a persistent EGM-UDP session.

Improved version: one background thread holds UDP 6511 open the whole time
and streams EgmSensor at 250 Hz with the CURRENT TARGET. Between MoveIt goals
that target is "hold last feedback". When a goal arrives, we set target to the
trajectory's final point. Convergence is detected from EgmRobot feedback.

This keeps the controller's EGM session in EGM_STATE_CONNECTED → EGMRunJoint
loop stays active. Without the heartbeat, MainModule.main drops to HOLD after
each drive ends, and the next goal cannot re-engage cleanly.

Run inside the ROS container:
    ros2 run metamove_bridge jtc_egm_stub
"""
from __future__ import annotations

import math
import os
import socket
import sys
import threading
import time
from pathlib import Path

import rclpy
from rclpy.action import ActionServer
from rclpy.action.server import GoalResponse, CancelResponse
from rclpy.node import Node
from control_msgs.action import FollowJointTrajectory
from sensor_msgs.msg import JointState

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import egm_pb2  # type: ignore

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
EGM_IP    = os.environ.get('EGM_CTRL_IP', '192.168.125.1')
EGM_PORT  = int(os.environ.get('EGM_CTRL_PORT', '6511'))


class JtcEgmStub(Node):
    def __init__(self) -> None:
        super().__init__('joint_trajectory_controller')

        self.declare_parameter('max_step_deg', 0.5)
        self.declare_parameter('tolerance_deg', 2.0)
        self.declare_parameter('safety_timeout_s', 300.0)
        self.declare_parameter('settle_ticks', 25)

        # EGM session state (held forever once first feedback arrives)
        self._egm_lock = threading.Lock()
        self._target_deg: list[float] | None = None    # what we're pushing toward
        self._feedback_deg: list[float] | None = None  # latest joint feedback
        self._last_addr: tuple | None = None
        self._egm_running = threading.Event()

        # Goal handling
        self._active_goal = threading.Lock()

        self.create_subscription(JointState, '/joint_states', self._on_js, 10)

        # Start EGM session loop
        self._egm_thread = threading.Thread(target=self._egm_loop, daemon=True)
        self._egm_thread.start()

        self._action_server = ActionServer(
            self, FollowJointTrajectory,
            'joint_trajectory_controller/follow_joint_trajectory',
            execute_callback=self._execute_cb,
            goal_callback=self._goal_cb,
            cancel_callback=self._cancel_cb,
        )
        self.get_logger().info(
            'JTC stub up (persistent EGM): '
            'action /joint_trajectory_controller/follow_joint_trajectory'
        )

    # --------------------------- EGM heartbeat loop ---------------------

    def _egm_loop(self) -> None:
        """Persistent UDP session — keeps EGM_STATE_CONNECTED alive."""
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            sock.bind(('0.0.0.0', EGM_PORT))
        except OSError as e:
            self.get_logger().error(f'EGM bind err: {e}')
            return
        sock.settimeout(0.02)
        self.get_logger().info(f'EGM listening on 0.0.0.0:{EGM_PORT}')

        seq = 0
        max_step = float(self.get_parameter('max_step_deg').value)
        bootstrap_t = time.monotonic()
        while rclpy.ok():
            # Periodically send unsolicited EgmSensor to bootstrap session
            now = time.monotonic()
            if not self._egm_running.is_set() and now - bootstrap_t > 0.04:
                bootstrap_t = now
                m = egm_pb2.EgmSensor()
                m.header.seqno = seq; seq += 1
                m.header.tm = int(time.time()*1000) & 0xFFFFFFFF
                m.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
                if self._target_deg:
                    m.planned.joints.joints.extend(self._target_deg)
                sock.sendto(m.SerializeToString(), (EGM_IP, EGM_PORT))

            try:
                data, addr = sock.recvfrom(4096)
            except socket.timeout:
                continue

            robot = egm_pb2.EgmRobot()
            try:
                robot.ParseFromString(data)
            except Exception:
                continue
            if not (robot.HasField('feedBack') and robot.feedBack.HasField('joints')):
                continue
            current = list(robot.feedBack.joints.joints)[:6]
            if len(current) < 6:
                continue

            self._egm_running.set()
            with self._egm_lock:
                self._feedback_deg = current
                self._last_addr = addr
                if self._target_deg is None:
                    # Initialize hold-current target
                    self._target_deg = list(current)
                target = list(self._target_deg)

            # Rate-limit commanded position
            cmd = []
            for c, t in zip(current, target):
                d = t - c
                d = max(-max_step, min(max_step, d))
                cmd.append(c + d)

            s = egm_pb2.EgmSensor()
            s.header.seqno = seq; seq += 1
            s.header.tm = int(time.time()*1000) & 0xFFFFFFFF
            s.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
            s.planned.joints.joints.extend(cmd)
            sock.sendto(s.SerializeToString(), addr)

    # --------------------------- on /joint_states (informational only) ---

    def _on_js(self, msg: JointState) -> None:
        pass  # not used; feedback comes from EGM directly

    # --------------------------- action callbacks ------------------------

    def _goal_cb(self, _goal_request) -> GoalResponse:
        if self._active_goal.locked():
            self.get_logger().warn('rejecting goal — another active')
            return GoalResponse.REJECT
        if not self._egm_running.is_set():
            self.get_logger().warn('rejecting goal — EGM session not up')
            return GoalResponse.REJECT
        return GoalResponse.ACCEPT

    def _cancel_cb(self, _goal_handle) -> CancelResponse:
        return CancelResponse.ACCEPT

    def _execute_cb(self, goal_handle):
        if not self._active_goal.acquire(blocking=False):
            goal_handle.abort()
            result = FollowJointTrajectory.Result()
            result.error_code = result.PATH_TOLERANCE_VIOLATED
            return result
        try:
            return self._do_execute(goal_handle)
        finally:
            self._active_goal.release()

    def _do_execute(self, goal_handle):
        traj = goal_handle.request.trajectory
        names = list(traj.joint_names)
        pts = list(traj.points)
        result = FollowJointTrajectory.Result()
        if not pts:
            goal_handle.abort()
            result.error_code = result.INVALID_GOAL
            return result
        try:
            idx_map = [names.index(n) for n in JOINT_NAMES]
        except ValueError as e:
            self.get_logger().error(f'missing joint: {e}')
            goal_handle.abort()
            result.error_code = result.INVALID_JOINTS
            return result

        target_rad = [pts[-1].positions[i] for i in idx_map]
        target_deg = [v * 180.0 / math.pi for v in target_rad]
        self.get_logger().info(
            f'goal: J(deg)=[{", ".join(f"{v:+.1f}" for v in target_deg)}]'
        )

        # Switch target in the persistent EGM loop
        with self._egm_lock:
            self._target_deg = list(target_deg)

        # Wait for convergence
        tol = float(self.get_parameter('tolerance_deg').value)
        timeout = float(self.get_parameter('safety_timeout_s').value)
        settle_need = int(self.get_parameter('settle_ticks').value)

        started = time.monotonic()
        settle = 0
        min_err = 999.0
        last_log = 0.0
        last_progress_err = None
        last_progress_t = started

        while time.monotonic() - started < timeout:
            if goal_handle.is_cancel_requested:
                self.get_logger().info('cancel honored')
                with self._egm_lock:
                    # freeze target at current feedback
                    if self._feedback_deg:
                        self._target_deg = list(self._feedback_deg)
                goal_handle.canceled()
                result.error_code = result.GOAL_TOLERANCE_VIOLATED
                return result

            with self._egm_lock:
                fb = list(self._feedback_deg) if self._feedback_deg else None
            if fb is None:
                time.sleep(0.05)
                continue
            err = max(abs(c - t) for c, t in zip(fb, target_deg))
            if err < min_err:
                min_err = err
            if err < tol:
                settle += 1
                if settle >= settle_need:
                    self.get_logger().info(f'arrived (err {err:.2f}°)')
                    goal_handle.succeed()
                    result.error_code = result.SUCCESSFUL
                    return result
            else:
                settle = 0

            now = time.monotonic()
            if now - last_log > 1.0:
                last_log = now
                print(f'  err={err:5.2f}°  min={min_err:5.2f}°  settle={settle}/{settle_need}', flush=True)
                fbk = FollowJointTrajectory.Feedback()
                fbk.joint_names = JOINT_NAMES
                fbk.actual.positions = [v * math.pi / 180.0 for v in fb]
                goal_handle.publish_feedback(fbk)
                # Stall detection: if err hasn't dropped for 10 sec, give up
                if last_progress_err is None or err < last_progress_err - 0.5:
                    last_progress_err = err
                    last_progress_t = now
                elif now - last_progress_t > 15.0:
                    self.get_logger().warn(f'stall: err stuck at {err:.2f}° for 15s')
                    break

            time.sleep(0.01)

        self.get_logger().warn(f'timeout/stall, min_err={min_err:.2f}°')
        goal_handle.abort()
        result.error_code = result.GOAL_TOLERANCE_VIOLATED
        return result


def main() -> None:
    rclpy.init()
    node = JtcEgmStub()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
