"""Teach the MANUAL-mode safety box by jogging the GoFa to its corners.

  python tools/teach-box/teach_box.py point <n>     # record the current TCP as corner n
  python tools/teach-box/teach_box.py top           # record the current TCP height as the lid
  python tools/teach-box/teach_box.py list          # show recorded corners
  python tools/teach-box/teach_box.py box           # compute the box, print profile lines
  python tools/teach-box/teach_box.py box --write   # ...and write them into robot_profile.yaml
  python tools/teach-box/teach_box.py clear

Reads the TCP from the controller over RWS (tool0, wobj0, Base frame; one session per
call). On this cell that frame equals ROS base_link exactly - checked 2026-09-25
(controller 597.5/0.0/837.8 mm vs MoveIt FK 597.5/0.0/837.8 mm).

The box moveit_ik_relay enforces is axis-aligned in base_link. Jog the corners linearly
in the Base frame and they line up. If they don't, the box is the INNER one - per
axis the tightest of the low corners and the tightest of the high corners - so a
skewed teach makes the box smaller, never larger.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import ssl
import sys
import urllib.request
from http.cookiejar import CookieJar
from pathlib import Path

HERE = Path(__file__).resolve().parent
POINTS = HERE / "box_points.json"
PROFILE = HERE.parents[1] / "ros2" / "docker" / "metamove_bridge" / "config" / "robot_profile.yaml"
ROBOT = "https://192.168.125.1"


def rws_get(opener, path: str) -> dict:
    req = urllib.request.Request(ROBOT + path, headers={"Accept": "application/hal+json;v=2.0"})
    with opener.open(req, timeout=8) as r:
        return json.loads(r.read().decode())


def read_tcp() -> dict:
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    pw = urllib.request.HTTPPasswordMgrWithDefaultRealm()
    pw.add_password(None, ROBOT, "Default User", "robotics")
    opener = urllib.request.build_opener(
        urllib.request.HTTPSHandler(context=ctx),
        urllib.request.HTTPDigestAuthHandler(pw),
        urllib.request.HTTPBasicAuthHandler(pw),
        urllib.request.HTTPCookieProcessor(CookieJar()),   # one session for all reads
    )
    rt = rws_get(opener, "/rw/motionsystem/mechunits/ROB_1/robtarget?tool=tool0&wobj=wobj0&coordinate=Base")["state"][0]
    jt = rws_get(opener, "/rw/motionsystem/mechunits/ROB_1/jointtarget")["state"][0]
    return {
        "xyz_m": [round(float(rt[k]) / 1000.0, 4) for k in ("x", "y", "z")],
        "joints_deg": [round(float(jt[f"rax_{i}"]), 2) for i in range(1, 7)],
        "time": dt.datetime.now().isoformat(timespec="seconds"),
    }


def load() -> dict:
    return json.loads(POINTS.read_text()) if POINTS.exists() else {}


def inner_box(pts: list[list[float]]) -> tuple[list[float], list[float], list[float]]:
    """Per axis: split the corners into a low and a high half, take the tightest of
    each. Returns (min, max, skew) - skew = spread inside each half, i.e. how far the
    teach is from a clean axis-aligned cuboid."""
    lo, hi, skew = [], [], []
    for ax in range(3):
        vals = sorted(p[ax] for p in pts)
        half = len(vals) // 2
        low, high = vals[:half], vals[half:]
        lo.append(max(low))
        hi.append(min(high))
        skew.append(max(max(low) - min(low), max(high) - min(high)))
    return lo, hi, skew


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["point", "top", "list", "box", "clear"])
    ap.add_argument("n", nargs="?", type=int)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    pts = load()

    if a.cmd == "clear":
        POINTS.unlink(missing_ok=True)
        print("cleared")
        return 0
    if a.cmd == "point":
        if a.n is None:
            print("usage: point <n>")
            return 1
        p = read_tcp()
        pts[str(a.n)] = p
        POINTS.write_text(json.dumps(pts, indent=2))
        x, y, z = p["xyz_m"]
        print(f"corner {a.n}: x={x:.3f} y={y:.3f} z={z:.3f} m   joints {p['joints_deg']}   ({len(pts)}/8)")
        return 0
    if a.cmd == "top":
        p = read_tcp()
        pts["top"] = p
        POINTS.write_text(json.dumps(pts, indent=2))
        print(f"lid height: z={p['xyz_m'][2]:.3f} m   (tcp {p['xyz_m']})")
        return 0
    corners = {k: v for k, v in pts.items() if k != "top"}
    lid = pts.get("top")
    complete = len(corners) >= 8 or (len(corners) >= 4 and lid is not None)
    if a.cmd == "list" or not complete:
        for k in sorted(corners, key=int):
            x, y, z = corners[k]["xyz_m"]
            print(f"corner {k}: x={x:.3f} y={y:.3f} z={z:.3f} m")
        if lid is not None:
            print(f"lid: z={lid['xyz_m'][2]:.3f} m")
        if a.cmd == "box" and not complete:
            print(f"need 8 corners, or 4 floor corners + 'top' ({len(corners)} corners, lid {'set' if lid else 'missing'})")
            return 1
        return 0

    if lid is not None and len(corners) < 8:
        # Floor (4 corners) + one lid height for the whole box: x/y inner box of the
        # floor, bottom = the HIGHEST floor corner, top = the lid.
        floor = [p["xyz_m"] for p in corners.values()]
        lo, hi, skew = inner_box([[x, y, 0.0] for x, y, _ in floor] + [[x, y, 1.0] for x, y, _ in floor])
        lo[2], hi[2] = max(z for _, _, z in floor), lid["xyz_m"][2]
        skew[2] = max(z for _, _, z in floor) - min(z for _, _, z in floor)
    else:
        lo, hi, skew = inner_box([p["xyz_m"] for p in corners.values()])
    size = [h - l for l, h in zip(lo, hi)]
    print(f"box min {[round(v, 3) for v in lo]}  max {[round(v, 3) for v in hi]}  "
          f"size {[round(v, 3) for v in size]} m")
    print(f"skew per axis {[round(v * 1000) for v in skew]} mm "
          f"({'clean cuboid' if max(skew) < 0.02 else 'corners not axis-aligned - inner box used'})")
    if min(size) <= 0.05:
        print("box too thin (<5 cm on an axis) - check the corners")
        return 1
    if a.write:
        text = PROFILE.read_text(encoding="utf-8")
        stamp = dt.date.today().isoformat()
        new = (f"    safety_box_min: [{lo[0]:.3f}, {lo[1]:.3f}, {lo[2]:.3f}]   # taught {stamp}, tools/teach-box\n"
               f"    safety_box_max: [{hi[0]:.3f}, {hi[1]:.3f}, {hi[2]:.3f}]")
        text, n = re.subn(r"    safety_box_min: \[[^\]]*\][^\n]*\n    safety_box_max: \[[^\]]*\]", new, text)
        if n != 1:
            print("could not find safety_box_min/max in robot_profile.yaml")
            return 1
        PROFILE.write_text(text, encoding="utf-8")
        print(f"written to {PROFILE}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
