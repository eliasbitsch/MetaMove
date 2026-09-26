"""
DPP waypoint playback — loops through teach'd waypoints via MoveIt MoveGroup.

Sends MotionPlanRequest goals to /move_action one waypoint at a time, in
teach order (sequential, non-shuffled by default). Speed is set per-plan via
`max_velocity_scaling_factor` so changing the ROS param mid-run takes effect
on the next waypoint (typical lag: 1–3 seconds at v_scale=0.5).

Params:
  waypoints_file       path to dpp_waypoints.yaml (produced by dpp_teach)
  velocity_scaling     0.05 .. 1.0 (default 0.25 — phase "normal")
  acceleration_scaling 0.05 .. 1.0 (default 0.25)
  planning_group       MoveIt planning group name (default: "manipulator")
  planner_id           OMPL planner (default: "RRTConnectkConfigDefault")
  dwell_seconds        sleep between waypoints (default 0.5)
  reshuffle_each_pass  if true, reshuffle order every full pass (default false)

Publishes the current waypoint index (Int32 on /dpp/wp_index) right after the
robot reaches each waypoint, so a visualiser (e.g. Unity PlannedPathFade) can
record the TCP world position per waypoint and fade traversed segments. The
index is the waypoint's identity (its position in dpp_waypoints.yaml), stable
across passes — in sequential mode it simply cycles 0,1,…,N-1,0,1,….

Live speed change for the 4 phases:
  ros2 param set /dpp_playback velocity_scaling 0.25   # normal
  ros2 param set /dpp_playback velocity_scaling 0.50   # bewegung
  ros2 param set /dpp_playback velocity_scaling 1.00   # schnell
  ros2 service call /dpp_playback/pause std_srvs/srv/Trigger   # stop phase
  ros2 service call /dpp_playback/resume std_srvs/srv/Trigger
"""
from __future__ import annotations

import math
import json
import random
import threading
import time
from pathlib import Path

import rclpy
from rclpy.action import ActionClient
from rclpy.node import Node
from std_msgs.msg import String, Int32
from std_srvs.srv import Trigger

import os
import sys

from moveit_msgs.action import ExecuteTrajectory, MoveGroup
from moveit_msgs.srv import GetCartesianPath, GetPositionFK
from moveit_msgs.msg import RobotState
from sensor_msgs.msg import JointState

# tcp_fk lives next to this file; realpath so it resolves from the symlinked install too.
sys.path.insert(0, os.path.dirname(os.path.realpath(__file__)))
from tcp_fk import tcp_position  # noqa: E402
from moveit_msgs.msg import (
    Constraints,
    JointConstraint,
    MotionPlanRequest,
    PlanningOptions,
)

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']


