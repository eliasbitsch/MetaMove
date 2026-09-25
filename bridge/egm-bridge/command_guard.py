"""Decides per EGM cycle whether a ROS joint command may reach the robot.

The bridge used to forward any fresh /servo_node/commands target. If RAPID was
stopped at the pendant mid-motion, ROS kept streaming the trajectory; on the
next Play, EGM saw a target far from the arm and drove there at full EGM
dynamics. This guard closes that:

  * commands only pass while RAPID is RUNNING and motors are ON (UNDEFINED
    counts as not ready - fail safe);
  * after any trip, and at bridge start, the guard is DISARMED and only re-arms
    on a command within ``arm_tol_deg`` of the measured pose - a freshly
    planned trajectory or a freshly seeded grab, never a stale far target;
  * while armed, a command more than ``jump_tol_deg`` from the measured pose
    trips it.

Disarmed = echo the measured pose, so the robot holds still.
"""
from __future__ import annotations

RAPID_RUNNING = 2   # egm_pb2.EgmRobot.rapidExecState
MOTORS_ON = 1       # egm_pb2.EgmRobot.motorState


class CommandGuard:
    def __init__(self, arm_tol_deg: float = 2.0, jump_tol_deg: float = 15.0) -> None:
        self.arm_tol_deg = arm_tol_deg
        self.jump_tol_deg = jump_tol_deg
        self.armed = False
        self.last_trip: str | None = None

    def decide(self, rapid_state: int, motor_state: int,
               feedback_deg: list[float], command_deg: list[float] | None
               ) -> tuple[list[float], str | None]:
        """Return (joints to send, trip reason or None). Trip reason is only
        returned on the transition armed -> disarmed, so callers act once."""
        ready = rapid_state == RAPID_RUNNING and motor_state == MOTORS_ON
        if not ready:
            return feedback_deg, self._trip(
                f"robot not ready (rapidExecState={rapid_state}, motorState={motor_state})")
        if command_deg is None:
            return feedback_deg, None
        dev = max(abs(c - f) for c, f in zip(command_deg, feedback_deg))
        if not self.armed:
            if dev <= self.arm_tol_deg:
                self.armed = True
                self.last_trip = None
                return command_deg, None
            return feedback_deg, None
        if dev > self.jump_tol_deg:
            return feedback_deg, self._trip(f"command {dev:.1f} deg away from the robot")
        return command_deg, None

    def _trip(self, reason: str) -> str | None:
        was = self.armed
        self.armed = False
        self.last_trip = reason
        return reason if was else None
