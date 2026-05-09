# MetaMove AI Stack — Voice Pipeline

**Stand**: 2026-05-10 · WSL2 auf RTX 3080 Ti Laptop (16 GB VRAM).

## Architektur

```
┌── Unity (Quest 3) ──┐       ┌── PC-Server (WSL2, 3080 Ti) ──────────┐
│ JarvisTtsClient.cs  │──────→│ tts-qwen3 (CLI, future server)        │
│ (future: WebSocket) │       │   faster-qwen3-tts (CUDA-graphs)      │
│                     │       │   EN/DE/RU clone, ~600ms TTFA         │
│                     │       │   Pedalboard FX (helmet/ceiling)      │
│                     │←──────│ :8766  llm-gemma                      │
│                     │ audio │   Gemma 4 E4B-it (4bit nf4)           │
└─────────────────────┘       └────────────────────────────────────────┘
```

| Service | venv | Port |
|---|---|---|
| tts-qwen3 (CLI) | `~/tts-qwen3-fast-venv` | — (CLI, kein Server bisher) |
| llm-gemma | `~/llm-gemma-venv` | 8766 |

Getrennte venvs weil Qwen3-TTS auf andere `transformers`-Version pinnt als Gemma 4.

## Verzeichnisse

```
ai-services/
├── README.md
├── TTS.md                      # dieses Dokument
├── tts-qwen3/
│   ├── chat_cli.py             # Terminal-Chat (LLM → TTS → paplay)
│   ├── jarvis_fx.py            # Helmet + Ceiling Pedalboard-Chains
│   ├── samples/                # Voice-Refs (gitignored)
│   │   ├── jarvis_ref.wav + .txt        — EN (Bettany)
│   │   ├── jarvis_de_ref.wav + .txt     — DE (Vollbrecht)
│   │   └── jarvis_ru_ref.wav + .txt     — RU (Qwen3-VoiceDesign)
│   └── .gitignore
└── llm-gemma/
    ├── server.py               # /chat, /chat/stream, /chat/voice, /chat/voice/stream
    └── prompts/jarvis.md       # Persona
```

## Modelle (Disk)

- `~/.cache/huggingface/hub/models--Qwen--Qwen3-TTS-12Hz-1.7B-Base` (~3.5 GB)
- `~/.cache/huggingface/hub/models--google--gemma-4-E4B-it` (~16 GB)

## Starten

**Gemma-Server** (für Chat-Mode):
```bash
source ~/llm-gemma-venv/bin/activate
cd /mnt/c/git/MetaMove/ai-services/llm-gemma
HF_HUB_OFFLINE=1 uvicorn server:app --host 0.0.0.0 --port 8766
```

**TTS-CLI**:
```bash
source ~/tts-qwen3-fast-venv/bin/activate
cd /mnt/c/git/MetaMove/ai-services/tts-qwen3

# Echo-Mode (tippen → wird gesprochen, kein LLM)
TTS_AUDIO_DEVICE=pulse python chat_cli.py --echo --lang de --fx helmet

# Chat-Mode (Gemma + TTS)
TTS_AUDIO_DEVICE=pulse python chat_cli.py --lang de --fx helmet
```

Optionen:
- `--lang en|de|ru` — Default-Sprache (auto-detect pro Satz aktiv)
- `--fx none|helmet|ceiling` — Post-FX Pedalboard-Chain
- `--voice-mode full|xvec` — full = expressiv (default), xvec = stabil/flach
- `--temperature 0.7` — niedrig = stabilere Stimme, hoch = mehr Variation
- `--speed 1.0` — <1 = langsamer + leicht tieferer Pitch
- `--de-helmet` — DE-Sample mit Helmet-FX als Ref (Experiment, nicht empfohlen)

## Performance auf 3080 Ti Laptop

- **TTFA**: ~600 ms steady-state (nach Warmup), ~10s einmalige CUDA-Graph-Capture beim Start
- **RTF**: ~0.4 (1s Audio in 0.4s) → live-streaming reicht locker für realtime
- **First-token Gemma**: 0.5-2s (sdpa, 4bit nf4)
- **End-to-end** (Tippen → erster Ton via Gemma + TTS): typ. 1.5-3s

## Voice-Pipeline-Design

```
Text → faster-qwen3-tts → Pedalboard(reset=False) live → paplay → Speakers
```

**Key tricks**:
- `voice_clone_prompt` precomputed (x-vector) für stabile Stimme + kürzeren Prefill bei `--voice-mode xvec`
- `temperature=0.7` (lower than lib default 0.9) + fixed seed → kein Speaker-Drift
- `chunk_size=12` für niedrige TTFA bei live-streaming
- `Pedalboard(reset=False)` hält Reverb/Filter-State über Chunks → seamless
- 400 ms Tail-Drain am Ende → Reverb klingt sauber aus
- Short-Mode (<20 Zeichen): Collect + Trim + Edge-Fades + 1× Pedalboard → kein Buzz aus Trailing-Silence

## Persona

Datei: `llm-gemma/prompts/jarvis.md`

- British RP, formell, deadpan humor
- Gender-neutral: kein "sir", nutze "operator"
- Extrem kurz antworten (1-2 Sätze max) — kritisch gegen Latenz
- Antwortet in der Sprache in der der User schreibt

## Quick-Reference

```bash
# CosyVoice + XTTS sind nicht mehr installiert — siehe Git-History falls Retry nötig.

# Logs
tail -f /tmp/gemma_server.log

# Gemma-Server killen
pkill -9 -f 'uvicorn.*8766'

# Curl-Test Gemma
curl -X POST http://localhost:8766/chat -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Hi"}]}'
```

## Was als nächstes ansteht

1. **FastAPI-Wrapper** um `chat_cli.py`-Logik (Port :8767) damit Unity konsumieren kann
2. **Unity-Streaming-Client** — JarvisTtsClient WebSocket → progressiver AudioSource-Playback
3. **Wake Word** (OpenWakeWord, CPU)
4. **Tool-Bridge** Unity ↔ Gemma Tool-Calls
