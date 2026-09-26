import math

from preview_core import clamp_to_box, inside_box, q_angle_deg, relative_target

I = (0.0, 0.0, 0.0, 1.0)


def yaw(deg):
    h = math.radians(deg) / 2
    return (0.0, 0.0, math.sin(h), math.cos(h))


TCP0 = ((0.55, 0.0, 0.72), (0.0, 0.7071068, 0.0, 0.7071068))   # tool pointing down


def test_no_handle_motion_means_no_target_motion():
    # The handle's own pose is arbitrary - only its motion counts: no jump at grab start.
    h0 = ((1.2, -3.0, 0.4), yaw(73))
    p, q = relative_target(h0, h0, TCP0)
    assert all(abs(a - b) < 1e-9 for a, b in zip(p, TCP0[0]))
    assert q_angle_deg(q, TCP0[1]) < 1e-6


def test_translation_is_applied_one_to_one():
    h0 = ((0.0, 0.0, 0.0), I)
    p, _ = relative_target(h0, ((0.05, -0.02, 0.10), I), TCP0)
    assert abs(p[0] - 0.60) < 1e-9 and abs(p[1] + 0.02) < 1e-9 and abs(p[2] - 0.82) < 1e-9


def test_rotation_is_applied_in_base_frame():
    h0 = ((0.0, 0.0, 0.0), yaw(30))
    p, q = relative_target(h0, ((0.0, 0.0, 0.0), yaw(45)), TCP0)
    # handle turned 15 deg about z -> tool turned 15 deg about base z
    assert abs(q_angle_deg(q, TCP0[1]) - 15.0) < 1e-6
    assert p == TCP0[0]


def test_yaw_only_ignores_tilt():
    tilt = (math.sin(math.radians(20) / 2), 0.0, 0.0, math.cos(math.radians(20) / 2))   # 20 deg about x
    _, q = relative_target(((0, 0, 0), I), ((0, 0, 0), tilt), TCP0)
    assert q_angle_deg(q, TCP0[1]) < 1e-6
    _, q = relative_target(((0, 0, 0), I), ((0, 0, 0), tilt), TCP0, yaw_only=False)
    assert abs(q_angle_deg(q, TCP0[1]) - 20.0) < 1e-6


def test_clamp_reports_and_limits():
    lo, hi = (-0.445, -0.623, 0.381), (0.660, 0.292, 0.869)
    c, clamped = clamp_to_box((0.5, -0.3, 0.2), lo, hi)
    assert clamped and c[2] == 0.381 and c[0] == 0.5
    c, clamped = clamp_to_box((0.5, -0.3, 0.6), lo, hi)
    assert not clamped and inside_box(c, lo, hi)
