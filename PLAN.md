# GoHolo → Meta Quest 3 — Projektplan

**Datei umbenannt von HANDOFF.md → PLAN.md am 2026-04-23** (lebender Plan, kein einmaliger Session-Übergabe-Zettel).

**Letzter Arbeitsschritt (2026-04-23):** Gesten-Vokabular finalisiert (Unified Swipe-Regel, Mode-Gating, Dual-Use Palm-zum-Roboter), Gesten-Stack + Ghost-Overlay-Kern + VLM-Service + Audio-Stack komplett scripted. Sieben Commits `fa9557b..d1209b5`.

## Session-Stand 2026-04-23 — was neu drin ist

**Docs:**
- PLAN.md (dieser Text), [docs/gesture-vocabulary.md](docs/gesture-vocabulary.md) mit Mode-Gating + Erkenner-Priorität + Meta-OS-reservierten Gesten

**Unity-Scripts (alle SDK-frei, kompilieren ohne Meta XR SDK dank Interface-Stubs):**
- Gesten (`Scripts/Interaction/Gestures/`): `IHandPoseProvider`, `MockHandPoseProvider`, `OVRHandPoseProvider` (`#if METAMOVE_META_SDK`), `GestureRouter` mit Mode-State-Machine (Waypoint/Teleop/Jog/Command), `SwipeGestureController` (Unified Palm-Normal), `BeckonGestureController`, `HoldStopController` (Dual-Mode), `SpatialPinchController` + `IWorldSurfaceProbe`, `PhysicsSurfaceProbe` (Editor), `MRUKSurfaceProbe` (`#if METAMOVE_MRUK`), `GestureToGhostBridge`
- Robot (`Scripts/Robot/`): `IRobotCommandSink`, `MockRobotSink`, `GhostRobotController` (Ghost-Overlay-Kern, Step 9), `EgmRobotSink` (Adapter über bestehenden `EgmClient`)
- AI (`Scripts/AI/`): `VlmClient` (Gemma 3 HTTP-Client), `PassthroughFrameSource` (Editor-RTT + Meta-PCA-guarded), `JarvisVlmBridge` (Frame→VLM→TTS)
- Audio (`Scripts/Audio/`): `MusicManager` (State-Crossfade), `RobotSoundFX` (3D-Servo-Whine), `AmbientFactoryLoop` (Teleop-Ducking)
- Settings: `GestureConfig` ScriptableObject

**Services:**
- `ai-services/vlm-gemma/` — FastAPI-Proxy auf Ollama, Endpoints `/describe` + `/v1/chat/completions`, Port 8770

**End-to-End-Loop im Editor testbar** (ohne Quest/SDK): `MockHandPoseProvider` Transforms bewegen → Gesten-Events → Ghost-Step → `MockRobotSink` Console-Log. Spatial Pinch gegen Plane mit Collider via `PhysicsSurfaceProbe`.

## Was morgen als Erstes zu tun ist

1. **Unity öffnen, Compile prüfen** — alle neuen Scripts compilen hoffentlich ohne Warnings. Meta-Files werden beim Import erzeugt. Wenn einzelne Scripts meckern: hier weitermachen.
2. **Scene_GestureTest.unity anlegen** unter `Assets/MetaMove/Scenes/Playground/` — GameObjects: `GestureRouter`, `MockHandPoseProvider` mit zwei Child-Transforms (LeftHand/RightHand), `GhostRobotController` + `MockRobotSink`, alle Gesten-Controller, `GestureToGhostBridge` gewired, Plane mit Collider für SpatialPinch. **Manuelle Unity-Arbeit.**
3. **Scripting Define Symbols setzen** sobald Meta SDK importiert: `METAMOVE_META_SDK`, `METAMOVE_MRUK`, `METAMOVE_META_PCA`.
4. **Audio-Assets besorgen** — Jarvis-Soundtrack (royalty-free AC/DC-Alternative), Factory-Ambient-Loop, Servo-Whine — Freesound.org Starter-Set.
5. **Offene Konflikte abarbeiten** falls Compile/Runtime-Probleme auftauchen.

## Noch offene autonome Kandidaten (weiter autonom codebar)

