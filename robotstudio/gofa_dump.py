"""
Consolidated single-file JSON dump of the real GoFa for downstream processing
(e.g. Digital Product Passport mapping).

Writes ONE file: gofa_dump_<timestamp>.json with everything under one top-level
object. Reuses the same RWS session/auth/throttle pattern as gofa_snapshot.py.

Usage (PowerShell or WSL):
    python gofa_dump.py                    # default https://192.168.125.1:443
    python gofa_dump.py lab
    python gofa_dump.py https://10.0.0.5:443
    python gofa_dump.py local              # http://localhost:80

Env:
    GOFA_USER / GOFA_PASS  (default "Default User" / "robotics")
    GOFA_THROTTLE=0.1      (delay between requests, default 0.1s)
    GOFA_DUMP_OUT=path     (override output file path)
"""
from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime, timezone
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

USER = os.environ.get("GOFA_USER", "Default User")
PASS = os.environ.get("GOFA_PASS", "robotics")
THROTTLE_S = float(os.environ.get("GOFA_THROTTLE", "0.1"))
MAX_RETRIES = 4

HERE = Path(__file__).resolve().parent
OUT_OVERRIDE = os.environ.get("GOFA_DUMP_OUT")
if OUT_OVERRIDE:
    OUT_FILE = Path(OUT_OVERRIDE)
else:
    STAMP = datetime.now().strftime("%Y%m%d_%H%M%S")
    OUT_FILE = HERE / f"gofa_dump_{STAMP}.json"


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
            wait = 2.0 * (attempt + 1)
            print(f"  [503, backoff {wait:.0f}s] {path}")
            time.sleep(wait)
            continue
        r.raise_for_status()
        return r.content
    r.raise_for_status()
    return b""


def jget(path: str) -> dict | None:
    try:
        return json.loads(req_bytes(path).decode("utf-8"))
    except Exception as e:
        print(f"  ! {path}: {str(e)[:80]}")
        return None


def tget(path: str) -> str | None:
    try:
        return req_bytes(path, accept="*/*").decode("utf-8", errors="replace")
    except Exception as e:
        print(f"  ! {path}: {str(e)[:80]}")
        return None


def jpost(path: str, form: dict) -> dict | None:
    """POST form-encoded data with the OmniCore-required versioned content-type."""
    url = f"{URL}{path}"
    headers = {"Content-Type": "application/x-www-form-urlencoded;v=2.0"}
    for attempt in range(MAX_RETRIES):
        if THROTTLE_S:
            time.sleep(THROTTLE_S)
        try:
            r = SESSION.post(url, data=form, headers=headers, timeout=20)
        except Exception as e:
            print(f"  ! POST {path}: {str(e)[:80]}")
            return None
        if r.status_code == 503:
            wait = 2.0 * (attempt + 1)
            print(f"  [503, backoff {wait:.0f}s] POST {path}")
            time.sleep(wait)
            continue
        try:
            return r.json()
        except Exception as e:
            print(f"  ! POST {path}: {r.status_code} {str(e)[:60]}")
            return None
    return None


# ---- XHTML transport (legacy RAPID symbol endpoints only support v2.0 xhtml) ----

import re
from html.parser import HTMLParser

_XHTML_ACCEPT = "application/xhtml+xml;v=2.0"


class _LiParser(HTMLParser):
    """Parse RWS XHTML <li>…</li> records into a list of dicts.

    Each <li class="…"> becomes a dict; <span class="key">val</span> children
    populate it; <a href rel="self"> is captured as `_self_href`. The element's
    `class` is stored as `_class`, `title` as `_title`.
    """

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.items: list[dict] = []
        self._cur: dict | None = None
        self._span_key: str | None = None
        self._span_buf: list[str] = []

    def handle_starttag(self, tag: str, attrs: list) -> None:
        a = dict(attrs)
        if tag == "li":
            self._cur = {"_class": a.get("class", ""), "_title": a.get("title", "")}
        elif self._cur is not None:
            if tag == "span" and a.get("class"):
                self._span_key = a["class"]
                self._span_buf = []
            elif tag == "a" and a.get("rel") == "self":
                self._cur["_self_href"] = a.get("href", "")

    def handle_data(self, data: str) -> None:
        if self._span_key is not None:
            self._span_buf.append(data)

    def handle_endtag(self, tag: str) -> None:
        if tag == "span" and self._span_key is not None and self._cur is not None:
            self._cur[self._span_key] = "".join(self._span_buf).strip()
            self._span_key = None
            self._span_buf = []
        elif tag == "li" and self._cur is not None:
            self.items.append(self._cur)
            self._cur = None


