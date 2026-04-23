"""
MetaMove RWS bridge node.

Not in the motion hot-path — Unity still talks EGM directly to the controller.
This node gives you:
  * CLI visibility (`ros2 topic echo /metamove/robot_state`)
  * Services to trigger demos / grip / abort from a terminal
  * Bag recording of the whole session

Topics published:
  /metamove/robot_state   std_msgs/String   JSON blob of latest RWS snapshot (~2 Hz)
  /metamove/demo_state    std_msgs/String   {id, state, step}
  /metamove/event_log     std_msgs/String   RWS elog entries as they arrive

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
import os
import threading
import time
from typing import Any

import rclpy
import requests
import urllib3
from rclpy.node import Node
from std_msgs.msg import String
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


class RwsClient:
    """Minimal RWS REST wrapper. Session-based digest auth."""

    def __init__(self, host: str, port: int, user: str, password: str):
        self.base = f'https://{host}:{port}'
        self.session = requests.Session()
        self.session.auth = requests.auth.HTTPDigestAuth(user, password)
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

        self.rws = RwsClient(
            self.get_parameter('rws_ip').value,
            self.get_parameter('rws_port').value,
            self.get_parameter('rws_user').value,
            self.get_parameter('rws_password').value,
        )
        self.rapid_task = self.get_parameter('rapid_task').value
        self.rapid_module = self.get_parameter('rapid_module').value

        # Publishers
        self.pub_state = self.create_publisher(String, '/metamove/robot_state', 10)
        self.pub_demo = self.create_publisher(String, '/metamove/demo_state', 10)
        self.pub_log = self.create_publisher(String, '/metamove/event_log', 20)

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
