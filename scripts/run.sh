#!/usr/bin/env bash
# Runs one backend on this machine, against the dependencies in Docker.
#
#   scripts/run.sh commerce      REST :5100, gRPC :5101
#   scripts/run.sh fulfillment   gRPC :5201
#   scripts/run.sh analytics     REST :5300
#
# MP Core comes from a clone of its repository next to this one (../mpcore) when there is one, and from
# nuget.org otherwise; see Directory.Build.targets. DOTNET_ARGS passes extra arguments to dotnet run.
# STOREFRONT_BIND=0.0.0.0 is for Linux, where the edge cannot reach the loopback address.
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

# The backends listen on the loopback address. The edge runs in a container: Docker Desktop lets it
# reach that address, Docker on Linux does not. There, STOREFRONT_BIND=0.0.0.0 makes a backend listen
# where the container can reach it.
if [ -n "${STOREFRONT_BIND:-}" ]; then
  case "$1" in
    commerce)    export Kestrel__Endpoints__Rest__Url="http://$STOREFRONT_BIND:5100" Kestrel__Endpoints__Grpc__Url="http://$STOREFRONT_BIND:5101" ;;
    fulfillment) export Kestrel__Endpoints__Grpc__Url="http://$STOREFRONT_BIND:5201" ;;
    analytics)   export Kestrel__Endpoints__Rest__Url="http://$STOREFRONT_BIND:5300" ;;
  esac
fi

# The three backends share MP Core's projects when it is built from source, and two builds at the same
# moment write the same files. So builds take turns; running does not. A turn that was never given back,
# because its build was killed, is taken over after fifteen minutes.
turn="${TMPDIR:-/tmp}/storefront-build.turn"
until mkdir "$turn" 2>/dev/null; do
  [ -n "$(find "$turn" -maxdepth 0 -mmin +15 2>/dev/null)" ] && rmdir "$turn" 2>/dev/null
  sleep 1
done
trap 'rmdir "$turn" 2>/dev/null' EXIT
dotnet build "$project" ${DOTNET_ARGS:-}
rmdir "$turn" 2>/dev/null; trap - EXIT

exec dotnet run --project "$project" --no-build ${DOTNET_ARGS:-}
