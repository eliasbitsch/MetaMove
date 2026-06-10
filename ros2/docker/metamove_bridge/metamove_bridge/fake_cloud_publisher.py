"""Synthetic PointCloud2 publisher for octomap_server sanity testing.

Publishes a rotating 60cm cube of points on /cloud_in at 10 Hz, expressed in
frame `gofa_base_link` 1.0 m in front of the robot. No sensor, no Quest — pure
test data so we can verify the octomap pipeline before plumbing anything real.
"""
from __future__ import annotations

import math
import struct

import numpy as np
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import PointCloud2, PointField
from std_msgs.msg import Header


def _make_cube_points(side: float, step: float) -> np.ndarray:
    axis = np.arange(-side / 2.0, side / 2.0 + step / 2.0, step, dtype=np.float32)
    xs, ys, zs = np.meshgrid(axis, axis, axis, indexing="ij")
    pts = np.stack([xs.ravel(), ys.ravel(), zs.ravel()], axis=1)
    # Hollow cube — keep only points within `step` of the surface to mimic a
    # surface scan rather than a solid blob.
    half = side / 2.0
    surface = np.max(np.abs(pts), axis=1) >= (half - step * 0.5)
    return pts[surface]


class FakeCloudPublisher(Node):
    def __init__(self) -> None:
        super().__init__("fake_cloud_publisher")
        self.declare_parameter("frame_id", "gofa_base_link")
        self.declare_parameter("topic", "/cloud_in")
        self.declare_parameter("rate_hz", 10.0)
        self.declare_parameter("offset_x", 1.0)
        self.declare_parameter("offset_z", 0.4)
        self.declare_parameter("cube_side", 0.6)
        self.declare_parameter("point_step", 0.04)
        self.declare_parameter("rotate", True)

        self.frame_id = self.get_parameter("frame_id").value
        self.offset = np.array(
            [self.get_parameter("offset_x").value, 0.0, self.get_parameter("offset_z").value],
            dtype=np.float32,
        )
        self.rotate = bool(self.get_parameter("rotate").value)
        rate = float(self.get_parameter("rate_hz").value)

        side = float(self.get_parameter("cube_side").value)
        step = float(self.get_parameter("point_step").value)
        self.cube = _make_cube_points(side, step)
        self.get_logger().info(f"fake cloud: {len(self.cube)} points, side={side}m, step={step}m")

        self.pub = self.create_publisher(PointCloud2, self.get_parameter("topic").value, 10)
        self.t0 = self.get_clock().now().nanoseconds * 1e-9
        self.create_timer(1.0 / rate, self._tick)

    def _tick(self) -> None:
        if self.rotate:
            t = self.get_clock().now().nanoseconds * 1e-9 - self.t0
            yaw = (t * 0.5) % (2 * math.pi)
            c, s = math.cos(yaw), math.sin(yaw)
            rot = np.array([[c, -s, 0.0], [s, c, 0.0], [0.0, 0.0, 1.0]], dtype=np.float32)
            pts = self.cube @ rot.T
        else:
            pts = self.cube
        pts = pts + self.offset
        self.pub.publish(self._to_msg(pts))

    def _to_msg(self, pts: np.ndarray) -> PointCloud2:
        header = Header()
        header.stamp = self.get_clock().now().to_msg()
        header.frame_id = self.frame_id
        fields = [
            PointField(name="x", offset=0,  datatype=PointField.FLOAT32, count=1),
            PointField(name="y", offset=4,  datatype=PointField.FLOAT32, count=1),
            PointField(name="z", offset=8,  datatype=PointField.FLOAT32, count=1),
        ]
        data = b"".join(struct.pack("<fff", *p) for p in pts.astype(np.float32))
        return PointCloud2(
            header=header,
            height=1,
            width=pts.shape[0],
            is_dense=True,
            is_bigendian=False,
            fields=fields,
            point_step=12,
            row_step=12 * pts.shape[0],
            data=data,
        )


def main() -> None:
    rclpy.init()
    node = FakeCloudPublisher()
    try:
        rclpy.spin(node)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
