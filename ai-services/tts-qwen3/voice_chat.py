"""Voice-Conversation: mic -> Gemma (audio understanding + reply) -> faster-qwen3-tts.

Push-to-talk: Enter starts recording, Enter stops.

Run inside ~/tts-qwen3-fast-venv (WSL2):
    source ~/tts-qwen3-fast-venv/bin/activate
    cd /mnt/c/git/MetaMove/ai-services/tts-qwen3
    TTS_AUDIO_DEVICE=pulse python voice_chat.py --lang de --fx helmet
"""
from __future__ import annotations

import argparse
import io
import os
import sys
import threading
import time
import wave
from pathlib import Path

import httpx
import numpy as np
import sounddevice as sd
import torch

from faster_qwen3_tts import FasterQwen3TTS

# Reuse from chat_cli (same dir)
from chat_cli import LANG_MAP, TTSPlayer

GEMMA_VOICE_URL = "http://127.0.0.1:8766/chat/voice"
MIC_SR = 16000  # Gemma audio processor expects 16 kHz mono


def record_until_enter(samplerate: int = MIC_SR) -> np.ndarray:
    """Record from default mic until user presses Enter again. Returns mono float32."""
    print("[rec] Aufnahme laeuft — Enter zum Beenden ...", flush=True)
    buf: list[np.ndarray] = []

    def cb(indata, frames, time_info, status):
        if status:
            print(f"[rec] WARN: {status}", file=sys.stderr)
        buf.append(indata[:, 0].copy())

    with sd.InputStream(samplerate=samplerate, channels=1, dtype="float32", callback=cb):
        try:
            input()
        except (EOFError, KeyboardInterrupt):
            pass
    if not buf:
        return np.zeros(0, dtype=np.float32)
    audio = np.concatenate(buf).astype(np.float32)
    print(f"[rec] {len(audio)/samplerate:.2f}s aufgenommen.", flush=True)
    return audio


def to_wav_bytes(audio: np.ndarray, sr: int = MIC_SR) -> bytes:
    """Encode mono float32 audio as WAV (PCM16) bytes."""
    pcm = np.clip(audio, -1.0, 1.0)
    pcm = (pcm * 32767.0).astype(np.int16)
    bio = io.BytesIO()
    with wave.open(bio, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())
    return bio.getvalue()


def gemma_voice(client: httpx.Client, audio: np.ndarray, session_id: str,
                lang_hint: str | None = None, max_new_tokens: int = 200,
                temperature: float = 0.6) -> tuple[str, float]:
    """POST audio to gemma /chat/voice, return (text, latency_seconds)."""
    wav_bytes = to_wav_bytes(audio)
    files = {"audio": ("user.wav", wav_bytes, "audio/wav")}
    data: dict[str, str] = {
        "session_id": session_id,
        "max_new_tokens": str(max_new_tokens),
        "temperature": str(temperature),
    }
    # Optional language hint piggybacked as text instruction
    if lang_hint:
        data["text"] = f"(reply in {lang_hint})"
    t0 = time.time()
    r = client.post(GEMMA_VOICE_URL, data=data, files=files, timeout=120.0)
    r.raise_for_status()
    payload = r.json()
    return payload["text"], time.time() - t0


