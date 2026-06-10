"""
EGM-based waypoint driver for the DPP lab run.

Talks directly to the GoFa over UDP EGM. No ROS, no MoveIt, no Docker.
Just listens for EgmRobot feedback on 6511 and answers with EgmSensor
that points at the current target waypoint. The controller's RAPID
(MainModule.main / EGMRunJoint) handles the actual motion-supervised
follow with proper acceleration limits.

Prereqs on the controller:
  1) RAPID program running with PP at MainModule.main (or anything that
     calls EGMSetupUC + EGMRunJoint on UDPUC "ROB_Michi").
  2) UDPUC "ROB_Michi" in CFG: RemoteAddress = this PC's IP, RemotePort = 6511.
  3) Motors on (deadman held in MANR, or AUTO with motors-on).

Usage:
    python dpp_egm_drive.py                 # built-in safe demo path, 1 lap
    python dpp_egm_drive.py --loop          # endless loop, Ctrl-C to stop
    python dpp_egm_drive.py --waypoints my.yaml --loop --speed 30
    python dpp_egm_drive.py --snap          # only read joints, print + exit

GUI hook: writes a small status file each iteration so dpp_lab_gui can
display progress without running its own EGM client.
"""
from __future__ import annotations

import argparse
import math
import signal
import socket
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / 'ai-services' / 'egm-mock'))
import egm_pb2  # type: ignore

JOINT_NAMES = ['J1', 'J2', 'J3', 'J4', 'J5', 'J6']


# ---------------- Built-in waypoint set ----------------------------------
# Safe demo: small motions around home with J5=90° (non-singular).
# Joints in degrees, order J1..J6. All within reasonable workspace.
SAFE_WAYPOINTS_DEG: list[list[float]] = [
    [   0,   0,   0,   0,  90,   0],   # home
    [ +30, -15,  15,   0,  90,   0],   # front-right reach
    [ +30, -15,  15,   0,  60,   0],   # same, wrist down
    [   0, -30,  30,   0,  90,   0],   # front-low
    [ -30, -15,  15,   0,  90,   0],   # front-left reach
    [ -30, -15,  15,   0,  60,   0],
    [   0,   0,   0,   0,  90,  +45],  # home + tool twist
    [   0,   0,   0,   0,  90,  -45],
]


# ---------------- Helpers ------------------------------------------------

_stop = False
def _sigint(*_a):
    global _stop
    _stop = True
signal.signal(signal.SIGINT, _sigint)
if hasattr(signal, 'SIGTERM'):
    signal.signal(signal.SIGTERM, _sigint)


def load_yaml_waypoints(path: Path) -> list[list[float]]:
    """Accept the same YAML format as dpp_teach.py — joints in radians."""
    import yaml
    data = yaml.safe_load(path.read_text()) or {}
    wps_rad = [w['joints'] for w in data.get('waypoints', [])]
    return [[v * 180.0 / math.pi for v in w] for w in wps_rad]


def diff_max_abs(a: list[float], b: list[float]) -> float:
    return max(abs(x - y) for x, y in zip(a, b))


# ---------------- Main loop ----------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--host', default='0.0.0.0', help='UDP bind host')
    ap.add_argument('--port', type=int, default=6511, help='UDP port (default 6511)')
    ap.add_argument('--waypoints', type=Path,
                    help='YAML waypoints (joints in rad). If absent, use built-in safe set.')
    ap.add_argument('--loop', action='store_true',
                    help='loop forever, Ctrl-C to stop')
    ap.add_argument('--snap', action='store_true',
                    help='just print current joints and exit')
    ap.add_argument('--tolerance', type=float, default=0.5,
                    help='convergence tolerance per joint, degrees')
    ap.add_argument('--max-step-deg', type=float, default=2.0,
                    help='max change per EGM tick per joint (rate-limits motion)')
    ap.add_argument('--quiet', action='store_true')
    args = ap.parse_args()

    if args.waypoints:
        waypoints = load_yaml_waypoints(args.waypoints)
        print(f'loaded {len(waypoints)} waypoints from {args.waypoints}')
    else:
        waypoints = SAFE_WAYPOINTS_DEG
        print(f'using built-in safe demo path ({len(waypoints)} waypoints)')

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((args.host, args.port))
    sock.settimeout(5.0)
    print(f'listening on {args.host}:{args.port}/udp — waiting for EGM session...')

    target_deg: list[float] = list(waypoints[0])
    wp_idx = 0
    out_seq = 0
    last_status_t = 0.0
    last_feedback: list[float] | None = None
    n_recv = 0
    n_arrive = 0

    try:
        while not _stop:
            try:
                data, addr = sock.recvfrom(4096)
            except socket.timeout:
                print('  [warn] no EgmRobot in 5s. Is RAPID running on MainModule.main?')
                continue

            robot = egm_pb2.EgmRobot()
            try:
                robot.ParseFromString(data)
            except Exception as e:
                if not args.quiet:
                    print(f'  parse err: {e}')
                continue
            n_recv += 1

            if not (robot.HasField('feedBack') and robot.feedBack.HasField('joints')):
                continue
            joints = list(robot.feedBack.joints.joints)
            if len(joints) < 6:
                continue
            last_feedback = joints[:6]

            if args.snap:
                print('current joints (deg):', [round(j, 3) for j in last_feedback])
                return 0

            # Convergence check
            err = diff_max_abs(last_feedback, target_deg)
            if err < args.tolerance:
                n_arrive += 1
                wp_idx += 1
                if wp_idx >= len(waypoints):
                    if not args.loop:
                        print(f'finished {len(waypoints)} waypoints in one pass. Done.')
                        return 0
                    wp_idx = 0
                    print('  -- loop --')
                target_deg = list(waypoints[wp_idx])
                if not args.quiet:
                    print(f'  -> wp[{wp_idx}] = {[round(v,1) for v in target_deg]}')

            # Rate-limit per-tick change to stay within EGM_minmax & joint limits.
            # The controller's EGMActJoint MaxSpeedDeviation also limits this,
            # but explicit clamping makes the trajectory predictable.
            cmd_deg = []
            for cur, tgt in zip(last_feedback, target_deg):
                d = tgt - cur
                d = max(-args.max_step_deg, min(args.max_step_deg, d))
                cmd_deg.append(cur + d)

            sensor = egm_pb2.EgmSensor()
            sensor.header.seqno = out_seq
            out_seq += 1
            sensor.header.tm = int(time.time() * 1000) & 0xFFFFFFFF
            sensor.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
            sensor.planned.joints.joints.extend(cmd_deg)
            sock.sendto(sensor.SerializeToString(), addr)

            now = time.monotonic()
            if now - last_status_t > 1.0:
                last_status_t = now
                print(f'  wp={wp_idx}/{len(waypoints)}  err={err:5.2f}°  '
                      f'arrivals={n_arrive}  rx={n_recv}'
                      f'  J5={last_feedback[4]:+6.1f}°')
    finally:
        sock.close()

    return 0


if __name__ == '__main__':
    sys.exit(main())
