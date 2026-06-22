#!/usr/bin/env bash
# Phase J Wave 8 (Apone) — auth-flow smoke test.
#
# Validates the *anonymous* identity round-trip end-to-end against a
# live Docker image, and (forward-compatibly) probes the OAuth surface
# Bishop is shipping in parallel without failing if it's not yet
# registered on this image.
#
# Steps:
#   1. POST /api/identity                       → mahjong_pid cookie minted, JSON {playerId, displayName, …}
#   2. POST /api/identity   with cookie         → same playerId returned (idempotent)
#   3. GET  /api/auth/providers                 → 200 (Bishop's surface) OR 404 (skip)
#   4. GET  /api/auth/me     anonymous          → 200 with isAuthenticated=false (skip if 404)
#
# Inputs:
#   - $IMAGE  → image tag to test (default: build from local Dockerfile)
#   - $PORT   → host port to bind (default: 18081 to avoid collision with
#               docker-build-smoke.sh which uses 18080)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

IMAGE="${IMAGE:-}"
PORT="${PORT:-18081}"
CONTAINER_NAME="mahjong-auth-smoke-$$"
LOG_DIR="$REPO_ROOT/tests/smoke/.run-auth-$$"
mkdir -p "$LOG_DIR"
COOKIE_JAR="$LOG_DIR/cookies.txt"
RESP1="$LOG_DIR/identity1.json"
RESP2="$LOG_DIR/identity2.json"
PROVIDERS_RESP="$LOG_DIR/providers.json"
ME_RESP="$LOG_DIR/me.json"

cleanup() {
    docker stop "$CONTAINER_NAME" >/dev/null 2>&1 || true
    docker rm "$CONTAINER_NAME" >/dev/null 2>&1 || true
    if [[ -z "${IMAGE_PRESET:-}" && -n "${BUILT_IMAGE:-}" ]]; then
        docker rmi "$BUILT_IMAGE" >/dev/null 2>&1 || true
    fi
    rm -rf "$LOG_DIR" 2>/dev/null || true
}
trap cleanup EXIT

if [[ -z "$IMAGE" ]]; then
    IMAGE="mahjong-auth-smoke:$$"
    BUILT_IMAGE="$IMAGE"
    echo "==> [build] no \$IMAGE supplied — building $IMAGE locally"
    docker build -t "$IMAGE" -q . >/dev/null
else
    IMAGE_PRESET=1
    echo "==> [build] using preset image $IMAGE"
fi

echo "==> [start] launching container on port $PORT"
# Production image refuses to boot without Authentication:JwtSigningKeys[0]
# (JwtSigningKeyProvider.cs:121). Mint a per-run HMAC key so the container
# starts and /health responds — mirrors Bishop's #96 fix.
JWT_KEY="$(openssl rand -base64 48)"
docker run -d --name "$CONTAINER_NAME" -p "$PORT:8080" \
    -e ASPNETCORE_ENVIRONMENT=Production \
    -e Authentication__JwtSigningKeys__0="$JWT_KEY" \
    "$IMAGE" >/dev/null

echo "==> [wait] /health"
HEALTH_OK=0
for i in $(seq 1 30); do
    if curl -fsS "http://localhost:$PORT/health" >/dev/null 2>&1; then
        echo "    /health responding after ${i}s"
        HEALTH_OK=1
        break
    fi
    sleep 1
done
if [[ "$HEALTH_OK" -ne 1 ]]; then
    echo "❌ /health did not respond within 30s"
    docker logs "$CONTAINER_NAME" 2>&1 | tail -50
    exit 1
fi

##############################################################################
# 1. POST /api/identity (mint)
##############################################################################
echo "==> [1/4] POST /api/identity (mint anonymous identity)"
http_code=$(curl -fsS -o "$RESP1" -w '%{http_code}' \
    -c "$COOKIE_JAR" \
    -X POST "http://localhost:$PORT/api/identity" \
    -H 'Accept: application/json')
