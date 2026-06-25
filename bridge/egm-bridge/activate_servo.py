"""
Robust servo activation via rosbridge (NOT `ros2 service call`).

A fresh `docker exec ... ros2 service call` spins up its own DDS discovery
participant. With moveit_servo's duplicate `/servo_node` graph entries that
discovery can wedge ("rcl node's context is invalid") and the call hangs.
rosbridge_websocket is an already-running, healthy node in the container, so
routing the service call through it is reliable.

Steps:
  1. /servo_node/start_servo            (std_srvs/Trigger)
  2. /servo_node/switch_command_type    -> TWIST (moveit_msgs/ServoCommandType)
  3. read /servo_node/status for a moment and report

Run from Windows (bridge host):
  python activate_servo.py --rosbridge-host 127.0.0.1
"""
from __future__ import annotations

import argparse
import sys
import time

import roslibpy

# moveit_msgs/srv/ServoCommandType command_type enum
JOINT_JOG, TWIST, POSE = 0, 1, 2

# moveit_servo status codes (StatusCode enum) for human-readable output
STATUS_TEXT = {
    0: "INVALID",
    1: "NO_WARNING (ok)",
    2: "DECELERATE_FOR_APPROACHING_SINGULARITY",
    3: "HALT_FOR_SINGULARITY",
    4: "DECELERATE_FOR_LEAVING_SINGULARITY",
    5: "DECELERATE_FOR_COLLISION",
    6: "HALT_FOR_COLLISION",
    7: "JOINT_BOUND",
    8: "DECELERATE_FOR_JOINT_BOUND",
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--rosbridge-host", default="127.0.0.1")
    ap.add_argument("--rosbridge-port", type=int, default=9090)
    ap.add_argument("--command-type", type=int, default=TWIST,
                    help="0=JOINT_JOG 1=TWIST 2=POSE")
    ap.add_argument("--timeout", type=float, default=10.0)
    args = ap.parse_args()

    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    deadline = time.monotonic() + args.timeout
    while not ros.is_connected and time.monotonic() < deadline:
        time.sleep(0.1)
    if not ros.is_connected:
        print("ERROR: rosbridge not reachable")
        return 1
    print(f"rosbridge connected ws://{args.rosbridge_host}:{args.rosbridge_port}")

    # NOTE: moveit_servo 2.12 (the "new servo") has NO start_servo service.
    # Activation = switch the command type, then publish commands. The old
    # /servo_node/start_servo Trigger does not exist in this build (rosbridge
    # returns InvalidServiceException for it).

    # switch_command_type -> TWIST
    switch = roslibpy.Service(ros, "/servo_node/switch_command_type",
                              "moveit_msgs/ServoCommandType")
    try:
        resp = switch.call(roslibpy.ServiceRequest(
            {"command_type": args.command_type}), timeout=args.timeout)
        print(f"switch_command_type({args.command_type}) -> "
              f"success={resp.get('success')}")
    except Exception as e:  # noqa: BLE001
        print(f"switch_command_type FAILED (continuing): {e}")

    # 3) read status for ~2 s
    seen = {}

    def _on_status(msg):
        code = msg.get("code", msg.get("data"))
        seen[code] = seen.get(code, 0) + 1

    status = roslibpy.Topic(ros, "/servo_node/status",
                            "moveit_msgs/ServoStatus")
    status.subscribe(_on_status)
    t_end = time.monotonic() + 2.0
    while time.monotonic() < t_end:
        time.sleep(0.1)
    status.unsubscribe()

    if seen:
        for code, n in sorted(seen.items()):
            print(f"status code={code} ({STATUS_TEXT.get(code, '?')})  x{n}")
    else:
        print("status: no messages on /servo_node/status "
              "(servo may publish only on change)")

    print("\nServo activated. Hold keys in the teleop window to jog.")
    ros.terminate()
    return 0


if __name__ == "__main__":
    sys.exit(main())
