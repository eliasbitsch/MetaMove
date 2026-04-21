# UI Wiring Guide — L1 Radial / L2 Panels / L3 Fixtures

Scripts are in place (`Assets/MetaMove/Scripts/Settings|UI|Haptics/`). This doc is the checklist for **what YOU do in the Unity Editor** to bring the 3-Layer UI to life.

See [docs/ui-panels.md](../../../docs/ui-panels.md) (repo root) for the full catalog + Meta asset inventory.

---

## 0. Prerequisites

Packages (Window → Package Manager, Add by name):
- `com.meta.xr.sdk.interaction` (already in project)
- `com.meta.xr.sdk.interaction.samples` — ships `UISet`, `ComprehensiveSample`, `PokeExamples`
- `com.meta.xr.mrutilitykit` — MRUK for floor/table anchors
- (later) `com.meta.xr.sdk.movement` — Body tracking for Step 19
- (later) bHaptics Unity plugin from bhaptics.com Developer Portal

---

## 1. Create ScriptableObject assets (one-click each)

Right-click in `Assets/MetaMove/Settings/` → **Create → MetaMove → Settings → …**
Create one asset of each:

- `UiThemeConfig` → name `UiTheme.asset`
- `RobotConnectionConfig` → name `RobotConnection.asset` (already has `.1`, `.99`, VC preset)
- `SafetyConfig` → name `Safety.asset`
- `HapticsConfig` → name `Haptics.asset`

These are Data Singletons — every panel references one of them in the Inspector.

---

## 2. Scene structure (recommended split per Step 2b)

Create one Bootstrap scene + feature scenes under `Assets/MetaMove/Scenes/`:

```
Bootstrap.unity       ← entry point, loads others additively
Scene_Robot.unity     ← GoFa + Ghost + Spatial Anchors + Pedestal
Scene_UI.unity        ← Radial + PanelManager + panel prefabs
Scene_Safety.unity    ← Zones + E-Stop fixture
Playground/PinchDragGoFa.unity   ← move the existing sample here as archive
```

Bootstrap.unity has one GO `Bootstrap` with a small script that does
`SceneManager.LoadSceneAsync("Scene_Robot", Additive)` etc. (That script is not generated —
one-liner, easy for you to add when needed.)

---

## 3. L3 Physical Fixtures (Scene_Robot)

### 3a. Glass Pedestal + GoFa Twin
- Drop `Desk.prefab` (MR Template) into the scene — it's the base
- Create empty `GlassPedestal` GO on top of the desk
- Attach `GlassPedestal.cs`, assign `robotTwinRoot` = your GoFa prefab root
- Assign `topSurface` = an empty transform on the desk top at the center
- Material: apply URP Lit Transparent, Smoothness 0.95, tint `#3DA5FF` @ 8%, enable Emission
- Optional: two-hand scale — add `Grabbable` + `TwoGrabScaleTransformer`, wire its scale output to `GlassPedestal.SetScale`

### 3b. Physical E-Stop Mushroom
- Duplicate `[BB] Pokeable Plane` from Meta Building Blocks, OR copy `PokeButton.prefab` from `PokeExamples.unity`
- Replace the visible plane with a cylinder base + mushroom cap mesh (red Metallic 0.8, Smoothness 0.6)
- Attach `EmergencyStopFixture.cs` to the root
- Assign `mushroomCap` (the cap transform), `capRenderer` (the cap mesh renderer)
- Wire Meta's `PokeInteractable` events:
  - `WhenSelect` → `EmergencyStopFixture.OnPressBegin`
  - `WhenUnselect` → `EmergencyStopFixture.OnPressEnd`
- Hook `onEmergencyStop` → `SpeedScaler.onHardStop` (or directly `SafetyPanel.TriggerEmergencyStop`)
- For twist-reset: add a small child with `Grabbable` + `OneGrabRotateTransformer`, on completed rotation call `ResetLatch()`

### 3c. Floor Grid
- Drop MRUK `FloorAnchor` (or a manual quad at y=0)
- Attach `FloorGridFixture.cs`
- Assign `floorRenderer` = the quad's renderer
- `userAnchor` = OVRCameraRig root (or leave null → falls back to Camera.main)
- Create a URP Unlit shader graph that takes `_UserWorldPos` + `_FadeRadius` properties and a procedural grid

---

## 4. L1 Radial Menu (Scene_UI)