def setup_tts(args) -> TTSPlayer:
    """Mirror chat_cli.main() setup minus the chat/echo loop."""
    fx_board = None
    if args.fx != "none":
        from jarvis_fx import CHAINS  # type: ignore
        fx_board = CHAINS[args.fx]
        print(f"[fx] Pedalboard chain = {args.fx}")

    print(f"[load] {args.model} ...", flush=True)
    t0 = time.time()
    model = FasterQwen3TTS.from_pretrained(args.model)
    print(f"[load] ready in {time.time()-t0:.1f}s")

    spk_embeddings: dict[str, dict] = {}
    if args.voice_mode == "xvec":
        print("[speaker] precomputing x-vector embeddings ...", flush=True)
        seen_wavs: dict[str, dict] = {}
        for lang_code, (_lname, ref_wav, _ref_txt) in LANG_MAP.items():
            wav_key = str(ref_wav.resolve())
            if wav_key in seen_wavs:
                spk_embeddings[lang_code] = seen_wavs[wav_key]
                continue
            prompt_items = model.model.create_voice_clone_prompt(
                ref_audio=str(ref_wav), ref_text="", x_vector_only_mode=True,
            )
            prompt = {"ref_spk_embedding": [prompt_items[0].ref_spk_embedding]}
            spk_embeddings[lang_code] = prompt
            seen_wavs[wav_key] = prompt
        print(f"[speaker] {len(seen_wavs)} embedding(s) ready.")
    else:
        print(f"[speaker] full mode — temp={args.temperature}")

    # Warmup
    print("[warmup] CUDA-graph capture ...", flush=True)
    t0 = time.time()
    lang_name, ref_wav, ref_txt = LANG_MAP[args.lang]
    warmup_kwargs: dict
    if args.voice_mode == "xvec":
        warmup_kwargs = {"voice_clone_prompt": spk_embeddings[args.lang]}
    else:
        warmup_kwargs = {
            "ref_audio": str(ref_wav),
            "ref_text": ref_txt.read_text(encoding="utf-8").strip(),
        }
    torch.manual_seed(args.seed)
    for _ in model.generate_voice_clone_streaming(
        text="Initialisierung." if args.lang == "de" else "Initializing.",
        language=lang_name, chunk_size=8,
        temperature=args.temperature, top_p=args.top_p,
        **warmup_kwargs,
    ):
        pass
    print(f"[warmup] done in {time.time()-t0:.1f}s\n")

    return TTSPlayer(
        model, default_lang=args.lang, spk_embeddings=spk_embeddings,
        voice_mode=args.voice_mode, temperature=args.temperature,
        top_p=args.top_p, seed=args.seed, fx_board=fx_board, speed=args.speed,
    )


def wait_player_drain(player: TTSPlayer, poll_s: float = 0.05) -> None:
    """Block until player queue is empty AND last render finished."""
    # Queue empty
    while not player.q.empty():
        time.sleep(poll_s)
    # Worker thread might still be rendering one item; small grace + poll size
    time.sleep(0.1)


def voice_loop(player: TTSPlayer, lang: str, session: str) -> None:
    print(f"[voice] session={session}  lang={lang}  gemma={GEMMA_VOICE_URL}")
    print("[voice] Enter -> aufnehmen, Enter -> stoppen + Antwort. Ctrl-C zum Beenden.\n")
    with httpx.Client() as client:
        while True:
            try:
                input("you (press Enter to record)> ")
            except (EOFError, KeyboardInterrupt):
                print()
                return
            audio = record_until_enter()
            if len(audio) < MIC_SR * 0.3:  # < 300ms
                print("[voice] zu kurz, ignoriere.\n")
                continue
            try:
                text, dt = gemma_voice(client, audio, session_id=session, lang_hint=lang)
            except Exception as e:
                print(f"[gemma-error] {e}\n", file=sys.stderr)
                continue
            print(f"jarvis ({dt:.1f}s)> {text}\n", flush=True)
            player.mark_request()
            player.speak(text)
            wait_player_drain(player)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lang", default="de", choices=list(LANG_MAP),
                    help="TTS-Stimme (Gemma erkennt User-Sprache automatisch)")
    ap.add_argument("--model", default="Qwen/Qwen3-TTS-12Hz-1.7B-Base")
    ap.add_argument("--voice-mode", choices=["xvec", "full"], default="full")
    ap.add_argument("--temperature", type=float, default=0.85)
    ap.add_argument("--top-p", type=float, default=0.9)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--speed", type=float, default=1.0)
    ap.add_argument("--fx", choices=["none", "helmet", "ceiling"], default="helmet")
    args = ap.parse_args()

    player = setup_tts(args)
    voice_loop(player, args.lang, session=f"voice-{int(time.time())}")


if __name__ == "__main__":
    main()
