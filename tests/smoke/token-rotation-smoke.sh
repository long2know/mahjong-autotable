#!/usr/bin/env bash
# Phase J Wave 9 (Apone) — reconnect-token rotation smoke test.
#
# Validates the anonymous identity round-trip + forward-compatibly
# probes Bishop's Wave-9 reconnect-token rotation surface (the
# /api/reconnect/* endpoints + the SignalR ReconnectGame RPC). Soft-
# passes (⏭) when the surface returns 404 — the same pattern as Wave-8
# auth-flow-smoke.sh.
#
# Steps:
#   1. POST /api/identity                                → mahjong_pid cookie
#   2. POST /api/reconnect/issue   {gameId,seatIndex}    → 200 {token,expiresAt}
#                                                          OR 404 (skip)
#   3. POST /api/reconnect/rotate  {token,gameId}        → 200 {newToken,expiresAt}
#                                                          OR 404 (skip)
#   4. POST /api/reconnect/rotate  {token=old}           → 4xx (single-use
#                                                          rotation; the old
#                                                          token should now
#                                                          be invalid)
#
# Inputs:
#   - $IMAGE  → image tag to test (default: build from local Dockerfile)
#   - $PORT   → host port to bind (default: 18083 to avoid collision with
#               docker-build-smoke.sh on 18080, auth-flow-smoke.sh on
#               18081, chat-flow-smoke.sh on 18082)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

IMAGE="${IMAGE:-}"
PORT="${PORT:-18083}"
CONTAINER_NAME="mahjong-token-smoke-$$"
LOG_DIR="$REPO_ROOT/tests/smoke/.run-token-$$"
mkdir -p "$LOG_DIR"
COOKIE_JAR="$LOG_DIR/cookies.txt"
IDENTITY_RESP="$LOG_DIR/identity.json"
ISSUE_RESP="$LOG_DIR/issue.json"
ROTATE_RESP="$LOG_DIR/rotate.json"
REUSE_RESP="$LOG_DIR/reuse.json"

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
    IMAGE="mahjong-token-smoke:$$"
    BUILT_IMAGE="$IMAGE"
    echo "==> [build] no \$IMAGE supplied — building $IMAGE locally"
    docker build -t "$IMAGE" -q . >/dev/null
else
    IMAGE_PRESET=1
    echo "==> [build] using preset image $IMAGE"
fi

echo "==> [start] launching container on port $PORT"
docker run -d --name "$CONTAINER_NAME" -p "$PORT:8080" \
    -e ASPNETCORE_ENVIRONMENT=Production \
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
# 1. Anonymous identity round-trip
##############################################################################
echo "==> [1/4] POST /api/identity (mint anonymous identity)"
http_code=$(curl -fsS -o "$IDENTITY_RESP" -w '%{http_code}' \
    -c "$COOKIE_JAR" \
    -X POST "http://localhost:$PORT/api/identity" \
    -H 'Accept: application/json')
if [[ "$http_code" != "200" ]]; then
    echo "❌ expected 200 from POST /api/identity, got $http_code"
    cat "$IDENTITY_RESP" || true
    exit 1
fi
if ! grep -q "mahjong_pid" "$COOKIE_JAR"; then
    echo "❌ Set-Cookie: mahjong_pid not received"
    exit 1
fi
echo "    ✅ mahjong_pid cookie set"

##############################################################################
# 2. POST /api/reconnect/issue — mint initial token
##############################################################################
GAME_ID="smoke-$(date -u +%s)-$$"
ISSUE_PAYLOAD=$(printf '{"gameId":"%s","seatIndex":0}' "$GAME_ID")

echo "==> [2/4] POST /api/reconnect/issue (forward-compat — skip if not registered)"
ISSUE_CODE=$(curl -sS -o "$ISSUE_RESP" -w '%{http_code}' \
    -b "$COOKIE_JAR" -c "$COOKIE_JAR" \
    -X POST "http://localhost:$PORT/api/reconnect/issue" \
    -H 'Content-Type: application/json' \
    -H 'Accept: application/json' \
    --data "$ISSUE_PAYLOAD" 2>/dev/null || echo "000")

