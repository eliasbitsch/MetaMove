"""MetaMove robot console - start/stop the taught path and set the speed from the PC.

A small Tk window next to the pendant. Talks to ROS through rosbridge (the same
ws://127.0.0.1:9090 the EGM bridge uses):

  Start / Stop / Home   -> /dpp_playback/start (via HOME) | pause | home   (Trigger)
  Max speed -/+ 10 %    -> /quest/max_speed request, shown from /robot/max_speed
  Baseline profile      -> fixed "100 %": relay time_scale 1.5, MoveIt vel 0.30 / acc 0.10,
                           TCP capped at 0.38 m/s by the relay
  Mode                  -> distance_speed_scaler.distance_override

Mode "Headset" (default): the headset distance scales the speed and taking the
headset off stops the robot. Mode "PC test": distance ignored, the robot runs at
max speed - only you and the e-stop stop it. Closing the window stops the path
and switches back to "Headset".

  python tools/robot-console/robot_console.py [--host 127.0.0.1] [--port 9090]
"""
from __future__ import annotations

import argparse
import json
import math
import sys
import threading
import time
from pathlib import Path
import tkinter as tk
from tkinter import ttk

import roslibpy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from rws_client import Rws  # noqa: E402  (one reused RWS session)

SCALER = "/distance_speed_scaler"
RELAY = "/joint_trajectory_controller"
PLAYBACK = "/dpp_playback"
STEP = 0.1
# The baseline motion profile that defines "100 %": (relay time_scale, MoveIt velocity,
# acceleration). Real GoFa 2026-09-25: without a TCP cap x1.5 tripped the cell's SafeMove
# tool speed supervision (Gesamtzone_TSP); with the relay capping tool0 at 0.38 m/s it
# runs clean and a lap is ~20 % shorter than at x2. Below 100 % is max_speed and the
# headset distance scaling down from here.
BASELINE = (1.5, 0.30, 0.10)