if [[ "$http_code" != "200" ]]; then
    echo "❌ expected 200 from POST /api/identity, got $http_code"
    cat "$RESP1" || true
    exit 1
fi

if ! grep -q "mahjong_pid" "$COOKIE_JAR"; then
    echo "❌ Set-Cookie: mahjong_pid not received"
    cat "$COOKIE_JAR"
    exit 1
fi
echo "    ✅ mahjong_pid cookie set"

PLAYER_ID_1=$(grep -o '"playerId":"[^"]*"' "$RESP1" | head -1 | sed 's/.*:"\(.*\)"/\1/')
if [[ -z "$PLAYER_ID_1" ]]; then
    echo "❌ no playerId in mint response:"
    cat "$RESP1"
    exit 1
fi
echo "    ✅ playerId: $PLAYER_ID_1"

##############################################################################
# 2. POST /api/identity (refresh — idempotent)
##############################################################################
echo "==> [2/4] POST /api/identity (refresh — same playerId expected)"
http_code=$(curl -fsS -o "$RESP2" -w '%{http_code}' \
    -b "$COOKIE_JAR" -c "$COOKIE_JAR" \
    -X POST "http://localhost:$PORT/api/identity" \
    -H 'Accept: application/json')
if [[ "$http_code" != "200" ]]; then
    echo "❌ expected 200 from refresh, got $http_code"
    cat "$RESP2" || true
    exit 1
fi
PLAYER_ID_2=$(grep -o '"playerId":"[^"]*"' "$RESP2" | head -1 | sed 's/.*:"\(.*\)"/\1/')
if [[ "$PLAYER_ID_1" != "$PLAYER_ID_2" ]]; then
    echo "❌ playerId mismatch on refresh: $PLAYER_ID_1 vs $PLAYER_ID_2"
    exit 1
fi
echo "    ✅ idempotent refresh returned same playerId"

##############################################################################
# 3. GET /api/auth/providers (Bishop's surface — forward-compatible probe)
##############################################################################
echo "==> [3/4] GET /api/auth/providers (forward-compat — skip if not yet registered)"
http_code=$(curl -fsS -o "$PROVIDERS_RESP" -w '%{http_code}' \
    -b "$COOKIE_JAR" \
    "http://localhost:$PORT/api/auth/providers" 2>/dev/null || echo "000")
case "$http_code" in
    200)
        echo "    ✅ /api/auth/providers reachable; payload:"
        head -c 200 "$PROVIDERS_RESP"; echo
        ;;
    404)
        echo "    ⏭  /api/auth/providers not yet registered on this image — skipping"
        ;;
    *)
        echo "❌ /api/auth/providers returned $http_code (expected 200 or 404)"
        cat "$PROVIDERS_RESP" || true
        exit 1
        ;;
esac

##############################################################################
# 4. GET /api/auth/me (Bishop's surface — anonymous → isAuthenticated=false)
##############################################################################
echo "==> [4/4] GET /api/auth/me (anonymous; skip if not registered)"
http_code=$(curl -fsS -o "$ME_RESP" -w '%{http_code}' \
    -b "$COOKIE_JAR" \
    "http://localhost:$PORT/api/auth/me" 2>/dev/null || echo "000")
case "$http_code" in
    200)
        if grep -q '"isAuthenticated":false' "$ME_RESP"; then
            echo "    ✅ /api/auth/me reports isAuthenticated=false for anonymous caller"
        else
            echo "❌ /api/auth/me responded 200 but did NOT have isAuthenticated=false:"
            cat "$ME_RESP" || true
            exit 1
        fi
        ;;
    401)
        echo "    ✅ /api/auth/me returned 401 anonymous (alternative valid contract)"
        ;;
    404)
        echo "    ⏭  /api/auth/me not yet registered on this image — skipping"
        ;;
    *)
        echo "❌ /api/auth/me returned $http_code"
        cat "$ME_RESP" || true
        exit 1
        ;;
esac

echo "✅ auth-flow smoke test passed"
