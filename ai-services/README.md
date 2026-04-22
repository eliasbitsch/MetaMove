# ai-services/

Lokale AI-Services für MetaMove. Voll offline-fähig, keine Cloud-Abhängigkeit.

Läuft auf **WSL2 (Ubuntu 24.04)** mit RTX 3080 Ti Laptop (16 GB VRAM).

## Services

| Service | Port | Zweck |
|---|---|---|
| [tts-qwen3/](tts-qwen3/) | 8765 | Text → Sprache (Jarvis-Voice, EN/DE Clone + RU/weitere VoiceDesign) |
| [llm-gemma/](llm-gemma/) | 8766 | Multimodaler Chat (Audio + Vision + Text) mit Jarvis-Persona |

Geplant: `grounding/` (DINO + SAM 2 für Tier-3 Object-Grounding).

## Architektur

```
┌─ Meta Quest 3 (Unity XR) ───────────────────┐
│  Mic  ─────→ Audio                           │
│  Cam  ─────→ Frames (bei Bedarf)             │
│  UI   ─────→ Tool-Call-Bridge                │
│  Speaker ←── Audio                           │
└────────────────┬─────────────────────────────┘
                 │ HTTP + WebSocket
                 ▼
┌─ PC-Server (3080 Ti, WSL2) ─────────────────┐
│                                               │
│  tts-qwen3 (:8765)                           │
│   POST /tts  {text, language, variant} → WAV │
│                                               │
│  llm-gemma (:8766)                           │
│   POST /chat {audio|text|image, history}     │
│        → Text + optional tool_calls          │
│                                               │
│  grounding (:8767, TBD)                      │
│   POST /ground {image, query} → bbox + mask  │
└───────────────────────────────────────────────┘
```

## Python venvs (pro Service eine)

Jeder Service hat eine eigene venv im Linux-Home (NICHT `/mnt/c/`, wegen I/O-Perf):

| Service | venv-Pfad | Grund separater venv |
|---|---|---|
| tts-qwen3 | `~/tts-qwen3-venv` | `qwen-tts` pinnt `transformers==4.57.3` |
| llm-gemma | `~/llm-gemma-venv` | Gemma 4 braucht `transformers>=5.x` (main) |

Inkompatible transformers-Versionen → strikt getrennte Environments.
Disk-Overhead: ~6 GB pro venv (hauptsächlich Torch + CUDA-Libs).

## VRAM-Budget

| Komponente | VRAM |
|---|---|
| Qwen3-TTS 1.7B (bf16) | ~5 GB |
| Gemma 4 E4B-it (4bit nf4) | ~5 GB |
| Grounding DINO (Lazy-Load) | ~1 GB on-demand |
| SAM 2 Base (Lazy-Load) | ~2-4 GB on-demand |
| **Gleichzeitig-Max** | **~10 GB** (TTS + LLM) |

Grounding wird on-demand geladen/entladen, Details siehe jeweilige README.

## Start-Reihenfolge

```bash
# Terminal 1
cd ai-services/tts-qwen3
source ~/tts-qwen3-venv/bin/activate
uvicorn server:app --port 8765

# Terminal 2 (wenn verfügbar)
cd ai-services/llm-gemma
source ~/tts-qwen3-venv/bin/activate
uvicorn server:app --port 8766
```

## Tool-Use-Konzept

Gemma erhält zur Laufzeit ein **dynamisches Tool-Schema** von Unity:
- Welche Panels sind verfügbar? (`open_panel`, `explain_panel`)
- Welche Robot-Commands sind erlaubt? (`move_end_effector`, `emergency_stop`)
- Welche Perception-Calls sind aktiv? (`describe_scene`, `locate_object`)

Gemma emittiert Tool-Calls im JSON-Format, Service forwarded an Unity via
WebSocket, Unity führt aus und antwortet. Details siehe `llm-gemma/README.md`
(kommt mit Tool-Use-Implementierung).