- `PathScaleController.cs` — 2-Hand-Spread-Math für Path-Scale (Step 8)
- `RwsRobotSink.cs` — RWS-Parallel zu EgmRobotSink (Alternative Transport-Schicht)
- `GoFaVisualFeedback.cs` — IK-Reachability-Farbcode (grün/gelb/rot) fürs SpatialPinch-Reticle
- `JointArcPokeStepper` → Gesture-Mode-Aware machen (existiert schon, nur gaten)
- Safety-Zone-Breach-Handler + HoldStop-Kopplung
- **Demo-Tutorial-Panels** (Step 22) — `TutorialPanel.cs` + `TutorialPanelSpawner.cs` + `HandAnimationLoop.cs` für Messe-taugliche Onboarding-UI im Meta-First-Hand-Stil (Video + virtuelle Hand + Text pro Geste)

---

---

# Urspünglicher Plan (Stand 2026-04-17)

**Datum:** 2026-04-17
**Letzter Arbeitsschritt (damals):** Docker/ROS 2 Jazzy Stack fertig, Gesten-Skeletons gelegt, Unity-Entscheidung auf 6.4 (6000.4.0f1) umgestellt

## Quick-Reference für neue Claude-Session

- **Plan-Datei**: `C:\Users\BitschE\.claude\plans\delegated-crafting-church.md`
- **Working-Dir**: `C:\GoHolo\` (außerhalb OneDrive)
- **Memory**: automatisch in `C:\Users\BitschE\.claude\projects\C--GoHolo\memory\`
- **Backup**: OneDrive-Version `C:\Users\BitschE\OneDrive - AIT\Dokumente\GoHolo\` bleibt als Backup

## Projekt-Struktur

```
C:\GoHolo\
├── docker/                                  # ROS 2 Jazzy Dockerfile + compose (WSL-nativ)
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── entrypoint.sh
├── docs/
│   └── gesture-vocabulary.md                # Gesten + Safety + Proxy-Modes
├── GoHolo/
│   ├── ABB/                                 # ROS 2 Pakete (alle lokal)
│   │   ├── abb_egm_rws_managers
│   │   ├── abb_gofa_custom/
│   │   │   ├── abb_crb15000_moveit          # ⭐ FERTIGE MoveIt-Config für GoFa
│   │   │   └── abb_crb15000_support         # URDF + xacro GoFa 5kg 950mm
│   │   ├── abb_libegm
│   │   ├── abb_librws
│   │   ├── abb_ros2/                        # PickNik-Driver, bringup, hardware_interface
│   │   └── abb_ros2_msgs
│   ├── ABBGoFa_HoloLens2/                   # Original HoloLens (Unity 2019.4)
│   ├── ABBGoFa_Quest3/                      # NEU — Unity 6.4 Quest-Projekt
│   │   └── Assets/Scripts/Gestures/         # C# Skeletons für Gesten-Router
│   └── GoHolo_Simulation.rspag              # RobotStudio Pack-and-Go
└── HANDOFF.md                               # diese Datei
```

## Launch-Command im Docker-Container

```bash
wsl -d Ubuntu-24.04 -- docker exec -it goholo_ros2 bash
# Im Container:
source /opt/ros/jazzy/setup.bash && source /ros2_ws/install/setup.bash
ros2 launch abb_bringup abb_control.launch.py \
  description_package:=abb_crb15000_support \
  description_file:=crb15000_5_95.xacro \
  launch_rviz:=false \
  use_fake_hardware:=false \
  rws_ip:=192.168.125.1 \
  rws_port:=443
