#!/usr/bin/env bash
# Storefront: every business scenario, end to end, against the running backends.
#
# Each scenario is a story from docs/business.md, told through real HTTP and gRPC calls with real
# tokens from Keycloak. Every step states what it expects; the script exits non-zero
# if any expectation failed, so it is a demo and an end-to-end test at the same time.
#
#   scripts/scenarios.sh            # all scenarios
#   scripts/scenarios.sh S1 S5      # only these
#
# Needs: the Commerce backend running (scripts/run.sh commerce), curl and jq. S18 also needs the Fulfillment
# backend, S19 the Analytics backend, and S20 and S21 all three and the edge. grpcurl for the gRPC scenarios and docker for a look inside Kafka
# and PostgreSQL are optional. A step whose tool or backend is missing is skipped, not failed.
set -uo pipefail

source "$(dirname "$0")/lib.sh"
set +e
BASE_URL="${BASE_URL:-http://localhost:5100}"
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:$(env_value KEYCLOAK_PORT 48180)}"
WIREMOCK_URL="${WIREMOCK_URL:-http://localhost:$(env_value WIREMOCK_PORT 48081)}"
GRPC_ADDR="${GRPC_ADDR:-localhost:5101}"
FULFILLMENT_ADDR="${FULFILLMENT_ADDR:-localhost:5201}"
ANALYTICS_URL="${ANALYTICS_URL:-http://localhost:5300}"
RABBITMQ_API="${RABBITMQ_API:-http://localhost:$(env_value RABBITMQ_UI_PORT 45673)/api}"
EDGE_ADDR="${EDGE_ADDR:-localhost:$(env_value GATEWAY_HTTPS_PORT 49443)}"
EDGE_URL="https://$EDGE_ADDR"
# The edge's certificate was made for this machine and is trusted by nobody, so every call names it.
EDGE_CA="${EDGE_CA:-$GATEWAY_CA}"
ca=(); case "$BASE_URL" in https://*) ca=(--cacert "$EDGE_CA") ;; esac
psql_in() { # psql_in SERVICE DATABASE SQL  → runs a read-only query in a dependency's container
  compose exec -T "$1" psql -U "$(env_value POSTGRES_USER storefront)" -d "$2" -Atc "$3"
}
# A message from RabbitMQ that is given up goes to the broker's own dead-letter queue, not to a table.
rabbit_dead() { # prints how many messages wait there; nothing when the management API does not answer
  curl -s -m 5 -u "$(env_value RABBITMQ_USER storefront):$(env_value RABBITMQ_PASSWORD storefront)" "$RABBITMQ_API/queues" \
    | jq -r '[.[] | select(.name == "wolverine-dead-letter-queue") | .messages] | add // 0' 2>/dev/null
}
# The edge is there when it answers a request that needs a token with 401, whoever of the two says it.
edge_is_up() { [ -s "$EDGE_CA" ] && [ "$(curl -s -m 5 --cacert "$EDGE_CA" -o /dev/null -w '%{http_code}' "$EDGE_URL/v1/basket")" = 401 ]; }
docker_up() { command -v docker >/dev/null && compose ps "$1" --status running -q 2>/dev/null | grep -q .; }
WAIT_SECONDS="${WAIT_SECONDS:-30}"
# Only when the API runs inside the compose network: the issuer it expects is http://keycloak:8080/...,
# so tokens are requested with that Host header. Leave unset when the API runs on this machine.
KEYCLOAK_ISSUER_HOST="${KEYCLOAK_ISSUER_HOST:-}"
kc_host=(); [ -n "$KEYCLOAK_ISSUER_HOST" ] && kc_host=(-H "Host: $KEYCLOAK_ISSUER_HOST")

# ------------------------------------------------------------------------------------ presentation
if [ -t 1 ]; then B=$'\e[1m'; G=$'\e[32m'; R=$'\e[31m'; Y=$'\e[33m'; D=$'\e[2m'; N=$'\e[0m'; else B=; G=; R=; Y=; D=; N=; fi
passed=0; failed=0; skipped=0
section() { printf '\n%s━━ %s%s\n' "$B" "$*" "$N"; }
say()     { printf '   %s\n' "$*"; }
show()    { jq -C "${1:-.}" <<<"$LAST" 2>/dev/null | sed 's/^/      /' || printf '      %s\n' "$LAST"; }
ok()      { passed=$((passed + 1)); printf '   %s✔%s %s\n' "$G" "$N" "$*"; }
bad()     { failed=$((failed + 1)); printf '   %s✘ %s%s\n' "$R" "$*" "$N"; }
skip()    { skipped=$((skipped + 1)); printf '   %s∅ skipped: %s%s\n' "$Y" "$*" "$N"; }
expect()  { local want="$1"; shift; if [ "$STATUS" = "$want" ]; then ok "$* → HTTP $STATUS"; else bad "$* → HTTP $STATUS, expected $want"; show; fi; }
check()   { local actual="$1" want="$2"; shift 2; if [ "$actual" = "$want" ]; then ok "$* = $actual"; else bad "$* = $actual, expected $want"; fi; }

# ------------------------------------------------------------------------------------ transport
LAST=""; STATUS=""; REPLAYED=""
api() { # api METHOD PATH [TOKEN] [JSON]  → sets STATUS, LAST and REPLAYED ("true" when the answer is a stored one)
  local method="$1" path="$2" token="${3:-}" body="${4:-}" out headers
  out=$(mktemp); headers=$(mktemp)
  local args=(-s -o "$out" -D "$headers" -w '%{http_code}' -X "$method" "$BASE_URL$path" -H 'Accept: application/json')
  case "$BASE_URL" in https://*) args+=(--cacert "$EDGE_CA") ;; esac
  [ -n "${EXTRA_HEADER:-}" ] && args+=(-H "$EXTRA_HEADER")
  [ -n "${LANG_HEADER:-}" ] && args+=(-H "Accept-Language: $LANG_HEADER")
  [ -n "${IDEMPOTENCY_KEY:-}" ] && args+=(-H "Idempotency-Key: $IDEMPOTENCY_KEY")
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' --data "$body")
  STATUS=$(curl "${args[@]}")
  LAST=$(cat "$out")
  REPLAYED=$(tr -d '\r' <"$headers" | awk -F': ' 'tolower($1) == "idempotency-replayed" { print tolower($2) }')
  REQUEST_ID=$(tr -d '\r' <"$headers" | awk -F': ' 'tolower($1) == "x-request-id" { print $2 }')
  SERVED_BY=$(tr -d '\r' <"$headers" | awk -F': ' 'tolower($1) == "server" { print $2 }')
  # A backend answers a failure as Problem Details (RFC 9457). What answers anything else is not a backend.
  ANSWERED_BY=$(tr -d '\r' <"$headers" | awk -F': ' 'tolower($1) == "content-type" { print ($2 ~ /^application\/problem\+json/) ? "backend" : "other" }')
  rm -f "$out" "$headers"
}

new_key() { uuidgen | tr 'A-Z' 'a-z'; } # an Idempotency-Key: any unique text will do, a UUID is the usual choice

token_for() { # a shopper, manager or agent signs in at the storefront client (password grant: for a demonstration only)
  curl -s -X POST ${kc_host[@]+"${kc_host[@]}"} "$KEYCLOAK_URL/realms/storefront/protocol/openid-connect/token" \
    -d grant_type=password -d client_id=storefront-web -d "username=$1" -d "password=$1-lab" | jq -r '.access_token // empty'
}

warehouse_token() { # the warehouse system authenticates as itself: client credentials
  curl -s -X POST ${kc_host[@]+"${kc_host[@]}"} "$KEYCLOAK_URL/realms/storefront/protocol/openid-connect/token" \
    -d grant_type=client_credentials -d client_id=storefront-warehouse -d client_secret=lab-only-warehouse-secret | jq -r '.access_token // empty'
}

