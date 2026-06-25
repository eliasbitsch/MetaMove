"""Compare /servo_node/commands to /joint_states for 8s.

If commands == current state  -> servo is outputting "hold position"
   (singularity/limit halt) -> SERVO side problem.
If commands drift away from state -> servo IS commanding motion, but robot
   doesn't follow -> CONTROLLER/EGM side problem (e.g. EGM in pose mode,
   ignoring joint corrections).
"""
import math
import time
import rclpy
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray

R2D = 180.0 / math.pi
rclpy.init()
n = rclpy.create_node("cmd_vs_state")
st = {"js": None, "first_cmd": None, "last_cmd": None, "ncmd": 0, "maxdelta": 0.0}


def on_js(m):
    st["js"] = list(m.position)


def on_cmd(m):
    d = list(m.data)
    st["ncmd"] += 1
    if st["first_cmd"] is None:
        st["first_cmd"] = d
    st["last_cmd"] = d
    if st["js"] and len(d) == len(st["js"]):
        st["maxdelta"] = max(st["maxdelta"],
                             max(abs(a - b) for a, b in zip(d, st["js"])))


n.create_subscription(JointState, "/joint_states", on_js, 10)
n.create_subscription(Float64MultiArray, "/servo_node/commands", on_cmd, 10)

end = time.monotonic() + 8.0
while time.monotonic() < end:
    rclpy.spin_once(n, timeout_sec=0.1)


def fmt(v):
    return None if v is None else [round(x * R2D, 3) for x in v]


print(f"commands received     = {st['ncmd']}")
print(f"current state (deg)   = {fmt(st['js'])}")
print(f"first command (deg)   = {fmt(st['first_cmd'])}")
print(f"last command  (deg)   = {fmt(st['last_cmd'])}")
print(f"max |cmd-state| (deg) = {round(st['maxdelta'] * R2D, 3)}")
if st["first_cmd"] and st["last_cmd"]:
    drift = max(abs(a - b) for a, b in zip(st["first_cmd"], st["last_cmd"])) * R2D
    print(f"cmd drift first->last = {round(drift, 3)} deg")
rclpy.shutdown()
