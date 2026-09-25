from command_guard import CommandGuard, RAPID_RUNNING, MOTORS_ON

RUN, ON, STOPPED, OFF, UNDEF = RAPID_RUNNING, MOTORS_ON, 1, 2, 0
HOME = [0, 0, 0, 0, 90, 0]


def near(d):
    return [HOME[0] + d] + HOME[1:]


def test_starts_disarmed_and_ignores_far_command():
    g = CommandGuard()
    out, trip = g.decide(RUN, ON, HOME, near(30))
    assert out == HOME and not g.armed and trip is None


def test_arms_on_command_close_to_robot():
    g = CommandGuard()
    out, _ = g.decide(RUN, ON, HOME, near(1.0))
    assert g.armed and out == near(1.0)


def test_rapid_stop_trips_once_and_echoes():
    g = CommandGuard()
    g.decide(RUN, ON, HOME, near(1.0))
    out, trip = g.decide(STOPPED, ON, HOME, near(5))
    assert out == HOME and "not ready" in trip and not g.armed
    _, trip2 = g.decide(STOPPED, ON, HOME, near(5))
    assert trip2 is None  # reported once


def test_stale_far_target_after_restart_does_not_move():
    """The incident: stopped mid-motion, ROS kept going, Play pressed again."""
    g = CommandGuard()
    g.decide(RUN, ON, HOME, near(1.0))
    g.decide(STOPPED, ON, HOME, near(20))          # pendant stop
    out, _ = g.decide(RUN, ON, HOME, near(40))      # Play again, stale target
    assert out == HOME and not g.armed


def test_rearms_after_fresh_plan():
    g = CommandGuard()
    g.decide(RUN, ON, HOME, near(1.0))
    g.decide(STOPPED, ON, HOME, near(20))
    out, _ = g.decide(RUN, ON, HOME, near(0.5))     # new trajectory from current pose
    assert g.armed and out == near(0.5)


def test_jump_while_armed_trips():
    g = CommandGuard()
    g.decide(RUN, ON, HOME, near(1.0))
    out, trip = g.decide(RUN, ON, HOME, near(16))
    assert out == HOME and "away" in trip and not g.armed


def test_normal_tracking_lead_passes():
    g = CommandGuard()
    g.decide(RUN, ON, HOME, near(1.0))
    out, trip = g.decide(RUN, ON, near(3), near(8))  # 5 deg lead
    assert out == near(8) and trip is None


def test_undefined_state_is_not_ready():
    g = CommandGuard()
    out, _ = g.decide(UNDEF, UNDEF, HOME, near(0.5))
    assert out == HOME and not g.armed


def test_motors_off_blocks():
    g = CommandGuard()
    g.decide(RUN, ON, HOME, near(1.0))
    out, trip = g.decide(RUN, OFF, HOME, near(1.0))
    assert out == HOME and trip
