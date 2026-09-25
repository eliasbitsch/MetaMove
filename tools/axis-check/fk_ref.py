"""Reference FK from MoveIt /compute_fk for a fixed set of joint configs.

Runs inside the ROS container while move_group is up. Prints JSON to stdout:
{config: {"q": [6 rad], "links": {link_i: [x,y,z,qx,qy,qz,qw]}}} in base_link.
See run.ps1 for the whole check."""
import json, rclpy
from rclpy.node import Node
from moveit_msgs.srv import GetPositionFK
from sensor_msgs.msg import JointState

J = [f"joint_{i}" for i in range(1, 7)]
LINKS = [f"link_{i}" for i in range(1, 7)]
CONFIGS = {"zero": [0, 0, 0, 0, 0, 0], "mix": [0.4, 0.3, -0.5, 0.6, 0.7, 0.8]}
for i in range(6):
    for s in (+1, -1):
        q = [0.0] * 6
        q[i] = 0.5 * s
        q[4] = q[4] if i == 4 else 0.3  # bend the wrist so j4/j6 move something
        CONFIGS[f"j{i+1}{'+' if s > 0 else '-'}"] = q

rclpy.init()
n = Node("fk_ref")
cli = n.create_client(GetPositionFK, "/compute_fk")
cli.wait_for_service(timeout_sec=10)
out = {}
for name, q in CONFIGS.items():
    req = GetPositionFK.Request()
    req.header.frame_id = "base_link"
    req.fk_link_names = LINKS
    req.robot_state.joint_state = JointState(name=J, position=[float(v) for v in q])
    fut = cli.call_async(req)
    rclpy.spin_until_future_complete(n, fut, timeout_sec=5)
    r = fut.result()
    out[name] = {"q": q, "links": {
        l: [ps.pose.position.x, ps.pose.position.y, ps.pose.position.z,
                                   ps.pose.orientation.x, ps.pose.orientation.y, ps.pose.orientation.z, ps.pose.orientation.w]
        for l, ps in zip(r.fk_link_names, r.pose_stamped)}}
print(json.dumps(out))
rclpy.shutdown()
