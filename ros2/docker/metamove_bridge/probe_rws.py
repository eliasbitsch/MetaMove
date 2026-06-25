"""Time each RWS endpoint the DPP orchestrator samples — find the slow ones."""
import time
import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
s = requests.Session()
s.auth = ('Default User', 'robotics')
s.verify = False
s.headers.update({'Accept': 'application/hal+json;v=2.0'})
base = 'https://192.168.125.1:443'
targets = {
    'cartesian':    '/rw/motionsystem/mechunits/ROB_1/cartesian',
    'jointtarget':  '/rw/motionsystem/mechunits/ROB_1/jointtarget',
    'motion_err':   '/rw/motionsystem/errorstate',
    'rapid_exec':   '/rw/rapid/execution',
    'panel_speed':  '/rw/panel/speedratio',
    'panel_opmode': '/rw/panel/opmode',
    'panel_ctrl':   '/rw/panel/ctrl-state',
    'energy':       '/rw/system/energy',
}
for rnd in range(2):
    print(f'--- Runde {rnd+1} ---')
    for k, p in targets.items():
        t0 = time.monotonic()
        try:
            r = s.get(base + p, timeout=2.0)
            dt = time.monotonic() - t0
            print(f'{k:14s} HTTP {r.status_code}  {dt*1000:6.0f} ms')
        except Exception as e:  # noqa: BLE001
            dt = time.monotonic() - t0
            print(f'{k:14s} FEHLER {type(e).__name__}  {dt*1000:6.0f} ms')
