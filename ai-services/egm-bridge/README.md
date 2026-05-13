# EGM Bridge — Dockerized (WSL native Docker)

The bridge translates between ABB EGM (UDP/protobuf) and ROS (joint_states +
servo commands via rosbridge). It needs to receive EGM packets from the GoFa
controller (real or RobotStudio VC).

## Why macvlan on WSL2

WSL2's `networkingMode=mirrored` shares the Windows host IP but has a known
bug where UDP packets arriving at the Windows NIC are not delivered into the
WSL/container namespace reliably. We hit this on 2026-05-12 — Windows-side
Python received 3001 EGM packets/s, identical container-side listener got 0.

The fix is **macvlan**: the container gets its own MAC + IPv4 on the same
physical LAN, behaving like a separate Linux PC. Packets flow through the
kernel macvlan driver, not through the buggy mirrored translation.

## Prerequisites

- WSL2 with Ubuntu (any distro that runs Docker)
- Docker installed inside WSL (`apt install docker.io`) — **not Docker Desktop**
- `.wslconfig` in `%USERPROFILE%`:
  ```
  [wsl2]
  networkingMode=mirrored
  ```
- WSL `eth0` bridged to the lab network (default with mirrored mode)
- Bridge container will claim IP `192.168.125.150` — make sure no other
  device on the lab LAN uses it. Adjust in `docker-compose.yml` if needed.

## Build & run

From inside WSL:

```bash
cd /mnt/c/git/MetaMove/ai-services/egm-bridge
docker compose up --build
```

By default this starts `egm_bridge_identity.py` — empty-echo back to the
controller, robot stays still, EGM session stays alive.

## Servo mode (ROS bridge integration)

Once the ROS launch is running and rosbridge_websocket is reachable, swap to:

```bash
docker compose run --rm bridge python -u egm_bridge_servo.py \
    --rosbridge-host 192.168.125.99
```

## Robot config

The GoFa controller's UDPUC device must point to the bridge IP. With macvlan
at `192.168.125.150`:

```cfg
UDPUC_HOST:
    -Name "ROB_1" -Type "UDPUC" -RemoteAddress "192.168.125.150" \
    -RemotePortNumber 6511 -LocalPortNumber 6511
```

Loaded via RWS into the controller and warm-restarted.

## Sanity check

After `docker compose up`, verify with:

```bash
docker exec metamove-egm-bridge ip a show eth0
# Should print inet 192.168.125.150/24

docker exec metamove-egm-bridge ss -uln | grep 6511
# Should show 0.0.0.0:6511 bound by python
```
