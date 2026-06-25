"""Inject a known joint target into /servo_node/commands, bypassing servo+IK.

The EGM bridge subscribes to /servo_node/commands (Float64MultiArray, 6 joints
in rad) and forwards as EGM joint corrections. By publishing a target =
current + offset on one joint directly, we test whether the CONTROLLER applies
EGM joint corrections at all — independent of servo's Cartesian singularity
damping.

Servo also publishes hold-position on the same topic, so the robot will jitter
between current and current+offset; any visible motion proves EGM works.

  python inject_command.py --joint-index 0 --offset-deg 2 --secs 2.5
"""
from __future__ import annotations
import argparse, math, time, roslibpy

D2R = math.pi / 180.0
R2D = 180.0 / math.pi


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rosbridge-host", default="127.0.0.1")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--joint-index", type=int, default=0, help="0..5")
    ap.add_argument("--offset-deg", type=float, default=2.0)
    ap.add_argument("--secs", type=float, default=2.5)
    args = ap.parse_args()

    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        print("rosbridge not reachable"); return 1
    print("connected")

    cur = {"q": None}

    def on_js(m):
        cur["q"] = list(m["position"])

    sub = roslibpy.Topic(ros, "/joint_states", "sensor_msgs/JointState")
    sub.subscribe(on_js)
    t0 = time.monotonic()
    while cur["q"] is None and time.monotonic() - t0 < 5:
        time.sleep(0.05)
    sub.unsubscribe()
    if cur["q"] is None:
        print("no /joint_states"); return 2
    base = list(cur["q"])
    print(f"current (deg) = {[round(x*R2D,2) for x in base]}")

    target = list(base)
    target[args.joint_index] = base[args.joint_index] + args.offset_deg * D2R
    print(f"target  (deg) = {[round(x*R2D,2) for x in target]}  "
          f"(joint_{args.joint_index+1} +{args.offset_deg} deg)")

    pub = roslibpy.Topic(ros, "/servo_node/commands", "std_msgs/Float64MultiArray")
    pub.advertise()
    print(f"injecting for {args.secs}s ...")
    period = 1.0 / 50.0
    end = time.monotonic() + args.secs
    while time.monotonic() < end:
        pub.publish({"layout": {"dim": [], "data_offset": 0}, "data": target})
        time.sleep(period)
    pub.unadvertise()
    print("done.")
    ros.terminate()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
