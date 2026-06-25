"""Chain measurement v2 — 10s window, robust against key-release timing.

Counts NONZERO twists (not just last), peak twist magnitude, and command
drift vs joint state. Tells exactly which hop swallows the motion.
"""
import math
import time
import rclpy
from geometry_msgs.msg import TwistStamped
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray

R2D = 180.0 / math.pi
rclpy.init()
n = rclpy.create_node("chain_meter2")

st = {"tw": 0, "tw_nonzero": 0, "tw_peak": 0.0,
      "cmd": 0, "js": None, "cmd_min": None, "cmd_max": None}


def on_tw(m):
    st["tw"] += 1
    mag = max(abs(m.twist.linear.x), abs(m.twist.linear.y), abs(m.twist.linear.z),
              abs(m.twist.angular.x), abs(m.twist.angular.y), abs(m.twist.angular.z))
    if mag > 1e-9:
        st["tw_nonzero"] += 1
        st["tw_peak"] = max(st["tw_peak"], mag)


def on_js(m):
    st["js"] = list(m.position)


def on_cmd(m):
    d = list(m.data)
    st["cmd"] += 1
    if st["cmd_min"] is None:
        st["cmd_min"] = list(d)
        st["cmd_max"] = list(d)
    else:
        st["cmd_min"] = [min(a, b) for a, b in zip(st["cmd_min"], d)]
        st["cmd_max"] = [max(a, b) for a, b in zip(st["cmd_max"], d)]


n.create_subscription(TwistStamped, "/servo_node/delta_twist_cmds", on_tw, 50)
n.create_subscription(JointState, "/joint_states", on_js, 10)
n.create_subscription(Float64MultiArray, "/servo_node/commands", on_cmd, 50)

end = time.monotonic() + 10.0
while time.monotonic() < end:
    rclpy.spin_once(n, timeout_sec=0.05)

print(f"twists total         = {st['tw']}")
print(f"twists NONZERO       = {st['tw_nonzero']}")
print(f"twist peak magnitude = {st['tw_peak']}")
print(f"commands             = {st['cmd']}")
if st["cmd_min"]:
    span = [round((mx - mn) * R2D, 4) for mn, mx in zip(st["cmd_min"], st["cmd_max"])]
    print(f"cmd span per joint (deg) = {span}")
rclpy.shutdown()
