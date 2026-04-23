# Safety Zone + Spatial Anchor Drift Test

End-to-end validation of Meta Quest spatial anchor stability using MRUK QR-code tracking
and a physical surrogate (3D-printed mock-up or real GoFa base).

Relates to HANDOFF.md steps 8 (ArUco/QR + Spatial Anchor) and 10 (Safety Zones).

## What this test validates

- **Spatial anchor stability**: does the anchored CAD stay locked to the real object while
  the user walks around the workspace, or does it drift?
- **Dynamic safety zone rendering**: does a TCP-following safety zone sit correctly in the
  real world when viewed via passthrough?
- **QR-based auto-match**: does Meta MRUK detect the printed QR code and snap the CAD onto
  the real object in <1 s?

## What's auto-built vs. manual

| Task | Who | Status |
|---|---|---|
| QR code generation | script | ✅ `docs/markers/METAMOVE_ROBOT_BASE_01*` |
| Print-ready PDF | script | ✅ `docs/markers/METAMOVE_ROBOT_BASE_01_print.pdf` |
| C# components (SafetyZone, Binder, Calibrator, ProximityHaptics, HUD) | script | ✅ `Assets/MetaMove/Scripts/Safety/` |
| Android manifest permissions | already in repo | ✅ `USE_SCENE`, `USE_ANCHOR_API` |
| `Scene_SafetyAnchorTest.unity` | Editor menu | ⏳ run `MetaMove > Setup Safety Anchor Test Scene` |
| `AnchoredRobotBase.prefab` | Editor menu (auto) | ⏳ same menu builds it if missing |
| MRUK QR tracking toggle | **you** in Inspector | ⏳ manual — see below |
| Build Settings entry | **you** | ⏳ manual |
| Print + attach marker | **you** | ⏳ manual |

## Steps

### 1. Regenerate Unity assets

In Unity: **MetaMove → Setup Safety Anchor Test Scene (overwrite)**.

This creates:
- `Assets/MetaMove/Scenes/Playground/Scene_SafetyAnchorTest.unity`
- `Assets/MetaMove/Prefabs/AnchoredRobotBase.prefab` (if missing)

Both are overwritable — safe to re-run after code changes.

### 2. Enable QR tracking on MRUK

In the created scene, select `MRUK` in the Hierarchy. In the Inspector, find
`MRUK (Script) → Scene Settings → Tracker Configuration → QR Code Tracking Enabled = ✅`.

(The rest of `Tracker Configuration` can stay at defaults.)

### 3. Add to Build Settings

`File → Build Settings → Add Open Scenes`. Ensure target is Android.

### 4. Print the marker

Print `docs/markers/METAMOVE_ROBOT_BASE_01_print.pdf`:
- **100% scale, no fit-to-page** (printer dialog — uncheck "Fit to printable area")
- **White paper, matte** if possible (reduces glare under room lights)
- After printing, measure the calibration scale under the QR with a ruler. Should be exactly
  **100 mm**. If off by >1 mm, re-print with correct scale — pose accuracy depends on it.

### 5. Attach the marker

Stick the printed sheet flat onto your test object at a **known, repeatable pose**.

For the 3D-print GoFa surrogate:
- Center the QR on the base plate
- QR Z-axis (the outward normal) = GoFa base up-axis
- QR Y-axis = GoFa base "forward" (toward Axis 1 zero direction)
- If CAD origin is not at QR center: measure the offset and set `payloadPositionOffset`
  on the `QrCalibrator` in the scene

For the real GoFa: same idea, flat magnetic plate on base top works well.

### 6. Test

Build & Run to Quest OR Play Mode via Quest Link:

1. App starts, Quest prompts for Scene permission → Accept
2. Look at the QR marker for ~1 second
3. Console: `[QrAnchorCalibrator] QR 'METAMOVE_ROBOT_BASE_01' detected ...`
4. Then: `Anchor committed ...` — the CAD prefab spawns exactly on the real object
5. Remove the QR marker — CAD stays put (Quest SLAM holds the anchor)
6. Walk around the workspace, observe the HUD for drift

### 7. Measure drift

**Modus A — Baseline capture (no extra hardware)**:
- When CAD visually aligns to the real object, press `B` on the keyboard (Quest Link focus)
- HUD logs live drift vs. that baseline
- Walk for 5–10 minutes, note max drift

**Modus B — With a second QR reference marker (optional, for absolute ground truth)**:
- Place a second QR (different payload, e.g. `METAMOVE_DRIFT_REF`) at a fixed world location
- Add a second `QrAnchorCalibrator` listening for that payload
- Compare distance CAD-feature ↔ reference-marker-world-pose frame by frame

## Components reference

| Script | Path | Purpose |
|---|---|---|
| `SafetyZone` | `Assets/MetaMove/Scripts/Safety/SafetyZone.cs` | Box/Sphere/Capsule zone, optional `followTarget` |
| `SpeedScaler` | `Assets/MetaMove/Scripts/Safety/SpeedScaler.cs` | Evaluates zones vs TCP, produces `Factor ∈ [0,1]` |
| `QrAnchorCalibrator` | `Assets/MetaMove/Scripts/Safety/QrAnchorCalibrator.cs` | MRUK QR detection → OVRSpatialAnchor + prefab spawn |
| `AnchoredBaseBinder` | `Assets/MetaMove/Scripts/Safety/AnchoredBaseBinder.cs` | CAD pivot offset tuning |
| `ZoneProximityHaptics` | `Assets/MetaMove/Scripts/Safety/ZoneProximityHaptics.cs` | bHaptics pulse escalates with zone proximity |
| `AnchorDriftHud` | `Assets/MetaMove/Scripts/Safety/AnchorDriftHud.cs` | On-screen drift telemetry |

## Known gotchas

- **`MRUK.Instance` null on Awake**: MRUK takes a frame to initialize. `QrAnchorCalibrator`
  polls `GetTrackables()` each frame, so this is tolerated.
- **QR detected but pose jumps around**: printed marker not flat (wavy paper). Laminate or
  tape under glass.
- **Anchor placed but CAD rotated 90°/180°**: the QR Z-axis convention (out of the sheet)
  may not match your CAD forward. Fix via `payloadEulerOffset` on `QrCalibrator`.
- **`QRCodeTrackingSupported` returns false**: Quest firmware too old — needs v74+ (check
  Settings → System → Software Update on the device).

## Regenerating the marker

Different payload or size? Edit `docs/markers/generate.py` (or run inline):

```bash
cd docs/markers
python -c "import qrcode; from qrcode.constants import ERROR_CORRECT_H; \
  qr = qrcode.QRCode(version=None, error_correction=ERROR_CORRECT_H, box_size=40, border=4); \
  qr.add_data('YOUR_PAYLOAD_HERE'); qr.make(fit=True); \
  qr.make_image().save('YOUR_PAYLOAD_HERE.png')"
```

Remember to update `QrAnchorCalibrator.expectedPayload` to match.
