# Qwen3-TTS — Jarvis Voice Cloning (Phase A)

Smoke-Test für Voice-Cloning mit [Qwen3-TTS-12Hz-1.7B-Base](https://huggingface.co/Qwen/Qwen3-TTS-12Hz-1.7B-Base).
Ziel: Bettany-Referenz-Sample → englische + deutsche Output-WAVs zum Anhören.

Läuft in **WSL2 Ubuntu** auf RTX 3080 (10 GB, bfloat16, ~5–6 GB VRAM im Betrieb).

## Voraussetzungen

- WSL2 mit Ubuntu 22.04+
- NVIDIA Driver auf Windows installiert, CUDA in WSL verfügbar (`nvidia-smi` läuft)
- Python 3.10 oder 3.11

```bash
nvidia-smi   # muss die 3080 zeigen
```

## Setup

> **WICHTIG**: venv MUSS auf nativem Linux-FS liegen (`~/...`), NICHT auf `/mnt/c/...`.
> Auf `/mnt/c/` läuft pip install 10–20× langsamer (9P-Protokoll-Overhead).
> Der Projekt-Code bleibt auf `/mnt/c/`, nur die Python-Umgebung liegt im Home.

```bash
# venv ins Home-Verzeichnis (schnelles ext4)
python3 -m venv ~/tts-qwen3-venv
source ~/tts-qwen3-venv/bin/activate

pip install --upgrade pip wheel

# PyTorch erst separat (richtiger CUDA-Build, cu121 passt zu RTX 3080)
pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu121

# In Projekt-Verzeichnis wechseln und Rest installieren
cd /mnt/c/git/MetaMove/ai-services/tts-qwen3
pip install qwen-tts soundfile "numpy<2.0"
```

> **flash-attn** lassen wir erstmal weg — ist optional. Testen mit `--no_flash_attn`.
> Wenn später gewünscht: `pip install flash-attn --no-build-isolation` (kompiliert 10–15 min).

Für spätere Sessions aktivieren:
```bash
source ~/tts-qwen3-venv/bin/activate
cd /mnt/c/git/MetaMove/ai-services/tts-qwen3
```

## Sample vorbereiten

Lege in `samples/` ab:

1. **`jarvis_ref.wav`** — 10–15 s saubere Bettany-Stimme, 16 kHz mono 16-bit WAV
2. **`jarvis_ref.txt`** — exaktes Transkript des Samples, z. B.
   `I find the prospect of meeting your family enormously entertaining.`

Aufbereitung in Audacity:
- Saubersten Abschnitt (10–15 s) ausschneiden, isolierte Stimme, keine Musik/Effekte
- Noise Reduction (Profil aus Stille, dann aufs Ganze)
- Normalisieren auf −3 dB
- Hochpass 80 Hz
- Export: 16 kHz / Mono / 16-bit WAV

## Ausführen

```bash
python test_clone.py
```

Erster Lauf lädt ~3–4 GB Modell-Weights von Hugging Face (einmalig, wird in `~/.cache/huggingface/` gecacht).

Output: `outputs/jarvis_en_01..03.wav` + `outputs/jarvis_de_01..03.wav`.

## Troubleshooting

- **CUDA out of memory**: kleineres Modell nehmen → `--model Qwen/Qwen3-TTS-12Hz-0.6B-Base`
- **FlashAttention Fehler**: `--no_flash_attn` anhängen
- **Langsam / CPU**: prüfe `torch.cuda.is_available()` in Python-REPL
- **Deutsches Output klingt stark britisch**: erwartbar bei Cross-Lingual aus EN-Sample (Feature, nicht Bug — Jarvis *ist* britisch)

## Nächste Phase

Wenn die WAVs überzeugen: Phase B = FastAPI-Server mit `/tts`-Endpoint für Unity-Anbindung.
