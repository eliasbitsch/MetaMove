"""Persistent in-container servo auto-starter.

Runs as a long-lived node (stable DDS participant -> reliable service
discovery, unlike a transient `ros2 service call` from `docker exec`).

Sequence:
  1. wait for /joint_states (so servo has robot state)
  2. wait for /servo_node/start_servo, call it
  3. switch command type to TWIST
  4. keep spinning so the participant stays alive (and re-arms if servo restarts)

Designed to be added to the launch, but also runnable standalone for testing:
  docker exec -e ROS_DOMAIN_ID=42 metamove-servo bash -lc \
    'source /opt/metamove_ws/install/setup.bash && \
     python3 /opt/metamove_ws/src/metamove_bridge/servo_autostart.py'
"""
import sys
import time

import rclpy
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_srvs.srv import Trigger

try:
    from moveit_msgs.srv import ServoCommandType
    HAVE_SCT = True
except Exception:  # noqa: BLE001
    HAVE_SCT = False

TWIST = 1


class ServoAutostart(Node):
    def __init__(self):
        super().__init__("servo_autostart")
        self._have_state = False
        self.create_subscription(JointState, "/joint_states",
                                 self._on_js, 10)
        # moveit_servo 2.12 has NO start_servo service. Activation = switch the
        # command type to TWIST once the node has robot state, then publish
        # commands. Calling the (nonexistent) start_servo is what made earlier
        # attempts hang forever.
        self.switch_cli = (self.create_client(ServoCommandType,
                           "/servo_node/switch_command_type")
                           if HAVE_SCT else None)
        self._done = False
        self.create_timer(1.0, self._tick)

    def _on_js(self, _msg):
        self._have_state = True

    def _tick(self):
        if self._done:
            return
        if not self._have_state:
            self.get_logger().info("autostart: waiting for /joint_states ...")
            return
        if not (self.switch_cli and self.switch_cli.service_is_ready()):
            self.get_logger().info("autostart: waiting for switch_command_type ...")
            return
        self.get_logger().info("autostart: switching command type -> TWIST")
        req = ServoCommandType.Request()
        req.command_type = TWIST
        fut = self.switch_cli.call_async(req)
        fut.add_done_callback(self._after_switch)
        self._done = True  # don't re-fire while in flight

    def _after_switch(self, fut):
        try:
            res = fut.result()
            ok = getattr(res, "success", None)
            self.get_logger().info(
                f"autostart: switch_command_type(TWIST) success={ok}")
            if not ok:
                self._done = False
                return
        except Exception as e:  # noqa: BLE001
            self.get_logger().error(f"autostart: switch failed: {e}")
            self._done = False
            return
        self.get_logger().info("autostart: servo ACTIVE (TWIST) — jog away")


def main():
    rclpy.init()
    node = ServoAutostart()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        rclpy.shutdown()


if __name__ == "__main__":
    sys.exit(main())
