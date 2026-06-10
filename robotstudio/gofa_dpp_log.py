"""
DPP runtime logger — 1 Hz RWS snapshots tagged with the current lab phase.

Pure RWS, no ROS dependency. Runs on the lab PC, samples once per second,
writes one slim JSON per sample plus a summary.json at the end.

Use case: companion to ros2/docker/metamove_bridge/dpp_playback.py during lab
runs. The bridge drives the robot through waypoints via MoveIt; this script
captures cartesian pose, joint state, exec/motion state, speed override, and
energy counters every second. Per-phase energy delta + sample count go into
summary.json — that's the data Alexandra needs for the DPP OperationalData
submodel.

Usage:
    python gofa_dpp_log.py --phases normal:300,move:300,fast:120,stop:60
    python gofa_dpp_log.py --phases normal:300,move:300,fast:120,stop:60 lab
    python gofa_dpp_log.py --phases test:30 alt

  • Hit <Enter> in the terminal to skip ahead to the next phase.
  • Hit Ctrl-C to abort; summary.json is still written.

Env:
    GOFA_USER / GOFA_PASS  (default "Default User" / "robotics")
    GOFA_THROTTLE=0.0      (no throttle — we need 1 Hz)
"""
from __future__ import annotations

import argparse
import json
import os
import select
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import requests
from requests.auth import HTTPBasicAuth
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


PRESETS = {
    'lab':   'https://192.168.125.1:443',
    'alt':   'https://192.168.125.99:443',
    'local': 'http://localhost:80',
}


def parse_phases(spec: str) -> list[tuple[str, int]]:
    out: list[tuple[str, int]] = []
    for chunk in spec.split(','):
        chunk = chunk.strip()
        if not chunk:
            continue
        if ':' not in chunk:
            raise ValueError(f'bad phase spec "{chunk}" — expected name:seconds')
        name, dur = chunk.split(':', 1)
        out.append((name.strip(), int(dur)))
    if not out:
        raise ValueError('no phases')
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument('target', nargs='?', default='lab',
                    help='lab|alt|local|<full url>  (default: lab)')
    ap.add_argument('--phases', required=True,
                    help='comma list of name:seconds, e.g. normal:300,move:300,fast:120,stop:60')
    ap.add_argument('--out-dir', default=None,
                    help='output directory (default: lab_test_<stamp>/dpp_samples)')
    ap.add_argument('--period', type=float, default=1.0, help='sample period seconds')
    args = ap.parse_args()

    url = PRESETS.get(args.target, args.target)
    phases = parse_phases(args.phases)
    period = args.period

    user = os.environ.get('GOFA_USER', 'Default User')
    pwd = os.environ.get('GOFA_PASS', 'robotics')

    stamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    here = Path(__file__).resolve().parent
    out_dir = Path(args.out_dir) if args.out_dir else here / f'lab_test_{stamp}' / 'dpp_samples'
    out_dir.mkdir(parents=True, exist_ok=True)
    summary_path = out_dir.parent / 'summary.json'

    sess = requests.Session()
    sess.auth = HTTPBasicAuth(user, pwd)
    sess.verify = False
    sess.headers.update({'Accept': 'application/hal+json;v=2.0'})

    def get(path: str) -> dict | None:
        try:
            r = sess.get(f'{url}{path}', timeout=2.0)
            if r.status_code != 200:
                return None
            return r.json()
        except Exception:
            return None

    # Endpoints sampled every tick. Kept small so each JSON stays a few KB.
    targets = {
        'cartesian':      '/rw/motionsystem/mechunits/ROB_1/cartesian',
        'jointtarget':    '/rw/motionsystem/mechunits/ROB_1/jointtarget',
        'motion_err':     '/rw/motionsystem/errorstate',
        'rapid_exec':     '/rw/rapid/execution',
        'panel_speed':    '/rw/panel/speedratio',
        'panel_opmode':   '/rw/panel/opmode',
        'panel_ctrl':     '/rw/panel/ctrl-state',
        'energy':         '/rw/system/energy',
    }

    print(f'target:   {url}')
    print(f'out dir:  {out_dir}')
    print(f'phases:   {phases}')
    print(f'period:   {period}s')
    print()
    print('Press <Enter> to skip current phase. Ctrl-C to abort.')
    print()

    summary: dict = {
        'target': url,
        'started_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
        'sample_period_s': period,
        'phases': [],
    }
    sample_idx = 0
    aborted = False
    try:
        for phase_name, phase_dur in phases:
            phase_start = time.monotonic()
            phase_start_utc = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
            phase_samples = 0
            energy_start: float | None = None
            energy_last: float | None = None
            print(f'== phase "{phase_name}" — {phase_dur}s ==')

            next_tick = time.monotonic()
            while time.monotonic() - phase_start < phase_dur:
                # Sample
                sample: dict = {
                    'phase': phase_name,
                    'phase_elapsed_s': round(time.monotonic() - phase_start, 3),
                    'sample_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                    'sample_idx': sample_idx,
                }
                for key, path in targets.items():
                    d = get(path)
                    if d is not None:
                        sample[key] = d

                # Pull cumulative energy for delta-tracking
                if 'energy' in sample:
                    for s in sample['energy'].get('state', []):
                        if s.get('_type') == 'sys-energy-state':
                            try:
                                kwh = float(s.get('accumulated-energy', 0))
                                if energy_start is None:
                                    energy_start = kwh
                                energy_last = kwh
                            except Exception:
                                pass
                            break

                fname = out_dir / f'sample_{sample_idx:06d}_{phase_name}.json'
                fname.write_text(json.dumps(sample, ensure_ascii=False))
                sample_idx += 1
                phase_samples += 1

                # Tight loop with skip-on-Enter
                next_tick += period
                while True:
                    remaining = next_tick - time.monotonic()
                    if remaining <= 0:
                        break
                    rlist, _, _ = select.select([sys.stdin], [], [], min(remaining, 0.2))
                    if rlist:
                        sys.stdin.readline()
                        print(f'  [skip] cutting phase "{phase_name}" short '
                              f'at {time.monotonic() - phase_start:.1f}s '
                              f'(samples: {phase_samples})')
                        break
                else:
                    continue
                break  # <Enter> pressed → next phase

            summary['phases'].append({
                'name': phase_name,
                'planned_s': phase_dur,
                'actual_s': round(time.monotonic() - phase_start, 2),
                'started_utc': phase_start_utc,
                'finished_utc': datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z'),
                'samples': phase_samples,
                'energy_kwh_start': energy_start,
                'energy_kwh_end': energy_last,
                'energy_kwh_delta':
                    (energy_last - energy_start) if (energy_start is not None and energy_last is not None) else None,
            })
            print(f'   done — {phase_samples} samples, '
                  f'ΔE = {summary["phases"][-1]["energy_kwh_delta"]} kWh')
    except KeyboardInterrupt:
        aborted = True
        print('\n[abort] writing summary anyway…')

    summary['finished_utc'] = datetime.now(timezone.utc).isoformat().replace('+00:00', 'Z')
    summary['total_samples'] = sample_idx
    summary['aborted'] = aborted
    summary_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
    print(f'\nsummary: {summary_path}')
    print(f'samples: {sample_idx} in {out_dir}')


if __name__ == '__main__':
    main()