### 4a. HandRadialMenu GO
- Create empty `HandRadialMenu` under a parent anchored to the left hand
- Attach `HandRadialMenu.cs`
- Assign `theme` = UiTheme.asset
- Assign `handAnchor` = left palm-center transform (from `OVRInteractionComprehensive` hand skeleton)
- Fill `wedges` array with 8 entries:

| # | id | label | targetPanelId |
|---|---|---|---|
| 0 | status | Status | `dashboard` (Dashboard opens with Status-Tab preselected — set default-tab in MainDashboardPanel) |
| 1 | control | Control | `teleop` |
| 2 | path | Path | `waypoints` |
| 3 | safety | Safety | `safety` |
| 4 | motors | Motors | `motor-loads` |
| 5 | body | Body | `body` |
| 6 | voice | Voice | `voice` |
| 7 | system | System | `system` |

For MVP only wire `status` / `safety` targets; rest can stay empty until their panels exist.

### 4b. Wedge prefab
- Create a small world-space Canvas prefab (~60mm square) with:
  - `RadialMenuItem.cs`
  - `Image` backdrop (rounded, assigned to `backdrop` field)
  - `Image` icon (assigned to `iconImage`)
  - `TextMeshProUGUI` label (assigned to `labelText`)
  - Meta `PokeInteractable` (or `HandGrabInteractable`) on the backdrop
  - Wire `WhenSelect` → `RadialMenuItem.Activate`
  - Wire `WhenHover` → `RadialMenuItem.OnHoverEnter`, `WhenUnhover` → `OnHoverExit`
- Assign prefab to `HandRadialMenu.wedgePrefab`

### 4c. Palm-up detection
- In the scene: add `ActiveStateSelector` + `ShapeRecognition` (Meta SDK) on the left hand, recognizer preset `PalmFacingHead` or author a custom hand-pose asset
- Wire `WhenSelected`/`WhenUnselected` → `HandRadialMenu.SetPalmOpen(true/false)`

---

## 5. L2 Panels (Scene_UI)

### 5a. PanelManager GO
- Create `PanelManager` GO, attach `PanelManager.cs`
- Don't register panels yet — they come via prefabs in step 5c

### 5b. WorldPanelBase prefab
Build one reusable prefab `Prefabs/UI/WorldPanelBase.prefab`:

- Root: empty GO with `WorldPanelBase.cs`
- Child: `Canvas` (World Space), scale 0.001
  - Child `TopBar`: `HandGrabInteractable` + `Grabbable` + `OneGrabTranslateTransformer` — panel is translatable by grabbing the top bar
  - Child `Content`: `PointableCanvas` + `RayInteractable` for buttons/sliders
  - Child `CloseButton` (IconButton from Meta UISet) → onClick → `WorldPanelBase.Close`
  - Child `PinButton` (IconButton) → onClick → `WorldPanelBase.TogglePin`
  - Child `MinimizeButton` (IconButton) → onClick → `WorldPanelBase.ToggleMinimize`
- **Important:** `OneGrabTranslateTransformer`, NOT `OneGrabFreeTransformer`. `WorldPanelBase.LateUpdate` also re-locks rotation every frame as belt-and-braces.

### 5c. Concrete panel prefabs (derive from WorldPanelBase prefab as variants)

For each of the **MVP 4 panels**:

**Main Dashboard** (`Prefabs/UI/Panel_MainDashboard.prefab`)
- Variant of WorldPanelBase prefab
- Replace the script on the root with `MainDashboardPanel.cs` (inherits WorldPanelBase)
- Add Tab-Bar row below top-bar with 4 `Button_Primary` (Meta UISet): Status / Control / Path / Safety
- Add Content-Area with 4 child GOs (one per tab, toggled by MainDashboardPanel.SelectTab)
- Add a persistent `Button_Destructive_Large` footer labeled "E-STOP" — onClick → `MainDashboardPanel.TriggerEmergencyStop`
- Configure `tabs[]` array in inspector: pair each header button with its content GO

**Connection Panel** (`Prefabs/UI/Panel_Connection.prefab`)
- Variant of WorldPanelBase
- Attach `ConnectionPanel.cs`
- UI inside Content-Area:
  - `TMP_Dropdown` endpointDropdown (Meta UISet Dropdown)
  - `TMP_Dropdown` modeDropdown
  - `TMP_InputField` customIpField (bring up Meta `[BB] Virtual Keyboard` on focus)
  - `Button` connectButton (Primary)
  - `Button` disconnectButton (Secondary)
  - 4× `Image` LEDs (ledEgm/ledRws/ledRos/ledMoveIt)
  - `TMP_Text` latencyLabel
