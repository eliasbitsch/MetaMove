"""
Snapshot everything we need from the real GoFa at 192.168.125.1 for local
replication on a Virtual Controller.

Uses requests.Session so we open exactly ONE RWS session and reuse it for
every call (OmniCore has a 70-session pool limit).

Usage (PowerShell or WSL):
    python gofa_snapshot.py                    # default https://192.168.125.1:443, all
    python gofa_snapshot.py lab cfg            # only CFG domains
    python gofa_snapshot.py https://10.0.0.5:443 rapid
    python gofa_snapshot.py local all          # http://localhost:80

Env:
    GOFA_USER / GOFA_PASS  (default "Default User" / "robotics")
    GOFA_THROTTLE=0.1      (delay between requests, default 0.1s)
"""
from __future__ import annotations

import json
import os
import sys
import time
from base64 import b64encode
from datetime import datetime
from pathlib import Path

import requests
from requests.auth import HTTPBasicAuth
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)


PRESETS = {
    "lab": "https://192.168.125.1:443",
    "alt": "https://192.168.125.99:443",
    "local": "http://localhost:80",
}

arg = sys.argv[1] if len(sys.argv) > 1 else "lab"
URL = PRESETS.get(arg, arg)
SECTION = sys.argv[2] if len(sys.argv) > 2 else "all"

USER = os.environ.get("GOFA_USER", "Default User")
PASS = os.environ.get("GOFA_PASS", "robotics")
THROTTLE_S = float(os.environ.get("GOFA_THROTTLE", "0.1"))
MAX_RETRIES = 4

HERE = Path(__file__).resolve().parent
OUT_OVERRIDE = os.environ.get("GOFA_SNAPSHOT_DIR")
if OUT_OVERRIDE:
    OUT = Path(OUT_OVERRIDE)
else:
    STAMP = datetime.now().strftime("%Y%m%d_%H%M%S")
    OUT = HERE / f"snapshot_{STAMP}"
OUT.mkdir(parents=True, exist_ok=True)


# ---------------------------------------------------------------- session
SESSION = requests.Session()
SESSION.auth = HTTPBasicAuth(USER, PASS)
SESSION.verify = False
SESSION.headers.update({"Accept": "application/hal+json;v=2.0"})


def req_bytes(path: str, accept: str | None = None) -> bytes:
    url = f"{URL}{path}"
    headers = {}
    if accept:
        headers["Accept"] = accept
    for attempt in range(MAX_RETRIES):
        if THROTTLE_S:
            time.sleep(THROTTLE_S)
        r = SESSION.get(url, headers=headers, timeout=20)
        if r.status_code == 503:
            body = r.text[:100]
            wait = 2.0 * (attempt + 1)
            print(f"  [503 {body!r}, backoff {wait:.0f}s] {path}")
            time.sleep(wait)
            continue
        r.raise_for_status()
        return r.content
    r.raise_for_status()
    return b""


def json_get(path: str) -> dict:
    return json.loads(req_bytes(path).decode("utf-8"))


def raw_get(path: str) -> bytes:
    return req_bytes(path, accept="*/*")


def write(rel: str, data: bytes | str) -> Path:
    p = OUT / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(data, str):
        p.write_text(data, encoding="utf-8")
    else:
        p.write_bytes(data)
    return p


