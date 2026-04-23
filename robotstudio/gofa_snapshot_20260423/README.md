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

## Replay on a Virtual Controller

Not automated yet. Manual path:

1. Create a clean VC with matching options (EGM 3124-1, UDPUC Driver, Multitasking 3114-1, SafeMove Collaborative 3043-3, IoT Data Gateway 3154-1).
2. Import CFG: in RobotStudio → Controller → Configuration → **Load Parameters** → point at the relevant JSON-equivalent `.cfg` files. (CFG JSON here needs conversion to the RobotWare `.cfg` format — todo: write a converter.)
3. Copy `rapid/T_ROB1/*.mod` into the VC's T_ROB1 task.

The immediate value of this snapshot is **read-only reference** — we have everything needed to understand the working EGM setup and replicate it correctly on a VC or a new MetaMoveDemos deployment.
