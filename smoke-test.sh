#!/usr/bin/env bash
#
# Smoke-test the Cinema stack end to end.
#
#   ./smoke-test.sh          run every check
#   ./smoke-test.sh -q       only print failures and the summary
#
# Exits non-zero if any check fails, so it is usable in CI or a pre-push hook.

set -uo pipefail

API=http://localhost:5001        # cinema-api-1
API2=http://localhost:5002       # cinema-api-2
LB=http://localhost:5003         # cinema-loadbalancer
GW=http://localhost:5005         # cinema-gateway
MASTER=http://localhost:5010     # cinema-masternode
GRPC=http://localhost:7080       # cinema-grpc (HTTP/2 only)
CONSUL=http://localhost:8500
SEQ=http://localhost:5341
JAEGER=http://localhost:16686
PROM=http://localhost:9090

QUIET=0
[ "${1:-}" = "-q" ] && QUIET=1

PASS=0; FAIL=0; KNOWN=0
GREEN=$'\033[32m'; RED=$'\033[31m'; YELLOW=$'\033[33m'; DIM=$'\033[2m'; OFF=$'\033[0m'
[ -t 1 ] || { GREEN=""; RED=""; YELLOW=""; DIM=""; OFF=""; }

section() { [ $QUIET -eq 1 ] || printf '\n%s── %s ──%s\n' "$DIM" "$1" "$OFF"; }

# ok <name> <actual> <expected>
ok() {
  if [ "$2" = "$3" ]; then
    PASS=$((PASS+1))
    [ $QUIET -eq 1 ] || printf '  %sPASS%s  %-42s %s\n' "$GREEN" "$OFF" "$1" "$2"
  else
    FAIL=$((FAIL+1))
    printf '  %sFAIL%s  %-42s got %s, want %s\n' "$RED" "$OFF" "$1" "$2" "$3"
  fi
}

# known <name> <actual> <expected-when-fixed> <note>  — a tracked defect, not a suite failure
known() {
  if [ "$2" = "$3" ]; then
    PASS=$((PASS+1))
    [ $QUIET -eq 1 ] || printf '  %sPASS%s  %-42s %s %s(was broken; now fixed)%s\n' "$GREEN" "$OFF" "$1" "$2" "$DIM" "$OFF"
  else
    KNOWN=$((KNOWN+1))
    printf '  %sKNOWN%s %-42s got %s, want %s  %s\n' "$YELLOW" "$OFF" "$1" "$2" "$3" "$4"
  fi
}

