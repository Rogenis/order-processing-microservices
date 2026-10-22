#!/usr/bin/env bash
# Fluxo completo contra o ambiente do docker compose: um pedido confirmado, um rejeitado
# por falta de estoque e uma repetição idempotente.
set -euo pipefail

API="${API:-http://localhost:8080}"
KEY="${ORDERS_API_KEY:-local-dev-api-key-change-me}"

post() { # $1 = idempotency key, $2 = body
  curl -s -X POST "$API/orders" -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
       -H "Idempotency-Key: $1" -d "$2" -w '\nHTTP %{http_code}\n'
}
status() { curl -s "$API/orders/$1" -H "X-Api-Key: $KEY"; echo; }
id_of() { sed -n 's/.*"id":"\([^"]*\)".*/\1/p' <<<"$1" | head -1; }

echo "== 1. Pedido com estoque disponível"
r1=$(post "demo-order-0001-$RANDOM" '{"customerId":"maria","items":[{"sku":"KEYCHAIN-01","quantity":2,"unitPrice":15.5}]}')
echo "$r1"

echo "== 2. Pedido acima do estoque (LAMP-04 tem 3 unidades)"
r2=$(post "demo-order-0002-$RANDOM" '{"customerId":"joao","items":[{"sku":"LAMP-04","quantity":5,"unitPrice":199}]}')
echo "$r2"

echo "== 3. Repetição com a mesma Idempotency-Key (mesmo pedido, HTTP 200)"
key="demo-order-0003-$RANDOM"
body='{"customerId":"ana","items":[{"sku":"PLANTER-02","quantity":1,"unitPrice":89.9}]}'
post "$key" "$body" | tail -1
post "$key" "$body" | tail -1

echo "== Aguardando o worker processar a fila..."
sleep 4
echo "== Status final"
status "$(id_of "$r1")"
status "$(id_of "$r2")"
