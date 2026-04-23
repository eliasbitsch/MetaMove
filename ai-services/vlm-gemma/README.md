# vlm-gemma — local Gemma 3 Vision proxy for MetaMove

Implements plan step **13c4** (Tier 4 object grounding: natural-language
description). Unity asks "what am I holding?" / "what is this?" via Spatial
Pinch + voice; Unity captures a Quest 3 Passthrough Camera frame and POSTs it
here; the answer goes back through Jarvis-TTS.

Thin proxy on top of [Ollama](https://ollama.com). Ollama does the heavy
lifting (model load, GPU scheduling); this server shapes the API for Unity
and adds an OpenAI-compatible endpoint so other internal clients can reuse it.

## Setup

```bash
# 1. Install and start ollama on the RTX 3080 laptop
ollama pull gemma3:4b     # fits easily; use gemma3:12b for better recognition
ollama serve &            # default port 11434

# 2. Python deps
pip install -r requirements.txt

# 3. Start the proxy
VLM_MODEL=gemma3:4b bash start_server.sh
```

Default port: **8770**. Override with `VLM_PORT`.

## Endpoints

- `GET /health` → `{ ok, model, ollama_reachable }`
- `POST /describe` — MetaMove-native:
  ```json
  { "image_b64": "<raw base64 jpeg/png>", "prompt": "optional", "max_tokens": 128 }
  ```
  returns `{ text, latency_ms, model }`
- `POST /v1/chat/completions` — OpenAI-compatible subset (single-turn,
  single-image). Accepts `image_url` as `data:image/jpeg;base64,...`.

## Latency target

< 2 s end-to-end for 640×480 frame on RTX 3080 with `gemma3:4b`.

## Integration

Unity side: `Assets/MetaMove/Scripts/AI/VlmClient.cs`.
