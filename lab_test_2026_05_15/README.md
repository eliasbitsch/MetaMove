# Lab Test 2026-05-15 — Python RWS vs Unity EGM

Compare two end-to-end paths to the real GoFa:

**Path A — Python RWS Bridge** (new, untested):
```
Twist publisher → MoveIt-Servo → metamove_bridge (Python rclpy) → RWS HTTPS → GoFa RAPID MoveAbsJ
```

**Path B — Unity EGM Bridge** (validated 2026-05-08):
```
Twist publisher → MoveIt-Servo → ROS-TCP-Connector → Unity ServoCommandSubscriber → EgmClient SendJoints → EGM UDP → GoFa MetaMoveCore EGMRunJoint
```

## Prerequisites at the lab

- Real GoFa powered + on Service Port LAN (default IP 192.168.125.1)
- Windows PC connected to same LAN, has reachable route to GoFa
- WSL2 running, Docker Desktop OR docker-in-WSL2 up
- Container `metamove-ros2-...` running

## Test sequence

Do A first (new code, more risk). If A fails, B is the proven fallback.

### Path A — Python RWS Bridge

1. **Load RAPID module** `c:/git/MetaMove/robotstudio/rapid/MetaMoveCorePers.mod` on the GoFa:
   - RobotStudio connected to real controller (or via FlexPendant USB-stick load)
   - Place in `T_ROB1`, PP → `MetaMoveCorePers/main`
   - Motors On → Play
   - Status: Running, no errors in event log
2. **Rebuild Docker container** with new bridge code:
   ```bash
   wsl docker exec metamove-ros2-... bash -lc "source /opt/ros/jazzy/setup.bash && cd /opt/metamove_ws && colcon build --packages-select metamove_bridge && source install/setup.bash"
   ```
3. **Launch ROS Servo + Bridge** (two terminals):
   ```bash
   # Term 1: Servo + MoveIt
   wsl docker exec -it metamove-ros2-... bash -lc "source /opt/metamove_ws/install/setup.bash && ros2 launch abb_crb15000_moveit metamove_servo.launch.py"
   
   # Term 2: RWS Bridge in sim-servo mode (BUT pointing at REAL ROBOT IP)
   wsl docker exec -it metamove-ros2-... bash -lc "source /opt/metamove_ws/install/setup.bash && ros2 launch metamove_bridge sim_servo.launch.py rws_ip:=192.168.125.1 rws_port:=443"
   ```
4. **Trigger Servo** (term 3):
   ```bash
   wsl docker exec -it metamove-ros2-... bash -lc "source /opt/metamove_ws/install/setup.bash && ros2 service call /servo_node/start_servo std_srvs/srv/Trigger"
   ```
5. **Run latency probe**:
   ```bash
   wsl docker cp c:/git/MetaMove/lab_test_2026_05_15/latency_probe.py metamove-ros2-...:/tmp/
   wsl docker exec metamove-ros2-... bash -lc "source /opt/metamove_ws/install/setup.bash && python3 /tmp/latency_probe.py --path rws"
   ```
6. **Observe**:
   - Robot moves following twist commands
   - `/metamove/motion_rate` shows hz_in ≈ hz_out
   - Latency probe prints e2e mean/p95
   - Stop with Ctrl-C

### Path B — Unity EGM Bridge

1. **Load RAPID module** `MetaMoveCore.mod` (EGM version, from `unity-quest/...` or backup) on the GoFa:
   - PP → `MetaMoveCore/main`
   - Motors On → Play
   - Pendant shows "EGM Connected - calling EGMRunJoint"
2. **Start Unity Editor**, open MetaMove project, load `Scene_Robot` scene
3. **Verify Unity ROSConnection IP** in `RosBridgeBootstrap` — should target the Docker container TCP endpoint at `host.docker.internal:10000` (or `127.0.0.1:10000` if port-forwarded)
4. **Launch ROS Servo** (term 1, same as path A):
   ```bash
   wsl docker exec -it metamove-ros2-... bash -lc "source /opt/metamove_ws/install/setup.bash && ros2 launch abb_crb15000_moveit metamove_servo.launch.py"
   ```
5. **Press Play in Unity Editor** — EgmClient binds UDP 6511, ServoCommandSubscriber subscribes /servo_node/commands, JointStatePublisher publishes /joint_states
6. **Start Servo** (term 2):
   ```bash
   wsl docker exec -it metamove-ros2-... bash -lc "source /opt/metamove_ws/install/setup.bash && ros2 service call /servo_node/start_servo std_srvs/srv/Trigger"
   ```
7. **Run latency probe**:
   ```bash
   wsl docker exec metamove-ros2-... bash -lc "python3 /tmp/latency_probe.py --path egm"
   ```

## Measurement — what to record

For each path, capture and write to `results.md` in this folder:

| Metric | Path A (RWS) | Path B (EGM) |
|---|---|---|
| e2e latency mean (ms) |  |  |
| e2e latency p95 (ms) |  |  |
| Commands/s sent to robot |  |  |
| Commands/s succeeded |  |  |
| Motion smoothness (1-5 subjective) |  |  |
| Setup time (min) |  |  |
| Notable issues |  |  |

## Safety notes

- **Speed override at 30% first run** (`/metamove/motors_off` then `speedratio` via FlexPendant)
- Stand outside robot envelope during twist test
- Have e-stop within reach
- First 5 sec: only +Z twist 2cm/s — small motion
- If robot moves unexpectedly: Motors Off via FlexPendant immediately

## Memory updates after test

After running both paths and recording results, update:
- `project_egm_joint_via_sm_working` (Path B confirmation)
- New memory `project_rws_servo_validated_2026_05_15` if Path A works
- `project_architecture_dual_path` with measured latency numbers