address() { # address RECIPIENT CITY
  jq -nc --arg r "$1" --arg c "$2" '{recipientName:$r, phone:"+14155550123", province:$c, city:$c, line:"12 Harbour Street", postalCode:"94103"}'
}

calc() { jq -n "$1"; }                                        # calc '485.00 - 25'  → 460: money has cents, the shell has integers
same_amount() { [ "$(jq -n --argjson a "${1:-null}" --argjson b "${2:-null}" '$a == $b' 2>/dev/null)" = true ]; }

# The browser hands the provider's token to Payments first and checks out with the reference it gets
# back, so the token never travels further (rule P5). Prints the payment intent's identity.
new_intent() { # new_intent TOKEN PAYMENT_TOKEN
  local trust=(); case "$BASE_URL" in https://*) trust=(--cacert "$EDGE_CA") ;; esac
  curl -s ${trust[@]+"${trust[@]}"} -X POST "$BASE_URL/v1/payments/intents" -H "Authorization: Bearer $1" -H 'Content-Type: application/json' \
    --data "$(jq -nc --arg t "$2" '{paymentToken:$t}')" | jq -r '.paymentIntentId // empty'
}

checkout_body() { # checkout_body PAYMENT_INTENT_ID EXPECTED_TOTAL RECIPIENT CITY
  jq -nc --argjson a "$(address "$3" "$4")" --arg i "$1" --argjson e "$2" '{shippingAddress:$a, paymentIntentId:$i, expectedTotal:$e}'
}

# The endpoint requires an Idempotency-Key. Every call here is a new checkout, so every call sends a new
# key; S14 sends the same key twice on purpose.
checkout() { # checkout TOKEN PAYMENT_TOKEN EXPECTED_TOTAL RECIPIENT CITY  → sets ORDER_ID and INTENT_ID
  INTENT_ID=$(new_intent "$1" "$2")
  IDEMPOTENCY_KEY="$(new_key)" api POST /v1/basket/checkout "$1" "$(checkout_body "$INTENT_ID" "$3" "$4" "$5")"
  ORDER_ID=$(jq -r '.orderId // empty' <<<"$LAST")
}

