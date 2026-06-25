"""
Keyboard teleop for moveit_servo via rosbridge_websocket.

Publishes TwistStamped to /servo_node/delta_twist_cmds at 50 Hz while a key is
held. Stops automatically when no key has been pressed for >100 ms (servo also
times out at 500 ms anyway).

Bindings (base_link frame, EE jogging):
  w/s : +x / -x   (1 cm/s)
  a/d : +y / -y
  q/e : +z / -z
  i/k : +rx / -rx (0.2 rad/s ~= 11 deg/s)
  j/l : +ry / -ry
  u/o : +rz / -rz
  shift : 5x speed
  space : emergency zero (publish 0 twist for 0.2s)
  esc / Ctrl+C : exit
"""
from __future__ import annotations

import argparse
import sys
import threading
import time

import roslibpy

try:
    import keyboard  # type: ignore
except ImportError:
    print("pip install keyboard", file=sys.stderr)
    sys.exit(1)


KEYS = {
    "w": ("lin", 0,  1.0),  "s": ("lin", 0, -1.0),
    "a": ("lin", 1,  1.0),  "d": ("lin", 1, -1.0),
    "q": ("lin", 2,  1.0),  "e": ("lin", 2, -1.0),
    "i": ("ang", 0,  1.0),  "k": ("ang", 0, -1.0),
    "j": ("ang", 1,  1.0),  "l": ("ang", 1, -1.0),
    "u": ("ang", 2,  1.0),  "o": ("ang", 2, -1.0),
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--rosbridge-host", default="192.168.125.99")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--linear", type=float, default=0.30,
                    help="m/s baseline linear speed (per-axis)")
    ap.add_argument("--angular", type=float, default=2.0,
                    help="rad/s baseline angular speed (per-axis)")
    args = ap.parse_args()

    print(f"connecting rosbridge ws://{args.rosbridge_host}:{args.rosbridge_port}")
    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    deadline = time.monotonic() + 10
    while not ros.is_connected and time.monotonic() < deadline:
        time.sleep(0.1)
    if not ros.is_connected:
        print("rosbridge not reachable")
        return 1

    pub = roslibpy.Topic(ros, "/servo_node/delta_twist_cmds",
                         "geometry_msgs/TwistStamped")
    pub.advertise()

    print(f"keyboard teleop active  lin={args.linear} m/s  ang={args.angular} rad/s")
    print("hold w/a/s/d/q/e for translate, i/j/k/l/u/o for rotate, shift=5x, esc=quit")

    running = True

    def _on_esc(_event):
        nonlocal running
        running = False

    keyboard.on_press_key("esc", _on_esc)

    try:
        period = 1.0 / 50.0
        next_t = time.monotonic()
        last_dbg = time.monotonic()
        while running:
            scale = 5.0 if keyboard.is_pressed("shift") else 1.0
            lin = [0.0, 0.0, 0.0]
            ang = [0.0, 0.0, 0.0]
            held = []
            for k, (axis_kind, idx, sign) in KEYS.items():
                if keyboard.is_pressed(k):
                    held.append(k)
                    if axis_kind == "lin":
                        lin[idx] += sign * args.linear * scale
                    else:
                        ang[idx] += sign * args.angular * scale
            now_dbg = time.monotonic()
            if held and (now_dbg - last_dbg) > 0.25:
                print(f"  held: {held}  lin={lin}  ang={ang}")
                last_dbg = now_dbg

            now_s = time.time()
            sec = int(now_s); nsec = int((now_s - sec) * 1e9)
            pub.publish({
                "header": {"stamp": {"sec": sec, "nanosec": nsec},
                           "frame_id": "base_link"},
                "twist": {"linear":  {"x": lin[0], "y": lin[1], "z": lin[2]},
                          "angular": {"x": ang[0], "y": ang[1], "z": ang[2]}},
            })
            next_t += period
            sleep = next_t - time.monotonic()
            if sleep > 0:
                time.sleep(sleep)
            else:
                next_t = time.monotonic()
    finally:
        pub.unadvertise()
        ros.terminate()
        print("\nteleop stopped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
