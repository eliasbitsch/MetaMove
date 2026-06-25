"""Distance-based speed scaler — Quest min-distance -> continuous relay throttle.

Subscribes to /quest/min_distance (std_msgs/Float32, meters) published by the
Unity HumanDistanceHud (nearest human point <-> robot base via the QR spatial
anchor). The HUD shows the same number, so display and speed are consistent.

Maps distance to jtc_servo_relay.live_speed (0..1), continuously:
    dist <= d_near  ->  0.0   (human close -> FREEZE: safety)
    dist >= d_far   ->  1.0   (human far   -> full planned speed)
    linear in between

Smoothing (so Quest/QR tracking jitter doesn't make the robot judder):
  * EMA low-pass on the distance
  * asymmetric slew on live_speed: ramp UP slowly (smooth accel), drop DOWN
    immediately (safety stays reactive). Hard close / stale -> instant 0.

`enabled` param toggles the scaler (control_console: q=on / m=off for manual).
Fail-safe: no distance for stale_timeout s -> 0 (freeze).
"""
from __future__ import annotations

import threading
import time

import rclpy
from rclpy.node import Node
from std_msgs.msg import Bool, Float32
from std_srvs.srv import Trigger
from rcl_interfaces.srv import SetParameters
from rcl_interfaces.msg import Parameter, ParameterType, ParameterValue


