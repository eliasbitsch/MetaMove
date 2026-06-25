"""Playback remote control — pause/resume the waypoint loop, set speed live.

Talks to the running dpp_playback node via rosbridge:
  /dpp_playback/pause | /dpp_playback/resume   (std_srvs/Trigger)
  /dpp_playback/set_parameters  velocity_scaling (live, applies next leg)

Keys:
  p     = PAUSE  (finishes current leg, then holds)
  r     = RESUME
  1-9   = speed 10%..90%
  0     = speed 100%
  ESC   = quit this console (playback keeps running!)
"""
from __future__ import annotations

import msvcrt
import time

import roslibpy


def main() -> int:
    ros = roslibpy.Ros(host="127.0.0.1", port=9090)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        print("rosbridge nicht erreichbar")
        return 1
    print("verbunden — p=Pause  r=Resume  1-9/0=Tempo 10-100%  ESC=Konsole zu")

    pause_srv = roslibpy.Service(ros, "/dpp_playback/pause", "std_srvs/Trigger")
    resume_srv = roslibpy.Service(ros, "/dpp_playback/resume", "std_srvs/Trigger")
    stop_srv = roslibpy.Service(ros, "/jtc_relay/stop_now", "std_srvs/Trigger")
    param_srv = roslibpy.Service(ros, "/dpp_playback/set_parameters",
                                 "rcl_interfaces/srv/SetParameters")

    def set_speed(frac: float) -> None:
        try:
            param_srv.call(roslibpy.ServiceRequest({"parameters": [
                {"name": "velocity_scaling",
                 "value": {"type": 3, "double_value": frac}}]}), timeout=5)
            print(f"-> Tempo {int(frac*100)}% (gilt ab naechstem Waypoint)")
        except Exception as e:  # noqa: BLE001
            print(f"-> Tempo-Set FEHLER: {e}")

    def trigger(srv, label: str) -> None:
        try:
            r = srv.call(roslibpy.ServiceRequest({}), timeout=5)
            print(f"-> {label}: {r.get('message') or 'ok'}")
        except Exception as e:  # noqa: BLE001
            print(f"-> {label} FEHLER: {e}")

    try:
        while True:
            ch = msvcrt.getwch()
            if ch == "\x1b":
                break
            elif ch == " ":
                # SOFORT-STOPP: Playback pausieren + laufende Bahn einfrieren
                trigger(pause_srv, "PAUSE (Playback)")
                trigger(stop_srv, "STOP NOW (Bahn eingefroren)")
            elif ch in ("p", "P"):
                trigger(pause_srv, "PAUSE")
            elif ch in ("r", "R"):
                trigger(resume_srv, "RESUME")
            elif ch == "0":
                set_speed(1.0)
            elif ch.isdigit():
                set_speed(int(ch) / 10.0)
    except KeyboardInterrupt:
        pass
    finally:
        ros.terminate()
        print("Konsole beendet (Playback laeuft weiter).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
