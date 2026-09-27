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
  # What the caller's environment says wins: EDGE_AUTH=keycloak scripts/up.sh
  if [ -n "${!key:-}" ]; then printf '%s' "${!key}"; return; fi
  for f in "$ENV_FILE" "$ENV_EXAMPLE"; do
    [ -f "$f" ] || continue
    value=$(grep -E "^${key}=" "$f" | tail -1 | cut -d= -f2-)
    [ -n "$value" ] && { printf '%s' "$value"; return; }
  done
  printf '%s' "$default"
}

compose() { docker compose --env-file "$ENV_FILE" -f "$INFRA_DIR/compose.yaml" "$@"; }

# The edge needs a certificate. One is made for this machine, for the name "localhost", and kept outside
# the repository's history (infrastructure/apisix/generated is ignored). It is trusted by nobody: a caller
# names it explicitly (curl --cacert), which is what the scenarios do.
GATEWAY_DIR="$INFRA_DIR/apisix/generated"
GATEWAY_CA="$GATEWAY_DIR/localhost.crt"
ensure_gateway_config() {
  mkdir -p "$GATEWAY_DIR"
  if [ ! -s "$GATEWAY_CA" ] || [ ! -s "$GATEWAY_DIR/localhost.key" ]; then
    openssl req -x509 -newkey rsa:2048 -nodes -days 825 -subj "/CN=localhost" \
      -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
      -keyout "$GATEWAY_DIR/localhost.key" -out "$GATEWAY_CA" >/dev/null 2>&1
    echo "made a certificate for the edge: infrastructure/apisix/generated/localhost.crt"
  fi
  local auth; auth="$(env_value EDGE_AUTH off)"
  [ -f "$INFRA_DIR/apisix/edge-auth.$auth.yaml" ] || { echo "EDGE_AUTH=$auth: there is no infrastructure/apisix/edge-auth.$auth.yaml (off, keycloak)" >&2; return 2; }
  awk -v cert="$GATEWAY_CA" -v key="$GATEWAY_DIR/localhost.key" -v auth="$INFRA_DIR/apisix/edge-auth.$auth.yaml" '
    function paste(file, margin,   line) { while ((getline line < file) > 0) if (line !~ /^#/) print margin line; close(file) }
    /^__CERTIFICATE__$/ { paste(cert, "      "); next }
    /^__KEY__$/         { paste(key, "      "); next }
    /^__EDGE_AUTH__$/   { paste(auth, "      "); next }
    { print }' "$INFRA_DIR/apisix/apisix.template.yaml" > "$GATEWAY_DIR/apisix.yaml"
  echo "the edge: EDGE_AUTH=$auth"
}

