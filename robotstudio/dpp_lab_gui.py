"""
DPP Lab GUI — all-in-one for the Alexandra DPP run.

Loads waypoints from ~/dpp_waypoints.yaml (teached via "snap" tool earlier),
drives them in a continuous loop via persistent UDP EGM at speed controlled
by a live slider, and logs 1 Hz RWS samples tagged with a phase label.

Requirements:
  - RAPID running MainModule.main (EGMRunJoint loop on UDPUC "ROB_Michi")
  - UDPUC ROB_Michi pointing at this PC's IP, port 6511
  - protobuf installed (pip install protobuf), egm_pb2.py importable

Usage (Windows):
  python robotstudio/dpp_lab_gui.py
  python robotstudio/dpp_lab_gui.py --ip 192.168.125.1 --waypoints ~/dpp_waypoints.yaml
"""
from __future__ import annotations

import argparse
import json
import math
import os
import socket
import sys
import threading
import time
import tkinter as tk
import tkinter.filedialog as tkfd
import tkinter.messagebox as tkmb
from datetime import datetime, timezone
from pathlib import Path

import requests
import urllib3
import yaml
from requests.auth import HTTPBasicAuth

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# Protobuf import — egm_pb2 lives next to egm-mock dir
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / 'ai-services' / 'egm-mock'))
import egm_pb2  # type: ignore

DEFAULT_YAML   = Path.home() / 'dpp_waypoints.yaml'
DEFAULT_LOGDIR = Path.home() / 'dpp_runs'
EGM_PORT = 6511


# ---------------------- RWS client (logger only) -------------------------
class Rws:
    def __init__(self, ip: str, user: str, pwd: str) -> None:
        self.base = f'https://{ip}:443'
        self.s = requests.Session()
        self.s.auth = HTTPBasicAuth(user, pwd)
        self.s.verify = False

    def get(self, path: str, timeout: float = 1.5):
        try:
            r = self.s.get(self.base + path,
                           headers={'Accept': 'application/hal+json;v=2.0'},
                           timeout=timeout)
            return r.json() if r.status_code == 200 else None
        except Exception:
            return None


# ---------------------- EGM driver (background thread) -------------------
class EgmDriver:
    """Persistent EGM session + waypoint loop. Thread-safe target/speed updates."""

    def __init__(self, ctrl_ip: str, port: int = EGM_PORT) -> None:
        self.ctrl = (ctrl_ip, port)
        self.port = port

        self._lock = threading.Lock()
        self._waypoints: list[list[float]] = []   # joints in deg, per waypoint
        self._wp_idx = 0
        self._target_deg: list[float] | None = None
        self._feedback_deg: list[float] | None = None

        # Speed control: max joint change per EGM tick (250Hz)
        self.max_step_deg = 0.5
        self.tolerance_deg = 1.5
        self.loop = True
        self._driving = False    # True while iterating waypoints
        self._egm_up = threading.Event()
        self._stop = threading.Event()

        self.pass_count = 0
        self.arrival_count = 0
        self.last_err = 0.0

        self._sock: socket.socket | None = None
        self._thread: threading.Thread | None = None

    def set_waypoints(self, wps_deg: list[list[float]]) -> None:
        with self._lock:
            self._waypoints = [list(w) for w in wps_deg]
            self._wp_idx = 0
            # Don't reset target — driver may be holding current pose

    def start_driving(self) -> None:
        with self._lock:
            if not self._waypoints:
                return
            self._wp_idx = 0
            self._target_deg = list(self._waypoints[0])
            self._driving = True

    def stop_driving(self) -> None:
        """Hold at current feedback position (no longer chase waypoints)."""
        with self._lock:
            self._driving = False
            if self._feedback_deg:
                self._target_deg = list(self._feedback_deg)

    def start(self) -> None:
        self._stop.clear()
        self._thread = threading.Thread(target=self._loop, daemon=True)
        self._thread.start()

    def shutdown(self) -> None:
        self._stop.set()
        if self._sock:
            try: self._sock.close()
            except Exception: pass

    # The persistent EGM session — runs at ~250 Hz
    def _loop(self) -> None:
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            sock.bind(('0.0.0.0', self.port))
        except OSError as e:
            print(f'[egm] bind err: {e}', file=sys.stderr)
            return
        self._sock = sock
        sock.settimeout(0.02)

        seq = 0
        bootstrap_t = time.monotonic()
        while not self._stop.is_set():
            now = time.monotonic()

            # Bootstrap until session is up
            if not self._egm_up.is_set() and now - bootstrap_t > 0.04:
                bootstrap_t = now
                m = egm_pb2.EgmSensor()
                m.header.seqno = seq; seq += 1
                m.header.tm = int(time.time() * 1000) & 0xFFFFFFFF
                m.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
                with self._lock:
                    t = list(self._target_deg) if self._target_deg else []
                if t:
                    m.planned.joints.joints.extend(t)
                try:
                    sock.sendto(m.SerializeToString(), self.ctrl)
                except OSError: pass

            try:
                data, addr = sock.recvfrom(4096)
            except socket.timeout:
                continue
            except OSError:
                break

            robot = egm_pb2.EgmRobot()
            try:
                robot.ParseFromString(data)
            except Exception:
                continue
            if not (robot.HasField('feedBack') and robot.feedBack.HasField('joints')):
                continue
            current = list(robot.feedBack.joints.joints)[:6]
            if len(current) < 6:
                continue

            self._egm_up.set()

            with self._lock:
                self._feedback_deg = current
                # Initialize target on first feedback if not set
                if self._target_deg is None:
                    self._target_deg = list(current)
                target = list(self._target_deg)
                driving = self._driving
                wp_idx = self._wp_idx
                wps = self._waypoints
                tol = self.tolerance_deg
                max_step = self.max_step_deg

            err = max(abs(c - t) for c, t in zip(current, target))
            self.last_err = err

            # Convergence: advance to next waypoint
            if driving and err < tol and wps:
                self.arrival_count += 1
                wp_idx = (wp_idx + 1) % len(wps)
                if wp_idx == 0:
                    self.pass_count += 1
                if not self.loop and wp_idx == 0 and self.pass_count > 0:
                    # one-pass complete → stop driving, hold last pose
                    with self._lock:
                        self._driving = False
                        self._target_deg = list(current)
                else:
                    with self._lock:
                        self._wp_idx = wp_idx
                        self._target_deg = list(wps[wp_idx])
                        target = list(self._target_deg)

            # Rate-limit commanded delta
            cmd = []
            for c, t in zip(current, target):
                d = t - c
                d = max(-max_step, min(max_step, d))
                cmd.append(c + d)

            s = egm_pb2.EgmSensor()
            s.header.seqno = seq; seq += 1
            s.header.tm = int(time.time() * 1000) & 0xFFFFFFFF
            s.header.mtype = egm_pb2.EgmHeader.MSGTYPE_CORRECTION
            s.planned.joints.joints.extend(cmd)
            try:
                sock.sendto(s.SerializeToString(), addr)
            except OSError:
                break

    # Snapshots
    def current_feedback_deg(self) -> list[float] | None:
        with self._lock:
            return list(self._feedback_deg) if self._feedback_deg else None

    def current_wp_idx(self) -> int:
        with self._lock:
            return self._wp_idx


