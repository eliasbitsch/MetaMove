"""Quick visual-demo stub: maps /metamove/ik_target Pose to /servo_node/commands.

This is NOT real IK — it's a crude proportional mapping so you can see the robot
respond to the Phantom-grab visualization without spinning up full MoveIt-Servo
pose-tracking. Real IK validation happens at the lab with the actual controller.

Mapping:
    ik_target.position.x   →   J1  (base yaw, ~30°/m)
    ik_target.position.y   →   J2  (shoulder pitch, ~20°/m)
    ik_target.position.z   →   J5  (wrist pitch, ~25°/m)
    ik_target.orientation  →   J6  (TCP roll from quat Z-axis)

Idle ik_target is at the EE's actual position, so motion starts at 0 delta and
the robot only moves when the user drags the phantom marker.

Also publishes /joint_states echoing the commands so any Servo / state monitor
sees a coherent current state.
"""
import math
import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray
from geometry_msgs.msg import PoseStamped


GAIN_XY = 30.0 * math.pi / 180.0   # rad per metre
GAIN_Z  = 25.0 * math.pi / 180.0
JOINT_NAMES = ["joint_1", "joint_2", "joint_3", "joint_4", "joint_5", "joint_6"]


class PhantomStub(Node):
    def __init__(self):
        super().__init__("phantom_ik_stub")
        self.create_subscription(PoseStamped, "/metamove/ik_target", self._on_target, 10)
        self.pub_cmd = self.create_publisher(Float64MultiArray, "/servo_node/commands", 10)
        self.pub_js = self.create_publisher(JointState, "/joint_states", 10)
        self.create_timer(1.0 / 30.0, self._tick)
        self._anchor = None       # first target position becomes the zero-point
        self._last_target = None
        self.get_logger().info("phantom IK stub: /metamove/ik_target → /servo_node/commands + /joint_states")

    def _on_target(self, msg: PoseStamped):
        self._last_target = msg

    def _tick(self):
        if self._last_target is None:
            return
        p = self._last_target.pose.position
        q = self._last_target.pose.orientation
        if self._anchor is None:
            self._anchor = (p.x, p.y, p.z)
        dx = p.x - self._anchor[0]
        dy = p.y - self._anchor[1]
        dz = p.z - self._anchor[2]

        # Crude proportional map to joints (degrees in rad)
        j1 = dx * GAIN_XY
        j2 = -dy * GAIN_XY
        j3 = 0.0
        j4 = 0.0
        j5 = dz * GAIN_Z
        # Yaw from quaternion (approx Z-axis rotation)
        siny_cosp = 2.0 * (q.w * q.z + q.x * q.y)
        cosy_cosp = 1.0 - 2.0 * (q.y * q.y + q.z * q.z)
        j6 = math.atan2(siny_cosp, cosy_cosp)

        positions = [j1, j2, j3, j4, j5, j6]

        cmd = Float64MultiArray()
        cmd.data = positions
        self.pub_cmd.publish(cmd)

        js = JointState()
        js.header.stamp = self.get_clock().now().to_msg()
        js.name = JOINT_NAMES
        js.position = positions
        self.pub_js.publish(js)


def main():
    rclpy.init()
    n = PhantomStub()
    try:
        rclpy.spin(n)
    except KeyboardInterrupt:
        pass
    finally:
        n.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
