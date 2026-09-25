"""Why is the robot (not) moving? - one answer from the three state reports.

Pure function, no ROS, so every situation that cost time on 2026-09-25 is a unit
test (test_status_core.py). robot_status.py feeds it and publishes the result.

Inputs are the latest JSON reports plus their age in seconds (None = never seen):
  egm      /egm/status          from the Windows EGM bridge
           {"rapid": 1|2|0, "motors": 1|2|0, "armed": bool, "cmd_fresh": bool, "rx_hz": float}
  scaler   /robot/scaler_state  from distance_speed_scaler
           {"mode": "AUTO"|"MANUAL", "override": bool, "dist": float|None, "stale": bool,
            "live_speed": float, "max_speed": float, "d_near": float}
  playback /dpp/state           from dpp_playback
           {"state": "running"|"paused"|"homing"|"at_home"|"stopped", "wp": str}

Output: {"moving": bool, "reason": str, "hint": str, "level": "ok"|"info"|"warn"|"error"}
The first matching rule wins; rules are ordered from "nothing can work" to "fine".
"""
from __future__ import annotations

STALE_S = 1.5          # a report older than this counts as missing
RAPID_RUNNING, RAPID_STOPPED = 2, 1
MOTORS_ON, MOTORS_OFF = 1, 2


def _res(moving: bool, level: str, reason: str, hint: str = "") -> dict:
    return {"moving": moving, "reason": reason, "hint": hint, "level": level}


def decide(egm: dict | None, egm_age: float | None,
           scaler: dict | None, scaler_age: float | None,
           playback: dict | None, playback_age: float | None) -> dict:
    # --- the robot side ------------------------------------------------------
    if egm is None or egm_age is None or egm_age > STALE_S:
        return _res(False, "error", "EGM bridge not running",
                    "start bridge/egm-bridge/egm_bridge_servo.py (metamove_up.ps1 does it)")
    if egm.get("rx_hz", 0.0) < 50.0:
        return _res(False, "error", "no EGM packets from the controller",
                    "check the Ethernet cable / NIC 192.168.125.100 and that MetaJointMain is loaded")
    if egm.get("motors") != MOTORS_ON:
        return _res(False, "warn", "motors off", "Motors On at the pendant")
    if egm.get("rapid") != RAPID_RUNNING:
        return _res(False, "warn", "RAPID stopped",
                    "press Play at the pendant (program pointer on MetaJointMain)")

    # --- the ROS side --------------------------------------------------------
    if scaler is None or scaler_age is None or scaler_age > STALE_S:
        return _res(False, "error", "speed scaler not running", "restart the ROS launch")
    manual = scaler.get("mode") == "MANUAL"
    if not manual and (playback is None or playback_age is None or playback_age > STALE_S):
        return _res(False, "error", "path playback not running", "restart the ROS launch")

    if manual:
        if egm.get("cmd_fresh") and egm.get("armed"):
            return _res(True, "ok", "MANUAL - following your grab")
        return _res(False, "info", "MANUAL - waiting for a grab", "grab the ball at the tool to move the robot")

    pb = playback.get("state")
    if pb == "stopped":
        return _res(False, "warn", "path playback stopped", "restart the ROS launch to replay the path")
    if pb == "homing":
        if egm.get("armed") and egm.get("cmd_fresh"):
            return _res(True, "ok", "driving home")
        return _res(False, "info", "homing - waiting for motion", "")
    if pb == "at_home":
        return _res(False, "info", "at home, path paused", "press Start (console) or Automatik (headset)")

    override = bool(scaler.get("override"))
    if not override and scaler.get("stale"):
        return _res(False, "info", "no headset distance", "put the headset on (or PC test mode in the console)")
    if pb == "paused":
        return _res(False, "info", "path paused", "press Start (console)")

    live = float(scaler.get("live_speed") or 0.0)
    dist = scaler.get("dist")
    if not override and live <= 0.0 and dist is not None and dist <= float(scaler.get("d_near", 0.0)):
        return _res(False, "warn", f"too close ({dist:.2f} m)",
                    f"step back beyond {float(scaler.get('d_near', 0.0)):.1f} m")
    if live <= 0.0:
        return _res(False, "info", "speed 0", "raise max speed or step back")

    if not egm.get("armed"):
        return _res(False, "info", "waiting for a fresh path", "the bridge re-arms on the next planned move")
    if not egm.get("cmd_fresh"):
        return _res(True, "ok", f"at {playback.get('wp', 'waypoint')} (dwell)")
    return _res(True, "ok", f"moving to {playback.get('wp', '?')} at {live * 100:.0f} %")
