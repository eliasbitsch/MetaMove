# metamove_tools

Python API that lets Jarvis (or any Python process on the host) call the
`metamove_bridge` ROS2 services over WebSocket — **without** installing rclpy
natively.

## Why this exists

Jarvis stays FastAPI/WebSocket native (best fit for streaming audio + SSE
tokens). But we want every tool call the LLM makes to be visible in
`ros2 topic echo` and recordable in `ros2 bag`. This module bridges the gap:
Jarvis does its normal Python function call, which goes Python →
rosbridge_websocket → ROS2 service, exactly like any `ros2 service call`
from the terminal.

## Setup

```bash
pip install -r requirements.txt
```

The ROS2 bridge container must be running:

```bash
docker compose -f ros2/docker/docker-compose.yml up -d bridge
```

That launch brings up both `metamove_bridge` and `rosbridge_websocket` on
port 9090.

## Usage

```python
from metamove_tools import MetaMoveTools

with MetaMoveTools(host="localhost", port=9090) as t:
    # High-level tool calls — each routes through rosbridge → ROS service
    t.run_demo("chess")      # sets scenario param, triggers /metamove/run_demo
    t.grip_close()
    t.motors_on()
    t.abort()

    # Read latest state (pushed via subscription)
    snap = t.get_state()
    print(snap.ctrl, snap.exec)

    # Recent event log entries
    for ev in t.recent_events(5):
        print(ev)
```

## Tool schema for LLM function-calling

`metamove_tools.TOOL_SCHEMA` gives you an OpenAI-style JSON schema for all 6
tools. Pass it to the LLM via whatever tool-use mechanism it supports. When
the LLM returns a tool call, dispatch to the matching `MetaMoveTools` method.

## Smoke test

With a running bridge:

```bash
python metamove_tools/smoke_test.py
```

Verifies the full chain (WS connect, topic subscription, service calls, param
set). Does NOT require a physical robot — failures at the RWS layer are
expected and verified to propagate correctly through rosbridge.

## Wire-up

```
User voice ──► Jarvis (FastAPI)
                  │
                  │ LLM decides tool: {"name": "run_demo", "args": {"scenario": "chess"}}
                  ▼
              MetaMoveTools.run_demo("chess")
                  │
                  │ WebSocket JSON-RPC
                  ▼
              rosbridge_websocket  (in ROS2 container)
                  │
                  ▼
              /metamove/run_demo service
                  │
                  ▼
              metamove_bridge node
                  │
                  │ HTTPS RWS call
                  ▼
              GoFa OmniCore
```

Every hop observable via `ros2 topic echo`, recordable via `ros2 bag`.