fill_basket() { # fill_basket TOKEN SKU QTY [SKU QTY ...]  → sets BASKET_TOTAL
  local token="$1"; shift
  empty_basket "$token"
  while [ $# -gt 0 ]; do api PUT "/v1/basket/items/$1" "$token" "{\"quantity\":$2}"; shift 2; done
  BASKET_TOTAL=$(jq -r '.total' <<<"$LAST")
}

empty_basket() { # removes every line from the caller's basket
  api GET /v1/basket "$1"
  for sku in $(jq -r '.lines[].sku' <<<"$LAST"); do api DELETE "/v1/basket/items/$sku" "$1"; done
}

wait_for() { # wait_for ORDER_ID TOKEN STATUS...  → polls until the order reaches one of STATUS (404 at first: it is being created)
  local id="$1" token="$2"; shift 2
  local deadline=$((SECONDS + WAIT_SECONDS)) status=""
  while [ $SECONDS -lt $deadline ]; do
    api GET "/v1/orders/$id" "$token"
    status=$(jq -r '.status // empty' <<<"$LAST")
    for want in "$@"; do [ "$status" = "$want" ] && { ORDER_STATUS="$status"; return 0; }; done
    sleep 0.5
  done
  ORDER_STATUS="$status"; return 1
}

history() { jq -r '.history[] | "      \(.occurredOnUtc[11:23])  \(.status)\(if .note then "  (\(.note))" else "" end)"' <<<"$LAST"; }

stock_of() { api GET "/v1/catalog/products/$1/stock" "$MINA"; }

wiremock_calls_for() { # how many authorization calls DemoPay received for an order
  curl -s "$WIREMOCK_URL/__admin/requests" | jq --arg id "$1" '[.requests[] | select(.request.url == "/demo-pay/v1/authorizations") | select(.request.body | contains($id))] | length'
}

selected() { [ ${#ONLY[@]} -eq 0 ] && return 0; for s in "${ONLY[@]}"; do [ "$s" = "$1" ] && return 0; done; return 1; }
ONLY=("$@")

# ------------------------------------------------------------------------------------ preflight
section "Preflight"
command -v jq >/dev/null || { echo "jq is required"; exit 2; }
command -v uuidgen >/dev/null || { echo "uuidgen is required"; exit 2; }
api GET /health/ready; expect 200 "the API is ready at $BASE_URL"
[ "$STATUS" = 200 ] || { echo "Start the backend first: scripts/run.sh commerce"; exit 2; }
SARA=$(token_for sara); REZA=$(token_for reza); MINA=$(token_for mina); ALI=$(token_for ali); WAREHOUSE=$(warehouse_token)
[ -n "$SARA" ] && [ -n "$MINA" ] && [ -n "$ALI" ] && [ -n "$WAREHOUSE" ] && ok "tokens issued by Keycloak for sara, reza, mina, ali and the warehouse" \
  || { bad "could not get tokens from $KEYCLOAK_URL. Did scripts/up.sh run?"; exit 2; }
say "${D}sara's token carries: $(cut -d. -f2 <<<"$SARA" | tr '_-' '/+' | base64 -d 2>/dev/null | jq -c '{sub, preferred_username, aud, roles: .realm_access.roles}' 2>/dev/null)${N}"
for u in sara reza; do empty_basket "$(token_for $u)"; done

# ------------------------------------------------------------------------------------ S0
if selected S0; then
section "S0  Anybody can browse the storefront — no token"
api GET "/v1/catalog/products?category=tents&sort=price&desc=true"
expect 200 "anonymous browse, tents, most expensive first"
show '{total, items: [.items[] | {sku, name, price, inStock}]}'
api GET /v1/catalog/products/TNT-ALV-2P
expect 200 "product page (served through MP Core's hybrid read-through cache)"
show '{sku, name, price, currency, available, priceVersion}'
api GET "/v1/catalog/products?sort=cost_price"
expect 400 "sorting by an unpublished field is refused by MP Core's SortAllowlist"
show '{title, errorCode, violations}'
fi

# ------------------------------------------------------------------------------------ S1
if selected S1; then
section "S1  Sara buys a tent and a stove: the happy path"
fill_basket "$SARA" TNT-ALV-2P 1 CKG-SHL-STV 2
show '{lines: [.lines[] | {sku, unitPrice, quantity, lineTotal}], total}'
checkout "$SARA" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Lisbon
expect 202 "checkout accepted: the basket emptied and said so — one transaction, one module"
show '{orderId, total, currency}'
HAPPY_ORDER="$ORDER_ID"
api GET /v1/basket "$SARA"; check "$(jq '.lines | length' <<<"$LAST")" 0 "sara's basket after checkout: lines"
say "Ordering creates the order from BasketCheckedOut; Payments, the Catalog and Payments again answer in turn"
wait_for "$ORDER_ID" "$SARA" Paid && ok "the order process ran through the local queues: Paid" || bad "order stuck at $ORDER_STATUS"
show '{orderNumber, status, total}'
say "history (charge registered → Catalog reserved → Payments charged DemoPay → Ordering marked it paid):"; history
fi

# ------------------------------------------------------------------------------------ S2
if selected S2; then
section "S2  The price changes while Reza is shopping"
fill_basket "$REZA" TNT-ALV-2P 1
OLD_TOTAL="$BASKET_TOTAL"; say "reza's basket: $OLD_TOTAL USD"
stock_of TNT-ALV-2P; current=$(jq -r '.price' <<<"$LAST")
new=$(calc "if $current >= 485 then $current - 25.50 else $current + 25.50 end")
api PUT /v1/catalog/products/TNT-ALV-2P/price "$MINA" "{\"newPrice\":$new,\"reason\":\"supplier price list 2026-Q4\"}"
expect 200 "mina (catalog-manager) reprices TNT-ALV-2P: $current → $new"
say "ProductPriceChanged travels Catalog → outbox → Kafka → Basket's consumer; waiting for reza's basket…"
deadline=$((SECONDS + WAIT_SECONDS)); seen=""
while [ $SECONDS -lt $deadline ]; do
  api GET /v1/basket "$REZA"
  same_amount "$(jq -r '.lines[0].unitPrice' <<<"$LAST")" "$new" && { seen=1; break; }; sleep 0.5
done
[ -n "$seen" ] && ok "reza's basket repriced from Kafka" || bad "price change never reached the basket"
show '{pricesChanged, lines: [.lines[] | {sku, previousUnitPrice, unitPrice}], total}'
NEW_TOTAL=$(jq -r '.total' <<<"$LAST")
api GET /v1/basket "$REZA"
check "$(jq -r '.pricesChanged' <<<"$LAST")" true "reading the basket again still marks the change (a GET changes nothing): pricesChanged"
checkout "$REZA" tok_visa_ok "$OLD_TOTAL" "Reza Karimi" Toronto
expect 422 "checkout with the total reza saw before the change is refused"
show '{errorCode, title}'
api GET /v1/basket "$REZA"
check "$(jq '.lines | length' <<<"$LAST")" 1 "reza's basket after the refused checkout (MP Core rolled the taken basket back): lines"
api POST /v1/basket/acknowledge-prices "$REZA"
expect 200 "reza has seen the new price and says so"
check "$(jq -r '.pricesChanged' <<<"$LAST")" false "the basket no longer marks the price: pricesChanged"
checkout "$REZA" tok_visa_ok "$NEW_TOTAL" "Reza Karimi" Toronto
expect 202 "checkout with the confirmed new total"
fi

# ------------------------------------------------------------------------------------ S3
if selected S3; then
section "S3  A fat-finger repricing is refused, and the refusal is audited"
stock_of TNT-ALV-2P; current=$(jq -r '.price' <<<"$LAST"); typo=$(calc "$current * 10")
api PUT /v1/catalog/products/TNT-ALV-2P/price "$MINA" "{\"newPrice\":$typo,\"reason\":\"oops\"}"
expect 422 "mina types $typo instead of $current"
show '{errorCode, title, violations}'
api PUT /v1/catalog/products/TNT-ALV-2P/price "$SARA" "{\"newPrice\":10,\"reason\":\"why not\"}"
expect 403 "sara (customer) may not reprice anything"
api GET "/v1/backoffice/audit?entityType=Product&entityId=TNT-ALV-2P&size=5" "$ALI"
expect 200 "ali (support) reads the product's audit trail"
show '[.items[] | {occurredAtUtc, action, outcome, actor: .actor.userName, changes, metadata}]'
fi

# ------------------------------------------------------------------------------------ S4
if selected S4; then
section "S4  Out of stock: the order is cancelled, nothing is charged"
fill_basket "$SARA" CLT-DRY-JKT 5
checkout "$SARA" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Nairobi
expect 202 "sara orders 5 shell jackets (the warehouse has 3)"
wait_for "$ORDER_ID" "$SARA" Cancelled && ok "cancelled by the process" || bad "order at $ORDER_STATUS"
check "$(jq -r '.cancellationReason' <<<"$LAST")" OUT_OF_STOCK "cancellation reason"
history
fi

# ------------------------------------------------------------------------------------ S5
if selected S5; then
section "S5  The card is declined: the order is cancelled and the stock goes back"
stock_of SLP-SAB-M5; before=$(jq -r '.available' <<<"$LAST")
fill_basket "$REZA" SLP-SAB-M5 1
checkout "$REZA" tok_insufficient_funds "$BASKET_TOTAL" "Reza Karimi" Osaka
expect 202 "reza pays with a card that has no money"
wait_for "$ORDER_ID" "$REZA" Cancelled && ok "cancelled by the process" || bad "order at $ORDER_STATUS"
check "$(jq -r '.cancellationReason' <<<"$LAST")" PAYMENT_DECLINED "cancellation reason"
history
sleep 1; stock_of SLP-SAB-M5; check "$(jq -r '.available' <<<"$LAST")" "$before" "available sleeping bags after the release"
fi

# ------------------------------------------------------------------------------------ S6
if selected S6; then
section "S6  DemoPay hiccups once: MP Core's resilient HTTP client retries it"
curl -s -X POST "$WIREMOCK_URL/__admin/scenarios/reset" >/dev/null
fill_basket "$SARA" BPK-DNA-28 1
checkout "$SARA" tok_flaky "$BASKET_TOTAL" "Sara Ahmadi" Lima
expect 202 "sara pays; the provider answers 503 to the first attempt"
wait_for "$ORDER_ID" "$SARA" Paid && ok "paid anyway" || bad "order at $ORDER_STATUS"
check "$(wiremock_calls_for "$ORDER_ID")" 2 "authorization calls DemoPay received (503, then 201 — same Idempotency-Key)"
fi

# ------------------------------------------------------------------------------------ S7
if selected S7; then
section "S7  DemoPay is down: the order waits honestly, then the shopper cancels"
fill_basket "$REZA" CKG-TTN-POT 1
checkout "$REZA" tok_psp_down "$BASKET_TOTAL" "Reza Karimi" Oslo
expect 202 "reza pays while the provider is down"
PSP_ORDER="$ORDER_ID"
wait_for "$ORDER_ID" "$REZA" AwaitingPayment && ok "stock held, awaiting payment" || bad "order at $ORDER_STATUS"
sleep 6
api GET "/v1/orders/$ORDER_ID" "$REZA"
check "$(jq -r '.status' <<<"$LAST")" AwaitingPayment "status while Wolverine retries AuthorizePayment with a cooldown"
say "DemoPay has seen $(wiremock_calls_for "$ORDER_ID") authorization attempts so far (HTTP retries × message retries)"
api POST "/v1/orders/$ORDER_ID/cancel" "$REZA" '{"note":"took too long"}'
expect 200 "reza gives up and cancels"
check "$(jq -r '.cancellationReason' <<<"$LAST")" CUSTOMER_REQUEST "cancellation reason"
say "compensations sent: ReleaseStock → Catalog, VoidPayment → Payments. The next retry finds the payment voided and stops."
fi

# ------------------------------------------------------------------------------------ S8
if selected S8; then
section "S8  The last pairs of boots: purchasing gets a restock alert"
stock_of FTW-ALM-42; available=$(jq -r '.available' <<<"$LAST"); threshold=$(jq -r '.reorderThreshold' <<<"$LAST")
if [ "$available" -le "$threshold" ]; then
  api POST /v1/catalog/products/FTW-ALM-42/restock "$MINA" "{\"quantity\":$((threshold - available + 1)),\"reference\":\"PO-DEMO-$RANDOM\"}"
  expect 200 "mina restocks the boots above the threshold first"
fi
api GET "/v1/catalog/restock-alerts?size=100" "$MINA"; alerts_before=$(jq -r '.total' <<<"$LAST")
fill_basket "$SARA" FTW-ALM-42 1
checkout "$SARA" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Porto
expect 202 "sara buys a pair; available stock crosses the reorder threshold"
wait_for "$ORDER_ID" "$SARA" Paid >/dev/null
# A domain event is delivered after the reservation commits, on its own queue: poll, do not assume.
deadline=$((SECONDS + WAIT_SECONDS))
until [ $SECONDS -ge $deadline ]; do
  api GET "/v1/catalog/restock-alerts?size=1" "$MINA"; [ "$(jq -r '.total' <<<"$LAST")" -gt "$alerts_before" ] && break; sleep 0.5
done
check "$(jq -r '.total' <<<"$LAST")" $((alerts_before + 1)) "restock alerts (a domain event, handled after commit)"
show '.items[0]'
fi

# ------------------------------------------------------------------------------------ S9
if selected S9; then
section "S9  The warehouse ships over gRPC"
if ! command -v grpcurl >/dev/null; then
  skip "grpcurl is not installed (brew install grpcurl)"
elif [ -z "${HAPPY_ORDER:-}" ]; then
  skip "run S1 first: it produces the order to ship"
else
  say "the warehouse lists paid orders (client-credentials token, role 'warehouse'):"
  grpcurl -plaintext -H "authorization: Bearer $WAREHOUSE" -d '{"page":1,"size":5}' "$GRPC_ADDR" storefront.commerce.v1.Fulfillment/ListOrdersToShip \
    | jq -C '{total, orders: [.orders[] | {order_number, total, item_count}]}' | sed 's/^/      /'
  stock_of TNT-ALV-2P; on_hand=$(jq -r '.onHand' <<<"$LAST")
  reply=$(grpcurl -plaintext -H "authorization: Bearer $WAREHOUSE" \
    -d "{\"order_id\":\"$HAPPY_ORDER\",\"carrier\":\"Parcelway\",\"tracking_code\":\"PCW-$RANDOM$RANDOM\"}" \
    "$GRPC_ADDR" storefront.commerce.v1.Fulfillment/ShipOrder 2>&1)
  check "$(jq -r '.status' <<<"$reply" 2>/dev/null)" Shipped "ShipOrder over gRPC"
  sleep 1; stock_of TNT-ALV-2P; check "$(jq -r '.onHand' <<<"$LAST")" $((on_hand - 1)) "tents on hand after the parcel left (CommitStock)"
  say "a customer token on the warehouse service:"
  grpcurl -plaintext -H "authorization: Bearer $SARA" -d '{}' "$GRPC_ADDR" storefront.commerce.v1.Fulfillment/ListOrdersToShip 2>&1 | sed 's/^/      /' | head -4
fi
fi

# ------------------------------------------------------------------------------------ S10
if selected S10; then
section "S10 Sara cancels a paid order: the money comes back"
fill_basket "$SARA" CLT-MRN-BAS 2
checkout "$SARA" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Seoul
expect 202 "sara buys two base layers"
wait_for "$ORDER_ID" "$SARA" Paid >/dev/null && ok "paid"
api POST "/v1/orders/$ORDER_ID/cancel" "$SARA" '{"note":"wrong size"}'
expect 200 "sara cancels the paid order"
deadline=$((SECONDS + WAIT_SECONDS))
until [ $SECONDS -ge $deadline ]; do
  api GET "/v1/orders/$ORDER_ID" "$SARA"; jq -e '.history[] | select(.status == "Refunded")' <<<"$LAST" >/dev/null && break; sleep 0.5
done
jq -e '.history[] | select(.status == "Refunded")' <<<"$LAST" >/dev/null && ok "refund recorded in the order history" || bad "no refund recorded"
history
fi

# ------------------------------------------------------------------------------------ S11
if selected S11; then
section "S11 Who may see what"
api GET /v1/basket; expect 401 "a basket without a token"
api GET /v1/backoffice/orders "$SARA"; expect 403 "sara (customer) in the back office"
if [ -n "${HAPPY_ORDER:-}" ]; then
  api GET "/v1/orders/$HAPPY_ORDER" "$REZA"; expect 404 "reza reads sara's order — not found, never 'forbidden'"
  api GET "/v1/orders/$HAPPY_ORDER" "$ALI"; expect 200 "ali (support) reads sara's order"
fi
api GET "/v1/backoffice/orders?status=Cancelled&size=5" "$ALI"; expect 200 "ali lists cancelled orders"
show '{total, orders: [.items[] | {orderNumber, status, total}]}'
api POST /v1/payments/intents "$SARA" '{"paymentToken":"4111111111111111"}'
expect 400 "a card number instead of a provider token is refused before anything is stored"
show '{errorCode, violations}'
IDEMPOTENCY_KEY="$(new_key)" api POST /v1/basket/checkout "$SARA" '{"shippingAddress":null,"expectedTotal":1}'
expect 400 "a checkout with no address and no payment intent is refused before the basket is touched"
fi

# ------------------------------------------------------------------------------------ S12
if selected S12; then
section "S12 The integration events on Kafka, keyed for ordering"
if docker_up kafka; then
  for topic in storefront.ordering.order-paid.v1 storefront.catalog.product-price-changed.v1; do
    say "$topic (key → value):"
    compose exec -T kafka /opt/kafka/bin/kafka-console-consumer.sh \
      --bootstrap-server localhost:9092 --topic "$topic" --from-beginning --timeout-ms 5000 --max-messages 2 \
      --property print.key=true --property key.separator=' → ' 2>/dev/null | cut -c1-220 | sed 's/^/      /'
  done
  ok "open the Kafka browser to see every topic"
else
  skip "docker compose is not reachable from here"
fi
fi

# ------------------------------------------------------------------------------------ S13
if selected S13; then
section "S13 Ali translates a message; Reza reads it in Persian"
# Failures leave the modules as message keys. MP Core's catalog renders them in the language the caller
# asked for, from the modules' resource files, and support can override a text at run time; the change is
# stored in this host's own database and reaches every instance within the refresher's interval.
ALI13=$(token_for ali); REZA13=$(token_for reza); MINA13=$(token_for mina)
NEW_TEXT="قیمت سبد شما تغییر کرد؛ لطفاً پیش از پرداخت دوباره نگاه کنید."
api PUT /v1/backoffice/translations/fa/basket.total_changed "$ALI13" "$(jq -nc --arg t "$NEW_TEXT" '{text:$t}')"
expect 200 "ali stores a Persian text for basket.total_changed"
api PUT /v1/backoffice/translations/fa/ordering.not_a_real_key "$ALI13" '{"text":"x"}'
expect 400 "a key the code does not use is refused"
show '{errorCode, violations}'
api PUT /v1/backoffice/translations/fa/basket.total_changed "$REZA13" '{"text":"x"}'
expect 403 "reza (customer) may not translate anything"
say "waiting for the refresher to pick the change up"; sleep 3

fill_basket "$REZA13" BPK-DNA-28 1; OLD13="$BASKET_TOTAL"
api GET /v1/catalog/products/BPK-DNA-28/stock "$MINA13"; current13=$(jq -r '.price' <<<"$LAST")
api PUT /v1/catalog/products/BPK-DNA-28/price "$MINA13" "$(jq -nc --argjson p "$(calc "($current13 * 105 | round) / 100")" '{newPrice:$p, reason:"S13 supplier price rise"}')"
expect 200 "mina raises the day pack's price by 5%"
LANG_HEADER=fa checkout "$REZA13" tok_visa_ok "$OLD13" "Reza Karimi" Osaka
expect 422 "reza checks out with the old total and asks for Persian"
check "$(jq -r '.detail // empty' <<<"$LAST")" "$NEW_TEXT" "the detail is ali's text"
LANG_HEADER=en checkout "$REZA13" tok_visa_ok "$OLD13" "Reza Karimi" Osaka
expect 422 "the same failure in English comes from the module's resource file"
show '{errorCode, detail}'

# A business rule with arguments, rendered in Persian: the price ceiling rule of the Catalog aggregate.
LANG_HEADER=fa api PUT /v1/catalog/products/BPK-DNA-28/price "$MINA13" '{"newPrice":1,"reason":"typo"}'
expect 422 "a price move of more than half breaks rule C7"
case "$(jq -r '.detail // empty' <<<"$LAST")" in *حداکثر*) ok "the rule's message is rendered in Persian with its arguments" ;; *) bad "no Persian detail"; show ;; esac
show '{errorDomain, errorCode, detail}'

api DELETE /v1/backoffice/translations/fa/basket.total_changed "$ALI13"
expect 204 "ali removes the override; the resource file's text applies again"
api DELETE /v1/basket/items/BPK-DNA-28 "$REZA13"; expect 200 "reza empties his basket"
api DELETE /v1/basket/items/BPK-DNA-28 "$REZA13"; expect 200 "removing it again is idempotent"
fi

# ------------------------------------------------------------------------------------ S14
if selected S14; then
section "S14 Twice is once: a retried checkout, a repeated delivery note"
# A shopper whose connection dropped does not know whether the checkout arrived, and retries. The
# Idempotency-Key (IETF HTTPAPI draft, made common by Stripe) lets the API answer the retry with the first
# answer instead of running it again. MP Core stores the key and the answer in the transaction that empties
# the basket, so there is no moment at which one exists without the other (ADR-013).
fill_basket "$SARA" CKG-SHL-STV 1
INTENT14=$(new_intent "$SARA" tok_visa_ok)
BODY14=$(checkout_body "$INTENT14" "$BASKET_TOTAL" "Sara Ahmadi" Lisbon)
KEY14=$(new_key)
api POST /v1/basket/checkout "$SARA" "$BODY14"
expect 400 "checkout without an Idempotency-Key"
show '{errorDomain, errorCode, violations}'
api GET /v1/basket "$SARA"; check "$(jq '.lines | length' <<<"$LAST")" 1 "sara's basket after the refused checkout: lines"
IDEMPOTENCY_KEY="$KEY14" api POST /v1/basket/checkout "$SARA" "$BODY14"
expect 202 "checkout with key ${KEY14%%-*}…"
FIRST14=$(jq -r '.orderId // empty' <<<"$LAST"); check "${REPLAYED:-false}" false "the first answer is a replay"
IDEMPOTENCY_KEY="$KEY14" api POST /v1/basket/checkout "$SARA" "$BODY14"
expect 202 "the same request with the same key, although the basket is empty by now"
check "$(jq -r '.orderId // empty' <<<"$LAST")" "$FIRST14" "the order it names"
check "${REPLAYED:-false}" true "Idempotency-Replayed"
IDEMPOTENCY_KEY="$KEY14" api POST /v1/basket/checkout "$SARA" "$(checkout_body "$INTENT14" "$BASKET_TOTAL" "Sara Ahmadi" Perth)"
expect 422 "the same key with another request (another city)"
show '{errorDomain, errorCode}'
wait_for "$FIRST14" "$SARA" Paid && ok "one order, paid once" || bad "order at $ORDER_STATUS"
check "$(wiremock_calls_for "$FIRST14")" 1 "authorization calls DemoPay received for it"

# Restocking is protected by what the business already has: the delivery note. It names a delivery once,
# for as long as the receipt exists, with or without an Idempotency-Key (rule C14).
stock_of BPK-DNA-28; on_hand14=$(jq -r '.onHand' <<<"$LAST"); NOTE14="DN-S14-$RANDOM$RANDOM"
api POST /v1/catalog/products/BPK-DNA-28/restock "$MINA" "{\"quantity\":4,\"reference\":\"$NOTE14\"}"
expect 200 "mina receives delivery note $NOTE14: 4 day packs"
check "$(jq -r '.onHand' <<<"$LAST")" $((on_hand14 + 4)) "day packs on hand"
api POST /v1/catalog/products/BPK-DNA-28/restock "$MINA" "{\"quantity\":4,\"reference\":\" $(tr 'A-Z' 'a-z' <<<"$NOTE14") \"}"
expect 200 "the same delivery note again, typed in lower case"
check "$(jq -r '.onHand' <<<"$LAST")" $((on_hand14 + 4)) "day packs on hand (added once)"
api POST /v1/catalog/products/BPK-DNA-28/restock "$MINA" "{\"quantity\":5,\"reference\":\"$NOTE14\"}"
expect 422 "the same delivery note with another quantity is a contradiction, not a repeat"
show '{errorDomain, errorCode, detail}'
KEY14=$(new_key); NOTE14="DN-S14-$RANDOM$RANDOM"
IDEMPOTENCY_KEY="$KEY14" api POST /v1/catalog/products/BPK-DNA-28/restock "$MINA" "{\"quantity\":1,\"reference\":\"$NOTE14\"}"
expect 200 "another delivery, this time with an Idempotency-Key"
IDEMPOTENCY_KEY="$KEY14" api POST /v1/catalog/products/BPK-DNA-28/restock "$MINA" "{\"quantity\":1,\"reference\":\"$NOTE14\"}"
expect 200 "the same request with the same key"
check "${REPLAYED:-false}" true "Idempotency-Replayed"
stock_of BPK-DNA-28; check "$(jq -r '.onHand' <<<"$LAST")" $((on_hand14 + 5)) "day packs on hand after both deliveries"
fi

# ------------------------------------------------------------------------------------ S15
if selected S15; then
section "S15 Eight checkouts of one basket at the same moment: one order, one charge"
# A double click, two tabs, an impatient app: the same basket is checked out several times at once, each
# time as a request of its own, with its own Idempotency-Key. Every attempt reads the basket, empties it
# and publishes BasketCheckedOut; the database lets one of them commit. The others fail at the save, are
# retried, find the basket empty and are refused. What they had published must go with them: the outbox
# releases a message only with the change that caused it (MP Core's HandlerAttemptMiddleware).
REZA15=$(token_for reza)
api GET "/v1/orders?size=1" "$REZA15"; orders15=$(jq -r '.total' <<<"$LAST")
charges15=$(curl -s "$WIREMOCK_URL/__admin/requests" | jq '[.requests[] | select(.request.url == "/demo-pay/v1/authorizations")] | length')
fill_basket "$REZA15" CKG-TTN-POT 1
BODY15=$(checkout_body "$(new_intent "$REZA15" tok_visa_ok)" "$BASKET_TOTAL" "Reza Karimi" Dublin)
race15=$(mktemp -d)
for n in 1 2 3 4 5 6 7 8; do
  ( curl -s -o "$race15/$n.body" -w '%{http_code}' -X POST "$BASE_URL/v1/basket/checkout" -H "Authorization: Bearer $REZA15" \
      -H "Idempotency-Key: $(new_key)" -H 'Content-Type: application/json' --data "$BODY15" > "$race15/$n.status" ) &
done
wait
check "$(cat "$race15"/*.status | grep -c 202)" 1 "checkouts accepted"
check "$(cat "$race15"/*.body | jq -r '.errorCode // empty' | sort -u | tr '\n' ' ')" "BASKET_EMPTY " "what the other seven were told"
ORDER_ID=$(cat "$race15"/*.body | jq -r '.orderId // empty'); rm -rf "$race15"
wait_for "$ORDER_ID" "$REZA15" Paid && ok "the accepted checkout became an order: Paid" || bad "order at $ORDER_STATUS"
sleep 3 # time for a message that should not exist to do its damage
api GET "/v1/orders?size=1" "$REZA15"; check "$(jq -r '.total' <<<"$LAST")" $((orders15 + 1)) "reza's orders"
check "$(curl -s "$WIREMOCK_URL/__admin/requests" | jq '[.requests[] | select(.request.url == "/demo-pay/v1/authorizations")] | length')" $((charges15 + 1)) "authorizations DemoPay has received"
api GET /v1/basket "$REZA15"; check "$(jq '.lines | length' <<<"$LAST")" 0 "reza's basket: lines"
fi

# ------------------------------------------------------------------------------------ S16
if selected S16; then
section "S16 A product everybody wants at once: every order comes to an end"
# Twelve orders for the same product within a few seconds. Each ReserveStock changes the same product row,
# so they collide (optimistic concurrency) and are retried, with a growing pause and some randomness so
# that the losers do not collide again. An order whose reservation is given up all the same is cancelled
# and its charge voided: no order is left "submitted" with nobody told.
rush16=$(mktemp -d)
for who in sara reza; do
  ( token=$(token_for "$who")
    for n in 1 2 3 4 5 6; do
      fill_basket "$token" BPK-DNA-28 1
      checkout "$token" tok_visa_ok "$BASKET_TOTAL" "Rush $who" Lisbon
      [ "$STATUS" = 202 ] && echo "$ORDER_ID" >> "$rush16/$who"
    done ) &
done
wait
check "$(cat "$rush16"/* 2>/dev/null | wc -l | tr -d ' ')" 12 "checkouts accepted"
deadline=$((SECONDS + WAIT_SECONDS)); open16=12
while [ $SECONDS -lt $deadline ] && [ "$open16" -gt 0 ]; do
  open16=0
  for id in $(cat "$rush16"/*); do
    api GET "/v1/orders/$id" "$ALI"
    case "$(jq -r '.status // empty' <<<"$LAST")" in Paid|Cancelled) ;; *) open16=$((open16 + 1)) ;; esac
  done
  [ "$open16" -gt 0 ] && sleep 1
done
check "$open16" 0 "orders still waiting after ${WAIT_SECONDS}s"
paid16=0; for id in $(cat "$rush16"/*); do api GET "/v1/orders/$id" "$ALI"; [ "$(jq -r '.status' <<<"$LAST")" = Paid ] && paid16=$((paid16 + 1)); done
say "paid: $paid16 of 12; the others were cancelled and told so"
rm -rf "$rush16"
fi

# ------------------------------------------------------------------------------------ S17
if selected S17; then
section "S17 The card token stays with Payments"
# The provider's token is handed to Payments once, as a payment intent, and checkout carries only the
# intent's identity. So no message between modules holds the token, and neither do the queue tables.
# An intent pays for one order, of its own shopper.
SARA17=$(token_for sara); REZA17=$(token_for reza)
fill_basket "$SARA17" CKG-SHL-STV 1
checkout "$SARA17" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Lisbon
expect 202 "sara checks out with a payment intent"
USED17="$INTENT_ID"
wait_for "$ORDER_ID" "$SARA17" Paid && ok "paid through the intent" || bad "order at $ORDER_STATUS"
fill_basket "$SARA17" CKG-SHL-STV 1
IDEMPOTENCY_KEY="$(new_key)" api POST /v1/basket/checkout "$SARA17" "$(checkout_body "$USED17" "$BASKET_TOTAL" "Sara Ahmadi" Lisbon)"
expect 422 "the same intent a second time: it has paid for an order already"
show '{errorDomain, errorCode}'
REZAS17=$(new_intent "$REZA17" tok_visa_ok)
IDEMPOTENCY_KEY="$(new_key)" api POST /v1/basket/checkout "$SARA17" "$(checkout_body "$REZAS17" "$BASKET_TOTAL" "Sara Ahmadi" Lisbon)"
expect 422 "reza's intent in sara's checkout"
api GET /v1/basket "$SARA17"; check "$(jq '.lines | length' <<<"$LAST")" 1 "sara's basket after both refusals: lines"
empty_basket "$SARA17"
if docker_up postgres; then
  in_queues=$(psql_in postgres storefront_commerce \
    "select (select count(*) from wolverine.wolverine_incoming_envelopes where position('tok_'::bytea in body) > 0)
          + (select count(*) from wolverine.wolverine_outgoing_envelopes where position('tok_'::bytea in body) > 0)
          + (select count(*) from wolverine.wolverine_dead_letters where position('tok_'::bytea in body) > 0)")
  check "$in_queues" 0 "messages in the queue tables that hold a card token"
else
  skip "docker compose is not reachable from here, so the queue tables cannot be read"
fi
fi

# ------------------------------------------------------------------------------------ S18
if selected S18; then
section "S18 Another service ships the order: RabbitMQ there, gRPC in, RabbitMQ back"
warehouse() { # warehouse TOKEN METHOD JSON  → sets REPLY; the warehouse service speaks gRPC only
  REPLY=$(grpcurl -plaintext -max-time 10 -H "authorization: Bearer $1" -d "$3" "$FULFILLMENT_ADDR" "storefront.fulfillment.v1.Shipments/$2" 2>&1)
}
if ! command -v grpcurl >/dev/null; then
  skip "grpcurl is not installed (brew install grpcurl)"
elif ! grpcurl -plaintext -max-time 3 "$FULFILLMENT_ADDR" grpc.health.v1.Health/Check 2>/dev/null | grep -q SERVING; then
  skip "the Fulfillment backend is not running (scripts/run.sh fulfillment)"
else
  SARA18=$(token_for sara); WAREHOUSE18=$(warehouse_token)
  dead18=$(rabbit_dead)
  stock_of CKG-TTN-POT; on_hand18=$(jq -r '.onHand' <<<"$LAST")
  fill_basket "$SARA18" CKG-TTN-POT 1
  checkout "$SARA18" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Vienna
  expect 202 "sara buys a titanium pot"
  wait_for "$ORDER_ID" "$SARA18" Paid >/dev/null && ok "paid" || bad "order at $ORDER_STATUS"
  say "OrderReadyToShip travels Ordering → outbox → RabbitMQ → the warehouse's inbox; waiting for the shipment…"
  deadline=$((SECONDS + WAIT_SECONDS)); status18=""
  while [ $SECONDS -lt $deadline ]; do
    warehouse "$WAREHOUSE18" GetShipment "{\"order_id\":\"$ORDER_ID\"}"
    status18=$(jq -r '.status // empty' <<<"$REPLY" 2>/dev/null); [ "$status18" = Pending ] && break; sleep 0.5
  done
  check "$status18" Pending "the shipment the warehouse holds for sara's order"
  jq -C '{order_number, status, city: .ship_to.city, lines: [.lines[] | {sku, quantity}]}' <<<"$REPLY" 2>/dev/null | sed 's/^/      /'
  check "$(jq -r 'has("total") or has("payment_reference")' <<<"$REPLY" 2>/dev/null)" false "the warehouse was told about money"

  warehouse "$SARA18" ListShipments '{"page":1,"size":5}'
  # Keycloak addresses sara's token to the shop and to the figures (the "aud" claim), not to the warehouse.
  grep -q Unauthenticated <<<"$REPLY" && ok "a token issued for the shop, shown to the warehouse → Unauthenticated" || bad "a token issued for the shop, shown to the warehouse → $REPLY"

  warehouse "$WAREHOUSE18" DispatchShipment "{\"order_id\":\"$ORDER_ID\",\"carrier\":\"Parcelway\",\"tracking_code\":\"PCW-18-$RANDOM\"}"
  check "$(jq -r '.status // empty' <<<"$REPLY" 2>/dev/null)" Dispatched "DispatchShipment over gRPC"
  TRACKING18=$(jq -r '.tracking_code // empty' <<<"$REPLY" 2>/dev/null)
  say "ShipmentDispatched travels the warehouse → outbox → RabbitMQ → Commerce's inbox; waiting for the order…"
  wait_for "$ORDER_ID" "$SARA18" Shipped >/dev/null && ok "sara's order is Shipped" || bad "order at $ORDER_STATUS"
  check "$(jq -r '.trackingCode // empty' <<<"$LAST")" "$TRACKING18" "the tracking code sara sees"
  history

  warehouse "$WAREHOUSE18" DispatchShipment "{\"order_id\":\"$ORDER_ID\",\"carrier\":\"Parcelway\",\"tracking_code\":\"PCW-AGAIN\"}"
  grep -q FailedPrecondition <<<"$REPLY" && ok "the same parcel a second time → FailedPrecondition" || bad "the same parcel a second time → $REPLY"
  sed 's/^/      /' <<<"$REPLY" | head -4
  api GET "/v1/orders/$ORDER_ID" "$SARA18"; check "$(jq -r '.trackingCode // empty' <<<"$LAST")" "$TRACKING18" "the tracking code after the refused repeat"
  deadline=$((SECONDS + WAIT_SECONDS))
  while [ $SECONDS -lt $deadline ]; do stock_of CKG-TTN-POT; [ "$(jq -r '.onHand' <<<"$LAST")" = "$((on_hand18 - 1))" ] && break; sleep 0.5; done
  check "$(jq -r '.onHand' <<<"$LAST")" $((on_hand18 - 1)) "pots on hand after the parcel left"

  say "an order can also leave by Commerce's own gRPC door (S9). The warehouse service then reports it as well:"
  fill_basket "$SARA18" CKG-TTN-POT 1
  checkout "$SARA18" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Vienna
  wait_for "$ORDER_ID" "$SARA18" Paid >/dev/null && ok "a second order, paid" || bad "order at $ORDER_STATUS"
  reply=$(grpcurl -plaintext -max-time 10 -H "authorization: Bearer $WAREHOUSE18" \
    -d "{\"order_id\":\"$ORDER_ID\",\"carrier\":\"Counter\",\"tracking_code\":\"CTR-18\"}" "$GRPC_ADDR" storefront.commerce.v1.Fulfillment/ShipOrder 2>&1)
  check "$(jq -r '.status // empty' <<<"$reply" 2>/dev/null)" Shipped "shipped at Commerce's door"
  deadline=$((SECONDS + WAIT_SECONDS)); status18=""
  while [ $SECONDS -lt $deadline ]; do
    warehouse "$WAREHOUSE18" GetShipment "{\"order_id\":\"$ORDER_ID\"}"
    status18=$(jq -r '.status // empty' <<<"$REPLY" 2>/dev/null); [ "$status18" = Pending ] && break; sleep 0.5
  done
  warehouse "$WAREHOUSE18" DispatchShipment "{\"order_id\":\"$ORDER_ID\",\"carrier\":\"Parcelway\",\"tracking_code\":\"PCW-LATE\"}"
  check "$(jq -r '.status // empty' <<<"$REPLY" 2>/dev/null)" Dispatched "the warehouse service reports the same order"
  sleep 5 # long enough for the message to arrive and, were it refused, to be given up
  api GET "/v1/orders/$ORDER_ID" "$SARA18"
  check "$(jq -r '.status + " " + .trackingCode' <<<"$LAST")" "Shipped CTR-18" "the order, after news that said nothing new"
  if [ -n "$dead18" ]; then
    check "$(rabbit_dead)" "$dead18" "messages in RabbitMQ's dead-letter queue, as before this scenario"
  else
    skip "RabbitMQ's management API does not answer at $RABBITMQ_API"
  fi
fi
fi

# ------------------------------------------------------------------------------------ S19
if selected S19; then
section "S19 The figures: three Kafka topics into a hypertable, read over REST"
figures() { # figures PATH [TOKEN]  → sets STATUS and LAST, from the Analytics backend
  BASE_URL="$ANALYTICS_URL" api GET "$1" "${2:-}"
}
placed_in() { jq -r --arg c "$1" '[.rows[] | select(.city == $c) | .placedOrders] | add // 0' <<<"$LAST"; }
paid_now() { jq -r '[.rows[].paidOrders] | add // 0' <<<"$LAST"; }
if [ "$(curl -s -m 3 -o /dev/null -w '%{http_code}' "$ANALYTICS_URL/health/ready")" != 200 ]; then
  skip "the Analytics backend is not running (scripts/run.sh analytics)"
else
  NORA=$(token_for nora); SARA19=$(token_for sara)
  [ -n "$NORA" ] && ok "token issued for nora (analyst)" || bad "no token for nora"
  figures /v1/analytics/sales/by-region "$NORA"; expect 200 "nora reads the orders per city"
  placed19=$(placed_in Reykjavik)
  figures /v1/analytics/sales/hourly "$NORA"; expect 200 "nora reads the paid orders per hour"
  paid19=$(paid_now)

  fill_basket "$SARA19" CKG-SHL-STV 2
  checkout "$SARA19" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Reykjavik
  expect 202 "sara buys two stoves, to be delivered in Reykjavik"
  wait_for "$ORDER_ID" "$SARA19" Paid >/dev/null && ok "paid" || bad "order at $ORDER_STATUS"
  say "OrderPlaced and OrderPaid travel Ordering → outbox → Kafka → the figures' inbox; waiting for the report…"
  deadline=$((SECONDS + WAIT_SECONDS))
  while [ $SECONDS -lt $deadline ]; do
    figures /v1/analytics/sales/by-region "$NORA"; [ "$(placed_in Reykjavik)" = "$((placed19 + 1))" ] && break; sleep 0.5
  done
  check "$(placed_in Reykjavik)" $((placed19 + 1)) "orders placed for Reykjavik"
  show '{from, to, rows: [.rows[] | select(.city == "Reykjavik")]}'
  deadline=$((SECONDS + WAIT_SECONDS))
  while [ $SECONDS -lt $deadline ]; do
    figures /v1/analytics/sales/hourly "$NORA"; [ "$(paid_now)" -ge "$((paid19 + 1))" ] && break; sleep 0.5
  done
  [ "$(paid_now)" -ge "$((paid19 + 1))" ] && ok "paid orders in the last day: $paid19 → $(paid_now)" || bad "paid orders in the last day stayed at $(paid_now)"
  show '{rows: [.rows[-2:][] | {hour, currency, paidOrders, revenue}]}'

  figures /v1/analytics/sales/hourly "$SARA19"; expect 403 "sara (customer) asks for the figures"
  figures /v1/analytics/sales/hourly; expect 401 "the figures without a token"
  LANG_HEADER=fa figures "/v1/analytics/sales/hourly?from=2026-01-01T00:00:00Z&to=2026-06-01T00:00:00Z" "$NORA"
  expect 400 "a period too long for an hourly report, asked in Persian"
  show '{title, detail, violations}'
  if docker_up timescale; then
    check "$(psql_in timescale storefront_analytics "select count(*) from timescaledb_information.hypertables where hypertable_schema = 'analytics' and hypertable_name = 'order_facts'")" 1 "order_facts is a hypertable"
    check "$(psql_in timescale storefront_analytics "select count(*) from wolverine.wolverine_dead_letters")" 0 "messages the figures gave up on"
  fi
fi
fi

# ------------------------------------------------------------------------------------ S20
if selected S20; then
section "S20 Through the edge: Apache APISIX in front of the three backends"
edge_grpc() { # edge_grpc BACKEND PROTO TOKEN METHOD JSON  → sets REPLY; the edge does not offer reflection, so the caller brings the contract
  local auth=(); [ -n "$3" ] && auth=(-H "authorization: Bearer $3")
  REPLY=$(grpcurl -cacert "$EDGE_CA" -max-time 10 -import-path "$REPO_ROOT/$1/src/Storefront.$(tr '[:lower:]' '[:upper:]' <<<"${1:0:1}")${1:1}.Api/Protos" -proto "$2" \
    ${auth[@]+"${auth[@]}"} -d "$5" "$EDGE_ADDR" "$4" 2>&1)
}
direct_url="$BASE_URL"
if ! edge_is_up; then
  skip "the edge does not answer at $EDGE_URL (scripts/up.sh starts it; on Linux run the backends with STOREFRONT_BIND=0.0.0.0)"
else
  BASE_URL="$EDGE_URL"
  SARA20=$(token_for sara); NORA20=$(token_for nora); WAREHOUSE20=$(warehouse_token)

  api GET "/v1/catalog/products?size=1"
  expect 200 "anybody browses the catalog over TLS, through the edge"
  case "$SERVED_BY" in APISIX*) ok "answered by $SERVED_BY" ;; *) bad "answered by '$SERVED_BY', expected APISIX" ;; esac

  api GET /v1/basket
  expect 401 "a basket without a token, through the edge"
  api GET /v1/backoffice/orders "$SARA20"
  expect 403 "sara in the back office: the backend decides what a caller may do, not the edge"
  [ -n "$REQUEST_ID" ] && check "$(jq -r '.requestId' <<<"$LAST")" "$REQUEST_ID" "the request's identity in the backend's answer, as the edge gave it" \
    || bad "the edge gave the request no identity"

  api GET /openapi/v1.json
  if [ "$STATUS" = 200 ]; then
    check "$(jq -r '.servers[0].url' <<<"$LAST")" "$EDGE_URL/" "the address the backend believes it is reached at (X-Forwarded-Proto and -Host, from a trusted proxy)"
  else
    skip "the API description is off (it is served in Development only)"
  fi

  api GET /v1/basket "$SARA20"; expect 200 "sara reads her basket through the edge"
  EXTRA_HEADER="X-Forwarded-User: ali" api GET /v1/backoffice/orders "$SARA20"
  expect 403 "sara in the back office, with a header that says she is ali"

  BASE_URL="$EDGE_URL" api GET /v1/analytics/sales/hourly "$NORA20"
  expect 200 "nora reads the figures through the same door: another backend"

  fill_basket "$SARA20" CKG-SHL-STV 1
  checkout "$SARA20" tok_visa_ok "$BASKET_TOTAL" "Sara Ahmadi" Geneva
  expect 202 "sara buys a stove through the edge"
  wait_for "$ORDER_ID" "$SARA20" Paid >/dev/null && ok "paid" || bad "order at $ORDER_STATUS"

  if ! command -v grpcurl >/dev/null; then
    skip "grpcurl is not installed (brew install grpcurl)"
  else
    edge_grpc fulfillment storefront_fulfillment.proto "$WAREHOUSE20" storefront.fulfillment.v1.Shipments/GetShipment "{\"order_id\":\"$ORDER_ID\"}"
    deadline=$((SECONDS + WAIT_SECONDS))
    until [ "$(jq -r '.status // empty' <<<"$REPLY" 2>/dev/null)" = Pending ] || [ $SECONDS -ge $deadline ]; do
      sleep 0.5; edge_grpc fulfillment storefront_fulfillment.proto "$WAREHOUSE20" storefront.fulfillment.v1.Shipments/GetShipment "{\"order_id\":\"$ORDER_ID\"}"
    done
    check "$(jq -r '.status // empty' <<<"$REPLY" 2>/dev/null)" Pending "the warehouse's shipment, asked over gRPC through the edge"
    edge_grpc commerce storefront_commerce.proto "$WAREHOUSE20" storefront.commerce.v1.Fulfillment/ListOrdersToShip '{"page":1,"size":1}'
    jq -e '.orders | length >= 1' <<<"$REPLY" >/dev/null 2>&1 && ok "Commerce's gRPC service through the same door" || bad "Commerce's gRPC service through the edge → $REPLY"
    edge_grpc fulfillment storefront_fulfillment.proto "" storefront.fulfillment.v1.Shipments/ListShipments '{}'
    grep -q Unauthenticated <<<"$REPLY" && ok "gRPC without a token → Unauthenticated" || bad "gRPC without a token → $REPLY"
  fi

  say "anybody may browse, so anybody can be asked to slow down: fifty requests at once"
  limited=0
  for _ in $(seq 1 50); do
    [ "$(curl -s -m 5 --cacert "$EDGE_CA" -o /dev/null -w '%{http_code}' "$EDGE_URL/v1/catalog/products?size=1")" = 429 ] && limited=$((limited + 1))
  done
  [ "$limited" -gt 0 ] && ok "the edge answered 429 to $limited of 50" || bad "the edge limited nothing"
  BASE_URL="$direct_url" api GET "/v1/catalog/products?size=1"
  expect 200 "the backend itself, asked directly at the same moment"
  empty_basket "$SARA20"
fi
BASE_URL="$direct_url"
fi

# ------------------------------------------------------------------------------------ S21
if selected S21; then
section "S21 Two walls: the edge verifies a token, and the backend verifies it again"
direct_url="$BASE_URL"
forged() { # forged TOKEN → the same token, with roles its holder was never given; the signature no longer fits
  python3 - "$1" <<'FORGE'
import base64, json, sys
head, payload, signature = sys.argv[1].split(".")
claims = json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))
claims["realm_access"] = {"roles": ["support-agent", "catalog-manager", "customer"]}
payload = base64.urlsafe_b64encode(json.dumps(claims, separators=(",", ":")).encode()).decode().rstrip("=")
print(".".join([head, payload, signature]))
FORGE
}
edge_answers() { [ "$ANSWERED_BY" != backend ] && ok "$* → HTTP $STATUS, answered by the edge" || bad "$* → answered by the backend: the edge let it through"; }
backend_answers() { [ "$ANSWERED_BY" = backend ] && ok "$* → HTTP $STATUS, answered by the backend" || bad "$* → HTTP $STATUS, not answered by the backend"; }
if ! edge_is_up; then
  skip "the edge does not answer at $EDGE_URL"
else
  BASE_URL="$EDGE_URL"
  api GET /v1/basket
  if [ "$STATUS" = 401 ] && [ "$ANSWERED_BY" = backend ]; then
    skip "the edge does not verify tokens: EDGE_AUTH is off (docs/variations.md). The backends verify, as S11 and S20 show"
  else
    SARA21=$(token_for sara); WAREHOUSE21=$(warehouse_token)
    expect 401 "a basket without a token"; edge_answers "no token"
    api GET /v1/basket "not-a-token"
    expect 401 "a token that is no token"; edge_answers "it never reached a backend"
    api GET /v1/backoffice/orders "$(forged "$SARA21")"
    expect 401 "sara's token, rewritten to make her a support agent"; edge_answers "the signature no longer fits"
    api GET /v1/backoffice/orders "$SARA21"
    expect 403 "sara's real token, in the back office"; backend_answers "the edge let her in; her role did not"
    api GET /v1/analytics/sales/hourly "$WAREHOUSE21"
    expect 401 "the warehouse's token at the figures: signed by Keycloak, issued for other backends"
    backend_answers "the edge cannot know for whom a token was issued; the backend does"
    api GET /v1/basket "$SARA21"
    expect 200 "sara's real token, her basket"
    # S20 may have used up what one address may browse in ten seconds; the window is waited out.
    deadline=$((SECONDS + 15))
    until api GET "/v1/catalog/products?size=1"; [ "$STATUS" != 429 ] || [ $SECONDS -ge $deadline ]; do sleep 1; done
    expect 200 "anybody still browses the catalog without a token"
    BASE_URL="$direct_url" api GET /v1/backoffice/orders "$(forged "$SARA21")"
    expect 401 "the rewritten token, shown to the backend directly, past the edge"; backend_answers "the second wall stands alone"
  fi
fi
BASE_URL="$direct_url"
fi

# ------------------------------------------------------------------------------------ summary
section "Summary"
printf '   %s%d passed%s, %s%d failed%s, %d skipped\n' "$G" "$passed" "$N" "$([ $failed -gt 0 ] && echo "$R")" "$failed" "$N" "$skipped"
cat <<INFO
   Look behind the scenes:
     traces     http://localhost:$(env_value JAEGER_UI_PORT 46686)  (one trace crosses the three services: HTTP → handler → SQL → broker → consumer)
     events     http://localhost:$(env_value KAFKA_UI_PORT 48080)  (Kafka topics storefront.*)
     queues     http://localhost:$(env_value RABBITMQ_UI_PORT 45673)  (RabbitMQ queues storefront.*)
     edge       $EDGE_URL  (Apache APISIX; curl --cacert infrastructure/apisix/generated/localhost.crt)
     metrics    http://localhost:$(env_value GRAFANA_PORT 43000)  (dashboard "Storefront Commerce")
     payments   $WIREMOCK_URL/__admin/requests
INFO
[ "$failed" -eq 0 ]