class Console:
    def __init__(self, root: tk.Tk, ros: roslibpy.Ros) -> None:
        self.root, self.ros = root, ros
        self.state = {"max": None, "speed": None, "joints": None, "override": False, "status": None,
                      "ctrl": None}
        # Controller over RWS: one session, polled every 2 s in the background.
        self.rws = Rws()
        threading.Thread(target=self._poll_controller, daemon=True).start()
        self.lock = threading.Lock()

        self.max_req = roslibpy.Topic(ros, "/quest/max_speed", "std_msgs/Float32")
        self.max_req.advertise()
        roslibpy.Topic(ros, "/robot/max_speed", "std_msgs/Float32").subscribe(
            lambda m: self._set("max", m["data"]))
        roslibpy.Topic(ros, "/robot/speed_factor", "std_msgs/Float32").subscribe(
            lambda m: self._set("speed", m["data"]))
        roslibpy.Topic(ros, "/robot/status", "std_msgs/String").subscribe(
            lambda m: self._set("status", json.loads(m["data"])))
        roslibpy.Topic(ros, "/joint_states", "sensor_msgs/JointState", throttle_rate=100).subscribe(
            lambda m: self._set("joints", m["position"]))

        root.title("MetaMove Robot Console")
        root.configure(padx=16, pady=12)
        big = ("Segoe UI", 16, "bold")
        mid = ("Segoe UI", 12)

        self.conn = ttk.Label(root, text="", font=mid)
        self.conn.grid(row=12, column=0, columnspan=3, sticky="w")
        # Why the robot is (not) moving - from robot_status.
        self.status_lbl = tk.Label(root, text="", font=("Segoe UI", 15, "bold"), anchor="w", justify="left")
        self.status_lbl.grid(row=0, column=0, columnspan=3, sticky="ew", pady=(0, 4))

        path = ttk.LabelFrame(root, text=" Taught path ", padding=10)
        path.grid(row=1, column=0, columnspan=3, sticky="ew", pady=8)
        tk.Button(path, text="▶  Start", font=big, bg="#2e7d32", fg="white", width=9,
                  command=lambda: self._trigger("start")).grid(row=0, column=0, padx=4)
        tk.Button(path, text="■  Stop", font=big, bg="#c62828", fg="white", width=9,
                  command=lambda: self._trigger("pause")).grid(row=0, column=1, padx=4)
        tk.Button(path, text="⌂  Home", font=big, width=9,
                  command=lambda: self._trigger("home")).grid(row=0, column=2, padx=4)
        self.msg = ttk.Label(path, text="", font=mid)
        self.msg.grid(row=1, column=0, columnspan=3, sticky="w", pady=(8, 0))

        spd = ttk.LabelFrame(root, text=" Max speed (what 100 % means) ", padding=10)
        spd.grid(row=2, column=0, columnspan=3, sticky="ew", pady=8)
        tk.Button(spd, text="− 10 %", font=big, width=7, command=lambda: self._step(-1)).grid(row=0, column=0)
        self.max_lbl = ttk.Label(spd, text="--", font=("Segoe UI", 28, "bold"), width=6, anchor="center")
        self.max_lbl.grid(row=0, column=1, padx=12)
        tk.Button(spd, text="+ 10 %", font=big, width=7, command=lambda: self._step(+1)).grid(row=0, column=2)
        self.bar = ttk.Progressbar(spd, length=360, maximum=100)
        self.bar.grid(row=1, column=0, columnspan=3, pady=(10, 0))
        self.speed_lbl = ttk.Label(spd, text="", font=mid)
        self.speed_lbl.grid(row=2, column=0, columnspan=3)

        ts, v, a = BASELINE
        ttk.Label(root, text=f"100 % = stretch x{ts:g} | MoveIt velocity {v:.0%} | accel {a:.0%} "
                             f"+ TCP cap 380 mm/s (cell tool speed supervision)",
                  font=("Segoe UI", 10)).grid(row=3, column=0, columnspan=3, sticky="w")

        mode = ttk.LabelFrame(root, text=" Mode ", padding=10)
        mode.grid(row=4, column=0, columnspan=3, sticky="ew", pady=8)
        self.mode_var = tk.StringVar(value="headset")
        ttk.Radiobutton(mode, text="Headset: distance scales speed, headset off = stop",
                        variable=self.mode_var, value="headset", command=self._mode).grid(row=0, column=0, sticky="w")
        ttk.Radiobutton(mode, text="PC test: distance ignored, runs at max speed",
                        variable=self.mode_var, value="pc", command=self._mode).grid(row=1, column=0, sticky="w")
        self.warn = tk.Label(mode, text="", font=mid, fg="#c62828")
        self.warn.grid(row=2, column=0, sticky="w")

        ctl = ttk.LabelFrame(root, text=" Controller ", padding=10)
        ctl.grid(row=6, column=0, columnspan=3, sticky="ew", pady=8)
        self.ctrl_lbl = tk.Label(ctl, text="", font=mid, anchor="w", justify="left")
        self.ctrl_lbl.grid(row=0, column=0, sticky="w")
        tk.Button(ctl, text="PP \u2192 MetaJointMain", font=mid,
                  command=self._set_pp).grid(row=0, column=1, padx=(12, 0))

        self.joint_lbl = ttk.Label(root, text="", font=("Consolas", 10))
        self.joint_lbl.grid(row=5, column=0, columnspan=3, sticky="w")

        self._set_param(SCALER, "distance_override", False)
        self._apply_baseline()
        root.protocol("WM_DELETE_WINDOW", self._close)
        self._refresh()

    # --- ROS ---------------------------------------------------------------
    def _set(self, key, value) -> None:
        with self.lock:
            self.state[key] = value

    def _trigger(self, what: str) -> None:
        srv = roslibpy.Service(self.ros, f"{PLAYBACK}/{what}", "std_srvs/Trigger")
        srv.call(roslibpy.ServiceRequest(),
                 callback=lambda r: self._say(r.get("message", "ok")),
                 errback=lambda e: self._say(f"{what} failed: {e}"))

    def _step(self, direction: int) -> None:
        with self.lock:
            cur = self.state["max"] if self.state["max"] is not None else 0.5
        v = min(1.0, max(0.1, round((cur + direction * STEP) / STEP) * STEP))
        self.max_req.publish(roslibpy.Message({"data": v}))

    def _apply_baseline(self) -> None:
        ts, v, a = BASELINE
        self._set_param(RELAY, "time_scale", ts)
        self._set_param(PLAYBACK, "velocity_scaling", v)
        self._set_param(PLAYBACK, "acceleration_scaling", a)

    def _mode(self) -> None:
        on = self.mode_var.get() == "pc"
        self._set_param(SCALER, "distance_override", on)
        self._set("override", on)

    def _set_param(self, node: str, name: str, value) -> None:
        if isinstance(value, bool):
            pv = {"type": 1, "bool_value": value}
        else:
            pv = {"type": 3, "double_value": float(value)}
        srv = roslibpy.Service(self.ros, f"{node}/set_parameters", "rcl_interfaces/srv/SetParameters")
        srv.call(roslibpy.ServiceRequest({"parameters": [{"name": name, "value": pv}]}),
                 callback=lambda r: None,
                 errback=lambda e: self._say(f"set {node}.{name} failed: {e}"))

    def _poll_controller(self) -> None:
        while True:
            try:
                self._set("ctrl", self.rws.state())
            except Exception as e:  # noqa: BLE001 - keep polling through cable pulls
                self._set("ctrl", {"error": str(e)[:60]})
            time.sleep(2.0)

    def _set_pp(self) -> None:
        def work():
            try:
                self._say(self.rws.set_pp("MetaJointMain"))
            except Exception as e:  # noqa: BLE001
                self._say(f"PP failed: {e}")
        threading.Thread(target=work, daemon=True).start()

    def _say(self, text: str) -> None:
        self.root.after(0, lambda: self.msg.configure(text=text))

    # --- UI loop -----------------------------------------------------------
    def _refresh(self) -> None:
        with self.lock:
            s = dict(self.state)
        self.conn.configure(text="rosbridge: connected" if self.ros.is_connected else "rosbridge: NOT CONNECTED")
        self.max_lbl.configure(text=f"{s['max'] * 100:.0f} %" if s["max"] is not None else "--")
        sp = s["speed"]
        self.bar["value"] = (sp or 0.0) * 100
        self.speed_lbl.configure(text=f"robot now at {sp * 100:.0f} % of full speed" if sp is not None
                                 else "no speed from the scaler")
        self.warn.configure(text="PC test active: no proximity slow-down, no headset-off stop. "
                                 "Hand on the e-stop." if s["override"] else "")
        st = s["status"]
        colors = {"ok": "#2e7d32", "info": "#1565c0", "warn": "#e65100", "error": "#c62828"}
        if st:
            text = ("MOVING: " if st["moving"] else "STOPPED: ") + st["reason"]
            if st["hint"]:
                text += "\n\u2192 " + st["hint"]
            self.status_lbl.configure(text=text, fg=colors.get(st["level"], "black"))
        else:
            self.status_lbl.configure(text="no robot status (robot_status node not running?)", fg="#c62828")
        c = s["ctrl"]
        if c is None:
            self.ctrl_lbl.configure(text="reading controller...", fg="black")
        elif "error" in c:
            self.ctrl_lbl.configure(text=f"controller not reachable ({c['error']})", fg="#c62828")
        else:
            pp_ok = c["routine"] == "MetaJointMain"
            self.ctrl_lbl.configure(
                text=f"{c['opmode']} | {c['ctrl']} | RAPID {c['exec']}\n"
                     f"PP {c['module']}/{c['routine']}" + ("" if pp_ok else "   <- WRONG PROGRAM, press the button"),
                fg="#2e7d32" if pp_ok else "#c62828")
        if s["joints"]:
            self.joint_lbl.configure(text="joints [deg]  " + "  ".join(
                f"{math.degrees(v):+7.1f}" for v in s["joints"][:6]))
        self.root.after(100, self._refresh)

    def _close(self) -> None:
        self._trigger("pause")
        self._set_param(SCALER, "distance_override", False)
        self.root.after(400, self.root.destroy)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=9090)
    args = ap.parse_args()
    ros = roslibpy.Ros(host=args.host, port=args.port)
    ros.run()
    root = tk.Tk()
    Console(root, ros)
    root.mainloop()
    ros.terminate()


if __name__ == "__main__":
    main()
