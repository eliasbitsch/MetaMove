"""Smoke test — exercises every MetaMoveTools entry point against a live
rosbridge + metamove_bridge pair.

Expected environment: bridge container running, rosbridge on ws://localhost:9090.
GoFa does NOT need to be reachable — services will fail fast with "connection
refused" from the bridge's RWS poll, which is fine; we're testing the ROS
plumbing, not the robot.
"""
from __future__ import annotations

import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from metamove_tools import MetaMoveTools, DEMO_SCENARIOS  # noqa: E402


def main() -> int:
    print("[test] connecting to ws://localhost:9090 ...")
    with MetaMoveTools(host='localhost', port=9090) as t:
        print(f"[test] connected: {t.is_connected()}")

        # Give subscriptions 2s to start receiving robot_state pushes
        time.sleep(2.0)
        snap = t.get_state()
        print(f"[test] last state ts={snap.ts:.1f} ctrl_keys={list(snap.ctrl)[:5]}")

        checks: list[tuple[str, tuple[bool, str]]] = []
        print("[test] calling /metamove/abort ...")
        checks.append(('abort', t.abort()))

        print("[test] calling /metamove/grip_open ...")
        checks.append(('grip_open', t.grip_open()))

        print("[test] calling /metamove/grip_close ...")
        checks.append(('grip_close', t.grip_close()))

        print("[test] calling /metamove/motors_off ...")
        checks.append(('motors_off', t.motors_off()))

        print("[test] calling run_demo('chess') ...")
        checks.append(('run_demo', t.run_demo('chess')))

        print("[test] calling run_demo('bogus') — should be rejected client-side ...")
        checks.append(('run_demo_bogus', t.run_demo('bogus')))

        print()
        print(f"{'tool':<18} {'ok':<6} {'message'}")
        print("-" * 70)
        for name, (ok, msg) in checks:
            print(f"{name:<18} {str(ok):<6} {msg[:60]}")

        # We expect the ROS call itself to succeed (rosbridge plumbing works).
        # The RWS-side calls will FAIL with ConnectionRefused since the GoFa is
        # offline, but that's an authentic failure mode — we're just verifying
        # the service contract carries it back correctly.
        rosbridge_ok = all(
            'timeout' not in msg and 'rosbridge error' not in msg
            for name, (_, msg) in checks
            if name != 'run_demo_bogus'
        )
        bogus_rejected = checks[-1][1][0] is False and 'unknown scenario' in checks[-1][1][1]

        print()
        print(f"rosbridge plumbing: {'OK' if rosbridge_ok else 'FAIL'}")
        print(f"bogus scenario rejected client-side: {'OK' if bogus_rejected else 'FAIL'}")

        return 0 if (rosbridge_ok and bogus_rejected) else 1


if __name__ == "__main__":
    sys.exit(main())