# ---------------------- GUI ----------------------------------------------
class DppLabGui:
    def __init__(self, root: tk.Tk, rws: Rws, driver: EgmDriver,
                 waypoints_path: Path) -> None:
        self.root = root
        self.rws = rws
        self.driver = driver
        self.waypoints_path = waypoints_path
        self.waypoints: list[dict] = []  # entries with 'name' and 'joints' (rad)

        self.root.title('MetaMove DPP — Lab Run')
        self.root.geometry('680x780')

        # Tk vars
        self.speed_var = tk.DoubleVar(value=driver.max_step_deg)
        self.loop_var = tk.BooleanVar(value=True)
        self.phase_var = tk.StringVar(value='normal')
        self.live_var = tk.StringVar(value='waiting for EGM session...')
        self.status_var = tk.StringVar(value='idle')

        # Logger state
        self.log_thread: threading.Thread | None = None
        self.log_stop = threading.Event()
        self.log_dir: Path | None = None
        self.log_count = 0

        self._build_ui()
        self._load_waypoints()
        self._tick_ui()

    def _build_ui(self) -> None:
        # --- live feedback ---
        f = tk.LabelFrame(self.root, text='Live (EGM feedback)', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)
        tk.Label(f, textvariable=self.live_var, font=('Consolas', 10),
                 justify='left', anchor='w').pack(fill='x')

        # --- waypoints ---
        f = tk.LabelFrame(self.root, text='Waypoints (from ~/dpp_waypoints.yaml)', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)
        self.wp_list = tk.Listbox(f, height=8, font=('Consolas', 9))
        self.wp_list.pack(fill='x')
        btns = tk.Frame(f); btns.pack(fill='x', pady=4)
        tk.Button(btns, text='Reload YAML', command=self._load_waypoints, width=12).pack(side='left', padx=2)
        tk.Button(btns, text='Snap current here', command=self._snap_current, width=18).pack(side='left', padx=2)
        tk.Button(btns, text='Delete sel.', command=self._delete_selected, width=10).pack(side='left', padx=2)

        # --- driver controls ---
        f = tk.LabelFrame(self.root, text='Driver (EGM)', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)
        tk.Label(f, text='Speed (max step deg/tick @250Hz, RAPID cap ~10°/s):').grid(row=0, column=0, columnspan=4, sticky='w')
        tk.Scale(f, from_=0.05, to=2.0, resolution=0.05, orient='horizontal',
                 variable=self.speed_var, length=420,
                 command=self._on_speed).grid(row=1, column=0, columnspan=4, sticky='ew')
        tk.Checkbutton(f, text='Loop forever (reshuffle off, sequential order)',
                       variable=self.loop_var, command=self._on_loop).grid(row=2, column=0, columnspan=4, sticky='w')

        ctrl = tk.Frame(f); ctrl.grid(row=3, column=0, columnspan=4, pady=6, sticky='ew')
        tk.Button(ctrl, text='▶ Start loop',   command=self._start_drive, width=14, bg='#cfc').pack(side='left', padx=2)
        tk.Button(ctrl, text='⏸ Hold pose',    command=self._stop_drive,  width=14, bg='#ffc').pack(side='left', padx=2)
        tk.Button(ctrl, text='Goto sel.',      command=self._goto_selected, width=12).pack(side='left', padx=2)

        # --- logger ---
        f = tk.LabelFrame(self.root, text='RWS sample logger (1 Hz)', padx=8, pady=6)
        f.pack(fill='x', padx=8, pady=4)
        tk.Label(f, text='Phase tag:').grid(row=0, column=0, sticky='w')
        tk.Entry(f, textvariable=self.phase_var, width=18).grid(row=0, column=1, sticky='w')
        for col, ph in enumerate(['normal', 'move', 'fast', 'stop'], start=2):
            tk.Button(f, text=ph, width=8,
                      command=lambda p=ph: self.phase_var.set(p)).grid(row=0, column=col, padx=2)
        ctrl = tk.Frame(f); ctrl.grid(row=1, column=0, columnspan=8, pady=4, sticky='ew')
        tk.Button(ctrl, text='● Start logging', command=self._start_log, width=15, bg='#cfc').pack(side='left', padx=2)
        tk.Button(ctrl, text='■ Stop logging',  command=self._stop_log,  width=15, bg='#fcc').pack(side='left', padx=2)

        # --- status ---
        bar = tk.Frame(self.root); bar.pack(fill='x', padx=8, pady=6)
        tk.Label(bar, textvariable=self.status_var, font=('Consolas', 10), fg='blue',
                 anchor='w', justify='left').pack(fill='x')

    # ---- waypoint loading ----
    def _load_waypoints(self) -> None:
        try:
            data = yaml.safe_load(self.waypoints_path.read_text()) or {}
            self.waypoints = list(data.get('waypoints', []))
        except FileNotFoundError:
            self.waypoints = []
        self.wp_list.delete(0, tk.END)
        for wp in self.waypoints:
            deg = wp.get('joints_deg') or [v * 180 / math.pi for v in wp['joints']]
            label = f"{wp['name']:24} " + ' '.join(f'{v:+6.1f}' for v in deg)
            self.wp_list.insert(tk.END, label)
        # Push to driver
        wps_deg = []
        for wp in self.waypoints:
            if 'joints_deg' in wp:
                wps_deg.append(list(wp['joints_deg']))
            else:
                wps_deg.append([v * 180 / math.pi for v in wp['joints']])
        self.driver.set_waypoints(wps_deg)

    def _snap_current(self) -> None:
        # Read live joints via RWS, append as new waypoint
        d = self.rws.get('/rw/motionsystem/mechunits/ROB_1/jointtarget', timeout=2.0)
        if not d:
            tkmb.showerror('snap', 'RWS read failed')
            return
        st = d.get('state', [{}])[0]
        try:
            deg = [float(st[f'rax_{i}']) for i in range(1, 7)]
        except Exception:
            tkmb.showerror('snap', 'parse failed')
            return
        rad = [v * math.pi / 180 for v in deg]
        name = f'wp_{len(self.waypoints) + 1:02d}_snap'
        self.waypoints.append({
            'name': name,
            'joints': [round(v, 6) for v in rad],
            'joints_deg': [round(v, 2) for v in deg],
            'recorded_utc': datetime.now(timezone.utc).isoformat(timespec='seconds').replace('+00:00', 'Z'),
        })
        self.waypoints_path.write_text(yaml.safe_dump({'waypoints': self.waypoints}, sort_keys=False))
        self._load_waypoints()

    def _delete_selected(self) -> None:
        sel = self.wp_list.curselection()
        if not sel: return
        del self.waypoints[sel[0]]
        self.waypoints_path.write_text(yaml.safe_dump({'waypoints': self.waypoints}, sort_keys=False))
        self._load_waypoints()

    # ---- driver controls ----
    def _on_speed(self, _v) -> None:
        self.driver.max_step_deg = float(self.speed_var.get())

    def _on_loop(self) -> None:
        self.driver.loop = bool(self.loop_var.get())

    def _start_drive(self) -> None:
        if not self.waypoints:
            tkmb.showinfo('no wps', 'load waypoints first')
            return
        self.driver.start_driving()

    def _stop_drive(self) -> None:
        self.driver.stop_driving()

    def _goto_selected(self) -> None:
        sel = self.wp_list.curselection()
        if not sel: return
        wp = self.waypoints[sel[0]]
        deg = wp.get('joints_deg') or [v * 180 / math.pi for v in wp['joints']]
        # Push as single target via internal driver mechanism
        self.driver.set_waypoints([list(deg)])
        self.driver.loop = False
        self.driver.start_driving()

    # ---- logger ----
    def _start_log(self) -> None:
        if self.log_thread and self.log_thread.is_alive():
            return
        stamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        self.log_dir = DEFAULT_LOGDIR / f'run_{stamp}' / 'samples'
        self.log_dir.mkdir(parents=True, exist_ok=True)
        self.log_count = 0
        self.log_stop.clear()
        self.log_thread = threading.Thread(target=self._log_loop, daemon=True)
        self.log_thread.start()
        tkmb.showinfo('logger', f'logging to {self.log_dir.parent}')

    def _stop_log(self) -> None:
        self.log_stop.set()

    def _log_loop(self) -> None:
        targets = {
            'cartesian':   '/rw/motionsystem/mechunits/ROB_1/cartesian',
            'jointtarget': '/rw/motionsystem/mechunits/ROB_1/jointtarget',
            'motion_err':  '/rw/motionsystem/errorstate',
            'rapid_exec':  '/rw/rapid/execution',
            'panel_speed': '/rw/panel/speedratio',
            'energy':      '/rw/system/energy',
        }
        next_tick = time.monotonic()
        while not self.log_stop.is_set():
            sample = {
                'sample_idx': self.log_count,
                'sample_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                'phase': self.phase_var.get(),
                'max_step_deg': float(self.speed_var.get()),
                'wp_idx': self.driver.current_wp_idx(),
                'driving': self.driver._driving,
            }
            fb = self.driver.current_feedback_deg()
            if fb is not None:
                sample['joints_deg_egm'] = [round(v, 3) for v in fb]
            for k, p in targets.items():
                d = self.rws.get(p, timeout=1.5)
                if d is not None:
                    sample[k] = d
            fn = self.log_dir / f"sample_{self.log_count:06d}_{sample['phase']}.json"
            fn.write_text(json.dumps(sample, ensure_ascii=False))
            self.log_count += 1
            next_tick += 1.0
            sl = next_tick - time.monotonic()
            if sl > 0:
                time.sleep(sl)
            else:
                next_tick = time.monotonic()

    # ---- 1 Hz tick (status display) ----
    def _tick_ui(self) -> None:
        fb = self.driver.current_feedback_deg()
        if fb is not None:
            self.live_var.set(
                f'J1={fb[0]:+7.2f}°  J2={fb[1]:+7.2f}°  J3={fb[2]:+7.2f}°\n'
                f'J4={fb[3]:+7.2f}°  J5={fb[4]:+7.2f}°  J6={fb[5]:+7.2f}°'
            )
        else:
            self.live_var.set('waiting for EGM session (is RAPID MainModule.main running?)')

        wp_idx = self.driver.current_wp_idx()
        self.status_var.set(
            f'EGM={"up" if self.driver._egm_up.is_set() else "..."}  '
            f'driving={self.driver._driving}  '
            f'wp={wp_idx}/{len(self.waypoints)}  '
            f'pass={self.driver.pass_count}  arrivals={self.driver.arrival_count}  '
            f'err={self.driver.last_err:.2f}°  '
            f'log={self.log_count}'
        )
        # Highlight current waypoint
        if 0 <= wp_idx < len(self.waypoints):
            self.wp_list.selection_clear(0, tk.END)
            self.wp_list.selection_set(wp_idx)
            self.wp_list.see(wp_idx)

        self.root.after(500, self._tick_ui)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument('--ip', default=os.environ.get('METAMOVE_RWS_IP', '192.168.125.1'))
    ap.add_argument('--user', default=os.environ.get('GOFA_USER', 'Default User'))
    ap.add_argument('--pwd', default=os.environ.get('GOFA_PASS', 'robotics'))
    ap.add_argument('--waypoints', type=Path, default=DEFAULT_YAML)
    args = ap.parse_args()

    rws = Rws(args.ip, args.user, args.pwd)
    driver = EgmDriver(args.ip)
    driver.start()  # start EGM session thread

    root = tk.Tk()
    DppLabGui(root, rws, driver, args.waypoints)
    try:
        root.mainloop()
    finally:
        driver.shutdown()


if __name__ == '__main__':
    main()
