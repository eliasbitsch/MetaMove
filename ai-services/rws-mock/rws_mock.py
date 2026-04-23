"""
Minimal RWS mock for MetaMove dev without a real OmniCore controller.

Implements the handful of endpoints Unity + metamove_bridge actually use:
  * GET  /rw/panel/ctrl-state
  * POST /rw/panel/ctrl-state            (ctrl-state=motoron|motoroff)
  * GET  /rw/rapid/execution
  * POST /rw/rapid/execution/start
  * POST /rw/rapid/execution/stop
  * GET  /rw/iosystem/signals/{name}
  * POST /rw/iosystem/signals/{name}/set-value  (lvalue=N)
  * GET  /rw/iosystem/signals               (list signals)
  * GET  /rw/elog/0                         (last events — in-memory FIFO)
  * WS   /subscription                      (stub for future)

Auth: Basic (same as real RWS). Accept: application/hal+json;v=2.0.
Bind: http://0.0.0.0:8081 by default (non-TLS — don't want to faff with certs).

Not a physics sim. Signal writes take effect in the internal state only —
a connected Unity doesn't see robot motion via this mock (use the VC + EGM
mock for motion).

Usage:
    pip install fastapi uvicorn
    python rws_mock.py
    # optionally: HOST=0.0.0.0 PORT=8081 python rws_mock.py

Then in .claude.json MCP env or metamove_bridge config:
    ABB_RWS_URL=http://localhost:8081
"""
from __future__ import annotations

import base64
import json
import os
import threading
import time
from collections import deque
from dataclasses import dataclass, field
from typing import Any

from fastapi import FastAPI, Form, HTTPException, Request, Response
from fastapi.responses import JSONResponse


HOST = os.environ.get("HOST", "0.0.0.0")
PORT = int(os.environ.get("PORT", "8081"))
USER = os.environ.get("RWS_MOCK_USER", "Default User")
PASSWORD = os.environ.get("RWS_MOCK_PASS", "robotics")


# ---------------------------------------------------------------- state
@dataclass
class MockState:
    motors_on: bool = False
    exec_running: bool = False
    exec_cycle: str = "forever"
    op_mode: str = "AUTO"
    signals: dict[str, dict[str, Any]] = field(default_factory=dict)
    events: deque = field(default_factory=lambda: deque(maxlen=200))
    lock: threading.Lock = field(default_factory=threading.Lock)

    def log(self, severity: int, title: str, desc: str = "") -> None:
        self.events.appendleft({
            "id": str(int(time.time() * 1000)),
            "msgtype": str(severity),
            "tstamp": time.strftime("%Y-%m-%d T %H:%M:%S"),
            "title": title,
            "desc": desc,
        })

    def ensure_signal(self, name: str, sig_type: str = "DO") -> dict[str, Any]:
        if name not in self.signals:
            self.signals[name] = {
                "name": name,
                "type": sig_type,
                "lvalue": "0",
                "pvalue": "0",
                "lstate": "not simulated",
                "phstate": "valid",
                "write-access": "Rapid|LocalManual|LocalAuto|RemoteAuto",
                "devicenm": "MockDevice",
            }
        return self.signals[name]


STATE = MockState()

# Pre-populate some of the signals Unity / metamove_bridge may hit
for _name, _typ in [
    ("mm_demo_start", "DI"),
    ("mm_demo_abort", "DI"),
    ("mm_gripper_close", "DO"),
    ("mm_gripper_open", "DO"),
    ("mm_motion_running", "DO"),
    ("ox_multi_vac_greifer_schliessen", "DO"),   # match real robot naming
    ("ox_multi_vac_greifer_oeffnen", "DO"),
    ("ox_kupplung_schliessen", "DO"),
    ("ox_kupplung_oeffnen", "DO"),
]:
    STATE.ensure_signal(_name, _typ)


# ---------------------------------------------------------------- auth
def check_auth(request: Request) -> None:
    """Minimal HTTP Basic auth check. Only warn on mismatch — don't block
    (this is a dev mock, not a security boundary)."""
    auth = request.headers.get("authorization", "")
    if not auth.startswith("Basic "):
        return
    try:
        creds = base64.b64decode(auth[6:]).decode("utf-8")
        user, _, pwd = creds.partition(":")
        if user != USER or pwd != PASSWORD:
            # Log but don't 401 — the mock is permissive
            STATE.log(2, "auth", f"credentials mismatch for '{user}'")
    except Exception:  # noqa: BLE001
        pass


# ---------------------------------------------------------------- hal helpers
def hal_ok(state_entries: list[dict[str, Any]], self_href: str = "") -> Response:
    body = {
        "_links": {
            "base": {"href": f"http://{HOST}:{PORT}/rw/"},
            "self": {"href": self_href},
        },
        "status": {"code": 294912},
        "state": state_entries,
    }
    return JSONResponse(
        body,
        media_type="application/hal+json;v=2.0",
        headers={"Server": "RWS-Mock/0.1"},
    )


# ---------------------------------------------------------------- app
app = FastAPI(title="MetaMove RWS-Mock", version="0.1")


@app.middleware("http")
async def auth_mw(request: Request, call_next):
    check_auth(request)
    return await call_next(request)


