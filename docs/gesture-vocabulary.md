# GoHolo — Gesten-Vokabular für Quest 3 MR Teleop

**Zielplattform:** Meta Quest 3 + Meta XR Interaction SDK (Hand Tracking)
**Roboter:** ABB GoFa CRB 15000 5 kg / 950 mm, RobotWare 7.x mit EGM (UDP 6511 @ 250 Hz)
**Test-Backend:** RobotStudio Virtual Controller (Schritt 1.5) vor echtem Roboter

## Gesten-Mapping

| Geste | Action | Modus |
|-------|--------|-------|
| **Pinch kurz** (< 200 ms Tap) | Waypoint an aktueller Hand-Pose setzen | Waypoint-Modus |
| **Pinch lang + Drag** (≥ 200 ms) | Endeffektor ziehen (Position) | Live-EGM-Teleop |
| **Daumen-Point** (Hitchhiker) | Jog TCP in Daumen-Richtung, solange Geste aktiv | Jog-Modus |
| **Flat-Hand horizontal, drehen** | TCP-Orientierung A4/A5/A6 (Wrist-Achsen) | Orientation |
| **Handkante vor** (Stop-Hand) | Hold — Roboter friert ein, EGM hält Position | Soft-Stop |
| **Beidhändige Faust** | 🛑 E-Stop — Motors off via RWS | Hard-Stop |
| **OK-Ring** (Daumen + Zeigefinger zu Ring) | Commit / Aufgezeichneten Pfad abspielen | Commit |
| **2-Hand Pinch-Spread** (beide Hände im Pinch, Abstand ändern) | Scale Path — aufgezeichneten Pfad räumlich skalieren | Path-Edit |

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
- `ThumbJogController.cs` — Richtungs-Jog via Daumen
- `WristRotationController.cs` — Flat-Hand → A4/A5/A6
- `HoldStopController.cs` — Handkante Soft-Stop
- `EmergencyStopHandler.cs` — Zwei-Hand-Faust Hard-Stop
- `CommitGestureHandler.cs` — OK-Ring Commit/Play
- `PathScaleController.cs` — 2-Hand-Spread Path-Scale
- `GhostRobotVisualizer.cs` — Ghost + Proxy + Trajectory Rendering
- `WaypointSequence.cs` — Pfad-Datenmodell, Serialisierung
- `EGMTeleopBridge.cs` — Interface zu `EGMClient.cs` (kommt in Phase 4)

## Open Design Decisions

- [ ] Pinch-Dauer-Threshold 200 ms ist Schätzung — mit User-Tests validieren
- [ ] Proxy-Handle Visual (Mini-Roboter vs. Kugel) — Quest-3-Test, welches weniger "distracting" ist
- [ ] Scale-Path-Range — beliebig skalierbar oder Clamp auf 0.1–3×?
- [ ] Haptic Feedback via Quest-Controller? User hat keinen Controller im Hand-Tracking-Modus, also nein — stattdessen Audio-Cue + visual flash bei Gesten-Commit
