"""Oscillate ONE joint slowly so its rotation direction is unmistakable.

Usage:  python3 axis_test.py <joint 1-6> [amp_rad=0.5] [secs=12]
Publishes /servo_node/commands with only that joint moving (sine), others at 0.
Watch Unity (Scene view) vs RViz: same direction = OK, opposite = needs signFlip.
Pause dpp_playback first so it doesn't fight.
"""
import sys
import math
import time
import rclpy
from std_msgs.msg import Float64MultiArray


def main():
    joint = int(sys.argv[1]) if len(sys.argv) > 1 else 1   # 1..6
    amp = float(sys.argv[2]) if len(sys.argv) > 2 else 0.5
    secs = float(sys.argv[3]) if len(sys.argv) > 3 else 12.0
    period = 4.0
    idx = max(0, min(5, joint - 1))

    rclpy.init()
    node = rclpy.create_node('axis_test')
    pub = node.create_publisher(Float64MultiArray, '/servo_node/commands', 10)
    time.sleep(0.5)

    print(f'J{joint}: oscillating +-{amp} rad ({math.degrees(amp):.0f} deg), {secs:.0f}s', flush=True)
    t0 = time.time()
    while time.time() - t0 < secs:
        t = time.time() - t0
        v = [0.0] * 6
        v[idx] = amp * math.sin(2 * math.pi * t / period)
        pub.publish(Float64MultiArray(data=v))
        rclpy.spin_once(node, timeout_sec=0.01)
        time.sleep(0.03)

    for _ in range(12):
        pub.publish(Float64MultiArray(data=[0.0] * 6))
        rclpy.spin_once(node, timeout_sec=0.01)
        time.sleep(0.03)
    print(f'J{joint}: done (home)', flush=True)
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
