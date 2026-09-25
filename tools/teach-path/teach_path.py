"""Teach the AUTO path: jog the GoFa to each waypoint, record it here.

  python tools/teach-path/teach_path.py add [name]   # record the current pose as the next waypoint
  python tools/teach-path/teach_path.py list
  python tools/teach-path/teach_path.py undo         # drop the last one
  python tools/teach-path/teach_path.py save         # replace dpp_waypoints.yaml (old one kept as .bak-<date>)
  python tools/teach-path/teach_path.py clear

Joint angles come from the controller over RWS (one session per call), in the same
convention as the EGM feedback / URDF (verified 2026-09-25: RWS rax_5 29.9999 deg ==
EGM feedback 29.9999 deg). Recorded into a draft (path_draft.yaml) until 'save', so the
running playback keeps its old path while you teach. After 'save', restart the playback
node (the launch respawns it: tools/metamove_up.ps1 or pkill dpp_playback).
"""
from __future__ import annotations

import argparse
import datetime as dt
import math
import shutil
import sys
from pathlib import Path

import yaml

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
sys.path.insert(0, str(REPO / "tools" / "teach-box"))
sys.path.insert(0, str(REPO / "ros2" / "docker" / "metamove_bridge" / "metamove_bridge"))
from teach_box import read_tcp  # noqa: E402  (RWS read: TCP + joints, one session)
from tcp_fk import tcp_position  # noqa: E402

DRAFT = HERE / "path_draft.yaml"
TARGET = REPO / "ros2" / "docker" / "metamove_bridge" / "dpp_waypoints.yaml"
BOX_PROFILE = REPO / "ros2" / "docker" / "metamove_bridge" / "config" / "robot_profile.yaml"


def load() -> list[dict]:
    if not DRAFT.exists():
        return []
    return (yaml.safe_load(DRAFT.read_text()) or {}).get("waypoints", [])


def dump(wps: list[dict], path: Path) -> None:
    path.write_text(yaml.safe_dump({"waypoints": wps}, sort_keys=False))


def describe(i: int, wp: dict) -> str:
    q = wp["joints"]
    p = tcp_position(q)
    deg = " ".join(f"{math.degrees(v):+7.1f}" for v in q)
    warn = ""
    if abs(math.degrees(q[4])) < 10:
        warn = "   ! J5 near 0 deg - wrist singularity, prefer |J5| > 10"
    return f"{i + 1:2d} {wp['name']:8s} tcp=({p[0]:+.3f},{p[1]:+.3f},{p[2]:+.3f}) m  joints [{deg}]{warn}"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["add", "list", "undo", "save", "clear"])
    ap.add_argument("name", nargs="?")
    a = ap.parse_args()
    wps = load()

    if a.cmd == "clear":
        DRAFT.unlink(missing_ok=True)
        print("draft cleared")
        return 0
    if a.cmd == "add":
        r = read_tcp()
        q = [round(math.radians(v), 6) for v in r["joints_deg"]]
        wp = {"name": a.name or f"wp_{len(wps) + 1:02d}", "joints": q,
              "recorded_utc": dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}
        # Cross-check: our FK of the controller's joints vs the controller's own TCP.
        fk = tcp_position(q)
        err = max(abs(fk[i] - r["xyz_m"][i]) for i in range(3))
        wps.append(wp)
        dump(wps, DRAFT)
        print(describe(len(wps) - 1, wp) + (f"   (FK vs controller {err * 1000:.1f} mm)"
                                             if err > 0.002 else ""))
        return 0
    if a.cmd == "undo":
        if wps:
            print("dropped", wps.pop()["name"])
            dump(wps, DRAFT)
        return 0
    if a.cmd == "list":
        for i, wp in enumerate(wps):
            print(describe(i, wp))
        print(f"{len(wps)} waypoints in the draft")
        return 0
    if a.cmd == "save":
        if len(wps) < 2:
            print("need at least 2 waypoints")
            return 1
        if TARGET.exists():
            bak = TARGET.with_suffix(f".yaml.bak-{dt.datetime.now():%Y%m%d-%H%M%S}")
            shutil.copy2(TARGET, bak)
            print(f"old path kept as {bak.name}")
        dump(wps, TARGET)
        print(f"saved {len(wps)} waypoints to {TARGET}")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
