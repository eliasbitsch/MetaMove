"""
FastAPI Gemma-Chat-Server mit Jarvis-Persona.

Endpoints:
  POST /chat          -> blocking {text: str}
  POST /chat/stream   -> SSE stream (token-by-token)
  GET  /health        -> ready status
  GET  /system_prompt -> current Jarvis persona prompt
  POST /reset         -> clear conversation history (keyed by session_id)

Start:
  source ~/llm-gemma-venv/bin/activate
  uvicorn server:app --host 0.0.0.0 --port 8766
"""
from __future__ import annotations

import asyncio
import json
import logging
import time
from contextlib import asynccontextmanager
from pathlib import Path
from threading import Thread
from typing import AsyncIterator

import io
from typing import Optional

import librosa
import numpy as np
import torch
from fastapi import FastAPI, File, Form, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from PIL import Image
from pydantic import BaseModel, Field
from transformers import (
    AutoModelForCausalLM,
    AutoProcessor,
    BitsAndBytesConfig,
    TextIteratorStreamer,
)

log = logging.getLogger("llm-gemma")
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

HERE = Path(__file__).parent
PROMPT_FILE = HERE / "prompts" / "jarvis.md"
MODEL_ID = "google/gemma-4-E4B-it"


class ChatMessage(BaseModel):
    role: str = Field(..., pattern="^(user|assistant|system)$")
    content: str


class ChatRequest(BaseModel):
    messages: list[ChatMessage] = Field(..., min_length=1)
    session_id: str | None = None
    max_new_tokens: int = 200
    temperature: float = 0.6
    top_p: float = 0.9
    repetition_penalty: float = 1.08


class Models:
    model = None
    tokenizer = None
    processor = None
    system_prompt: str = ""
    # Simple in-process session store: session_id -> list[ChatMessage]
    sessions: dict[str, list[dict]] = {}


def _load_model():
    log.info("Loading %s (4bit nf4, sdpa)", MODEL_ID)
    bnb = BitsAndBytesConfig(
        load_in_4bit=True,
        bnb_4bit_quant_type="nf4",
        bnb_4bit_compute_dtype=torch.bfloat16,
        bnb_4bit_use_double_quant=True,
        # Keep multimodal encoders and lm_head in bf16 (quantization breaks them).
        llm_int8_skip_modules=[
            "model.audio_tower",
            "model.vision_tower",
            "model.embed_audio",
            "model.embed_vision",
            "lm_head",
        ],
    )
    model = AutoModelForCausalLM.from_pretrained(
        MODEL_ID,
        device_map="cuda:0",
        quantization_config=bnb,
        attn_implementation="sdpa",
    )
    processor = AutoProcessor.from_pretrained(MODEL_ID)
    tokenizer = processor.tokenizer if hasattr(processor, "tokenizer") else processor
    return model, tokenizer, processor


@asynccontextmanager
async def lifespan(app: FastAPI):
    Models.system_prompt = PROMPT_FILE.read_text(encoding="utf-8")
    Models.model, Models.tokenizer, Models.processor = _load_model()
    log.info("Gemma ready (%d char system prompt)", len(Models.system_prompt))
    yield
    log.info("Shutdown")


app = FastAPI(title="Jarvis Chat (Gemma 4)", version="0.1", lifespan=lifespan)
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])


def _build_messages(req: ChatRequest) -> list[dict]:
    """Combine system prompt + history + new messages. Persist to session if given."""
    history = Models.sessions.get(req.session_id, []) if req.session_id else []
    incoming = [{"role": m.role, "content": m.content} for m in req.messages]
    full = [{"role": "system", "content": Models.system_prompt}] + history + incoming
    return full


def _voice_messages(session_id: str | None, audio: np.ndarray, text: str | None) -> list[dict]:
    """Build multimodal messages with audio (and optional text) as user turn."""
    history = Models.sessions.get(session_id, []) if session_id else []
    user_content = [{"type": "audio", "audio": audio}]
    if text:
        user_content.append({"type": "text", "text": text})
    return (
        [{"role": "system", "content": [{"type": "text", "text": Models.system_prompt}]}]
        + history
        + [{"role": "user", "content": user_content}]
    )


def _persist(session_id: str | None, incoming: list[dict], response: str) -> None:
    if not session_id:
        return
    hist = Models.sessions.setdefault(session_id, [])
    hist.extend(incoming)
    hist.append({"role": "assistant", "content": response})
    # cap history at 20 turns to bound VRAM
    if len(hist) > 40:
        Models.sessions[session_id] = hist[-40:]


def _prepare_inputs(messages: list[dict]):
    prompt = Models.tokenizer.apply_chat_template(
        messages, tokenize=False, add_generation_prompt=True,
    )
    return Models.tokenizer(prompt, return_tensors="pt").to("cuda:0")


@app.get("/health")
async def health() -> dict:
    return {
        "status": "ok",
        "model": MODEL_ID,
        "system_prompt_chars": len(Models.system_prompt),
        "active_sessions": len(Models.sessions),
    }


