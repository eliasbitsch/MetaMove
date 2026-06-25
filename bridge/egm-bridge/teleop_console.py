"""Console keyboard teleop for moveit_servo via rosbridge — Windows msvcrt.

Replaces teleop_keyboard.py's global-hook `keyboard` lib, whose hook silently
dies on some Windows sessions (keys held -> is_pressed() stays False -> zero
twists). This reads keys from THIS console via msvcrt and relies on key
auto-repeat: while a key is held the console delivers repeats (~30 Hz); we
keep the twist alive for HOLD_S after the last repeat. Only reacts when this
window has focus — safer for robot teleop anyway.

Keys:
  w/s : +x/-x    a/d : +y/-y    q/e : +z/-z      (m/s, --linear)
  i/k : +rx/-rx  j/l : +ry/-ry  u/o : +rz/-rz    (rad/s, --angular)
  1-9 : speed multiplier x1..x9
  space : immediate zero twist
  ESC : quit
"""
from __future__ import annotations

import argparse
import msvcrt
import sys
import time

import roslibpy

KEYS = {
    "w": ("lin", 0,  1.0), "s": ("lin", 0, -1.0),
    "a": ("lin", 1,  1.0), "d": ("lin", 1, -1.0),
    "q": ("lin", 2,  1.0), "e": ("lin", 2, -1.0),
    "i": ("ang", 0,  1.0), "k": ("ang", 0, -1.0),
    "j": ("ang", 1,  1.0), "l": ("ang", 1, -1.0),
    "u": ("ang", 2,  1.0), "o": ("ang", 2, -1.0),
}

# Windows console auto-repeat has an initial delay of up to ~1s before
# repeats start. HOLD_S must exceed it, or the key is cleared between the
# first press and the first repeat and only zero twists ever go out.
HOLD_S = 1.10
RATE_HZ = 50.0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--rosbridge-host", default="127.0.0.1")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--linear", type=float, default=0.03, help="m/s per axis")
    ap.add_argument("--angular", type=float, default=0.2, help="rad/s per axis")
    args = ap.parse_args()

    print(f"connecting rosbridge ws://{args.rosbridge_host}:{args.rosbridge_port} ...")
    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        print("ERROR: rosbridge not reachable")
        return 1
    print("connected.")
    print(f"lin={args.linear} m/s  ang={args.angular} rad/s  | 1-9=speed  space=stop  ESC=quit")
    print("HOLD a key to jog (auto-repeat keeps it alive). This window must be focused.")

    pub = roslibpy.Topic(ros, "/servo_node/delta_twist_cmds",
                         "geometry_msgs/TwistStamped")
    pub.advertise()

    active_key: str | None = None
    last_press = 0.0
    mult = 1.0
    last_print = ""
    sent_nonzero = 0
    last_stat = time.monotonic()

    period = 1.0 / RATE_HZ
    next_t = time.monotonic()
    try:
        while True:
            # drain all pending keystrokes
            while msvcrt.kbhit():
                ch = msvcrt.getwch()
                if ch == "\x1b":          # ESC
                    raise KeyboardInterrupt
                if ch == " ":
                    active_key = None
                    print("\n[STOP] zero twist")
                    continue
                if ch.isdigit() and ch != "0":
                    mult = float(ch)
                    print(f"\n[speed x{int(mult)}]")
                    continue
                ch = ch.lower()
                if ch in KEYS:
                    active_key = ch
                    last_press = time.monotonic()

            now = time.monotonic()
            if active_key and (now - last_press) > HOLD_S:
                active_key = None

            lin = [0.0, 0.0, 0.0]
            ang = [0.0, 0.0, 0.0]
            if active_key:
                kind, idx, sign = KEYS[active_key]
                if kind == "lin":
                    lin[idx] = sign * args.linear * mult
                else:
                    ang[idx] = sign * args.angular * mult

            if any(lin) or any(ang):
                sent_nonzero += 1
            now2 = time.monotonic()
            if now2 - last_stat >= 1.0:
                if not ros.is_connected:
                    print("\n!!! ROSBRIDGE-VERBINDUNG TOT — Fenster schliessen, "
                          "Claude um Neustart bitten !!!")
                    raise KeyboardInterrupt
                label = (f"jog:{active_key} x{int(mult)}"
                         if active_key else "idle")
                print(f"  {label:<12} sent_nonzero/s: {sent_nonzero:3d}  "
                      f"ws:OK   ", end="\r", flush=True)
                sent_nonzero = 0
                last_stat = now2

            now_s = time.time()
            sec = int(now_s)
            pub.publish({
                "header": {"stamp": {"sec": sec,
                                     "nanosec": int((now_s - sec) * 1e9)},
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
    except KeyboardInterrupt:
        pass
    finally:
        # send a short burst of zero twists so the robot stops cleanly
        for _ in range(10):
            now_s = time.time()
            sec = int(now_s)
            pub.publish({
                "header": {"stamp": {"sec": sec,
                                     "nanosec": int((now_s - sec) * 1e9)},
                           "frame_id": "base_link"},
                "twist": {"linear": {"x": 0, "y": 0, "z": 0},
                          "angular": {"x": 0, "y": 0, "z": 0}},
            })
            time.sleep(0.02)
        pub.unadvertise()
        ros.terminate()
        print("\nteleop beendet.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