- Assign `config` = RobotConnection.asset, `theme` = UiTheme.asset, `egm` = scene's EgmClient

**Telemetry Panel** (`Prefabs/UI/Panel_Telemetry.prefab`)
- Attach `TelemetryPanel.cs`
- 6 joint rows: each with `TMP_Text` label + radial `Image` (filled type) — assign to jointLabels[] and jointFills[]
- TCP grid: 6 TMP_Text labels assigned to tcpX/Y/Z/Rx/Ry/Rz
- Hz-Label at bottom
- Assign `telemetry` = scene RobotTelemetry, `limits` = JointLimits_GoFa5_95.asset

**Safety Panel** (`Prefabs/UI/Panel_Safety.prefab`)
- Attach `SafetyPanel.cs`
- ScrollView for zone list → content root assigned to `zoneListRoot`, row prefab = a Meta UISet `Toggle` → `zoneRowPrefab`
- Sliders for speed cap + separation
- Dropdown for ISO mode
- Big `Button_Destructive_Large` → `onClick` → `SafetyPanel.TriggerEmergencyStop`
- Assign `config` = Safety.asset, `speedScaler` = scene SpeedScaler, `zones` = drag Safety Zone GOs

### 5d. Register panels with PanelManager
- In scene, select `PanelManager`
- Fill `panels` array:
  - `dashboard` → Panel_MainDashboard
  - `connection` → Panel_Connection
  - `telemetry` → Panel_Telemetry
  - `safety` → Panel_Safety

---

## 6. Haptics (Scene_UI)

- Create `BHapticsAdapter` GO, attach `BHapticsAdapter.cs`
- Assign `config` = Haptics.asset
- Until bHaptics SDK is imported this runs as no-op stub (logs once)
- Wire example sources:
  - `GestureRouter.onRightPinchBegin` → `BHapticsAdapter.PlayPinchTap`
  - `GestureRouter.onRightOkRingBegin` → `BHapticsAdapter.PlayCommit`
  - `SafetyZone` breach event → `BHapticsAdapter.PlaySafetyViolation`

---

## 7. Visualization (Scene_Robot)

### DistanceRuler
- Empty GO `DistanceRuler`, attach `LineRenderer` + `DistanceRuler.cs`
- Assign `source` = left hand anchor (or head)
- Assign `target` = robot base spatial anchor transform
- Assign `lineMaterial` = new URP Unlit material, white, Emission on
- Create a TMP world-space text child → assign to `distanceLabel`
- Toggle via Overlays panel

### PathPreviewRenderer
- Empty GO under the waypoint system, attach `LineRenderer` + `PathPreviewRenderer.cs`
- Assign materials, `theme` = UiTheme.asset
- Feed waypoints by calling `SetWaypoints(positions, ikScores)` from your WaypointManager
- Optional: Assign `scrubGhost` = a ghost TCP marker and toggle `scrubEnabled` from the Path panel

---

## 8. Sanity check

1. Enter Play Mode with the MR Template Link setup
2. Console should be clean (0 errors, 1 expected warning: `[BHapticsAdapter] stub active`)
3. Show left palm → radial appears after ~0.2 s dwell, 8 wedges visible
4. Pinch the "Status" wedge → Main Dashboard panel spawns in front of you
5. Try to rotate the panel by grabbing the top bar and spinning → it resists (lockRotation works)
6. Press the Physical E-Stop → cap depresses, mushroom latches, onEmergencyStop fires
7. Twist the cap → `ResetLatch` called, cap springs back

---

## What's NOT yet wired (post-MVP)

- Panels for Teleop / Waypoint List / Path Exec / Motors / Voice / Body / Demo — scripts exist only as part of the script namespace; more panel scripts to come
- 3D Glass Poke-Buttons (F4) — use Meta PokeButton.prefab + URP Glass material when needed
- Curved Scroll Lists (F3) — wrap the ScrollView in `CanvasCylinder` when lists get long
- Spatial Anchor Pucks (F5) — use the sample visualizer from `[BB] Spatial Anchor`
- Body Tracking Panel (Step 19) — needs `com.meta.xr.sdk.movement` first
