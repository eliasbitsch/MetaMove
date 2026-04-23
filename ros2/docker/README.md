# MetaMove ROS2 Jazzy — Docker

A Jazzy container bundling the vendored ABB ROS2 stack plus MoveIt 2, RViz and a
thin `metamove_bridge` node that wraps RWS for CLI access.

**Rolle im Gesamt-Stack:** Nicht im Motion-Hot-Path. Unity↔EGM bleibt direkt
(niedrige Latenz). Dieser Container ist für CLI-Debugging, Bag-Recording,
MoveIt-Planung (optional) und Orchestrierung per `ros2 service call`.

---

## Build

```bash
# from repo root
docker compose -f ros2/docker/docker-compose.yml build
```

Der Build COPY't alle `ros2/abb_*` Ordner rein und führt `colcon build` aus.
Wenn Pakete aus der Upstream-Source noch nicht Jazzy-ready sind, bleibt das
Image trotzdem nutzbar — du kannst inkrementell im Container mounten + rebuilden
(siehe *Dev Loop* unten).

## Run — interaktive Shell

```bash
docker compose -f ros2/docker/docker-compose.yml run --rm ros2
```

Du landest in `/opt/metamove_ws` mit gesourcter Jazzy- und Workspace-Umgebung.

## Run — Bridge headless

```bash
docker compose -f ros2/docker/docker-compose.yml run --rm bridge
```

## Run — RViz

Windows/WSLg:
```bash
docker compose -f ros2/docker/docker-compose.yml run --rm rviz
```
Wenn RViz schwarz bleibt, ist DISPLAY/X11 nicht durchgereicht — einfacher Workaround
unter Windows: **Container aus WSL2 starten** (nicht aus PowerShell), dort ist
WSLg transparent aktiv.

---

## Quick Debug-Rezepte

Innen Container:

```bash
# Topic-Liste
ros2 topic list

# Live Joints (sobald abb_driver läuft oder ein Mock Joint-States publisht)
ros2 topic echo /joint_states
ros2 topic hz   /joint_states

# MetaMove-Bridge State
ros2 topic echo /metamove/robot_state
ros2 topic echo /metamove/event_log

# Demo-Szenario starten
ros2 param set /metamove_bridge scenario chess
ros2 service call /metamove/run_demo std_srvs/srv/Trigger

# Not-Stop
ros2 service call /metamove/abort std_srvs/srv/Trigger

# Greifer
ros2 service call /metamove/grip_close std_srvs/srv/Trigger
ros2 service call /metamove/grip_open  std_srvs/srv/Trigger

# Motors
ros2 service call /metamove/motors_on  std_srvs/srv/Trigger
ros2 service call /metamove/motors_off std_srvs/srv/Trigger

# Full-Session mitschneiden
ros2 bag record -a -o bags/session_$(date +%Y%m%d_%H%M%S)

# Später abspielen
ros2 bag play bags/session_XXXX
```

Von außen (Host) per `docker exec`:

```bash
docker exec -it <container> bash -lc "source /opt/metamove_ws/install/setup.bash && ros2 topic list"
```

---

## Dev Loop (Code live editieren ohne Rebuild)

Die `docker-compose.yml` mountet alle `ros2/*` Ordner und `metamove_bridge/`
als Volumes. Workflow:

```bash
# Terminal 1 — interaktive Shell im Container
docker compose -f ros2/docker/docker-compose.yml run --rm ros2

# im Container
cd /opt/metamove_ws
colcon build --packages-select metamove_bridge --symlink-install
source install/setup.bash
ros2 launch metamove_bridge bridge.launch.py
```

Python-Änderungen am Bridge-Node greifen durch `--symlink-install` ohne Rebuild.
Nur bei C++-Paketen (librws/libegm) musst du `colcon build` neu anwerfen.

---

## Netzwerk & Connectivity

| Kanal | Port | Richtung | Hinweis |
|---|---|---|---|
| RWS HTTPS | 443/tcp | Container → GoFa | outbound, keine Docker-Konfig nötig |
| RWS WebSocket (Subscriptions) | 443/tcp | Container → GoFa | gleiche Verbindung |
| EGM UDP | 6511/udp | beide Richtungen | `ports: ["6511:6511/udp"]` in Compose |

**Alternative IP:** HANDOFF.md führt `192.168.125.1` als primäre IP, der User
hat zusätzlich `192.168.125.99` als Alternative genannt. Setz per ENV:

```bash
METAMOVE_RWS_IP=192.168.125.99 docker compose -f ros2/docker/docker-compose.yml run --rm bridge
```

**Windows-Specific:** Docker Desktop unterstützt kein `network_mode: host`.
Für niedrigste EGM-Latenz besser Container aus **WSL2** starten mit
`networkingMode=mirrored` in `.wslconfig`.

---

## Zusammenspiel mit Unity

```
Unity (Editor/Quest) ──► EGM UDP ──► GoFa OmniCore           ◄── RWS HTTPS ──► Unity/Jarvis
                         │                                     ▲
                         └───(nicht durch diesen Container)────┘

                                 ┌──► /joint_states, /tcp_pose (optional Mirror)
ROS2 Jazzy Container ────────────┼──► /metamove/robot_state, /demo_state, /event_log
(bridge_node + abb_ros2 stack)   │
                                 └──► Services: run_demo / abort / grip / motors
```

Dieser Container betrifft Unity **nicht direkt** — er läuft parallel, fragt RWS
selbst an, und gibt dir CLI-Zugriff auf denselben Roboter, mit dem Unity über
EGM spricht. Beide Seiten sind gleichberechtigte RWS-Clients.

Wenn später `/joint_states` aus EGM in ROS gespiegelt werden soll, erweitert
das der Bridge-Node — oder ihr nutzt den vollen `abb_ros2`-Treiber, der genau
das macht.

---

## ADR (falls ihr das später revidiert)

Siehe `docs/adr/ros2-bridge-only.md` (noch anzulegen) für den Kontext, warum
**nur eine schlanke Bridge** statt voller Motion-Stack-Integration gewählt wurde.
Trigger für vollen Wechsel zu ROS2-Motion: MoveIt-Planung, zweiter Roboter,
Sim-Parität mit Gazebo/Isaac, oder Kunden-Requirement.