def _parse_xhtml_lis(xhtml: str) -> list[dict]:
    p = _LiParser()
    try:
        p.feed(xhtml)
    except Exception as e:
        print(f"  ! xhtml parse: {str(e)[:80]}")
    return p.items


def xpost_lis(path: str, form: dict) -> list[dict]:
    """POST form, parse XHTML response into a list of <li> dicts."""
    url = f"{URL}{path}"
    headers = {
        "Accept": _XHTML_ACCEPT,
        "Content-Type": "application/x-www-form-urlencoded;v=2.0",
    }
    for attempt in range(MAX_RETRIES):
        if THROTTLE_S:
            time.sleep(THROTTLE_S)
        try:
            r = SESSION.post(url, data=form, headers=headers, timeout=30)
        except Exception as e:
            print(f"  ! POST {path}: {str(e)[:80]}")
            return []
        if r.status_code == 503:
            time.sleep(2.0 * (attempt + 1))
            continue
        if r.status_code != 200:
            print(f"  ! POST {path}: {r.status_code}")
            return []
        return _parse_xhtml_lis(r.text)
    return []


def xget_lis(path: str) -> list[dict]:
    """GET XHTML, return list of <li> dicts."""
    url = f"{URL}{path}"
    for attempt in range(MAX_RETRIES):
        if THROTTLE_S:
            time.sleep(THROTTLE_S)
        try:
            r = SESSION.get(url, headers={"Accept": _XHTML_ACCEPT}, timeout=20)
        except Exception as e:
            print(f"  ! {path}: {str(e)[:80]}")
            return []
        if r.status_code == 503:
            time.sleep(2.0 * (attempt + 1))
            continue
        if r.status_code != 200:
            return []
        return _parse_xhtml_lis(r.text)
    return []


def collect_misc() -> dict:
    print("[misc] controller + panel + motion + io + system + ctrl/*")
    endpoints = {
        # Core /rw
        "system":              "/rw/system",
        "system_products":     "/rw/system/products",
        "system_options":      "/rw/system/options",
        "system_license":      "/rw/system/license",
        "system_energy":       "/rw/system/energy",
        "system_network":      "/rw/system/network",
        "system_timezone":     "/rw/system/timezone",
        "system_locale":       "/rw/system/locale",
        "system_robotware":    "/rw/system/robotware",
        "system_energy_data":  "/rw/system/energy/data",
        # Panel
        "panel_ctrl_state":    "/rw/panel/ctrl-state",
        "panel_opmode":        "/rw/panel/opmode",
        "panel_speedratio":    "/rw/panel/speedratio",
        "panel_safetymode":    "/rw/panel/safetymode",
        "panel_restartmode":   "/rw/panel/restartmode",
        "panel_coldstart":     "/rw/panel/coldstart",
        "panel_operatingmode": "/rw/panel/operatingmode",
        "panel_server_status": "/rw/panel/server-status",
        "panel_mastership":    "/rw/mastership",
        # RAPID
        "rapid_execution":     "/rw/rapid/execution",
        "rapid_execution_cycle":"/rw/rapid/execution/cycle",
        "rapid_pcp":           "/rw/rapid/tasks/T_ROB1/pcp",
        "rapid_program":       "/rw/rapid/tasks/T_ROB1/program",
        "rapid_motion":        "/rw/rapid/tasks/T_ROB1/motion",
        "rapid_modules_global":"/rw/rapid/modules",
        "rapid_uiinstr":       "/rw/rapid/uiinstr",
        # Motion
        "motionsystem":        "/rw/motionsystem",
        "motionsystem_err":    "/rw/motionsystem/errorstate",
        "mechunits":           "/rw/motionsystem/mechunits",
        "mechunit_rob1":       "/rw/motionsystem/mechunits/ROB_1",
        "robtarget":           "/rw/motionsystem/mechunits/ROB_1/robtarget",
        "jointtarget":         "/rw/motionsystem/mechunits/ROB_1/jointtarget",
        "cartesian":           "/rw/motionsystem/mechunits/ROB_1/cartesian",
        "joints":              "/rw/motionsystem/mechunits/ROB_1/joints",
        "motionproperties":    "/rw/motionsystem/mechunits/ROB_1/motionproperties",
        "baseframe":           "/rw/motionsystem/mechunits/ROB_1/baseframe",
        "lead_through":        "/rw/motionsystem/mechunits/ROB_1/lead-through",
        "jog":                 "/rw/motionsystem/mechunits/ROB_1/jog",
        # I/O
        "iosignals":           "/rw/iosystem/signals",
        "ionetworks":          "/rw/iosystem/networks",
        "iodevices":           "/rw/iosystem/devices",
        # Devices / vision / IPC
        "devices":             "/rw/devices",
        "vision":              "/rw/vision",
        "dipc":                "/rw/dipc",
        "dipc_queue":          "/rw/dipc/queue",
        # Misc /rw
        "elog":                "/rw/elog/0?lang=en&limit=500",
        "elog_categories":     "/rw/elog",
        "auditlog":            "/rw/auditlog",
        "process":             "/rw/process",
        "apprmw":              "/rw/apprmw",
        # Users / auth / subscription
        "users":               "/users",
        "users_rmmp":          "/users/rmmp",
        "users_grant":         "/users/grant",
        "subscription":        "/subscription",
        # /ctrl/*
        "ctrl_index":          "/ctrl",
        "ctrl_state":          "/ctrl/state",
        "ctrl_clock":          "/ctrl/clock",
        "ctrl_identity":       "/ctrl/identity",
        "ctrl_system":         "/ctrl/system",
        "ctrl_network":        "/ctrl/network",
        "ctrl_options":        "/ctrl/options",
        "ctrl_features":       "/ctrl/features",
        "ctrl_diagnostics":    "/ctrl/diagnostics",
        "ctrl_certstore":      "/ctrl/certstore",
        "ctrl_processing":     "/ctrl/processing",
        "ctrl_analytics":      "/ctrl/analytics",
        "ctrl_ipc":            "/ctrl/ipc",
        "ctrl_services":       "/ctrl/services",
        "ctrl_backup":         "/ctrl/backup",
        "ctrl_restore":        "/ctrl/restore",
        "ctrl_restart":        "/ctrl/restart",
    }
    out: dict = {}
    for key, path in endpoints.items():
        d = jget(path)
        if d is not None:
            out[key] = d
            print(f"  ok  {key}")
    return out


