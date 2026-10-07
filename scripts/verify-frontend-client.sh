#!/usr/bin/env bash
# Source-only integration gate: never prints identities, credentials or realm contents.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
realm="${1:-$repo/infrastructure/keycloak/storefront-realm.json}"
jq -e '
  [.clients[] | select(.clientId == "storefront-web")] as $matches |
  ($matches | length) == 1 and
  ($matches[0] | .publicClient == true and .standardFlowEnabled == true and
    .serviceAccountsEnabled == false and .attributes["pkce.code.challenge.method"] == "S256" and
    .redirectUris == ["http://localhost:4401/api/session/callback", "http://localhost:4402/api/session/callback"] and
    .webOrigins == ["http://localhost:4401", "http://localhost:4402"])
' "$realm" >/dev/null
echo 'Storefront frontend client: exact callbacks and S256 verified'
