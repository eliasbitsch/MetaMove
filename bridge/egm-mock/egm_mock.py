"""
Minimal EGM mock server for Unity development without a real GoFa.

Binds UDP :6511 (configurable). For every incoming EgmSensor packet from
Unity (joint/cartesian command), replies with an EgmRobot feedback message
that echoes the command back as if the controller instantly reached it.

Use to:
  * validate your Unity EGM sender/receiver end-to-end
  * measure round-trip latency (reported every second)
  * verify protobuf encoding — bad packets are logged loudly

First run will compile `egm_pb2.py` from egm.proto via `protoc`. Requires
`protobuf` Python package and `protoc` on PATH (apt install protobuf-compiler).

Not a physics simulator. For physically accurate behaviour use RobotStudio
Virtual Controller instead.
"""
from __future__ import annotations

import argparse
import os
import socket
import statistics
import subprocess
import sys
import time
from pathlib import Path


HERE = Path(__file__).resolve().parent


def ensure_proto_compiled() -> None:
    """Run protoc on egm.proto if egm_pb2.py is missing or stale."""
    proto = HERE / "egm.proto"
    py = HERE / "egm_pb2.py"
    if py.exists() and py.stat().st_mtime >= proto.stat().st_mtime:
        return
    print(f"[mock] compiling {proto.name} -> {py.name}")
    subprocess.check_call([
        "protoc",
        f"--proto_path={HERE}",
        f"--python_out={HERE}",
        str(proto),
    ])


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="0.0.0.0")
    ap.add_argument("--port", type=int, default=6511)
    ap.add_argument("--verbose", "-v", action="store_true",
                    help="log every packet (default: 1s summary)")
    args = ap.parse_args()

    ensure_proto_compiled()
    sys.path.insert(0, str(HERE))
    import egm_pb2  # noqa: E402 — generated

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((args.host, args.port))
    print(f"[mock] EGM mock listening on {args.host}:{args.port}/udp")
    print("[mock] send EgmSensor packets from Unity; we reply with EgmRobot")
    print("[mock] Ctrl+C to stop\n")

    seq_out = 0
    last_summary = time.monotonic()
    rtts_us: list[float] = []
    recv_count = 0
    parse_errors = 0
    last_joints: list[float] | None = None
    t0 = time.monotonic()

    try:
        while True:
            data, addr = sock.recvfrom(4096)
            t_recv = time.monotonic()
            try:
                sensor = egm_pb2.EgmSensor()
                sensor.ParseFromString(data)
            except Exception as e:  # noqa: BLE001
                parse_errors += 1
                if args.verbose:
                    print(f"[mock] parse error from {addr}: {e}")
                continue

            recv_count += 1

            joints: list[float] = []
            if sensor.HasField("planned"):
                if sensor.planned.HasField("joints"):
                    joints = list(sensor.planned.joints.joints)
            if joints:
                last_joints = joints

            # Build EgmRobot feedback — echo the planned joints back as measured
            robot = egm_pb2.EgmRobot()
            robot.header.seqno = seq_out
            seq_out += 1
            robot.header.tm = int((time.time()) * 1000) & 0xFFFFFFFF
            robot.header.mtype = egm_pb2.EgmHeader.MSGTYPE_DATA
            if joints:
                robot.feedBack.joints.joints.extend(joints)
                robot.planned.joints.joints.extend(joints)
            robot.motorState.state = egm_pb2.EgmMotorState.MOTORS_ON
            robot.mciState.state = egm_pb2.EgmMCIState.MCI_RUNNING
            robot.rapidExecState.state = egm_pb2.EgmRapidCtrlExecState.RAPID_RUNNING

            sock.sendto(robot.SerializeToString(), addr)
            rtts_us.append((time.monotonic() - t_recv) * 1e6)

            if args.verbose:
                print(f"[mock] {addr} seq={sensor.header.seqno} joints={joints}")

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
                print(f"[mock] t={uptime:6.1f}s  rx={hz:6.1f} Hz  "
                      f"mock_rtt_avg={rtt_avg:5.0f}us p95={rtt_p95:5.0f}us  "
                      f"parse_err={parse_errors}  last_joints={last_joint_str}")
                last_summary = now
                recv_count = 0
                rtts_us.clear()
    except KeyboardInterrupt:
        print("\n[mock] stopped.")
        return 0


if __name__ == "__main__":
    sys.exit(main())
