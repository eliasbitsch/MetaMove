"""
Pulses EGM_START_JOINT IO signal to keep SM Add-In v2.0 in continuous EGM mode.

ABB SM Add-In's TRobEGM enters joint-streaming on the rising edge of
EGM_START_JOINT, runs EGMRunJoint until convergence/CondTime, then returns to
idle. The signal is edge-triggered, so without explicit retrigger the state
machine sits idle even if the IO is still HIGH.

This script toggles 1 -> 0 -> 1 on a configurable cadence so a fresh rising
edge keeps firing. Each pulse generates one EGM motion segment; with --period
matched to a typical EGMRunJoint settle time the gap is barely perceptible.
"""
from __future__ import annotations

import argparse
import base64
import sys
import time

try:
    import requests
    import urllib3
    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
except ImportError:
    print("pip install requests", file=sys.stderr)
    sys.exit(1)

CTRL = "https://192.168.125.1:443"
IO   = "/rw/iosystem/signals/EGM_START_JOINT/set-value"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--period", type=float, default=2.0,
                    help="seconds between rising-edge pulses (default 2.0)")
    ap.add_argument("--user", default="Default User")
    ap.add_argument("--password", default="robotics")
    args = ap.parse_args()

    s = requests.Session()
    s.auth = (args.user, args.password)
    s.verify = False
    s.headers.update({
        "Content-Type": "application/x-www-form-urlencoded;v=2.0",
        "Accept":       "application/xhtml+xml;v=2.0",
        "Connection":   "keep-alive",
    })

    def write(v: int) -> int:
        try:
            r = s.post(CTRL + IO, data=f"lvalue={v}", timeout=3)
            return r.status_code
        except Exception as e:
            print(f"  IO write err: {e}", file=sys.stderr)
            return -1

    print(f"pulsing EGM_START_JOINT every {args.period}s. Ctrl-C to stop.")
    n = 0; fails = 0
    try:
        while True:
            c0 = write(0)
            time.sleep(0.10)
            c1 = write(1)
            n += 1
            if c0 != 204 or c1 != 204:
                fails += 1
                print(f"  pulse {n}: 0->{c0} 1->{c1}")
            elif n % 5 == 0:
                print(f"  pulses ok: {n}  fails: {fails}")
            time.sleep(max(0.0, args.period - 0.10))
    except KeyboardInterrupt:
        print("\nstopped — leaving IO HIGH")
        write(1)
        return 0


if __name__ == "__main__":
    sys.exit(main())
