#!/usr/bin/env bash
# Runs one backend on this machine, against the dependencies in Docker.
#
#   scripts/run.sh commerce      REST :5100, gRPC :5101
#   scripts/run.sh fulfillment   gRPC :5201
#   scripts/run.sh analytics     REST :5300
#
# MP Core comes from a clone of its repository next to this one (../mpcore) when there is one, and from
# nuget.org otherwise; see Directory.Build.targets. DOTNET_ARGS passes extra arguments to dotnet run.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
[ $# -ge 1 ] || { echo "usage: $0 <${BACKENDS[*]}>"; exit 2; }
project="$(project_of "$1")"

if [ "$(uname -s)" = "Darwin" ] && [ "$(uname -m)" = "arm64" ] && ! /usr/bin/arch -x86_64 /usr/bin/true 2>/dev/null \
   && ! command -v grpc_csharp_plugin >/dev/null && [ -z "${DOTNET_ARGS:-}" ]; then
  cat >&2 <<'MSG'
This Mac has neither Rosetta nor a native gRPC code generator, so the .proto files cannot be compiled.
Install one of them once, then run this script again:
    softwareupdate --install-rosetta --agree-to-license
or
    brew install protobuf grpc
MSG
  exit 1
fi

export ASPNETCORE_ENVIRONMENT=Development
exec dotnet run --project "$project" ${DOTNET_ARGS:-}
