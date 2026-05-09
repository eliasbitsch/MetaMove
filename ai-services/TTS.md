# MetaMove AI Stack — Session Handoff

**Stand**: 2026-05-10 · alle Services laufen lokal in WSL2 auf RTX 3080 Ti Laptop (16 GB VRAM).

## 🎯 Was gebaut ist

Kompletter lokaler Voice-Assistant-Stack "J.A.R.V.I.S." für MetaMove mit:
- **Voice Cloning** EN/DE/RU mit Qwen3-TTS-12Hz-1.7B-Base + `faster-qwen3-tts` (CUDA-graphs, ~600 ms TTFA)
- **Multimodales LLM** (Gemma 4) mit Audio- und Vision-Input
- **Post-FX Pedalboard** (Helmet/Ceiling) für Iron-Man-Intercom-Sound
- **Persona-Prompt** mit MetaMove-Kontext + Safety-Events

## 🏗️ Architektur

```
┌── Unity (Quest 3) ──┐       ┌── PC-Server (WSL2, 3080 Ti) ──────────┐
│ JarvisTtsClient.cs  │──────→│ :8767  tts-qwen3-fast (Production)    │
│                     │       │   faster-qwen3-tts (CUDA-graphs)      │
│                     │       │   EN/DE/RU clone, ~600ms TTFA         │
│                     │       │   Pedalboard FX (helmet/ceiling)      │
│                     │←──────│ :8766  llm-gemma                      │
│                     │ audio │   Gemma 4 E4B-it (4bit nf4)           │
└─────────────────────┘       └────────────────────────────────────────┘

Legacy / dev only:
  :8765  tts-qwen3  (vanilla HF transformers, ~4-6s TTFA — kept for FX-chain code)
```

### Services
| Port | Service | venv | Status |
|---|---|---|---|
| 8767 | tts-qwen3-fast | `~/tts-qwen3-fast-venv` | **Production** |
| 8766 | llm-gemma | `~/llm-gemma-venv` | Production |
| 8765 | tts-qwen3 (slow) | `~/tts-qwen3-venv` | Legacy / FX source |

`tts-qwen3-fast` reuses `tts-qwen3/samples/` (refs) + `tts-qwen3/apply_jarvis_fx.py` (FX chains).
Getrennte venvs weil `qwen-tts` transformers 4.57.3 pinnt und Gemma 4 transformers ≥5.x braucht.

### Sprach-Refs
- `samples/jarvis_ref.wav` — EN (Bettany)
- `samples/jarvis_de_ref.wav` — DE (Vollbrecht)
- `samples/jarvis_ru_ref.wav` — RU (Qwen3-VoiceDesign generated, native phonetics)

### Verzeichnisse
```
ai-services/
├── README.md              # high-level overview
├── TTS.md                 # dieses Dokument
├── jarvis_cli.py          # CLI text→speech demo
├── tts-qwen3/             # TTS-Server
│   ├── server.py          # /tts, /tts/stream
│   ├── apply_jarvis_fx.py # Helmet + Ceiling FX chains
│   ├── samples/           # jarvis_ref.wav (EN) + jarvis_de_ref.wav (DE) — .gitignored
│   ├── outputs/           # generated WAVs — .gitignored
│   └── test_clone.py, test_voice_design.py, test_stream.py, generate_ref_samples.py
└── llm-gemma/
    ├── server.py          # /chat, /chat/stream, /chat/voice, /chat/voice/stream
    ├── prompts/jarvis.md  # Persona
    ├── test_chat.py, test_multimodal.py, test_audio_raw.py, test_e2e.py, test_voice_stream.py
```

### Modelle (Disk)
- `~/.cache/huggingface/hub/models--Qwen--Qwen3-TTS-12Hz-1.7B-Base` (~3.5 GB)
- `~/.cache/huggingface/hub/models--Qwen--Qwen3-TTS-12Hz-1.7B-VoiceDesign` (~3.5 GB)
- `~/.cache/huggingface/hub/models--google--gemma-4-E4B-it` (~16 GB)

## 🚀 Starten

Beide Server in Background:
```bash
# Terminal 1
cd /mnt/c/git/MetaMove/ai-services/tts-qwen3
source ~/tts-qwen3-venv/bin/activate
uvicorn server:app --host 0.0.0.0 --port 8765

# Terminal 2
cd /mnt/c/git/MetaMove/ai-services/llm-gemma
source ~/llm-gemma-venv/bin/activate
uvicorn server:app --host 0.0.0.0 --port 8766
```

