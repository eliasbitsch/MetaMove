"""One-shot JOINT jog test via rosbridge — bypasses IK/singularity.

Switches servo to JOINT_JOG, drives joint_1 at a small velocity for a short
time, then stops and switches back to TWIST. Proves the full chain
(servo -> /servo_node/commands -> EGM bridge -> controller) actually moves the
robot, independent of the Cartesian wrist-singularity damping.
"""
from __future__ import annotations
import argparse, time, roslibpy

JOINT_JOG, TWIST = 0, 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rosbridge-host", default="127.0.0.1")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--joint", default="joint_1")
    ap.add_argument("--vel", type=float, default=0.1, help="rad/s")
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

    switch = roslibpy.Service(ros, "/servo_node/switch_command_type",
                              "moveit_msgs/ServoCommandType")
    print("switch -> JOINT_JOG:",
          switch.call(roslibpy.ServiceRequest({"command_type": JOINT_JOG}),
                      timeout=10).get("success"))

    pub = roslibpy.Topic(ros, "/servo_node/delta_joint_cmds",
                         "control_msgs/JointJog")
    pub.advertise()

    print(f"jogging {args.joint} @ {args.vel} rad/s for {args.secs}s ...")
    period = 1.0 / 50.0
    end = time.monotonic() + args.secs
    while time.monotonic() < end:
        now = time.time(); sec = int(now)
        pub.publish({
            "header": {"stamp": {"sec": sec, "nanosec": int((now - sec) * 1e9)},
                       "frame_id": "base_link"},
            "joint_names": [args.joint],
            "velocities": [args.vel],
            "displacements": [],
            "duration": 0.0,
        })
        time.sleep(period)

    # stop: publish zero velocity briefly
    for _ in range(15):
        now = time.time(); sec = int(now)
        pub.publish({
            "header": {"stamp": {"sec": sec, "nanosec": int((now - sec) * 1e9)},
                       "frame_id": "base_link"},
            "joint_names": [args.joint], "velocities": [0.0],
            "displacements": [], "duration": 0.0,
        })
        time.sleep(period)
    print("stopped.")
    pub.unadvertise()
    # leave servo back in TWIST for normal teleop
    switch.call(roslibpy.ServiceRequest({"command_type": TWIST}), timeout=10)
    ros.terminate()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
