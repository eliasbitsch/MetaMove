"""
Pose-to-twist converter for MoveIt Servo cartesian teleop.

Subscribes:
  /metamove/ik_target  geometry_msgs/PoseStamped
      Absolute target pose (e.g. Unity IK ball) in `base_link` frame.
  /joint_states         sensor_msgs/JointState
      Current joint positions. Used to compute current EE pose via FK.

Publishes:
  /servo_node/delta_twist_cmds  geometry_msgs/TwistStamped
      Linear + angular velocity in `base_link` (or whatever
      robot_link_command_frame is set to in servo_crb15000.yaml).
      Sent at fixed rate so Servo's incoming_command_timeout never trips.

Approach mirrors moveit_servo::PoseTracking:
  1. dpos = target_pos - current_pos
  2. drot = target_rot * inverse(current_rot)
  3. twist.linear  = dpos  * linear_gain   (clamped)
  4. twist.angular = log(drot) * angular_gain  (clamped)

Where log(drot) is the Lie-algebra log of the unit quaternion = rotation
axis * angle. Servo treats `command_in_type: speed_units` as m/s + rad/s,
so the gains directly tune approach speed.

Phase-1 (sim) closes the loop via fake_joint_state_publisher.py which
echos /servo_node/commands back as /joint_states. Phase-2 (real robot)
gets joint_states from the EGM bridge instead.
"""
from __future__ import annotations

import math
from typing import Optional

import numpy as np
import rclpy
from geometry_msgs.msg import PoseStamped, TwistStamped
from rclpy.node import Node
from sensor_msgs.msg import JointState

# Local FK via KDL would be cleaner, but adds a dependency on tf2_kdl /
# kdl_parser. For a 6-DOF chain the per-iteration cost of a TF lookup is
# negligible compared to network I/O, so we ask tf2 for base_link → tool0
# instead of replicating FK here. Falls back to "no current pose" if TF
# is not yet populated, in which case we publish zero twist (Servo holds).
import tf2_ros


