"""A stand-in for the Quest app on the ROS-TCP link, for the end-to-end tests.

Speaks the ros_tcp_endpoint wire protocol exactly like Unity's ROSConnection:
  frame = <u32 len><destination utf-8><u32 len><payload>
  payload = CDR-serialised ROS 2 message, or for "__publish"/"__subscribe" a JSON
  object terminated by a NUL byte.
So the endpoint, its topic registration and the serialisation are all exercised; only
the headset input itself is scripted.

  q = FakeQuest(); q.publisher('/quest/min_distance', Float32)
  q.send('/quest/min_distance', Float32(data=3.0))
  q.subscribe('/robot/speed_factor', Float32); q.last('/robot/speed_factor')
  q.stream('/quest/min_distance', lambda: Float32(data=q.distance), hz=20)   # like the app
"""
from __future__ import annotations

import json
import socket
import struct
import threading
import time
from typing import Callable

from rclpy.serialization import deserialize_message, serialize_message


def _type_name(cls) -> str:
    # std_msgs.msg._float32.Float32 -> std_msgs/Float32
    return f"{cls.__module__.split('.')[0]}/{cls.__name__}"


class FakeQuest:
    def __init__(self, host: str = "127.0.0.1", port: int = 10000, timeout: float = 10.0) -> None:
        deadline = time.monotonic() + timeout
        while True:
            try:
                self.sock = socket.create_connection((host, port), timeout=2.0)
                break
            except OSError:
                if time.monotonic() > deadline:
                    raise
                time.sleep(0.5)
        self.sock.settimeout(None)
        self._send_lock = threading.Lock()
        self._subs: dict[str, type] = {}
        self._last: dict[str, tuple[float, object]] = {}
        self._history: dict[str, list[tuple[float, object]]] = {}
        self._streams: list[threading.Event] = []
        self._alive = True
        threading.Thread(target=self._reader, daemon=True).start()

    # --- wire ---------------------------------------------------------------------
    def _frame(self, dest: str, payload: bytes) -> None:
        d = dest.encode()
        with self._send_lock:
            self.sock.sendall(struct.pack("<I", len(d)) + d + struct.pack("<I", len(payload)) + payload)

    def _syscommand(self, cmd: str, params: dict) -> None:
        self._frame(cmd, json.dumps(params).encode() + b"\0")

    def _recv(self, n: int) -> bytes:
        buf = b""
        while len(buf) < n:
            chunk = self.sock.recv(n - len(buf))
            if not chunk:
                raise ConnectionError("endpoint closed the connection")
            buf += chunk
        return buf

    def _reader(self) -> None:
        try:
            while self._alive:
                dest = self._recv(struct.unpack("<I", self._recv(4))[0]).decode().rstrip("\0")
                data = self._recv(struct.unpack("<I", self._recv(4))[0])
                cls = self._subs.get(dest)
                if cls is None:
                    continue          # syscommands from the endpoint (__handshake, errors, ...)
                msg = deserialize_message(data, cls)
                now = time.monotonic()
                self._last[dest] = (now, msg)
                self._history.setdefault(dest, []).append((now, msg))
        except (ConnectionError, OSError):
            self._alive = False

    # --- API ----------------------------------------------------------------------
    def publisher(self, topic: str, cls) -> None:
        self._syscommand("__publish", {"topic": topic, "message_name": _type_name(cls)})

    def subscribe(self, topic: str, cls) -> None:
        self._subs[topic] = cls
        self._syscommand("__subscribe", {"topic": topic, "message_name": _type_name(cls)})

    def send(self, topic: str, msg) -> None:
        self._frame(topic, serialize_message(msg))

    def stream(self, topic: str, make: Callable[[], object | None], hz: float) -> threading.Event:
        """Publish make() at hz until the returned event is set; make() -> None skips a tick."""
        stop = threading.Event()

        def run() -> None:
            while not stop.is_set() and self._alive:
                m = make()
                if m is not None:
                    self.send(topic, m)
                stop.wait(1.0 / hz)
        threading.Thread(target=run, daemon=True).start()
        self._streams.append(stop)
        return stop

    def last(self, topic: str, max_age: float | None = None):
        t_msg = self._last.get(topic)
        if t_msg is None or (max_age is not None and time.monotonic() - t_msg[0] > max_age):
            return None
        return t_msg[1]

    def history(self, topic: str, since: float = 0.0) -> list[tuple[float, object]]:
        return [(t, m) for t, m in self._history.get(topic, []) if t >= since]

    def close(self) -> None:
        for s in self._streams:
            s.set()
        self._alive = False
        try:
            self.sock.close()
        except OSError:
            pass
