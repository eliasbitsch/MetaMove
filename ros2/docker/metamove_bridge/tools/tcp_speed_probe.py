"""Peak TCP speed of the taught path, planned but never executed.

Plans every waypoint-to-waypoint segment of dpp_waypoints.yaml with MoveIt
(plan only, same planner/velocity/acceleration scaling as dpp_playback), samples
the trajectory at 250 Hz with /compute_fk on tool0 and reports the peak Cartesian
TCP speed for each relay time_scale given. Used to turn "x2 ran clean, x1.5 tripped
the SafeMove tool speed supervision" into a TCP speed cap for jtc_servo_relay.

  python3 tcp_speed_probe.py [--scales 2.0 1.5] [--vel 0.3] [--acc 0.1] [--runs 3]

RRTConnect is randomized, so each segment is planned --runs times; the max counts.
"""
from __future__ import annotations

import argparse
import math

import rclpy
import yaml
from moveit_msgs.msg import Constraints, JointConstraint, MotionPlanRequest, RobotState
from moveit_msgs.srv import GetMotionPlan, GetPositionFK
from rclpy.node import Node
from sensor_msgs.msg import JointState

JOINTS = [f"joint_{i}" for i in range(1, 7)]
WAYPOINTS = "/opt/metamove_ws/src/metamove_bridge/dpp_waypoints.yaml"


def call(node, cli, req):
    fut = cli.call_async(req)
    rclpy.spin_until_future_complete(node, fut, timeout_sec=10)
    return fut.result()


def plan(node, cli, start, goal, vel, acc):
    req = GetMotionPlan.Request()
    r = MotionPlanRequest()
    r.group_name = "manipulator"
    r.planner_id = "RRTConnectkConfigDefault"
    r.num_planning_attempts = 1
    r.allowed_planning_time = 5.0
    r.max_velocity_scaling_factor = vel
    r.max_acceleration_scaling_factor = acc
    r.start_state = RobotState(joint_state=JointState(name=JOINTS, position=list(start)))
    c = Constraints()
    for n, q in zip(JOINTS, goal):
        c.joint_constraints.append(JointConstraint(joint_name=n, position=float(q),
                                                   tolerance_above=1e-3, tolerance_below=1e-3, weight=1.0))
    r.goal_constraints.append(c)
    req.motion_plan_request = r
    res = call(node, cli, req).motion_plan_response
    if res.error_code.val != 1:
        return None
    return res.trajectory.joint_trajectory


def sample(traj, dt):
    """Linear resample of a JointTrajectory at dt (s) in the unstretched timeline."""
    pts = [(p.time_from_start.sec + p.time_from_start.nanosec * 1e-9, list(p.positions)) for p in traj.points]
    idx = [traj.joint_names.index(j) for j in JOINTS]
    out, t, k = [], 0.0, 0
    while t <= pts[-1][0]:
        while k + 1 < len(pts) and pts[k + 1][0] < t:
            k += 1
        (t0, q0), (t1, q1) = pts[k], pts[min(k + 1, len(pts) - 1)]
        a = 0.0 if t1 <= t0 else (t - t0) / (t1 - t0)
        q = [q0[i] + a * (q1[i] - q0[i]) for i in range(len(q0))]
        out.append([q[i] for i in idx])
        t += dt
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--scales", type=float, nargs="+", default=[2.0, 1.5])
    ap.add_argument("--vel", type=float, default=0.3)
    ap.add_argument("--acc", type=float, default=0.1)
    ap.add_argument("--runs", type=int, default=3)
    args = ap.parse_args()

    rclpy.init()
    node = Node("tcp_speed_probe")
    plan_cli = node.create_client(GetMotionPlan, "/plan_kinematic_path")
    fk_cli = node.create_client(GetPositionFK, "/compute_fk")
    plan_cli.wait_for_service(timeout_sec=10)
    fk_cli.wait_for_service(timeout_sec=10)

    wps = [w["joints"] for w in yaml.safe_load(open(WAYPOINTS))["waypoints"]]
    segments = list(zip(wps, wps[1:] + wps[:1]))
    dt = 1.0 / 250.0
    peak = 0.0          # m/s in the unstretched timeline
    for si, (a, b) in enumerate(segments):
        seg_peak = 0.0
        for _ in range(args.runs):
            traj = plan(node, plan_cli, a, b, args.vel, args.acc)
            if traj is None:
                continue
            qs = sample(traj, dt)
            req = GetPositionFK.Request()
            req.header.frame_id = "base_link"
            req.fk_link_names = ["tool0"]
            prev = None
            for q in qs:
                req.robot_state = RobotState(joint_state=JointState(name=JOINTS, position=q))
                p = call(node, fk_cli, req).pose_stamped[0].pose.position
                if prev is not None:
                    seg_peak = max(seg_peak, math.dist((p.x, p.y, p.z), prev) / dt)
                prev = (p.x, p.y, p.z)
        print(f"segment {si + 1}->{(si + 1) % len(segments) + 1}: peak {seg_peak:.3f} m/s (unstretched)")
        peak = max(peak, seg_peak)
    for s in args.scales:
        print(f"time_scale x{s:g}: peak TCP {peak / s * 1000:.0f} mm/s")
    rclpy.shutdown()


if __name__ == "__main__":
    main()
