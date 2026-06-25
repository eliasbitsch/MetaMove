"""Rescue jog — free the robot from servo's collision/singularity braking.

Publishes joint targets DIRECTLY to /servo_node/commands (the bridge input),
bypassing moveit_servo's velocity scaling entirely. Use ONLY to nudge the arm
out of a zone where servo has braked to ~zero; for normal driving always use
the teleop (servo's safety scaling exists for a reason).

The move is executed in small ramped steps (default 1.5 deg per 0.8 s) so the
controller follows smoothly (EGM-side MaxSpeedDeviation still applies).

Usage:
  python rescue_jog.py --axis 3 --deg  10     # open elbow +10 deg
  python rescue_jog.py --axis 5 --deg -20     # wrist J5 -20 deg
  python rescue_jog.py --axis 1 --deg 5 --step-deg 1 --step-secs 1.0

Safety:
  - max ±20 deg per invocation (run again for more)
  - one axis per invocation
  - E-Stop in reach, workspace clear — this bypasses servo's checks!
"""
from __future__ import annotations

import argparse
import math
import sys
import time

import roslibpy

D2R = math.pi / 180.0
R2D = 180.0 / math.pi
MAX_TOTAL_DEG = 20.0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--rosbridge-host", default="127.0.0.1")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--axis", type=int, required=True,
                    help="joint number 1..6")
    ap.add_argument("--deg", type=float, required=True,
                    help=f"relative degrees, max +/-{MAX_TOTAL_DEG}")
    ap.add_argument("--step-deg", type=float, default=1.5)
    ap.add_argument("--step-secs", type=float, default=0.8)
    args = ap.parse_args()

    if not 1 <= args.axis <= 6:
        print("--axis must be 1..6"); return 1
    if abs(args.deg) > MAX_TOTAL_DEG:
        print(f"|deg| > {MAX_TOTAL_DEG} — refuse. Run multiple times instead.")
        return 1
    idx = args.axis - 1

    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        print("rosbridge not reachable"); return 1

    cur = {"q": None}

    def on_js(m):
        cur["q"] = list(m["position"])

    sub = roslibpy.Topic(ros, "/joint_states", "sensor_msgs/JointState")
    sub.subscribe(on_js)
    t0 = time.monotonic()
    while cur["q"] is None and time.monotonic() - t0 < 5:
        time.sleep(0.05)
    if cur["q"] is None:
        print("no /joint_states — is the bridge running?"); return 2

    start = list(cur["q"])
    print(f"start (deg) = {[round(v*R2D,2) for v in start]}")
    print(f"rescue: joint_{args.axis} {args.deg:+.1f} deg in "
          f"{args.step_deg} deg steps")

    pub = roslibpy.Topic(ros, "/servo_node/commands",
                         "std_msgs/Float64MultiArray")
    pub.advertise()

    moved = 0.0
    sign = 1.0 if args.deg > 0 else -1.0
    try:
        while abs(moved) < abs(args.deg):
            step = sign * min(args.step_deg, abs(args.deg) - abs(moved))
            moved += step
            # re-read current pose each step so we ramp from reality,
            # not from a stale assumption
            base = list(cur["q"]) if cur["q"] else start
            target = list(base)
            target[idx] = base[idx] + step * D2R
            end = time.monotonic() + args.step_secs
            while time.monotonic() < end:
                pub.publish({"layout": {"dim": [], "data_offset": 0},
                             "data": target})
                time.sleep(0.02)
            print(f"  joint_{args.axis} -> "
                  f"{round((cur['q'][idx] if cur['q'] else 0)*R2D,2)} deg "
                  f"({moved:+.1f}/{args.deg:+.1f})")
    except KeyboardInterrupt:
        print("abgebrochen.")
    finally:
        pub.unadvertise()
        sub.unsubscribe()
        ros.terminate()

    print("fertig — Teleop übernimmt wieder (servo hold).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