@app.get("/system_prompt")
async def system_prompt() -> dict:
    return {"prompt": Models.system_prompt}


@app.post("/reset")
async def reset(session_id: str) -> dict:
    removed = Models.sessions.pop(session_id, None)
    return {"removed": bool(removed)}


@app.post("/chat")
async def chat(req: ChatRequest) -> dict:
    messages = _build_messages(req)
    inputs = _prepare_inputs(messages)
    t0 = time.time()
    with torch.inference_mode():
        out = Models.model.generate(
            **inputs,
            max_new_tokens=req.max_new_tokens,
            do_sample=req.temperature > 0,
            temperature=req.temperature,
            top_p=req.top_p,
            repetition_penalty=req.repetition_penalty,
            pad_token_id=Models.tokenizer.eos_token_id,
        )
    resp = Models.tokenizer.decode(
        out[0][inputs.input_ids.shape[1]:], skip_special_tokens=True,
    ).strip()
    dt = time.time() - t0
    log.info("[chat] %.2fs -> %r", dt, resp[:80])
    _persist(req.session_id, [{"role": m.role, "content": m.content} for m in req.messages], resp)
    return {"text": resp, "latency_s": round(dt, 3)}


@app.post("/chat/voice")
async def chat_voice(
    audio: UploadFile = File(..., description="Audio file (wav, mp3, flac, ogg, ...)"),
    text: Optional[str] = Form(None, description="Optional accompanying text instruction"),
    session_id: Optional[str] = Form(None),
    max_new_tokens: int = Form(200),
    temperature: float = Form(0.6),
) -> dict:
    """Accept uploaded audio, transcribe implicitly via Gemma, respond as Jarvis."""
    raw = await audio.read()
    try:
        wav, _ = librosa.load(io.BytesIO(raw), sr=16000, mono=True)
    except Exception as e:
        raise HTTPException(400, f"Could not decode audio: {e}")
    log.info("[voice] %s %.2fs %s", audio.filename, len(wav) / 16000, f"text={text!r}" if text else "")

    messages = _voice_messages(session_id, wav, text)
    inputs = Models.processor.apply_chat_template(
        messages, add_generation_prompt=True, tokenize=True,
        return_dict=True, return_tensors="pt",
    ).to("cuda:0")

    t0 = time.time()
    with torch.inference_mode():
        out = Models.model.generate(
            **inputs,
            max_new_tokens=max_new_tokens,
            do_sample=temperature > 0,
            temperature=temperature,
            top_p=0.9,
            repetition_penalty=1.08,
            pad_token_id=Models.tokenizer.eos_token_id,
        )
    resp = Models.tokenizer.decode(
        out[0][inputs["input_ids"].shape[1]:], skip_special_tokens=True,
    ).strip()
    dt = time.time() - t0
    log.info("[voice] %.2fs -> %r", dt, resp[:80])

    # Persist audio turn as a placeholder; store text response
    if session_id:
        hist = Models.sessions.setdefault(session_id, [])
        hist.append({"role": "user", "content": f"[audio {len(wav)/16000:.1f}s]" + (f" {text}" if text else "")})
        hist.append({"role": "assistant", "content": resp})

    return {"text": resp, "latency_s": round(dt, 3), "audio_duration_s": round(len(wav) / 16000, 2)}


@app.post("/chat/stream")
async def chat_stream(req: ChatRequest):
    messages = _build_messages(req)
    inputs = _prepare_inputs(messages)

    streamer = TextIteratorStreamer(
        Models.tokenizer, skip_prompt=True, skip_special_tokens=True,
    )
    gen_kwargs = dict(
        **inputs,
        max_new_tokens=req.max_new_tokens,
        do_sample=req.temperature > 0,
        temperature=req.temperature,
        top_p=req.top_p,
        repetition_penalty=req.repetition_penalty,
        pad_token_id=Models.tokenizer.eos_token_id,
        streamer=streamer,
    )
    Thread(target=lambda: Models.model.generate(**gen_kwargs), daemon=True).start()

    async def event_gen() -> AsyncIterator[str]:
        collected = []
        t0 = time.time()
        first = True
        for token in streamer:
            if not token:
                continue
            collected.append(token)
            if first:
                log.info("[stream] first token %.2fs", time.time() - t0)
                first = False
            payload = json.dumps({"token": token}, ensure_ascii=False)
            yield f"data: {payload}\n\n"
            # yield to asyncio
            await asyncio.sleep(0)
        full = "".join(collected).strip()
        log.info("[stream] complete %.2fs -> %r", time.time() - t0, full[:80])
        _persist(req.session_id, [{"role": m.role, "content": m.content} for m in req.messages], full)
        yield f"data: {json.dumps({'done': True, 'text': full}, ensure_ascii=False)}\n\n"

    return StreamingResponse(event_gen(), media_type="text/event-stream")
