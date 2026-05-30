#!/usr/bin/env bash
# End-to-end smoke test for the Attrition microservices stack (through the YARP gateway on :8080).
# Run AFTER `docker compose up`. Exits non-zero on first hard failure.
set -uo pipefail
GW="${GATEWAY_URL:-http://localhost:8080}"
pass=0; fail=0
ok()   { echo "  PASS: $1"; pass=$((pass+1)); }
bad()  { echo "  FAIL: $1"; fail=$((fail+1)); }

echo "== 1. Gateway health =="
curl -fsS "$GW/health" >/dev/null && ok "gateway /health" || bad "gateway /health"

echo "== 2. Register a user (Identity) =="
U="user_$RANDOM"
REG=$(curl -fsS -X POST "$GW/api/auth/register" -H 'Content-Type: application/json' \
  -d "{\"username\":\"$U\",\"password\":\"Passw0rd!23\",\"email\":\"$U@example.com\"}")
echo "$REG" | grep -q '"success":true' && ok "register" || bad "register ($REG)"
TOKEN=$(echo "$REG" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')

echo "== 3. /api/auth/me with token (cross-cutting auth) =="
curl -fsS "$GW/api/auth/me" -H "Authorization: Bearer $TOKEN" | grep -q '"success":true' \
  && ok "auth/me" || bad "auth/me"

echo "== 4. Public enemy list (Enemy, no auth) =="
curl -fsS "$GW/api/enemies" | grep -q '"success":true' && ok "GET enemies" || bad "GET enemies"

echo "== 5. Create enemy as non-admin -> expect 403 (cross-service role check) =="
CODE=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$GW/api/enemies" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"enemyId":"e1","name":"Test","tier":"common","hp":10,"ad":1,"ap":0,"def":0,"res":0,"attackSpeed":1,"isRanged":false,"expReward":1,"goldReward":1,"lootTable":[]}')
[ "$CODE" = "403" ] && ok "non-admin blocked from enemy create (403)" || bad "expected 403, got $CODE"

echo "== 6. Wiki + Forum public reads =="
curl -fsS "$GW/api/wiki/categories" | grep -q '"success":true' && ok "wiki categories" || bad "wiki categories"
curl -fsS "$GW/api/forum/categories" | grep -q '"success":true' && ok "forum categories" || bad "forum categories"

echo "== 7. Global search fan-out (Search aggregator) =="
curl -fsS "$GW/api/search?q=test" | grep -q '"success":true' && ok "search" || bad "search"

echo "== 8. Admin stats without admin role -> expect 403 =="
CODE=$(curl -s -o /dev/null -w '%{http_code}' "$GW/api/admin/stats" -H "Authorization: Bearer $TOKEN")
[ "$CODE" = "403" ] && ok "non-admin blocked from admin stats (403)" || bad "expected 403, got $CODE"

echo ""
echo "== RESULT: $pass passed, $fail failed =="
[ "$fail" -eq 0 ]
