"""
Minimal EGM identity-echo bridge for ABB SM Add-In v2.0 + GoFa CRB 15000.

Binds UDP :6511 (or configurable) on Windows host. Receives `EgmRobot`
feedback packets from the controller (joint feedback at 250 Hz) and replies
with `EgmSensor` correction packets that echo current joints back as the
target — no motion, but the controller sees a live sensor and EGMRunJoint
stays in CONNECTED state instead of timing out.

Use as the foundation for MoveIt/Unity-driven joint streaming: replace the
joint echo with real targets in `compute_target_joints()`.

Run on Windows native (NOT in WSL/Docker container — mirrored networking
does not forward UDP into containers reliably).

Requires: pip install protobuf (and egm_pb2.py compiled from egm.proto).
"""
from __future__ import annotations

import argparse
import socket
import statistics
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
EGM_MOCK_DIR = HERE.parent / "egm-mock"
sys.path.insert(0, str(EGM_MOCK_DIR))
import egm_pb2  # noqa: E402 — generated


def compute_target_joints(current: list[float]) -> list[float]:
    """Return joint targets to send back to controller.

    Identity echo: send current joints back -> no robot motion.
    Replace with real targets driven by MoveIt/Unity/etc. once verified.
    """
    return list(current)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="0.0.0.0")
    ap.add_argument("--port", type=int, default=6511)
    ap.add_argument("--verbose", "-v", action="store_true")
    args = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((args.host, args.port))
    print(f"[bridge] EGM identity-echo listening on {args.host}:{args.port}/udp")

    seq_out = 0
    last_summary = time.monotonic()
    rtts_us: list[float] = []
    recv_count = 0
    parse_errors = 0
    last_joints: list[float] | None = None
    last_addr = None
    t0 = time.monotonic()

    try:
        while True:
            data, addr = sock.recvfrom(4096)
            t_recv = time.monotonic()
            try:
                robot = egm_pb2.EgmRobot()
                robot.ParseFromString(data)
            except Exception as e:  # noqa: BLE001
                parse_errors += 1
                if args.verbose:
                    print(f"[bridge] parse error from {addr}: {e}")
                continue

            recv_count += 1
            last_addr = addr

            joints: list[float] = []
            if robot.HasField("feedBack") and robot.feedBack.HasField("joints"):
                joints = list(robot.feedBack.joints.joints)
            if joints:
                last_joints = joints

            target = compute_target_joints(joints) if joints else []

            sensor = egm_pb2.EgmSensor()
            sensor.header.seqno = seq_out
            seq_out += 1
            sensor.header.tm = int(time.time() * 1000) & 0xFFFFFFFF
            sensor.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
            if target:
                sensor.planned.joints.joints.extend(target)

            sock.sendto(sensor.SerializeToString(), addr)
            rtts_us.append((time.monotonic() - t_recv) * 1e6)

            if args.verbose and recv_count <= 5:
                print(f"[bridge] {addr} seq={robot.header.seqno} fb_joints={joints}")

            now = time.monotonic()
            if now - last_summary >= 1.0:
                dt = now - last_summary
                hz = recv_count / dt if dt > 0 else 0.0
                rtt_avg = statistics.mean(rtts_us) if rtts_us else 0
                rtt_p95 = (statistics.quantiles(rtts_us, n=20)[18]
                           if len(rtts_us) > 20 else rtt_avg)
                last_joint_str = (
                    "[" + ", ".join(f"{j:+7.2f}" for j in last_joints) + "]"
                    if last_joints else "(none)"
                )
                uptime = now - t0
                print(f"[bridge] t={uptime:6.1f}s rx={hz:6.1f}Hz "
                      f"rtt_avg={rtt_avg:5.0f}us p95={rtt_p95:5.0f}us "
                      f"parse_err={parse_errors} src={last_addr} "
                      f"joints={last_joint_str}")
                last_summary = now
                recv_count = 0
                rtts_us.clear()
    except KeyboardInterrupt:
        print("\n[bridge] stopped.")
        return 0


if __name__ == "__main__":
    sys.exit(main())
