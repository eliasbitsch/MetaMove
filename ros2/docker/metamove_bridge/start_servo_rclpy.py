"""Direct rclpy probe+start for moveit_servo — bypasses ros2 CLI daemon & rosbridge.

Decides whether /servo_node/start_servo is genuinely unreachable (duplicate-node
discovery breakage) or whether earlier failures were just CLI/rosbridge quirks.
"""
import sys
import rclpy
from std_srvs.srv import Trigger

rclpy.init()
node = rclpy.create_node("servo_starter_probe")

# Enumerate what discovery actually sees
names_types = dict(node.get_service_names_and_types())
servo_services = {n: t for n, t in names_types.items() if "servo" in n}
print("=== servo services seen by a clean rclpy node ===")
for n, t in sorted(servo_services.items()):
    print(f"  {n}  {t}")

cli = node.create_client(Trigger, "/servo_node/start_servo")
ready = cli.wait_for_service(timeout_sec=5.0)
print(f"\nstart_servo reachable = {ready}")
if not ready:
    print("=> service server not answering discovery (likely duplicate-node graph)")
    rclpy.shutdown()
    sys.exit(3)

fut = cli.call_async(Trigger.Request())
rclpy.spin_until_future_complete(node, fut, timeout_sec=8.0)
res = fut.result()
if res is None:
    print("=> call timed out (server advertised but not responding)")
    rclpy.shutdown()
    sys.exit(4)
print(f"start_servo -> success={res.success} message={res.message!r}")

# Switch command type to TWIST
try:
    from moveit_msgs.srv import ServoCommandType
    sc = node.create_client(ServoCommandType, "/servo_node/switch_command_type")
    if sc.wait_for_service(timeout_sec=5.0):
        req = ServoCommandType.Request()
        req.command_type = 1  # TWIST
        f2 = sc.call_async(req)
        rclpy.spin_until_future_complete(node, f2, timeout_sec=5.0)
        r2 = f2.result()
        print(f"switch_command_type(TWIST) -> success={getattr(r2,'success',None)}")
    else:
        print("switch_command_type not reachable")
except Exception as e:  # noqa: BLE001
    print(f"switch_command_type error: {e}")

rclpy.shutdown()
