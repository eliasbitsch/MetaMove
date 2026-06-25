"""Single-axis sweep to verify Unity joint directions against RViz/ground-truth.

Pauses nothing itself — call after pausing dpp_playback. Moves ONE joint at a
time to +0.6 rad (~+34 deg), holds, returns home. Watch Unity vs RViz: if a
joint rotates the OPPOSITE way in Unity, that axis needs signFlip.
"""
import time
import rclpy
from std_msgs.msg import Float64MultiArray


def main():
    rclpy.init()
    node = rclpy.create_node('axis_check')
    pub = node.create_publisher(Float64MultiArray, '/servo_node/commands', 10)
    time.sleep(1.0)

    def send(v, hold):
        end = time.time() + hold
        while time.time() < end:
            pub.publish(Float64MultiArray(data=list(v)))
            rclpy.spin_once(node, timeout_sec=0.02)
            time.sleep(0.05)

    home = [0.0] * 6
    print('=> HOME', flush=True)
    send(home, 2.0)
    for i in range(6):
        v = [0.0] * 6
        v[i] = 0.6
        print(f'=> J{i + 1}  +0.6 rad (+34 deg)  [watch Unity vs RViz]', flush=True)
        send(v, 4.0)
        print('   back HOME', flush=True)
        send(home, 1.5)
    print('=> sweep done', flush=True)
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
