# UI Panels Catalog — MetaMove / GoHolo Quest 3

**Design-Prinzip (verbindlich):** So Meta-SDK-native wie möglich. Jedes UI-Element ist ein Meta XR Interaction SDK Building Block oder ein UI-Set-Prefab. Custom-Code nur wo Meta nichts Passendes bietet (z.B. Torque-Bar-Graph mit URP-Shader, Radial-Layout-Math).

## UI-Leitbild: Immersive First, Panels Minimal

**Primäre Info-Quelle = In-World.** Daten die während Live-Arbeit am Roboter relevant sind stehen räumlich direkt am Geschehen. Panels sind klein (≤ 240×300 mm) und nur für Config-Tasks, nie als Dashboards.

**Persistenz-Hierarchie (default an, toggelbar per Robo-Info-Panel):**

| Element | Default | Trigger | Toggle-Ort |
|---|---|---|---|
| **Curved HUD** | ON | Permanent vor User, Radius 0.8 m | Radial → System → HUD close / Robo-Info toggle |
| **Joint Compass-Arcs** | ON | Ring + Tick + Nadel am Joint mit Farbwechsel bei Limits | Robo-Info-Panel |
| **Torque-Farben am Joint-Mesh** | ON | Material-Overlay grün→gelb→rot | Robo-Info-Panel |
| **TCP-Pose-Label** | ON | TMP floating am TCP | Robo-Info-Panel |
| **Safety-Zones** | ON | Semi-transparente Zone-Meshes | Safety-Mini-Panel |
| **Distance-Ruler** | **Proximity-triggered** | Fade-in wenn Hand/TCP < 30 cm an Zone/Robo, wird rot bei < 15 cm | Robo-Info-Panel overrides |
| **Path-Preview** | OFF | Nur wenn Path-Mini-Panel offen oder expliziter Toggle | Path-Mini-Panel |
| **Ghost-Robot** | Nur beim Editieren | Auto-spawn bei Joint-Drag / Waypoint-Add | System-Logik |
| **Working Envelope** | OFF | Toggle für seltene Planungs-Checks | Robo-Info-Panel |

## 3-Layer UI Architecture

Alle UI zerfällt in drei Ebenen, jede mit klarer Persistenz und Trigger:

| Layer | Was | Persistenz | Trigger |
|---|---|---|---|
| **L1 — Home (Radial)** | Hand-anchored Launcher, **App Home-Screen** | Auf Abruf, folgt der Hand | Palm-Up-Geste (Inner-Palm) |
| **L2 — Floating Panels** | Tab-Navigation, Daten, Config, Listen (14 Panels weiter unten) | World-fixed, spawn/dismiss on demand | Radial-Wedge-Klick |
| **L3 — Physical Fixtures** | Permanente 3D-Objekte im Raum (E-Stop, Glas-Sockel, etc.) | Raum-fix, immer da | Spawn beim Bootstrap, nie "geschlossen" |

**Regel Meta-Native:** **Jedes L2- und L3-Element referenziert ein Meta Building Block oder Interaction SDK Sample-Prefab.** Custom-Code nur für Layout-Math (Radial-Wedges) und URP-Shader. Siehe Meta-Asset-Inventar am Ende dieser Doku.

## Radial-Wedges: Panel-Opener ODER Toggle — oder beides

Jeder Wedge hat im Inspector zwei Slots:

| Feld | Zweck |
|---|---|
| `targetPanelId` | Öffnet das zugehörige Panel per `PanelManager.OpenExclusive`. Leer lassen für reinen Toggle-Wedge. |
| `onActivate` (UnityEvent) | Beliebige Funktion(en) — Toggle, Action, API-Call, etc. Gemischt mit `targetPanelId` möglich. |

**Häufige Overlay-Toggles sind über `OverlayToggleHub.cs`-Methoden erreichbar** — per Drag-Drop im UnityEvent-Editor:
- `ToggleGhost`, `ToggleWorkingEnvelope`, `ToggleHud`, `ToggleDistanceRuler`, `ToggleTorqueOverlay`, `TogglePathPreview`, …

**Empfohlene Default-Radial-Belegung (gemischt Panel-Opener + Toggle):**

| Wedge | Label | Action |
|---|---|---|
| 1 | Status | Panel `status-dashboard` |
| 2 | Paths | Panel `paths` |
| 3 | Position | Panel `precise-position` |
| 4 | Safety | Panel `safety` |
| 5 | **Ghost** | Toggle `OverlayToggleHub.ToggleGhost` (kein Panel) |
| 6 | **Envelope** | Toggle `OverlayToggleHub.ToggleWorkingEnvelope` |
| 7 | **HUD** | Toggle `OverlayToggleHub.ToggleHud` — HUD schließen/öffnen |
| 8 | Robo-Info | Panel `robo-info` (Sammlung aller Overlay-Checkboxes) |

Damit sind die 3 meistgenutzten Overlays als 1-Klick-Toggle auf dem Rad, die restlichen kommen feiner im Robo-Info-Panel.

**Customizable by user:** im Inspector des HandRadialMenu kannst du wedges tauschen ohne Code — jeder onActivate-Event ist per Drag-Drop belegbar. Du baust dein eigenes Quick-Action-Set.

## Radial als Home-Screen (Revised L1)

Nicht nur Quick-Actions — echter App-Launcher. Öffnet immer bei Palm-Up, **8 Wedges = 8 Sektionen**:

| Wedge | Icon | Öffnet |
|---|---|---|
| **Status** | Monitor | Dashboard → Status-Tab (Connection + Telemetry) |
| **Control** | Joystick | Teleop-Panel + spawnt In-World-Arcs visible |
| **Path** | Route | Waypoint-List + Path-Exec-Panels |
| **Safety** | Shield | Safety-Panel (E-Stop doppelt: persistent als L3-Fixture + Panel-Button) |
| **Motors** | Gauge | Motor-Loads-Panel |
| **Body** | User | Ergonomics-Panel (Step 19) |
| **Voice** | Mic | Voice-Panel (Step 13) |
| **System** | Gear | Twin / Envelope / Overlays / Demo / Theme |

**Center des Radials:** runde Status-Kachel mit aktuellem Robot-Mode + Connection-LED + Current-Panel-Badge (zeigt welcher Panel-Set gerade offen ist). Click = schließt alle offenen Panels (Panic-Clear).

**Unterebene:** wenn eine Sektion mehrere Panels hat, öffnet das Dashboard **mit dem passenden Tab vorselektiert**. User lernt: Radial → Sektion → Dashboard mit Tab. Immer dieselbe mentale Map.

**Implementation:** Wedges sind `PokeInteractable` + custom Mesh (Pie-Slice), zentrale Status-Kachel ist `PokeInteractable` auf Disc. Palm-Detection via `ActiveStateSelector` + `ShapeRecognition` (Meta-BB). **Nur Layout-Math custom** (Polar → Position), Rest Meta-nativ.

## Dual-Mode: In-World Direct Manipulation + Panels

**Panels ersetzen NICHT die immersive Interaktion — sie ergänzen sie.** Primäre Steuerung bleibt in-world direkt am Roboter-Hologramm (pinch, drag, poke). Panels liefern strukturierten Zugriff auf Konfiguration, datenintensive Views, Listen und Workflows die in der Luft zu fummelig wären.

| Aktion | Primär (In-World) | Panel-Ergänzung |
|---|---|---|
| End-Effektor bewegen | Pinch auf `IKHandle` (HandGrab Step 9) | Teleop-Panel: Sensitivity, Axis-Locks, Frame |
| Joint drehen | Arc-Knob-Pinch am Joint (Step 15a, `ArcKnobAffordance`) | Joint Control-Panel: präziser Slider + typed Value + Reset |
| Torque sehen | **Joint-Mesh-Farbe direkt am Roboter** (Step 14a, `ShowTorque.cs`) | Motor-Loads-Panel: Bar-Graphs + Historie + Export |
| Joint-Winkel sehen | **Gauge floating am Joint** (Step 14b, `ShowAngles.cs` / HoloLens `gaugeAngle.cs`) | Telemetry-Panel: kompakte 6-Gauge-Übersicht |
| TCP-Pose sehen | **Label floating am TCP** (Step 14c, `ShowPose.cs`) | Telemetry-Panel: XYZ+RPY numerisch groß |
| Waypoint setzen | **Pinch-Platzierung in der Luft** (Step 15b) | Waypoint-Panel: Liste + Reorder + Edit |
| Ghost-Path committen | **OK-Ring-Geste** (Step 9) | Path-Exec-Panel: Commit-Button als Fallback |
| Safety-Zone anpassen | **Corner-Handles im Raum greifen** (Step 10) | Safety-Panel: On/Off pro Zone, globale Limits |
| Overlay an/aus | Palm-Radial Quick-Toggle | Overlays-Panel: alle Checkboxen feingranular |
| Envelope ein/ausblenden | Palm-Radial | Envelope-Panel: Style, Payload, Color |