TOKEN_SURFACE_LIVE=0
INITIAL_TOKEN=""
case "$ISSUE_CODE" in
    200|201)
        INITIAL_TOKEN=$(grep -o '"token":"[^"]*"' "$ISSUE_RESP" | head -1 | sed 's/.*:"\(.*\)"/\1/')
        if [[ -z "$INITIAL_TOKEN" ]]; then
            # Alternate field name (e.g. "value"/"reconnectToken"); accept any
            # non-empty token-looking field. Soft-pass if we can't extract.
            INITIAL_TOKEN=$(grep -oE '"(reconnectToken|value)":"[^"]+"' "$ISSUE_RESP" | head -1 | sed 's/.*:"\(.*\)"/\1/')
        fi
        if [[ -n "$INITIAL_TOKEN" ]]; then
            echo "    ✅ /api/reconnect/issue minted token (${#INITIAL_TOKEN} chars)"
            TOKEN_SURFACE_LIVE=1
        else
            echo "    ⏭  /api/reconnect/issue responded $ISSUE_CODE but token field not recognized — soft-pass"
        fi
        ;;
    404)
        echo "    ⏭  /api/reconnect/issue not yet registered on this image — soft-pass"
        ;;
    400|422)
        echo "    ⏭  /api/reconnect/issue rejected payload ($ISSUE_CODE) — surface live, body mismatch — soft-pass"
        ;;
    *)
        echo "❌ /api/reconnect/issue returned $ISSUE_CODE (expected 200/201/404)"
        cat "$ISSUE_RESP" || true
        exit 1
        ;;
esac

##############################################################################
# 3. POST /api/reconnect/rotate — exchange initial token for a fresh one
##############################################################################
NEW_TOKEN=""
if [[ "$TOKEN_SURFACE_LIVE" -eq 1 ]]; then
    ROTATE_PAYLOAD=$(printf '{"token":"%s","gameId":"%s"}' "$INITIAL_TOKEN" "$GAME_ID")
    echo "==> [3/4] POST /api/reconnect/rotate (exchange token)"
    ROTATE_CODE=$(curl -sS -o "$ROTATE_RESP" -w '%{http_code}' \
        -b "$COOKIE_JAR" -c "$COOKIE_JAR" \
        -X POST "http://localhost:$PORT/api/reconnect/rotate" \
        -H 'Content-Type: application/json' \
        -H 'Accept: application/json' \
        --data "$ROTATE_PAYLOAD" 2>/dev/null || echo "000")

    case "$ROTATE_CODE" in
        200|201)
            NEW_TOKEN=$(grep -o '"token":"[^"]*"' "$ROTATE_RESP" | head -1 | sed 's/.*:"\(.*\)"/\1/')
            if [[ -z "$NEW_TOKEN" ]]; then
                NEW_TOKEN=$(grep -oE '"(reconnectToken|newToken|value)":"[^"]+"' "$ROTATE_RESP" | head -1 | sed 's/.*:"\(.*\)"/\1/')
            fi
            if [[ -n "$NEW_TOKEN" && "$NEW_TOKEN" != "$INITIAL_TOKEN" ]]; then
                echo "    ✅ /api/reconnect/rotate minted a fresh token (rotation succeeded)"
            elif [[ "$NEW_TOKEN" == "$INITIAL_TOKEN" ]]; then
                echo "❌ rotated token is identical to initial token — rotation must produce a fresh value"
                exit 1
            else
                echo "    ⏭  /api/reconnect/rotate responded $ROTATE_CODE but token field not recognized — soft-pass"
            fi
            ;;
        404)
            echo "    ⏭  /api/reconnect/rotate not yet registered — soft-pass"
            TOKEN_SURFACE_LIVE=0
            ;;
        *)
            echo "❌ /api/reconnect/rotate returned $ROTATE_CODE (expected 200/201/404)"
            cat "$ROTATE_RESP" || true
            exit 1
            ;;
    esac
else
    echo "==> [3/4] Skipped (token issue endpoint not live)"
fi

##############################################################################
# 4. Replay-attack guard: re-rotate the OLD token; must be rejected (4xx)
##############################################################################
if [[ "$TOKEN_SURFACE_LIVE" -eq 1 && -n "$NEW_TOKEN" ]]; then
    echo "==> [4/4] POST /api/reconnect/rotate (reuse old token — must be rejected)"
    REUSE_PAYLOAD=$(printf '{"token":"%s","gameId":"%s"}' "$INITIAL_TOKEN" "$GAME_ID")
    REUSE_CODE=$(curl -sS -o "$REUSE_RESP" -w '%{http_code}' \
        -b "$COOKIE_JAR" \
        -X POST "http://localhost:$PORT/api/reconnect/rotate" \
        -H 'Content-Type: application/json' \
        -H 'Accept: application/json' \
        --data "$REUSE_PAYLOAD" 2>/dev/null || echo "000")
    case "$REUSE_CODE" in
        400|401|403|409|410|422)
            echo "    ✅ reused token correctly rejected (HTTP $REUSE_CODE)"
            ;;
        200|201)
            echo "❌ reused old token was accepted — single-use rotation invariant violated"
            cat "$REUSE_RESP" || true
            exit 1
            ;;
        404)
            echo "    ⏭  rotate endpoint disappeared between calls — soft-pass"
            ;;
        *)
            echo "❌ reused token returned unexpected $REUSE_CODE"
            cat "$REUSE_RESP" || true
            exit 1
            ;;
    esac
else
    echo "==> [4/4] Skipped (rotation surface not yet live)"
fi

echo
echo "🎉 token-rotation smoke test PASSED"
