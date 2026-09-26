"""Simulated OmniCore EGM channel for the end-to-end tests - the controller side of the chain.

Plays what the real controller does in MetaJointMain: sends EgmRobot at 250 Hz to the EGM
bridge (joint feedback + motor/RAPID state) and follows the planned joints in the bridge's
EgmSensor replies, limited to a joint speed like the real arm. Not a dynamics model -
enough to close the loop so every stage upstream runs exactly as on the robot.

Control/inspection over a local UDP port (JSON in, JSON out), used by the tests:
  {"cmd": "status"}                                   -> joints, rx/tx counts, max jumps
  {"cmd": "set", "rapid": "running"|"stopped", "motors": "on"|"off", "egm": true|false,
   "joints": [6 x deg]}                               (joints: teleport, like a pendant jog)
  {"cmd": "reset_stats"}

Recorded for the safety assertions: the largest single-tick jump the bridge ever
commanded (target vs. current joints) - a guard or planner fault shows up there.
"""
from __future__ import annotations

import argparse
import json
import socket
import sys
import threading
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "bridge" / "egm-mock"))
import egm_pb2  # noqa: E402

HOME_DEG = [0.0, 0.0, 0.0, 0.0, 90.0, 0.0]


class ControllerSim:
    def __init__(self, bridge: tuple[str, int], ctl_port: int, max_joint_speed_dps: float) -> None:
        self.bridge = bridge
        self.max_step = max_joint_speed_dps / 250.0
        self.joints = list(HOME_DEG)
        self.target: list[float] | None = None
        self.rapid_running = True
        self.motors_on = True
        self.egm_on = True
        self.tx = self.rx = 0
        self.max_cmd_jump = 0.0      # largest |planned - current| seen in one packet (deg)
        self.lock = threading.Lock()
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.sock.bind(("127.0.0.1", 0))
        self.sock.setblocking(False)
        self.ctl = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.ctl.bind(("127.0.0.1", ctl_port))

    # --- EGM side -------------------------------------------------------------------
    def _robot_msg(self, seq: int) -> bytes:
        m = egm_pb2.EgmRobot()
        m.header.seqno = seq
        m.header.tm = int(time.time() * 1000) & 0xFFFFFFFF
        m.header.mtype = egm_pb2.EgmHeader.MSGTYPE_DATA
        with self.lock:
            m.feedBack.joints.joints.extend(self.joints)
            m.planned.joints.joints.extend(self.joints)
            m.motorState.state = (egm_pb2.EgmMotorState.MOTORS_ON if self.motors_on
                                  else egm_pb2.EgmMotorState.MOTORS_OFF)
            m.rapidExecState.state = (egm_pb2.EgmRapidCtrlExecState.RAPID_RUNNING if self.rapid_running
                                      else egm_pb2.EgmRapidCtrlExecState.RAPID_STOPPED)
        return m.SerializeToString()

    def egm_loop(self) -> None:
        period, seq, nxt = 1.0 / 250.0, 0, time.monotonic()
        while True:
            with self.lock:
                active = self.egm_on
            if active:
                self.sock.sendto(self._robot_msg(seq), self.bridge)
                seq += 1
                self.tx += 1
            # drain replies
            while True:
                try:
                    data, _ = self.sock.recvfrom(4096)
                except (BlockingIOError, OSError):
                    break
                s = egm_pb2.EgmSensor()
                try:
                    s.ParseFromString(data)
                except Exception:  # noqa: BLE001
                    continue
                self.rx += 1
                planned = list(s.planned.joints.joints)
                with self.lock:
                    if len(planned) == 6:
                        jump = max(abs(a - b) for a, b in zip(planned, self.joints))
                        self.max_cmd_jump = max(self.max_cmd_jump, jump)
                        self.target = planned
                    else:
                        self.target = None       # empty correction = hold
            # follow the target like the arm would (only with RAPID running + motors on)
            with self.lock:
                if self.target and self.rapid_running and self.motors_on and self.egm_on:
                    self.joints = [c + max(-self.max_step, min(self.max_step, t - c))
                                   for c, t in zip(self.joints, self.target)]
            nxt += period
            d = nxt - time.monotonic()
            if d > 0:
                time.sleep(d)
            else:
                nxt = time.monotonic()

    # --- control side ---------------------------------------------------------------
    def ctl_loop(self) -> None:
        while True:
            data, addr = self.ctl.recvfrom(4096)
            try:
                req = json.loads(data.decode())
            except ValueError:
                continue
            with self.lock:
                if req.get("cmd") == "set":
                    if "rapid" in req:
                        self.rapid_running = req["rapid"] == "running"
                    if "motors" in req:
                        self.motors_on = req["motors"] == "on"
                    if "egm" in req:
                        self.egm_on = bool(req["egm"])
                    if "joints" in req and len(req["joints"]) == 6:
                        self.joints = [float(v) for v in req["joints"]]
                        self.target = None
                elif req.get("cmd") == "reset_stats":
                    self.max_cmd_jump = 0.0
                reply = {"joints": self.joints, "rapid_running": self.rapid_running,
                         "motors_on": self.motors_on, "egm_on": self.egm_on,
                         "tx": self.tx, "rx": self.rx, "max_cmd_jump_deg": self.max_cmd_jump}
            self.ctl.sendto(json.dumps(reply).encode(), addr)


def query(req: dict, port: int = 6600, timeout: float = 2.0) -> dict:
    """Test helper: one request to a running simulator."""
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    s.settimeout(timeout)
    try:
        s.sendto(json.dumps(req).encode(), ("127.0.0.1", port))
        return json.loads(s.recvfrom(65536)[0].decode())
    finally:
        s.close()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--bridge-port", type=int, default=6515)
    ap.add_argument("--ctl-port", type=int, default=6600)
    ap.add_argument("--max-joint-speed", type=float, default=120.0, help="deg/s per joint")
    a = ap.parse_args()
    sim = ControllerSim(("127.0.0.1", a.bridge_port), a.ctl_port, a.max_joint_speed)
    threading.Thread(target=sim.ctl_loop, daemon=True).start()
    print(f"[sim] EGM -> 127.0.0.1:{a.bridge_port}, control on :{a.ctl_port}, home {HOME_DEG}", flush=True)
    sim.egm_loop()
    return 0


if __name__ == "__main__":
    sys.exit(main())
