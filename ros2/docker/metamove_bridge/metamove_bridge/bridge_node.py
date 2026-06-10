"""
MetaMove RWS bridge node.

Two roles:
1. Telemetry + service-trigger (status polling, demos, grip, motors-on/off, event log)
2. Sim-motion-bridge: subscribe /servo_node/commands and forward joint targets
   via RWS REST to a PERS jointtarget in RAPID. Pair with MetaMoveCorePers.mod.

Topics published:
  /metamove/robot_state   std_msgs/String   JSON blob of latest RWS snapshot (~2 Hz)
  /metamove/demo_state    std_msgs/String   {id, state, step}
  /metamove/event_log     std_msgs/String   RWS elog entries as they arrive
  /metamove/motion_rate   std_msgs/String   {hz_in, hz_out, last_latency_ms}

Topics subscribed (sim-motion path, when servo_bridge=true):
  /servo_node/commands    std_msgs/Float64MultiArray   6 joint positions in rad

Services:
  /metamove/run_demo        std_srvs/Trigger (param: scenario via ROS param)
  /metamove/abort           std_srvs/Trigger
  /metamove/grip_open       std_srvs/Trigger
  /metamove/grip_close      std_srvs/Trigger
  /metamove/motors_on       std_srvs/Trigger
  /metamove/motors_off      std_srvs/Trigger

The RWS client here is deliberately small (requests + urllib3). Swap to the
official ABB PC SDK or abb_librws if you need push-subscriptions beyond polling.
"""
from __future__ import annotations

import json
import math
import os
import threading
import time
from typing import Any

import rclpy
import requests
import urllib3
from rclpy.node import Node
from sensor_msgs.msg import JointState
from std_msgs.msg import Float64MultiArray, String
from std_srvs.srv import Trigger

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


DEMO_IDS = {
    'chess': 1,
    'stone_sort': 2,
    'framing': 3,
    'mug': 4,
    'pins': 5,
    'bigstone': 6,
}

RAD2DEG = 180.0 / math.pi


class RwsClient:
    """Minimal RWS REST wrapper. Session-based digest auth."""

    def __init__(self, host: str, port: int, user: str, password: str):
        self.base = f'https://{host}:{port}'
        self.session = requests.Session()
        # OmniCore RWS 2.0 uses Basic auth, not Digest (legacy IRC5 was Digest)
        self.session.auth = (user, password)
        self.session.verify = False
        self.session.headers.update({'Accept': 'application/hal+json;v=2.0'})

    def get(self, path: str) -> dict[str, Any]:
        r = self.session.get(self.base + path, timeout=2.0)
        r.raise_for_status()
        return r.json() if r.content else {}

    def post(self, path: str, data: dict[str, str] | None = None) -> None:
        r = self.session.post(self.base + path, data=data or {}, timeout=2.0)
        r.raise_for_status()

    def set_rapid_var(self, task: str, module: str, name: str, value: str) -> None:
        path = f'/rw/rapid/symbol/RAPID/{task}/{module}/{name}/data'
        self.post(path, {'value': value})

    def set_io(self, signal: str, value: str) -> None:
        self.post(f'/rw/iosystem/signals/{signal}/set-value', {'lvalue': value})

    def motors(self, on: bool) -> None:
        self.post('/rw/panel/ctrl-state', {'ctrl-state': 'motoron' if on else 'motoroff'})

    @staticmethod
    def format_jointtarget(joints_deg: list[float]) -> str:
        """RAPID jointtarget literal: [[J1,J2,J3,J4,J5,J6],[E1,E2,E3,E4,E5,E6]]
        External axes set to 9E9 (= not connected)."""
        j = ','.join(f'{v:.4f}' for v in joints_deg)
        ext = '9E9,9E9,9E9,9E9,9E9,9E9'
        return f'[[{j}],[{ext}]]'


