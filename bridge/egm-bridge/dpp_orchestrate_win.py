"""DPP orchestrator — Windows edition.

The container cannot reach the controller (WSL mirrored networking blocks
container->LAN), so RWS sampling runs HERE on Windows where it is fast and
proven. Playback control (velocity param, pause/resume) goes via rosbridge.

Phases & 1 Hz JSON snapshots per Alexandra's spec (DPP dynamic mode).

Usage:
  python dpp_orchestrate_win.py
  python dpp_orchestrate_win.py --phases "normal:120,move:120,fast:120,stop:60"
"""
from __future__ import annotations

import argparse
import json
import time
from datetime import datetime, timezone
from pathlib import Path

import requests
import roslibpy
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

# fast capped at 0.50 (user decision: "nicht übertreiben") — higher trips the
# GoFa collaborative supervision (RAPID error kills the measurement).
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


def utcnow() -> str:
    return datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('--phases', default='normal:120,move:120,fast:120,stop:60')
    ap.add_argument('--rws-url', default='https://192.168.125.1:443')
    ap.add_argument('--rosbridge-host', default='127.0.0.1')
    ap.add_argument('--rosbridge-port', type=int, default=9090)
    ap.add_argument('--out-root', default=r'C:\git\MetaMove\lab_runs')
    ap.add_argument('--period', type=float, default=1.0)
    args = ap.parse_args()

    phases = []
    for chunk in args.phases.split(','):
        name, dur = chunk.strip().split(':')
        phases.append((name.strip(), int(dur)))

    stamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    run_dir = Path(args.out_root) / f'lab_test_{stamp}'
    samples_dir = run_dir / 'dpp_samples'
    samples_dir.mkdir(parents=True, exist_ok=True)
    log_path = run_dir / 'orchestrate.log'

    def log(msg: str) -> None:
        line = f'{datetime.now().isoformat(timespec="seconds")}  {msg}'
        print(line, flush=True)
        with open(log_path, 'a', encoding='utf-8') as f:
            f.write(line + '\n')

    # RWS session (Windows -> controller, fast)
    sess = requests.Session()
    sess.auth = ('Default User', 'robotics')
    sess.verify = False
    sess.headers.update({'Accept': 'application/hal+json;v=2.0'})

    def rws_get(path: str):
        # No ?json=1 — the Accept: hal+json header already selects JSON, and
        # the energy endpoint answers 400 if the query param is present.
        try:
            r = sess.get(f'{args.rws_url}{path}', timeout=1.5)
            return r.json() if r.status_code == 200 else None
        except Exception:
            return None

    # rosbridge (playback control)
    ros = roslibpy.Ros(host=args.rosbridge_host, port=args.rosbridge_port)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        log('FEHLER: rosbridge nicht erreichbar')
        return 1
    param_srv = roslibpy.Service(ros, '/dpp_playback/set_parameters',
                                 'rcl_interfaces/srv/SetParameters')
    pause_srv = roslibpy.Service(ros, '/dpp_playback/pause', 'std_srvs/Trigger')
    resume_srv = roslibpy.Service(ros, '/dpp_playback/resume', 'std_srvs/Trigger')

    def set_velocity(v: float) -> None:
        try:
            param_srv.call(roslibpy.ServiceRequest({'parameters': [
                {'name': 'velocity_scaling',
                 'value': {'type': 3, 'double_value': float(v)}}]}), timeout=5)
            log(f'velocity_scaling={v}')
        except Exception as e:
            log(f'! velocity set: {e}')

    def trigger(srv, label: str) -> None:
        try:
            r = srv.call(roslibpy.ServiceRequest({}), timeout=5)
            log(f'{label}: {r.get("message", "ok")}')
        except Exception as e:
            log(f'! {label}: {e}')

    log(f'run dir: {run_dir}')
    log(f'phases:  {phases}')

    summary = {'started_utc': utcnow(), 'rws_url': args.rws_url,
               'sample_period_s': args.period, 'phases': []}
    sample_idx = 0
    try:
        for phase_name, phase_dur in phases:
            plan = PHASE_PLAN[phase_name]
            log(f'=== Phase "{phase_name}"  v={plan["v"]}  play={plan["play"]} ===')
            set_velocity(plan['v'])
            trigger(resume_srv if plan['play'] else pause_srv,
                    'resume' if plan['play'] else 'pause')
            time.sleep(1.0)

            phase_start = time.monotonic()
            phase_start_utc = utcnow()
            phase_samples = 0
            e_start = e_last = None
            next_tick = time.monotonic()

            while time.monotonic() - phase_start < phase_dur:
                sample = {
                    'phase': phase_name,
                    'phase_elapsed_s': round(time.monotonic() - phase_start, 3),
                    'sample_utc': utcnow(),
                    'sample_idx': sample_idx,
                }
                for k, p in RWS_TARGETS.items():
                    d = rws_get(p)
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

                fn = samples_dir / f'sample_{sample_idx:06d}_{phase_name}.json'
                fn.write_text(json.dumps(sample, ensure_ascii=False),
                              encoding='utf-8')
                sample_idx += 1
                phase_samples += 1
                if phase_samples % 15 == 0:
                    log(f'  {phase_name}: {phase_samples} samples')

                next_tick += args.period
                sleep_for = next_tick - time.monotonic()
                if sleep_for > 0:
                    time.sleep(sleep_for)
                else:
                    next_tick = time.monotonic()

            delta = (e_last - e_start) if (e_start is not None
                                           and e_last is not None) else None
            summary['phases'].append({
                'name': phase_name, 'planned_s': phase_dur,
                'started_utc': phase_start_utc, 'samples': phase_samples,
                'energy_delta_kwh': delta,
            })
            log(f'   Phase "{phase_name}" fertig — {phase_samples} Samples, '
                f'dE={delta} kWh')
    finally:
        trigger(pause_srv, 'pause (cleanup)')
        summary['finished_utc'] = utcnow()
        summary['total_samples'] = sample_idx
        (run_dir / 'summary.json').write_text(
            json.dumps(summary, indent=2, ensure_ascii=False), encoding='utf-8')
        log(f'summary: {run_dir / "summary.json"}')
        log(f'samples: {sample_idx} in {samples_dir}')
        ros.terminate()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
