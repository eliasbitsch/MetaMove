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
import random
import threading
import time
from pathlib import Path

import rclpy
from rclpy.action import ActionClient
from rclpy.node import Node
from std_msgs.msg import Int32
from std_srvs.srv import Trigger

from moveit_msgs.action import MoveGroup
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

        self._paused = False
        self._goal_handle = None
        self._goal_lock = threading.Lock()
        self._stop = False
        self._go_home = False

        self._waypoints = self._load_waypoints()
        if not self._waypoints:
            raise RuntimeError('no waypoints loaded — run dpp_teach first')
        self.get_logger().info(f'loaded {len(self._waypoints)} waypoints')

        self._mg_client = ActionClient(self, MoveGroup, '/move_action')
        self._wp_pub = self.create_publisher(Int32, '/dpp/wp_index', 10)
        self.create_service(Trigger, '~/pause', self._svc_pause)
        self.create_service(Trigger, '~/resume', self._svc_resume)
        self.create_service(Trigger, '~/stop', self._svc_stop)
        self.create_service(Trigger, '~/home', self._svc_home)

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

    def _svc_pause(self, _req, resp):
        self._paused = True
        with self._goal_lock:
            if self._goal_handle is not None:
                self._goal_handle.cancel_goal_async()
        resp.success = True
        resp.message = 'paused — cancelling current goal'
        return resp

    def _svc_resume(self, _req, resp):
        self._paused = False
        resp.success = True
        resp.message = 'resumed'
        return resp

    def _svc_stop(self, _req, resp):
        self._stop = True
        self._paused = True
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

    def _execute_one(self, wp: dict) -> bool:
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
                self._execute_one({'name': 'HOME',
                                   'joints': [0.0, 0.0, 0.0, 0.0, 1.5707963, 0.0]})
                continue
            if self._paused:
                time.sleep(0.2)
                continue
            wp = self._waypoints[order[i]]
            v = float(self.get_parameter('velocity_scaling').value)
            self.get_logger().info(
                f'pass {pass_count} · {wp["name"]} · v={v:.2f} · ok={ok_count} fail={fail_count}'
            )
            success = self._execute_one(wp)
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
