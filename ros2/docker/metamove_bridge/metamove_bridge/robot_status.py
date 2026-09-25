"""robot_status - publishes WHY the robot is (not) moving, for the console and the HUD.

Collects /egm/status (EGM bridge), /robot/scaler_state (distance scaler) and
/dpp/state (path playback), runs status_core.decide on them at 4 Hz and publishes
/robot/status (std_msgs/String, JSON {"moving", "reason", "hint", "level"}).
Logs every change of reason, so the log reads as a timeline of the session.
"""
from __future__ import annotations

import json
import os
import sys
import time

import rclpy
from rclpy.node import Node
from std_msgs.msg import String

sys.path.insert(0, os.path.dirname(os.path.realpath(__file__)))
from status_core import decide  # noqa: E402

TOPICS = {"egm": "/egm/status", "scaler": "/robot/scaler_state", "playback": "/dpp/state"}


class RobotStatus(Node):
    def __init__(self) -> None:
        super().__init__("robot_status")
        self._latest: dict[str, tuple[float, dict]] = {}
        for key, topic in TOPICS.items():
            self.create_subscription(String, topic, lambda m, k=key: self._on(k, m), 10)
        self._pub = self.create_publisher(String, "/robot/status", 10)
        self._last_reason = None
        self.create_timer(0.25, self._tick)
        self.get_logger().info("robot_status up - /robot/status")

    def _on(self, key: str, msg: String) -> None:
        try:
            self._latest[key] = (time.monotonic(), json.loads(msg.data))
        except ValueError:
            self.get_logger().warn(f"bad JSON on {TOPICS[key]}: {msg.data[:80]}")

    def _get(self, key: str):
        if key not in self._latest:
            return None, None
        t, d = self._latest[key]
        return d, time.monotonic() - t

    def _tick(self) -> None:
        egm, ea = self._get("egm")
        sc, sa = self._get("scaler")
        pb, pa = self._get("playback")
        r = decide(egm, ea, sc, sa, pb, pa)
        self._pub.publish(String(data=json.dumps(r)))
        if r["reason"] != self._last_reason:
            self._last_reason = r["reason"]
            log = self.get_logger().info if r["level"] in ("ok", "info") else self.get_logger().warn
            log(f"{'MOVING' if r['moving'] else 'STOPPED'}: {r['reason']}"
                + (f" -> {r['hint']}" if r["hint"] else ""))


def main() -> None:
    rclpy.init()
    node = RobotStatus()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.try_shutdown()


if __name__ == "__main__":
    main()
