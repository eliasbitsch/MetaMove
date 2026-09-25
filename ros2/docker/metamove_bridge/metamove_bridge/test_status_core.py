"""Every "why isn't it moving?" moment from the 2026-09-25 lab session as a case."""
from status_core import decide

EGM_OK = {"rapid": 2, "motors": 1, "armed": True, "cmd_fresh": True, "rx_hz": 250.0}
SC_AUTO = {"mode": "AUTO", "override": False, "dist": 1.8, "stale": False,
           "live_speed": 0.5, "max_speed": 1.0, "d_near": 1.1}
PB_RUN = {"state": "running", "wp": "wp_03"}


def run(egm=EGM_OK, scaler=SC_AUTO, pb=PB_RUN, ages=(0.1, 0.1, 0.1)):
    return decide(egm, ages[0], scaler, ages[1], pb, ages[2])


def test_happy_path_moves():
    r = run()
    assert r["moving"] and "wp_03" in r["reason"] and "50 %" in r["reason"]


def test_bridge_missing():
    assert run(egm=None)["reason"] == "EGM bridge not running"
    assert run(ages=(5.0, 0.1, 0.1))["reason"] == "EGM bridge not running"


def test_no_packets_from_controller():
    r = run(egm={**EGM_OK, "rx_hz": 0.0})
    assert "no EGM packets" in r["reason"] and "MetaJointMain" in r["hint"]


def test_bridge_alive_but_controller_silent_is_not_bridge_down():
    # The bridge reports rx 0 on a socket timeout; that must not read as "bridge down".
    silent = {"rapid": 0, "motors": 0, "armed": False, "cmd_fresh": False, "rx_hz": 0.0}
    assert run(egm=silent)["reason"] == "no EGM packets from the controller"


def test_rapid_stopped_is_the_first_answer():
    # The session's most common confusion: headset test while RAPID was stopped.
    r = run(egm={**EGM_OK, "rapid": 1, "armed": False})
    assert not r["moving"] and r["reason"] == "RAPID stopped" and "Play" in r["hint"]


def test_motors_off():
    assert run(egm={**EGM_OK, "motors": 2})["reason"] == "motors off"


def test_at_home_needs_start():
    r = run(pb={"state": "at_home", "wp": "HOME"})
    assert not r["moving"] and "Start" in r["hint"]


def test_headset_off():
    r = run(scaler={**SC_AUTO, "stale": True, "dist": None, "live_speed": 0.0})
    assert r["reason"] == "no headset distance"


def test_pc_override_ignores_stale_distance():
    r = run(scaler={**SC_AUTO, "override": True, "stale": True, "dist": None})
    assert r["moving"]


def test_too_close():
    r = run(scaler={**SC_AUTO, "dist": 1.05, "live_speed": 0.0})
    assert not r["moving"] and r["reason"].startswith("too close") and "1.1" in r["hint"]


def test_waiting_for_rearm_after_stop():
    r = run(egm={**EGM_OK, "armed": False})
    assert not r["moving"] and "fresh path" in r["reason"]


def test_manual_modes():
    man = {**SC_AUTO, "mode": "MANUAL"}
    assert run(scaler=man, pb=None)["moving"]
    r = run(scaler=man, egm={**EGM_OK, "cmd_fresh": False}, pb=None)
    assert not r["moving"] and "grab" in r["hint"]


def test_scaler_or_playback_missing():
    assert run(scaler=None)["reason"] == "speed scaler not running"
    assert run(pb=None)["reason"] == "path playback not running"