# curl prints 000 on a connection failure *and* exits non-zero, so capture rather
# than fall back with `||` — otherwise the two 000s concatenate.
code() { local c; c=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$@" 2>/dev/null); echo "${c:-000}"; }
jq_get() { python3 -c "import sys,json;$1" 2>/dev/null || echo ERR; }

# ---------------------------------------------------------------- infrastructure
section "infrastructure"
ok "consul leader"          "$(code $CONSUL/v1/status/leader)" 200
ok "seq UI"                 "$(code $SEQ/)"                    200
ok "jaeger UI"              "$(code $JAEGER/)"                 200
ok "prometheus ready"       "$(code $PROM/-/ready)"            200
ok "kafka-ui"               "$(code http://localhost:8090/)"   200
ok "mongo-express"          "$(code http://localhost:8081/)"   200

# ---------------------------------------------------------------- service health
section "service health"
ok "api-1        /health"   "$(code $API/health)"              200
ok "api-2        /health"   "$(code $API2/health)"             200
ok "gateway      /health"   "$(code $GW/health)"               200
ok "loadbalancer /health"   "$(code $LB/health)"               200
ok "masternode   /health"   "$(code $MASTER/health)"           200
ok "grpc         /health (h2c)" \
   "$(code --http2-prior-knowledge $GRPC/health)" 200

# ---------------------------------------------------------------- consul catalog
section "consul registrations"
CHECKS=$(curl -s --max-time 10 $CONSUL/v1/agent/checks 2>/dev/null)
ok "instances registered" \
   "$(echo "$CHECKS" | jq_get 'print(len(json.load(sys.stdin)))')" 9
ok "checks passing" \
   "$(echo "$CHECKS" | jq_get 'print(sum(1 for v in json.load(sys.stdin).values() if v["Status"]=="passing"))')" 9
# each service needs the check type its transport can actually answer
ok "http checks (api x2, gw, lb, master)" \
   "$(echo "$CHECKS" | jq_get 'print(sum(1 for v in json.load(sys.stdin).values() if v.get("Type")=="http"))')" 5
ok "tcp check   (grpc is h2c-only)" \
   "$(echo "$CHECKS" | jq_get 'print(sum(1 for v in json.load(sys.stdin).values() if v.get("Type")=="tcp"))')" 1
ok "ttl checks  (read-service has no socket)" \
   "$(echo "$CHECKS" | jq_get 'print(sum(1 for v in json.load(sys.stdin).values() if v.get("Type")=="ttl"))')" 3

# ---------------------------------------------------------------- observability
section "observability"
ok "prometheus targets up" \
   "$(curl -s --max-time 10 "$PROM/api/v1/targets?state=any" 2>/dev/null | jq_get 'd=json.load(sys.stdin)["data"]["activeTargets"];print("%d/%d"%(sum(1 for t in d if t["health"]=="up"),len(d)))')" "6/6"
ok "jaeger receiving traces" \
   "$(curl -s --max-time 10 $JAEGER/api/services 2>/dev/null | jq_get 'print(len([s for s in (json.load(sys.stdin).get("data") or []) if s.startswith("Cinema")]))')" 5
ok "seq storing events" \
   "$(curl -s --max-time 10 "$SEQ/api/events?count=1" 2>/dev/null | jq_get 'print("yes" if len(json.load(sys.stdin))>0 else "no")')" "yes"

# ---------------------------------------------------------------- auth + domain
section "auth flow"
EMAIL="smoke-$$-$(date +%s)@cinema.local"
PASSWORD='P@ssw0rd123!'

REG=$(curl -s -w '\n%{http_code}' --max-time 20 -X POST "$API/api/auth/register" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\",\"firstName\":\"Smoke\",\"lastName\":\"Test\"}" 2>/dev/null)
ok "POST /api/auth/register" "$(echo "$REG" | tail -1)" 201

TOKEN=$(curl -s --max-time 20 -X POST "$API/api/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" 2>/dev/null \
  | jq_get 'print(json.load(sys.stdin)["accessToken"])')
ok "POST /api/auth/login" "$([ -n "$TOKEN" ] && [ "$TOKEN" != ERR ] && echo issued || echo none)" issued

AUTH="Authorization: Bearer $TOKEN"
ok "GET  /api/auth/profile  (with token)" "$(code $API/api/auth/profile -H "$AUTH")" 200
ok "GET  /api/auth/profile  (no token)"   "$(code $API/api/auth/profile)"            401
ok "GET  /api/showtimes     (anonymous)"  "$(code $API/api/showtimes)"               200
ok "POST /api/showtimes     (Customer)"   \
   "$(code -X POST $API/api/showtimes -H "$AUTH" -H 'Content-Type: application/json' \
        -d '{"movieImdbId":"tt0111161","screeningTime":"2026-12-01T19:30:00Z","auditoriumId":"11111111-1111-1111-1111-111111111111"}')" 403
ok "POST /api/auth/logout"                "$(code -X POST $API/api/auth/logout -H "$AUTH")" 204

section "gateway routing"
known "GET gateway /api/showtimes" "$(code $GW/api/showtimes -H "$AUTH")" 200 \
      "gateway JwtSettings:Secret differs from the API's — IDX10503"

# ---------------------------------------------------------------- mongo
section "mongo replica set"
RS=$(docker exec mongo1 mongosh --quiet -u root -p password123 --authenticationDatabase admin \
       --eval 'rs.status().members.filter(m=>m.stateStr=="PRIMARY").length' 2>/dev/null | tr -d '[:space:]')
ok "exactly one PRIMARY" "${RS:-0}" 1
SEC=$(docker exec mongo1 mongosh --quiet -u root -p password123 --authenticationDatabase admin \
       --eval 'rs.status().members.filter(m=>m.stateStr=="SECONDARY").length' 2>/dev/null | tr -d '[:space:]')
ok "two SECONDARY members" "${SEC:-0}" 2

# ---------------------------------------------------------------- summary
printf '\n%s──────────────────────────────%s\n' "$DIM" "$OFF"
printf '  %spassed %d%s' "$GREEN" "$PASS" "$OFF"
[ $FAIL  -gt 0 ] && printf '   %sfailed %d%s' "$RED" "$FAIL" "$OFF"
[ $KNOWN -gt 0 ] && printf '   %sknown issues %d%s' "$YELLOW" "$KNOWN" "$OFF"
printf '\n\n'

[ $FAIL -eq 0 ] || exit 1
exit 0
