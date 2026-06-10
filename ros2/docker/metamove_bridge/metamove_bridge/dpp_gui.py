"""
DPP Lab GUI — Tkinter UI for teaching waypoints, running playback, live speed control.

Single-process: ROS node + Tkinter mainloop. No extra Python deps beyond rclpy/tk.

Features:
  - Live joint-state display (1 Hz)
  - "Snap" current joints as next waypoint (wp_NN)
  - Save / Load waypoints YAML
  - MoveGroup goals sent in shuffled loop
  - Speed slider (0.05 .. 1.0)   — takes effect on NEXT waypoint
  - Loop toggle (reshuffle each pass)
  - Start / Pause / Stop buttons
  - Sample logger toggle (1 Hz RWS snapshots tagged with current phase)
  - Phase label (free text) appended to each sample for Alexandra's data

Usage in container (with crb15000_complete already running):
    ros2 run metamove_bridge dpp_gui
"""
from __future__ import annotations

import math
import os
import random
import threading
import time
import tkinter as tk
import tkinter.filedialog as tkfd
import tkinter.messagebox as tkmb
from datetime import datetime, timezone
from pathlib import Path
from queue import Queue

import rclpy
from rclpy.action import ActionClient
from rclpy.node import Node
from rclpy.qos import QoSDurabilityPolicy, QoSProfile, QoSReliabilityPolicy
from sensor_msgs.msg import JointState

from moveit_msgs.action import MoveGroup
from moveit_msgs.msg import (
    Constraints,
    JointConstraint,
    MotionPlanRequest,
    PlanningOptions,
)

import json
import requests
import urllib3
from requests.auth import HTTPBasicAuth

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

JOINT_NAMES = ['joint_1', 'joint_2', 'joint_3', 'joint_4', 'joint_5', 'joint_6']
DEFAULT_YAML = Path.home() / 'dpp_waypoints.yaml'
DEFAULT_LOG_DIR = Path.home() / 'dpp_runs'


class DppNode(Node):
    """rclpy worker for joint state subscription + MoveGroup action client."""

    def __init__(self) -> None:
        super().__init__('dpp_gui')
        self._latest_joints: list[float] | None = None
        self._lock = threading.Lock()
        self.create_subscription(
            JointState, '/joint_states', self._on_js,
            QoSProfile(depth=1,
                       reliability=QoSReliabilityPolicy.BEST_EFFORT,
                       durability=QoSDurabilityPolicy.VOLATILE),
        )
        self.mg_client = ActionClient(self, MoveGroup, '/move_action')

    def _on_js(self, msg: JointState) -> None:
        idx = {n: i for i, n in enumerate(msg.name)}
        try:
            ordered = [msg.position[idx[n]] for n in JOINT_NAMES]
        except KeyError:
            return
        with self._lock:
            self._latest_joints = ordered

    def latest_joints(self) -> list[float] | None:
        with self._lock:
            return list(self._latest_joints) if self._latest_joints else None