class DppPlayback(Node):
    def __init__(self) -> None:
        super().__init__('dpp_playback')

        self.declare_parameter('waypoints_file', '')
        self.declare_parameter('velocity_scaling', 0.25)
        self.declare_parameter('acceleration_scaling', 0.25)
        self.declare_parameter('planning_group', 'manipulator')
        self.declare_parameter('planner_id', 'RRTConnectkConfigDefault')
        self.declare_parameter('dwell_seconds', 0.5)
        self.declare_parameter('reshuffle_each_pass', False)
        self.declare_parameter('joint_tolerance_rad', 0.005)
        # Real robot: never start moving just because ROS came up (robot_profile.yaml).
        self.declare_parameter('start_paused', False)
        # Approach/retreat moves (TCP shifts less than this sideways, i.e. mostly up or
        # down) are driven as a straight Cartesian line through IK instead of a joint-space
        # plan, so a pick/place goes straight down and up. Falls back to joint space when
        # the line is incomplete, jumps, or ends in another arm configuration.
        self.declare_parameter('linear_enabled', True)
        self.declare_parameter('linear_max_lateral', 0.05)       # m
        self.declare_parameter('linear_max_end_dev_deg', 5.0)    # vs the taught joints
        self._joints_now: list[float] | None = None

        self._paused = False
        # Reported on /dpp/state for robot_status: running | paused | homing | at_home | stopped
        self._state = 'running'
        if bool(self.get_parameter('start_paused').value):
            self._paused, self._state = True, 'paused'
        self._wp_name = ''
        self._goal_handle = None
        self._goal_lock = threading.Lock()
        self._stop = False
        self._go_home = False
        # Every (re)start of the path goes through HOME first: the robot may have been
        # moved (MANUAL preview, Home, pendant jog) since the path last ran, and a direct
        # plan from there to the next waypoint can sweep through the cell. Set on the
        # first start, on /start, and on a resume after the arm moved while paused.
        self._restart = True
        self._interrupted = False     # the running goal was cancelled by pause/home/stop
        self._pause_pose: list[float] | None = None
        self.declare_parameter('restart_move_tol_deg', 2.0)

        self._waypoints = self._load_waypoints()
        if not self._waypoints:
            raise RuntimeError('no waypoints loaded — run dpp_teach first')
        self.get_logger().info(f'loaded {len(self._waypoints)} waypoints')

        self._mg_client = ActionClient(self, MoveGroup, '/move_action')
        self._exec_client = ActionClient(self, ExecuteTrajectory, '/execute_trajectory')
        self._cart_cli = self.create_client(GetCartesianPath, '/compute_cartesian_path')
        self._fk_cli = self.create_client(GetPositionFK, '/compute_fk')
        self.create_subscription(JointState, '/joint_states', self._on_js, 10)
        self._wp_pub = self.create_publisher(Int32, '/dpp/wp_index', 10)
        self.create_service(Trigger, '~/pause', self._svc_pause)
        self.create_service(Trigger, '~/resume', self._svc_resume)
        self.create_service(Trigger, '~/start', self._svc_start)
        self.create_service(Trigger, '~/stop', self._svc_stop)
        self.create_service(Trigger, '~/home', self._svc_home)
        self._state_pub = self.create_publisher(String, '/dpp/state', 10)
        self.create_timer(0.25, self._publish_state)

        # Worker thread runs the playback loop; main thread spins ROS.
        self._worker = threading.Thread(target=self._run, daemon=True)
        self._worker.start()

    def _load_waypoints(self) -> list[dict]:
        path_str = self.get_parameter('waypoints_file').value
        if not path_str:
            # Try package share fallback
            import ament_index_python.packages as aip
            try:
                share = aip.get_package_share_directory('metamove_bridge')
                path_str = str(Path(share) / 'config' / 'dpp_waypoints.yaml')
            except Exception:
                pass
        if not path_str or not Path(path_str).exists():
            self.get_logger().error(f'waypoints_file not found: {path_str}')
            return []
        import yaml
        data = yaml.safe_load(Path(path_str).read_text()) or {}
        wps = data.get('waypoints', [])
        valid = [w for w in wps if isinstance(w.get('joints'), list) and len(w['joints']) == 6]
        if len(valid) != len(wps):
            self.get_logger().warn(f'skipped {len(wps) - len(valid)} malformed waypoints')
        return valid

    def _publish_state(self) -> None:
        self._state_pub.publish(String(data=json.dumps({'state': self._state, 'wp': self._wp_name})))

    def _svc_pause(self, _req, resp):
        if not self._paused:
            self._pause_pose = list(self._joints_now) if self._joints_now else None
        self._paused = True
        self._interrupted = True
        if self._state != 'at_home':
            self._state = 'paused'
        with self._goal_lock:
            if self._goal_handle is not None:
                self._goal_handle.cancel_goal_async()
        resp.success = True
        resp.message = 'paused — cancelling current goal'
        return resp

    def _moved_since_pause(self) -> bool:
        if self._pause_pose is None or self._joints_now is None:
            return True
        dev = max(abs(a - b) for a, b in zip(self._pause_pose, self._joints_now))
        return math.degrees(dev) > float(self.get_parameter('restart_move_tol_deg').value)

    def _svc_resume(self, _req, resp):
        # Continue where the path stopped - unless the arm is no longer where it stopped.
        if not self._paused:
            resp.success = True
            resp.message = 'already running'
            return resp
        if self._go_home or self._state in ('homing', 'at_home', 'stopped'):
            # Home / Stop end the automatic path: only an explicit start (/start: headset
            # "Automatik", console Start) runs it again - not a headset put back on, not a
            # mode toggle racing the homing move.
            resp.success = False
            resp.message = 'path stopped (Home/Stop) - press Automatik / Start to run it again'
            return resp
        if self._state in ('at_home', 'stopped') or self._moved_since_pause():
            self._restart = True
        self._paused = False
        self._state = 'running'
        resp.success = True
        resp.message = 'resumed via HOME (arm moved since the pause)' if self._restart else 'resumed'
        return resp

    def _svc_start(self, _req, resp):
        # Fresh start (headset "Automatik", console Start): HOME first, then wp 1.
        self._restart = True
        self._stop = False
        self._paused = False
        self._state = 'running'
        resp.success = True
        resp.message = 'starting: HOME, then the path from the first waypoint'
        return resp

    def _svc_stop(self, _req, resp):
        self._stop = True
        self._paused = True
        self._state = 'stopped'
        with self._goal_lock:
            if self._goal_handle is not None:
                self._goal_handle.cancel_goal_async()
        resp.success = True
        resp.message = 'stopping after current cancel'
        return resp

    def _svc_home(self, _req, resp):
        # Stop looping, plan a smooth move to the home pose, then stay there.
        self._paused = True
        self._go_home = True
        self._interrupted = True
        with self._goal_lock:
            if self._goal_handle is not None:
                self._goal_handle.cancel_goal_async()
        resp.success = True
        resp.message = 'homing — fahre zu [0,0,0,0,90,0] und bleibe stehen'
        return resp

    def _build_goal(self, wp: dict) -> MoveGroup.Goal:
        v = float(self.get_parameter('velocity_scaling').value)
        a = float(self.get_parameter('acceleration_scaling').value)
        v = max(0.01, min(1.0, v))
        a = max(0.01, min(1.0, a))
        tol = float(self.get_parameter('joint_tolerance_rad').value)
        group = self.get_parameter('planning_group').value
        planner = self.get_parameter('planner_id').value

        constraint = Constraints(name=wp['name'])
        for name, target in zip(JOINT_NAMES, wp['joints']):
            jc = JointConstraint()
            jc.joint_name = name
            jc.position = float(target)
            jc.tolerance_above = tol
            jc.tolerance_below = tol
            jc.weight = 1.0
            constraint.joint_constraints.append(jc)

        req = MotionPlanRequest()
        req.group_name = group
        req.planner_id = planner
        req.num_planning_attempts = 5
        req.allowed_planning_time = 2.0
        req.max_velocity_scaling_factor = v
        req.max_acceleration_scaling_factor = a
        req.goal_constraints.append(constraint)

        goal = MoveGroup.Goal()
        goal.request = req
        goal.planning_options = PlanningOptions()
        goal.planning_options.plan_only = False
        goal.planning_options.look_around = False
        goal.planning_options.replan = False
        return goal

    def _on_js(self, msg: JointState) -> None:
        try:
            self._joints_now = [msg.position[list(msg.name).index(n)] for n in JOINT_NAMES]
        except ValueError:
            pass

    def _wait(self, future, timeout: float = 10.0):
        t0 = time.monotonic()
        while rclpy.ok() and not future.done():
            if time.monotonic() - t0 > timeout:
                return None
            time.sleep(0.01)
        return future.result()

    def _execute_one(self, wp: dict) -> bool:
        if bool(self.get_parameter('linear_enabled').value):
            linear = self._execute_linear(wp)
            if linear is not None:
                return linear
        return self._execute_joint(wp)

    def _execute_linear(self, wp: dict):
        """Straight TCP line for approach/retreat moves. None = not applicable, fall back."""
        q0 = self._joints_now
        if q0 is None:
            return None
        p0, p1 = tcp_position(q0), tcp_position(wp['joints'])
        lateral = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        if lateral > float(self.get_parameter('linear_max_lateral').value):
            return None
        if not (self._cart_cli.service_is_ready() and self._fk_cli.service_is_ready()):
            return None

        # Target pose = taught joints through MoveIt FK (orientation included).
        fk = GetPositionFK.Request()
        fk.header.frame_id = 'base_link'
        fk.fk_link_names = ['tool0']
        fk.robot_state = RobotState(joint_state=JointState(name=list(JOINT_NAMES), position=list(wp['joints'])))
        fk_res = self._wait(self._fk_cli.call_async(fk))
        if fk_res is None or not fk_res.pose_stamped:
            return None

        req = GetCartesianPath.Request()
        req.header.frame_id = 'base_link'
        req.group_name = self.get_parameter('planning_group').value
        req.link_name = 'tool0'
        req.start_state = RobotState(joint_state=JointState(name=list(JOINT_NAMES), position=list(q0)))
        req.waypoints = [fk_res.pose_stamped[0].pose]
        req.max_step = 0.005
        req.avoid_collisions = True
        req.max_velocity_scaling_factor = max(0.01, min(1.0, float(self.get_parameter('velocity_scaling').value)))
        req.max_acceleration_scaling_factor = max(0.01, min(1.0, float(self.get_parameter('acceleration_scaling').value)))
        res = self._wait(self._cart_cli.call_async(req))
        if res is None or res.fraction < 0.999 or not res.solution.joint_trajectory.points:
            self.get_logger().info(f'{wp["name"]}: straight line not possible '
                                   f'({0 if res is None else res.fraction:.0%}) - joint-space plan')
            return None
        jt = res.solution.joint_trajectory
        idx = [list(jt.joint_names).index(n) for n in JOINT_NAMES]
        pts = [[p.positions[i] for i in idx] for p in jt.points]
        # No joint jumps along the line, and it must end in the taught configuration.
        jump = max((max(abs(b - a) for a, b in zip(u, v)) for u, v in zip(pts, pts[1:])), default=0.0)
        end_dev = max(abs(a - b) for a, b in zip(pts[-1], wp['joints']))
        if jump > math.radians(10) or end_dev > math.radians(float(self.get_parameter('linear_max_end_dev_deg').value)):
            self.get_logger().info(f'{wp["name"]}: straight line rejected (jump {math.degrees(jump):.1f} deg, '
                                   f'end off {math.degrees(end_dev):.1f} deg) - joint-space plan')
            return None

        self.get_logger().info(f'{wp["name"]}: straight line ({math.dist(p0, p1) * 1000:.0f} mm, '
                               f'{len(pts)} pts)')
        goal = ExecuteTrajectory.Goal()
        goal.trajectory = res.solution
        if not self._exec_client.wait_for_server(timeout_sec=5.0):
            return None
        gh = self._wait(self._exec_client.send_goal_async(goal))
        if gh is None or not gh.accepted:
            self.get_logger().warn(f'{wp["name"]}: straight line rejected by /execute_trajectory')
            return False
        with self._goal_lock:
            self._goal_handle = gh
        r = self._wait(gh.get_result_async(), timeout=600.0)
        with self._goal_lock:
            self._goal_handle = None
        code = r.result.error_code.val if r is not None and r.result and r.result.error_code else -1
        if code == 1:
            return True
        self.get_logger().warn(f'{wp["name"]}: straight line execution error_code={code}')
        return False

    def _execute_joint(self, wp: dict) -> bool:
        goal = self._build_goal(wp)
        if not self._mg_client.wait_for_server(timeout_sec=5.0):
            self.get_logger().error('/move_action server not available')
            return False
        send_future = self._mg_client.send_goal_async(goal)
        while rclpy.ok() and not send_future.done():
            time.sleep(0.02)
        gh = send_future.result()
        if gh is None or not gh.accepted:
            self.get_logger().warn(f'{wp["name"]}: goal rejected')
            return False
        with self._goal_lock:
            self._goal_handle = gh
        result_future = gh.get_result_async()
        while rclpy.ok() and not result_future.done():
            time.sleep(0.02)
        with self._goal_lock:
            self._goal_handle = None
        res = result_future.result()
        if res is None:
            return False
        code = res.result.error_code.val if res.result and res.result.error_code else -1
        if code == 1:  # SUCCESS
            return True
        self.get_logger().warn(f'{wp["name"]}: MoveGroup error_code={code}')
        return False

    def _run(self) -> None:
        time.sleep(2.0)  # let MoveIt come up
        order = list(range(len(self._waypoints)))
        if bool(self.get_parameter('reshuffle_each_pass').value):
            random.shuffle(order)
        i = 0
        pass_count = 0
        ok_count = 0
        fail_count = 0
        while rclpy.ok() and not self._stop:
            if self._go_home:
                self._go_home = False
                self._paused = True
                self.get_logger().info('HOME — fahre zu [0,0,0,0,90,0] und bleibe stehen')
                self._state, self._wp_name = 'homing', 'HOME'
                self._execute_one({'name': 'HOME',
                                   'joints': [0.0, 0.0, 0.0, 0.0, 1.5707963, 0.0]})
                if self._paused and self._state == 'homing':
                    self._state = 'at_home'
                continue
            if self._paused:
                time.sleep(0.2)
                continue
            if self._restart:
                self.get_logger().info('START via HOME - fahre zu [0,0,0,0,90,0], dann ab dem ersten Wegpunkt')
                self._state, self._wp_name = 'homing', 'HOME'
                ok = self._execute_one({'name': 'HOME',
                                        'joints': [0.0, 0.0, 0.0, 0.0, 1.5707963, 0.0]})
                if ok and not self._paused:
                    self._restart = False
                    i = 0
                elif not self._paused:
                    # HOME could not be planned/executed: do not retry in a tight loop and
                    # never skip it - hold paused, the next start tries HOME again.
                    self.get_logger().error('START via HOME failed - path held (paused)')
                    self._paused, self._state = True, 'paused'
                # Paused on the way (headset off, too close): stay in restart mode,
                # the next resume goes HOME again.
                continue
            wp = self._waypoints[order[i]]
            self._state, self._wp_name = 'running', wp['name']
            v = float(self.get_parameter('velocity_scaling').value)
            self.get_logger().info(
                f'pass {pass_count} · {wp["name"]} · v={v:.2f} · ok={ok_count} fail={fail_count}'
            )
            self._interrupted = False
            success = self._execute_one(wp)
            if not success and self._interrupted:
                # Cancelled by a pause - same target again on resume, not the next one
                # (even if the resume came before the cancelled goal reported back).
                continue
            if success:
                ok_count += 1
                # Announce the reached waypoint's identity (stable index into the
                # yaml) so visualisers can record TCP pose + fade traversed segments.
                self._wp_pub.publish(Int32(data=int(order[i])))
            else:
                fail_count += 1
            dwell = float(self.get_parameter('dwell_seconds').value)
            if dwell > 0 and not self._paused:
                time.sleep(dwell)
            i += 1
            if i >= len(order):
                pass_count += 1
                i = 0
                if bool(self.get_parameter('reshuffle_each_pass').value):
                    random.shuffle(order)
        self.get_logger().info(f'playback done — ok={ok_count} fail={fail_count}')


def main() -> None:
    rclpy.init()
    try:
        node = DppPlayback()
    except RuntimeError as e:
        print(f'fatal: {e}')
        rclpy.shutdown()
        return
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
