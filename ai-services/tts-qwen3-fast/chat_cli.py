"""Terminal-Chat: llm-gemma (SSE) -> faster-qwen3-tts (streaming) -> Lautsprecher.

Run inside ~/tts-qwen3-fast-venv (WSL2):
    source ~/tts-qwen3-fast-venv/bin/activate
    cd /mnt/c/git/MetaMove/ai-services/tts-qwen3-fast
    python chat_cli.py                      # Jarvis-Chat (Gemma + TTS)
    python chat_cli.py --echo               # ohne LLM, du tippst -> wird gesprochen
    python chat_cli.py --lang de            # Default-Sprache fuer TTS
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path
from queue import Queue

# Override audio device via env, e.g. TTS_AUDIO_DEVICE=pulse
ENV_DEVICE = os.environ.get("TTS_AUDIO_DEVICE", "")
USE_PAPLAY = shutil.which("paplay") is not None

import httpx
import numpy as np
import sounddevice as sd
import torch

from faster_qwen3_tts import FasterQwen3TTS

HERE = Path(__file__).resolve().parent
SAMPLES = HERE / "samples"

LANG_MAP = {
    "en": ("English", SAMPLES / "jarvis_ref.wav",     SAMPLES / "jarvis_ref.txt"),
    "de": ("German",  SAMPLES / "jarvis_de_ref.wav",  SAMPLES / "jarvis_de_ref.txt"),
    "ru": ("Russian", SAMPLES / "jarvis_ru_ref.wav",  SAMPLES / "jarvis_ru_ref.txt"),
}

LLM_URL = "http://127.0.0.1:8766/chat/stream"

SENT_SPLIT = re.compile(r"(?<=[\.\!\?…])\s+|(?<=[\.\!\?…])$")

try:
    from num2words import num2words as _n2w
except ImportError:
    _n2w = None

_NUM_RE = re.compile(r"\d+(?:[.,]\d+)?")
_LANG_FOR_N2W = {"de": "de", "en": "en", "ru": "ru"}


def declick(audio: np.ndarray) -> np.ndarray:
    """Remove DC offset + single-sample spikes that excite FX-chain distortion."""
    if len(audio) < 3:
        return audio
    audio = audio.astype(np.float32, copy=False)
    audio = audio - float(audio.mean())  # DC remove
    # Detect samples whose 1st-difference is >8x the median absolute diff (spike).
    diff = np.abs(np.diff(audio, prepend=audio[0]))
    nz = diff[diff > 0]
    if nz.size == 0:
        return audio
    thresh = 8.0 * float(np.median(nz))
    if thresh <= 0:
        return audio
    spikes = np.where(diff > thresh)[0]
    if spikes.size == 0:
        return audio
    # Replace each spike with linear interp of neighbors (avoid endpoints).
    audio = audio.copy()
    for i in spikes:
        if 0 < i < len(audio) - 1:
            audio[i] = 0.5 * (audio[i - 1] + audio[i + 1])
    return audio


def crossfade_concat(chunks: list[np.ndarray], sr: int, ms: float = 2.0) -> np.ndarray:
    """Concatenate audio chunks with micro-crossfade to hide codec-boundary clicks."""
    if not chunks:
        return np.zeros(0, dtype=np.float32)
    fade_n = max(1, int(sr * ms / 1000.0))
    out = chunks[0].astype(np.float32, copy=True)
    fade_in = np.linspace(0.0, 1.0, fade_n, dtype=np.float32)
    fade_out = 1.0 - fade_in
    for c in chunks[1:]:
        c = c.astype(np.float32, copy=False)
        if len(out) >= fade_n and len(c) >= fade_n:
            blended = out[-fade_n:] * fade_out + c[:fade_n] * fade_in
            out = np.concatenate([out[:-fade_n], blended, c[fade_n:]])
        else:
            out = np.concatenate([out, c])
    return out


def expand_numbers(text: str, lang: str) -> str:
    """Expand digits to words. Avoids faster-qwen3-tts tokenizer crash on numbers."""
    if _n2w is None:
        return text
    n2w_lang = _LANG_FOR_N2W.get(lang, "en")
    def repl(m: re.Match) -> str:
        s = m.group(0).replace(",", ".")
        try:
            return _n2w(float(s) if "." in s else int(s), lang=n2w_lang)
        except Exception:
            return m.group(0)
    return _NUM_RE.sub(repl, text)


def detect_lang(text: str, fallback: str) -> str:
    t = text.lower()
    if re.search(r"[Ѐ-ӿ]", t):
        return "ru"
    de_hits = sum(t.count(w) for w in (" der ", " die ", " das ", " ist ", " und ", " ich ", "ä", "ö", "ü", "ß"))
    en_hits = sum(t.count(w) for w in (" the ", " is ", " and ", " you ", " are ", " of "))
    if de_hits > en_hits and de_hits > 0:
        return "de"
    if en_hits > 0:
        return "en"
    return fallback


class TTSPlayer:
    def __init__(self, model: FasterQwen3TTS, default_lang: str = "en",
                 spk_embeddings: dict | None = None, voice_mode: str = "full",
                 temperature: float = 0.7, top_p: float = 0.9, seed: int = 42,
                 fx_board=None, speed: float = 1.0):
        self.model = model
        self.default_lang = default_lang
        self.spk_embeddings = spk_embeddings or {}
        self.voice_mode = voice_mode
        self.temperature = temperature
        self.top_p = top_p
        self.seed = seed
        self.fx_board = fx_board
        self.speed = max(0.5, min(1.2, speed))
        self.q: Queue = Queue()
        self.first_audio_at: float | None = None
        self.t_request: float | None = None
        threading.Thread(target=self._worker, daemon=True).start()

    def speak(self, text: str, lang: str | None = None):
        text = text.strip()
        if not text:
            return
        lang = lang or detect_lang(text, self.default_lang)
        self.q.put((text, lang))

    def mark_request(self):
        self.t_request = time.time()
        self.first_audio_at = None

    def _worker(self):
        while True:
            text, lang = self.q.get()
            if text is None:
                break
            try:
                self._render(text, lang)
            except Exception as e:
                print(f"\n[tts-error] {e}", file=sys.stderr)

    def _render(self, text: str, lang: str):
        text = expand_numbers(text, lang)
        # Padding very short utterances avoids autoregressive instability + codec
        # boundary clicks (single-chunk outputs sound rough on Qwen3-TTS).
        if len(text) < 12 and not text.endswith((".", "!", "?", "…")):
            text = text + "."
        lang_name, ref_wav, ref_txt = LANG_MAP[lang]
        clone_kwargs: dict
        if self.voice_mode == "xvec" and lang in self.spk_embeddings:
            clone_kwargs = {"voice_clone_prompt": self.spk_embeddings[lang]}
        else:
            clone_kwargs = {
                "ref_audio": str(ref_wav),
                "ref_text": ref_txt.read_text(encoding="utf-8").strip(),
            }
        # Deterministic-ish sampling: lower temp = stable speaker, fixed seed = reproducibly natural
        torch.manual_seed(self.seed)

        # Live streaming: each chunk through pedalboard (reset=False keeps reverb
        # state across chunks -> seamless) -> paplay. No concat, no crossfade.
        sr_out: int | None = None
        proc: subprocess.Popen | None = None
        try:
            # Short text -> collect-then-FX (avoids buzz from trailing silence
            # being amplified by compressor in live mode).
            short_mode = len(text) < 20 and self.fx_board is not None
            short_chunks: list[np.ndarray] = []
            for chunk, sr, _timing in self.model.generate_voice_clone_streaming(
                text=text,
                language=lang_name,
                chunk_size=12,
                temperature=self.temperature,
                top_p=self.top_p,
                **clone_kwargs,
            ):
                if self.first_audio_at is None and self.t_request is not None:
                    self.first_audio_at = time.time()
                    ttfa = self.first_audio_at - self.t_request
                    print(f"\n[ttfa] {ttfa*1000:.0f} ms ({lang})", flush=True)
                if sr_out is None:
                    sr_out = int(sr)
                audio = np.ascontiguousarray(chunk, dtype=np.float32).reshape(-1)
                if short_mode:
                    short_chunks.append(audio)
                    continue
                if self.fx_board is not None:
                    audio = self.fx_board(audio, sr_out, reset=False).astype(np.float32, copy=False)
                if USE_PAPLAY:
                    if proc is None:
                        proc = subprocess.Popen(
                            ["paplay", "--raw", f"--rate={int(sr_out * self.speed)}",
                             "--channels=1", "--format=float32le",
                             "--latency-msec=120"],
                            stdin=subprocess.PIPE, stderr=subprocess.DEVNULL,
                        )
                    try:
                        proc.stdin.write(audio.tobytes())  # type: ignore[union-attr]
                    except BrokenPipeError:
                        break
                else:
                    sd.play(audio, samplerate=int(sr_out * self.speed), blocking=True,
                            device=ENV_DEVICE or None)
            # Short-mode: render whole short utterance once -> trim trailing silence
            # -> fade-edges -> pedalboard once -> paplay. Avoids compressor pumping
            # codec quantization noise in trailing silence.
            if short_mode and short_chunks and sr_out is not None:
                full = np.concatenate(short_chunks).astype(np.float32, copy=False)
                # Trim trailing silence (last sample where |x| > 0.01)
                env = np.abs(full)
                idx = np.where(env > 0.01)[0]
                if idx.size:
                    end = min(len(full), int(idx[-1]) + int(sr_out * 0.05))
                    full = full[:end]
                # Edge fades: 25ms in (cushions plosives), 30ms out
                fin = int(sr_out * 0.025)
                fout = int(sr_out * 0.030)
                if len(full) > fin + fout:
                    full[:fin] *= np.linspace(0.0, 1.0, fin, dtype=np.float32)
                    full[-fout:] *= np.linspace(1.0, 0.0, fout, dtype=np.float32)
                # Pre-pad 50ms silence (paplay spinup) + tail-pad 400ms (reverb decay)
                pre = np.zeros(int(sr_out * 0.05), dtype=np.float32)
                tail = np.zeros(int(sr_out * 0.4), dtype=np.float32)
                buf = np.concatenate([pre, full, tail]).astype(np.float32)
                buf = self.fx_board(buf, sr_out, reset=False).astype(np.float32, copy=False)
                drain_n = int(sr_out * 0.3)
                drain_in = np.zeros(drain_n, dtype=np.float32)
                drain_out = self.fx_board(drain_in, sr_out, reset=False).astype(np.float32, copy=False)
                drain_out *= np.linspace(1.0, 0.0, drain_n, dtype=np.float32)
                buf = np.concatenate([buf, drain_out])
                if USE_PAPLAY:
                    proc = subprocess.Popen(
                        ["paplay", "--raw", f"--rate={int(sr_out * self.speed)}",
                         "--channels=1", "--format=float32le", "--latency-msec=120"],
                        stdin=subprocess.PIPE, stderr=subprocess.DEVNULL,
                    )
                    proc.stdin.write(buf.tobytes())  # type: ignore[union-attr]
                    proc.stdin.close()  # type: ignore[union-attr]
                    proc.wait()
                else:
                    sd.play(buf, samplerate=int(sr_out * self.speed), blocking=True, device=ENV_DEVICE or None)
            # Long-mode drain: reverb tail with silence so it decays before paplay closes.
            elif self.fx_board is not None and sr_out is not None and proc is not None:
                tail_in = np.zeros(int(sr_out * 0.4), dtype=np.float32)
                tail_out = self.fx_board(tail_in, sr_out, reset=False).astype(np.float32, copy=False)
                try:
                    proc.stdin.write(tail_out.tobytes())  # type: ignore[union-attr]
                except BrokenPipeError:
                    pass
            if proc is not None:
                proc.stdin.close()  # type: ignore[union-attr]
                proc.wait()
        finally:
            if proc is not None and proc.poll() is None:
                proc.terminate()


def stream_tokens(client: httpx.Client, user_text: str, session_id: str):
    """Yield token-strings from llm-gemma /chat/stream."""
    payload = {
        "session_id": session_id,
        "messages": [{"role": "user", "content": user_text}],
        "max_new_tokens": 256,
        "temperature": 0.7,
        "top_p": 0.9,
        "repetition_penalty": 1.1,
    }
    with client.stream("POST", LLM_URL, json=payload, timeout=120.0) as r:
        r.raise_for_status()
        for line in r.iter_lines():
            if not line or not line.startswith("data:"):
                continue
            data = json.loads(line[5:].strip())
            if "token" in data:
                yield data["token"]
            if data.get("done"):
                return


def chat_loop(player: TTSPlayer, default_lang: str):
    session = f"cli-{int(time.time())}"
    print(f"[chat] session={session}  llm={LLM_URL}  default-lang={default_lang}")
    print("[chat] Tippe leer + Enter zum Beenden.\n")
    with httpx.Client() as client:
        while True:
            try:
                user = input("you> ").strip()
            except (EOFError, KeyboardInterrupt):
                print()
                return
            if not user:
                return
            print("jarvis> ", end="", flush=True)
            buf = ""
            player.mark_request()
            for tok in stream_tokens(client, user, session):
                print(tok, end="", flush=True)
                buf += tok
                # Sentence-flush
                while True:
                    m = SENT_SPLIT.search(buf)
                    if not m:
                        break
                    sentence, buf = buf[: m.end()].strip(), buf[m.end():]
                    if sentence:
                        player.speak(sentence)
            if buf.strip():
                player.speak(buf.strip())
            print()


def echo_loop(player: TTSPlayer, default_lang: str):
    print(f"[echo] default-lang={default_lang}.  Leer + Enter beendet.\n")
    while True:
        try:
            text = input("say> ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return
        if not text:
            return
        # Whole input as one TTS call -> one paplay stream -> no sentence-boundary
        # clicks. Speaker conditioning holds since we use clean ref + post-FX
        # (no helmet-clone-fade-out problem).
        player.mark_request()
        player.speak(text)


def main():
    global LANG_MAP
    ap = argparse.ArgumentParser()
    ap.add_argument("--echo", action="store_true", help="Skip LLM, direct text->speech")
    ap.add_argument("--lang", default="en", choices=list(LANG_MAP), help="Fallback language")
    ap.add_argument("--model", default="Qwen/Qwen3-TTS-12Hz-1.7B-Base")
    ap.add_argument(
        "--voice-mode", choices=["xvec", "full"], default="full",
        help="xvec=stable+flat (cached embedding), full=expressive (re-encoded ref each call)",
    )
    ap.add_argument("--temperature", type=float, default=0.7,
                    help="Lower=more stable speaker, less drift (lib default 0.9 driftet)")
    ap.add_argument("--top-p", type=float, default=0.9)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--speed", type=float, default=1.0,
                    help="Playback speed (0.85-1.0). <1 = slower + slight pitch drop")
    ap.add_argument("--fx", choices=["none", "helmet", "ceiling"], default="none",
                    help="Apply Pedalboard post-FX (Iron-Man helmet / Villa-ceiling speaker)")
    args = ap.parse_args()

    fx_board = None
    if args.fx != "none":
        from jarvis_fx import CHAINS  # type: ignore
        fx_board = CHAINS[args.fx]
        print(f"[fx] Pedalboard chain = {args.fx}")
    print(f"[load] {args.model} ...", flush=True)
    t0 = time.time()
    model = FasterQwen3TTS.from_pretrained(args.model)
    print(f"[load] ready in {time.time()-t0:.1f}s")

    # Audio devices
    try:
        devs = sd.query_devices()
        out_devs = [(i, d["name"]) for i, d in enumerate(devs) if d["max_output_channels"] > 0]
        print(f"[audio] output devices: {out_devs[:3]}{' ...' if len(out_devs) > 3 else ''}")
    except Exception as e:
        print(f"[audio] WARN: {e}. Install: sudo apt-get install -y pulseaudio-utils libasound2-plugins")

    # Speaker setup. Two modes:
    #   xvec: x-vector embedding precomputed once (10 tokens). Stable, no accent-bleed,
    #         fastest TTFA. Etwas flacher in der Prosodie.
    #   full: ref_audio + ref_text each call (80+ tokens). Expressiv, leichtes Accent-Bleed.
    #         Drift-Risiko mitigated by low temperature + fixed seed.
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
        print(f"[speaker] full mode — temp={args.temperature} seed={args.seed} (re-encode per call)")

    # Warmup — capture CUDA graphs before the user's first input
    print("[warmup] running CUDA-graph capture (one-time, ~10-20s) ...", flush=True)
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
    print(f"[warmup] done in {time.time()-t0:.1f}s. Subsequent TTFA reflects steady-state.\n")

    player = TTSPlayer(
        model, default_lang=args.lang, spk_embeddings=spk_embeddings,
        voice_mode=args.voice_mode, temperature=args.temperature,
        top_p=args.top_p, seed=args.seed, fx_board=fx_board, speed=args.speed,
    )
    if args.echo:
        echo_loop(player, args.lang)
    else:
        chat_loop(player, args.lang)


if __name__ == "__main__":
    main()
