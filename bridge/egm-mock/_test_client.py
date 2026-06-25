"""Tiny loopback client used to smoke-test egm_mock.py. Not imported in prod."""
import socket
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import egm_pb2  # noqa: E402


def main() -> int:
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 6511
    n = int(sys.argv[2]) if len(sys.argv) > 2 else 200

    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.settimeout(0.05)

    recv = 0
    for i in range(n):
        p = egm_pb2.EgmSensor()
        p.header.seqno = i
        p.planned.joints.joints.extend([float(i % 90), -45.0, 0.0, 0.0, 90.0, 0.0])
        s.sendto(p.SerializeToString(), ("127.0.0.1", port))
        try:
            data, _ = s.recvfrom(4096)
            r = egm_pb2.EgmRobot()
            r.ParseFromString(data)
            recv += 1
        except socket.timeout:
            pass
        time.sleep(0.004)

    print(f"sent={n} recv={recv}")
    return 0 if recv >= n * 0.95 else 1


if __name__ == "__main__":
    sys.exit(main())
