"""
DPP waypoint teach helper.

Workflow:
  1. ros2 launch abb_crb15000_moveit complete.launch.py   (RViz + MoveIt up)
  2. ros2 run metamove_bridge dpp_teach --ros-args -p out:=<path>/dpp_waypoints.yaml
  3. In RViz, drag the InteractiveMarker (or use MotionPlanning panel) and Plan+Execute
     the robot to each pose you want to record.
  4. In this terminal, hit <Enter> to snapshot current /joint_states as the next
     waypoint. Type 'list' to dump, 'undo' to drop last, 'save' to flush, 'quit' to exit.

Output YAML schema (consumed by dpp_playback):
    waypoints:
      - name: wp_01
        joints: [j1, j2, j3, j4, j5, j6]   # radians, order = joint_1..joint_6
        recorded_utc: 2026-05-20T14:33:12Z
"""
from __future__ import annotations

import os
import select
import sys
import threading
from datetime import datetime, timezone
from pathlib import Path

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSDurabilityPolicy, QoSProfile, QoSReliabilityPolicy
from sensor_msgs.msg import JointState

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']


def _default_out_path() -> str:
    # Prefer the package config dir if launched from a colcon install layout,
    # otherwise drop next to CWD.
    pkg_share = os.environ.get('AMENT_PREFIX_PATH', '').split(':')
    for p in pkg_share:
        cand = Path(p) / 'share' / 'metamove_bridge' / 'config' / 'dpp_waypoints.yaml'
        if cand.parent.exists():
            return str(cand)
    return str(Path.cwd() / 'dpp_waypoints.yaml')


class DppTeach(Node):
    def __init__(self) -> None:
        super().__init__('dpp_teach')
        self.declare_parameter('out', _default_out_path())
        self.out_path = Path(self.get_parameter('out').value)
        self.out_path.parent.mkdir(parents=True, exist_ok=True)

        self._latest: list[float] | None = None
        self._latest_lock = threading.Lock()
        self._waypoints: list[dict] = self._load_existing()

        self.create_subscription(
            JointState, '/joint_states', self._on_js,
            QoSProfile(depth=1,
                       reliability=QoSReliabilityPolicy.BEST_EFFORT,
                       durability=QoSDurabilityPolicy.VOLATILE),
        )

        self.get_logger().info(f'writing to: {self.out_path}')
        self.get_logger().info(f'existing waypoints: {len(self._waypoints)}')
        self.get_logger().info('commands: <Enter>=add  list  undo  save  quit')

    def _load_existing(self) -> list[dict]:
        if not self.out_path.exists():
            return []
        try:
            import yaml  # PyYAML
            data = yaml.safe_load(self.out_path.read_text()) or {}
            return list(data.get('waypoints', []))
        except Exception as e:
            self.get_logger().warn(f'could not parse existing yaml ({e}); starting empty')
            return []

    def _on_js(self, msg: JointState) -> None:
        # Re-order to canonical joint_1..joint_6 in case publisher uses a different order.
        idx = {n: i for i, n in enumerate(msg.name)}
        try:
            ordered = [msg.position[idx[n]] for n in JOINT_NAMES]
        except KeyError:
            return
        with self._latest_lock:
            self._latest = ordered

    def add(self) -> None:
        with self._latest_lock:
            snap = list(self._latest) if self._latest else None
        if snap is None:
            self.get_logger().warn('no /joint_states received yet')
            return
        name = f'wp_{len(self._waypoints) + 1:02d}'
        self._waypoints.append({
            'name': name,
            'joints': [round(v, 6) for v in snap],
            'recorded_utc': datetime.now(timezone.utc).isoformat(timespec='seconds').replace('+00:00', 'Z'),
        })
        deg = ', '.join(f'{v * 57.29578:+6.1f}' for v in snap)
        self.get_logger().info(f'+ {name}  [deg: {deg}]')

    def list_(self) -> None:
        if not self._waypoints:
            self.get_logger().info('(empty)')
            return
        for wp in self._waypoints:
            deg = ', '.join(f'{v * 57.29578:+6.1f}' for v in wp['joints'])
            self.get_logger().info(f"  {wp['name']}  [{deg}]")

    def undo(self) -> None:
        if not self._waypoints:
            return
        dropped = self._waypoints.pop()
        self.get_logger().info(f"- {dropped['name']}")

    def save(self) -> None:
        import yaml
        data = {'waypoints': self._waypoints}
        self.out_path.write_text(yaml.safe_dump(data, sort_keys=False))
        self.get_logger().info(f'saved {len(self._waypoints)} waypoints -> {self.out_path}')


def _stdin_ready(timeout: float) -> bool:
    # Cross-platform-ish: works on Linux/macOS. Docker container is Linux → fine.
    rlist, _, _ = select.select([sys.stdin], [], [], timeout)
    return bool(rlist)


def main() -> None:
    rclpy.init()
    node = DppTeach()
    try:
        while rclpy.ok():
            rclpy.spin_once(node, timeout_sec=0.05)
            if not _stdin_ready(0.0):
                continue
            line = sys.stdin.readline()
            if line == '':  # EOF
                break
            cmd = line.strip().lower()
            if cmd in ('', 'a', 'add'):
                node.add()
            elif cmd in ('l', 'list'):
                node.list_()
            elif cmd in ('u', 'undo'):
                node.undo()
            elif cmd in ('s', 'save'):
                node.save()
            elif cmd in ('q', 'quit', 'exit'):
                node.save()
                break
            else:
                node.get_logger().info('commands: <Enter>=add  list  undo  save  quit')
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