class MetaMoveBridge(Node):
    def __init__(self) -> None:
        super().__init__('metamove_bridge')

        self.declare_parameter('rws_ip', os.environ.get('METAMOVE_RWS_IP', '192.168.125.1'))
        self.declare_parameter('rws_port', int(os.environ.get('METAMOVE_RWS_PORT', '443')))
        self.declare_parameter('rws_user', 'Default User')
        self.declare_parameter('rws_password', 'robotics')
        self.declare_parameter('rapid_task', 'T_ROB1')
        self.declare_parameter('rapid_module', 'MetaMoveDemos')
        self.declare_parameter('poll_hz', 2.0)

        # Sim-motion path config
        self.declare_parameter('servo_bridge', False)
        self.declare_parameter('servo_module', 'MetaMoveCorePers')
        self.declare_parameter('servo_var', 'jTarget')
        self.declare_parameter('servo_max_rate_hz', 30.0)  # cap outbound RWS writes
        self.declare_parameter('joint_poll_hz', 30.0)      # cap inbound /joint_states publish rate
        self.declare_parameter('joint_names', [
            'joint_1','joint_2','joint_3','joint_4','joint_5','joint_6'
        ])

        self.rws = RwsClient(
            self.get_parameter('rws_ip').value,
            self.get_parameter('rws_port').value,
            self.get_parameter('rws_user').value,
            self.get_parameter('rws_password').value,
        )
        self.rapid_task = self.get_parameter('rapid_task').value
        self.rapid_module = self.get_parameter('rapid_module').value
        self.servo_module = self.get_parameter('servo_module').value
        self.servo_var = self.get_parameter('servo_var').value

        # Publishers
        self.pub_state = self.create_publisher(String, '/metamove/robot_state', 10)
        self.pub_demo = self.create_publisher(String, '/metamove/demo_state', 10)
        self.pub_log = self.create_publisher(String, '/metamove/event_log', 20)
        self.pub_motion = self.create_publisher(String, '/metamove/motion_rate', 10)

        # Services
        self.create_service(Trigger, '/metamove/run_demo', self._srv_run_demo)
        self.create_service(Trigger, '/metamove/abort', self._srv_abort)
        self.create_service(Trigger, '/metamove/grip_open', self._srv_grip_open)
        self.create_service(Trigger, '/metamove/grip_close', self._srv_grip_close)
        self.create_service(Trigger, '/metamove/motors_on', self._srv_motors_on)
        self.create_service(Trigger, '/metamove/motors_off', self._srv_motors_off)

        # Parameter for scenario name used by run_demo service
        self.declare_parameter('scenario', 'chess')

        poll_hz = float(self.get_parameter('poll_hz').value)
        self.create_timer(1.0 / max(poll_hz, 0.1), self._poll)

        # Sim-motion subscriber (only attached if explicitly enabled)
        self._motion_in = 0
        self._motion_out = 0
        self._motion_last_latency_ms = 0.0
        self._motion_last_send = 0.0
        self._motion_max_period = 1.0 / max(float(self.get_parameter('servo_max_rate_hz').value), 1.0)
        self._motion_lock = threading.Lock()
        self._motion_pending: list[float] | None = None
        self._motion_thread_running = False

        if bool(self.get_parameter('servo_bridge').value):
            self.create_subscription(Float64MultiArray, '/servo_node/commands', self._on_servo_cmd, 10)
            self.create_timer(1.0, self._publish_motion_rate)
            self.get_logger().info(
                f'servo bridge ON, writing to RAPID/{self.rapid_task}/'
                f'{self.servo_module}/{self.servo_var} at max '
                f'{self.get_parameter("servo_max_rate_hz").value} Hz'
            )

            # /joint_states publisher driven by RWS poll
            self.pub_joints = self.create_publisher(JointState, '/joint_states', 10)
            self._joint_names = list(self.get_parameter('joint_names').value)
            poll_hz = float(self.get_parameter('joint_poll_hz').value)
            self.create_timer(1.0 / max(poll_hz, 1.0), self._poll_joints_async)
            self._joint_poll_running = False
            self.get_logger().info(
                f'joint poll ON, GET /rw/motionsystem/mechunits/ROB_1/jointtarget '
                f'at {poll_hz} Hz'
            )

        self.get_logger().info(f'bridge up, RWS={self.rws.base}')

    # ------------------------------------------------------------------ polling
    def _poll(self) -> None:
        def work() -> None:
            try:
                ctrl = self.rws.get('/rw/panel/ctrl-state')
                exec_state = self.rws.get('/rw/rapid/execution')
                snapshot = {
                    'ts': time.time(),
                    'ctrl': ctrl,
                    'exec': exec_state,
                }
                self.pub_state.publish(String(data=json.dumps(snapshot)))
            except Exception as e:
                self.get_logger().warn(f'poll failed: {e}')

        threading.Thread(target=work, daemon=True).start()

    # ------------------------------------------------------------ sim-motion path
    def _on_servo_cmd(self, msg: Float64MultiArray) -> None:
        if msg.data is None or len(msg.data) < 6:
            return
        joints_deg = [float(msg.data[i]) * RAD2DEG for i in range(6)]
        self._motion_in += 1

        # Coalesce: store latest, fire one worker thread that drains until idle.
        with self._motion_lock:
            self._motion_pending = joints_deg
            if self._motion_thread_running:
                return
            self._motion_thread_running = True

        threading.Thread(target=self._motion_drain, daemon=True).start()

    def _motion_drain(self) -> None:
        try:
            while True:
                with self._motion_lock:
                    target = self._motion_pending
                    self._motion_pending = None
                    if target is None:
                        self._motion_thread_running = False
                        return

                # Rate-limit outbound writes
                now = time.perf_counter()
                wait = self._motion_max_period - (now - self._motion_last_send)
                if wait > 0:
                    time.sleep(wait)

                t0 = time.perf_counter()
                try:
                    literal = RwsClient.format_jointtarget(target)
                    self.rws.set_rapid_var(self.rapid_task, self.servo_module, self.servo_var, literal)
                    self._motion_last_latency_ms = (time.perf_counter() - t0) * 1000.0
                    self._motion_out += 1
                    self._motion_last_send = time.perf_counter()
                except Exception as e:
                    self.get_logger().warn(f'servo write failed: {e}')
                    self._motion_last_send = time.perf_counter()
        except Exception as e:
            self.get_logger().error(f'motion drain crashed: {e}')
            with self._motion_lock:
                self._motion_thread_running = False

    def _poll_joints_async(self) -> None:
        """Fire-and-forget thread that polls RWS jointtarget and publishes JointState."""
        if self._joint_poll_running:
            return
        self._joint_poll_running = True

        def work() -> None:
            try:
                data = self.rws.get('/rw/motionsystem/mechunits/ROB_1/jointtarget')
                # OmniCore returns {"_embedded":{"resources":[{"rax_1":..,"rax_2":..,...}]}} or similar
                joints_deg = self._extract_joints_deg(data)
                if joints_deg is None:
                    return
                msg = JointState()
                msg.header.stamp = self.get_clock().now().to_msg()
                msg.name = self._joint_names
                msg.position = [j * 0.017453292519943295 for j in joints_deg]  # deg → rad
                self.pub_joints.publish(msg)
            except Exception as e:
                self.get_logger().debug(f'joint poll failed: {e}')
            finally:
                self._joint_poll_running = False

        threading.Thread(target=work, daemon=True).start()

    @staticmethod
    def _extract_joints_deg(data: dict) -> list[float] | None:
        """Walk the RWS response and pull rax_1..rax_6 as floats. RW 7.x OmniCore
        returns under data['state'][0]; older RW under data['_embedded']['resources'][0].
        rax_* values can be strings or numbers."""
        candidates = []
        try:
            if 'state' in data and isinstance(data['state'], list):
                candidates.extend(data['state'])
            embedded = data.get('_embedded')
            if embedded and isinstance(embedded.get('resources'), list):
                candidates.extend(embedded['resources'])
            if all(f'rax_{i}' in data for i in range(1, 7)):
                candidates.append(data)
        except Exception:
            return None
        for r in candidates:
            try:
                if all(f'rax_{i}' in r for i in range(1, 7)):
                    return [float(r[f'rax_{i}']) for i in range(1, 7)]
            except (TypeError, ValueError):
                continue
        return None

    def _publish_motion_rate(self) -> None:
        msg = String(data=json.dumps({
            'hz_in': self._motion_in,
            'hz_out': self._motion_out,
            'last_latency_ms': round(self._motion_last_latency_ms, 2),
        }))
        self.pub_motion.publish(msg)
        self._motion_in = 0
        self._motion_out = 0

    # ----------------------------------------------------------------- services
    def _ok(self, resp: Trigger.Response, msg: str = 'ok') -> Trigger.Response:
        resp.success = True
        resp.message = msg
        return resp

    def _fail(self, resp: Trigger.Response, e: Exception) -> Trigger.Response:
        resp.success = False
        resp.message = f'{type(e).__name__}: {e}'
        self.get_logger().error(resp.message)
        return resp

    def _srv_run_demo(self, _req, resp):
        scenario = str(self.get_parameter('scenario').value)
        if scenario not in DEMO_IDS:
            resp.success = False
            resp.message = f'unknown scenario "{scenario}". One of: {list(DEMO_IDS)}'
            return resp
        try:
            self.rws.set_rapid_var(self.rapid_task, self.rapid_module, 'demoId', str(DEMO_IDS[scenario]))
            self.rws.set_rapid_var(self.rapid_task, self.rapid_module, 'demoStart', 'TRUE')
            return self._ok(resp, f'started {scenario}')
        except Exception as e:
            return self._fail(resp, e)

    def _srv_abort(self, _req, resp):
        try:
            self.rws.set_rapid_var(self.rapid_task, self.rapid_module, 'demoAbort', 'TRUE')
            self.rws.post('/rw/rapid/execution/stop', {'stopmode': 'stop'})
            return self._ok(resp, 'aborted')
        except Exception as e:
            return self._fail(resp, e)

    def _srv_grip_open(self, _req, resp):
        try:
            self.rws.set_io('grip_open', '1')
            return self._ok(resp)
        except Exception as e:
            return self._fail(resp, e)

    def _srv_grip_close(self, _req, resp):
        try:
            self.rws.set_io('grip_close', '1')
            return self._ok(resp)
        except Exception as e:
            return self._fail(resp, e)

    def _srv_motors_on(self, _req, resp):
        try:
            self.rws.motors(True)
            return self._ok(resp)
        except Exception as e:
            return self._fail(resp, e)

    def _srv_motors_off(self, _req, resp):
        try:
            self.rws.motors(False)
            return self._ok(resp)
        except Exception as e:
            return self._fail(resp, e)


def main() -> None:
    rclpy.init()
    node = MetaMoveBridge()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