class DistanceSpeedScaler(Node):
    def __init__(self) -> None:
        super().__init__('distance_speed_scaler')
        self.declare_parameter('enabled', True)
        self.declare_parameter('dist_topic', '/quest/min_distance')
        self.declare_parameter('relay_node', 'joint_trajectory_controller')
        self.declare_parameter('d_near', 0.6)        # m -> freeze
        self.declare_parameter('d_far', 2.0)         # m -> full speed
        self.declare_parameter('stale_timeout', 1.5)
        self.declare_parameter('ema_alpha', 0.3)     # distance smoothing (0..1)
        self.declare_parameter('up_rate', 0.6)       # max live_speed rise /s
        self.declare_parameter('min_delta', 0.01)
        self.declare_parameter('max_rate_hz', 15.0)
        # Safety: when the distance goes stale (e.g. Quest headset removed → app
        # pauses → no distance), cleanly PAUSE the playback so the robot stops;
        # RESUME it when distance returns. Cleaner than just live_speed=0 (which
        # cascades MoveGroup goal timeouts during a long stop).
        self.declare_parameter('pause_playback_on_stale', True)
        self.declare_parameter('playback_node', 'dpp_playback')

        self._lock = threading.Lock()
        self._raw_dist = None
        self._last_t = 0.0
        self._dist_filt = None
        self._v_out = 0.0
        self._last_sent = None
        self._last_send_t = 0.0
        self._log_t = 0.0
        self._tick_dt = 0.05

        self.create_subscription(Float32, self.get_parameter('dist_topic').value,
                                 self._on_dist, 10)
        # In-VR toggle (NearTouchButton -> ScalingModeToggle) flips the scaler on/off
        # over /quest/scaling_enabled, same effect as control_console q/m. True =
        # distance scaling owns live_speed; False = manual (scaler stops driving it).
        self.create_subscription(Bool, '/quest/scaling_enabled', self._on_enable_cmd, 10)
        # publish the effective speed factor back so the Quest HUD can show
        # "Speed %" + "Connected" (heartbeat) — the HUD reflects what the robot
        # actually does, not a local re-computation.
        self._speed_pub = self.create_publisher(Float32, '/robot/speed_factor', 10)
        self.param_cli = self.create_client(
            SetParameters,
            f"/{self.get_parameter('relay_node').value}/set_parameters")
        pb = self.get_parameter('playback_node').value
        self._pause_cli = self.create_client(Trigger, f'/{pb}/pause')
        self._resume_cli = self.create_client(Trigger, f'/{pb}/resume')
        self._was_paused = None
        self.create_timer(self._tick_dt, self._tick)
        self.get_logger().info(
            f"distance_speed_scaler up — {self.get_parameter('dist_topic').value} "
            f"-> {self.get_parameter('relay_node').value}.live_speed "
            f"(EMA + slew, continuous)")

    def _on_dist(self, msg: Float32) -> None:
        with self._lock:
            self._raw_dist = float(msg.data)
            self._last_t = time.monotonic()

    def _on_enable_cmd(self, msg: Bool) -> None:
        on = bool(msg.data)
        # rclpy.Parameter (not rcl_interfaces.msg.Parameter, which is imported
        # below for the SetParameters service); value-only ctor infers BOOL.
        self.set_parameters([rclpy.Parameter('enabled', value=on)])
        self.get_logger().info(
            f"scaling_enabled <- {'AUTO' if on else 'MANUELL'} (Quest-Toggle)")

    def _band(self, d: float) -> float:
        dn = float(self.get_parameter('d_near').value)
        df = float(self.get_parameter('d_far').value)
        if d <= dn:
            return 0.0
        if d >= df:
            return 1.0
        return (d - dn) / max(1e-6, df - dn)

    def _tick(self) -> None:
        enabled = bool(self.get_parameter('enabled').value)

        with self._lock:
            raw = self._raw_dist
            age = time.monotonic() - self._last_t if self._last_t else 1e9
        stale = age > float(self.get_parameter('stale_timeout').value)

        # Unified playback gate: pause the path if MANUAL (the IK relay owns
        # /servo_node/commands) OR the distance is stale (human absent / headset off).
        # Resume only in AUTO with a fresh human reading.
        if bool(self.get_parameter('pause_playback_on_stale').value):
            desired_paused = (not enabled) or stale
            if desired_paused != self._was_paused:
                self._was_paused = desired_paused
                cli = self._pause_cli if desired_paused else self._resume_cli
                if cli.service_is_ready():
                    cli.call_async(Trigger.Request())
                reason = 'MANUELL (IK)' if not enabled else 'keine Distanz (Brille ab?)'
                self.get_logger().info(
                    f'STOP — {reason} → Playback pausiert' if desired_paused
                    else 'WEITER — AUTO + Mensch da → Playback resumed')

        if not enabled:
            now = time.monotonic()
            if now - self._log_t > 2.0:
                self._log_t = now
                self.get_logger().info('MANUELL (Skalierer aus)')
            self._last_sent = None
            self._dist_filt = None
            return

        dn = float(self.get_parameter('d_near').value)
        if stale or raw is None:
            v_target = 0.0
            self._dist_filt = None
        else:
            a = float(self.get_parameter('ema_alpha').value)
            self._dist_filt = raw if self._dist_filt is None \
                else a * raw + (1 - a) * self._dist_filt
            v_target = self._band(self._dist_filt)
            if raw <= dn:                      # hard safety on RAW distance
                v_target = 0.0

        # asymmetric slew: up = limited, down = immediate
        up_step = float(self.get_parameter('up_rate').value) * self._tick_dt
        if v_target >= self._v_out:
            self._v_out = min(v_target, self._v_out + up_step)
        else:
            self._v_out = v_target
        v = self._v_out

        # heartbeat the effective speed factor to the HUD every tick
        self._speed_pub.publish(Float32(data=float(v)))

        now = time.monotonic()
        if now - self._log_t > 1.0:
            self._log_t = now
            dtxt = ('STALE' if stale else
                    (f'{self._dist_filt:.2f} m' if self._dist_filt is not None else '--'))
            self.get_logger().info(f'dist={dtxt}  -> live_speed={v:.3f}')

        if (now - self._last_send_t) < 1.0 / float(self.get_parameter('max_rate_hz').value):
            return
        if self._last_sent is not None and abs(v - self._last_sent) < float(self.get_parameter('min_delta').value):
            return
        if not self.param_cli.service_is_ready():
            return
        req = SetParameters.Request()
        pr = Parameter()
        pr.name = 'live_speed'
        pr.value = ParameterValue(type=ParameterType.PARAMETER_DOUBLE,
                                  double_value=float(v))
        req.parameters.append(pr)
        self.param_cli.call_async(req)
        self._last_sent = v
        self._last_send_t = now


def main() -> None:
    rclpy.init()
    node = DistanceSpeedScaler()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    node.destroy_node()
    rclpy.shutdown()


if __name__ == '__main__':
    main()
