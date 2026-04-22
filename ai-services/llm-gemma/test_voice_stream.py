"""L2 End-to-End: audio in -> streamed sentence-by-sentence Jarvis audio out."""
from __future__ import annotations

import io
import struct
import time
from pathlib import Path

import requests
import soundfile as sf

GEMMA = "http://localhost:8766"
AUDIO = "/mnt/c/git/MetaMove/ai-services/tts-qwen3/samples/jarvis_de_ref.wav"
OUT_DIR = Path("/mnt/c/git/MetaMove/ai-services/tts-qwen3/outputs")


def main() -> None:
    t0 = time.time()
    with open(AUDIO, "rb") as f:
        r = requests.post(
            f"{GEMMA}/chat/voice/stream",
            files={"audio": f},
            data={"language": "de", "variant": "helmet"},
            stream=True,
            timeout=180,
        )
    r.raise_for_status()
    raw = r.raw

    chunk_idx = 0
    first_chunk_ms = None
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
        if first_chunk_ms is None:
            first_chunk_ms = dt_ms
            print(f"[TTFA] {dt_ms:.0f}ms first chunk ({length} bytes)")
        else:
            print(f"[chunk {chunk_idx + 1}] {dt_ms:.0f}ms ({length} bytes)")
        chunk_idx += 1
        info = sf.info(io.BytesIO(wav_bytes))
        total_audio += info.duration
        out = OUT_DIR / f"voice_stream_{chunk_idx:02d}.wav"
        out.write_bytes(wav_bytes)

    dt_total = (time.time() - t0) * 1000
    print(f"\n[done] {chunk_idx} chunks, {dt_total:.0f}ms total, {total_audio:.2f}s audio output")
    if first_chunk_ms:
        print(f"       TTFA = {first_chunk_ms:.0f}ms (vs ~10500ms blocking end-to-end)")


if __name__ == "__main__":
    main()