# -------------------------------------------------------------------- rapid
def snapshot_rapid() -> None:
    print("[rapid] listing tasks...")
    tasks = json_get("/rw/rapid/tasks")
    write("rapid/_tasks.json", json.dumps(tasks, indent=2))
    task_names: list[str] = []
    # Tasks are in _embedded.resources with _type "rap-task-li"
    for r in tasks.get("_embedded", {}).get("resources", []):
        if r.get("_type") != "rap-task-li":
            continue
        name = r.get("name") or r.get("_title")
        if name:
            task_names.append(name)
    print(f"[rapid] real tasks: {task_names}")

    for task in task_names:
        print(f"[rapid] task {task}: listing modules...")
        try:
            mods = json_get(f"/rw/rapid/tasks/{task}/modules")
        except Exception as e:
            print(f"[rapid] {task} modules failed: {e}")
            continue
        write(f"rapid/{task}/_modules.json", json.dumps(mods, indent=2))

        for mr in mods.get("state", []):
            if mr.get("_type") != "rap-module-info-li":
                continue
            modname = mr.get("name")
            if not modname:
                continue
            try:
                txt_meta = json_get(f"/rw/rapid/tasks/{task}/modules/{modname}/text")
            except Exception as e:
                print(f"[rapid]   {modname} meta failed: {e}")
                continue
            write(f"rapid/{task}/{modname}.meta.json", json.dumps(txt_meta, indent=2))

            file_path = ""
            for s in txt_meta.get("state", []):
                fp = s.get("file-path")
                if fp:
                    file_path = fp
                    break
            if not file_path:
                print(f"[rapid]   {modname}: no file-path")
                continue
            fs = file_path.lstrip("/")
            parts = fs.split("/", 1)
            fs_url = f"/fileservice/${parts[0]}/{parts[1]}" if len(parts) == 2 else f"/fileservice/{fs}"
            try:
                src = raw_get(fs_url)
                write(f"rapid/{task}/{modname}.mod", src)
                print(f"[rapid]   {modname} ({len(src)} bytes)")
            except Exception as e:
                print(f"[rapid]   {modname} source failed: {e}")


# -------------------------------------------------------------------- cfg
CFG_DOMAINS = ["EIO", "MMC", "MOC", "PROC", "SIO", "SYS"]


def snapshot_cfg() -> None:
    for dom in CFG_DOMAINS:
        print(f"[cfg] domain {dom}")
        try:
            dom_idx = json_get(f"/rw/cfg/{dom}")
        except Exception as e:
            print(f"[cfg]   {dom} index failed: {e}")
            continue
        write(f"cfg/{dom}/_index.json", json.dumps(dom_idx, indent=2))
        # CFG domain types are in _embedded.resources, but title/name pattern varies
        entries = dom_idx.get("_embedded", {}).get("resources", []) or dom_idx.get("state", [])
        for r in entries:
            tname = r.get("_title") or r.get("name")
            if not tname:
                continue
            try:
                inst = json_get(f"/rw/cfg/{dom}/{tname}/instances")
                write(f"cfg/{dom}/{tname}.json", json.dumps(inst, indent=2))
                n = len(inst.get("_embedded", {}).get("resources", []))
                print(f"[cfg]   {dom}/{tname}: {n} instances")
            except Exception as e:
                err = str(e)[:80]
                print(f"[cfg]   {dom}/{tname}: {err}")


# -------------------------------------------------------------------- misc
MISC_ENDPOINTS = [
    ("/rw/system", "system.json"),
    ("/rw/panel/ctrl-state", "ctrl-state.json"),
    ("/rw/panel/opmode", "opmode.json"),
    ("/rw/rapid/execution", "rapid-execution.json"),
    ("/rw/rapid/tasks/T_ROB1/pcp", "rapid-pcp.json"),
    ("/rw/motionsystem/mechunits/ROB_1/robtarget", "robtarget.json"),
    ("/rw/motionsystem/mechunits/ROB_1/jointtarget", "jointtarget.json"),
    ("/rw/iosystem/signals", "iosignals.json"),
    ("/rw/iosystem/networks", "ionetworks.json"),
    ("/rw/elog/0?lang=en&limit=100", "elog.json"),
]


def snapshot_misc() -> None:
    print("[misc] controller + io + panel state")
    for path, name in MISC_ENDPOINTS:
        try:
            b = req_bytes(path)
            write(f"misc/{name}", b)
            print(f"[misc]   {name} ok ({len(b)} bytes)")
        except Exception as e:
            print(f"[misc]   {name}: {e}")


if __name__ == "__main__":
    print(f"snapshot target: {URL}")
    print(f"section:         {SECTION}")
    print(f"writing to:      {OUT}")
    print(f"throttle:        {THROTTLE_S}s per request")
    if SECTION in ("all", "rapid"):
        snapshot_rapid()
    if SECTION in ("all", "cfg"):
        snapshot_cfg()
    if SECTION in ("all", "misc"):
        snapshot_misc()
    print(f"\ndone. {OUT}")
