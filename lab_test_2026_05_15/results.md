# Lab Test Results 2026-05-15

Filled in during/after the lab session.

## Setup

- GoFa IP: `192.168.125.1`
- Windows IP on Service Port LAN: `___`
- RobotWare version on real GoFa: `___`
- Speed override: `___` %
- Time started: `___`
- Time finished: `___`

## Path A — Python RWS Bridge

### Pre-flight checks

- [ ] MetaMoveCorePers.mod loaded on `T_ROB1`
- [ ] PP set to `MetaMoveCorePers/main`
- [ ] Motors On, Status: Running
- [ ] No 41xxx errors in event log
- [ ] Docker container rebuilt with metamove_bridge update
- [ ] `ros2 launch metamove_bridge sim_servo.launch.py rws_ip:=192.168.125.1 rws_port:=443` started ok
- [ ] `/metamove/motion_rate` topic publishes `{hz_in:0, hz_out:0, ...}` (no errors)

### Probe results

```
e2e first motion latency:  ___ ms
joint_state rate:          ___ Hz
joint_state jitter p95:    ___ ms
RWS write rate:            ___ Hz (from /metamove/motion_rate hz_out)
RWS write success rate:    ___ % (hz_out / hz_in)
```

### Observations

- Robot motion smoothness (1=jerky, 5=glass-smooth): `___`
- Audible artifacts (clicking, stuttering): `___`
- Any errors / event log entries: `___`
- Notable behavior: `___`

## Path B — Unity EGM Bridge

### Pre-flight checks

- [ ] MetaMoveCore.mod (EGM version) loaded on `T_ROB1`
- [ ] PP set to `MetaMoveCore/main`
- [ ] Motors On, Status: Running
- [ ] Pendant Operator Messages shows "EGM Connected - calling EGMRunJoint"
- [ ] Unity Editor in Play mode, `EgmClient.Connected` = true
- [ ] Unity Console shows non-zero `_packetsReceived`
- [ ] `ros2 topic hz /joint_states` shows ~50 Hz from container side

### Probe results

```
e2e first motion latency:  ___ ms
joint_state rate:          ___ Hz
joint_state jitter p95:    ___ ms
EGM receive rate (Unity):  ___ Hz (Unity console _hz)
EGM send rate (Unity):     ___ Hz
```

### Observations

- Robot motion smoothness (1-5): `___`
- Audible artifacts: `___`
- Any errors / event log entries: `___`
- Notable behavior: `___`

## Comparison

|  | Path A (RWS) | Path B (EGM) |
|---|---|---|
| e2e first motion (ms) |  |  |
| Sustained command rate (Hz) |  |  |
| Smoothness (1-5) |  |  |
| Setup time (min) |  |  |
| Reliability over 5 min run |  |  |

## Decision

Recommended path for realtime Quest-Teleop: `___`
Recommended path for scripted demos / pick-and-place: `___`
Things to fix before next session: `___`