def collect_rapid() -> dict:
    print("[rapid] tasks + modules + sources")
    out: dict = {"tasks": {}}
    tasks = jget("/rw/rapid/tasks")
    if not tasks:
        return out
    out["_tasks_index"] = tasks

    for r in tasks.get("_embedded", {}).get("resources", []):
        if r.get("_type") != "rap-task-li":
            continue
        name = r.get("name") or r.get("_title")
        if not name:
            continue
        print(f"  task {name}")
        task_obj: dict = {"modules": {}, "sources": {}}
        mods = jget(f"/rw/rapid/tasks/{name}/modules")
        if not mods:
            out["tasks"][name] = task_obj
            continue
        task_obj["_modules_index"] = mods

        for mr in mods.get("state", []):
            if mr.get("_type") != "rap-module-info-li":
                continue
            modname = mr.get("name")
            if not modname:
                continue
            txt_meta = jget(f"/rw/rapid/tasks/{name}/modules/{modname}/text")
            if not txt_meta:
                continue
            task_obj["modules"][modname] = txt_meta

            file_path = ""
            for s in txt_meta.get("state", []):
                fp = s.get("file-path")
                if fp:
                    file_path = fp
                    break
            if not file_path:
                continue
            fs = file_path.lstrip("/")
            parts = fs.split("/", 1)
            fs_url = f"/fileservice/${parts[0]}/{parts[1]}" if len(parts) == 2 else f"/fileservice/{fs}"
            src = tget(fs_url)
            if src is not None:
                task_obj["sources"][modname] = src
                print(f"    {modname} ({len(src)} chars)")
        out["tasks"][name] = task_obj
    return out


CFG_DOMAINS = ["EIO", "MMC", "MOC", "PROC", "SIO", "SYS"]


def collect_cfg() -> dict:
    print("[cfg] all domains")
    out: dict = {}
    for dom in CFG_DOMAINS:
        dom_idx = jget(f"/rw/cfg/{dom}")
        if not dom_idx:
            continue
        dom_obj: dict = {"_index": dom_idx, "types": {}}
        entries = dom_idx.get("_embedded", {}).get("resources", []) or dom_idx.get("state", [])
        for r in entries:
            tname = r.get("_title") or r.get("name")
            if not tname:
                continue
            inst = jget(f"/rw/cfg/{dom}/{tname}/instances")
            if inst is not None:
                dom_obj["types"][tname] = inst
        out[dom] = dom_obj
        print(f"  {dom}: {len(dom_obj['types'])} types")
    return out


