"""End-to-end latency probe.

Publishes TwistStamped at 30 Hz for 5 seconds, subscribes /joint_states,
measures time between twist publish and observable joint motion exceeding a
threshold. Prints distribution statistics.

Usage:
    python3 latency_probe.py --path egm    # or --path rws
    python3 latency_probe.py --path egm --duration 10 --magnitude 0.03

Caveat: this measures the OUTBOUND closed-loop latency (twist published → joint
moved). Inbound feedback (joint state arriving) measurement requires the
robot to ACK via /joint_states which both paths provide.
"""
from __future__ import annotations

import argparse
import statistics
import time

import rclpy
from rclpy.node import Node
from geometry_msgs.msg import TwistStamped
from sensor_msgs.msg import JointState


class LatencyProbe(Node):
    def __init__(self, path_name: str, duration: float, magnitude: float):
        super().__init__('latency_probe')
        self.path_name = path_name
        self.duration = duration
        self.magnitude = magnitude

        self.pub = self.create_publisher(TwistStamped, '/servo_node/delta_twist_cmds', 10)
        self.sub = self.create_subscription(JointState, '/joint_states', self._on_js, 50)

        self.start_t = None
        self.first_motion_t = None
        self.initial_joints: list[float] | None = None
        self.frames_sent = 0
        self.frames_received = 0
        self.received_ts: list[float] = []

    def _on_js(self, msg: JointState) -> None:
        now = time.perf_counter()
        if msg.position is None or len(msg.position) < 6:
            return
        if self.initial_joints is None:
            self.initial_joints = list(msg.position[:6])
            return
        self.frames_received += 1
        self.received_ts.append(now)
        if self.first_motion_t is None:
            deltas = [abs(msg.position[i] - self.initial_joints[i]) for i in range(6)]
            if max(deltas) > 0.005:  # rad, ~0.3 deg
                self.first_motion_t = now

    def run(self) -> None:
        print(f'[probe] path={self.path_name} duration={self.duration}s magnitude={self.magnitude}m/s')
        print('[probe] waiting for initial /joint_states ...')
        deadline = time.perf_counter() + 5.0
        while self.initial_joints is None and time.perf_counter() < deadline:
            rclpy.spin_once(self, timeout_sec=0.05)
        if self.initial_joints is None:
            print('[probe] ERROR: no /joint_states received — is the bridge running and connected?')
            return
        print(f'[probe] initial joints captured: {[f"{j:.3f}" for j in self.initial_joints]}')
        print(f'[probe] starting twist publish at 30Hz for {self.duration}s')

        self.start_t = time.perf_counter()
        next_pub = self.start_t
        end_t = self.start_t + self.duration
        while time.perf_counter() < end_t:
            rclpy.spin_once(self, timeout_sec=0.001)
            now = time.perf_counter()
            if now >= next_pub:
                msg = TwistStamped()
                msg.header.stamp = self.get_clock().now().to_msg()
                msg.header.frame_id = 'base_link'
                msg.twist.linear.z = self.magnitude
                self.pub.publish(msg)
                self.frames_sent += 1
                next_pub = now + (1.0 / 30.0)

        # Stop twist (publish zero for 1 sec)
        stop_end = time.perf_counter() + 1.0
        while time.perf_counter() < stop_end:
            rclpy.spin_once(self, timeout_sec=0.01)
            msg = TwistStamped()
            msg.header.stamp = self.get_clock().now().to_msg()
            msg.header.frame_id = 'base_link'
            self.pub.publish(msg)
            time.sleep(0.033)

        self._report()

    def _report(self) -> None:
        if self.first_motion_t is None:
            print('[probe] no detectable motion within probe duration')
            print(f'[probe] frames_sent={self.frames_sent} frames_received={self.frames_received}')
            return
        e2e_first = (self.first_motion_t - self.start_t) * 1000
        if len(self.received_ts) >= 2:
            intervals = [(self.received_ts[i] - self.received_ts[i-1]) * 1000 for i in range(1, len(self.received_ts))]
            rate_hz = len(self.received_ts) / max(self.received_ts[-1] - self.received_ts[0], 1e-3)
            jitter_p95 = sorted(intervals)[int(len(intervals) * 0.95)] if intervals else 0
        else:
            rate_hz = 0
            jitter_p95 = 0

        print()
        print(f'=== LATENCY PROBE RESULTS — path={self.path_name} ===')
        print(f'frames_sent:           {self.frames_sent}')
        print(f'frames_received:       {self.frames_received}')
        print(f'first motion latency:  {e2e_first:.1f} ms  (twist-publish → joint-moves)')
        print(f'joint_state rate:      {rate_hz:.1f} Hz')
        print(f'joint_state jitter p95: {jitter_p95:.1f} ms')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--path', required=True, choices=['rws', 'egm'])
    parser.add_argument('--duration', type=float, default=5.0)
    parser.add_argument('--magnitude', type=float, default=0.02)
    args = parser.parse_args()

    rclpy.init()
    node = LatencyProbe(args.path, args.duration, args.magnitude)
    try:
        node.run()
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
