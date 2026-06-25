"""Unified control console — toggle MANUAL vs QUEST(distance) speed control.

One window. Avoids the scaler and manual control fighting over live_speed by
making it an explicit mode switch:

  q  = QUEST mode  -> distance_speed_scaler owns live_speed (distance-based)
  m  = MANUAL mode -> scaler off; YOU set live_speed with the number keys

Manual-mode keys (ignored in Quest mode):
  0 / space = freeze    1-9 = 10..90%    f = full

  space (any mode) = EMERGENCY: switch to manual + freeze immediately
  ESC = quit console (robot keeps its last state)
"""
from __future__ import annotations

import msvcrt
import time
import roslibpy

RELAY = 'joint_trajectory_controller'
SCALER = 'distance_speed_scaler'


def main() -> int:
    ros = roslibpy.Ros(host='127.0.0.1', port=9090)
    ros.run()
    t0 = time.monotonic()
    while not ros.is_connected and time.monotonic() - t0 < 10:
        time.sleep(0.1)
    if not ros.is_connected:
        print('rosbridge nicht erreichbar'); return 1

    relay_srv = roslibpy.Service(ros, f'/{RELAY}/set_parameters',
                                 'rcl_interfaces/srv/SetParameters')
    scaler_srv = roslibpy.Service(ros, f'/{SCALER}/set_parameters',
                                  'rcl_interfaces/srv/SetParameters')

    def set_scaler_enabled(on: bool) -> None:
        try:
            scaler_srv.call(roslibpy.ServiceRequest({'parameters': [
                {'name': 'enabled', 'value': {'type': 1, 'bool_value': on}}]}),
                timeout=5)
        except Exception as e:  # noqa: BLE001
            print(f'  (Skalierer-Schalter Fehler: {e})')

    def set_live_speed(v: float) -> None:
        v = max(0.0, min(1.0, v))
        try:
            relay_srv.call(roslibpy.ServiceRequest({'parameters': [
                {'name': 'live_speed', 'value': {'type': 3, 'double_value': v}}]}),
                timeout=5)
            print(f'  live_speed = {v:.2f}')
        except Exception as e:  # noqa: BLE001
            print(f'  (Fehler: {e})')

    mode = None

    def go_quest():
        nonlocal mode
        mode = 'QUEST'
        set_scaler_enabled(True)
        print('>>> QUEST-Modus: Distanz steuert das Tempo')

    def go_manual():
        nonlocal mode
        mode = 'MANUAL'
        set_scaler_enabled(False)
        print('>>> MANUELL: Zahlen 1-9 / f / 0 steuern das Tempo')

    go_manual()  # start safe in manual, frozen
    set_live_speed(0.0)
    print('--- q=QUEST  m=MANUELL  1-9/f/0=Tempo(manuell)  space=NOT-STOPP  ESC=quit ---')

    try:
        while True:
            ch = msvcrt.getwch()
            if ch == '\x1b':
                break
            elif ch in ('q', 'Q'):
                go_quest()
            elif ch in ('m', 'M'):
                go_manual()
            elif ch == ' ':
                go_manual(); set_live_speed(0.0); print('  [NOT-STOPP]')
            elif mode == 'MANUAL' and ch == '0':
                set_live_speed(0.0)
            elif mode == 'MANUAL' and ch in ('f', 'F'):
                set_live_speed(1.0)
            elif mode == 'MANUAL' and ch.isdigit():
                set_live_speed(int(ch) / 10.0)
            elif ch.isdigit() or ch in ('f', 'F'):
                print('  (Tempo-Tasten nur im MANUELL-Modus — erst m druecken)')
    except KeyboardInterrupt:
        pass
    finally:
        ros.terminate()
        print('Konsole beendet.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
