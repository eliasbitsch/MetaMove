"""Minimal RWS 2.0 client for the GoFa (OmniCore) - ONE session, reused.

Every request without the session cookie opens a new RWS session and the controller
allows 70; a 3 req/s poll loop filled them in ~20 s on 2026-09-25 and locked RWS
for everyone until they timed out. This client keeps one cookie jar for its lifetime.
Default User can read everything used here and, in AUTO with the program stopped,
set the program pointer (not in manual: 403).
"""
from __future__ import annotations

import json
import ssl
import urllib.parse
import urllib.request
from http.cookiejar import CookieJar

ACCEPT = {"Accept": "application/hal+json;v=2.0"}
FORM = {"Content-Type": "application/x-www-form-urlencoded;v=2.0"}


class Rws:
    def __init__(self, host: str = "https://192.168.125.1", user: str = "Default User",
                 password: str = "robotics", timeout: float = 5.0) -> None:
        self.host, self.timeout = host, timeout
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        pw = urllib.request.HTTPPasswordMgrWithDefaultRealm()
        pw.add_password(None, host, user, password)
        self._op = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=ctx),
            urllib.request.HTTPDigestAuthHandler(pw),
            urllib.request.HTTPBasicAuthHandler(pw),
            urllib.request.HTTPCookieProcessor(CookieJar()),
        )

    def get(self, path: str) -> dict:
        req = urllib.request.Request(self.host + path, headers=ACCEPT)
        with self._op.open(req, timeout=self.timeout) as r:
            return json.loads(r.read().decode())

    def post(self, path: str, data: dict | None = None) -> int:
        body = urllib.parse.urlencode(data or {}).encode()
        req = urllib.request.Request(self.host + path, data=body, headers={**ACCEPT, **FORM}, method="POST")
        try:
            with self._op.open(req, timeout=self.timeout) as r:
                return r.status
        except urllib.error.HTTPError as e:
            return e.code

    def state(self) -> dict:
        """Controller state, operating mode, execution state and program pointer."""
        pp = self.get("/rw/rapid/tasks/T_ROB1/pcp")["state"][0]
        return {
            "ctrl": self.get("/rw/panel/ctrl-state")["state"][0]["ctrlstate"],
            "opmode": self.get("/rw/panel/opmode")["state"][0]["opmode"],
            "exec": self.get("/rw/rapid/execution")["state"][0]["ctrlexecstate"],
            "module": pp.get("modulemame", ""),
            "routine": pp.get("routinename", ""),
        }

    def set_pp(self, routine: str = "MetaJointMain") -> str:
        """Program pointer to a routine. Only in AUTO with RAPID stopped."""
        st = self.state()
        if st["opmode"] != "AUTO" or st["exec"] != "stopped":
            return f"not allowed now ({st['opmode']}, RAPID {st['exec']}) - AUTO + stopped needed"
        codes = [self.post("/rw/mastership/edit/request")]
        try:
            codes.append(self.post("/rw/rapid/tasks/T_ROB1/pcp/routine",
                                   {"routine": routine, "userlevel": "FALSE"}))
        finally:
            codes.append(self.post("/rw/mastership/edit/release"))
        after = self.state()
        return (f"PP -> {after['module']}/{after['routine']}"
                if after["routine"] == routine else f"failed (HTTP {codes})")
