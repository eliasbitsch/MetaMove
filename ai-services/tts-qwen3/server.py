"""
FastAPI TTS-Server: Jarvis-Stimme in EN/DE (Base-Clone) + RU/Rest (VoiceDesign).

Endpoints:
  POST /tts       -> WAV-Response
  GET  /health    -> Status + geladene Modelle
  GET  /voices    -> verfuegbare Sprachen und Varianten

Start:
  uvicorn server:app --host 0.0.0.0 --port 8765
"""
from __future__ import annotations

import io
import logging
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Literal

import numpy as np
import soundfile as sf
import torch
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import Response
from pedalboard import Pedalboard
from pydantic import BaseModel, Field

from apply_jarvis_fx import CHAINS as FX_CHAINS
from qwen_tts import Qwen3TTSModel

log = logging.getLogger("tts-qwen3")
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

HERE = Path(__file__).parent
SAMPLES = HERE / "samples"

# Jarvis-Instruct-Prompt fuer VoiceDesign — shared mit test_voice_design.py
JARVIS_INSTRUCT = (
    "gender: Male. "
    "pitch: Mid-low baritone, very stable, minimal inflection. "
    "speed: Slow to moderate, deliberate cadence with precise pauses between clauses. "
    "volume: Calm conversational level, never raised. "
    "age: Late 40s. "
    "clarity: Extremely articulate and precise pronunciation. "
    "fluency: Highly fluent, no hesitation, no filler words. "
    "accent: British English, Received Pronunciation. "
    "texture: Warm but restrained, smooth, slightly nasal resonance, subtle electronic undertone. "
    "emotion: Detached, calm, minimal affect, with an undertone of dry amusement. "
    "tone: Formal, dignified, understated, almost butler-like. "
    "personality: Intellectually superior but polite, deadpan, subtly sarcastic, unflappable AI assistant."
)

# lang-code -> (qwen_language_name, mode: "clone" | "design", optional ref_audio, ref_text)
LANG_ROUTES: dict[str, dict] = {
    "en": {"qwen": "English", "mode": "clone",
           "ref_audio": SAMPLES / "jarvis_ref.wav",
           "ref_text_file": SAMPLES / "jarvis_ref.txt"},
    "de": {"qwen": "German",  "mode": "clone",
           "ref_audio": SAMPLES / "jarvis_de_ref.wav",
           "ref_text_file": SAMPLES / "jarvis_de_ref.txt"},
    "ru": {"qwen": "Russian", "mode": "design"},
    "ja": {"qwen": "Japanese", "mode": "design"},
    "ko": {"qwen": "Korean", "mode": "design"},
    "fr": {"qwen": "French", "mode": "design"},
    "es": {"qwen": "Spanish", "mode": "design"},
    "it": {"qwen": "Italian", "mode": "design"},
    "pt": {"qwen": "Portuguese", "mode": "design"},
}

Variant = Literal["dry", "helmet", "ceiling"]
LangCode = Literal["en", "de", "ru", "ja", "ko", "fr", "es", "it", "pt"]


class TTSRequest(BaseModel):
    text: str = Field(..., min_length=1, max_length=2000)
    language: LangCode = "en"
    variant: Variant = "helmet"


class Models:
    """Singleton-Container fuer geladene Qwen-Modelle."""
    base: Qwen3TTSModel | None = None
    design: Qwen3TTSModel | None = None
    # Cache: resolved ref-text pro lang
    ref_texts: dict[str, str] = {}


def _load_base() -> Qwen3TTSModel:
    log.info("Loading Qwen3-TTS-Base (1.7B, bf16)")
    return Qwen3TTSModel.from_pretrained(
        "Qwen/Qwen3-TTS-12Hz-1.7B-Base",
        device_map="cuda:0",
        dtype=torch.bfloat16,
        attn_implementation="eager",
    )


def _load_design() -> Qwen3TTSModel:
    log.info("Loading Qwen3-TTS-VoiceDesign (1.7B, bf16)")
    return Qwen3TTSModel.from_pretrained(
        "Qwen/Qwen3-TTS-12Hz-1.7B-VoiceDesign",
        device_map="cuda:0",
        dtype=torch.bfloat16,
        attn_implementation="eager",
    )


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Lade Base-Modell beim Start, VoiceDesign lazy on-demand."""
    Models.base = _load_base()
    for lang, cfg in LANG_ROUTES.items():
        if cfg["mode"] == "clone":
            ref_text_file = cfg["ref_text_file"]
            if ref_text_file.exists():
                Models.ref_texts[lang] = ref_text_file.read_text(encoding="utf-8").strip()
    log.info("Base model ready, %d clone refs cached", len(Models.ref_texts))
    yield
    log.info("Shutdown")


app = FastAPI(title="Jarvis TTS", version="1.0", lifespan=lifespan)
app.add_middleware(
    CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"],
)


def _apply_fx(audio: np.ndarray, sr: int, variant: Variant) -> np.ndarray:
    if variant == "dry":
        return audio
    board: Pedalboard = FX_CHAINS[variant]
    out = board(audio.astype(np.float32), sr)
    peak = np.max(np.abs(out))
    if peak > 0.98:
        out = out * (0.98 / peak)
    return out


def _generate(req: TTSRequest) -> tuple[np.ndarray, int]:
    cfg = LANG_ROUTES.get(req.language)
    if not cfg:
        raise HTTPException(400, f"Unsupported language '{req.language}'")

    if cfg["mode"] == "clone":
        ref_audio = cfg["ref_audio"]
        if not ref_audio.exists():
            raise HTTPException(500, f"Missing ref audio: {ref_audio}")
        ref_text = Models.ref_texts.get(req.language)
        if not ref_text:
            raise HTTPException(500, f"Missing ref text for '{req.language}'")

        log.info("[clone] lang=%s text=%r", req.language, req.text[:60])
        wavs, sr = Models.base.generate_voice_clone(
            text=req.text, language=cfg["qwen"],
            ref_audio=str(ref_audio), ref_text=ref_text,
        )
    else:  # design
        if Models.design is None:
            Models.design = _load_design()
        log.info("[design] lang=%s text=%r", req.language, req.text[:60])
        wavs, sr = Models.design.generate_voice_design(
            text=req.text, language=cfg["qwen"], instruct=JARVIS_INSTRUCT,
        )

    audio = wavs[0]
    if audio.ndim > 1:
        audio = audio.mean(axis=0) if audio.shape[0] < audio.shape[-1] else audio.mean(axis=1)
    return audio, sr


@app.get("/health")
async def health() -> dict:
    return {
        "status": "ok",
        "base_loaded": Models.base is not None,
        "design_loaded": Models.design is not None,
        "clone_langs": [l for l, c in LANG_ROUTES.items() if c["mode"] == "clone"],
        "design_langs": [l for l, c in LANG_ROUTES.items() if c["mode"] == "design"],
    }


@app.get("/voices")
async def voices() -> dict:
    return {
        "languages": sorted(LANG_ROUTES.keys()),
        "variants": ["dry", "helmet", "ceiling"],
        "default_variant": "helmet",
    }


@app.post("/tts")
async def tts(req: TTSRequest) -> Response:
    audio, sr = _generate(req)
    audio = _apply_fx(audio, sr, req.variant)
    buf = io.BytesIO()
    sf.write(buf, audio, sr, format="WAV", subtype="PCM_16")
    buf.seek(0)
    return Response(
        content=buf.read(),
        media_type="audio/wav",
        headers={
            "X-Sample-Rate": str(sr),
            "X-Language": req.language,
            "X-Variant": req.variant,
        },
    )