def collect_safety() -> dict:
    print("[safety] /ctrl/safety subtree")
    out: dict = {}
    seen: set[str] = set()
    queue = ["/ctrl/safety"]
    while queue:
        path = queue.pop(0)
        if path in seen:
            continue
        seen.add(path)
        d = jget(path)
        if d is None:
            continue
        out[path] = d
        for r in d.get("_embedded", {}).get("resources", []):
            href = r.get("_links", {}).get("self", {}).get("href", "")
            if not href or href.startswith("http"):
                continue
            nxt = href if href.startswith("/") else path.rstrip("/") + "/" + href
            if nxt.startswith("/ctrl/safety"):
                queue.append(nxt)
    print(f"  {len(out)} safety nodes")
    return out


def collect_fs() -> dict:
    print("[fs] $HOME + $BACKUP listings")
    out: dict = {}

    root = jget("/fileservice/")
    if root is not None:
        out["_root"] = root

    def walk(relurl: str, depth: int, max_depth: int = 3) -> None:
        if depth > max_depth:
            return
        d = jget(f"/fileservice/{relurl}")
        if d is None:
            return
        out[relurl] = d
        for r in d.get("_embedded", {}).get("resources", []):
            if r.get("_type") == "fs-dir":
                title = r.get("_title") or ""
                if title:
                    walk(f"{relurl}/{title}" if relurl else title, depth + 1, max_depth)

    # Walk all top-level dollar-prefixed roots discovered in /fileservice/
    extra_roots: list[str] = []
    if isinstance(root, dict):
        for r in root.get("_embedded", {}).get("resources", []):
            t = r.get("_title") or ""
            if t.startswith("$"):
                extra_roots.append(t)
    # Fallbacks if discovery failed
    if not extra_roots:
        extra_roots = ["$HOME", "$BACKUP", "$TEMP", "$INTERNAL"]
    for r in extra_roots:
        walk(r, 0, 4)
    print(f"  {len(out)} fs listings")
    return out


def collect_symbols(task_names: list[str]) -> dict:
    """Enumerate all RAPID symbols (PERS/CONST/VAR) per task and read their values.

    OmniCore RWS 2.0 only serves these endpoints with `Accept: application/xhtml+xml;v=2.0`
    — JSON returns 406. Responses are XHTML, parsed into <li> dicts.

        POST /rw/rapid/symbols/search   form: view=block, blockurl=RAPID/<task>,
                                              recursive=TRUE, symtyp=per|con|var
        GET  /rw/rapid/symbol/<symburl>/properties
        GET  /rw/rapid/symbol/<symburl>/data

    Captures: tooldata, wobjdata, calibration, MetaMoveCore PERS — everything
    that's runtime state in RAPID modules.
    """
    print("[symbols] RAPID symbol enumeration + values (xhtml)")
    out: dict = {}
    for task in task_names:
        task_out: dict = {"per": {}, "con": {}, "var": {}}
        for symtyp in ("per", "con", "var"):
            results = xpost_lis(
                "/rw/rapid/symbols/search",
                {
                    "view": "block",
                    "blockurl": f"RAPID/{task}",
                    "recursive": "TRUE",
                    "symtyp": symtyp,
                },
            )
            if not results:
                continue
            n_ok = 0
            for sym in results:
                symurl = sym.get("symburl") or sym.get("_title") or ""
                if not symurl:
                    continue
                entry: dict = {"meta": {k: v for k, v in sym.items() if not k.startswith("_")}}
                if sym.get("_self_href"):
                    entry["meta"]["_self_href"] = sym["_self_href"]

                # Read live value (skip for 'con' on read-only items — still try)
                data_lis = xget_lis(f"/rw/rapid/symbol/{symurl}/data")
                if data_lis:
                    # Typically one <li class="rap-data"> with a "value" span
                    entry["data"] = data_lis[0] if len(data_lis) == 1 else data_lis

                # Read properties (datatype, scope, persistence, RO/RW…)
                prop_lis = xget_lis(f"/rw/rapid/symbol/{symurl}/properties")
                if prop_lis:
                    entry["properties"] = prop_lis[0] if len(prop_lis) == 1 else prop_lis

                task_out[symtyp][symurl] = entry
                n_ok += 1
            print(f"  {task}/{symtyp}: {n_ok} symbols")
        out[task] = task_out
    return out