**Faustregel:**
- Wenn die Aktion auf einer räumlichen Position/Geometrie operiert → **primär in-world** (pinch, drag, grab direkt dort wo's passiert). Panel ist optional für Config
- Wenn die Aktion auf Config / numerischen Werten / Listen / Logs operiert → **primär Panel**
- Live-Daten die räumlich-relevant sind (Torque pro Joint, Winkel pro Joint) → **beides gleichzeitig anzeigen**: in-world als Kontext, Panel als Detail-View. Keine doppelte Arbeit — Scripts schreiben ihre Werte in ein gemeinsames `RobotTelemetry`-Singleton, In-World- und Panel-Views lesen beide daraus

## Globale Regeln

- **Panel-Base**: alle World-Panels teilen `_GoHolo/Prefabs/UI/WorldPanelBase.prefab` + Script `WorldPanelBase.cs`
- **Grab-Verhalten**: `Grabbable` + `OneGrabTranslateTransformer` (Translation only). KEIN `OneGrabFreeTransformer` weil der Rotation mitnimmt. Rotation am Panel ist explizit **verboten** — Panels orientieren sich beim Spawn zur Kamera und bleiben fix bis zum nächsten Spawn
- **Grab-Zonen**: near-grab via `HandGrabInteractable` am Panel-Border (Top-Bar). Far-grab via `DistanceHandGrabInteractable` am selben Border. Content-Area hat **keinen** Grabbable — sonst frisst Grab die Button-Clicks
- **Content-Interaktion**: `PointableCanvas` + `RayInteractable` + `CanvasCylinder` oder flach. Buttons/Toggles/Slider aus Meta UI Set (`com.meta.xr.sdk.interaction.samples` → `UISet`)
- **Close/Pin/Minimize**: IconButtons oben rechts, 3 Stück, aus UI-Set
- **Spawn-Position**: bei "Open Dashboard" 0.8 m vor Kamera, Eye-Height, Kamera-Facing. Neu-Spawn re-orientiert
- **Theme**: einheitlich dunkles Glas (BackdropBlur aus Meta Samples), weiße Typografie (TextMeshPro), Accent-Farbe GoHolo-Blue (`#3DA5FF`), Destructive-Rot (`#E24A4A`), Success-Grün (`#4ADE80`)
- **Icons**: bevorzugt Meta UI-Set-Icons; fehlende aus [Lucide](https://lucide.dev) (MIT), als SVG → PNG@2x-Import in `_GoHolo/Icons/`
- **Scrolling**: `PointableCanvas` + Unity `ScrollRect` + Meta-Sample-`ScrollableCanvas` für Finger-Scroll-Pattern

## Meta UI Set → Element Mapping

| Bedarf | Meta-Component | Prefab |
|---|---|---|
| Primary Button | `Button_Primary_Large` | UI-Set |
| Secondary Button | `Button_Secondary_Large` | UI-Set |
| Destructive (E-Stop, Delete) | `Button_Destructive_Large` | UI-Set |
| Icon-only Button | `IconButton` | UI-Set |
| Toggle (on/off) | `Toggle` | UI-Set |
| Checkbox (in Liste) | `Checkbox` | UI-Set |
| Slider (kontinuierlich) | `Slider` | UI-Set |
| Dropdown | `Dropdown` | UI-Set |
| Text-Input | `TextField` | UI-Set |
| Tab-Header | `Button_Primary` mit Selected-State | Custom-Group |
| List-Item (clickable) | `Button_Secondary` + Custom-Layout | Custom |
| ScrollView | Unity ScrollRect + Meta `ScrollableCanvas` | Meta Sample |
| Progress-Bar | Unity Image mit Filled-Typ | Custom |
| Radial-Menu-Wedge | Custom (`RadialMenuItem.cs`) | Custom |

## Radial Menu (Step 18, Hand-Anchored)

Palm-Up Inner-Palm Detection via `ActiveStateSelector` + `ShapeRecognition` (Meta Hand-Pose: Palm-Facing-Head). Spawnt 6–8 Wedges um Hand-Zentrum, 8 cm Radius.

**Quick-Action-Kacheln** (einstellbar):
1. **HOME** (Haus-Icon) — Roboter in Park-Pose
2. **E-Stop** (Stop-Icon, rot) — Emergency-Stop (Step 10)
3. **Simulate** (Play-Ghost-Icon) — Simulate-Wizard (Step 15c)
4. **Commit** (Check-Icon) — aktueller Ghost-Pfad committen
5. **Voice** (Mikrofon-Icon) — Voice-Input an/aus (Step 13a)
6. **Overlays** (Layers-Icon) — Quick-Toggle häufigster Overlays
7. **Waypoint Add** (Pin-Plus-Icon) — aktuellen TCP als Waypoint speichern
8. **Open Dashboard** (Grid-Icon) — Main-Panel spawnen

Auswahl: Pinch der anderen Hand auf Wedge, oder Gaze-Hover + Thumb-Tap. Wedge leuchtet GoHolo-Blue bei Hover, Commit-Pulse + bHaptics-Tick (Step 16) bei Activate.

---

## Panel-Katalog (14 Panels)

### 1. Main Dashboard (Hub)

**Zweck:** Navigation zu allen Sub-Panels via Tabs. Öffnet bei "Open Dashboard"-Radial-Kachel.

**Elemente:**
- Tab-Leiste oben: Status / Control / Path / Safety / Motors / Body / Voice / System
- Content-Area wechselt je Tab — pro Tab embedded ein Sub-Panel-Prefab
- Top-Bar: Robot-Status-LEDs (EGM / RWS / ROS / Body-Track) + Close/Pin-Icons

**Layout:** 60 × 40 cm, Canvas-Scale 0.001, ~1 m Abstand ideal.

---

### 2. Connection & Status Panel

**Elemente:**
- IP-Dropdown: `192.168.125.1` / `192.168.125.99` / Custom
- Custom-TextField (erscheint bei Custom-Auswahl)
- RWS-Port TextField (default 443)
- Connect / Disconnect Primary-Button
- Status-LEDs (custom Dots): EGM UDP ✓ / RWS HTTPS ✓ / ROS Bridge ✓ / MoveIt ✓
- Latency-Readout (ms, live)
- Mode-Dropdown: Real-GoFa / Virtual-Controller / Offline-Mock

**Nativ:** Dropdown, TextField, Button_Primary, custom Status-LED-Widget (Image mit Material-Swap).

---

### 3. Telemetry / Readout Panel

**Elemente:**
- 6 Angle-Gauges (kreisförmig, per Joint, aktueller Winkel + Min/Max-Range) — Port aus HoloLens `gaugeAngle.cs`, reused in `ShowAngles.cs`
- TCP-Pose-Readout: X/Y/Z (mm) + RPY (°), TextMeshPro-Grid 2×3
- Torque-Bars 6-stack, farbcodiert (nutzt `ShowTorque.cs`)
- Joint-Status-Icons (OK / Limit-nah / Fault) — reused `ShowJointStatusText.cs`

**Nativ:** reine Read-Only-Views, Layout aus Unity-Image + TMP. Keine Interaktion außer Top-Bar.

---

### 4. Joint Control Panel

**Elemente:**
- 6 Horizontal-Slider (J1–J6), min/max aus `JointLimits.cs`
- Live-Ist-Marker (kleiner Strich) auf Slider zeigt aktuelle Pose
- Numerisches Readout rechts vom Slider (° mit 1 Decimal)
- Reset-to-HOME-Button pro Joint (IconButton)
- Master-Reset-All Primary-Button
- **Achtung:** Slider-Bewegung editiert Ghost, nicht IST (Step 9-Architektur). Commit-Button unten oder via OK-Ring

**Nativ:** `Slider` UI-Set ×6, `IconButton` ×6, `Button_Primary` ×1.

---

### 5. Waypoint List & Path Editor Panel

**Elemente:**
- ScrollView mit Waypoint-Items (Zeilen)
- Pro Zeile: Drag-Handle-Icon (reorder), Index-Badge, Name-Label (TextField editierbar), Farbpunkt IK-Status, Goto-Icon, Delete-IconButton
- Unten: "Add at TCP" Primary-Button, "Add at Hand" Secondary-Button, "Clear All" Destructive-Button
- Header: Path-Name + Save/Load-Dropdown

**Nativ:** `ScrollableCanvas`, Custom-List-Item-Prefab (Button_Secondary als Row-Click + IconButtons + TextField).

---

### 6. Path Execution Panel

**Elemente:**
- Großer Run-Button (Primary, grün, mit Play-Icon)
- Simulate-Button (Secondary, Ghost-Play-Icon)
- Pause / Resume Toggle
- Cancel-Button (Destructive)
- Speed-Slider 10–100 %
- Loop-Toggle
- Progress-Bar aktueller Waypoint (3/12) + Progress-Fill
- ETA-Readout

**Nativ:** Button-Set, Slider, Toggle, Progress-Bar (Custom Image).

---

### 7. Safety Panel

**Elemente:**
- Zone-Liste: pro Zone Toggle (active), "Edit Corners"-Button (spawnt Zone-Handles in Scene, Step 10)
- Global Speed-Limit-Slider (mm/s, ISO/TS 15066)
- Separation-Distance-Slider (mm)
- ISO-Mode-Dropdown: SSM / PFL / Hand-Guided / Off
- **E-Stop**: großer Destructive-Button, oben, immer sichtbar, auch wenn Tab anderswo
- Stop-Reason-Log (letzte 5 Events)

**Nativ:** Toggle, Slider, Dropdown, `Button_Destructive_Large`.

---

### 8. Overlays Panel

**Checkbox-Liste:**
- Torque-Color-Overlay (Step 14a)
- Joint-Angle-Gauges in-scene (Step 14b)
- TCP-Pose-Label (Step 14c)
- Working Envelope (Step 14d)
- Distance Ruler (Step 17a)
- Path Preview (Step 17b)
- Ghost-Robot (Step 9)
- Safety-Zones visible
- Body-Skeleton (Step 19)

Plus: Ghost-Opacity-Slider, Skeleton-Opacity-Slider, Master-Opacity-Slider.

**Nativ:** `Checkbox` ×9, `Slider` ×3.

---

### 9. Voice / LLM Panel

**Elemente:**
- Wake-Word-Toggle ("Hey GoFa")
- Language-Dropdown (Meta Voice SDK locales)
- Transcript-ScrollView (Live-Stream der STT)
- "Confirmed Plan"-Box (parsed JSON vom Claude-Client, lesbar formatiert)
- Confirm / Cancel (Primary + Destructive-Buttons) — triggert Ghost-Move oder Discard
- Clear-History-IconButton

**Nativ:** Toggle, Dropdown, `ScrollableCanvas`, Button-Set.

---

### 10. Demo & Presets Panel

**Elemente:**
- Saved-Paths-ScrollView: Row = Name + Waypoint-Count + Load/Save/Delete-IconButtons
- "Save Current" Primary-Button
- Import-from-File IconButton (liest aus `persistentDataPath/paths/`)
- Demo-Path Play-Icon (entspricht Step 15d)

**Nativ:** ScrollableCanvas + Row-Prefab.

---

### 11. Teleop Panel (Use-Case 1 — Pinch-to-Move)

**Elemente:**
- Großer Enable-Toggle (groß, Status-LED grün/rot)
- Sensitivity-Slider (1:1 bis 1:10 Scale)
- Axis-Lock-Gruppe (6 Toggles): X / Y / Z / RX / RY / RZ
- Reference-Frame-Dropdown: World / TCP / Tool / Base
- Deadzone-Slider (0–10 mm)
- Speed-Cap-Slider (mm/s, koppelt Step 10 Safety-Limits)
- "Zero Pose" Secondary-Button (setzt aktuellen Pinch-Punkt als neuen Origin)

**Nativ:** Toggle-Grid, Slider, Dropdown, Button_Secondary.

---

### 12. Envelope Panel (Use-Case 2 — Arbeitsraum)

**Elemente:**
- Show/Hide-Toggle
- Style-Dropdown: Wireframe / Solid-Transparent / Convex-Hull / Reachable-Slice
- Transparency-Slider
- Reachable-Only-Toggle (IK-filtered)
- Payload-Dropdown: 0 kg / 2.5 kg / 5 kg (ändert Reach)
- Color-Preset-Dropdown: Default-Blue / Warning-Red / Neutral-Gray
- "Recompute"-IconButton (Monte-Carlo-Sample refresh)

**Nativ:** Toggle, Dropdown ×3, Slider, IconButton.

---

### 13. Twin Panel (Use-Case 3 — 3D Digital Twin)

**Elemente:**
- Ghost-Opacity-Slider (0–100 %)
- IST-Hologram-Toggle (an/aus/wireframe)
- Display-Mode-Dropdown: Realistic / Wireframe / Collision-Hulls / X-Ray
- Show-Frames-Toggle (TCP-Axis / Joint-Frames / Base-Frame — 3 Sub-Toggles)
- URDF-Variant-Dropdown (GoFa 5/950, 10/1300 für später)
- Scale-Slider (0.1×–1.0× für Table-Top-Preview-Mode)

**Nativ:** Slider ×2, Toggle ×4, Dropdown ×2.

---

### 14. Motor Loads Panel (Use-Case 4 — Motor-Lasten)

**Elemente:**
- 6 Vertikal-Bar-Graphs (Torque je Joint), Threshold-Marker (Nominal/Warning/Critical aus RWS Limits)
- Selectable-Joint-Chips oben (1–6) — wählt welchen Joint die Historie unten zeigt
- Historie-Line-Chart (letzte 30 s, rolling buffer) — Custom mit LineRenderer-on-Canvas oder UI-Image-Strips
- Avg / Peak / RMS-Readout (TMP-Grid)
- Overload-Event-Log (ScrollView, letzte 10)
- Reset-Peak-IconButton
- Export-CSV Primary-Button (schreibt nach `persistentDataPath/logs/`)

**Nativ:** Bar-Graph-Prefab (Custom URP-Shader mit threshold lines), Chips als `Button_Secondary`-Group, ScrollableCanvas, Buttons.

---

### 15. Ergonomics / Body Panel (Step 19 — Use-Case 5 — Body Pose)

**Elemente:**
- Body-Tracking-Enable-Toggle (Meta Movement SDK)
- Skeleton-Visibility-Toggle + Opacity-Slider
- Warnings-Gruppe (Checkboxes):
  - Reach-Warning (Hand-Shoulder > Faktor × Armlänge)
  - Twist-Warning (Torso-Yaw > Schwelle)
  - Zone-Breach-Warning (Körperteil in Safety-Zone, koppelt Step 10 + Step 16)
- Warning-Thresholds-Slider (Sensibilität)
- Posture-Heatmap-Toggle (Floor-Projection der Position über Session)
- RULA/REBA-Score-Display (live Zahl + Verlaufs-Chart, optional)
- "Reset Session" Secondary-Button

**Nativ:** Toggle ×4, Slider ×2, Checkbox ×3, Button_Secondary. Skeleton-Render nutzt Meta Movement SDK `OVRBody` + `OVRSkeletonRenderer`.

---

## Implementation-Reihenfolge

1. `WorldPanelBase.prefab` + `WorldPanelBase.cs` (grab-translate-only, close/pin/minimize, spawn-position-logic) — Fundament
2. `HandRadialMenu.prefab` + Scripts — Step 18 Radial
3. **MVP-Panels** (Main Dashboard + Connection + Telemetry + Path Exec + Safety) — ohne die kann man nichts steuern
4. **Overlay + Joint Control + Waypoint List** — für Path-Workflow
5. **Teleop + Envelope + Twin + Motor-Loads** — Use-Case-Panels
6. **Voice + Demo** — nachdem Step 13 Voice-Stack steht
7. **Ergonomics** — zuletzt, braucht Meta Movement SDK + Step 19

## UI-Modes (Show-Off / Demo-Presets)

Drei austauschbare Top-Level-Presets per `UiModeController.Switch(UiMode)` umschaltbar (z.B. aus Radial → System-Wedge). Jeder Mode hat ein Root-GO mit seinen Panels — der Controller toggelt nur SetActive.

### Mode A — Minimal (Default)

Wie im Leitbild: Radial + Mini-Panels on-demand + In-World-Overlays + HUD. Alltags-Mode, platzsparend, wenig Overhead.

### Mode B — 3-Panel Control Center (Command Station)

Drei Panels permanent im Raum-Dreieck um User:
- **Links (ca. −60° yaw, 1 m):** Telemetry / Connection-Status
- **Front (0°, 1 m):** Paths / Path-Execution
- **Rechts (+60°, 1 m):** Safety / Motor-Loads

Script `ControlCenterLayout.cs` positioniert bei Mode-Aktivierung einmalig auf Basis der aktuellen Kopf-Orientierung. Danach bleiben Panels raum-fix, aber individuell grab-translatable.

Fühlt sich an wie eine Kommando-Brücke. Gut fürs Messe-Show-off oder wenn man länger am Platz arbeitet und alles gleichzeitig sehen will.

### Mode C — Flex Pendant (ABB-Style Emulation)

**Standalone Resizable Panel** das einen echten ABB FlexPendant emuliert. Frei im Raum greifbar, per **Zwei-Hand-Ecken-Pinch** groß-/kleinzieh­bar (Meta `TwoGrabScaleTransformer` / `TwoGrabPlaneTransformer`). Kein Wrist-Mount — du positionierst ihn wo du willst, behält den Platz.

Fühlt sich an wie ein echtes Teach-Pendant das du dir an den Arbeitsplatz hältst. Für ABB-Nutzer sofort vertraut.

**Content (groß genug um Werkzeug zu ersetzen):**
- Program-Dropdown (aus `WaypointManager.paths`)
- Mode-Dropdown (Manual / Auto / Teach)
- 12 Jog-Buttons (6 Achsen × ±) als Grid
- Speed-Override-Slider (0–100%)
- Run / Pause / Stop — Primary + Secondary + Destructive
- Großer roter **E-STOP** unten
- Status-Label (State + Connection + Speed%)

**Resize-Pattern:**
- `canvasRect` auf das Canvas-Root zeigen
- `minScale = 0.5`, `maxScale = 2.5`
- Meta `TwoGrabPlaneTransformer` scale output → `FlexPendantPanel.SetScale(float)` → clamps + wendet auf `canvasRect.localScale` an

**Smartwatch-Face im Wrist-Mode:**
- Wenn attached wird automatisch `watchFaceRoot` aktiviert + `fullPendantRoot` ausgeblendet
- Watch-Face zeigt minimalistische digital clock + Datum + Mini-Status-Strip (Connection-LEDs, Mode)
- Beim Detach swapt's zurück auf full Teach-Pendant-UI
- `clockText` updated jeden Frame, `use24Hour`-Toggle für 24h oder 12h AM/PM
- `dateText` zeigt "Mo, Apr 21" o.ä.
- `watchStatusText` via `SetWatchStatusText(string)` von außen befüllbar (z.B. von einem Robot-Status-Script)

**Attach / Detach am Handgelenk:**
- Button auf dem Pendant selbst (oben rechts, IconButton) toggelt zwischen schwebend und am Wrist gemounted
- Attached: Pendant wird Child des `wristAnchor`, snapt auf `wristLocalOffset` + `wristScale` (kleinere Wristwatch-Size). Grab-Translate deaktiviert
- Detached: restauriert vorheriger Scale + spawnt vor Camera wie bei `Open()`
- UnityEvent `onAttachChanged(bool)` falls anderes UI reagieren soll (z.B. Icon wechseln)
- API: `AttachToWrist()`, `Detach()`, `ToggleAttach()`

**Script-Binding:** `onRun/onPause/onStop/onEstop/onJog(jointIdx, dir)` UnityEvents — connectbar an dieselben Backends wie Path-Exec-Mini-Panel.

---

## Curved HUD (L2+, immer vor User)

Die permanent sichtbare Info-Zentrale. Nicht Teil der Mini-Panel-Liste — eigene Kategorie. Kleiner curved Strip ~40×8 cm, floating ~60 cm vor User, unterer Sichtfeld-Bereich.

**Technik:**
- Flat Canvas + Meta `CanvasCylinder`-Component, Radius 0.8 m, Curvature-Angle 30°
- Grab-translatable (wie andere Panels) + Close-Button in der Ecke
- Re-Open via Radial-Wedge `System → Show HUD`

**Content (live aus `RobotTelemetry` / `SpeedScaler` / `EgmClient`):**
- 4 Status-LEDs: EGM / RWS / ROS / MoveIt
- TCP-Pose kompakt: `X:342  Y:-120  Z:580 mm`
- Hz-Rate: `250 Hz`
- Current Mode: `TELEOP` / `HOME` / `PATH` / `E-STOP` (Badge)
- 6× Mini-Torque-Bars (horizontal, farbcodiert) nebeneinander
- Safety-Badge: `SSM OK` / `PFL` / `BREACH`

**Script:** `CurvedHud.cs` (noch zu schreiben) — bindet `UiThemeConfig` + `RobotTelemetry` + `SafetyConfig` + `EgmClient`.

---

## Robo-Info-Panel (Mini-Panel für Overlay-Toggles)

Ein Mini-Panel (~200×280 mm). Sammelt alle togglebaren In-World-Overlays an einer Stelle — der Overlays-Panel des ursprünglichen Plans aber verkleinert.

**Elemente (alle `ToggleButton_Checkbox`):**
- ☑ Joint Compass-Arcs
- ☑ Torque-Colors am Joint-Mesh
- ☑ TCP-Pose-Label
- ☐ Distance-Ruler (force-on — überschreibt Proximity-Auto)
- ☐ Path-Preview
- ☐ Ghost-Robot always visible
- ☐ Working Envelope
- ☐ Body-Skeleton (Step 19)

Keine Sliders hier — reine Toggles. Opacity-Slider wären optional als zweite Seite.

---

## Feature-Rich Mini-Panels

Ergänzung zum Leitbild "Panels minimal" — diese Panels sind Task-spezifisch und liefern tiefen Zugriff trotz kleiner Fläche.

### Precise Position Panel (~260×320 mm)

6 Slider für XYZ (mm) + RPY (°), jeder Row hat einen **"Exact Entry"**-IconButton der das Numpad-Popup öffnet. Rest-Button setzt alle auf 0. Kann auf jede Transform gebunden werden (IK-Handle, Ghost, einzelner Joint).

**Prefab-Content:**
- Header mit TMP `Precise Position — TCP` oder `Joint 3`
- 6× Row = `LargeSlider_LabelsAndIcons.prefab` + TMP-Value-Label + `BorderlessButton_IconAndLabel_UnityUIButton.prefab` (Edit-Icon)
- Footer: Reset-All = `SecondaryButton_IconAndLabel_UnityUIButton.prefab`

**Script:** `PrecisePositionPanel.cs` — bindet an Target-Transform, optional worldSpace, zeigt Werte in mm (toggelbar m), Numpad-Callback synct Slider.

### Numpad Popup

Modal-Panel mit 3×4 Button-Grid. Wrappt Metas `Keypad.prefab` — jeder `KeypadButton.prefab` feuert eine Funktion auf `NumpadPopup.cs`:
- Digits 0–9 → `AppendDigit(int)`
- `.` → `AppendDecimal()`
- `+/-` → `ToggleSign()`
- Enter / Clear / Cancel → entsprechende Methoden
- Backspace als IconButton

Only-Instance-Singleton `NumpadPopup.Instance`. Jeder Slider oder Input ruft `Request(prompt, current, onSubmit)` und bekommt den Wert zurück.

### Paths Panel (~300×400 mm)

Waypoint-Workflow in einem Mini-Panel.

**Content:**
- Path-Dropdown (`DropDownIconAnd1LineText.prefab`) — wählt aktive Path aus `WaypointManager.paths`
- Waypoint-ScrollView: Rows = `ToggleButton_Checkbox.prefab` (2-zeilig mit Label + Index)
- `Active Waypoint Label` — "WP 3 / 12"
- Action-Row: `PrimaryButton` **Add @ TCP**, `SecondaryButton` **New Path**, `DestructiveButton` **Clear**
- Remove-Current: `DestructiveButton` (kleiner)
- Run-Program: `PrimaryButton_IconAndLabel_UnityUIButton.prefab` groß
- Speed-Slider (`LargeSlider_LabelsAndIcons`) + Loop-Toggle (`ToggleButton_Switch`)

**Swipe-Override:** Swipe auf diesem Panel **cycelt Waypoints** statt zu den Nachbar-Wedges (via `HandleSwipe`-Override). User kann mit Swipe schnell durch die Waypoint-Liste flippen, Ghost-Robot springt an die jeweilige Preview-Position.

**Scripts:**
- `PathData.cs` (ScriptableObject) — Waypoint-Liste + Speed + Loop
- `WaypointManager.cs` (scene-singleton) — Runtime-State: Paths-Liste, Active-Index, Selected-Waypoint, Add/Remove/Select-API + UnityEvents
- `PathsPanel.cs` — UI-Binding

### Precise Joint Control (optional Zwilling zu Precise Position)

Gleiche Mechanik wie Precise Position aber 6 Slider für J1–J6 mit Limits aus `JointLimits`-Asset. Numpad-Popup für Grad-Eingabe. Nutzt `PrecisePositionPanel.cs` als Template — nur mit anderem Target-Binding (Joints statt Pose).

---

## Joint Arc — Dual Interaction (Pinch-Drag + Quick-Poke)

Bestehende Joint-Arcs nutzen jetzt zwei Input-Modes:

**Mode A — Pinch-Drag (bestehend, tuned):**
- Near-Pinch = 1:1 Rotation (präzise)
- Distance-Pinch = 3.5–4× amplifiziert (grob, weniger Re-Pinches)
- Script: `SensitiveRotateTransformer.cs` (bereits im Projekt) — `distanceSensitivity` hochdrehen
- Ersetzt Metas Default `OneGrabRotateTransformer`

**Mode B — Quick-Poke = 15°-Step:**
- Ein kurzer Fingertipp auf den Arc dreht den Joint um `stepDegrees` (Default 15°) in Richtung des Poke-Punkts
- Capped at poke direction: überdreht nie über den Poke-Zielwinkel hinaus
- Clamped an `JointLimits` (respektiert Min/Max)
- Script: `JointArcPokeStepper.cs`

**Prefab-Setup pro Arc:**
- Bestehende `JointArcVisual`-Component bleibt (visueller Track)
- Zusätzlich:
  - Meta `PokeInteractable` (falls nicht schon da) + `InteractableUnityEventWrapper`
  - `JointArcPokeStepper.cs` mit `pivot=JointTransform`, `target=JointTransform`, `localAxis=<joint axis>`, `stepDegrees=15`
  - Wrapper's `WhenSelect` → `JointArcPokeStepper.StepTowardFingerTip`
  - `fingerTip` Feld = Index-Finger-Tip-Transform des pokenden Hand-Skeletts

**UX-Kombination:**
- Grob einstellen via Distance-Pinch-Drag (amplifiziert)
- Fein anpassen via Near-Pinch-Drag (1:1)
- Exakt diskret steppen via Poke (15°-Raster)

---

## IK Handle — Ball + MoveIt-Style 6-DOF Gizmo

Das End-Effektor-Handle (IK-Target) hat zwei Interaktions-Modi wie RViz/MoveIt-Marker:

1. **Ball** (Sphere, bestehende Lösung) — omnidirektionales 6-DOF Free-Move via Distance-Grab / Pinch
2. **Gizmo** (neu) — 3 Pfeil-Handles (X/Y/Z) für achs-constrained Translation + 3 Ringe (X/Y/Z) für achs-constrained Rotation

**Modi-Toggle (in Mini-Panel "Teleop" oder direkt am Handle):**
- `Ball` only — grobes Positionieren, frei 6DOF
- `Axes` only — Fein-Einstellung entlang genau einer Achse
- `BallAndAxes` — beides sichtbar, User wählt spontan
- `Off` — Handle unsichtbar (z.B. während autonomer Run)

**Scripts:**
- `TransformGizmoAxis.cs` — pro Arrow / Ring. Nutzt Meta `Grabbable` + `HandGrabInteractable` + `OneGrabTranslateTransformer` / `OneGrabRotateTransformer` standard (keine Axis-Constraints nötig — wird in dem Script on-the-fly gemacht)
- `TransformGizmo.cs` — Parent-Koordinator, toggelt Sichtbarkeit + Modi

**Snap-To-Grid optional:** `translateSnapMeters = 0.01` ergibt 1-cm-Rasterung, `rotateSnapDegrees = 5` ergibt 5°-Snaps.

**Prefab-Struktur (im Unity-Editor zu bauen):**
```
IKHandle (Parent, target des Gizmos)
├── Ball                           ← bestehender Distance-Grab Sphere
└── Gizmo                          ← TransformGizmo.cs
    ├── ArrowX  (rot, cylinder+cone)   → TransformGizmoAxis: axis=(1,0,0), Translate
    ├── ArrowY  (grün)                 → axis=(0,1,0), Translate
    ├── ArrowZ  (blau)                 → axis=(0,0,1), Translate
    ├── RingX   (rot, torus-LineRenderer-Circle) → axis=(1,0,0), Rotate
    ├── RingY   (grün)                 → axis=(0,1,0), Rotate
    └── RingZ   (blau)                 → axis=(0,0,1), Rotate
```

Jedes Arrow/Ring GO braucht in Unity:
- Unity `Collider` (BoxCollider für Pfeile, MeshCollider für Ringe)
- Meta `HandGrabInteractable` + Rigidbody (Kinematic)
- Meta `Grabbable`
- Meta `OneGrabTranslateTransformer` (für Arrows) **oder** `OneGrabRotateTransformer` (für Rings) — **ohne Axis-Constraint** (unser Script macht das)
- Unser `TransformGizmoAxis.cs` mit `target` = IKHandle, `axis` = entsprechend, `mode` = Translate/Rotate

**Meshes:** Unity-Primitive reichen als Baseline:
- Arrow = Cylinder (Shaft) + skalierter Cube oder custom Cone-Mesh (Tip), rot/grün/blau Material
- Ring = LineRenderer als Kreis um Achse (bestehendes `JointArcVisual`-Pattern wiederverwenden) + eine Capsule-Collider-Kette für Grab

**Alternative:** ProBuilder (hatten wir als optionales Package) kann Torus + Cone sauber erzeugen wenn du schönere Meshes willst.

---

## L3 Physical Fixtures Catalog

Dauerhafte 3D-Objekte im Raum. Nie "geschlossen", beim Bootstrap gespawnt, räumlich verankert (via `OVRSpatialAnchor`). Alles aus Meta-Samples gebaut, URP-Glass-Material draufgezogen.

### F1 — Glass Pedestal mit GoFa-Twin

Der Roboter-Twin schwebt nicht im Raum — er steht auf einem Museums-Sockel.

- **Basis:** Meta MR-Template `Desk.prefab` (bereits im Projekt) als Baseline ODER MRUK Scene-Anchor `TABLE` wenn Room-Setup vorhanden. Alternativ ein schlichtes Cylinder-Mesh (30 cm hoch, 50 cm Ø)
- **Material:** URP `Lit` mit `Surface=Transparent`, `Smoothness=0.95`, leicht tintiertes Blau (`#3DA5FF` @ 8%), Fresnel-Rim-Light — aus Meta `Interaction SDK Samples` → `ComprehensiveRig` Scene, Glass-Reference-Material wiederverwenden
- **Emissive Rim** am Sockelboden für visuelle Erdung in hellen Räumen
- **Tabletop-Mode-Trigger:** 2-Hand-Spread-Geste am Pedestal skaliert Twin hoch/runter (Meta `TwoGrabScaleTransformer` auf Root)
- **Fallback:** wenn MRUK keine Table-Anchor findet → eigenes Prefab spawnen auf ArUco-Marker-Position aus Step 8

### F2 — Physical E-Stop Mushroom

3D-Pilzkopf, drückbar, mit Travel-Animation + Twist-to-Reset (echtes Industrie-Not-Aus-Feel).

- **Basis:** Meta `[BB] Pokeable Plane` (wie in HANDOFF.md Step 10 bereits referenziert) + Custom-Mushroom-Mesh statt Flat-Plane
- **Alternativ:** Meta `PokeExamples.unity` → `PokeButton.prefab` als Referenz-Implementation, Mesh ersetzen, Travel-Distance beibehalten
- **Interaction:** `PokeInteractable` + UnityEvent → `SafetyZoneController.EmergencyStop()`
- **Visual:** roter Metall-Pilzkopf (URP Lit, Metallic=0.8, Smoothness=0.6), gelbe Ring-Base, Emissive-Pulse bei Idle (soft red glow)
- **Reset:** nach Drücken twist-Geste (2-Finger-Rotate auf Mushroom) hebt Lock auf — Meta `OneGrabRotateTransformer` mit Constraint
- **Platzierung:** spawnbar als Wand-Mount (auf MRUK-Wall-Anchor) oder eigener Mini-Pedestal neben Glas-Sockel. Beides gleichzeitig möglich
- **Haptic:** bHaptics-Patttern `safety_zone_violation` + Audio-Click beim Drücken

### F3 — Curved Scroll Lists

Lange Listen (Waypoints, Voice-Transcript, Demo-Saves) nicht flach, sondern gekrümmt um den User.

- **Basis:** Meta `CanvasCylinder` Component aus Interaction SDK (shipped in `com.meta.xr.sdk.interaction`)
- **Radius:** 1.2 m um User-Position beim Spawn
- **Curvature-Angle:** 60° für Waypoint-Liste, 90° für Voice-Transcript
- **Scroll:** Standard Unity `ScrollRect` funktioniert auf `CanvasCylinder` out-of-the-box, Pinch-Drag oder Ray-Scroll
- **Einsatz:** Panel-intern für lange Listen (innerhalb flacher Panels), oder als eigenständige curved Wall-Panels (z.B. Voice-Transcript als Wandtafel links vom User)

### F4 — 3D Glass Poke-Buttons

Für High-Value-Actions (Run, Commit, HOME) 3D-Tiefe statt Canvas-Flat-Button.

- **Basis:** Meta `PokeExamples.unity` → `PokeButton.prefab` — hat bereits Press-Travel + Release-Spring + UnityEvents
- **Material-Swap:** URP Glass-Shader (Transparent + Smoothness 0.9 + leichter IOR-Look)
- **Accent-Glow:** Emissive ändert Farbe bei Hover (accent) → Pressed (success) — fade via Material-Property-Block
- **Einsatz sparsam:** nur für Actions die "committed" wirken sollen. Für Toggles/Sliders bleibt flache Canvas-UI (overkill sonst)
- **Panel-Integration:** 3D-Button sitzt vor der Panel-Canvas mit Z-Offset +8 mm, wirkt aufliegend statt eingebettet

### F5 — Spatial Anchor Pucks

Kleine Marker wo Anchors sitzen, sichtbar im Calibration-Mode.

- **Basis:** Meta `[BB] Spatial Anchor` — hat Sample-Visualizer mit `SpatialAnchorPuck.prefab`
- **Visual:** Puck mit Ring + Center-Dot, pulsierendes Glow wenn ausgewählt
- **Interaction:** `HandGrabInteractable` für Fine-Adjust der Position, `RayInteractable` für Select
- **Toggle:** Overlays-Panel Checkbox "Show Anchors" — Default off, Calibration-Mode on

### F6 — Ambient Floor Grid

Orientierungshilfe im Passthrough — dezentes Grid-Muster auf dem Boden (MRUK Floor-Anchor).

- **Basis:** MRUK `FloorAnchor` + eigenes Quad mit Grid-Shader
- **Shader:** einfacher URP-Unlit mit prozeduralem Grid (world-space 10 cm Linien), fade-to-transparent radial um User
- **Zweck:** verankert Roboter-Basis visuell, macht Distanz intuitiv. Koppelt mit Distance-Ruler aus Step 17a
- **Toggle:** Overlays-Panel

---

## Meta Asset Inventory (Referenz)

Tabelle was wir aus welchem Meta-Package reuse-en:

| Asset | Package | Zweck |
|---|---|---|
| `OVRInteractionComprehensive.prefab` | `com.meta.xr.sdk.interaction` | Camera + Hands + alle Interactors |
| `Desk.prefab` | MR Template | Glass-Pedestal-Baseline F1 |
| `[BB] Pokeable Plane.prefab` | Meta Building Blocks | E-Stop-Base F2, generelle Poke-Actions |
| `[BB] Spatial Anchor` | Meta Building Blocks | Anchor-System für Roboter-Basis + F5 |
| `[BB] Hand Tracking` | Meta Building Blocks | Hand-Poses + Gestures |
| `[BB] Passthrough` | Meta Building Blocks | MR-View |
| `[BB] Scene Mesh` + MRUK | Meta Building Blocks + `com.meta.xr.mrutilitykit` | Room-Anchors, Walls, Floor, Table → F1 F6 |
| `[BB] Virtual Keyboard` | Meta Building Blocks | Text-Input für TextFields (IP, Pfad-Name) |
| `UISet` Prefabs (Button_Primary/Secondary/Destructive/Toggle/Checkbox/Slider/Dropdown/TextField/IconButton) | `com.meta.xr.sdk.interaction.samples` | Alle L2-Panel-Controls |
| `CanvasCylinder` Component | `com.meta.xr.sdk.interaction` | F3 Curved Scroll Lists |
| `PokeExamples.unity` → `PokeButton.prefab` | `com.meta.xr.sdk.interaction.samples` | F4 3D-Glass-Buttons + F2 E-Stop Referenz |
| `BackdropBlur` | Meta Samples | Panel-Frosted-Glass-Background |
| `HandGrabInteractable` / `DistanceHandGrabInteractable` | `com.meta.xr.sdk.interaction` | Panel-Grab + In-World-Objekte |
| `OneGrabTranslateTransformer` | `com.meta.xr.sdk.interaction` | Panel-Grab-ohne-Rotation |
| `OneGrabRotateTransformer` | `com.meta.xr.sdk.interaction` | Joint-Arcs + E-Stop-Twist-Reset |
| `TwoGrabScaleTransformer` | `com.meta.xr.sdk.interaction` | Tabletop-Mode Scale am Pedestal |
| `PointableCanvas` + `RayInteractable` | `com.meta.xr.sdk.interaction` | Canvas-UI clickable |
| `ShapeRecognition` + `ActiveStateSelector` | `com.meta.xr.sdk.interaction` | Palm-Up, OK-Ring, Stop-Hand Gesten |
| `OVRBody` + `OVRSkeletonRenderer` | `com.meta.xr.sdk.movement` | Step 19 Body-Pose/Skeleton |
| `ComprehensiveSample.unity` Scene | `com.meta.xr.sdk.interaction.samples` | Referenz-Scene für alle Pattern |
| `FirstHand` Sample (open-source repo) | github.com/oculus-samples/Unity-FirstHand | Panel-Grab-Pin-Pattern, Glove-Feel |

**Custom-Code-Liste** (bewusst minimal, nur wo Meta nichts Passendes bietet):
- `HandRadialMenu.cs` + `RadialMenuItem.cs` — Layout-Math (Polar → Position)
- `WorldPanelBase.cs` — Panel-Lifecycle + Config-Bindings (nutzt Meta-Components intern)
- `DistanceRuler.cs` + `PathPreviewRenderer.cs` — Step 17a/b Visualization
- `BHapticsAdapter.cs` — 3rd-party SDK-Bridge
- `GoFaCCDIK.cs` — IK-Mathe (pre-existing)
- `JointArcVisual.cs` — LineRenderer-Arc (pre-existing)
- Shader: `GoHoloGlass.shadergraph` für L3-Fixtures (Glass Morphism konsolidiert)

---

## Design Tokens

Einheitliche Werte für alle Panels. Als `ScriptableObject` `UiThemeConfig.asset` in `_GoHolo/Settings/` → von jedem Panel-Script gelesen, Editor-editierbar.

### Spacing (world-mm in Canvas-Units bei Scale 0.001)
| Token | Wert | Usage |
|---|---|---|
| `space-xs` | 4 mm | Icon-Padding, Checkbox-Gap |
| `space-sm` | 8 mm | Item-Padding innerhalb Row |
| `space-md` | 16 mm | Standard-Gap zwischen Controls |
| `space-lg` | 24 mm | Section-Separator, Gap zwischen Groups |
| `space-xl` | 40 mm | Panel-Border-Padding außen |

### Typography (TextMeshPro, world-mm)
| Token | Size | Weight | Usage |
|---|---|---|---|
| `type-label-sm` | 6 mm | Regular | Slider-Tick-Labels, Kleinschrift |
| `type-body` | 10 mm | Regular | Button-Labels, List-Items, Body |
| `type-heading` | 14 mm | Medium | Section-Header, Tab-Label |
| `type-title` | 20 mm | Semibold | Panel-Titel (Top-Bar) |
| `type-display` | 32 mm | Semibold | Große Readouts (TCP-XYZ, RPM, Torque-Peak) |

Font: **Inter** (frei, Meta-ähnlich) oder Meta-Default `LiberationSans`. Linehöhe 1.4×.

### Color Palette
| Token | Hex | Usage |
|---|---|---|
| `color-bg` | `#0E1520` @ 85% | Panel-Background (dark glass) |
| `color-bg-raised` | `#1A2332` | Input-Felder, Scroll-Content |
| `color-border` | `#2B3648` | Trenner, Panel-Border |
| `color-fg` | `#FFFFFF` | Primärtext |
| `color-fg-muted` | `#9AA5B8` | Labels, inaktive Texte |
| `color-accent` | `#3DA5FF` | Primary-Buttons, Active-Tab, Sliders, Links |
| `color-success` | `#4ADE80` | OK-State, EGM-Connected, IK-OK |
| `color-warning` | `#F5B941` | Limit-nah, High-Torque |
| `color-destructive` | `#E24A4A` | E-Stop, Delete, Fault |
| `color-ghost` | `#9DD5FF` @ 60% | Ghost-Roboter, Preview-Path |

### Sizing / Panel-Dimensionen
| Panel-Class | World-Size | Canvas-Size @ 0.001 |
|---|---|---|
| Main Dashboard | 600 × 400 mm | 600 × 400 |
| Sub-Panel (floated) | 400 × 300 mm | 400 × 300 |
| Mini-Panel (Status only) | 240 × 160 mm | 240 × 160 |
| Standard Button Height | 40 mm | — |
| Standard Slider Height | 32 mm | — |
| Top-Bar Height | 48 mm | — |
| Tab Height | 40 mm | — |

### Radius / Elevation
- Panel-Corner-Radius: **12 mm** (konsistent, über Meta `RoundedBoxImage`)
- Button-Corner-Radius: **8 mm**
- Panel-Elevation (Parallax Layer hinter Content): 3 mm Z-Offset für depth

---

## MVP Cut (First Live Demo)

Minimal funktionierender Stack für den ersten echten GoFa-Demo. Alles andere Phase-2.

**Must-Have Panels (4):**
1. **Main Dashboard** — Tab-Nav-Shell
2. **Connection & Status** — damit der Roboter überhaupt connected ist
3. **Telemetry / Readout** — Live-Feedback während Teleop
4. **Safety Panel** — Zone-Toggle + **E-Stop-Button** (non-negotiable)

**Must-Have Radial (4 Wedges statt 8):**
- **E-Stop** (Destructive, rot)
- **HOME**
- **Open Dashboard**
- **Overlays** (Quick-Toggle Torque + Ghost)

**Must-Have In-World (inkl. L3-Fixtures):**
- Pinch-to-Move End-Effektor (Step 7+9)
- Torque-Color-Overlay auf Joint-Meshes (Step 14a, existiert schon)
- Ghost-Robot (Step 9)
- ArUco-Anchor für Roboter-Basis (Step 8)
- **F1 Glass Pedestal** mit GoFa-Twin (Baseline `Desk.prefab`, URP Glass-Material)
- **F2 Physical E-Stop** neben Pedestal (`[BB] Pokeable Plane` + Mushroom-Mesh)
- **F6 Floor Grid** dezent (MRUK Floor-Anchor + Grid-Shader) — Orientierung

**Must-Have States:**
- Connected / Disconnected / Reconnecting
- IK-OK / IK-Fail (Ghost-Farbton)
- Safety-Zone-Breach (Alert-State)

**Bewusst rausgelassen für MVP:**
- Voice-Panel, Demo-Panel, Ergonomics-Panel, Twin-Panel, Motor-Loads-Panel, Teleop-Detail-Panel, Envelope-Panel, Joint-Control-Panel (In-World Arcs reichen), Waypoint-List-Panel, Path-Exec-Panel (Commit via OK-Ring reicht)
- 4 weitere Radial-Wedges (Simulate, Voice, Commit, Waypoint-Add — können später)
- Body-Tracking
- bHaptics Gloves (Nice-to-have, aber nicht MVP-blocking)

**Success-Criteria MVP-Demo:** Operator setzt Quest auf, sieht GoFa-Hologram in-place, pincht End-Effektor → echter Roboter folgt, Torque wird sichtbar wenn er an Limits kommt, Palm-Up → Radial → E-Stop → Roboter hält.

---

## Wireframe: Main Dashboard

Layout-Referenz für Implementation. Grid-Units = `space-md` (16 mm).

```
┌─────────────────────────────────────────────────────────────────┐
│  ● EGM  ● RWS  ● ROS        GoHolo Dashboard      [ — ][ 📌 ][ ✕ ]│  ← Top-Bar 48mm
├─────────────────────────────────────────────────────────────────┤
│ [ Status ][ Control ][ Path ][ Safety ][ Motors ][ Body ][ ⋯ ]  │  ← Tabs 40mm
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─ Tab-Content (Status-Tab shown) ───────────────────────────┐ │
│  │                                                             │ │
│  │  Connection ─────────────────────────────────────────────   │ │
│  │  Robot IP:  [ 192.168.125.99  ▼ ]   Port: [ 443  ]          │ │
│  │  Mode:      [ Real-GoFa       ▼ ]                           │ │
│  │  [ Connect ]   [ Disconnect ]        Latency: 2.1 ms        │ │
│  │                                                             │ │
│  │  ─────────────────────────────────────────────────────────  │ │
│  │  Telemetry                                                  │ │
│  │                                                             │ │
│  │    J1  [◐ 45.3°]    J2  [◐ -12.0°]    J3  [◐ 88.7°]       │ │
│  │    J4  [◐  3.2°]    J5  [◐  90.0°]   J6  [◐ 180.0°]        │ │
│  │                                                             │ │
│  │   TCP Pose                                                  │ │
│  │   X  342.1       RX  0.0                                    │ │
│  │   Y  -120.4      RY  90.0                                   │ │
│  │   Z  580.9       RZ  180.0        [mm / deg]               │ │
│  │                                                             │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
├─────────────────────────────────────────────────────────────────┤
│  [ 🛑 E-STOP                                                ]   │  ← persistent 56mm
└─────────────────────────────────────────────────────────────────┘
     ↑ Grab-Handle = gesamte Top-Bar (HandGrabInteractable)
```

**Notes:**
- E-Stop ist im Main Dashboard **persistent unten** — egal welcher Tab aktiv. Zweite E-Stop-Instanz im Radial. Zweite im Safety-Panel. Dreifache Abdeckung ist Absicht (Safety first)
- Status-LEDs oben sind farbige Punkte (`color-success` connected, `color-destructive` broken, `color-warning` connecting)
- Top-Bar ist Grab-Zone, Pin-Icon toggelt "spawn zur Kamera" vs "fix im Raum", Close wirft Panel weg (Radial → Dashboard re-spawnt es)
- Tabs overflowen → letzter Tab `⋯` öffnet Dropdown mit restlichen (Motors / Body / Voice / Demo)
- Bei Nicht-MVP-Tab wird "Coming Soon"-Placeholder gerendert statt kaputt

---

## Digital-Twin Demo Props Catalog

Für den Twin-Mode können wir Pick-and-Place-Demos bauen — Meta Interaction SDK Samples liefern fertige grabbare Props die wir direkt reinziehen. Alle unter `Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Objects/Props/`.

| Prop | Prefab | Demo-Einsatz |
|---|---|---|
| **Chess Piece** | `ChessPiece/ChessPiece.prefab` | Klassiker: Figur greifen und in Kiste stapeln / auf Schachbrett setzen |
| **Box** | `Box/Box.prefab` | Ziel-Container für Pick-and-Place |
| **IconBox** | `IconBox/IconBox.prefab` | Box mit Icon, Sortier-Demo (Objekt zur passenden Icon-Box bringen) |
| **Key** | `Key/Key.prefab` | Schloss-und-Schlüssel-Demo, präzise Positionierung |
| **Mug** | `Mug/Mug.prefab` | Becher-Handling, fragiler Griff, Tabletop-Serving-Demo |
| **Doll** | `Doll/Doll.prefab` | Weicher Pick-Demo, Heavy-Lift-Simulation |
| **Ping-Pong-Ball** | `PingPong/PingPongBall.prefab` | Leichter Ball — Geschicklichkeit, Safety-Limits-Demo |
| **Torch** | `Torch/Torch.prefab` | Tool-Handling, langer Gegenstand, Orientierung wichtig |
| **Big Stone** | `BigStone/BigStone.prefab` | Schwere Last, Payload-Limit-Test |
| **Stone Polyhedra** | `StonePolyhedra/StoneCube.prefab`, `StoneDodecahedron.prefab`, `StoneIcosahedron.prefab`, `StoneOctahedron.prefab`, `StoneTetrahedron.prefab`, `StonePolyhedron.prefab` | Sortier-Demo (Form erkennen und in passende Box) |
| **Picture Frame + Picture** | `PictureFrame/PictureFrame.prefab` + `Picture.prefab` | Zwei-Objekt-Assembly-Demo (Bild in Rahmen schieben) |
| **Map with Pins** | `MapWithPins/MapWithPins.prefab` + `Pin.prefab` | Präzisions-Platzierung auf 2D-Surface |

### Demo-Szenarien (für Demo-Panel + Presets)

1. **Chess Pick-and-Place** — ChessPiece + Box. Simpler Start-Demo. User teleopt Roboter → greift Figur → ablegen in Kiste. Auch als Auto-Demo mit fester Waypoint-Path.
2. **Stone Sorting** — mehrere Stone-Polyhedra + IconBoxes. Roboter soll passend zur Form in richtige Box. Braucht Object-Detection (Step 13c).
3. **Picture Framing** — Picture in Frame. Zwei-Step-Assembly, Orientierung kritisch.
4. **Mug Serving** — Mug auf MRDesk → anderer Punkt. Fragiler Handling-Demo, zeigt Safety-Zone-Einhaltung.
5. **Pin-Set** — MapWithPins + Pins. Roboter setzt Pins an präzise Positionen, ideal für Accuracy-Demo.
6. **Stress-Test Big Stone** — BigStone heben. Zeigt Motor-Loads live (Torque-Overlay wird rot bei Limit).

### Einbau-Pattern

Demo-Scenes als additive Scenes: `Scene_Demo_Chess.unity`, `Scene_Demo_Sorting.unity`. Jede Demo hat:
- Props gespawnt auf dem MRDesk / in Reichweite des GoFa
- Fixe Start-Positionen + "Reset Demo"-Button im Demo-Panel
- Pre-authored Waypoint-Path als `PathData`-Asset im WaypointManager
- Optional: SuccessDetector der registriert wenn Pick-and-Place gelungen ist (Collision-Detection zwischen Prop und Box-Trigger)

Demo-Panel (#10 im Katalog) listet diese als Dropdown-Optionen, Load-Button wechselt in die entsprechende Scene.

---

## Meta UISet Prefab-Inventar (Interaction SDK v85)

Alle Pfade unter `Packages/com.meta.xr.sdk.interaction/Runtime/Sample/`.

### Panel-Container
| Bedarf | Prefab |
|---|---|
| Leerer Backplate + Canvas | `Objects/UISet/Prefabs/Backplate/EmptyUIBackplateWithCanvas.prefab` |
| Grabbable Panel komplett | `Prefabs/PanelWithManipulators.prefab` |
| Flat Canvas (minimal) | `Objects/Props/FlatUnityCanvas.prefab` |
| Sample Canvas | `Objects/Props/SampleCanvas.prefab` |

### Buttons (alle mit IconAndLabel-Variante)
| Rolle | Prefab (UnityUIButton-based für Single-Click) |
|---|---|
| Primary (Connect, Run, Confirm) | `Objects/UISet/Prefabs/Button/UnityUIButtonBased/PrimaryButton_IconAndLabel_UnityUIButton.prefab` |
| Secondary (Cancel, Disconnect) | `.../SecondaryButton_IconAndLabel_UnityUIButton.prefab` |
| **Destructive (E-Stop, Delete)** | `.../DestructiveButton_IconAndLabel_UnityUIButton.prefab` |
| Borderless (TopBar Close/Pin/Min) | `.../BorderlessButton_IconAndLabel_UnityUIButton.prefab` |
| TextTile (Radial Wedge, Grid-Cell) | `.../TextTileButton_IconAndLabel_Regular_UnityUIButton.prefab` |
| Hover-reactive | `Objects/Props/HoverButtons.prefab` |
| Circular Poke-Button | `Prefabs/CircularButton.prefab` |
| Sample Poke-Button | `Prefabs/OculusInteractionSamplePokeButton.prefab` |

Toggle-basierte Varianten (Stateful Selected/Deselected) unter `Button/UnityUIToggleBased/`:
- `ButtonShelf_IconAndLabel_Toggle.prefab` — Dashboard Tab-Header
- `ToggleButton_Checkbox.prefab` — Zone-Toggle in Liste, Overlays
- `ToggleButton_Switch.prefab` — Enable/Disable (Teleop, Body-Track)
- `ToggleButton_Radio.prefab` — Exclusive Choice (ISO-Mode)

### Slider
| Größe | Prefab |
|---|---|
| Groß (Speed, Sensitivity) | `Objects/UISet/Prefabs/Slider/LargeSlider_LabelsAndIcons.prefab` |
| Mittel (Opacity) | `.../MediumSliderWithLabelsAndIcons.prefab` |
| Klein (inline) | `.../SmallSlider_LabelsAndIcons.prefab` |

### Dropdowns
| Variante | Prefab |
|---|---|
| Nur Text (ISO-Mode) | `Objects/UISet/Prefabs/DropDown/DropDown1LineTextOnly.prefab` |
| Icon+Text (IP-Endpoint) | `.../DropDownIconAnd1LineText.prefab` |
| Icon+2-Zeilen (Demo-Path mit Info) | `.../DropDownIconAnd2LineText.prefab` |

Liste-Items für DropDowns:
- `Button/UnityUIToggleBased/DropDownListButton_IconAndLabel_Toggle.prefab`
- `.../DropDownListButton_IconAndLabel2Lines_Toggle.prefab`
- `.../DropDownListButton_ImageAndLabel2Lines_Toggle.prefab`

### Text-Input / Keypad
| Rolle | Prefab |
|---|---|
| Textfeld (Custom-IP, Path-Name) | `Objects/UISet/Prefabs/TextInputField/TextInputField.prefab` |
| Such-Leiste (Waypoint-Filter) | `.../SearchBar.prefab` |
| **Numeric Keypad** (IP, PIN, Zahleneingabe) | `Objects/Props/Keypad/Keypad.prefab` (+ `KeypadButton.prefab`) |

### Dialogs / Tutorial
| Zweck | Prefab |
|---|---|
| Confirm 2-Button | `Objects/UISet/Prefabs/Dialog/Dialog2Button_TextOnly.prefab` |
| Fehler 1-Button | `Objects/UISet/Prefabs/Dialog/Dialog1Button_IconAndText.prefab` |
| **Tutorial-Slide (Text+Image+Video)** | `Objects/UISet/Prefabs/Dialog/Dialog2Button_ImageVideoAndText.prefab` |

### Context-Menu / Tooltip
- `Objects/UISet/Prefabs/ContextMenu/ContextMenuIconAnd1LineText.prefab`
- `Objects/UISet/Prefabs/Tooltip/Tooltip.prefab`

### Layout-Pattern-Referenzen (Inspiration)
- `Objects/UISet/Prefabs/Patterns/ContentUIExample-HorizonOS1/2/3.prefab` — drei Horizon-OS-Stile
- `Objects/UISet/Prefabs/Patterns/ContentUIExample-VideoPlayer.prefab` — Video-Tutorial-Layout
- `Objects/UISet/Prefabs/Patterns/GridMenuExample3x3.prefab` — Demo-Preset-Grid
- `Objects/UISet/Prefabs/Patterns/ContentUIExample1/2.prefab` — Generic Content-Panel

### Gesture-Prefabs (drop-in)
- `Prefabs/HandGesture/SwipeForwardGesture.prefab` — **ersetzt unseren velocity-basierten Swipe-Detector** wenn gewünscht
- `Prefabs/HandPose/StopPose.prefab` — Safety-Gesture
- `Prefabs/HandPose/ThumbsUpPose.prefab` — Confirm-Gesture
- `Prefabs/HandPose/LPose.prefab`, `RockPose.prefab`, `PaperPose.prefab`, `ScissorsPose.prefab` — verfügbar für Commands

### Props / L3 Fixtures
- `Objects/Props/BigRedButton/BigRedButton.prefab` — **Physical E-Stop Mushroom (F2)** ✓ eingebaut
- `Objects/Props/Keypad/Keypad.prefab` — als alternatives Input-Fixture falls gewünscht
- `Objects/Environment/MRDesk/Desk.prefab` — Glass-Pedestal-Baseline (F1)

### Selbstbau (kein Meta-Prefab)
- **Curved Panel** → `CanvasCylinder`-Component auf flachen Canvas aufsetzen, Radius + Curvature im Inspector
- **Radial-Gauge** (Joint-Angles) → Unity UI `Image`, Type=Filled, Method=Radial 360
- **Status-LED** → Unity `Image` + Color-Swap via Script
- **Progress-Bar** → Unity `Image`, Type=Filled, Method=Horizontal

---

## Panel-Build-Map (Konkrete Prefab-Paths pro Panel)

### 1. WorldPanelBase (Basis-Prefab)
- Canvas: `EmptyUIBackplateWithCanvas.prefab` als Fundament
- TopBar 3× IconButton: `BorderlessButton_IconAndLabel_UnityUIButton.prefab` (Close, Pin, Minimize)
- Grab auf TopBar: `HandGrabInteractable` + `Grabbable` + **`OneGrabTranslateTransformer`** (Rotation gesperrt)

### 2. MainDashboard
- Tab-Bar: 4× `ButtonShelf_IconAndLabel_Toggle.prefab`
- Persistent Footer E-Stop: `DestructiveButton_IconAndLabel_UnityUIButton.prefab` (Scale 1.4×)
- Tab-Content: leerer Container, jedes Tab-GO aktiv/inaktiv

### 3. Connection
- Endpoint-Dropdown: `DropDownIconAnd1LineText.prefab`
- Mode-Dropdown: `DropDown1LineTextOnly.prefab`
- Custom-IP: `TextInputField.prefab` (oder `Keypad.prefab` als Popup)
- Connect: `PrimaryButton_IconAndLabel_UnityUIButton.prefab`
- Disconnect: `SecondaryButton_IconAndLabel_UnityUIButton.prefab`
- 4× Status-LED: Unity `Image` (custom)
- Latency: `TMP_Text`

### 4. Telemetry
- 6× Radial-Gauge: Unity `Image` Type=Filled Radial 360 (custom)
- TCP Pose: 6× `TMP_Text` im Grid
- Hz-Label: `TMP_Text`

### 5. Safety
- ScrollView (Unity) für Zones mit Row-Template = `ToggleButton_Checkbox.prefab`
- Speed-Cap-Slider: `LargeSlider_LabelsAndIcons.prefab`
- Separation-Slider: `LargeSlider_LabelsAndIcons.prefab`
- ISO-Mode: `DropDown1LineTextOnly.prefab`
- E-Stop: `DestructiveButton_IconAndLabel_UnityUIButton.prefab` (Scale 2× — groß)

### 6. Joint Control
- 6× `LargeSlider_LabelsAndIcons.prefab` (J1–J6)
- 6× Reset-IconButton: `BorderlessButton_IconAndLabel_UnityUIButton.prefab`
- Master-Reset: `SecondaryButton_IconAndLabel_UnityUIButton.prefab`

### 7. Waypoint List
- ScrollView, Row-Template = `DropDownListButton_IconAndLabel2Lines_Toggle.prefab` (2-zeilig: Name + Koordinaten)
- Add-Buttons: `PrimaryButton_IconAndLabel_UnityUIButton.prefab` (Add at TCP) + `SecondaryButton_IconAndLabel_UnityUIButton.prefab` (Add at Hand)
- Clear-All: `DestructiveButton_IconAndLabel_UnityUIButton.prefab`

### 8. Path Execution
- Run/Simulate/Pause/Cancel: Primary/Secondary/Secondary/Destructive-Buttons
- Speed-Slider: `LargeSlider_LabelsAndIcons.prefab`
- Loop: `ToggleButton_Switch.prefab`
- Progress-Bar: Unity `Image` Filled-Horizontal (custom)

### 9. Overlays
- 9× `ToggleButton_Checkbox.prefab` — jeder Overlay
- 3× `MediumSliderWithLabelsAndIcons.prefab` — Ghost/Skeleton/Master-Opacity

### 10. Voice / LLM
- Wake-Word: `ToggleButton_Switch.prefab`
- Language: `DropDown1LineTextOnly.prefab`
- Transcript-Scroll: ScrollView, Row = `TMP_Text`
- Confirm: `PrimaryButton_IconAndLabel_UnityUIButton.prefab`
- Cancel: `DestructiveButton_IconAndLabel_UnityUIButton.prefab`

### 11. Demo & Presets
- Saved-Paths-Scroll: Row = `DropDownListButton_IconAndLabel_Toggle.prefab`
- Save: `PrimaryButton_IconAndLabel_UnityUIButton.prefab`
- Layout-Referenz: `GridMenuExample3x3.prefab`

### 12. Teleop
- Enable: `ToggleButton_Switch.prefab` (groß)
- Sensitivity: `LargeSlider_LabelsAndIcons.prefab`
- 6× Axis-Lock: `ToggleButton_Checkbox.prefab` im Grid
- Reference-Frame: `DropDown1LineTextOnly.prefab`
- Deadzone + Speed-Cap: 2× `LargeSlider_LabelsAndIcons.prefab`
- Zero-Pose: `SecondaryButton_IconAndLabel_UnityUIButton.prefab`

### 13. Envelope
- Show/Hide: `ToggleButton_Switch.prefab`
- Style + Payload + Color: 3× `DropDown1LineTextOnly.prefab`
- Transparency: `MediumSliderWithLabelsAndIcons.prefab`
- Reachable-Only: `ToggleButton_Checkbox.prefab`
- Recompute: `BorderlessButton_IconAndLabel_UnityUIButton.prefab`

### 14. Twin
- Ghost-Opacity + Scale: 2× `MediumSliderWithLabelsAndIcons.prefab`
- IST-Hologram: `ToggleButton_Switch.prefab`
- Display-Mode + URDF-Variant: 2× `DropDown1LineTextOnly.prefab`
- Show-Frames: 3× `ToggleButton_Checkbox.prefab`

### 15. Motor Loads
- 6× Vertikal-Bar-Graph: Unity `Image` Filled-Vertical (custom Shader)
- Joint-Chips: 6× `TextTileButton_IconAndLabel_Regular_UnityUIButton.prefab`
- Historie-Chart: Custom LineRenderer-on-Canvas
- Reset-Peak: `BorderlessButton_IconAndLabel_UnityUIButton.prefab`
- Export-CSV: `PrimaryButton_IconAndLabel_UnityUIButton.prefab`

### 16. Ergonomics / Body (Step 19)
- Body-Tracking-Enable: `ToggleButton_Switch.prefab`
- Skeleton-Visibility: `ToggleButton_Checkbox.prefab`
- Opacity + Thresholds: 2× `MediumSliderWithLabelsAndIcons.prefab`
- 3× Warning-Checkboxes: `ToggleButton_Checkbox.prefab`
- Reset-Session: `SecondaryButton_IconAndLabel_UnityUIButton.prefab`

### Tutorial Panel (CarouselPanel)
- Slide-Layout-Ref: `Dialog2Button_ImageVideoAndText.prefab` oder `ContentUIExample-VideoPlayer.prefab`
- Prev/Next: 2× `BorderlessButton_IconAndLabel_UnityUIButton.prefab`
- Dots: Unity `Image` Instances im Grid (custom)

---

## Referenzen

- Meta UI-Set Samples: `com.meta.xr.sdk.interaction.samples` → `UISetSamples.unity`
- Meta Canvas + Raycast Pattern: [developers.meta.com/horizon/documentation/unity/unity-isdk-canvas-interaction](https://developers.meta.com/horizon/documentation/unity/unity-isdk-canvas-interaction)
- Grab-Translate-Only Pattern: Meta `FirstHand` Sample, `PanelGrabbable.prefab`
- Meta Movement SDK (Body Tracking): `com.meta.xr.sdk.movement` (Quest 3 v71+)
- Hand-Pose-Authoring (für Palm-Detection): [developers.meta.com/horizon/documentation/unity/unity-isdk-hand-pose-authoring](https://developers.meta.com/horizon/documentation/unity/unity-isdk-hand-pose-authoring)
