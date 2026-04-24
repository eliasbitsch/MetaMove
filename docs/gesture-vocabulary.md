# GoHolo — Gesten-Vokabular für Quest 3 MR Teleop

**Zielplattform:** Meta Quest 3 + Meta XR Interaction SDK (Hand Tracking)
**Roboter:** ABB GoFa CRB 15000 5 kg / 950 mm, RobotWare 7.x mit EGM (UDP 6511 @ 250 Hz)
**Test-Backend:** RobotStudio Virtual Controller (Schritt 1.5) vor echtem Roboter

## Gesten-Mapping

| Geste | Action | Modus |
|-------|--------|-------|
| **Pinch kurz** (< 200 ms Tap, Hand in freier Luft) | Waypoint an aktueller Hand-Pose setzen | Waypoint-Modus |
| **Spatial Pinch** (Zeigefinger zielt auf reale Oberfläche, Pinch-Tap) | Waypoint / Go-To-Target an Welt-Punkt wo Hand-Ray reale Oberfläche trifft (Tisch, Werkstück, Boden). Mit Voice-Query („was ist das?") → VLM-Beschreibung statt Waypoint (Step 13c4). | Waypoint- / Query-Modus |
| **Pinch lang + Drag** (≥ 200 ms) | Endeffektor ziehen (Position) | Live-EGM-Teleop |
| **Daumen-Point** (Hitchhiker) | Jog TCP in Daumen-Richtung, solange Geste aktiv | Jog-Modus |
| **Zeigefinger-Point** (Index gestreckt, Mittel/Ring/Klein eingerollt) | Atomar (default): ein Step in Zeigefinger-Richtung pro Strecken (vor/zurück/links/rechts/oben/unten je nach wohin der Finger zeigt). Continuous-Toggle in Config: Jog solange gehalten. | Command-Mode (Step) / Jog-Modus (Continuous) |
| **Flat-Hand horizontal, drehen** | TCP-Orientierung A4/A5/A6 (Wrist-Achsen) | Orientation |
| **Palm zum Roboter — still halten** (Stop-Hand) | Hold — Roboter friert ein, EGM hält Position | Soft-Stop (nur in Teleop/Jog aktiv) |
| **Beidhändige Faust** | 🛑 E-Stop — Motors off via RWS | Hard-Stop |
| **OK-Ring** (Daumen + Zeigefinger zu Ring) | Commit / Aufgezeichneten Pfad abspielen | Commit |
| **2-Hand Pinch-Spread** (beide Hände im Pinch, Abstand ändern) | Scale Path — aufgezeichneten Pfad räumlich skalieren | Path-Edit |
| **Swipe (alle 6 Richtungen — unified rule)** | TCP im Welt-Frame einen Step in Palm-Richtung | Command-Mode |
|   → Palm rechts + Flick | +X | |
|   → Palm links + Flick | −X | |
|   → Palm oben + Flick | +Z | |
|   → Palm unten + Flick | −Z | |
|   → Palm vorn (zum Roboter) + Flick | weg vom User (deckt „Shoo") | |
|   → Palm hinten (zum User) + Flick | zum User zu | |
| **Beckon / Come-Here** (Palm nach oben, Finger rollen zum Handballen ein — universelles „komm her") | TCP um Step-Distanz auf User zu (Safety-Clamp bei Min-Abstand) | Command-Mode |

## Meta-OS-reservierte Gesten (nicht belegbar)

Diese Gesten fängt das Quest-OS ab, bevor unsere App sie sieht — dürfen nicht belegt werden:

| OS-Geste | Wirkung im OS | Konsequenz für uns |
|---|---|---|
| **Palm-to-Face + Pinch** (Wrist-Menu) | öffnet Universal Menu / Meta-Button-Äquivalent | Unsere Beckon ist **Palm-Up + Finger-Curl (kein Pinch)** — damit kein Overlap |
| **Palm-Up + Pinch + Hold** (einige Systembuilds) | System-UI-Trigger | dito |
| Doppel-Tap am Headset | Passthrough-Toggle | irrelevant, keine Hand-Geste |

**Design-Regel:** Alle unsere Gesten müssen mit **Palm NICHT zum Gesicht** auskommen (oder ohne Pinch wenn Palm zum Gesicht). Wer das verletzt, verliert die Geste an die OS-Shell.

## Mode-Gating (welche Geste in welchem Modus aktiv)

Nicht alle Gesten werden gleichzeitig ausgewertet — sonst gibt's Palm-Orientierungs- und Velocity-Konflikte. Der `GestureRouter` hält einen **aktiven Modus** und routet nur passende Gesten:

| Modus | Roboter-Zustand | Aktive Gesten | Soft-Stop nötig? |
|---|---|---|---|
| **Teleop** | folgt Hand kontinuierlich (Pinch-Drag aktiv) | Pinch-Drag, Handkante-Hold (Soft-Stop), E-Stop | **ja** |
| **Jog** | fährt in Daumen-Richtung solange Geste hält | Daumen-Jog, Wrist-Rotation, Handkante-Hold, E-Stop | **ja** |
| **Command** | steht still, wartet auf diskrete Kommandos | Swipe (alle 6 Richtungen via Palm-Normal), Beckon, E-Stop | **nein** (Palm-zum-Roboter ist hier Swipe-vorwärts statt Stop) |
| **Waypoint** | steht still | Pinch-Tap (in-air), Spatial Pinch (on-surface), OK-Ring-Commit, 2-Hand-Scale, E-Stop | **nein** |

**Wichtiger Effekt — Swipes sind atomar:** Jede Swipe/Beckon/Shoo-Geste löst **einen einzelnen Schritt** (default 10 cm) aus. Der Roboter fährt den Schritt, stoppt, wartet auf die nächste Geste. Dadurch:

- Kein kontinuierliches Mitschleifen → vorhersagbar, ISO/TS-15066-freundlich
- **Soft-Stop im Command-Mode überflüssig** — der Roboter steht zwischen Gesten sowieso. Das löst den Konflikt „Palm-zum-Roboter = Stop vs. Swipe-Frame" strukturell: Soft-Stop wird im Command-Mode **gar nicht erst ausgewertet**, Palm-Orientierung ist dort frei.
- Step-Distanz skaliert mit Wisch-Amplitude (kleine Wisch = 5 cm, große Wisch = 20 cm), Clamp auf Safety-Range

**E-Stop** (beidhändige Faust) ist **always-on** in allen Modi, nie gegated.

**Unified Swipe-Regel:** Swipe-Richtung folgt **Palm-Normal** (wohin die Handfläche zeigt) + Flick in diese Richtung. Gilt für alle 6 Raumachsen, beide Hände, egal welche. Ein mentales Modell: *„Palme zeigt wohin ich will, flicken."*

**Dual-Use „Palm zum Roboter":** Selbe Hand-Haltung, zwei Bedeutungen, disambiguiert durch Modus + Bewegung:
- **Teleop/Jog + still halten** → Soft-Stop (Roboter friert)
- **Command-Mode + Flick vorwärts** → Swipe-vorwärts (Step weg vom User)
- **Command-Mode + still** → ignoriert (Roboter steht eh)

Reale Intuition: ausgestreckte Handfläche heißt „Halt!" wenn etwas kommt, und „hau ab!" wenn man etwas wegscheucht — selbe Pose, Kontext trennt.

**Erkenner-Priorität im Command-Mode** (first-match wins, verhindert Beckon/Swipe-Up-Kollision):
1. **Beckon** — Palm-Up + Finger-Curl Shape-Transition bei stationärer Hand (Palm-Velocity < 0.3 m/s)
2. **Swipe** — Palm-Normal definiert Richtung, Flick-Velocity > 1.2 m/s in Palm-Normal-Richtung, Dauer < 400 ms

Modus-Wechsel automatisch:
- `Pinch erkannt` → Teleop
- `Daumen-Point erkannt` → Jog
- `Keine aktive Teleop-Geste + Hand frei` → Command
- `Pinch-Tap ohne Drag-Follow` → Waypoint

## Teleop-Modi (automatisch, distanzbasiert)

Kein manueller Mode-Switch. System entscheidet nach Hand-Distanz zum echten TCP:

- **Distanz < 30 cm zum echten TCP** → **Direct-Mode**: Pinch-Drag am echten Flansch, 1:1-Mapping
- **Distanz ≥ 30 cm** → **Proxy-Mode**: virtueller Handle erscheint in Griffweite vor dem User, Drag wird auf realen TCP gespiegelt

Threshold (30 cm) konfigurierbar via `GestureConfig.ProxyDistanceThreshold`.

## Visualisierung (Option iii — full ghost + proxy + trajectory)

1. **Ghost-Overlay** auf echtem Roboter: semi-transparente Kopie des GoFa-URDF-Meshes rendert die **Ziel-Pose** (wo der Roboter HINWILL) über dem echten Roboter (wo er JETZT ist). Differenz zeigt Latenz + Regler-Fehler.
2. **Proxy-Handle** bei Proxy-Mode: schwebender Mini-GoFa oder Handle-Kugel in Griffweite vor dem User, 1:1 synchronisiert mit TCP.
3. **3D-Trajektorie**: Aufgezeichnete Waypoint-Sequenz als Line-Renderer im Raum, Farb-Codierung für Geschwindigkeit, Marker an Waypoints. Live-Vorschau während Pinch-Drag zeigt geplanten Pfad der nächsten ~500 ms.

## Safety-Überlegungen

### ISO/TS 15066 — Kollaborative Roboter
- **Power-and-Force-Limiting (PFL)** muss im Controller weiter aktiv sein, unabhängig von Gesten. Gesten dürfen PFL-Limits nicht überschreiben.
- EGM-Geschwindigkeit via **`UseSpeedLimits := TRUE`** im RAPID-EGM-Setup begrenzen.

### E-Stop-Fehlauslösungs-Analyse
Beidhändige Faust wurde gewählt, weil:
- **Einhändige Faust ist zu häufig natürlich** (gelegentliches Greifen, Kratzen, Wetter-kalt-Reflex) → hohe Fehlauslösungsrate
- **Zwei-Hand-Gleichzeitigkeit** (< 300 ms Zeitfenster) ist nahezu ausschließlich intentional
- **Timeout-Recovery:** E-Stop-Geste muss 500 ms gehalten werden → keine Auslösung bei Hand-Tracking-Jitter

### Abgrenzung E-Stop vs. Scale-Path (beide zweihändig)
- **Beidhändige Faust = E-Stop**: Finger vollständig eingerollt, beide Hände, 500 ms Halten
- **2-Hand-Pinch-Spread = Scale Path**: beide Hände im Pinch-Pose (Daumen + Zeigefinger zusammen), andere Finger entspannt
Hand-Shape-Klassifikation trennt die beiden zuverlässig — `HandPose.IsFist()` vs `HandPose.IsPinching()` sind orthogonal im Meta SDK.

Implementierung in `EmergencyStopHandler.cs` — darf nicht debounced werden, muss innerhalb 10 ms feuern wenn echte Faust erkannt.

### Fallback-Stop-Kette
1. **Handkante-Hold** (soft): EGM sendet `stop_at_current_position`, Motors bleiben an → schnelles Resume
2. **E-Stop-Geste** (hard): RWS `/rw/panel/ctrl-state` → motors off → Roboter hält nach Brake-Ramp
3. **Physischer Taster** am GoFa-Controller (höchste Autorität, SafeMove-Kategorie 0)
4. **Safety-Zone-Violation** (wenn User zu nah an aktiver Roboterzelle) — separate Phase 8

### Hand-Tracking-Verlust
Wenn Meta SDK das `HandConfidence` < 0.6 meldet für > 200 ms im aktiven Teleop:
- Aktuelle Bewegung freeze (wie Handkante-Hold)
- Visuelles Warning-Overlay (rote Outline am Ghost)
- User muss Geste neu initiieren

## Implementation — Komponenten-Übersicht

Siehe `Assets/Scripts/Gestures/`:

- `GestureRouter.cs` — Dispatcher, hält Modus-State, feuert Events
- `PinchTeleopController.cs` — Pinch-Tap vs. Pinch-Drag, Direct/Proxy-Switch
- `SpatialPinchController.cs` — Hand-Ray + MRUK-Surface-Hit-Test, legt Waypoint/Go-To-Target am realen Welt-Punkt an den der Finger zeigt (Tisch, Werkstück, Boden); visuelles Reticle-Preview auf Oberfläche während Ziel-Phase
- `ThumbJogController.cs` — Richtungs-Jog via Daumen
- `WristRotationController.cs` — Flat-Hand → A4/A5/A6
- `HoldStopController.cs` — Handkante Soft-Stop
- `EmergencyStopHandler.cs` — Zwei-Hand-Faust Hard-Stop
- `CommitGestureHandler.cs` — OK-Ring Commit/Play
- `PathScaleController.cs` — 2-Hand-Spread Path-Scale
- `SwipeGestureController.cs` — Palm-Normal-basierte Swipe-Detection: wenn Flick-Velocity in Richtung Palm-Normal > 1.2 m/s, feuert `OnSwipe(Vector3 palmNormalWorld)` — 6 Richtungen unified
- `BeckonGestureController.cs` — Come-Here (Palm-Up → Finger-Curl Shape-Transition), feuert atomaren Step-User-zu-Event
- `HoldStopController.cs` bekommt Dual-Mode: still + Teleop/Jog = Soft-Stop; Flick-vorwärts + Command-Mode = Step-weg-Event
- `GhostRobotVisualizer.cs` — Ghost + Proxy + Trajectory Rendering
- `WaypointSequence.cs` — Pfad-Datenmodell, Serialisierung
- `EGMTeleopBridge.cs` — Interface zu `EGMClient.cs` (kommt in Phase 4)

## Open Design Decisions

- [ ] Pinch-Dauer-Threshold 200 ms ist Schätzung — mit User-Tests validieren
- [ ] Proxy-Handle Visual (Mini-Roboter vs. Kugel) — Quest-3-Test, welches weniger "distracting" ist
- [ ] Scale-Path-Range — beliebig skalierbar oder Clamp auf 0.1–3×?
- [ ] Haptic Feedback via Quest-Controller? User hat keinen Controller im Hand-Tracking-Modus, also nein — stattdessen Audio-Cue + visual flash bei Gesten-Commit