Oder One-shot:
```bash
cd /mnt/c/git/MetaMove/ai-services/tts-qwen3 && setsid nohup ~/tts-qwen3-venv/bin/uvicorn server:app --host 0.0.0.0 --port 8765 > /tmp/tts_server.log 2>&1 < /dev/null & disown
cd /mnt/c/git/MetaMove/ai-services/llm-gemma && setsid nohup ~/llm-gemma-venv/bin/uvicorn server:app --host 0.0.0.0 --port 8766 > /tmp/gemma_server.log 2>&1 < /dev/null & disown
```

Health-Check:
```bash
curl http://localhost:8765/health
curl http://localhost:8766/health
```

## 💬 CLI testen

```bash
python3 /mnt/c/git/MetaMove/ai-services/jarvis_cli.py
# Optionen: --lang en|de|ru|... --variant dry|helmet|ceiling --no-play
```

Spielt Chunks über Windows-PowerShell-SoundPlayer (WSL). WAVs landen in `tts-qwen3/outputs/cli/` (Windows-sichtbar unter `C:\git\MetaMove\...`).

## 📊 Performance auf 3080 Ti Laptop — ehrlich

**TTS RTF**: ~3x realtime (3s Generierung pro 1s Audio). flash-attn bringt auf TTS kaum Gewinn.
**Gemma**: 2-8s pro Antwort je nach Länge (sdpa, 4bit).
**Pipeline TTFA** (Text-in → erster Ton): **~5-15s** abhängig von Antwort-Länge.

Streaming-Endpoints liefern den **ersten Chunk** deutlich früher (~2-5s nach Gemma-Ende), aber **Pausen zwischen Chunks** 5-15s wenn Antwort länger — weil RTF > 1 Server kann nicht mit Playback mithalten.

**UX**: "zäher Walkie-Talkie". Für Test / Demo ausreichend, für echtes Live-Gespräch nicht.

## 🎭 Jarvis-Persona

Datei: `ai-services/llm-gemma/prompts/jarvis.md`

Key-Points:
- British RP, formell, deadpan humor
- **Gender-neutral**: kein "sir", nutze "operator" wenn Anrede nötig
- **MetaMove-Kontext**: FH Technikum Wien, GoFa CRB 15000
- **Extrem kurz** antworten (1 Satz default, max 2) — kritisch gegen Latenz
- **Tool-Use**: `open_panel`, `move_end_effector`, etc. (noch nicht mit Unity verdrahtet)
- **Safety-Events**: proaktiv bei proximity_caution/warning/critical
- Antwortet in der Sprache in der der User schreibt/spricht

## 🎙️ Voice-Samples

| Sprache | Datei | Quelle | Modus |
|---|---|---|---|
| EN | `tts-qwen3/samples/jarvis_ref.wav` | Bettany Malibu-Szene (Iron Man 1) | Voice-Clone |
| DE | `tts-qwen3/samples/jarvis_de_ref.wav` | deutsche Synchro (Bernd Vollbrecht) | Voice-Clone |
| RU + ja/ko/fr/es/it/pt | keine | — | VoiceDesign aus Jarvis-Prompt |

Alle `samples/` und `outputs/` sind `.gitignored` (Urheberrecht + Größe).

## 🔊 FX-Chain (pedalboard)

Zwei Presets:
- **helmet**: Iron-Man-Intercom (300-5k Bandpass, Presence @2.5k, Compressor 6:1, kurzer Reverb)
- **ceiling**: Villa-Deckenlautsprecher (120-9k, mittlerer Reverb)

Code: `tts-qwen3/apply_jarvis_fx.py` — gleiche Chain wird sowohl offline (Test-Scripts) als auch im Server-Response angewendet.

## 🛑 Was versucht wurde und nicht klappte

### flash-attn für TTS
Installiert (`flash-attn 2.8.3` prebuilt wheel für cu12/torch2.5/cp312), aber bringt kaum Geschwindigkeitsgewinn für TTS (kurze Sequenzen).

### flash-attn für Gemma
Geht nicht — Gemma 4 hat `head_dim > 256`, wird von flash-attn v2 nicht unterstützt. Bleibt bei sdpa.

### vllm-omni für echtes TTS-Streaming
~3 Stunden investiert. `async_chunk=True` lässt sich setzen, Engine startet (74-90s warmup), aber `async for stage_output in omni.generate(...)` yielded immer `finished=True` mit chunks=0. End2end.py-Beispiel funktioniert, mein Custom-Wrapper nicht. Würde weiteres Debug + vllm-Team-Support brauchen. **Parked**.

