"""preview_confirm - MANUAL mode as "move the ghost, press OK, the robot follows".

The headset's grab handle streams its pose on /metamove/preview_target. This node turns
the handle's MOTION (not its pose - see preview_core.relative_target) into a TCP target,
clamps it into the safety box, solves IK with MoveIt and publishes the joints for the
ghost robot (/metamove/preview_joints). The real robot does not move.

"confirm" on /metamove/preview_cmd plans a straight Cartesian line from the real TCP to
the previewed pose, checks every point against the box and for joint jumps, and executes
it through /execute_trajectory - i.e. through jtc_servo_relay with its TCP-speed cap -
at the user's max speed. "reset" drops the preview. Only active in MANUAL.

  in : /metamove/preview_target (PoseStamped, base_link)   /metamove/preview_cmd (String)
       /robot/safety_box  /robot/scaler_state  /robot/max_speed  /joint_states
  out: /metamove/preview_joints (Float64MultiArray)  /metamove/preview_state (String JSON)
"""
from __future__ import annotations

import json
import math
import os
import sys
import threading
import time

import rclpy
from geometry_msgs.msg import PoseStamped
from moveit_msgs.action import ExecuteTrajectory
from moveit_msgs.msg import PositionIKRequest, RobotState
from moveit_msgs.srv import GetCartesianPath, GetPositionFK, GetPositionIK
from rcl_interfaces.msg import Parameter, ParameterType, ParameterValue
from rcl_interfaces.srv import SetParameters
from rclpy.action import ActionClient
from rclpy.callback_groups import ReentrantCallbackGroup
from rclpy.executors import MultiThreadedExecutor
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Float32, Float64MultiArray, String

sys.path.insert(0, os.path.dirname(os.path.realpath(__file__)))
from preview_core import clamp_to_box, inside_box, max_joint_jump, relative_target  # noqa: E402
from tcp_fk import tcp_position  # noqa: E402

J = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
GRAB_GAP_S = 0.4          # handle silent this long -> the next message starts a new grab


