# TCP-Bridge Latency Probe

Measures RAPID ↔ Windows-Host TCP roundtrip latency for the planned VC-Sim bridge (see memory `project_tcp_bridge_workaround`).

## Run

```powershell
# 1. Start echo server (in this folder)
python tcp_echo_latency.py
```

```
# 2. In RobotStudio:
#    - Load MetaMoveCoreTCPLatencyTest.mod into T_ROB1 (RAPID tab → Modul laden)
#    - PP to MetaMoveCoreTCPLatencyTest.main
#    - Motors On (not strictly needed — no MoveAbsJ in this probe)
#    - Play
```

## What it does

200 frames, each:

1. RAPID reads `ClkRead(\HighRes)` → ms timestamp
2. RAPID sends timestamp as ASCII string + newline via `SocketSend`
3. Python server reads, echoes back verbatim
4. RAPID receives via `SocketReceive`, reads clock again, computes diff

At end, RAPID `TPWrite`s avg / min / max ms to the Operator Messages window.
Python prints its own server-side handling time (should be sub-ms).

## What we learn

- `avg`: average RAPID-perspective roundtrip
- `min`: best-case (likely network leg only)
- `max`: jitter / GC-pause / RAPID-scheduler hiccups

## Thresholds

- avg < 20 ms → bridge viable for sim-teleop
- avg 20-50 ms → demo OK, not realtime
- avg > 50 ms → unusable, drop the workaround

## If 41824 / SocketConnect fails

Means the VC's TCP stack also blocked by sandbox. Use a non-loopback IP
(192.168.176.1 = Hyper-V Default Switch, or Windows WLAN IP). RWS/HTTP works,
so plain TCP outbound *should* work — but if not, that's another data point.
