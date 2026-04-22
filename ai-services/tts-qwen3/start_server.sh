#!/usr/bin/env bash
# Startet den TTS-FastAPI-Server.
# Voraussetzungen: venv aktiviert ODER verwende den Pfad absolut.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
cd "$HERE"

: "${PORT:=8765}"
: "${HOST:=0.0.0.0}"
: "${VENV:=$HOME/tts-qwen3-venv}"

# Venv aktivieren falls nicht schon aktiv
if [[ -z "${VIRTUAL_ENV:-}" ]]; then
    source "$VENV/bin/activate"
fi

exec uvicorn server:app --host "$HOST" --port "$PORT" --log-level info