class PreviewConfirm(Node):
    def __init__(self) -> None:
        super().__init__('preview_confirm')
        self.declare_parameter('max_config_jump_deg', 45.0)   # preview IK may not flip the arm
        self.declare_parameter('line_max_jump_deg', 10.0)     # per 5 mm step on the executed line
        # Apply only the hand's turn about the vertical axis (tool keeps pointing down).
        self.declare_parameter('yaw_only', True)
        self._lock = threading.Lock()
        self._joints: list[float] | None = None
        self._box = None
        self._manual = False
        self._max_speed = 0.5
        self._h0 = None            # handle pose at grab start
        self._t0 = None            # TCP pose the grab is applied to
        self._last_handle_t = 0.0
        self._handle = None
        self._target = None        # clamped target pose (p, q)
        self._preview_q: list[float] | None = None
        self._busy = False         # executing
        self._ik_in_flight = False
        self._state = {'state': 'idle', 'msg': ''}
        # Reentrant everywhere: callbacks wait on service futures (FK at grab start), which
        # the executor must be free to complete meanwhile.
        cg = ReentrantCallbackGroup()

        self.create_subscription(PoseStamped, '/metamove/preview_target', self._on_handle, 10, callback_group=cg)
        self.create_subscription(String, '/metamove/preview_cmd', self._on_cmd, 10, callback_group=cg)
        self.create_subscription(Float64MultiArray, '/robot/safety_box',
                                 lambda m: setattr(self, '_box', (tuple(m.data[:3]), tuple(m.data[3:6]))), 10, callback_group=cg)
        self.create_subscription(String, '/robot/scaler_state', self._on_scaler, 10, callback_group=cg)
        self.create_subscription(Float32, '/robot/max_speed', lambda m: setattr(self, '_max_speed', float(m.data)), 10, callback_group=cg)
        self.create_subscription(JointState, '/joint_states', self._on_js, 10, callback_group=cg)

        self._joints_pub = self.create_publisher(Float64MultiArray, '/metamove/preview_joints', 10)
        self._state_pub = self.create_publisher(String, '/metamove/preview_state', 10)
        self._ik = self.create_client(GetPositionIK, '/compute_ik', callback_group=cg)
        self._fk = self.create_client(GetPositionFK, '/compute_fk', callback_group=cg)
        self._cart = self.create_client(GetCartesianPath, '/compute_cartesian_path', callback_group=cg)
        self._exec = ActionClient(self, ExecuteTrajectory, '/execute_trajectory', callback_group=cg)
        self._relay_params = self.create_client(SetParameters, '/joint_trajectory_controller/set_parameters', callback_group=cg)
        self.create_timer(0.05, self._tick, callback_group=cg)
        self.create_timer(0.25, self._publish_state, callback_group=cg)
        self.get_logger().info('preview_confirm up - move the ghost, "confirm" moves the robot')

    # --- inputs --------------------------------------------------------------
    def _on_js(self, m: JointState) -> None:
        try:
            self._joints = [m.position[list(m.name).index(n)] for n in J]
        except ValueError:
            pass

    def _on_scaler(self, m: String) -> None:
        manual = json.loads(m.data).get('mode') == 'MANUAL'
        if manual != self._manual:
            self._manual = manual
            self._reset('entered MANUAL' if manual else 'left MANUAL')

    def _on_handle(self, m: PoseStamped) -> None:
        if not self._manual or self._busy:
            self.get_logger().info('handle pose ignored: not in MANUAL' if not self._manual else 'handle pose ignored: executing',
                                   throttle_duration_sec=5.0)
            return
        p, o = m.pose.position, m.pose.orientation
        pose = ((p.x, p.y, p.z), (o.x, o.y, o.z, o.w))
        now = time.monotonic()
        with self._lock:
            if now - self._last_handle_t > GRAB_GAP_S or self._h0 is None:
                # New grab: apply the motion to the current preview, else to the real TCP.
                t0 = self._target or self._real_tcp_pose()
                if t0 is None:
                    self.get_logger().warn('grab start without a real TCP pose (no /joint_states or /compute_fk) - ignored',
                                           throttle_duration_sec=2.0)
                    return
                self._h0, self._t0 = pose, t0
                self.get_logger().info(f'grab started at TCP {tuple(round(v, 3) for v in t0[0])}')
            self._last_handle_t = now
            self._handle = pose

    def _on_cmd(self, m: String) -> None:
        cmd = m.data.strip().lower()
        if cmd == 'reset':
            self._reset('reset')
        elif cmd == 'confirm' and not self._busy:
            threading.Thread(target=self._confirm, daemon=True).start()

    # --- preview ---------------------------------------------------------------
    def _tick(self) -> None:
        if not self._manual or self._busy or self._ik_in_flight:
            return
        if self._box is None:
            self.get_logger().warn('no /robot/safety_box yet - preview waits for it', throttle_duration_sec=5.0)
            return
        with self._lock:
            if self._handle is None or self._h0 is None:
                return
            p, q = relative_target(self._h0, self._handle, self._t0,
                                   yaw_only=bool(self.get_parameter('yaw_only').value))
        p, clamped = clamp_to_box(p, *self._box)
        seed = self._preview_q or self._joints
        if seed is None:
            return
        req = GetPositionIK.Request()
        req.ik_request = PositionIKRequest()
        req.ik_request.group_name = 'manipulator'
        target = PoseStamped()
        target.header.frame_id = 'base_link'
        target.pose.position.x, target.pose.position.y, target.pose.position.z = p
        (target.pose.orientation.x, target.pose.orientation.y,
         target.pose.orientation.z, target.pose.orientation.w) = q
        req.ik_request.pose_stamped = target
        req.ik_request.avoid_collisions = True
        req.ik_request.timeout.nanosec = 50_000_000
        req.ik_request.robot_state = RobotState(joint_state=JointState(name=J, position=list(seed)))
        self._ik_in_flight = True
        self._ik.call_async(req).add_done_callback(lambda f: self._on_ik(f, (p, q), clamped, seed))

    def _on_ik(self, fut, pose, clamped: bool, seed: list[float]) -> None:
        self._ik_in_flight = False
        res = fut.result()
        if res is None or res.error_code.val != 1:
            self._set_state('unreachable', 'no IK solution there - move the handle back')
            return
        sol = dict(zip(res.solution.joint_state.name, res.solution.joint_state.position))
        q = [sol[n] for n in J]
        jump = math.degrees(max_joint_jump(q, seed))
        if jump > float(self.get_parameter('max_config_jump_deg').value):
            self._set_state('unreachable', f'arm would flip ({jump:.0f} deg) - move the handle back')
            return
        self._preview_q, self._target = q, pose
        self._joints_pub.publish(Float64MultiArray(data=q))
        self._set_state('preview', 'held at the working-area edge' if clamped else 'press OK to move the robot')

    # --- confirm -----------------------------------------------------------------
    def _confirm(self) -> None:
        if self._target is None or self._joints is None or self._box is None:
            self._set_state('rejected', 'nothing to confirm - grab the handle first')
            return
        self._busy = True
        try:
            self._set_state('executing', 'planning a straight line')
            fk = self._wait(self._fk.call_async(self._fk_req(self._joints)))
            if fk is None:
                self._set_state('rejected', 'FK failed')
                return
            req = GetCartesianPath.Request()
            req.header.frame_id = 'base_link'
            req.group_name, req.link_name = 'manipulator', 'tool0'
            req.start_state = RobotState(joint_state=JointState(name=J, position=list(self._joints)))
            goal = PoseStamped().pose
            (goal.position.x, goal.position.y, goal.position.z) = self._target[0]
            (goal.orientation.x, goal.orientation.y, goal.orientation.z, goal.orientation.w) = self._target[1]
            req.waypoints = [goal]
            req.max_step = 0.005
            req.avoid_collisions = True
            req.max_velocity_scaling_factor, req.max_acceleration_scaling_factor = 0.3, 0.1
            res = self._wait(self._cart.call_async(req))
            if res is None or res.fraction < 0.999:
                self._set_state('rejected', f'straight line only {0 if res is None else res.fraction:.0%} possible')
                return
            jt = res.solution.joint_trajectory
            idx = [list(jt.joint_names).index(n) for n in J]
            pts = [[p.positions[i] for i in idx] for p in jt.points]
            lo, hi = self._box
            start_inside = inside_box(tuple(tcp_position(pts[0])), lo, hi)
            for a, b in zip(pts, pts[1:]):
                if math.degrees(max_joint_jump(a, b)) > float(self.get_parameter('line_max_jump_deg').value):
                    self._set_state('rejected', 'the line would flip the wrist - choose another pose')
                    return
            if start_inside and not all(inside_box(tuple(tcp_position(q)), lo, hi) for q in pts):
                self._set_state('rejected', 'the line leaves the working area')
                return
            if not start_inside:
                self.get_logger().warn('robot starts outside the working area - only the target is checked')
            self._set_live_speed(self._max_speed)
            self._set_state('executing', f'moving ({len(pts)} pts, {self._max_speed:.0%})')
            gh = self._wait(self._exec.send_goal_async(ExecuteTrajectory.Goal(trajectory=res.solution)))
            if gh is None or not gh.accepted:
                self._set_state('rejected', 'execution not accepted')
                return
            r = self._wait(gh.get_result_async(), timeout=300.0)
            code = r.result.error_code.val if r is not None else -1
            if code == 1:
                self._set_state('done', 'robot at the previewed pose')
                self._reset_grab()
            else:
                self._set_state('rejected', f'execution error {code} (RAPID running? motors on?)')
        finally:
            self._busy = False

    # --- helpers -------------------------------------------------------------------
    def _real_tcp_pose(self):
        if self._joints is None or not self._fk.service_is_ready():
            return None
        fut = self._fk.call_async(self._fk_req(self._joints))
        res = self._wait(fut, timeout=2.0)
        if res is None or not res.pose_stamped:
            return None
        p, o = res.pose_stamped[0].pose.position, res.pose_stamped[0].pose.orientation
        return (p.x, p.y, p.z), (o.x, o.y, o.z, o.w)

    def _fk_req(self, q):
        r = GetPositionFK.Request()
        r.header.frame_id = 'base_link'
        r.fk_link_names = ['tool0']
        r.robot_state = RobotState(joint_state=JointState(name=J, position=list(q)))
        return r

    def _set_live_speed(self, v: float) -> None:
        if not self._relay_params.service_is_ready():
            return
        req = SetParameters.Request()
        req.parameters = [Parameter(name='live_speed', value=ParameterValue(
            type=ParameterType.PARAMETER_DOUBLE, double_value=float(v)))]
        self._relay_params.call_async(req)

    @staticmethod
    def _wait(fut, timeout: float = 10.0):
        t0 = time.monotonic()
        while not fut.done():
            if time.monotonic() - t0 > timeout:
                return None
            time.sleep(0.01)
        return fut.result()

    def _reset_grab(self) -> None:
        with self._lock:
            self._h0 = self._t0 = self._handle = self._target = None
            self._preview_q = None

    def _reset(self, why: str) -> None:
        self._reset_grab()
        self._set_state('idle', why)

    def _set_state(self, state: str, msg: str) -> None:
        if (state, msg) != (self._state['state'], self._state['msg']):
            self.get_logger().info(f'{state}: {msg}')
        self._state = {'state': state, 'msg': msg}
        self._publish_state()

    def _publish_state(self) -> None:
        self._state_pub.publish(String(data=json.dumps(self._state)))


def main() -> None:
    rclpy.init()
    node = PreviewConfirm()
    executor = MultiThreadedExecutor(num_threads=4)
    executor.add_node(node)
    try:
        executor.spin()
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == '__main__':
    main()