### CosyVoice 2
Installiert, Modell runtergeladen (3 GB). Voice-Clone EN funktioniert mit Pfad-Input (nicht Tensor).
**Deutsche Outputs halluzinieren extrem** (16s Audio für 5s Satz), chinesisch-geprägter Klang.
**Nicht geeignet** für unser EN/DE/RU-Szenario. Venv `~/cosyvoice-venv` bleibt auf Disk falls wir irgendwann wieder testen.

### vllm-omni-Bug-Kontext für späteres Retry
- Args-Setup: `args = parse_args()` aus `end2end.py` funktioniert, mein `FlexibleArgumentParser()` mit nichts reichen nicht für Default-Config
- `_estimate_prompt_len` braucht `file://` URL für lokale Pfade (nicht plain `/mnt/c/...`), sonst `MediaConnector.fetch_audio()` crasht
- Bug: Selbst mit korrekten Inputs yielded Engine nur ein `finished=True`-Output mit 3840 Samples = 0.16s statt echtem Streaming

## ⏭️ Was als nächstes ansteht

Priorisiert:
1. **Unity-Streaming-Client** — `JarvisTtsClient.cs` erweitern um `/tts/stream` zu konsumieren, progressiven `AudioClip.SetData()` Playback
2. **Unity-Orchestrator** — Push-to-Talk → `/chat/voice/stream` (Gemma→TTS pipeline) — WAV-Chunks live in Queue abspielen
3. **Wake Word** — OpenWakeWord-Service in `ai-services/wakeword/`, ~5 MB Modell, CPU
4. **Tool-Bridge** — Unity WebSocket-Endpoint der die Tool-Calls aus Gemma empfängt (`open_panel` etc.) und UI-Aktionen ausführt
5. **Streaming-Verbesserung** (nur wenn nötig): vllm-omni retry mit längerer Zeit oder alternative TTS

## 🌐 Git

Die TTS/Gemma/CLI-Arbeit sitzt in den Commits:
```
57f78c0  Phase L2: /chat/voice/stream — Gemma tokens piped live to TTS
2960a55  TTS pseudo-streaming (/tts/stream)
b584bd0  Gemma /chat/voice — audio-in Jarvis-out in one step
98b77c7  Gemma FastAPI + SSE streaming
600ebb9  Split venvs, add flash-attn, first working Jarvis chat via Gemma 4
c555708  TTS FastAPI + Unity client
9255a97  Qwen3-TTS Jarvis pipeline (EN/DE clone + RU VoiceDesign)
```

Noch uncommitted (Stand jetzt):
- `ai-services/jarvis_cli.py`
- Prompt-Update (kürzere Antworten)
- CosyVoice Experiment-Verzeichnis falls noch da (kannst du löschen)

## 📌 Quick-Reference Commands

```bash
# Server starten
cd /mnt/c/git/MetaMove/ai-services/tts-qwen3 && setsid nohup ~/tts-qwen3-venv/bin/uvicorn server:app --host 0.0.0.0 --port 8765 > /tmp/tts_server.log 2>&1 < /dev/null & disown
cd /mnt/c/git/MetaMove/ai-services/llm-gemma && setsid nohup ~/llm-gemma-venv/bin/uvicorn server:app --host 0.0.0.0 --port 8766 > /tmp/gemma_server.log 2>&1 < /dev/null & disown

# Server killen
pkill -9 -f 'uvicorn.*876[56]'

# Logs schauen
tail -f /tmp/tts_server.log
tail -f /tmp/gemma_server.log

# CLI-Test
python3 /mnt/c/git/MetaMove/ai-services/jarvis_cli.py

# Curl-Test TTS
curl -X POST http://localhost:8765/tts -H "Content-Type: application/json" \
  -d '{"text":"Test.","language":"de","variant":"helmet"}' -o /tmp/t.wav

# Curl-Test Gemma
curl -X POST http://localhost:8766/chat -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Hi"}]}'
```

## 💡 Wiedereinsteiger-Prompt für neue Session

> "Lies ai-services/TTS.md. Status: TTS+Gemma-Pipeline läuft, Jarvis spricht EN/DE/RU via CLI. Nächster Schritt: [Unity-Streaming-Client / Wake Word / Tool-Bridge / ...]"
