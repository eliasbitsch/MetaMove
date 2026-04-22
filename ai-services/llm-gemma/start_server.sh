#!/usr/bin/env bash
# Startet den Gemma-Chat-FastAPI-Server.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
cd "$HERE"

: "${PORT:=8766}"
: "${HOST:=0.0.0.0}"
: "${VENV:=$HOME/llm-gemma-venv}"

if [[ -z "${VIRTUAL_ENV:-}" ]]; then
    source "$VENV/bin/activate"
fi

exec uvicorn server:app --host "$HOST" --port "$PORT" --log-level info
