"""Client-Side Test fuer /tts/stream: misst TTFA und inter-chunk Latenzen."""
from __future__ import annotations

import io
import struct
import sys
import time
from pathlib import Path

import requests
import soundfile as sf

TTS = "http://localhost:8765"
TEXT = (
    "Guten Morgen. Die Wetterinformationen sind für mich nicht relevant. "
    "Wie kann ich Ihnen bei der Arbeit am Roboter helfen?"
)
OUT_DIR = Path("/mnt/c/git/MetaMove/ai-services/tts-qwen3/outputs")
OUT_DIR.mkdir(exist_ok=True)


def main() -> None:
    t0 = time.time()
    r = requests.post(
        f"{TTS}/tts/stream",
        json={"text": TEXT, "language": "de", "variant": "helmet"},
        stream=True,
        timeout=120,
    )
    r.raise_for_status()

    chunk_idx = 0
    raw = r.raw  # urllib3 response, allows read()
    first_chunk_time = None
    total_audio = 0.0

    while True:
        header = raw.read(4)
        if not header:
            break
        (length,) = struct.unpack(">I", header)
        if length == 0:
            break
        wav_bytes = b""
        while len(wav_bytes) < length:
            wav_bytes += raw.read(length - len(wav_bytes))
        dt_ms = (time.time() - t0) * 1000
        if first_chunk_time is None:
            first_chunk_time = dt_ms
            print(f"[TTFA] {dt_ms:.0f}ms first chunk ({length} bytes)")
        else:
            print(f"[chunk {chunk_idx + 1}] {dt_ms:.0f}ms ({length} bytes)")
        chunk_idx += 1
        # Save + count duration
        info = sf.info(io.BytesIO(wav_bytes))
        total_audio += info.duration
        out = OUT_DIR / f"stream_chunk_{chunk_idx:02d}.wav"
        out.write_bytes(wav_bytes)

    dt_total = (time.time() - t0) * 1000
    print(f"\n[done] {chunk_idx} chunks, {dt_total:.0f}ms total, {total_audio:.2f}s audio")
    print(f"       RTF = {dt_total / 1000 / total_audio:.2f}x realtime")
    if first_chunk_time and total_audio:
        print(f"       TTFA = {first_chunk_time:.0f}ms for {total_audio:.2f}s total audio output")


if __name__ == "__main__":
    main()
