"""
Dynamic / historical data dump from the real GoFa for DPP operational submodels.

Focused on what the controller actually persists between sessions:
  - Energy counters (cumulative + per-axis interval)
  - Full event-log across ALL domains (Operational, System, HW, Program, Motion,
    Operator, IO, Internal, Process, Configuration, RAPID, Connected Services, …)
  - Live snapshot (pose, joints, exec/ctrl state, IO levels)
  - Service-relevant device states (CONNECTED_SERVICES_GW, SERVICE_EMB, …)

Writes ONE file: gofa_dynamic_<timestamp>.json

Usage:
    python gofa_dynamic.py            # default lab (https://192.168.125.1:443)
    python gofa_dynamic.py alt
    python gofa_dynamic.py https://10.0.0.5:443

Env:
    GOFA_USER / GOFA_PASS  (default "Default User" / "robotics")
    GOFA_THROTTLE=0.05     (default 0.05s — light load, all GETs)
    GOFA_DYN_OUT=path      (override output file path)
    GOFA_ELOG_LIMIT=2000   (max entries per elog domain, default 2000)
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
    "lab":   "https://192.168.125.1:443",
    "alt":   "https://192.168.125.99:443",
    "local": "http://localhost:80",
}
arg = sys.argv[1] if len(sys.argv) > 1 else "lab"
URL = PRESETS.get(arg, arg)

USER = os.environ.get("GOFA_USER", "Default User")
PASS = os.environ.get("GOFA_PASS", "robotics")
THROTTLE_S = float(os.environ.get("GOFA_THROTTLE", "0.05"))
ELOG_LIMIT = int(os.environ.get("GOFA_ELOG_LIMIT", "2000"))
MAX_RETRIES = 4

HERE = Path(__file__).resolve().parent
OUT_OVERRIDE = os.environ.get("GOFA_DYN_OUT")
if OUT_OVERRIDE:
    OUT_FILE = Path(OUT_OVERRIDE)
else:
    STAMP = datetime.now().strftime("%Y%m%d_%H%M%S")
    OUT_FILE = HERE / f"gofa_dynamic_{STAMP}.json"

SESSION = requests.Session()
SESSION.auth = HTTPBasicAuth(USER, PASS)
SESSION.verify = False
SESSION.headers.update({"Accept": "application/hal+json;v=2.0"})


def jget(path: str, quiet_codes: tuple = ()) -> dict | None:
    url = f"{URL}{path}"
    for attempt in range(MAX_RETRIES):
        if THROTTLE_S:
            time.sleep(THROTTLE_S)
        try:
            r = SESSION.get(url, timeout=20)
        except Exception as e:
            print(f"  ! {path}: {str(e)[:80]}")
            return None
        if r.status_code == 503:
            time.sleep(2.0 * (attempt + 1))
            continue
        if r.status_code in quiet_codes:
            return None
        if r.status_code == 204:
            return None
        if r.status_code != 200:
            print(f"  ! {path}: {r.status_code}")
            return None
        try:
            return r.json()
        except Exception as e:
            print(f"  ! {path}: parse {str(e)[:60]}")
            return None
    return None


# ---- Energy: cumulative + per-axis ----------------------------------------

def collect_energy() -> dict:
    print("[energy] cumulative + per-axis")
    out: dict = {}
    e = jget("/rw/system/energy")
    if e:
        out["raw"] = e
        for s in e.get("state", []):
            if s.get("_type") == "sys-energy-state":
                out["accumulated_kwh"] = float(s.get("accumulated-energy", 0))
                out["interval_kwh"] = float(s.get("interval-energy", 0))
                out["interval_seconds"] = int(s.get("interval-length", 0))
                out["energy_state"] = s.get("energy-state")
                out["reset_time"] = s.get("reset-time")
                out["sample_time"] = s.get("time-stamp")
                out["change_count"] = s.get("change-count")
            elif s.get("_type") == "sys-energy-mec":
                out["per_mechunit"] = out.get("per_mechunit", {})
                out["per_mechunit"][s.get("_title")] = [
                    {"axis": a.get("_title"),
                     "interval_kwh": float(a.get("interval-energy", 0))}
                    for a in s.get("axes", [])
                ]
        print(f"  accumulated: {out.get('accumulated_kwh', 0):.3f} kWh"
              f" since {out.get('reset_time', '?')}")
    return out


# ---- Event-Log: all domains, all entries ----------------------------------

def collect_elog() -> dict:
    """Read every event-log domain. The controller holds a small ring-buffer
    (~50 events) per domain and the `limit`/`start` query params are ignored,
    so a single GET per domain returns everything available.
    """
    print("[elog] all domains")
    out: dict = {"domains": {}, "categories": None, "all_entries": []}

    cats = jget("/rw/elog")
    out["categories"] = cats

    domain_ids: list[str] = []
    if cats:
        for r in cats.get("_embedded", {}).get("resources", []):
            href = r.get("_links", {}).get("self", {}).get("href", "")
            if href:
                last = href.rstrip("/").split("/")[-1].split("?")[0]
                if last.isdigit():
                    domain_ids.append(last)
    if not domain_ids:
        domain_ids = [str(i) for i in range(0, 20)]

    for did in domain_ids:
        idx = jget(f"/rw/elog/{did}?lang=en", quiet_codes=(400, 404))
        if not idx:
            continue
        entries: list[dict] = []
        for r in idx.get("_embedded", {}).get("resources", []):
            if r.get("_type") != "elog-message-li":
                continue
            href = r.get("_links", {}).get("self", {}).get("href", "")
            seq = href.split("?")[0].split("/")[-1] if href else ""
            flat: dict = {"domain": did, "seq": seq}
            for k, v in r.items():
                if not k.startswith("_"):
                    flat[k] = v
            entries.append(flat)
        out["domains"][did] = {"index": idx, "entries": entries}
        out["all_entries"].extend(entries)
        print(f"  domain {did}: {len(entries)} entries")

    out["all_entries"].sort(key=lambda e: e.get("tstamp", ""), reverse=True)
    print(f"  total: {len(out['all_entries'])} entries across {len(out['domains'])} domains")
    return out


# ---- Live snapshot of dynamic state ---------------------------------------

def collect_live() -> dict:
    print("[live] pose / joints / exec / ctrl / io")
    out: dict = {"sample_time_utc": datetime.now(timezone.utc).isoformat()}
    targets = {
        "ctrl_state":     "/rw/panel/ctrl-state",
        "opmode":         "/rw/panel/opmode",
        "speed_override": "/rw/panel/speedratio",
        "rapid_exec":     "/rw/rapid/execution",
        "rapid_pcp":      "/rw/rapid/tasks/T_ROB1/pcp",
        "rapid_motion":   "/rw/rapid/tasks/T_ROB1/motion",
        "cartesian":      "/rw/motionsystem/mechunits/ROB_1/cartesian",
        "robtarget":      "/rw/motionsystem/mechunits/ROB_1/robtarget",
        "jointtarget":    "/rw/motionsystem/mechunits/ROB_1/jointtarget",
        "motion_err":     "/rw/motionsystem/errorstate",
        "iosignals":      "/rw/iosystem/signals",
    }
    for k, p in targets.items():
        d = jget(p)
        if d is not None:
            out[k] = d
            print(f"  ok  {k}")
    return out


# ---- Service / device health -----------------------------------------------

def collect_services() -> dict:
    print("[services] connected-services / service_emb / device states")
    out: dict = {}
    targets = {
        "connected_services_gw":  "/rw/devices/HW_DEVICES/CONNECTED_SERVICES_GW",
        "service_emb":            "/rw/devices/SW_RESOURCES/SERVICE_EMB",
        "services_index":         "/rw/devices/SW_RESOURCES/SERVICES",
        "hw_devices":             "/rw/devices/HW_DEVICES",
        "sw_resources":           "/rw/devices/SW_RESOURCES",
        "ctrl_diagnostics_index": "/ctrl",
        "ctrl_backup":            "/ctrl/backup",
        "ctrl_backup_state":      "/ctrl/backup/state",
    }
    for k, p in targets.items():
        d = jget(p)
        if d is not None:
            out[k] = d
            print(f"  ok  {k}")
    return out


# ---- Derived counters (rough Maintenance / HealthIndicator) ----------------

ERROR_CODES_OF_INTEREST = {
    "E-Stop":          ["10010", "10011", "10012", "10013", "20223"],
    "Collision":       ["50056", "50057", "50204"],
    "SafeMove":        ["20461", "20462", "20463", "20464"],
    "MotorOff":        ["10014", "10015"],
    "ProgramStop":     ["10005", "10006"],
}


def derive_health(elog: dict) -> dict:
    print("[derive] health indicators from elog")
    entries = elog.get("all_entries", [])
    out: dict = {
        "total_events": len(entries),
        "by_domain": {},
        "by_severity": {},
        "category_counts": {},
        "first_event": None,
        "last_event": None,
    }
    for e in entries:
        out["by_domain"][e.get("domain", "?")] = out["by_domain"].get(e.get("domain", "?"), 0) + 1
        sev = e.get("msg-type") or e.get("severity") or "?"
        out["by_severity"][sev] = out["by_severity"].get(sev, 0) + 1

    for cat, codes in ERROR_CODES_OF_INTEREST.items():
        n = sum(1 for e in entries if str(e.get("code", "")) in codes)
        out["category_counts"][cat] = n

    timestamps = sorted(e.get("tstamp", "") for e in entries if e.get("tstamp"))
    if timestamps:
        out["first_event"] = timestamps[0]
        out["last_event"] = timestamps[-1]
    print(f"  events: {out['total_events']}  "
          f"first: {out['first_event']}  last: {out['last_event']}")
    print(f"  category_counts: {out['category_counts']}")
    return out


if __name__ == "__main__":
    print(f"target:       {URL}")
    print(f"output file:  {OUT_FILE}")
    print(f"throttle:     {THROTTLE_S}s per request")
    print(f"elog limit:   {ELOG_LIMIT} per domain")
    print()

    started = datetime.now(timezone.utc)
    dump: dict = {
        "_meta": {
            "target": URL,
            "started_utc": started.isoformat().replace("+00:00", "Z"),
            "tool": "gofa_dynamic.py",
            "schema_version": 1,
            "purpose": "operational/historical data for DPP submodels",
        },
    }
    dump["energy"]   = collect_energy()
    dump["live"]     = collect_live()
    dump["services"] = collect_services()
    dump["elog"]     = collect_elog()
    dump["health"]   = derive_health(dump["elog"])

    finished = datetime.now(timezone.utc)
    dump["_meta"]["finished_utc"] = finished.isoformat().replace("+00:00", "Z")
    dump["_meta"]["duration_s"] = round((finished - started).total_seconds(), 2)

    OUT_FILE.write_text(json.dumps(dump, indent=2, ensure_ascii=False), encoding="utf-8")
    print()
    print(f"done. {OUT_FILE}  ({OUT_FILE.stat().st_size / 1024:.1f} KB)")
