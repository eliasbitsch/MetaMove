"""Load a RAPID module from controller HOME into a task via RWS 2.0 (OmniCore/RW7).

The abb-robotstudio MCP tool sends the wrong Content-Type for RW7 — the
controller demands 'application/x-www-form-urlencoded;v=2.0'. This script
speaks RWS 2.0 directly: Basic auth over HTTPS, explicit mastership request,
then loadmod from a file already on the controller filesystem.
"""
from __future__ import annotations
import argparse
import sys

import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

CT = "application/x-www-form-urlencoded;v=2.0"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="https://192.168.125.1:443")
    ap.add_argument("--user", default="Default User")
    ap.add_argument("--pw", default="robotics")
    ap.add_argument("--task", default="T_ROB1")
    ap.add_argument("--modulepath", default="$HOME/MetaMoveJointStream.mod")
    args = ap.parse_args()

    s = requests.Session()
    s.verify = False
    s.auth = (args.user, args.pw)
    s.headers.update({"Accept": "application/hal+json;v=2.0"})

    def show(name, r):
        body = (r.text or "")[:400].replace("\n", " ")
        print(f"{name}: HTTP {r.status_code}  {body}")
        return r

    # 0) sanity: can we read at all?
    show("GET system", s.get(f"{args.base}/rw/system?json=1", timeout=10))

    # 1) request rapid mastership (RWS 2.0 style, then RWS 1.0 fallback)
    r = s.post(f"{args.base}/rw/mastership/edit/request",
               headers={"Content-Type": CT}, data="", timeout=10)
    show("mastership edit/request", r)
    if r.status_code >= 400:
        r = s.post(f"{args.base}/rw/mastership/request",
                   headers={"Content-Type": CT}, data="", timeout=10)
        show("mastership request (fallback)", r)

    # 2) loadmod — try RWS2.0 endpoint, then RWS1.0 action form
    ok = False
    r = s.post(f"{args.base}/rw/rapid/tasks/{args.task}/loadmod",
               headers={"Content-Type": CT},
               data={"modulepath": args.modulepath}, timeout=15)
    show("loadmod (2.0)", r)
    ok = r.status_code in (200, 201, 204)
    if not ok:
        r = s.post(f"{args.base}/rw/rapid/tasks/{args.task}?action=loadmod",
                   headers={"Content-Type": CT},
                   data={"modulepath": args.modulepath}, timeout=15)
        show("loadmod (1.0 action)", r)
        ok = r.status_code in (200, 201, 204)

    # 3) release mastership (best effort)
    for ep in ("/rw/mastership/edit/release", "/rw/mastership/release"):
        rr = s.post(f"{args.base}{ep}", headers={"Content-Type": CT},
                    data="", timeout=10)
        if rr.status_code < 400:
            show("mastership release", rr)
            break

    print("\nRESULT:", "MODULE LOADED" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
