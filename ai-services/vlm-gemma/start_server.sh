#!/usr/bin/env bash
# Start VLM proxy. Assumes ollama is already running on $OLLAMA_HOST
# (default http://127.0.0.1:11434) with the configured model pulled.
#
# Quickstart:
#   ollama pull gemma3:4b     # or gemma3:12b on the RTX 3080 laptop
#   VLM_MODEL=gemma3:4b bash start_server.sh
set -euo pipefail
cd "$(dirname "$0")"
export VLM_MODEL="${VLM_MODEL:-gemma3:4b}"
export OLLAMA_HOST="${OLLAMA_HOST:-http://127.0.0.1:11434}"
exec uvicorn server:app --host 0.0.0.0 --port "${VLM_PORT:-8770}"
