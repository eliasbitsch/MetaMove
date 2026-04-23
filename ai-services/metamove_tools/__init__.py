"""MetaMove tools — Python API for calling the ROS2 bridge from Jarvis.

Jarvis stays native (FastAPI/WebSocket) — no rclpy dependency. All tool calls
go through rosbridge_websocket to the metamove_bridge ROS2 node. That gives:

  * rosbag recording of every tool call (for debugging + demos)
  * `ros2 topic echo /metamove/*` visibility into what Jarvis is doing
  * Claude / any CLI client can call the same services Jarvis uses

Usage:

    from metamove_tools import MetaMoveTools
    tools = MetaMoveTools()               # connects to ws://localhost:9090
    tools.run_demo("chess")
    tools.grip_close()
    tools.abort()
    state = tools.get_state()             # last robot_state snapshot
"""
from .client import MetaMoveTools, DEMO_SCENARIOS

__all__ = ["MetaMoveTools", "DEMO_SCENARIOS"]