```

- **Echter GoFa**: IP 192.168.125.1, RWS Port 443 (HTTPS)
- **Virtual Controller**: IP vom VC (lokal meist 127.0.0.1 oder VC-spezifisch), RWS Port 80 HTTP

## Stand im Plan

| Schritt | Status |
|---------|--------|
| 0 — HoloLens-Projekt inspiziert, Panels exportiert | ✅ |
| 1 — Unity-Version-Entscheidung: **6.4 (6000.4.0f1)** statt 6.3 LTS (Proxy-Validation-Fails) | ✅ |
| 2 — Neues URP-Projekt `ABBGoFa_Quest3/` anlegen mit Android Build Support | ⏳ **NÄCHSTES** |
| 2b — **Project Restructure (Clean Slate)**: Folder-Layout `Assets/_GoHolo/{Scenes,Prefabs,Scripts/{Gestures,Haptics,Visualization,UI,Robot,Safety},Materials,Textures,Icons,HandPoses,Settings}`. Scene-Split: `Bootstrap.unity` (Entry, additive-lädt Rest) + `Scene_Robot.unity` + `Scene_UI.unity` + `Scene_Safety.unity` + Dev-Sandboxes. Bestehende Sample-Hack-Szene mit GoFa → extrahiere GoFa als Prefab `_GoHolo/Prefabs/GoFa.prefab`, archiviere alte Szene nach `_GoHolo/Scenes/Playground/` (für Ad-hoc-Tests behalten). System-Config als ScriptableObjects (`RobotConnectionConfig`, `SafetyConfig`, `HapticsConfig`) statt statischer Singletons. | ⏳ |
| 3 — Meta XR Core SDK + Interaction SDK Samples importieren | ⏳ |
| 3a — Quest 3 über USB/AirLink verbinden, Dev-Mode aktivieren | ⏳ |
| 3b — Meta Sample-Szene `PoseExamples.unity` auf Quest deployen, Gesten-Smoke-Test | ⏳ |
| 4 — Gesten-Skeleton-Scripts mit Meta SDK-Events verdrahten (Adapter-Layer) | ⏳ (Skeletons liegen) |
| 5 — Eigene GoHolo-Gesten (Pinch-Tap/Drag, Daumen-Jog, OK-Ring, 2-Hand-Spread) als HandPose-Assets capturen | ⏳ |
| 6 — RobotStudio Virtual Controller aus `.rspag` starten (EGM + RWS bit-identisch zum echten GoFa) | ⏳ |
| 7 — EGM UDP-Client in Unity (protobuf) gegen VC testen | ⏳ |
| 8 — ArUco-Kalibrierung + Spatial Anchor für Roboter-Basis | ⏳ |
| 9 — **Ghost-Overlay (Kern-Architektur)** + Proxy-Handle + Trajectory-Rendering: solider Holo-Roboter zeigt IST-Pose (EGM-Feedback live), semi-transparenter Ghost zeigt SOLL-Pose (User-Editier-Zustand). Alle Joint-/TCP-Manipulationen laufen ausschließlich am Ghost. Kein Direct-Live-Control. Commit-Geste (OK-Ring) sendet Ghost-Pose an Roboter → IST-Hologramm holt auf | 🔄 Controller-Logik in `GhostRobotController.cs` + `IRobotCommandSink` + `EgmRobotSink`; Visualizer/Proxy-Handle-Mesh-Wiring fehlt |
| 10 — Safety Zones (ISO/TS 15066) + Live-Teleop + Pfad-Recording | ⏳ |
| 11 — Docker + ROS 2 Jazzy + MoveIt abb_crb15000_moveit | ✅ Image gebaut, alle 16 ABB-Pakete kompiliert |
| 11b — Gazebo-Sim (optional, nur falls Physik/Payload/Kollisionen brauchen) | ⏳ optional |
| 12 — React + Vite Dashboard + Vuplex WebView | ⏳ |
| 13 — Voice + LLM Task Interface (natürlichsprachliche Kommandos) | ⏳ |
| 13a — Meta Voice SDK Integration + Wake-Word "Hey GoFa" | ⏳ |
| 13b — Claude API Unity-Client mit JSON-Tool-Use-Schema für Task-Primitives | ⏳ |
| 13c1 — Objekt-Grounding Tier 1: ArUco-Marker (OpenCV, lokal, <50 ms) | ⏳ |
| 13c2 — Objekt-Grounding Tier 2: Gaze+Pinch "das da" (Quest Depth Raycast) | ⏳ |
| 13c3 — Objekt-Grounding Tier 3: Grounding DINO / SAM 2 auf RTX 3080 Laptop (optional Node) | 🔄 Skeleton angelegt |
| 13c4 🔄 — **Objekt-Grounding Tier 4: VLM-Beschreibung (Gemma 3 Vision)** — natürlichsprachliche Objekt-Erkennung via lokales Vision-Language-Model. User hält Objekt hoch oder zeigt mit Spatial Pinch drauf + Voice „Was ist das?" / „Was halte ich da?" → Quest **Passthrough Camera API** (PCA, v74+, braucht `horizonos.permission.HEADSET_CAMERA`) liefert Frame → Frame + User-Prompt geht an lokalen Gemma-3-VLM-Service (Docker `goholo_vlm` auf RTX 3080, OpenAI-kompatibles API via vLLM oder ollama) → Antwort zurück → Jarvis-TTS (Step 13 Stack) spricht die Antwort. **Abgrenzung zu 13c3:** DINO/SAM gibt strukturierte Bounding-Boxes + Labels (für Pick-and-Place-Targets), Gemma gibt konversationelle Beschreibung (für User-Info, Disambiguierung, „welches davon meinst du?"). **Use-Cases:** Werkstück-Identifikation beim Teach-In, Fehler-Diagnose („was hängt da am Gripper?"), Safety-Check („sehe ich etwas im Arbeitsraum?"), Operator-Onboarding. Implementation: `VlmClient.cs` in `Assets/MetaMove/Scripts/AI/`, Service-Ordner `ai-services/vlm-gemma/`. Trigger-Geste optional: Objekt in Hand + Palm-Up-Curl-nach-Halten (länger als Beckon-Threshold) → „zeige mir was ich halte". Latenz-Ziel < 2 s End-to-End. Fallback bei Service-Down: „VLM nicht erreichbar, fallback auf DINO-Labels". | ⏳ |
| 13d — MoveIt Task Constructor Pick-and-Place Primitive | ⏳ |
| 13e — Voice-Confirmation-Loop: Kommando → Ghost zeigt Plan → OK-Ring Commit | ⏳ |
| 14 — Visualization-Overlays (Port aus HoloLens, brauchen Live-EGM/RWS-Daten) | ⏳ |
| 14a — **Show Torque**: Joints farbcodiert nach aktuellem Drehmoment (grün → gelb → rot, Schwellen aus RWS Joint Limits) | ⏳ |
| 14b — **Show Angles**: Gauge-Anzeige pro Joint (aktueller Winkel + Min/Max-Range, wie HoloLens `gaugeAngle.cs`) | ⏳ |
| 14c — **Show Pose**: Live TCP-Pose-Panel (XYZ-Position + RPY-Orientierung, aktualisiert mit EGM-Feedback) | ⏳ |
| 14d — **Working Envelope**: Arbeitsraum-Visualisierung als semi-transparente Hülle (aus URDF joint-limits → Monte-Carlo-Sampling oder analytische Envelope) | ⏳ |
| 15 — Feature-Parität HoloLens (weitere Ports) | ⏳ |
| 15a — **Joint Control 2–6**: Arc-Handles + Knob-Affordance für Joints 2–6 (Achsen/Limits aus URDF xacro, gleiche Interaction wie J1). **Bewegt Ghost-Joints, nicht IST-Hologramm.** Commit via OK-Ring sendet an Roboter (Step 9-Architektur) | ⏳ |
| 15b — **Create Path UI**: Waypoint-Workflow (Punkt platzieren via Pinch → Waypoint-Liste → Edit/Reorder/Delete → Validierung via Farbcode → Send to Robot). Port aus `CTRL_HoloPath/` | ⏳ |
| 15c — **Simulate Wizard** (wie HoloLens): Waypoints werden mit `simulate:true`-Flag an PC-Server → RobotStudio Virtual Controller gesendet. VC führt das RAPID-Programm aus (kein reales Movement), sendet EGM/RWS-Pose-Feedback zurück → Ghost-Robot in Quest spielt die tatsächliche Controller-Bewegung ab. Bit-identisch zum Real-Run, erkennt Reichweiten/Kollisionen/Singularitäten bevor commit | ⏳ |
| 15d — **Demo-Mode**: vorprogrammierter Demo-Path den Roboter auf Knopfdruck abspielt (für Messen/Vorstellungen, `CMD_RANDOMPATH`-Äquivalent) | ⏳ |
| 15e — **HOME-Mode**: sichere Park-Position + Idle-State (Roboter stoppt, alle Interactables deaktiviert, Status-Anzeige "Home"). Nicht zu verwechseln mit Emergency-Stop aus Step 10 | ⏳ |
| 16 — **bHaptics TactGloves 2 Integration**: Vibrations-Feedback für Pinch/Grab/Safety-Zone/Commit via bHaptics Unity SDK, `.tact`-Pattern-Assets, `BHapticsAdapter.cs` entkoppelt von Meta SDK (No-Op-Fallback). Safety-Zone-Puls eskaliert mit Nähe | ⏳ |
| 17 — **AR Spatial Ruler + 3D Path Preview**: weiße Maßstabslinie (10 cm Minor-Ticks, 100 cm Major-Ticks + Label) Hand↔Roboter-Basis-Anchor. Waypoint-Pfad-Preview vor Commit mit IK-Farbcode (grün/gelb/rot) + optionalem Scrub-Ghost entlang Trajektorie | ⏳ |
| 18 — **3-Layer UI (L1 Radial-Home / L2 Panels / L3 Physical Fixtures)**: Palm-Up öffnet Radial als **App-Home-Screen** mit 8 Sektions-Wedges (Status/Control/Path/Safety/Motors/Body/Voice/System). Wedge-Click spawnt passenden Floating-Panel-Set (L2, 14 Panels). L3 Physical Fixtures permanent im Raum: **Glass Pedestal** mit GoFa-Twin (`Desk.prefab` + URP Glass), **Physical E-Stop Mushroom** (`[BB] Pokeable Plane`), **Curved Scroll Lists** (`CanvasCylinder`), **3D Glass Poke-Buttons** (Meta `PokeButton.prefab`), **Spatial Anchor Pucks**, **Ambient Floor Grid** (MRUK). Alles Meta-native — Custom-Code nur für Radial-Math + Panel-Lifecycle. Details + Asset-Inventory → [docs/ui-panels.md](docs/ui-panels.md). | ⏳ |
| 19 — **Body Pose / Ergonomics**: Meta **Movement SDK** + **Body Tracking SDK** (Upper-Body, Quest 3 v71+). Skeleton-Visualization toggle, Reach-/Twist-Warnings, Operator-in-Robot-Zone-Detection (koppelt Step 10 Safety + Step 16 bHaptics), Posture-Heatmap, optional RULA/REBA-Score. Panel: **Ergonomics / Body Panel** aus `docs/ui-panels.md`. | ⏳ |
| 22 — **Demo-Tutorial-Panels (Meta-First-Hand-Stil)**: für Messen/Vorstellungen floating Panels die jede Geste/Interaktion erklären — direkt aus Metas eigener UX-Bibliothek kopiert. Pro Panel: **(a) Loop-Video** der Geste (kurzer `.mp4`/`.webm` im `VideoPlayer`, UI-RawImage Target, ~3–5 s Loop), **(b) animierter Virtueller-Hand-Ghost** der die Geste live vormacht (Meta Hand-Visual-Prefab mit Recorded-Animation-Clip aus `HandPoseRecorder`, loopt synchron zum Video), **(c) Text-Beschreibung** mit Titel + 1–2 Zeilen Kurzerklärung + optional Schritt-Liste. Panels spawnen **automatisch mode-kontextuell**: User wechselt in Command-Mode → Tutorial-Panels für Swipe/Beckon erscheinen am Glass-Pedestal-Rim; Teleop-Mode → Pinch-Drag + Soft-Stop; Waypoint-Mode → Pinch-Tap + Spatial-Pinch + OK-Ring. **Dismiss** via Pinch-auf-Close-Button oder Auto-Fade nach X Sekunden Gesten-Erfolg. **Tutorial-Modus-Toggle** in L2 System-Panel („Show tutorials for new users"). Referenz: Meta First-Hand Sample, `com.meta.xr.sdk.interaction.samples` Szenen `PoseExamples.unity` / `HandGrabExamples.unity` — deren Panel-Layout 1:1 klonen. Asset-Aufnahme: `VideoCaptureTool` Editor-Script das während Gesten-Ausführung am Quest per USB aufnimmt + automatisch als Asset importiert. Scripts: `TutorialPanel.cs` (Prefab-Controller), `TutorialPanelSpawner.cs` (Mode-Router), `HandAnimationLoop.cs` (Ghost-Hand-Recorder-Player). Panel-Visual: L2-Panel-Style aus [docs/ui-panels.md](docs/ui-panels.md), Curved Canvas, Glass-Material. | ⏳ |
| 21 🔄 — **Audio-Stack (Soundtrack + spatial FX)**: `MusicManager` State-Crossfade für Szenen-Musik (Iron-Man-Style, Idle/Working/Alert/Celebration), `RobotSoundFX` 3D-Servo-Whine skaliert mit TCP-Velocity + Commit-Click + Abort-Beep, `AmbientFactoryLoop` Factory-Ambient mit Teleop-Ducking via `GestureRouter.OnModeChanged`. Meta XR Audio SDK für HRTF-Spatialization (drop-in). Audio-Assets (Soundtrack + Whine + Ambient) noch zu besorgen — Freesound.org + royalty-free Epidemic/Artlist für Soundtrack. | 🔄 Code fertig, Clips fehlen |
| 20a 🔄 — **Spatial Pinch (Point-at-World)**: Waypoint/Go-To-Target direkt auf realen Oberflächen setzen. User zeigt mit Zeigefinger auf Tisch/Werkstück/Boden + Pinch-Tap → Hand-Ray trifft **MRUK-Scene-Mesh** (Room/Surface aus Meta Scene Understanding) → TCP-Target wird an exakt diesem Welt-Punkt angelegt, Höhe optional via Surface-Normal-Offset (z.B. +5 cm über Tischplatte für Greif-Approach). Während Ziel-Phase zeigt **Reticle-Preview** (Kreis/Crosshair) auf der Oberfläche, Farbcode nach IK-Reachability (grün/gelb/rot, live). Nutzt Meta `RayInteractor` aus `OVRInteractionComprehensive` + MRUK `RoomMeshAnchor` Surface-Hit-Test — kein Custom-Raycast. Implementierung: `SpatialPinchController.cs` in `Assets/MetaMove/Scripts/Gestures/`. Abgrenzung zu 13c2: dort Voice-Grounding („das da"), hier direkte Gesten-Primitive fürs Waypoint-Legen ohne Voice-Kontext. | ⏳ |
| 20 🔄 — **Command-Mode-Gesten (Disney-Lamp-Style)**: semantische Kommando-Gesten ergänzen das präzise Pinch-Drag-Vokabular — Roboter wird wie Hund/Disney-Lampe dirigiert. **Unified Swipe** — Palm-Normal definiert Richtung, Flick in diese Richtung → TCP-Step dorthin. Ein Regel-Set für alle 6 Raumachsen (±X, ±Y, ±Z), beide Hände, egal welche. Mentales Modell: *„Palme zeigt wohin, flicken."* Deckt auch „Shoo" (Palm-vorn + Flick = Step weg) ab — kein eigener Shape. **Beckon** (Palm nach oben, Finger rollen ein — universelles „komm her") bleibt als **shape-basierte Alternative** für „zum User ziehen" wenn Bewegungsraum fehlt; unterscheidet sich von Swipe-nach-oben durch stationäre Hand (Velocity < 0.3 m/s) + Finger-Curl statt Translation. Erkennung: Meta `ShapeRecognition` (Flat-Hand / Palm-Up-Curl / Back-of-Hand) + Velocity-Threshold > 1.2 m/s < 400 ms auf dominanter Weltachse. Step-Distanz default 10 cm, skaliert mit Wisch-Amplitude (5–20 cm Clamp). **Atomar** — jede Geste = ein Einzel-Schritt, Roboter stoppt dazwischen selbst → ISO/TS-15066-freundlich, kein kontinuierliches Mitschleifen. **Mode-Gating** im `GestureRouter`: nur im Command-Mode aktiv (nicht während Pinch-Drag-Teleop oder Jog, sonst False-Positives durch Palm-Orientierungs-Konflikte mit Soft-Stop). Soft-Stop ist im Command-Mode redundant (Roboter steht eh) → Palm-zum-Roboter-Konflikt strukturell gelöst. Scripts: `SwipeGestureController.cs` + `BeckonGestureController.cs` in `Assets/MetaMove/Scripts/Gestures/`. bHaptics-Puls (Step 16) bestätigt jede erkannte Geste. Details → [docs/gesture-vocabulary.md](docs/gesture-vocabulary.md) Mode-Gating-Tabelle. | ⏳ |

## Meta Building Block Mapping

Jede Interaktion wird nativ über einen Meta XR Interaction SDK 85 Building Block umgesetzt — keine Custom-Scripts für Standardverhalten. Einziger bleibender Custom-Code: `GoFaCCDIK` (IK-Mathe) + `JointArcVisual` (weißer Bogen, LineRenderer).

| Phase | Interaktion | Meta Building Block / Component | Verantwortlich in Scene |
|---|---|---|---|
| aktuell | Camera + Hands + Interactors | `OVRInteractionComprehensive.prefab` | `CameraRig` |
| aktuell | Table | `Desk.prefab` (MRDesk) | `Desk` |
| aktuell | End-Effektor near-pinch | `HandGrabInteractable` + `Grabbable` + `GrabFreeTransformer` | `GoFa/IKHandle` |
| aktuell | End-Effektor distance-pinch | `DistanceHandGrabInteractable` | `GoFa/IKHandle` (zusätzl.) |
| aktuell | Joint-Rotation (×6) | `OneGrabRotateTransformer` + `HandGrabInteractable` + `Grabbable` | `GoFa/Joint_N/RotaryHandle_N` |
| aktuell | Joint-Arc Visual | `JointArcVisual` (Custom LineRenderer, weiß) | `GoFa/Joint_N/Arc_N` |
| 3b/3c | Hand-Pose-Recognition (ThumbsUp, OKRing, StopHand) | `ActiveStateSelector` + `ShapeRecognition` | eigenes GO pro Geste |
| 3c2 | Gaze+Pinch "das da" Grounding | `HandRayInteractor` (im Comprehensive rig) + Ray-Cast auf `RayInteractable` | Targets in Scene |
| 9 | Ghost-Proxy-Handle (Trajectory-Anpassung) | `HandGrabInteractable` + `OneGrabFreeTransformer` an Proxy-GameObject | `GhostRobot/ProxyHandle` |
| 9 | Path-Commit via OK-Ring Geste | `ActiveStateSelector` + `ShapeRecognition` → UnityEvent | `ConfirmGesture` GO |
| 10 | Emergency-Stop Button | `[BB] Pokeable Plane.prefab` + rotes Material + `PokeInteractable` + `UnityEvent` | Wand- oder Tisch-Mount |
| 10 | Safety-Zone-Edit-Handles | `DistanceHandGrabInteractable` + `OneGrabTranslateTransformer` an Zone-Corners | pro Zone |
| 12 | React-Dashboard im VR | `PointableCanvas` + `RayInteractable` + Vuplex WebView | WebView-Panel |
| 13 | Task-Status-UI (Voice-Feedback) | `PointableCanvas` + UI-Set Primary/Secondary Buttons | Floating Panel |
| 13 | Confirm / Cancel Buttons | UI-Set `Button_Primary_Large.prefab` / `Button_Destructive.prefab` | auf Status-Panel |
| 13e | Voice-Confirmation-Commit | `ActiveStateSelector` (OK-Ring) oder Primary-Button-Click | — |

**Prinzipien:**
- Grab-Interaktionen → immer `Grabbable` + passender `*Transformer` (Free / Rotate / Translate / Scale) + `HandGrabInteractable` (für Near) und/oder `DistanceHandGrabInteractable` (für Ray)
- Button-Press → `PokeInteractable` (3D) oder Canvas-Button + `RayInteractable` (UI)
- Hand-Pose-Detection → nie Custom-Code, immer `ShapeRecognition` + `ActiveStateSelector`
- Alles über denselben Interactor-Pool aus `OVRInteractionComprehensive` → keine doppelten Hand-Refs

**Nicht-BB-Custom (absichtlich):**
- `GoFaCCDIK.cs` — pure mathematische IK, keine UI-Logik, kein BB-Ersatz sinnvoll
- `JointArcVisual.cs` — minimaler LineRenderer-Arc; Metas ArcAffordanceController wäre schöner, braucht aber eine Skinned-Mesh-Rig-Copy aus `PanelWithManipulators.prefab` (fragil, deshalb Custom-Fallback)
- `PinchDragSceneSetup.cs` — Editor-Tool das Meta-Components programmatisch wired (ersetzbar wenn Meta QuickActions dieselbe Automation bieten)

## Wichtige technische Entscheidungen

- **Unity**: **6000.4.0f1 (Unity 6.4)** — 6.3 LTS Download scheiterte wiederholt an AIT-Proxy (Installer-Integrity-Check failed). 6.4 ist neuer, Meta XR SDK-kompatibel (unterstützt Unity 6.0+). LTS-Downgrade später möglich falls nötig.
- **Projekt**: Clean-Start-URP, **nicht** Migration (80% des Stacks wird eh ersetzt)
- **Aus HoloLens übernehmen**: nur `Assets/Resources/CAD/*.fbx` (GoFa LINK0–6)
- **Kommunikation**: EGM UDP Port 6511 (250 Hz) statt TCP/IP
- **Marker-Tracking**: OpenCV for Unity + ArUco (statt Vuforia)
- **UI**: React 19 + Vite + Tailwind + shadcn/ui, im VR via Vuplex WebView
- **ROS**: ROS 2 Jazzy in Docker **nativ in Ubuntu-24.04 WSL** (nicht Docker Desktop — wegen EGM-UDP `network_mode: host`), abb_ros2 + abb_crb15000_moveit (alles lokal + gebaut)
- **Simulation**: RobotStudio Virtual Controller als primärer Test-Backend (echte RobotWare-Firmware → EGM/RWS bit-identisch). Gazebo nur optional.
- **Perception**: 3-Tier-Fallback-Kette — ArUco-Marker (Tier 1, immer verfügbar) → Gaze+Pinch-Pointing (Tier 2, nutzt Quest Depth) → Grounding DINO auf RTX 3080 (Tier 3, optional Docker-Service `goholo_perception` auf Port 8000, Health-Check entscheidet über Availability)
- **LLM**: Claude API für Natural-Language-Task-Parsing (Sprache → strukturiertes JSON via Tool-Use). Nicht für Trajektorien-Generierung — Motion Planning bleibt bei MoveIt2+OMPL+Pilz (deterministisch, ISO/TS-15066-zertifizierbar). Ghost-Overlay zeigt geplante Trajektorie vor Commit.
- **Gesten-Architektur**: entkoppelt — Controller-Scripts in `Assets/Scripts/Gestures/` nutzen nur UnityEngine, Meta-SDK-Events werden via Adapter angedockt → kompilieren vor SDK-Import
- **Roboter**: ABB GoFa CRB 15000 5kg 950mm, IP 192.168.125.1, RobotWare 7.x mit EGM

## Was als nächstes zu tun ist

**Phase A — Unity-Projekt aufsetzen (manuell in Unity Hub)**

1. Unity Hub öffnen → **New Project** → Template: **Mixed Reality** (für Quest 3 Passthrough — bringt OVR Camera Rig, XR Plugin Management mit Oculus-Provider, Android Target, URP vorkonfiguriert). Falls nicht direkt sichtbar: im Template-Picker auf `Add Template` / Feature-Set nachziehen. → Version **6000.4.0f1** → Path: `C:\GoHolo\GoHolo\ABBGoFa_Quest3\`
2. Nach Projekt-Öffnung prüfen (sollte alles bereits gesetzt sein):
   - Build Target: Android (Texture Compression ASTC)
   - XR Plug-in Management → Android tab → ✅ Oculus
   - Passthrough Layer aktiv in der Beispiel-Szene
3. **Player Settings** (nur falls nicht durch Template gesetzt):
   - Company: AIT, Product: GoHolo
   - Minimum API Level: Android 10 (API 29), Target: Automatic (Highest Installed)
   - Scripting Backend: IL2CPP, Architecture: ARM64
   - Color Space: Linear

**Phase B — Meta XR SDK importieren (Package Manager)**

MR-Template bringt `com.meta.xr.sdk.core` + `com.meta.xr.sdk.interaction` schon mit. Nur zusätzlich nachziehen:

4. `Window → Package Manager → + → Add package by name`:
   - `com.meta.xr.sdk.interaction.samples` → `PoseExamples.unity`, `HandGrabExamples.unity` (Gesten-Smoke-Test)
   - `com.meta.xr.mrutilitykit` → MRUK für Room/Scene Understanding (Phase 8 Safety-Zonen, Roboter-Basis-Kalibrierung)
   - (optional) `com.meta.xr.sdk.voice` → Voice Commands als Fallback für Nebenaktionen
5. Falls OVR Project Setup-Popup erscheint → **Fix All** (sollte bei MR-Template minimal sein)

**Phase C — Smoke-Test auf Quest**

6. Quest 3 per USB anstöpseln (oder AirLink/Link aktivieren) → Developer-Mode + USB-Debug aktiv
7. `File → Build Settings → Add Open Scene` → Szene `PoseExamples.unity` aus Samples-Ordner einziehen
8. **Build and Run** → APK wird auf Quest installiert und startet
9. Gesten durchspielen: Thumbs Up, OK-Ring, Pinch, Point, Peace — prüfen welche zuverlässig erkannt werden

**Phase D — Unsere Gesten wiren**

10. Neue Szene `GoHolo_GestureTest.unity` mit Meta `HandExamplesCameraRig` Prefab
11. Unsere `GestureRouter` + `PinchTeleopController` + Co. auf GameObjects hängen
12. Adapter-Script schreiben das Meta SDK `ActiveStateSelector`/`SelectorUnityEventWrapper` auf unsere `OnPinchBegin/End` etc. mapped
13. HandPose-Assets für unsere spezifischen Gesten (OK-Ring, Daumen-Point, Stop-Hand, Fist, Flat-Hand) per **Hand Pose Authoring Tool** im Editor capturen

## Externe Ressourcen

- Unity 6.x Docs: https://docs.unity3d.com/6000.4/Documentation/Manual/index.html
- Meta XR SDK: https://developers.meta.com/horizon/develop
- Meta Interaction SDK Samples: https://github.com/oculus-samples/Unity-FirstHand
- Meta Hand Pose Authoring: https://developers.meta.com/horizon/documentation/unity/unity-isdk-hand-pose-authoring
- abb_ros2 (lokal + https://github.com/PickNikRobotics/abb_ros2)
- Vuplex WebView: https://developer.vuplex.com/webview/overview
- OpenCV for Unity: https://enoxsoftware.com/opencvforunity/
- ROS 2 Jazzy: https://docs.ros.org/en/jazzy/
- MoveIt 2: https://moveit.picknik.ai/
