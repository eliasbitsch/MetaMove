"""Manual IMMEDIATE speed control — sets jtc_servo_relay.live_speed directly.

Unlike the dpp_playback velocity cap (which only applies at the next waypoint),
this changes the relay's live throttle that is read every 50 Hz tick — so the
robot speeds up / slows / freezes MID-MOTION, instantly. Same knob the distance
scaler drives; use this for manual testing while the scaler is OFF.

Keys (immediate):
  0  = freeze (live_speed 0.0)
  1-9 = 10%..90% of plan speed
  f  = full (1.0 = the 50% plan cap)
  space = freeze
  ESC = quit
"""
from __future__ import annotations

import msvcrt
import time
import roslibpy

RELAY = 'joint_trajectory_controller'


def main() -> int:
    ros = roslibpy.Ros(host='127.0.0.1', port=9090)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        print('rosbridge nicht erreichbar'); return 1
    srv = roslibpy.Service(ros, f'/{RELAY}/set_parameters',
                           'rcl_interfaces/srv/SetParameters')
    home_srv = roslibpy.Service(ros, '/dpp_playback/home',
                                'std_srvs/srv/Trigger')

    resume_srv = roslibpy.Service(ros, '/dpp_playback/resume',
                                  'std_srvs/srv/Trigger')
    pause_srv = roslibpy.Service(ros, '/dpp_playback/pause',
                                 'std_srvs/srv/Trigger')

    def go_home() -> None:
        try:
            home_srv.call(roslibpy.ServiceRequest({}), timeout=15)
            print('-> HOME: stoppt, fährt zu Home [0,0,0,0,90,0], bleibt stehen')
        except Exception as e:  # noqa: BLE001
            print(f'-> HOME Fehler: {e}')

    def _trigger(srv) -> None:
        try:
            srv.call(roslibpy.ServiceRequest({}), timeout=5)
        except Exception as e:  # noqa: BLE001
            print(f'  (service Fehler: {e})')

    def set_speed(v: float) -> None:
        v = max(0.0, min(1.0, v))
        try:
            srv.call(roslibpy.ServiceRequest({'parameters': [
                {'name': 'live_speed',
                 'value': {'type': 3, 'double_value': v}}]}), timeout=5)
            print(f'-> live_speed = {v:.2f}  (sofort)')
        except Exception as e:  # noqa: BLE001
            print(f'-> FEHLER: {e}')

    print('verbunden. 1-9=Tempo(+Resume) f=voll 0/space=Freeze(+Pause) '
          'H=Home(stop+park) ESC=quit')
    try:
        while True:
            ch = msvcrt.getwch()
            if ch == '\x1b':
                break
            elif ch in ('h', 'H'):
                go_home()
            elif ch in (' ', '0'):
                set_speed(0.0)
                _trigger(pause_srv)        # sauberer Freeze: Loop pausieren
            elif ch in ('f', 'F'):
                _trigger(resume_srv)        # Zahl/f = weiterfahren
                set_speed(1.0)
            elif ch.isdigit():
                _trigger(resume_srv)        # Zahl = Resume + Tempo
                set_speed(int(ch) / 10.0)
    except KeyboardInterrupt:
        pass
    finally:
        ros.terminate()
        print('Konsole beendet.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