@app.get("/rw/system")
async def system() -> Response:
    return hal_ok([{
        "_type": "sys-system",
        "_title": "system",
        "name": "RWS-Mock",
        "rwversion": "7.20.0-mock",
        "sysid": "{00000000-0000-0000-0000-000000000000}",
    }])


# ----------------------------------------------------------------- panel
@app.get("/rw/panel/ctrl-state")
async def ctrl_state_get() -> Response:
    return hal_ok([{
        "_type": "pnl-ctrlstate",
        "_title": "ctrl-state",
        "ctrlstate": "motoron" if STATE.motors_on else "motoroff",
    }])


@app.post("/rw/panel/ctrl-state")
async def ctrl_state_post(ctrl_state: str = Form(alias="ctrl-state")) -> Response:
    with STATE.lock:
        if ctrl_state == "motoron":
            STATE.motors_on = True
        elif ctrl_state == "motoroff":
            STATE.motors_on = False
        else:
            raise HTTPException(400, f"unknown ctrl-state: {ctrl_state}")
        STATE.log(1, "motors", ctrl_state)
    return ctrl_state_get


@app.get("/rw/panel/opmode")
async def opmode() -> Response:
    return hal_ok([{"_type": "pnl-opmode", "_title": "opmode", "opmode": STATE.op_mode}])


# ------------------------------------------------------------------ rapid
@app.get("/rw/rapid/execution")
async def rapid_execution_get() -> Response:
    return hal_ok([{
        "_type": "rap-execution",
        "_title": "execution",
        "ctrlexecstate": "running" if STATE.exec_running else "stopped",
        "cycle": STATE.exec_cycle,
    }])


@app.post("/rw/rapid/execution/start")
async def rapid_start() -> Response:
    with STATE.lock:
        STATE.exec_running = True
        STATE.log(1, "rapid", "execution started")
    return hal_ok([{"_type": "rap-execution", "ctrlexecstate": "running"}])


@app.post("/rw/rapid/execution/stop")
async def rapid_stop() -> Response:
    with STATE.lock:
        STATE.exec_running = False
        STATE.log(1, "rapid", "execution stopped")
    return hal_ok([{"_type": "rap-execution", "ctrlexecstate": "stopped"}])


# ------------------------------------------------------------ iosystem
@app.get("/rw/iosystem/signals")
async def iosignals() -> Response:
    with STATE.lock:
        entries = list(STATE.signals.values())
    return JSONResponse({
        "_links": {"base": {"href": f"http://{HOST}:{PORT}/rw/iosystem/"}},
        "_embedded": {"resources": [
            {"_type": "ios-signal-li", "name": s["name"], "type": s["type"],
             "lvalue": s["lvalue"], "lstate": s["lstate"]}
            for s in entries
        ]},
    }, media_type="application/hal+json;v=2.0")


@app.get("/rw/iosystem/signals/{name}")
async def iosignal_get(name: str) -> Response:
    with STATE.lock:
        s = STATE.ensure_signal(name)
        # copy so we don't leak the ref
        payload = dict(s)
    return JSONResponse({
        "_links": {"base": {"href": f"http://{HOST}:{PORT}/rw/iosystem/"}},
        "_embedded": {"resources": [
            {"_type": "ios-signal-li", **payload}
        ]},
    }, media_type="application/hal+json;v=2.0")


@app.post("/rw/iosystem/signals/{name}/set-value")
async def iosignal_set(name: str, lvalue: str = Form(...)) -> Response:
    with STATE.lock:
        s = STATE.ensure_signal(name)
        s["lvalue"] = lvalue
        s["pvalue"] = lvalue
        STATE.log(1, "io", f"{name}={lvalue}")
    return hal_ok([{"_type": "ios-signal-set", "_title": name, "lvalue": lvalue}])


# ------------------------------------------------------------------ elog
@app.get("/rw/elog/0")
async def elog(limit: int = 20) -> Response:
    with STATE.lock:
        events = list(STATE.events)[:limit]
    return JSONResponse({
        "_links": {"base": {"href": f"http://{HOST}:{PORT}/rw/elog/"}},
        "_embedded": {"resources": [
            {"_type": "elog-message-li", **e} for e in events
        ]},
    }, media_type="application/hal+json;v=2.0")


# -------------------------------------------------------- convenience
@app.get("/mock/state")
async def mock_state() -> dict:
    """Debug helper — dump the mock's internal state."""
    with STATE.lock:
        return {
            "motors_on": STATE.motors_on,
            "exec_running": STATE.exec_running,
            "op_mode": STATE.op_mode,
            "signals": STATE.signals,
            "events": list(STATE.events)[:20],
        }


@app.post("/mock/reset")
async def mock_reset() -> dict:
    """Debug helper — reset state (for test isolation)."""
    global STATE
    STATE = MockState()
    return {"ok": True}


if __name__ == "__main__":
    import uvicorn
    print(f"[rws-mock] starting on http://{HOST}:{PORT}")
    print(f"[rws-mock] auth: Basic {USER}:{PASSWORD}")
    print(f"[rws-mock] state dump: GET /mock/state")
    uvicorn.run(app, host=HOST, port=PORT, log_level="info")
