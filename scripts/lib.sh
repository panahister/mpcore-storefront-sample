#!/usr/bin/env bash
# Shared settings for the scripts. Sourced, never run.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INFRA_DIR="$REPO_ROOT/infrastructure"
ENV_FILE="$INFRA_DIR/.env"
ENV_EXAMPLE="$INFRA_DIR/.env.example"

# The three backends: name, project, and the address its health probe answers on.
BACKENDS=(commerce fulfillment analytics)
project_of() {
  case "$1" in
    commerce)    echo "$REPO_ROOT/commerce/src/Storefront.Commerce.Api" ;;
    fulfillment) echo "$REPO_ROOT/fulfillment/src/Storefront.Fulfillment.Api" ;;
    analytics)   echo "$REPO_ROOT/analytics/src/Storefront.Analytics.Api" ;;
    *) echo "unknown backend: $1 (one of: ${BACKENDS[*]})" >&2; return 2 ;;
  esac
}
health_of() {
  case "$1" in
    commerce)    echo "http://localhost:5100/health/ready" ;;
    fulfillment) echo "grpc://localhost:5201 grpc.health.v1.Health/Check" ;;
    analytics)   echo "http://localhost:5300/health/ready" ;;
  esac
}

ensure_env() {
  if [ ! -f "$ENV_FILE" ]; then
    cp "$ENV_EXAMPLE" "$ENV_FILE"
    echo "created infrastructure/.env from .env.example"
  fi
}

# Reads a variable from infrastructure/.env, falling back to .env.example, then to a default.
env_value() {
  local key="$1" default="$2" value=""
  for f in "$ENV_FILE" "$ENV_EXAMPLE"; do
    [ -f "$f" ] || continue
    value=$(grep -E "^${key}=" "$f" | tail -1 | cut -d= -f2-)
    [ -n "$value" ] && { printf '%s' "$value"; return; }
  done
  printf '%s' "$default"
}

compose() { docker compose --env-file "$ENV_FILE" -f "$INFRA_DIR/compose.yaml" "$@"; }