def _resolve_href(href: str, base_url: str) -> str | None:
    """Resolve a HAL relative href against its document's _links.base.href.
    Returns an absolute path (starts with '/') or None if unresolvable."""
    if not href:
        return None
    if href.startswith("http"):
        # Strip scheme://host:port/, keep the path
        from urllib.parse import urlparse
        return urlparse(href).path or None
    if href.startswith("/"):
        return href
    # Relative: resolve against base_url (which is e.g. https://host:port/rw/rapid/)
    if base_url:
        from urllib.parse import urlparse
        base_path = urlparse(base_url).path or "/"
        if not base_path.endswith("/"):
            base_path += "/"
        return base_path + href
    return None


def collect_crawl(roots: tuple = ("/rw", "/ctrl", "/users", "/subscription"),
                  max_depth: int = 4) -> dict:
    """Generic BFS crawl. Resolves relative hrefs against _links.base.href (RWS uses
    relative hrefs with a per-document base, NOT relative to the request path)."""
    print(f"[crawl] BFS over {roots} (max depth {max_depth})")
    out: dict = {}
    seen: set[str] = set()
    skip_prefixes = (
        "/fileservice", "/rw/cfg", "/ctrl/safety",
        "/rw/rapid/symbol", "/rw/rapid/symbols",
        "/rw/rw", "/ctrl/ctrl",  # accidental doubled paths from inconsistent base hrefs
    )
    queue: list[tuple[str, int]] = [(r, 0) for r in roots]

    while queue:
        path, depth = queue.pop(0)
        if path in seen:
            continue
        if any(path.startswith(p) for p in skip_prefixes):
            continue
        seen.add(path)
        d = jget(path)
        if d is None:
            continue
        out[path] = d
        if depth >= max_depth:
            continue
        # Document's base for relative href resolution
        base_url = (d.get("_links", {}).get("base", {}) or {}).get("href", "")

        # Follow embedded resources
        for r in d.get("_embedded", {}).get("resources", []):
            href = r.get("_links", {}).get("self", {}).get("href", "")
            nxt = _resolve_href(href, base_url)
            if nxt:
                queue.append((nxt.split("?")[0], depth + 1))
        # Follow top-level _links (besides nav/self/base)
        links = d.get("_links", {}) if isinstance(d.get("_links"), dict) else {}
        for key, link in links.items():
            if key in ("self", "next", "prev", "first", "last", "base"):
                continue
            href = link.get("href", "") if isinstance(link, dict) else ""
            nxt = _resolve_href(href, base_url)
            if nxt:
                queue.append((nxt.split("?")[0], depth + 1))
    print(f"  crawled {len(out)} additional endpoints")
    return out


if __name__ == "__main__":
    print(f"dump target:  {URL}")
    print(f"output file:  {OUT_FILE}")
    print(f"throttle:     {THROTTLE_S}s per request")
    print()

    started = datetime.now(timezone.utc)
    dump: dict = {
        "_meta": {
            "target": URL,
            "started_utc": started.isoformat().replace("+00:00", "Z"),
            "user": USER,
            "tool": "gofa_dump.py",
            "schema_version": 1,
        },
    }
    dump["misc"]   = collect_misc()
    dump["rapid"]  = collect_rapid()
    # Re-use task list discovered during rapid collection for symbol enumeration
    task_names = list(dump["rapid"].get("tasks", {}).keys())
    dump["symbols"] = collect_symbols(task_names) if task_names else {}
    dump["cfg"]    = collect_cfg()
    dump["safety"] = collect_safety()
    dump["fs"]     = collect_fs()
    dump["crawl"]  = collect_crawl()
    finished = datetime.now(timezone.utc)
    dump["_meta"]["finished_utc"] = finished.isoformat().replace("+00:00", "Z")
    dump["_meta"]["duration_s"] = round((finished - started).total_seconds(), 2)

    OUT_FILE.write_text(json.dumps(dump, indent=2, ensure_ascii=False), encoding="utf-8")
    size_kb = OUT_FILE.stat().st_size / 1024
    print()
    print(f"done. {OUT_FILE}  ({size_kb:.1f} KB)")
