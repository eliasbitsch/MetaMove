# EGM Mock

Tiny Python UDP server that pretends to be a GoFa's EGM channel so you can
develop the Unity EGM sender/receiver without hardware.

Not a physics sim — it just echoes your commanded joints back as if the
controller reached them instantly. For real dynamics use RobotStudio Virtual
Controller.

## What it does

* Listens on `:6511/udp` for `EgmSensor` packets from Unity
* Parses joints from `planned.joints.joints` (if present)
* Replies with `EgmRobot` feedback carrying the same joints as `feedBack` + `measured`
* Reports rx Hz, RTT (mock-internal), parse errors, last joints every second

## Requirements

```bash
pip install protobuf
sudo apt install protobuf-compiler   # for protoc
```

(On Windows: install protoc from https://github.com/protocolbuffers/protobuf/releases
and put it on PATH. Or run this inside WSL/the ROS2 container, both have it.)

## Run

```bash
cd ai-services/egm-mock
python egm_mock.py                 # binds 0.0.0.0:6511
python egm_mock.py --port 6512 -v  # custom port, verbose
```

First start will auto-compile `egm.proto` → `egm_pb2.py`.

Point your Unity EGM sender at `<host>:6511` (localhost if same machine).

## Interpreting output

```
[mock] t=  12.3s  rx= 249.8 Hz  mock_rtt_avg=  42us p95=  71us  parse_err=0  last_joints=[  0.12, -45.00, ...]
```

* `rx Hz` — how often Unity is sending. Target is 250 Hz for EGM.
* `mock_rtt_avg/p95` — time the mock itself spent parsing and replying.
  Does NOT include network latency. To measure end-to-end RTT, add a
  timestamp round-trip in Unity (outgoing seqno → match incoming seqno).
* `parse_err` — non-zero means your Unity-side protobuf encoding is broken.
  Turn on `-v` to see the exception.
* `last_joints` — what Unity last asked for. Use this to sanity-check your
  coordinate conventions (degrees, joint ordering, signs).

## Proto source

`egm.proto` is copied from `ros2/abb_libegm/proto/egm.proto`. If that upstream
file changes, delete `egm_pb2.py` and re-run — `ensure_proto_compiled()` will
rebuild it.
