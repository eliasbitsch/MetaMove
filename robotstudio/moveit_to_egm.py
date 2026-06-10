"""
MoveIt → EGM bridge for the DPP lab.

Subscribes /display_planned_path. When a new plan arrives, extracts the FINAL
joint configuration and drives the robot there via the same Python EGM driver
that already works (UDP 6511 to MainModule.main's EGMRunJoint loop).

Workflow:
  1. User clicks "Plan" in RViz MotionPlanning panel
  2. RViz/move_group publishes /display_planned_path with the planned trajectory
  3. This bridge picks the last point in the trajectory.joint_trajectory
  4. Writes those joint values to a YAML
  5. Optionally drives the EGM driver to that target

Run inside the ROS container (since rosbridge would add latency):
    ros2 run metamove_bridge moveit_to_egm  # if installed as entry point
    # or directly:
    python3 moveit_to_egm.py
"""
from __future__ import annotations

import json
import os
import socket
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path

import rclpy
from rclpy.node import Node
from moveit_msgs.msg import DisplayTrajectory
from sensor_msgs.msg import JointState

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
EGM_IP   = os.environ.get('EGM_CTRL_IP', '192.168.125.1')
EGM_PORT = int(os.environ.get('EGM_CTRL_PORT', '6511'))


class MoveItToEgm(Node):
    def __init__(self) -> None:
        super().__init__('moveit_to_egm')

        # Last seen plan + EGM driver state
        self._last_plan_joints: list[float] | None = None
        self._driver_thread: threading.Thread | None = None
        self._driver_stop = threading.Event()
        self._current_joints: list[float] | None = None
        self._lock = threading.Lock()

        self.create_subscription(DisplayTrajectory, '/display_planned_path',
                                  self._on_plan, 1)
        self.create_subscription(JointState, '/joint_states',
                                  self._on_joints, 10)
        self.get_logger().info(
            'subscribed to /display_planned_path. Plan in RViz → robot will move.'
        )

    def _on_joints(self, msg: JointState) -> None:
        idx = {n: i for i, n in enumerate(msg.name)}
        try:
            ordered = [msg.position[idx[n]] for n in JOINT_NAMES]
        except KeyError:
            return
        with self._lock:
            self._current_joints = ordered

    def _on_plan(self, msg: DisplayTrajectory) -> None:
        if not msg.trajectory:
            return
        rt = msg.trajectory[0]  # RobotTrajectory
        if not rt.joint_trajectory.points:
            return
        # Joint name order in trajectory might not be JOINT_NAMES order
        names = list(rt.joint_trajectory.joint_names)
        last = rt.joint_trajectory.points[-1].positions
        # Re-order to canonical JOINT_NAMES (rad)
        try:
            idx = {n: i for i, n in enumerate(names)}
            target_rad = [last[idx[n]] for n in JOINT_NAMES]
        except KeyError as e:
            self.get_logger().warn(f'plan missing joint {e}')
            return

        target_deg = [v * 57.29578 for v in target_rad]
        self.get_logger().info(
            f'new plan goal: '
            f'J=[{target_deg[0]:+6.1f}, {target_deg[1]:+6.1f}, '
            f'{target_deg[2]:+6.1f}, {target_deg[3]:+6.1f}, '
            f'{target_deg[4]:+6.1f}, {target_deg[5]:+6.1f}]°'
        )
        with self._lock:
            self._last_plan_joints = target_rad

    def get_last_target_rad(self) -> list[float] | None:
        with self._lock:
            return list(self._last_plan_joints) if self._last_plan_joints else None


