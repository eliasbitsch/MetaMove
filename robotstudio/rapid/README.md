# MetaMove RAPID

## Architecture

**RAPID owns motion and demo execution. Unity owns UI, HUD, safety-zone
rendering, and voice. Jarvis owns LLM + tool-calling.**

Safety-motivated split: pre-programmed demo scenarios run as **deterministic
RAPID procedures** on the OmniCore realtime OS. Unity triggers them via RWS
PERS writes, then observes state. Only the free-form teleop path
(Pinch-to-Move) is actually Unity-driven EGM streaming.

```
┌───────────┐      ┌───────────┐
│  Jarvis   │      │   Unity   │
│ voice→LLM │────▶ │   C# UI   │
└───────────┘      └─────┬─────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
     RWS write       RWS read          EGM UDP 6511
     metaMode+Start  metaState,Step    (only in mode 9 teleop)
        │                │                │
        └────────────────┼────────────────┘
                         ▼
              ┌─────────────────────┐
              │  RAPID T_ROB1       │
              │  MetaMoveDispatcher │  ◄── this folder
              │  module_EGM (GoHolo)│
              └─────────────────────┘
```

## Files

### `MetaMoveDispatcher.mod`

Adapted from the proven **GoHolo `module_MAIN_GOHOLO`** pattern
(Jakob Hörbst 2021, extended Alex Korn 2022). Mode-dispatcher:

| mode | what |
|---|---|
| 0 | idle |
| 1 | chess — pick-and-place, single piece |
| 2 | stone_sort — 6 polyhedra into 6 boxes |
| 3 | framing — two-step assembly |
| 4 | mug — fragile handling, reduced speed |
| 5 | pins — precision, N pins on map |
| 6 | bigstone — payload showcase |
| 9 | egm_teleop — Unity Pinch-to-Move |

External PERS vars:

**Write from Unity/Jarvis:**
- `metaMode` — pick which mode
- `metaStart` — trigger (TRUE → start, auto-reset to FALSE)
- `metaAbort` — abort current motion
- `metaSpeed` — 10..100 override
- `metaPickTarget`, `metaPlaceTarget` — Unity fills before `metaStart`
- `metaStoneClass`, `metaPinIndex` — scenario params

**Read from Unity HUD:**
- `metaState` — 0=idle 1=running 2=done 3=error
- `metaStep` — sub-step within current demo
- `metaMsg` — last status for HUD

Demo procedures (`RunDemoChess`, `RunDemoStoneSort`, …) are **skeletons** —
chess is the most complete, others are stubs ready to flesh out as we test.

## UDPUC configuration

MetaMove uses a dedicated UDPUC device so the existing setup is untouched.
Add in Communication → Transmission Protocol:

| Name | Type | Remote Address | Remote Port | Local Port |
|---|---|---|---|---|
| **MetaMoveUC** | UDPUC | 127.0.0.1 (VC) / Unity-PC-IP (lab) | 6511 | 6511 |

Keep existing `UCdevice`, `UCstream`, `ROB_Michi` as-is — `ROB_Michi` is the
surviving working EGM target from the prior ROS2 integration and serves as a
**backup path**. Having both means we can fall back to the old pipeline if
MetaMove has issues.

## Required RobotWare options

Already on the GoFa VC and real robot:
- 3124-1 Externally Guided Motion (EGM)
- UDPUC Driver
- 3114-1 Multitasking
- 3043-3 SafeMove Collaborative
- 3154-1 IoT Data Gateway

No new options needed.

## I/O signals used

`SetDO mm_gripper_close` / `mm_gripper_open` — define as virtual DOs on the
controller (Communication → EIO_SIGNAL). On the real robot, alias/map them
to the existing `ox_multi_vac_greifer_schliessen` / `ox_multi_vac_greifer_oeffnen`
as needed.

## Deploy to VC (dev loop)

Via MCP:
```python
rs_write_module(task="T_ROB1", module="MetaMoveDispatcher",
                code=open("MetaMoveDispatcher.mod").read())
```

Then set program pointer to `MetaMoveDispatcher.metaMain` and start the
simulation (`rs_start_simulation`). Unity / metamove_bridge / RWS-Mock can
then drive it.

## Deploy to real GoFa (production)

1. Connect MCP to real GoFa (`ABB_RWS_URL=https://192.168.125.1:443`)
2. Switch controller to **Manual Reduced Speed**
3. Upload module via `rws_write_module`
4. Add UDPUC entry `MetaMoveUC` (CFG write to SIO/UDPUC_HOST)
5. Set PP to `metaMain`
6. Test each mode in Manual with deadman pressed
7. Once validated, switch to Auto mode

Existing modules (`MainModule`, `module_MAIN_GOHOLO`, etc.) remain untouched
as backups — we only add `MetaMoveDispatcher` alongside.

## Relation to `module_EGM` (GoHolo)

MetaMove has its own `MetaEGM_Init`/`RunPose`/`Stop` procedures using the
`MetaMoveUC` device, so the existing GoHolo `module_EGM` (using `UCdevice` /
`UCstream`) stays intact and usable independently.
