#!/usr/bin/env bash
# Builds and tests every backend against PACKED MP Core packages instead of MP Core source. Local mode
# never builds a .nupkg, so it cannot catch a packaging defect; this does.
#
#   scripts/verify-against-packages.sh              # every backend
#   scripts/verify-against-packages.sh commerce     # one
#
# MP Core is packed from ../mpcore with a throwaway version (<version>-verify.<timestamp>) into a temporary
# folder, and the backends restore it into an isolated NuGet cache (NUGET_PACKAGES). Nothing reaches your
# global NuGet cache or a feed, so no real version is spent. Set PROTOC_PROPS to extra -p: arguments if
# your machine needs a protoc override.
set -euo pipefail
source "$(dirname "$0")/lib.sh"
MPCORE="${MPCORE_REPOSITORY_ROOT:-$(cd "$REPO_ROOT/../mpcore" && pwd)}"
backends=("$@"); [ ${#backends[@]} -gt 0 ] || backends=("${BACKENDS[@]}")

work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT
suffix="verify.$(date -u +%Y%m%d%H%M%S)"
prefix="$(sed -n 's/.*<VersionPrefix>\(.*\)<\/VersionPrefix>.*/\1/p' "$MPCORE/src/Directory.Build.props" | head -1)"
version="$prefix-$suffix"
extra=(); [ -n "${PROTOC_PROPS:-}" ] && read -r -a extra <<<"$PROTOC_PROPS"

echo "== packing MP Core from $MPCORE as $version"
dotnet pack "$MPCORE/MPCore.sln" -c Release -o "$work/packages" -p:VersionSuffix="$suffix" ${extra[@]+"${extra[@]}"} -v quiet
echo "   $(ls "$work/packages" | grep -c '\.nupkg$') packages"

cat > "$work/NuGet.Config" <<CFG
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear />
    <add key="mpcore-verify" value="$work/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="mpcore-verify"><package pattern="MPCore.*" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <fallbackPackageFolders><add key="host-cache" value="$HOME/.nuget/packages" /></fallbackPackageFolders>
</configuration>
CFG

export NUGET_PACKAGES="$work/nuget"
props=(-p:MPCoreSource=NuGet -p:MPCoreVersion="$version")
for backend in "${backends[@]}"; do
  sln="$(ls "$REPO_ROOT/$backend"/*.sln | head -1)"
  echo "== $backend: restoring against the packages"
  dotnet restore "$sln" --configfile "$work/NuGet.Config" "${props[@]}" -v quiet
  echo "== $backend: building (warnings are errors)"
  dotnet build "$sln" -c Release --no-restore "${props[@]}" ${extra[@]+"${extra[@]}"} -v quiet
  echo "== $backend: testing"
  dotnet test "$sln" -c Release --no-build "${props[@]}" -v quiet
done
if [ $# -eq 0 ]; then
  contracts="$REPO_ROOT/tests/Storefront.Contracts.Tests/Storefront.Contracts.Tests.csproj"
  echo "== contracts between the services: testing"
  dotnet restore "$contracts" --configfile "$work/NuGet.Config" "${props[@]}" -v quiet
  dotnet build "$contracts" -c Release --no-restore "${props[@]}" ${extra[@]+"${extra[@]}"} -v quiet
  dotnet test "$contracts" -c Release --no-build "${props[@]}" -v quiet
fi
echo "== OK: ${backends[*]} build and pass their tests against packed MP Core $version"
echo "   Restore afterwards with: dotnet restore <solution>"
