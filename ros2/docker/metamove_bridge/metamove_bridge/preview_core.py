"""Pure math for preview & confirm (no ROS) - see preview_confirm.py, tested in test_preview_core.py.

Relative mapping: when a grab starts we remember the handle pose H0 and the TCP pose T0
(the last preview, or the real TCP). While grabbed, the target is T0 moved by exactly
what the handle did since: position T0.p + (H.p - H0.p), rotation (H.q * H0.q^-1) * T0.q,
all in base_link. So the handle's own position/orientation never matters - only how the
hand moves it - and there is no jump when the grab starts.
"""
from __future__ import annotations

import math

Quat = tuple[float, float, float, float]   # x, y, z, w
Vec = tuple[float, float, float]


def q_mul(a: Quat, b: Quat) -> Quat:
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def q_inv(q: Quat) -> Quat:
    return (-q[0], -q[1], -q[2], q[3])


def q_norm(q: Quat) -> Quat:
    n = math.sqrt(sum(v * v for v in q)) or 1.0
    return tuple(v / n for v in q)  # type: ignore[return-value]


def q_angle_deg(a: Quat, b: Quat) -> float:
    d = abs(sum(x * y for x, y in zip(q_norm(a), q_norm(b))))
    return math.degrees(2 * math.acos(min(1.0, d)))


def relative_target(h0: tuple[Vec, Quat], h: tuple[Vec, Quat], t0: tuple[Vec, Quat]) -> tuple[Vec, Quat]:
    (h0p, h0q), (hp, hq), (t0p, t0q) = h0, h, t0
    p = tuple(t0p[i] + (hp[i] - h0p[i]) for i in range(3))
    q = q_norm(q_mul(q_mul(hq, q_inv(h0q)), t0q))
    return p, q  # type: ignore[return-value]


def clamp_to_box(p: Vec, lo: Vec, hi: Vec) -> tuple[Vec, bool]:
    c = tuple(min(max(p[i], lo[i]), hi[i]) for i in range(3))
    return c, any(abs(c[i] - p[i]) > 1e-9 for i in range(3))  # type: ignore[return-value]


def inside_box(p: Vec, lo: Vec, hi: Vec, tol: float = 1e-3) -> bool:
    return all(lo[i] - tol <= p[i] <= hi[i] + tol for i in range(3))


def max_joint_jump(a: list[float], b: list[float]) -> float:
    return max(abs(x - y) for x, y in zip(a, b))
