"""Closed-form forward kinematics of the GoFa CRB 15000 (tool0) and TCP speed.

Chain from the URDF the stack loads (abb_crb15000_support, crb15000_5_95): every
joint origin has rpy = 0, so each step is a translation followed by a rotation
about a principal axis. tool0 sits at link_6's origin (flange-tool0 only rotates),
so joint 6 never moves the TCP point. Checked against MoveIt /compute_fk in
test_tcp_fk.py.

Pure numpy - microseconds per pose, so jtc_servo_relay can sample a whole
trajectory before replaying it without calling /compute_fk hundreds of times.
"""
from __future__ import annotations

import math

import numpy as np

# (translation from parent [m], rotation axis) for joint_1..joint_6
_CHAIN = [
    ((0.0, 0.0, 0.265), "z"),
    ((0.0, 0.0, 0.0), "y"),
    ((0.0, 0.0, 0.444), "y"),
    ((0.0, 0.0, 0.110), "x"),
    ((0.470, 0.0, 0.0), "y"),
    ((0.101, 0.0, 0.080), "x"),
]


def _rot(axis: str, q: float) -> np.ndarray:
    c, s = math.cos(q), math.sin(q)
    if axis == "x":
        return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == "y":
        return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def tcp_position(q: list[float]) -> np.ndarray:
    """tool0 position in base_link [m] for joint angles q (rad, joint_1..6)."""
    p = np.zeros(3)
    r = np.eye(3)
    for (t, axis), qi in zip(_CHAIN, q):
        p = p + r @ np.asarray(t)
        r = r @ _rot(axis, qi)
    return p


def peak_tcp_speed(keys: list[tuple[float, list[float]]], dt: float = 0.004) -> float:
    """Peak Cartesian TCP speed [m/s] of a joint trajectory given as (t, q) keyframes,
    linearly interpolated in joint space - the way jtc_servo_relay replays it."""
    if len(keys) < 2 or keys[-1][0] <= 0:
        return 0.0
    peak, prev, k, t = 0.0, None, 0, 0.0
    end = keys[-1][0]
    while t <= end + 1e-9:
        while k + 1 < len(keys) - 1 and keys[k + 1][0] <= t:
            k += 1
        (t0, q0), (t1, q1) = keys[k], keys[k + 1]
        a = 0.0 if t1 <= t0 else min(1.0, max(0.0, (t - t0) / (t1 - t0)))
        p = tcp_position([x0 + a * (x1 - x0) for x0, x1 in zip(q0, q1)])
        if prev is not None:
            peak = max(peak, float(np.linalg.norm(p - prev)) / dt)
        prev = p
        t += dt
    return peak