class DppGui:
    def __init__(self, root: tk.Tk, node: DppNode) -> None:
        self.root = root
        self.node = node
        self.root.title('MetaMove DPP — Lab Run')
        self.root.geometry('560x720')

        self.waypoints: list[dict] = []
        self.yaml_path: Path = DEFAULT_YAML

        # Playback state
        self.play_thread: threading.Thread | None = None
        self.play_stop = threading.Event()
        self.play_paused = threading.Event()
        self.current_goal_handle = None
        self.pass_count = 0
        self.ok_count = 0
        self.fail_count = 0

        # Logger state
        self.log_thread: threading.Thread | None = None
        self.log_stop = threading.Event()
        self.log_dir: Path | None = None
        self.log_count = 0
        self.phase_label = tk.StringVar(value='normal')

        # Tk variables
        self.velocity_var = tk.DoubleVar(value=0.25)
        self.acceleration_var = tk.DoubleVar(value=0.25)
        self.loop_var = tk.BooleanVar(value=True)
        self.dwell_var = tk.DoubleVar(value=0.5)
        self.rws_ip_var = tk.StringVar(value=os.environ.get('METAMOVE_RWS_IP', '192.168.125.1'))
        self.rws_enable_var = tk.BooleanVar(value=True)
        self.status_var = tk.StringVar(value='idle')
        self.joints_var = tk.StringVar(value='waiting for /joint_states ...')

        self._build_ui()
        self._tick_ui()

        if DEFAULT_YAML.exists():
            self._load_yaml(DEFAULT_YAML)

    # ---- UI layout -----------------------------------------------------

    def _build_ui(self) -> None:
        # Live joint display
        f = tk.LabelFrame(self.root, text='Live joint state', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)
        tk.Label(f, textvariable=self.joints_var, font=('Consolas', 10),
                 justify='left', anchor='w').pack(fill='x')

        # Waypoints
        f = tk.LabelFrame(self.root, text='Waypoints', padx=8, pady=6)
        f.pack(fill='both', expand=True, padx=8, pady=4)

        list_frame = tk.Frame(f)
        list_frame.pack(fill='both', expand=True)
        self.wp_list = tk.Listbox(list_frame, height=8, font=('Consolas', 9))
        sb = tk.Scrollbar(list_frame, command=self.wp_list.yview)
        self.wp_list.config(yscrollcommand=sb.set)
        self.wp_list.pack(side='left', fill='both', expand=True)
        sb.pack(side='right', fill='y')

        btns = tk.Frame(f)
        btns.pack(fill='x', pady=4)
        tk.Button(btns, text='Snap current  +', command=self.snap_current, width=14).pack(side='left', padx=2)
        tk.Button(btns, text='Delete sel.',     command=self.delete_selected, width=10).pack(side='left', padx=2)
        tk.Button(btns, text='Clear all',       command=self.clear_all, width=8).pack(side='left', padx=2)
        tk.Button(btns, text='Goto sel.',       command=self.goto_selected, width=10).pack(side='left', padx=2)

        btns2 = tk.Frame(f)
        btns2.pack(fill='x')
        tk.Button(btns2, text='Save YAML', command=self.save_yaml, width=12).pack(side='left', padx=2)
        tk.Button(btns2, text='Load YAML', command=self.open_yaml, width=12).pack(side='left', padx=2)
        tk.Label(btns2, textvariable=tk.StringVar(value=''), width=2).pack(side='left')
        self.yaml_label = tk.Label(btns2, text=str(self.yaml_path), font=('Consolas', 8), fg='gray')
        self.yaml_label.pack(side='left')

        # Playback controls
        f = tk.LabelFrame(self.root, text='Playback', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)

        # Velocity scaling slider
        tk.Label(f, text='Velocity scaling:').grid(row=0, column=0, sticky='w')
        tk.Scale(f, from_=0.05, to=1.0, resolution=0.05, orient='horizontal',
                 variable=self.velocity_var, length=300).grid(row=0, column=1, columnspan=3, sticky='ew')

        tk.Label(f, text='Acceleration scaling:').grid(row=1, column=0, sticky='w')
        tk.Scale(f, from_=0.05, to=1.0, resolution=0.05, orient='horizontal',
                 variable=self.acceleration_var, length=300).grid(row=1, column=1, columnspan=3, sticky='ew')

        tk.Label(f, text='Dwell (s):').grid(row=2, column=0, sticky='w')
        tk.Spinbox(f, from_=0.0, to=10.0, increment=0.5, textvariable=self.dwell_var, width=6
                  ).grid(row=2, column=1, sticky='w')
        tk.Checkbutton(f, text='Loop (reshuffle every pass)',
                       variable=self.loop_var).grid(row=2, column=2, columnspan=2, sticky='w')

        ctrl = tk.Frame(f)
        ctrl.grid(row=3, column=0, columnspan=4, pady=6, sticky='ew')
        tk.Button(ctrl, text='▶ Start',  command=self.start_playback, width=10, bg='#cfc').pack(side='left', padx=2)
        tk.Button(ctrl, text='⏸ Pause',  command=self.pause_playback, width=10, bg='#ffc').pack(side='left', padx=2)
        tk.Button(ctrl, text='⏹ Stop',   command=self.stop_playback, width=10, bg='#fcc').pack(side='left', padx=2)

        # RWS logger
        f = tk.LabelFrame(self.root, text='RWS sample logger', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)

        tk.Label(f, text='Phase:').grid(row=0, column=0, sticky='w')
        tk.Entry(f, textvariable=self.phase_label, width=14).grid(row=0, column=1, sticky='w')
        tk.Label(f, text='  RWS IP:').grid(row=0, column=2, sticky='w')
        tk.Entry(f, textvariable=self.rws_ip_var, width=15).grid(row=0, column=3, sticky='w')
        tk.Checkbutton(f, text='Enabled', variable=self.rws_enable_var).grid(row=0, column=4, sticky='w')

        ctrl = tk.Frame(f)
        ctrl.grid(row=1, column=0, columnspan=5, pady=4, sticky='ew')
        tk.Button(ctrl, text='● Start logging', command=self.start_logging, width=15, bg='#cfc').pack(side='left', padx=2)
        tk.Button(ctrl, text='■ Stop logging',  command=self.stop_logging,  width=15, bg='#fcc').pack(side='left', padx=2)

        # Status bar
        f = tk.Frame(self.root)
        f.pack(fill='x', padx=8, pady=6)
        tk.Label(f, text='Status:').pack(side='left')
        tk.Label(f, textvariable=self.status_var, font=('Consolas', 10), fg='blue').pack(side='left')

    # ---- 1 Hz UI tick (joint display + status refresh) -----------------

    def _tick_ui(self) -> None:
        j = self.node.latest_joints()
        if j:
            deg = [v * 57.29578 for v in j]
            self.joints_var.set(
                f"J1={deg[0]:+7.2f}°  J2={deg[1]:+7.2f}°  J3={deg[2]:+7.2f}°\n"
                f"J4={deg[3]:+7.2f}°  J5={deg[4]:+7.2f}°  J6={deg[5]:+7.2f}°"
            )
        self.status_var.set(
            f"wps={len(self.waypoints)} | pass={self.pass_count} ok={self.ok_count} fail={self.fail_count}"
            f" | log={self.log_count}"
        )
        self.root.after(500, self._tick_ui)

    # ---- waypoint editing ----------------------------------------------

    def _refresh_wp_list(self) -> None:
        self.wp_list.delete(0, tk.END)
        for wp in self.waypoints:
            deg = ', '.join(f'{v * 57.29578:+6.1f}' for v in wp['joints'])
            self.wp_list.insert(tk.END, f"{wp['name']}  [{deg}]")

    def snap_current(self) -> None:
        j = self.node.latest_joints()
        if j is None:
            tkmb.showwarning('no joints', 'still waiting for /joint_states')
            return
        name = f"wp_{len(self.waypoints) + 1:02d}"
        self.waypoints.append({
            'name': name,
            'joints': [round(v, 6) for v in j],
            'recorded_utc': datetime.now(timezone.utc).isoformat(timespec='seconds').replace('+00:00', 'Z'),
        })
        self._refresh_wp_list()

    def delete_selected(self) -> None:
        sel = self.wp_list.curselection()
        if not sel: return
        del self.waypoints[sel[0]]
        self._refresh_wp_list()

    def clear_all(self) -> None:
        if not self.waypoints: return
        if tkmb.askyesno('clear all', f'Drop all {len(self.waypoints)} waypoints?'):
            self.waypoints.clear()
            self._refresh_wp_list()

    def goto_selected(self) -> None:
        sel = self.wp_list.curselection()
        if not sel:
            tkmb.showinfo('select', 'select a waypoint first')
            return
        wp = self.waypoints[sel[0]]
        threading.Thread(target=self._execute_one, args=(wp,), daemon=True).start()

    # ---- YAML I/O ------------------------------------------------------

    def _load_yaml(self, path: Path) -> None:
        try:
            import yaml
            data = yaml.safe_load(path.read_text()) or {}
            self.waypoints = list(data.get('waypoints', []))
            self.yaml_path = path
            self.yaml_label.config(text=str(path))
            self._refresh_wp_list()
        except Exception as e:
            tkmb.showerror('load', str(e))

    def open_yaml(self) -> None:
        p = tkfd.askopenfilename(title='Load waypoints YAML',
                                 filetypes=[('YAML', '*.yaml *.yml'), ('All', '*.*')])
        if p:
            self._load_yaml(Path(p))

    def save_yaml(self) -> None:
        p = tkfd.asksaveasfilename(initialfile=self.yaml_path.name,
                                   initialdir=str(self.yaml_path.parent),
                                   filetypes=[('YAML', '*.yaml')],
                                   defaultextension='.yaml')
        if not p: return
        try:
            import yaml
            Path(p).write_text(yaml.safe_dump({'waypoints': self.waypoints}, sort_keys=False))
            self.yaml_path = Path(p)
            self.yaml_label.config(text=p)
        except Exception as e:
            tkmb.showerror('save', str(e))

    # ---- MoveGroup goal execution --------------------------------------

    def _build_goal(self, wp: dict) -> MoveGroup.Goal:
        v = max(0.01, min(1.0, float(self.velocity_var.get())))
        a = max(0.01, min(1.0, float(self.acceleration_var.get())))

        constraint = Constraints(name=wp['name'])
        for name, target in zip(JOINT_NAMES, wp['joints']):
            jc = JointConstraint()
            jc.joint_name = name
            jc.position = float(target)
            jc.tolerance_above = 0.005
            jc.tolerance_below = 0.005
            jc.weight = 1.0
            constraint.joint_constraints.append(jc)

        req = MotionPlanRequest()
        req.group_name = 'manipulator'
        req.planner_id = 'RRTConnectkConfigDefault'
        req.num_planning_attempts = 5
        req.allowed_planning_time = 2.0
        req.max_velocity_scaling_factor = v
        req.max_acceleration_scaling_factor = a
        req.goal_constraints.append(constraint)

        goal = MoveGroup.Goal()
        goal.request = req
        goal.planning_options = PlanningOptions()
        goal.planning_options.plan_only = False
        return goal

    def _execute_one(self, wp: dict) -> bool:
        goal = self._build_goal(wp)
        if not self.node.mg_client.wait_for_server(timeout_sec=5.0):
            self.node.get_logger().error('/move_action not available')
            return False
        send_future = self.node.mg_client.send_goal_async(goal)
        while rclpy.ok() and not send_future.done():
            time.sleep(0.02)
        gh = send_future.result()
        if gh is None or not gh.accepted:
            return False
        self.current_goal_handle = gh
        result_future = gh.get_result_async()
        while rclpy.ok() and not result_future.done():
            if self.play_stop.is_set() or self.play_paused.is_set():
                gh.cancel_goal_async()
                break
            time.sleep(0.02)
        self.current_goal_handle = None
        res = result_future.result()
        if res is None:
            return False
        return (res.result.error_code.val == 1) if res.result and res.result.error_code else False

    # ---- playback loop -------------------------------------------------

    def start_playback(self) -> None:
        if self.play_thread and self.play_thread.is_alive():
            self.play_paused.clear()
            return
        if not self.waypoints:
            tkmb.showinfo('no wps', 'teach some waypoints first')
            return
        self.play_stop.clear()
        self.play_paused.clear()
        self.pass_count = 0
        self.ok_count = 0
        self.fail_count = 0
        self.play_thread = threading.Thread(target=self._playback_loop, daemon=True)
        self.play_thread.start()

    def pause_playback(self) -> None:
        self.play_paused.set()
        if self.current_goal_handle is not None:
            self.current_goal_handle.cancel_goal_async()

    def stop_playback(self) -> None:
        self.play_stop.set()
        if self.current_goal_handle is not None:
            self.current_goal_handle.cancel_goal_async()

    def _playback_loop(self) -> None:
        order = list(range(len(self.waypoints)))
        random.shuffle(order)
        i = 0
        while not self.play_stop.is_set():
            if self.play_paused.is_set():
                time.sleep(0.2)
                continue
            wp = self.waypoints[order[i]]
            ok = self._execute_one(wp)
            if self.play_stop.is_set(): break
            if ok: self.ok_count += 1
            else:  self.fail_count += 1
            d = float(self.dwell_var.get())
            if d > 0 and not self.play_paused.is_set():
                time.sleep(d)
            i += 1
            if i >= len(order):
                self.pass_count += 1
                i = 0
                if self.loop_var.get():
                    random.shuffle(order)

    # ---- RWS sample logger ---------------------------------------------

    def start_logging(self) -> None:
        if self.log_thread and self.log_thread.is_alive():
            return
        self.log_dir = DEFAULT_LOG_DIR / f"run_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
        (self.log_dir / 'samples').mkdir(parents=True, exist_ok=True)
        self.log_count = 0
        self.log_stop.clear()
        self.log_thread = threading.Thread(target=self._log_loop, daemon=True)
        self.log_thread.start()

    def stop_logging(self) -> None:
        self.log_stop.set()

    def _log_loop(self) -> None:
        sess = requests.Session()
        sess.auth = HTTPBasicAuth(
            os.environ.get('GOFA_USER', 'Default User'),
            os.environ.get('GOFA_PASS', 'robotics'))
        sess.verify = False
        sess.headers.update({'Accept': 'application/hal+json;v=2.0'})
        targets = {
            'cartesian':   '/rw/motionsystem/mechunits/ROB_1/cartesian',
            'jointtarget': '/rw/motionsystem/mechunits/ROB_1/jointtarget',
            'motion_err':  '/rw/motionsystem/errorstate',
            'rapid_exec':  '/rw/rapid/execution',
            'panel_speed': '/rw/panel/speedratio',
            'energy':      '/rw/system/energy',
        }
        while not self.log_stop.is_set():
            sample = {
                'sample_idx': self.log_count,
                'sample_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                'phase': self.phase_label.get(),
                'velocity_scaling': float(self.velocity_var.get()),
            }
            j = self.node.latest_joints()
            if j is not None:
                sample['joints_rad'] = [round(v, 6) for v in j]
            if self.rws_enable_var.get():
                base = f"https://{self.rws_ip_var.get()}:443"
                for k, p in targets.items():
                    try:
                        r = sess.get(base + p, timeout=2.0)
                        if r.status_code == 200:
                            sample[k] = r.json()
                    except Exception:
                        pass
            fn = self.log_dir / 'samples' / f"sample_{self.log_count:06d}_{sample['phase']}.json"
            fn.write_text(json.dumps(sample, ensure_ascii=False))
            self.log_count += 1
            time.sleep(1.0)


def main() -> None:
    rclpy.init()
    node = DppNode()
    # rclpy spinner thread
    spin_thread = threading.Thread(target=lambda: rclpy.spin(node), daemon=True)
    spin_thread.start()

    root = tk.Tk()
    gui = DppGui(root, node)
    try:
        root.mainloop()
    finally:
        gui.stop_playback()
        gui.stop_logging()
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
