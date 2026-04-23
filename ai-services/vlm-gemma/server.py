"""
FastAPI VLM server — lokale Gemma-3-Vision-Beschreibung fuer MetaMove.

Plan step 13c4 (Tier 4 object understanding): User haelt Objekt hoch oder
zielt per Spatial Pinch + Voice-Query ("Was ist das?" / "Was halte ich da?"),
Unity schickt Passthrough-Frame + Prompt hierher, bekommt Text zurueck, gibt
ihn an Jarvis-TTS.

Backend: Ollama laeuft als separate Prozess / Container (`ollama serve`) mit
einem Gemma-3-Vision-Modell installiert (`ollama pull gemma3:4b` oder
`gemma3:12b`). Dieser Server ist ein duenner Proxy, der:
  - einen einfachen JSON-Endpunkt fuer Unity bietet (/describe)
  - den OpenAI-kompatiblen Chat-Completions-Endpunkt (/v1/chat/completions)
    spricht, damit andere lokale Clients (jarvis_cli, etc.) wiederverwendet
    werden koennen.

Endpoints:
  POST /describe               { image_b64, prompt?, max_tokens? } -> { text, latency_ms }
  POST /v1/chat/completions    OpenAI-compatible (image_url data-uri supported)
  GET  /health                 { ok, model, ollama_reachable }

Start:
  uvicorn server:app --host 0.0.0.0 --port 8770
"""
from __future__ import annotations

import base64
import logging
import os
import time
from typing import Any

import httpx
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

log = logging.getLogger("vlm-gemma")
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")

OLLAMA_HOST = os.environ.get("OLLAMA_HOST", "http://127.0.0.1:11434")
MODEL = os.environ.get("VLM_MODEL", "gemma3:4b")
DEFAULT_PROMPT = (
    "Describe what is in the image in one or two short sentences. "
    "If a person is holding something, name the object and its likely purpose. "
    "Be specific and factual, no speculation."
)

app = FastAPI(title="MetaMove VLM (Gemma 3)", version="0.1")
app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])


class DescribeRequest(BaseModel):
    image_b64: str = Field(..., description="Raw base64 (no data: prefix) of a JPEG/PNG frame.")
    prompt: str | None = None
    max_tokens: int | None = 128


class DescribeResponse(BaseModel):
    text: str
    latency_ms: int
    model: str


@app.get("/health")
async def health() -> dict[str, Any]:
    ok = False
    try:
        async with httpx.AsyncClient(timeout=2.0) as c:
            r = await c.get(f"{OLLAMA_HOST}/api/tags")
            ok = r.status_code == 200
    except Exception as exc:
        log.warning("ollama unreachable: %s", exc)
    return {"ok": ok, "model": MODEL, "ollama_reachable": ok}


@app.post("/describe", response_model=DescribeResponse)
async def describe(req: DescribeRequest) -> DescribeResponse:
    t0 = time.time()
    prompt = req.prompt or DEFAULT_PROMPT

    # Ollama generate API accepts base64 images directly in `images`.
    payload = {
        "model": MODEL,
        "prompt": prompt,
        "images": [req.image_b64],
        "stream": False,
        "options": {"num_predict": req.max_tokens or 128},
    }
    try:
        async with httpx.AsyncClient(timeout=30.0) as c:
            r = await c.post(f"{OLLAMA_HOST}/api/generate", json=payload)
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=503, detail=f"ollama unreachable: {exc}") from exc

    if r.status_code != 200:
        raise HTTPException(status_code=502, detail=f"ollama error: {r.status_code} {r.text[:200]}")

    data = r.json()
    text = (data.get("response") or "").strip()
    return DescribeResponse(text=text, latency_ms=int((time.time() - t0) * 1000), model=MODEL)


# OpenAI-compatible subset: only single-turn, single-image chat completion.
# Sufficient for jarvis_cli and other internal callers that already speak
# the OpenAI protocol — we keep it minimal on purpose.
@app.post("/v1/chat/completions")
async def chat_completions(body: dict[str, Any]) -> dict[str, Any]:
    t0 = time.time()
    messages = body.get("messages") or []
    image_b64: str | None = None
    prompt_parts: list[str] = []
    for msg in messages:
        content = msg.get("content")
        if isinstance(content, str):
            prompt_parts.append(content)
        elif isinstance(content, list):
            for part in content:
                ptype = part.get("type")
                if ptype == "text":
                    prompt_parts.append(part.get("text", ""))
                elif ptype == "image_url":
                    url = (part.get("image_url") or {}).get("url", "")
                    if url.startswith("data:"):
                        image_b64 = url.split(",", 1)[-1]
                    else:
                        try:
                            async with httpx.AsyncClient(timeout=10.0) as c:
                                rr = await c.get(url)
                                rr.raise_for_status()
                                image_b64 = base64.b64encode(rr.content).decode()
                        except Exception as exc:
                            raise HTTPException(status_code=400, detail=f"image fetch failed: {exc}") from exc

    if image_b64 is None:
        raise HTTPException(status_code=400, detail="no image supplied")

    prompt = "\n".join(p for p in prompt_parts if p).strip() or DEFAULT_PROMPT
    describe_req = DescribeRequest(image_b64=image_b64, prompt=prompt,
                                   max_tokens=body.get("max_tokens") or 128)
    resp = await describe(describe_req)

    return {
        "id": f"vlm-{int(t0 * 1000)}",
        "object": "chat.completion",
        "created": int(t0),
        "model": resp.model,
        "choices": [{
            "index": 0,
            "message": {"role": "assistant", "content": resp.text},
            "finish_reason": "stop",
        }],
        "usage": {"prompt_tokens": -1, "completion_tokens": -1, "total_tokens": -1},
    }
