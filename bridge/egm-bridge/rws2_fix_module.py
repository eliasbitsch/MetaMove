"""Replace broken MetaMoveJointStream (duplicate main) on the real GoFa.

RWS 2.0 (OmniCore RW7): upload fixed .mod -> unload old module -> load fixed
-> set PP to MetaJointMain. All with the v2.0 content types the controller
demands.
"""
import sys
import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

CT = "application/x-www-form-urlencoded;v=2.0"
BASE = "https://192.168.125.1:443"
MODFILE = r"C:\git\MetaMove\robotstudio\rapid\MetaMoveJointStreamFix.mod"

s = requests.Session()
s.verify = False
s.auth = ("Default User", "robotics")
s.headers.update({"Accept": "application/hal+json;v=2.0"})


def show(name, r):
    print(f"{name}: HTTP {r.status_code}  {(r.text or '')[:200]}")
    return r


with open(MODFILE, encoding="utf-8") as f:
    mod_text = f.read()

# 1) upload with the content type fileservice demands
r = show("upload", s.put(f"{BASE}/fileservice/$HOME/MetaMoveJointStreamFix.mod",
                         headers={"Content-Type": "text/plain;v=2.0"},
                         data=mod_text.encode("utf-8"), timeout=30))
if r.status_code >= 400:
    sys.exit("upload failed")

# 2) mastership (long timeout — controller can be slow to grant)
show("mastership", s.post(f"{BASE}/rw/mastership/edit/request",
                          headers={"Content-Type": CT}, data="", timeout=30))

# 3) unload broken module (may 400 if not loaded — fine)
show("unloadmod", s.post(f"{BASE}/rw/rapid/tasks/T_ROB1/unloadmod",
                         headers={"Content-Type": CT},
                         data={"module": "MetaMoveJointStream"}, timeout=30))

# 4) load fixed module
r = show("loadmod", s.post(f"{BASE}/rw/rapid/tasks/T_ROB1/loadmod",
                           headers={"Content-Type": CT},
                           data={"modulepath": "$HOME/MetaMoveJointStreamFix.mod"},
                           timeout=30))

# 5) PP -> MetaJointMain
r2 = show("set PP", s.post(f"{BASE}/rw/rapid/tasks/T_ROB1/pcp/routine",
                           headers={"Content-Type": CT},
                           data={"routine": "MetaJointMain",
                                 "module": "MetaMoveJointStream",
                                 "userlevel": "FALSE"}, timeout=30))

# 6) release mastership
s.post(f"{BASE}/rw/mastership/edit/release",
       headers={"Content-Type": CT}, data="", timeout=30)

ok = r.status_code in (200, 201, 204) and r2.status_code in (200, 201, 204)
print("\nRESULT:", "MODULE FIXED + PP SET" if ok else "CHECK OUTPUT ABOVE")
sys.exit(0 if ok else 1)
