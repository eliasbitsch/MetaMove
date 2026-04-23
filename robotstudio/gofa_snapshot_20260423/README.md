# GoFa Snapshot — 2026-04-23

Full read-only export of the real GoFa at `192.168.125.1` (Serial `15000-500126`, RobotWare 7.20.0, GoFa CRB 15000-5/0.95 OmniCore). Pulled via RWS using [../gofa_snapshot.py](../gofa_snapshot.py).

Purpose: enable local replication on a Virtual Controller for safe development without needing to be at the physical robot.

## Contents

### `rapid/`

Per-task module sources + metadata. Tasks and their extracted module count:

| Task | Extracted sources | Skipped (no file-path) |
|---|---|---|
| `T_ROB1` | 11 (`MainModule`, `Morobot_Assembly`, `module_EGM`, `module_MAIN_GOHOLO`, `module_HOLOPATH`, `module_RANDOMPATH`, `module_WIZARD`, `module_mr19m010_copy`, `module_mr22m012`, `Calib_Def_wobj_kiste_5`, `Calib_Def1_wobj_kiste_1`) | `BASE`, `GOFA_ASI_Procedures`, `Wizard_LoadData`, `Wizard_Params`, `module_MAIN`, `Calib_Def_wobj_kiste_{1..4,6,7}` |
| `T_COMM` | 1 (`CommModule`) | `BASE` |
| `T_GOFA_LED` | 0 | `BASE`, `GOFA_Main` (500 error) |
| `SC_CBC` | 0 (safety-internal, opaque) | — |

"no file-path" means the module has no transient file reference — typical for `SYSMOD` modules loaded from system areas. The active code we care about (`MainModule`, `module_EGM`, everything GoHolo) is all captured.

### `cfg/`

Full controller configuration dump, 195 instances across 6 domains:

| Domain | What it contains |
|---|---|
| `EIO/` | I/O configuration, signals, devices, networks |
| `MMC/` | Man-Machine Control (FlexPendant menus, etc.) |
| `MOC/` | Motion (axes, velocities, soft servo, EGM limits) |
| `PROC/` | Process modules / RAPID task declarations |
| `SIO/` | Serial I/O, **UDPUC hosts (EGM targets)**, IP settings, firewall |
| `SYS/` | System-level — tools, system signals, options, presentation |

**Key finding: [`cfg/SIO/UDPUC_HOST.json`](cfg/SIO/UDPUC_HOST.json)** contains the working EGM UDPUC configuration:

| Name | Remote IP | RemotePort | LocalPort |
|---|---|---|---|
| `ROB_1` | 192.168.125.100 | 6515 | 6515 |
| `UCstream` | 192.168.125.10 | 6510 | 0 |
| `UCdevice` | 192.168.125.10 | 6510 | 6599 |
| **`ROB_Michi`** | **192.168.125.99** | **6511** | **6511** |

`ROB_Michi` is the presumed-working EGM target (from a prior ROS2/RViz/MoveIt integration). For the MetaMove Unity host, **point at 192.168.125.99** or replicate this device in the VC.

### `misc/`

One-shot snapshots of controller state at snapshot time:

- `system.json` — controller/RW version, serial
- `ctrl-state.json` — motors on/off
- `opmode.json` — auto/manual
- `rapid-execution.json` — running/stopped
- `rapid-pcp.json` — live program pointer (was `MainModule.main:23`)
- `robtarget.json` / `jointtarget.json` — live TCP + joint pose
- `iosignals.json` — all I/O signal values
- `ionetworks.json` — I/O bus network list
- `elog.json` — last 100 event log entries (contains the `ROB_Michi` connection-refused spam since 2026-04-16)

## Additional content pulled 2026-04-23 (extended snapshot)

### `safety/`

Full `/ctrl/safety` subtree: mode, cbc, load, config, violation. Covers SafeMove Collaborative state not exposed via the 6 standard CFG domains.

### `fs/`