class PoseToTwist(Node):
    def __init__(self) -> None:
        super().__init__('pose_to_twist')

        self.declare_parameter('input_topic', '/metamove/ik_target')
        self.declare_parameter('output_topic', '/servo_node/delta_twist_cmds')
        self.declare_parameter('base_frame', 'base_link')
        self.declare_parameter('ee_frame', 'tool0')
        self.declare_parameter('publish_hz', 100.0)
        self.declare_parameter('linear_gain', 1.5)        # m/s per m of error
        self.declare_parameter('angular_gain', 1.5)       # rad/s per rad of error
        self.declare_parameter('max_linear', 0.25)        # m/s clamp
        self.declare_parameter('max_angular', 1.0)        # rad/s clamp
        self.declare_parameter('deadband_pos_m', 0.002)   # ignore < 2 mm error
        self.declare_parameter('deadband_rot_rad', 0.01)  # ignore < 0.6° error
        self.declare_parameter('target_timeout_s', 0.5)   # silence if no target
        self.declare_parameter('track_orientation', False)  # set true to also command rotation

        self.input_topic = self.get_parameter('input_topic').value
        self.output_topic = self.get_parameter('output_topic').value
        self.base_frame = self.get_parameter('base_frame').value
        self.ee_frame = self.get_parameter('ee_frame').value
        publish_hz = float(self.get_parameter('publish_hz').value)
        self.linear_gain = float(self.get_parameter('linear_gain').value)
        self.angular_gain = float(self.get_parameter('angular_gain').value)
        self.max_linear = float(self.get_parameter('max_linear').value)
        self.max_angular = float(self.get_parameter('max_angular').value)
        self.deadband_pos = float(self.get_parameter('deadband_pos_m').value)
        self.deadband_rot = float(self.get_parameter('deadband_rot_rad').value)
        self.target_timeout = float(self.get_parameter('target_timeout_s').value)
        self.track_orientation = bool(self.get_parameter('track_orientation').value)

        self.tf_buffer = tf2_ros.Buffer()
        self.tf_listener = tf2_ros.TransformListener(self.tf_buffer, self)

        self.pub_twist = self.create_publisher(TwistStamped, self.output_topic, 10)
        self.create_subscription(PoseStamped, self.input_topic, self._on_target, 10)

        self._target: Optional[PoseStamped] = None
        self._target_stamp_ns: int = 0

        self.create_timer(1.0 / max(publish_hz, 1.0), self._tick)

        self.get_logger().info(
            f'pose_to_twist: {self.input_topic} → {self.output_topic} '
            f'@ {publish_hz:.0f} Hz, gains=(lin {self.linear_gain}, '
            f'ang {self.angular_gain}), max=({self.max_linear} m/s, '
            f'{self.max_angular} rad/s)'
        )

    def _on_target(self, msg: PoseStamped) -> None:
        if msg.header.frame_id and msg.header.frame_id != self.base_frame:
            self.get_logger().warn(
                f'target frame {msg.header.frame_id} != base {self.base_frame} — ignoring',
                throttle_duration_sec=2.0,
            )
            return
        self._target = msg
        self._target_stamp_ns = self.get_clock().now().nanoseconds

    def _tick(self) -> None:
        # Stale target → publish zero twist so Servo holds.
        now_ns = self.get_clock().now().nanoseconds
        if self._target is None or (now_ns - self._target_stamp_ns) * 1e-9 > self.target_timeout:
            self._publish_zero()
            return

        try:
            tf = self.tf_buffer.lookup_transform(
                self.base_frame, self.ee_frame, rclpy.time.Time())
        except (tf2_ros.LookupException, tf2_ros.ExtrapolationException, tf2_ros.ConnectivityException):
            self._publish_zero()
            return

        cur_pos = np.array([tf.transform.translation.x,
                            tf.transform.translation.y,
                            tf.transform.translation.z])
        cur_quat = np.array([tf.transform.rotation.x,
                             tf.transform.rotation.y,
                             tf.transform.rotation.z,
                             tf.transform.rotation.w])

        tgt = self._target.pose
        tgt_pos = np.array([tgt.position.x, tgt.position.y, tgt.position.z])
        tgt_quat = np.array([tgt.orientation.x, tgt.orientation.y,
                             tgt.orientation.z, tgt.orientation.w])

        # Linear: simple P-control with clamp + deadband.
        dpos = tgt_pos - cur_pos
        d = np.linalg.norm(dpos)
        if d < self.deadband_pos:
            lin = np.zeros(3)
        else:
            lin = dpos * self.linear_gain
            speed = np.linalg.norm(lin)
            if speed > self.max_linear:
                lin *= self.max_linear / speed

        # Angular: q_err = q_target * inverse(q_current) → axis-angle → ω.
        # Skip entirely if track_orientation is false — robot follows position only.
        if not self.track_orientation:
            ang = np.zeros(3)
        else:
            q_err = _quat_mul(tgt_quat, _quat_inverse(cur_quat))
            axis, angle = _quat_to_axis_angle(q_err)
            if abs(angle) < self.deadband_rot:
                ang = np.zeros(3)
            else:
                ang = axis * angle * self.angular_gain
                mag = np.linalg.norm(ang)
                if mag > self.max_angular:
                    ang *= self.max_angular / mag

        msg = TwistStamped()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.base_frame
        msg.twist.linear.x, msg.twist.linear.y, msg.twist.linear.z = float(lin[0]), float(lin[1]), float(lin[2])
        msg.twist.angular.x, msg.twist.angular.y, msg.twist.angular.z = float(ang[0]), float(ang[1]), float(ang[2])
        self.pub_twist.publish(msg)

    def _publish_zero(self) -> None:
        msg = TwistStamped()
        msg.header.stamp = self.get_clock().now().to_msg()
        msg.header.frame_id = self.base_frame
        self.pub_twist.publish(msg)


# ── quaternion helpers (xyzw convention, matches geometry_msgs) ──────────
def _quat_mul(a: np.ndarray, b: np.ndarray) -> np.ndarray:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return np.array([
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    ])


def _quat_inverse(q: np.ndarray) -> np.ndarray:
    return np.array([-q[0], -q[1], -q[2], q[3]])  # unit quat → conjugate


def _quat_to_axis_angle(q: np.ndarray) -> tuple[np.ndarray, float]:
    # Normalize defensively.
    n = np.linalg.norm(q)
    if n < 1e-9:
        return np.array([1.0, 0.0, 0.0]), 0.0
    q = q / n
    w = max(-1.0, min(1.0, q[3]))
    angle = 2.0 * math.acos(w)
    s = math.sqrt(max(0.0, 1.0 - w * w))
    if s < 1e-6:
        return np.array([1.0, 0.0, 0.0]), 0.0
    axis = np.array([q[0] / s, q[1] / s, q[2] / s])
    # Wrap angle to [-pi, pi] so we always rotate the short way.
    if angle > math.pi:
        angle -= 2.0 * math.pi
    return axis, angle


def main() -> None:
    rclpy.init()
    node = PoseToTwist()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
