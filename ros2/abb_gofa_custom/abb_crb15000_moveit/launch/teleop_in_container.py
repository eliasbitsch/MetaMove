"""
In-container Twist publisher for moveit_servo (no rosbridge hop).

Run via:
  docker exec -it <ros2-container> bash -c \
    "source /opt/ros/jazzy/setup.bash && \
     python3 /opt/metamove_ws/src/abb_gofa_custom/abb_crb15000_moveit/launch/teleop_in_container.py"

Bindings (continuous TwistStamped at 50 Hz, base_link frame):
  w/s: +x/-x   a/d: +y/-y   q/e: +z/-z
  i/k j/l u/o : rotation Rx/Ry/Rz
  shift hold = 5x
  ctrl-c to quit
"""
import sys, time, threading, termios, tty, select
import rclpy
from geometry_msgs.msg import TwistStamped


KEYS = {
    "w": ("lin", 0,  1.0),  "s": ("lin", 0, -1.0),
    "a": ("lin", 1,  1.0),  "d": ("lin", 1, -1.0),
    "q": ("lin", 2,  1.0),  "e": ("lin", 2, -1.0),
    "i": ("ang", 0,  1.0),  "k": ("ang", 0, -1.0),
    "j": ("ang", 1,  1.0),  "l": ("ang", 1, -1.0),
    "u": ("ang", 2,  1.0),  "o": ("ang", 2, -1.0),
}

LINEAR  = 0.01    # m/s baseline
ANGULAR = 0.20    # rad/s baseline


class _KeyState:
    """Background thread that tracks which keys are currently held."""
    def __init__(self):
        self.pressed: set[str] = set()
        self.shift = False
        self.alive = True
        threading.Thread(target=self._loop, daemon=True).start()

    def _loop(self):
        fd = sys.stdin.fileno()
        old = termios.tcgetattr(fd)
        tty.setcbreak(fd)
        try:
            while self.alive:
                r, _, _ = select.select([sys.stdin], [], [], 0.05)
                if not r:
                    # if no key seen for 200ms, clear pressed (release simulation)
                    self.pressed.clear()
                    continue
                ch = sys.stdin.read(1)
                if ch == "\x03":  # ctrl-c
                    self.alive = False
                    raise KeyboardInterrupt
                if ch == "\x1b":
                    self.alive = False
                    return
                # naive: re-pressed each event; clear happens on no-input slot
                self.pressed.add(ch.lower())
                self.shift = ch.isupper()
        finally:
            termios.tcsetattr(fd, termios.TCSADRAIN, old)


def main():
    rclpy.init()
    node = rclpy.create_node("teleop_keyboard_servo")
    pub = node.create_publisher(TwistStamped, "/servo_node/delta_twist_cmds", 10)
    keys = _KeyState()
    print("teleop active — w/a/s/d/q/e (translate)  i/j/k/l/u/o (rotate)  SHIFT=5x  Ctrl-C=exit")
    period = 1.0 / 50.0
    next_t = time.monotonic()
    try:
        while keys.alive:
            scale = 5.0 if keys.shift else 1.0
            lin = [0.0, 0.0, 0.0]
            ang = [0.0, 0.0, 0.0]
            for k, (kind, idx, sign) in KEYS.items():
                if k in keys.pressed:
                    if kind == "lin": lin[idx] += sign * LINEAR * scale
                    else:             ang[idx] += sign * ANGULAR * scale
            msg = TwistStamped()
            msg.header.stamp = node.get_clock().now().to_msg()
            msg.header.frame_id = "base_link"
            msg.twist.linear.x, msg.twist.linear.y, msg.twist.linear.z = lin
            msg.twist.angular.x, msg.twist.angular.y, msg.twist.angular.z = ang
            pub.publish(msg)
            rclpy.spin_once(node, timeout_sec=0.001)
            next_t += period
            sleep = next_t - time.monotonic()
            if sleep > 0: time.sleep(sleep)
            else:         next_t = time.monotonic()
    except KeyboardInterrupt:
        pass
    finally:
        keys.alive = False
        node.destroy_node()
        rclpy.shutdown()
        print("\nteleop stopped")


if __name__ == "__main__":
    main()
