#!/usr/bin/env bash
# Writes the addresses of the dependencies into each backend's user secrets
# (~/.microsoft/usersecrets/<id>), outside the repository. Nothing is written into a tracked file.
# Run once, and again if you change a port in infrastructure/.env.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
ensure_env

user=$(env_value POSTGRES_USER storefront); password=$(env_value POSTGRES_PASSWORD storefront)
postgres="Host=localhost;Port=$(env_value POSTGRES_PORT 45432);Username=$user;Password=$password"
timescale="Host=localhost;Port=$(env_value TIMESCALE_PORT 45433);Username=$user;Password=$password"
redis="localhost:$(env_value REDIS_PORT 46379),abortConnect=false"
authority="http://localhost:$(env_value KEYCLOAK_PORT 48180)/realms/storefront"
kafka="localhost:$(env_value KAFKA_PORT 49092)"
rabbit="amqp://$(env_value RABBITMQ_USER storefront):$(env_value RABBITMQ_PASSWORD storefront)@localhost:$(env_value RABBITMQ_PORT 45672)"
otlp="http://localhost:$(env_value OTLP_GRPC_PORT 44317)"
demopay="http://localhost:$(env_value WIREMOCK_PORT 48081)/demo-pay/"

secret() { dotnet user-secrets --project "$1" set "$2" "$3" >/dev/null; }
common() { # common PROJECT
  secret "$1" "Security:Authority" "$authority"
  for signal in Logs Metrics Traces; do secret "$1" "Observability:$signal:Endpoint" "$otlp"; done
}

for backend in "${BACKENDS[@]}"; do
  project="$(project_of "$backend")"
  [ -d "$project" ] || { echo "   $backend: not in this checkout, skipped"; continue; }
  common "$project"
  case "$backend" in
    commerce)
      secret "$project" "ConnectionStrings:PostgreSql" "$postgres;Database=storefront_commerce"
      secret "$project" "ConnectionStrings:Redis" "$redis"
      secret "$project" "Messaging:Kafka:BootstrapServers" "$kafka"
      secret "$project" "Messaging:RabbitMq:ConnectionString" "$rabbit"
      secret "$project" "PaymentProvider:BaseAddress" "$demopay" ;;
    fulfillment)
      secret "$project" "ConnectionStrings:PostgreSql" "$postgres;Database=storefront_fulfillment"
      secret "$project" "Messaging:RabbitMq:ConnectionString" "$rabbit" ;;
    analytics)
      secret "$project" "ConnectionStrings:PostgreSql" "$timescale;Database=storefront_analytics"
      secret "$project" "Messaging:Kafka:BootstrapServers" "$kafka" ;;
  esac
  echo "   $backend: user secrets written"
done
