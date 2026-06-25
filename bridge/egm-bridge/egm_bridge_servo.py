"""
MetaMove EGM <-> ROS rosbridge servo teleop.

Windows-native EGM bridge:
  EgmRobot (controller -> us, 250 Hz)        -> /joint_states (50 Hz, deg->rad)
  /servo_node/commands (servo -> us, 50 Hz)  -> EgmSensor planned joints (deg)

Runs on Windows because WSL2/Docker mirrored networking does not deliver UDP
to containers reliably. Connects to rosbridge_websocket on the ROS container.

Joint conventions:
  ROS:  rad
  EGM:  deg

EGM joint feedback is in degrees. ROS uses radians, so we convert in both
directions. Joint order (per SRDF): joint_1..joint_6.
"""
from __future__ import annotations

import argparse
import math
import socket
import statistics
import sys
import threading
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
EGM_MOCK_DIR = HERE.parent / "egm-mock"
sys.path.insert(0, str(EGM_MOCK_DIR))
import egm_pb2  # noqa: E402

import roslibpy  # noqa: E402

JOINT_NAMES = [f"joint_{i}" for i in range(1, 7)]
DEG2RAD = math.pi / 180.0
RAD2DEG = 180.0 / math.pi

# ----- shared state (protected by state_lock) ---------------------------------
state_lock = threading.Lock()
last_feedback_deg: list[float] | None = None      # latest from EGM (deg)
target_deg: list[float] | None = None             # what to send back (deg)
last_command_t: float = 0.0                       # last time servo cmd arrived
COMMAND_TIMEOUT_S = 0.5                           # if no servo cmd → identity echo


def on_servo_command(msg: dict) -> None:
    """Servo publishes joint positions (rad) as Float64MultiArray."""
    global last_command_t
    data = msg.get("data") or []
    if len(data) != 6:
        return
    with state_lock:
        # rad -> deg, store as new target
        global target_deg
        target_deg = [v * RAD2DEG for v in data]
        last_command_t = time.monotonic()


def joint_state_publisher(ros: roslibpy.Ros, hz: float = 50.0) -> None:
    """Periodic /joint_states publisher driven by latest EGM feedback."""
    pub = roslibpy.Topic(ros, "/joint_states", "sensor_msgs/JointState")
    pub.advertise()
    period = 1.0 / hz
    next_t = time.monotonic()
    while ros.is_connected:
        with state_lock:
            fb = list(last_feedback_deg) if last_feedback_deg else None
        if fb:
            now_s = time.time()
            sec = int(now_s)
            nsec = int((now_s - sec) * 1e9)
            pub.publish({
                "header": {"stamp": {"sec": sec, "nanosec": nsec},
                           "frame_id": ""},
                "name": JOINT_NAMES,
                "position": [v * DEG2RAD for v in fb],
                "velocity": [],
                "effort": [],
            })
        next_t += period
        sleep_for = next_t - time.monotonic()
        if sleep_for > 0:
            time.sleep(sleep_for)
        else:
            next_t = time.monotonic()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="0.0.0.0")
    ap.add_argument("--port", type=int, default=6511)
    ap.add_argument("--rosbridge-host", default="192.168.125.99")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--verbose", "-v", action="store_true")
    args = ap.parse_args()

    # ----- ROS bridge connection ---------------------------------------------
    print(f"[bridge] connecting rosbridge ws://{args.rosbridge_host}:{args.rosbridge_port}")
    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    deadline = time.monotonic() + 10
    while not ros.is_connected and time.monotonic() < deadline:
        time.sleep(0.1)
    if not ros.is_connected:
        print("[bridge] ERROR: rosbridge not reachable — start the ROS launch first")
        return 1
    print("[bridge] rosbridge connected")

    sub = roslibpy.Topic(ros, "/servo_node/commands", "std_msgs/Float64MultiArray")
    sub.subscribe(on_servo_command)

    pub_thread = threading.Thread(target=joint_state_publisher,
                                  args=(ros,), daemon=True)
    pub_thread.start()

    # ----- EGM UDP socket ----------------------------------------------------
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((args.host, args.port))
    print(f"[bridge] EGM listening on {args.host}:{args.port}/udp")

    seq_out = 0
    last_summary = time.monotonic()
    rtts_us: list[float] = []
    recv_count = 0
    parse_errors = 0
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

            joints_deg: list[float] = []
            if robot.HasField("feedBack") and robot.feedBack.HasField("joints"):
                joints_deg = list(robot.feedBack.joints.joints)

            with state_lock:
                global last_feedback_deg
                if joints_deg:
                    last_feedback_deg = joints_deg
                # decide target: recent servo cmd, else echo current feedback
                if (target_deg is not None and
                        (time.monotonic() - last_command_t) < COMMAND_TIMEOUT_S):
                    out_deg = list(target_deg)
                else:
                    out_deg = list(joints_deg) if joints_deg else []

            sensor = egm_pb2.EgmSensor()
            sensor.header.seqno = seq_out
            seq_out += 1
            sensor.header.tm = int(time.time() * 1000) & 0xFFFFFFFF
            sensor.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
            if out_deg:
                sensor.planned.joints.joints.extend(out_deg)

            sock.sendto(sensor.SerializeToString(), addr)
            rtts_us.append((time.monotonic() - t_recv) * 1e6)

            now = time.monotonic()
            if now - last_summary >= 1.0:
                dt = now - last_summary
                hz = recv_count / dt if dt > 0 else 0.0
                rtt_avg = statistics.mean(rtts_us) if rtts_us else 0
                rtt_p95 = (statistics.quantiles(rtts_us, n=20)[18]
                           if len(rtts_us) > 20 else rtt_avg)
                with state_lock:
                    cmd_age = (time.monotonic() - last_command_t
                               if last_command_t else float("inf"))
                    fb = list(last_feedback_deg) if last_feedback_deg else None
                cmd_state = (f"servo({cmd_age:.1f}s)"
                             if cmd_age < COMMAND_TIMEOUT_S else "echo")
                fb_str = ("[" + ", ".join(f"{j:+7.2f}" for j in fb) + "]"
                          if fb else "(none)")
                uptime = now - t0
                print(f"[bridge] t={uptime:6.1f}s rx={hz:6.1f}Hz "
                      f"rtt_avg={rtt_avg:5.0f}us p95={rtt_p95:5.0f}us "
                      f"mode={cmd_state} fb_deg={fb_str}")
                last_summary = now
                recv_count = 0
                rtts_us.clear()
    except KeyboardInterrupt:
        print("\n[bridge] stopped.")
        sub.unsubscribe()
        ros.terminate()
        return 0


if __name__ == "__main__":
    sys.exit(main())
