#!/usr/bin/env bash
# Stops the dependencies. Data volumes are kept; add --volumes to delete them too and start clean.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
ensure_env
all=(--profile observability --profile tools)
if [ "${1:-}" = "--volumes" ]; then compose "${all[@]}" down --volumes; else compose "${all[@]}" stop; fi
