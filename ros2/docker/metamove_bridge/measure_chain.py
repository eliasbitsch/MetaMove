"""Count messages on the servo chain for 8s to locate where data stops.

delta=0           -> teleop not publishing (keyboard/focus issue)
delta>0, cmd=0    -> servo not converting twist->joints (servo halted/inactive)
delta>0, cmd>0    -> data reaches bridge; issue is bridge->EGM->controller
"""
import time
import rclpy
from geometry_msgs.msg import TwistStamped
from std_msgs.msg import Float64MultiArray
from moveit_msgs.msg import ServoStatus

rclpy.init()
n = rclpy.create_node("chain_meter")
c = {"delta": 0, "cmd": 0, "status_codes": {}}
last_twist = {"v": None}


def od(msg):
    c["delta"] += 1
    last_twist["v"] = (msg.twist.linear.x, msg.twist.linear.y, msg.twist.linear.z,
                       msg.twist.angular.x, msg.twist.angular.y, msg.twist.angular.z)


def oc(msg):
    c["cmd"] += 1


def os_(msg):
    c["status_codes"][msg.code] = c["status_codes"].get(msg.code, 0) + 1


n.create_subscription(TwistStamped, "/servo_node/delta_twist_cmds", od, 10)
n.create_subscription(Float64MultiArray, "/servo_node/commands", oc, 10)
n.create_subscription(ServoStatus, "/servo_node/status", os_, 10)

end = time.monotonic() + 8.0
while time.monotonic() < end:
    rclpy.spin_once(n, timeout_sec=0.1)

print(f"delta_twist_cmds msgs = {c['delta']}")
print(f"commands msgs         = {c['cmd']}")
print(f"last twist            = {last_twist['v']}")
print(f"status codes          = {c['status_codes']}")
rclpy.shutdown()
