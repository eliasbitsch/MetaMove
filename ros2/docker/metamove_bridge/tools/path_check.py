"""Dry run of the taught path - plans every segment exactly like dpp_playback would
(straight line for approach/retreat, joint space otherwise), executes nothing.

Per segment: mode, straight-line fraction/jump/end deviation, planned TCP peak, the
relay's TCP-cap stretch and the replay duration. Run after teaching, before the robot
drives the new path.

  python3 path_check.py [--scale 1.5] [--cap 0.38] [--vel 0.3] [--acc 0.1] [--lateral 0.05]
"""
from __future__ import annotations

import argparse
import math
import sys

import rclpy
import yaml
from moveit_msgs.msg import RobotState
from moveit_msgs.srv import GetCartesianPath, GetMotionPlan, GetPositionFK
from rclpy.node import Node
from sensor_msgs.msg import JointState

sys.path.insert(0, "/opt/metamove_ws/src/metamove_bridge/metamove_bridge")
sys.path.insert(0, "/opt/metamove_ws/src/metamove_bridge/tools")
from tcp_fk import peak_tcp_speed, tcp_position  # noqa: E402
import tcp_speed_probe as probe  # noqa: E402

J = probe.JOINTS


def keys_of(jt):
    idx = [list(jt.joint_names).index(n) for n in J]
    return [(p.time_from_start.sec + p.time_from_start.nanosec * 1e-9, [p.positions[i] for i in idx])
            for p in jt.points]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--scale", type=float, default=1.5)
    ap.add_argument("--cap", type=float, default=0.38)
    ap.add_argument("--vel", type=float, default=0.3)
    ap.add_argument("--acc", type=float, default=0.1)
    ap.add_argument("--lateral", type=float, default=0.05)
    a = ap.parse_args()

    rclpy.init()
    n = Node("path_check")
    plan_cli = n.create_client(GetMotionPlan, "/plan_kinematic_path")
    cart_cli = n.create_client(GetCartesianPath, "/compute_cartesian_path")
    fk_cli = n.create_client(GetPositionFK, "/compute_fk")
    for c in (plan_cli, cart_cli, fk_cli):
        c.wait_for_service(timeout_sec=10)

    wps = yaml.safe_load(open(probe.WAYPOINTS))["waypoints"]
    total, bad = 0.0, 0
    geo = []
    print(f"{'segment':14s} {'mode':6s} {'detail':34s} {'peak@x' + format(a.scale, 'g'):>10s} {'stretch':>8s} {'time':>6s}")
    for i, wp in enumerate(wps):
        prev = wps[i - 1]
        q0, q1 = prev["joints"], wp["joints"]
        p0, p1 = tcp_position(q0), tcp_position(q1)
        lateral = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        mode, detail, keys = "joint", "", None
        if lateral <= a.lateral:
            fk = GetPositionFK.Request()
            fk.header.frame_id = "base_link"
            fk.fk_link_names = ["tool0"]
            fk.robot_state = RobotState(joint_state=JointState(name=J, position=q1))
            pose = probe.call(n, fk_cli, fk).pose_stamped[0].pose
            req = GetCartesianPath.Request()
            req.header.frame_id = "base_link"
            req.group_name, req.link_name = "manipulator", "tool0"
            req.start_state = RobotState(joint_state=JointState(name=J, position=q0))
            req.waypoints, req.max_step, req.avoid_collisions = [pose], 0.005, True
            req.max_velocity_scaling_factor, req.max_acceleration_scaling_factor = a.vel, a.acc
            res = probe.call(n, cart_cli, req)
            pts = keys_of(res.solution.joint_trajectory) if res.solution.joint_trajectory.points else []
            if res.fraction >= 0.999 and pts:
                jump = max(max(abs(v - u) for u, v in zip(x[1], y[1])) for x, y in zip(pts, pts[1:]))
                end = max(abs(u - v) for u, v in zip(pts[-1][1], q1))
                if jump <= math.radians(10) and end <= math.radians(5):
                    mode, keys = "LINE", pts
                    detail = f"{math.dist(p0, p1) * 1000:.0f} mm straight, jump {math.degrees(jump):.1f} deg"
                else:
                    detail = f"line rejected: jump {math.degrees(jump):.0f} / end {math.degrees(end):.0f} deg"
            else:
                detail = f"line only {res.fraction:.0%}"
        if keys is None:
            traj = probe.plan(n, plan_cli, q0, q1, a.vel, a.acc)
            if traj is None:
                print(f"{prev['name']}->{wp['name']:6s} PLAN FAILED")
                bad += 1
                continue
            keys = keys_of(traj)
            detail = detail or f"lateral {lateral * 1000:.0f} mm, J1 {math.degrees(q1[0] - q0[0]):+.0f} deg"
        # Where the TCP actually goes along the planned motion (sampled every 10 ms).
        tcp = []
        for k in range(len(keys) - 1):
            (ta, qa), (tb, qb) = keys[k], keys[k + 1]
            steps = max(1, int((tb - ta) / 0.01))
            for s_ in range(steps):
                f = s_ / steps
                q = [x + f * (y - x) for x, y in zip(qa, qb)]
                tcp.append((tcp_position(q), q))
        tcp.append((tcp_position(keys[-1][1]), keys[-1][1]))
        zs = [p[2] for p, _ in tcp]
        radius = min(math.hypot(p[0], p[1]) for p, _ in tcp)
        j5 = min(abs(math.degrees(q[4])) for _, q in tcp)
        rot = [math.degrees(keys[-1][1][i] - keys[0][1][i]) for i in (3, 5)]
        geo.append((f"{prev['name']}->{wp['name']}", min(zs), max(zs), radius, j5, rot,
                    min(zs) < min(p0[2], p1[2]) - 0.02))
        v = peak_tcp_speed(keys)
        eff = max(a.scale, v / a.cap)
        dur = keys[-1][0] * eff
        total += dur
        print(f"{prev['name']}->{wp['name']:6s} {mode:6s} {detail:34s} {v / a.scale * 1000:7.0f} mm/s "
              f"{'x' + format(eff, '.2f') + (' cap' if eff > a.scale else ''):>8s} {dur:5.1f} s")
    print(f"lap {total:.1f} s (+ dwell), {bad} segment(s) failed")
    print()
    print(f"{'segment':14s} {'TCP z min..max':>16s} {'min radius':>11s} {'min |J5|':>9s} {'J4 / J6 turn':>14s}")
    for name, zmin, zmax, rad, j5, rot, dips in geo:
        flags = []
        if dips:
            flags.append("DIPS below both ends")
        if j5 < 10:
            flags.append("near wrist singularity")
        if rad < 0.30:
            flags.append("close to the base axis")
        print(f"{name:14s} {zmin:7.3f}..{zmax:.3f} m {rad:8.3f} m {j5:7.1f} deg {rot[0]:+6.0f} / {rot[1]:+5.0f} deg  "
              + ("  ! " + ", ".join(flags) if flags else ""))
    rclpy.shutdown()
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
