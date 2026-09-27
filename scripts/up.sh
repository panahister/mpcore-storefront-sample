#!/usr/bin/env bash
# Starts everything the three backends depend on, in Docker. The backends themselves are NOT
# containerised: run them from your IDE or with scripts/run.sh.
#
#   scripts/up.sh                     # dependencies, plus tracing, metrics and the Kafka browser
#   scripts/up.sh --no-observability  # lighter: dependencies only
set -euo pipefail
source "$(dirname "$0")/lib.sh"

ensure_env
profiles=(--profile observability --profile tools)
[ "${1:-}" = "--no-observability" ] && profiles=()
echo "== starting the dependencies"
compose ${profiles[@]+"${profiles[@]}"} up -d --wait --wait-timeout 300

cat <<INFO

The dependencies are up.
  PostgreSQL   localhost:$(env_value POSTGRES_PORT 45432)   databases storefront_commerce, storefront_fulfillment
  TimescaleDB  localhost:$(env_value TIMESCALE_PORT 45433)   database storefront_analytics
  Kafka        localhost:$(env_value KAFKA_PORT 49092)   browser http://localhost:$(env_value KAFKA_UI_PORT 48080)
  RabbitMQ     localhost:$(env_value RABBITMQ_PORT 45672)   management http://localhost:$(env_value RABBITMQ_UI_PORT 45673)
  Keycloak     http://localhost:$(env_value KEYCLOAK_PORT 48180)   realm "storefront"
  DemoPay      http://localhost:$(env_value WIREMOCK_PORT 48081)/__admin/requests   (WireMock)
  Jaeger       http://localhost:$(env_value JAEGER_UI_PORT 46686)
  Grafana      http://localhost:$(env_value GRAFANA_PORT 43000)
  Prometheus   http://localhost:$(env_value PROMETHEUS_PORT 49090)

Next: scripts/setup.sh (once), then scripts/run.sh commerce, then scripts/scenarios.sh
INFO
