#!/usr/bin/env bash
# Phase J Wave 9 (Apone) — chat-flow smoke test.
#
# Validates the anonymous identity round-trip + forward-compatibly
# probes Bishop's Wave-9 chat surface (POST /api/chat/send,
# GET /api/games/{id}/chat) against a live Docker image. Soft-passes
# (⏭) when the chat endpoints return 404 — the same pattern as Wave-8
# auth-flow-smoke.sh.
#
# Steps:
#   1. POST /api/identity                            → mahjong_pid cookie minted
#   2. POST /api/chat/send  body={gameId,channel,body}
#                                                    → 200/202 (Bishop's surface)
#                                                       OR 404 (skip)
#   3. GET  /api/games/{gameId}/chat?limit=10        → 200 (Bishop's surface)
#                                                       OR 404 (skip)
#   4. validate the sent message appears in backfill (when both ends live)
#
# Inputs:
#   - $IMAGE  → image tag to test (default: build from local Dockerfile)
#   - $PORT   → host port to bind (default: 18082 to avoid collision with
#               docker-build-smoke.sh on 18080 and auth-flow-smoke.sh on
#               18081)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

IMAGE="${IMAGE:-}"
PORT="${PORT:-18082}"
CONTAINER_NAME="mahjong-chat-smoke-$$"
LOG_DIR="$REPO_ROOT/tests/smoke/.run-chat-$$"
mkdir -p "$LOG_DIR"
COOKIE_JAR="$LOG_DIR/cookies.txt"
IDENTITY_RESP="$LOG_DIR/identity.json"
SEND_RESP="$LOG_DIR/send.json"
BACKFILL_RESP="$LOG_DIR/backfill.json"

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
    IMAGE="mahjong-chat-smoke:$$"
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
# 1. Anonymous identity round-trip (mahjong_pid cookie minted)
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
    cat "$COOKIE_JAR"
    exit 1
fi
echo "    ✅ mahjong_pid cookie set"

##############################################################################
# 2. POST /api/chat/send — forward-compatible probe of Bishop's surface
##############################################################################
GAME_ID="smoke-$(date -u +%s)-$$"
BODY_TEXT="hello from chat smoke $(date -u +%FT%TZ)"
JSON_PAYLOAD=$(printf '{"gameId":"%s","channel":"table","body":"%s"}' "$GAME_ID" "$BODY_TEXT")

echo "==> [2/4] POST /api/chat/send (forward-compat — skip if not yet registered)"
SEND_CODE=$(curl -sS -o "$SEND_RESP" -w '%{http_code}' \
    -b "$COOKIE_JAR" -c "$COOKIE_JAR" \
    -X POST "http://localhost:$PORT/api/chat/send" \
    -H 'Content-Type: application/json' \
    -H 'Accept: application/json' \
    --data "$JSON_PAYLOAD" 2>/dev/null || echo "000")

CHAT_SURFACE_LIVE=0
case "$SEND_CODE" in
    200|201|202|204)
        echo "    ✅ /api/chat/send accepted message (HTTP $SEND_CODE)"
        CHAT_SURFACE_LIVE=1
        ;;
    404)
        echo "    ⏭  /api/chat/send not yet registered on this image — soft-pass"
        ;;
    400|422)
        # Endpoint exists but rejected the payload; the surface IS live —
        # log + continue to the backfill probe so the rest of the smoke
        # still exercises Bishop's GET endpoint.
        echo "    ⏭  /api/chat/send rejected payload ($SEND_CODE) — accepting as live-surface signal"
        CHAT_SURFACE_LIVE=1
        ;;
    *)
        echo "❌ /api/chat/send returned $SEND_CODE (expected 200/201/202/204/404)"
        cat "$SEND_RESP" || true
        exit 1
        ;;
esac

##############################################################################
# 3. GET /api/games/{gameId}/chat — backfill probe
##############################################################################
echo "==> [3/4] GET /api/games/$GAME_ID/chat?limit=10 (forward-compat)"
BACKFILL_CODE=$(curl -sS -o "$BACKFILL_RESP" -w '%{http_code}' \
    -b "$COOKIE_JAR" \
    "http://localhost:$PORT/api/games/$GAME_ID/chat?limit=10" 2>/dev/null || echo "000")

BACKFILL_SURFACE_LIVE=0
case "$BACKFILL_CODE" in
    200)
        echo "    ✅ /api/games/{id}/chat reachable"
        BACKFILL_SURFACE_LIVE=1
        ;;
    404)
        echo "    ⏭  /api/games/{id}/chat not yet registered on this image — soft-pass"
        ;;
    *)
        echo "❌ /api/games/{id}/chat returned $BACKFILL_CODE (expected 200 or 404)"
        cat "$BACKFILL_RESP" || true
        exit 1
        ;;
esac

##############################################################################
# 4. Verify backfill contains the sent message (only when both ends are live)
##############################################################################
echo "==> [4/4] Verify message round-trip (only when send + backfill both registered)"
if [[ "$CHAT_SURFACE_LIVE" -eq 1 && "$BACKFILL_SURFACE_LIVE" -eq 1 ]]; then
    # The response should be a JSON array (or envelope with messages).
    if ! grep -F -q "$BODY_TEXT" "$BACKFILL_RESP"; then
        echo "❌ sent message body not found in backfill response"
        cat "$BACKFILL_RESP" || true
        exit 1
    fi
    echo "    ✅ sent message body present in backfill response"
else
    echo "    ⏭  one or both endpoints not yet live — skipping round-trip assertion"
fi

echo
echo "🎉 chat-flow smoke test PASSED"