def egm_drive(target_rad: list[float], max_step_deg: float = 0.5,
              tolerance_deg: float = 0.5) -> bool:
    """Run the EGM driver inline (avoids subprocess overhead).

    Sends EgmSensor 250 Hz with target = interpolated current → target,
    returns when |joint_err|_inf < tolerance_deg or robot stops responding.
    """
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / 'ai-services' / 'egm-mock'))
    import egm_pb2  # type: ignore

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    try:
        sock.bind(('0.0.0.0', EGM_PORT))
    except OSError as e:
        print(f'  EGM bind err: {e}')
        return False
    sock.settimeout(0.05)

    target_deg = [v * 57.29578 for v in target_rad]
    seq = 0
    last_status = 0.0
    started = time.monotonic()
    max_runtime = 90.0  # safety

    print(f'  target (deg): {[round(v,1) for v in target_deg]}')
    while time.monotonic() - started < max_runtime:
        try:
            data, addr = sock.recvfrom(4096)
        except socket.timeout:
            # No EGM packets — bootstrap by sending an EgmSensor
            m = egm_pb2.EgmSensor()
            m.header.seqno = seq; seq += 1
            m.header.tm = int(time.time()*1000) & 0xFFFFFFFF
            m.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
            m.planned.joints.joints.extend(target_deg)
            sock.sendto(m.SerializeToString(), (EGM_IP, EGM_PORT))
            continue

        robot = egm_pb2.EgmRobot()
        try:
            robot.ParseFromString(data)
        except Exception:
            continue
        if not (robot.HasField('feedBack') and robot.feedBack.HasField('joints')):
            continue
        current = list(robot.feedBack.joints.joints)[:6]
        if len(current) < 6:
            continue

        err = max(abs(c - t) for c, t in zip(current, target_deg))
        if err < tolerance_deg:
            print(f'  ✓ arrived (err {err:.2f}°)')
            sock.close()
            return True

        # Rate-limit interpolation
        cmd = []
        for c, t in zip(current, target_deg):
            d = t - c
            d = max(-max_step_deg, min(max_step_deg, d))
            cmd.append(c + d)

        s = egm_pb2.EgmSensor()
        s.header.seqno = seq; seq += 1
        s.header.tm = int(time.time()*1000) & 0xFFFFFFFF
        s.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
        s.planned.joints.joints.extend(cmd)
        sock.sendto(s.SerializeToString(), addr)

        now = time.monotonic()
        if now - last_status > 1.0:
            last_status = now
            print(f'  err={err:6.2f}°  J6={current[5]:+6.1f}°')

    sock.close()
    print('  timeout after 90s')
    return False


def main() -> None:
    rclpy.init()
    node = MoveItToEgm()

    # Spin in background, watch for new plans
    spin_thread = threading.Thread(target=lambda: rclpy.spin(node), daemon=True)
    spin_thread.start()

    print()
    print('=== MoveIt → EGM bridge ===')
    print('In RViz: click "Plan" (NOT "Plan & Execute"). Then come back here.')
    print('Commands:  drive  — execute last plan via EGM')
    print('           save <name> — save last plan goal to ~/dpp_waypoints.yaml')
    print('           snap <name> — save CURRENT joint state (not plan goal)')
    print('           list  — show last plan target')
    print('           quit')
    print()

    waypoints_path = Path.home() / 'dpp_waypoints.yaml'
    try:
        import yaml
        if waypoints_path.exists():
            data = yaml.safe_load(waypoints_path.read_text()) or {}
            wps = data.get('waypoints', [])
            print(f'(loaded {len(wps)} existing waypoints from {waypoints_path})')
    except Exception:
        wps = []

    def save_yaml(name: str, joints_rad: list[float]) -> None:
        import yaml
        d = {'waypoints': []}
        if waypoints_path.exists():
            try:
                d = yaml.safe_load(waypoints_path.read_text()) or {'waypoints': []}
            except Exception:
                pass
        d['waypoints'].append({
            'name': name,
            'joints': [round(v, 6) for v in joints_rad],
            'saved_utc': datetime.now(timezone.utc).isoformat(timespec='seconds').replace('+00:00', 'Z'),
        })
        waypoints_path.write_text(yaml.safe_dump(d, sort_keys=False))
        print(f'  saved as "{name}" → {waypoints_path} ({len(d["waypoints"])} total)')

    try:
        while True:
            try:
                line = input('> ').strip()
            except EOFError:
                break
            if not line:
                continue
            tokens = line.split()
            cmd = tokens[0].lower()

            if cmd in ('q', 'quit', 'exit'):
                break
            elif cmd == 'list':
                t = node.get_last_target_rad()
                if t is None:
                    print('  (no plan received yet)')
                else:
                    print(f'  last plan goal (deg): {[round(v*57.29578, 1) for v in t]}')
            elif cmd == 'drive':
                t = node.get_last_target_rad()
                if t is None:
                    print('  no plan to drive. Click Plan in RViz first.')
                    continue
                ok = egm_drive(t)
                print('  result:', 'OK' if ok else 'FAILED/TIMEOUT')
            elif cmd == 'save':
                if len(tokens) < 2:
                    print('  usage: save <name>')
                    continue
                t = node.get_last_target_rad()
                if t is None:
                    print('  no plan to save. Click Plan in RViz first.')
                    continue
                save_yaml(tokens[1], t)
            elif cmd == 'snap':
                if len(tokens) < 2:
                    print('  usage: snap <name>')
                    continue
                with node._lock:
                    cur = list(node._current_joints) if node._current_joints else None
                if cur is None:
                    print('  no /joint_states yet')
                    continue
                save_yaml(tokens[1], cur)
            else:
                print(f'  unknown command: {cmd}')
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
