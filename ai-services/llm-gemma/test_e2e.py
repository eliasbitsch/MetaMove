"""End-to-end smoke test: Audio -> Gemma -> Text -> TTS -> WAV."""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

import requests

GEMMA = "http://localhost:8766"
TTS = "http://localhost:8765"
AUDIO = "/mnt/c/git/MetaMove/ai-services/tts-qwen3/samples/jarvis_de_ref.wav"
OUT = "/mnt/c/git/MetaMove/ai-services/tts-qwen3/outputs/e2e_test.wav"


def main() -> None:
    t0 = time.time()

    # 1) Audio -> Gemma -> Text
    with open(AUDIO, "rb") as f:
        r = requests.post(f"{GEMMA}/chat/voice", files={"audio": f}, timeout=60)
    r.raise_for_status()
    gemma = r.json()
    t_gemma = time.time() - t0
    print(f"[1] Gemma {t_gemma:.2f}s ({gemma['audio_duration_s']}s audio -> text)")
    print(f"    -> {gemma['text']!r}")

    # 2) Text -> TTS -> WAV
    t1 = time.time()
    r = requests.post(
        f"{TTS}/tts",
        json={"text": gemma["text"], "language": "de", "variant": "helmet"},
        timeout=60,
    )
    r.raise_for_status()
    t_tts = time.time() - t1
    with open(OUT, "wb") as f:
        f.write(r.content)
    print(f"[2] TTS   {t_tts:.2f}s ({len(r.content)} bytes -> {OUT})")

    t_total = time.time() - t0
    print(f"\n[TOTAL] {t_total:.2f}s audio-in -> jarvis-audio-out")


if __name__ == "__main__":
    main()
