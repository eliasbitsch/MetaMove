"""roslibpy-based client that exposes metamove_bridge services as Python functions.

Why roslibpy and not rclpy:
  * Jarvis runs on the GPU host (native Python), not inside the ROS2 container.
  * rclpy would need a full ROS2 install on the host. roslibpy needs only a
    WebSocket to rosbridge_server, which runs in the container already.
  * The semantics are identical: service-call / topic-echo / param-set.
"""
from __future__ import annotations

import json
import logging
import threading
import time
from dataclasses import dataclass, field
from typing import Any

import roslibpy

log = logging.getLogger(__name__)


DEMO_SCENARIOS = ("chess", "stone_sort", "framing", "mug", "pins", "bigstone")


@dataclass
class RobotSnapshot:
    """Latest robot state pushed over /metamove/robot_state."""
    ts: float = 0.0
    ctrl: dict[str, Any] = field(default_factory=dict)
    exec: dict[str, Any] = field(default_factory=dict)
    raw: str = ""


class MetaMoveTools:
    """Thin Python facade over the metamove_bridge ROS2 services.

    Thread-safe: internal ros client runs its own thread. Service calls are
    blocking with a timeout; topic subscriptions update in-memory state.
    """

    def __init__(
        self,
        host: str = "localhost",
        port: int = 9090,
        connect_timeout: float = 5.0,
        default_timeout: float = 5.0,
    ):
        self._default_timeout = default_timeout
        self._snapshot = RobotSnapshot()
        self._events: list[dict[str, Any]] = []
        self._events_lock = threading.Lock()

        self._ros = roslibpy.Ros(host=host, port=port)
        self._ros.run()

        # Wait for connection
        t0 = time.monotonic()
        while not self._ros.is_connected:
            if time.monotonic() - t0 > connect_timeout:
                raise TimeoutError(f"rosbridge connect timeout ({host}:{port})")
            time.sleep(0.05)

        # Subscribe to state topics
        self._sub_state = roslibpy.Topic(self._ros, '/metamove/robot_state', 'std_msgs/String')
        self._sub_state.subscribe(self._on_state)
        self._sub_events = roslibpy.Topic(self._ros, '/metamove/event_log', 'std_msgs/String')
        self._sub_events.subscribe(self._on_event)

        log.info("MetaMoveTools connected to ws://%s:%d", host, port)

    # ------------------------------------------------------------- lifecycle
    def close(self) -> None:
        try:
            self._sub_state.unsubscribe()
            self._sub_events.unsubscribe()
        except Exception:  # noqa: BLE001
            pass
        self._ros.terminate()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()

    # -------------------------------------------------------------- helpers
    def _call_trigger(self, service_name: str, timeout: float | None = None) -> tuple[bool, str]:
        """Call a std_srvs/Trigger service and return (success, message)."""
        svc = roslibpy.Service(self._ros, service_name, 'std_srvs/Trigger')
        req = roslibpy.ServiceRequest()
        result = {'ok': False, 'msg': 'no response'}
        done = threading.Event()

        def _cb(response):
            result['ok'] = bool(response.get('success', False))
            result['msg'] = str(response.get('message', ''))
            done.set()

        def _err(error):
            result['ok'] = False
            result['msg'] = f'rosbridge error: {error}'
            done.set()

        svc.call(req, _cb, _err)
        if not done.wait(timeout or self._default_timeout):
            return False, f'timeout calling {service_name}'
        return result['ok'], result['msg']

    def _set_param(self, node: str, name: str, value: Any, timeout: float | None = None) -> bool:
        """ROS2 params are per-node services, not the ROS1 global param server.

        roslibpy.Param maps to the ROS1 model and does not work over ROS2
        rosbridge reliably. We call /<node>/set_parameters directly.
        """
        svc = roslibpy.Service(self._ros, f'/{node}/set_parameters', 'rcl_interfaces/SetParameters')
        # Encode ParameterValue based on Python type
        if isinstance(value, bool):
            pv = {'type': 1, 'bool_value': value}
        elif isinstance(value, int):
            pv = {'type': 2, 'integer_value': value}
        elif isinstance(value, float):
            pv = {'type': 3, 'double_value': value}
        elif isinstance(value, str):
            pv = {'type': 4, 'string_value': value}
        else:
            return False

        req = roslibpy.ServiceRequest({
            'parameters': [{'name': name, 'value': pv}]
        })
        result = {'ok': False}
        done = threading.Event()

        def _cb(response):
            results = response.get('results', [])
            result['ok'] = bool(results and results[0].get('successful', False))
            done.set()

        def _err(_e):
            done.set()

        svc.call(req, _cb, _err)
        if not done.wait(timeout or self._default_timeout):
            return False
        return result['ok']

    # ------------------------------------------------------------- callbacks
    def _on_state(self, msg: dict[str, Any]) -> None:
        try:
            data = json.loads(msg.get('data', '{}'))
            self._snapshot = RobotSnapshot(
                ts=data.get('ts', time.time()),
                ctrl=data.get('ctrl', {}),
                exec=data.get('exec', {}),
                raw=msg.get('data', ''),
            )
        except Exception as e:  # noqa: BLE001
            log.warning("robot_state parse failed: %s", e)

    def _on_event(self, msg: dict[str, Any]) -> None:
        with self._events_lock:
            self._events.append({'ts': time.time(), 'data': msg.get('data', '')})
            # keep last 100
            if len(self._events) > 100:
                self._events = self._events[-100:]

    # ------------------------------------------------------------------ API
    def run_demo(self, scenario: str) -> tuple[bool, str]:
        """Set the scenario param then trigger /metamove/run_demo."""
        if scenario not in DEMO_SCENARIOS:
            return False, f'unknown scenario "{scenario}". One of: {DEMO_SCENARIOS}'
        if not self._set_param('metamove_bridge', 'scenario', scenario):
            return False, 'failed to set scenario parameter'
        return self._call_trigger('/metamove/run_demo')

    def abort(self) -> tuple[bool, str]:
        return self._call_trigger('/metamove/abort')

    def grip_open(self) -> tuple[bool, str]:
        return self._call_trigger('/metamove/grip_open')

    def grip_close(self) -> tuple[bool, str]:
        return self._call_trigger('/metamove/grip_close')

    def motors_on(self) -> tuple[bool, str]:
        return self._call_trigger('/metamove/motors_on')

    def motors_off(self) -> tuple[bool, str]:
        return self._call_trigger('/metamove/motors_off')

    # ----------------------------------------------------------------- state
    def get_state(self) -> RobotSnapshot:
        return self._snapshot

    def recent_events(self, n: int = 10) -> list[dict[str, Any]]:
        with self._events_lock:
            return self._events[-n:]

    def is_connected(self) -> bool:
        return self._ros.is_connected


# ---------------------------------------------------------------------------
# Tool schema (for wiring into an LLM with function-calling / tool-use)
# ---------------------------------------------------------------------------
TOOL_SCHEMA = [
    {
        "name": "run_demo",
        "description": "Start one of the pre-programmed pick-and-place scenarios on the GoFa.",
        "parameters": {
            "type": "object",
            "properties": {
                "scenario": {
                    "type": "string",
                    "enum": list(DEMO_SCENARIOS),
                    "description": "Which demo to run.",
                },
            },
            "required": ["scenario"],
        },
    },
    {
        "name": "abort",
        "description": "Stop the robot immediately and abort any running demo.",
        "parameters": {"type": "object", "properties": {}},
    },
    {
        "name": "grip_open",
        "description": "Open the gripper.",
        "parameters": {"type": "object", "properties": {}},
    },
    {
        "name": "grip_close",
        "description": "Close the gripper.",
        "parameters": {"type": "object", "properties": {}},
    },
    {
        "name": "motors_on",
        "description": "Enable robot motors.",
        "parameters": {"type": "object", "properties": {}},
    },
    {
        "name": "motors_off",
        "description": "Disable robot motors.",
        "parameters": {"type": "object", "properties": {}},
    },
]
