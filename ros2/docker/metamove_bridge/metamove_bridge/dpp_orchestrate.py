"""
DPP lab-run orchestrator — drives playback through the 4 phases and logs 1 Hz
RWS snapshots, all in one process.

Phases (matches Alexandra's request):
    normal : robot still, motors on        → /dpp_playback paused, v=0.25 (irrelevant)
    move   : waypoint loop, normal speed   → resume, v=0.25
    fast   : waypoint loop, full speed     → v=1.00
    stop   : program paused mid-motion     → pause (cancels current MoveGroup goal)

Default phase plan (matches her email):
    normal:300, move:300, fast:120, stop:60   (≈ 13 minutes total)

This node *requires* /dpp_playback to be running. It sends parameter updates
and Trigger services to that node. RWS sampling happens here, in the same
loop, so phase tags and samples never drift apart.

Output:
    lab_test_<stamp>/
        dpp_samples/sample_NNNNNN_<phase>.json   (~2-5 KB each)
        summary.json                              (per-phase ΔE, sample count)
        orchestrate.log                           (phase transitions + errors)

Usage (inside metamove_bridge container, with /dpp_playback already running):
    ros2 run metamove_bridge dpp_orchestrate
    ros2 run metamove_bridge dpp_orchestrate --ros-args \\
        -p phases:="normal:60,move:60,fast:30,stop:15" \\
        -p rws_url:="https://192.168.125.1:443" \\
        -p out_root:="/workspace/lab_runs"
"""
from __future__ import annotations

import json
import os
import threading
import time
from datetime import datetime, timezone
from pathlib import Path

import rclpy
import requests
import urllib3
from rcl_interfaces.srv import SetParameters
from rcl_interfaces.msg import Parameter, ParameterType, ParameterValue
from rclpy.node import Node
from requests.auth import HTTPBasicAuth
from std_srvs.srv import Trigger

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


PHASES_DEFAULT = 'normal:300,move:300,fast:120,stop:60'

# Per-phase velocity_scaling + whether playback should be running.
# fast capped at 0.50 (user decision 2026-06-11: "nicht übertreiben").
# Empirisch: ruckige Trajektorien-Starts triggern die kollaborative
# Überwachung des GoFa (RAPID-Fehler stoppt das Programm mitten in der
# Messung) — deshalb move konservativ und fast nur mit niedriger
# acceleration_scaling im Playback fahren.
PHASE_PLAN = {
    'normal': {'v': 0.15, 'play': False},
    'move':   {'v': 0.15, 'play': True},
    'fast':   {'v': 0.50, 'play': True},
    'stop':   {'v': 0.50, 'play': False},
}

RWS_TARGETS = {
    'cartesian':    '/rw/motionsystem/mechunits/ROB_1/cartesian',
    'jointtarget':  '/rw/motionsystem/mechunits/ROB_1/jointtarget',
    'motion_err':   '/rw/motionsystem/errorstate',
    'rapid_exec':   '/rw/rapid/execution',
    'panel_speed':  '/rw/panel/speedratio',
    'panel_opmode': '/rw/panel/opmode',
    'panel_ctrl':   '/rw/panel/ctrl-state',
    'energy':       '/rw/system/energy',
}


def parse_phases(spec: str) -> list[tuple[str, int]]:
    out = []
    for chunk in spec.split(','):
        chunk = chunk.strip()
        if not chunk:
            continue
        name, dur = chunk.split(':', 1)
        out.append((name.strip(), int(dur)))
    return out


