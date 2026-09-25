"""tcp_fk against MoveIt /compute_fk (tool0 = link_6 origin) - values recorded on
2026-09-25 with tools/axis-check/fk_ref.py from the running move_group."""
import math

import numpy as np

from tcp_fk import peak_tcp_speed, tcp_position

# (joint angles rad, MoveIt link_6/tool0 position m)
REF = [
    ([0, 0, 0, 0, 0, 0], (0.571, 0.0, 0.899)),
    ([0.4, 0.3, -0.5, 0.6, 0.7, 0.8], (0.641, 0.273, 0.913)),
    ([0.5, 0, 0, 0, 0.3, 0], (0.518, 0.283, 0.866)),
    ([0, 0.5, 0, 0, 0.3, 0], (0.806, 0.0, 0.509)),
    ([0, -0.5, 0, 0, 0.3, 0], (0.23, 0.0, 1.075)),
    ([0, 0, 0.5, 0, 0.3, 0], (0.593, 0.0, 0.563)),
    ([0, 0, 0, 0.5, 0.3, 0], (0.59, -0.022, 0.86)),
    ([0, 0, 0, 0, 0.5, 0], (0.597, 0.0, 0.841)),
    ([0, 0, 0, 0, 1.5708, 0], (0.550, 0.0, 0.718)),
]


def test_matches_moveit_fk():
    for q, ref in REF:
        assert np.allclose(tcp_position(q), ref, atol=2e-3), (q, tcp_position(q), ref)


def test_joint6_does_not_move_tcp():
    a = tcp_position([0.2, 0.3, -0.4, 0.5, 0.6, 0.0])
    b = tcp_position([0.2, 0.3, -0.4, 0.5, 0.6, 1.2])
    assert np.allclose(a, b)


def test_peak_speed_of_j1_rotation():
    # J1 at 1 rad/s with the TCP 0.55 m from the axis (home pose) -> 0.55 m/s
    q0 = [0, 0, 0, 0, math.pi / 2, 0]
    q1 = [1.0, 0, 0, 0, math.pi / 2, 0]
    v = peak_tcp_speed([(0.0, q0), (1.0, q1)])
    assert abs(v - 0.55) < 0.01, v


def test_stretching_time_scales_speed_down():
    q0 = [0, 0, 0, 0, math.pi / 2, 0]
    q1 = [1.0, 0.3, -0.2, 0, math.pi / 2, 0]
    fast = peak_tcp_speed([(0.0, q0), (1.0, q1)])
    slow = peak_tcp_speed([(0.0, q0), (2.0, q1)])
    assert abs(fast / slow - 2.0) < 0.05