Listings of `$HOME`, `$BACKUP`, and the fileservice root (directory trees only, no binary content — that's in `fs_files/`).

### `fs_files/`

Source files pulled directly from controller filesystem:

| Folder | Contents |
|---|---|
| `HOME_NewProgramdemoergo/` | Live project: MainModule.modx (60 KB), Morobot_Assembly.modx, all 7 Calib_Def_wobj_kiste files, module_MAIN/WIZARD/mr22/mr19, NewProgramdemoergo.pgf |
| `HOME_Wizard/` | Wizard_Params.sys, Wizard.mod, category_order.txt, Bind/ subfolder |
| `HOME_RECOVERY/` | RECOVERY copies of T_DATA/T_COMM modules (useful rollback reference) |
| `HOME_Dap/` | eg1bas/prc/tol.sysx — EGM-related sysmod files |

### `backups/`

Two full RobotWare backups, pulled from the controller's `$BACKUP` folder (spy logs stripped — 99 MB → 2.4 MB):

- **`15000-500126_Backup_20220728/`** — original 2022-07-28 snapshot. Contains `Safety Configuration Report.pdf` (1.2 MB, formal SafeMove PDF), full RAPID tree, SYSPAR .cfg files.
- **`15000-500126_Backup_20250127/`** — most recent formal backup (2025-01-27). Used as the canonical reference.

Each backup has the canonical RobotWare structure:

```
15000-500126_Backup_YYYYMMDD/
  BACKINFO/    ← system metadata, sc_cfg.xml, version.xml
  ADDINDATA/   ← ABB.ROBOTICS.ROBOTS/HOME/GOFA_Main.sysx etc.
  RAPID/       ← TASK1..4/{SYSMOD,PROGMOD}/*.sysx/*.modx — THE canonical source
  SYSPAR/      ← EIO.cfg, SIO.cfg, MOC.cfg, SYS.cfg, MMC.cfg, PROC.cfg
                 (real RobotWare .cfg files, loadable via RobotStudio)
  HOME/        ← project files: Dashboard.xml, Safety PDF, eg1*.cfg, MainModule.modx
  License/     ← license blobs
  hwsettings.rsf
  system.xml
```

## Replay on a Virtual Controller

The `backups/15000-500126_Backup_20250127/` folder is effectively a complete `Backup & Restore` package that RobotStudio can import directly:

1. In RobotStudio: **Datei → Zurücksichern von Steuerung** (or "Restore from Controller")
2. Point at the `15000-500126_Backup_20250127` directory
3. RobotStudio creates a new VC with all RAPID, CFG, and hardware settings

For a cleaner VC targeted at MetaMove without the legacy project:
- Take `SYSPAR/SIO.cfg` (UDPUC hosts incl. ROB_Michi @ 192.168.125.99:6511)
- Take `SYSPAR/MOC.cfg` (motion limits, EGM config in CFG terms)
- Skip `RAPID/TASK2/PROGMOD/*` (the old project) — replace with MetaMoveDemos.mod
- Keep `RAPID/TASK2/SYSMOD/module_EGM.sysx` as the EGM logic template

## Key findings for MetaMove

1. **UDPUC `ROB_Michi` → 192.168.125.99:6511** is the surviving working EGM config. Point Unity host there.
2. **All GoHolo code commented** on the live controller — not deleted. Can be reactivated via RobotStudio if needed, but MetaMoveDemos is the cleaner path.
3. **Production-Framework I/O signals** (ix_/ox_ pattern) present — MetaMove must use a different prefix to avoid collision.
4. **Multi-vacuum + clutch gripper hardware** — existing `ox_multi_vac_greifer_*`, `ox_kupplung_*` signals can be reused.
5. **SafeMove Collaborative** is active — MetaMove-Safety-Gate must integrate with it, not bypass.
6. **`Safety Configuration Report.pdf`** (in both backups) contains the formal zones + speed/force limits — read before designing MetaMove safety zones.