class DppOrchestrate(Node):
    def __init__(self) -> None:
        super().__init__('dpp_orchestrate')

        self.declare_parameter('phases', PHASES_DEFAULT)
        self.declare_parameter('rws_url', 'https://192.168.125.1:443')
        self.declare_parameter('rws_user', 'Default User')
        self.declare_parameter('rws_pass', 'robotics')
        self.declare_parameter('out_root', str(Path.home() / 'lab_runs'))
        self.declare_parameter('period', 1.0)
        self.declare_parameter('playback_node', '/dpp_playback')
        self.declare_parameter('dry_run', False)
        self.declare_parameter('rws_enabled', True)

        phases_str = self.get_parameter('phases').value
        self.phases = parse_phases(phases_str)
        for name, _ in self.phases:
            if name not in PHASE_PLAN:
                raise RuntimeError(f'unknown phase "{name}", expected one of {list(PHASE_PLAN)}')

        self.dry_run = bool(self.get_parameter('dry_run').value)
        self.rws_enabled = bool(self.get_parameter('rws_enabled').value)
        self.period = float(self.get_parameter('period').value)
        self.playback_node = self.get_parameter('playback_node').value

        stamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        self.run_dir = Path(self.get_parameter('out_root').value) / f'lab_test_{stamp}'
        self.samples_dir = self.run_dir / 'dpp_samples'
        self.samples_dir.mkdir(parents=True, exist_ok=True)
        self.log_path = self.run_dir / 'orchestrate.log'
        self.summary_path = self.run_dir / 'summary.json'

        # RWS session
        self.rws_url = self.get_parameter('rws_url').value
        self.sess = requests.Session()
        self.sess.auth = HTTPBasicAuth(self.get_parameter('rws_user').value,
                                       self.get_parameter('rws_pass').value)
        self.sess.verify = False
        self.sess.headers.update({'Accept': 'application/hal+json;v=2.0'})

        # ROS clients for playback control
        self.param_cli = self.create_client(SetParameters, f'{self.playback_node}/set_parameters')
        self.pause_cli = self.create_client(Trigger, f'{self.playback_node}/pause')
        self.resume_cli = self.create_client(Trigger, f'{self.playback_node}/resume')

        self._log(f'run dir: {self.run_dir}')
        self._log(f'phases:  {self.phases}')
        self._log(f'rws:     {self.rws_url}  (enabled={self.rws_enabled})')
        self._log(f'dry_run: {self.dry_run}')

    # ---- helpers ----------------------------------------------------------

    def _log(self, msg: str) -> None:
        line = f'{datetime.now().isoformat(timespec="seconds")}  {msg}'
        self.get_logger().info(msg)
        with open(self.log_path, 'a') as f:
            f.write(line + '\n')

    def _rws_get(self, path: str) -> dict | None:
        if not self.rws_enabled:
            return None
        try:
            r = self.sess.get(f'{self.rws_url}{path}', timeout=2.0)
            return r.json() if r.status_code == 200 else None
        except Exception:
            return None

    def _wait_for(self, cli, timeout: float = 5.0) -> bool:
        return cli.wait_for_service(timeout_sec=timeout)

    def _set_velocity_scaling(self, v: float) -> None:
        if self.dry_run:
            self._log(f'[dry] set velocity_scaling={v}')
            return
        if not self._wait_for(self.param_cli):
            self._log(f'! {self.playback_node}/set_parameters not available')
            return
        req = SetParameters.Request()
        p = Parameter()
        p.name = 'velocity_scaling'
        p.value = ParameterValue(type=ParameterType.PARAMETER_DOUBLE, double_value=float(v))
        req.parameters.append(p)
        fut = self.param_cli.call_async(req)
        self._await_future(fut, 3.0)
        res = fut.result()
        ok = bool(res and res.results and res.results[0].successful)
        self._log(f'velocity_scaling={v}  ok={ok}')

    @staticmethod
    def _await_future(fut, timeout_s: float) -> None:
        # The phase loop runs in a worker thread while the MAIN thread spins
        # the executor. spin_until_future_complete here raises "Executor is
        # already spinning" — just poll; the main spin completes the future.
        t0 = time.monotonic()
        while not fut.done() and time.monotonic() - t0 < timeout_s:
            time.sleep(0.05)

    def _call_trigger(self, cli, label: str) -> None:
        if self.dry_run:
            self._log(f'[dry] {label}')
            return
        if not self._wait_for(cli):
            self._log(f'! {label} service not available')
            return
        fut = cli.call_async(Trigger.Request())
        self._await_future(fut, 3.0)
        res = fut.result()
        self._log(f'{label}: ' + (res.message if res else 'no response'))

    # ---- phase transitions ------------------------------------------------

    def _enter_phase(self, name: str) -> None:
        plan = PHASE_PLAN[name]
        self._log(f'=== enter phase "{name}"  v={plan["v"]}  play={plan["play"]} ===')
        self._set_velocity_scaling(plan['v'])
        if plan['play']:
            self._call_trigger(self.resume_cli, 'resume')
        else:
            self._call_trigger(self.pause_cli, 'pause')

    # ---- main loop --------------------------------------------------------

    def run(self) -> None:
        sample_idx = 0
        summary: dict = {
            'started_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
            'rws_url': self.rws_url,
            'sample_period_s': self.period,
            'phases': [],
        }
        try:
            for phase_name, phase_dur in self.phases:
                self._enter_phase(phase_name)
                # Give the playback node a moment to act on pause/resume.
                time.sleep(1.0)

                phase_start = time.monotonic()
                phase_start_utc = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
                phase_samples = 0
                e_start: float | None = None
                e_last: float | None = None

                next_tick = time.monotonic()
                while time.monotonic() - phase_start < phase_dur:
                    sample: dict = {
                        'phase': phase_name,
                        'phase_elapsed_s': round(time.monotonic() - phase_start, 3),
                        'sample_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                        'sample_idx': sample_idx,
                    }
                    for k, p in RWS_TARGETS.items():
                        d = self._rws_get(p)
                        if d is not None:
                            sample[k] = d

                    if 'energy' in sample:
                        for s in sample['energy'].get('state', []):
                            if s.get('_type') == 'sys-energy-state':
                                try:
                                    kwh = float(s.get('accumulated-energy', 0))
                                    if e_start is None:
                                        e_start = kwh
                                    e_last = kwh
                                except Exception:
                                    pass
                                break

                    fn = self.samples_dir / f'sample_{sample_idx:06d}_{phase_name}.json'
                    fn.write_text(json.dumps(sample, ensure_ascii=False))
                    sample_idx += 1
                    phase_samples += 1

                    next_tick += self.period
                    sleep_for = next_tick - time.monotonic()
                    if sleep_for > 0:
                        time.sleep(sleep_for)
                    else:
                        # We're behind — reset to avoid runaway drift.
                        next_tick = time.monotonic()

                delta = (e_last - e_start) if (e_start is not None and e_last is not None) else None
                summary['phases'].append({
                    'name': phase_name,
                    'planned_s': phase_dur,
                    'actual_s': round(time.monotonic() - phase_start, 2),
                    'started_utc': phase_start_utc,
                    'finished_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                    'samples': phase_samples,
                    'energy_kwh_start': e_start,
                    'energy_kwh_end': e_last,
                    'energy_kwh_delta': delta,
                    'velocity_scaling': PHASE_PLAN[phase_name]['v'],
                    'playback_active': PHASE_PLAN[phase_name]['play'],
                })
                self._log(f'   phase "{phase_name}" done — {phase_samples} samples, ΔE={delta} kWh')
        except KeyboardInterrupt:
            self._log('aborted by Ctrl-C')
            summary['aborted'] = True
        finally:
            # Always leave the robot in a safe state: pause playback.
            self._call_trigger(self.pause_cli, 'pause (cleanup)')
            summary['finished_utc'] = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
            summary['total_samples'] = sample_idx
            self.summary_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
            self._log(f'summary: {self.summary_path}')
            self._log(f'samples: {sample_idx} in {self.samples_dir}')


def main() -> None:
    rclpy.init()
    try:
        node = DppOrchestrate()
    except RuntimeError as e:
        print(f'fatal: {e}')
        rclpy.shutdown()
        return
    # Run the phase loop in a worker so rclpy can keep spinning for service calls.
    worker = threading.Thread(target=node.run, daemon=True)
    worker.start()
    try:
        while worker.is_alive() and rclpy.ok():
            rclpy.spin_once(node, timeout_sec=0.1)
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()
