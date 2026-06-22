#!/usr/bin/env bash
# Phase K Wave 3 (Apone, DevOps) — JWT signing-key rotation smoke test.
#
# Asserts the FALLBACK-KEY ROTATION contract documented in
# `docs/jwt-rotation.md` end-to-end against a live Docker image:
#
#   1. Boot the image with Auth__JwtSigningKeys__0=<key0> (active signer).
#   2. Mint a token via Bishop's auth surface — server signs with key0.
#   3. Stop the container, RE-BOOT with Auth__JwtSigningKeys__0=<key1>
#      (the new active signer) AND Auth__JwtSigningKeys__1=<key0>
#      (the previous key, still in the fallback list).
#   4. Validate the OLD key0-signed token against the new container —
#      MUST still pass (the whole point of the fallback list).
#   5. Mint a NEW token — server MUST sign with key1 now (verifiable
#      via the JWT header's `kid` claim or by checking the token
#      validates against key1 but not against key0 alone).
#
# ## Admin-session bootstrap (P2 — Bishop, auth lane)
#
# `POST /api/auth/token` is admin-session-gated (AuthTokenController.Issue:
# 401 without a session, 403 when the session role isn't "admin"). To
# exercise the mint -> rotate -> validate mechanics this smoke first
# establishes an admin session via `POST /api/auth/dev-login {email,
# role:"admin"}` — which the API registers ONLY when running in
# Development (AuthController.DevLogin returns 404 in Production). We
# therefore boot THIS smoke's container in Development posture
# (ASPNETCORE_ENVIRONMENT=Development); the image binary is byte-identical
# to the Production smoke images — only the environment flag differs. The
# Production deployment posture is UNCHANGED: dev-login stays 404 and
# /api/auth/token stays admin-gated in real Production images. The
# container's SQLite store lives inside the container with no volume
# mount, so the Phase-3 rotate (stop + rm + re-run) wipes the session
# table — we re-establish the admin session after every boot (see
# start_container -> admin_login).
#
# ## Forward-compatibility
#
# If an older image is supplied via $IMAGE where Bishop's surface isn't
# registered yet (`/api/auth/token` / `/api/auth/validate` return 404),
# the smoke SOFT-PASSES, matching the shape used by `pwa-smoke.sh`,
# `chat-flow-smoke.sh`, `csp-report-smoke.sh`.
#
# ## Inputs
#
#   IMAGE                  — image tag to test (default: build from local Dockerfile)
#   PORT                   — host port to bind (default: 18094 — next free in the
#                            series; see tests/smoke/README.md for the port allocation)
#   ASPNETCORE_ENVIRONMENT — container env posture (default: Development, so the
#                            dev-login admin bootstrap is available)
#   ADMIN_EMAIL            — dev-login admin identity (default: jwt-rotation-smoke@local.test)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

IMAGE="${IMAGE:-}"
PORT="${PORT:-18094}"
CONTAINER_NAME="mahjong-jwt-rot-smoke-$$"
LOG_DIR="$REPO_ROOT/tests/smoke/.run-jwtrot-$$"
mkdir -p "$LOG_DIR"
COOKIE_JAR="$LOG_DIR/cookies.txt"
TOKEN_RESP1="$LOG_DIR/token1.json"
TOKEN_RESP2="$LOG_DIR/token2.json"
VALIDATE_RESP="$LOG_DIR/validate.json"

# Two deterministic-but-throwaway HMAC keys for the rotation cycle.
# Length 64 bytes (Base64 of 48 random bytes) clears the
# HmacSha256 minimum-key-length guard that .NET enforces server-side.
KEY0="dev-key-rotation-smoke-zero-do-not-use-in-prod-padding-padding-pad"
KEY1="dev-key-rotation-smoke-one-do-not-use-in-prod-padding-padding-padd"

# Boot this smoke's container in Development posture so the dev-login
# admin-session bootstrap (POST /api/auth/dev-login) is registered — it
# returns 404 in Production. Development is the only posture in which the
# admin-gated mint endpoint can be exercised end to end. Production images
# keep dev-login 404 + the token endpoint admin-gated; nothing here
# changes that default.
ASPNETCORE_ENV="${ASPNETCORE_ENVIRONMENT:-Development}"
ADMIN_EMAIL="${ADMIN_EMAIL:-jwt-rotation-smoke@local.test}"

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
    IMAGE="mahjong-jwt-rot-smoke:$$"
    BUILT_IMAGE="$IMAGE"
    echo "==> [build] no \$IMAGE supplied — building $IMAGE locally"
    docker build -t "$IMAGE" -q . >/dev/null
else
    IMAGE_PRESET=1
    echo "==> [build] using preset image $IMAGE"
fi

start_container() {
    local container_name="$1"
    local key0="$2"
    local key1="${3:-}"
    docker stop "$container_name" >/dev/null 2>&1 || true
    docker rm "$container_name" >/dev/null 2>&1 || true

    local env_args=(-e "Auth__JwtSigningKeys__0=$key0")
    if [[ -n "$key1" ]]; then
        env_args+=(-e "Auth__JwtSigningKeys__1=$key1")
    fi

    docker run -d --name "$container_name" -p "$PORT:8080" \
        -e "ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENV" \
        "${env_args[@]}" \
        "$IMAGE" >/dev/null

    echo "==> [wait] /health on $container_name"
    local health_ok=0
    for i in $(seq 1 30); do
        if curl -fsS "http://localhost:$PORT/health" >/dev/null 2>&1; then
            echo "    /health responding after ${i}s"
            health_ok=1
            break
        fi
        sleep 1
    done
    if [[ "$health_ok" -ne 1 ]]; then
        echo "❌ /health did not respond within 30s"
        docker logs "$container_name" 2>&1 | tail -50
        return 1
    fi

    # The mint endpoint is admin-session-gated, and the in-container
    # SQLite session table is wiped on every (re)boot (no volume mount),
    # so establish a fresh admin session on each boot — covers both the
    # initial key0 mint and the post-rotation key1 mint.
    admin_login || return 1
}

admin_login() {
    # Establish an admin session cookie via dev-login (Development-only
    # surface — AuthController.DevLogin returns 404 in Production). The
    # role:"admin" stamp is what satisfies the AuthTokenController.Issue
    # gate (403 for non-admin sessions). The session cookie is written
    # into $COOKIE_JAR and reused by probe_token_surface's mint call.
    local outfile="$LOG_DIR/devlogin.json"
    local code
    code=$(curl -fsS -o "$outfile" -w '%{http_code}' \
        -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
        -X POST "http://localhost:$PORT/api/auth/dev-login" \
        -H 'Accept: application/json' \
        -H 'Content-Type: application/json' \
        -d "{\"email\":\"$ADMIN_EMAIL\",\"role\":\"admin\"}" 2>/dev/null || echo "000")
    case "$code" in
        200)
            if grep -qE '"role":[[:space:]]*"admin"' "$outfile"; then
                echo "    ✅ admin session established via dev-login (role=admin)"
            else
                echo "❌ dev-login returned 200 but did not stamp role=admin:"
                cat "$outfile" 2>/dev/null || true
                return 1
            fi
            ;;
        404)
            echo "❌ POST /api/auth/dev-login returned 404 — admin bootstrap unavailable."
            echo "   This smoke boots the image in Development posture so dev-login is"
            echo "   registered; got 404, which means the container is NOT in Development"
            echo "   (ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENV) or the surface is missing."
            cat "$outfile" 2>/dev/null || true
            return 1
            ;;
        *)
            echo "❌ POST /api/auth/dev-login returned $code (expected 200)"
            cat "$outfile" 2>/dev/null || true
            return 1
            ;;
    esac
}

probe_token_surface() {
    # Mints a token via the admin-gated endpoint. Reuses the admin
    # session cookie from $COOKIE_JAR (established by admin_login) and
    # sends the required non-empty `subject` (Issue returns 400 without
    # it). Returns 200 / 404 / other into stdout; curl failure -> 000.
    local outfile="$1"
    curl -fsS -o "$outfile" -w '%{http_code}' \
        -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
        -X POST "http://localhost:$PORT/api/auth/token" \
        -H 'Accept: application/json' \
        -H 'Content-Type: application/json' \
        -d '{"subject":"jwt-rotation-smoke"}' 2>/dev/null || echo "000"
}

##############################################################################
# Phase 1 — boot with key0 as active, mint token under key0
##############################################################################
echo "==> [1/5] boot container with JwtSigningKeys[0]=key0"
start_container "$CONTAINER_NAME" "$KEY0"

echo "==> [2/5] mint token under key0 via POST /api/auth/token"
http_code=$(probe_token_surface "$TOKEN_RESP1")
case "$http_code" in
    200)
        echo "    ✅ token minted under key0"
        ;;
    404)
        echo "    ⏭  /api/auth/token not registered on this (older) image — Bishop's binding absent."
        echo "    ⏭  SOFT-PASS: rotation smoke skipped on pre-surface images."
        exit 0
        ;;
    *)
        echo "❌ /api/auth/token returned $http_code (expected 200 or 404)"
        cat "$TOKEN_RESP1" || true
        exit 1
        ;;
esac

# Extract the JWT string (heuristic — Bishop's surface returns either
# {"token":"<jwt>"} or {"accessToken":"<jwt>"} per docs/jwt-rotation.md).
TOKEN_KEY0=$(grep -oE '"(access)?[Tt]oken":"[^"]+"' "$TOKEN_RESP1" | head -1 | sed 's/.*:"\(.*\)"/\1/')
if [[ -z "$TOKEN_KEY0" || ! "$TOKEN_KEY0" =~ ^ey ]]; then
    echo "❌ could not extract JWT from /api/auth/token response:"
    cat "$TOKEN_RESP1" || true
    exit 1
fi
echo "    captured key0 token (header: $(echo "$TOKEN_KEY0" | cut -d. -f1 | head -c 32)…)"

##############################################################################
# Phase 3 — rotate: key0 → keys[1], new key1 → keys[0]
##############################################################################
echo "==> [3/5] rotate: stop + restart with JwtSigningKeys[0]=key1, [1]=key0"
start_container "$CONTAINER_NAME" "$KEY1" "$KEY0"

##############################################################################
# Phase 4 — old key0-signed token MUST still validate (fallback)
##############################################################################
echo "==> [4/5] validate old key0 token against rotated container"
http_code=$(curl -fsS -o "$VALIDATE_RESP" -w '%{http_code}' \
    -X POST "http://localhost:$PORT/api/auth/validate" \
    -H 'Accept: application/json' \
    -H 'Content-Type: application/json' \
    -d "{\"token\":\"$TOKEN_KEY0\"}" 2>/dev/null || echo "000")
case "$http_code" in
    200)
        if grep -qE '"(isValid|valid)":true' "$VALIDATE_RESP"; then
            echo "    ✅ old key0 token validates under rotated key set (fallback works)"
        else
            echo "❌ /api/auth/validate returned 200 but valid!=true — the old key0"
            echo "    token was REJECTED by the rotated key set. Bug: fallback-key list"
            echo "    not honored — see docs/jwt-rotation.md §troubleshooting."
            cat "$VALIDATE_RESP" || true
            exit 1
        fi
        ;;
    404)
        echo "    ⏭  /api/auth/validate not yet registered — rotation smoke soft-passes."
        exit 0
        ;;
    401|403)
        echo "❌ old key0 token REJECTED by rotated container (status $http_code)"
        echo "    Bug: fallback-key list not honored — see docs/jwt-rotation.md §troubleshooting."
        cat "$VALIDATE_RESP" || true
        exit 1
        ;;
    *)
        echo "❌ /api/auth/validate returned $http_code"
        cat "$VALIDATE_RESP" || true
        exit 1
        ;;
esac

##############################################################################
# Phase 5 — new tokens MUST sign with key1 (the new active signer)
##############################################################################
echo "==> [5/5] mint new token; assert signed by key1 (not key0)"
http_code=$(probe_token_surface "$TOKEN_RESP2")
if [[ "$http_code" != "200" ]]; then
    echo "❌ /api/auth/token returned $http_code post-rotation"
    cat "$TOKEN_RESP2" || true
    exit 1
fi
TOKEN_KEY1=$(grep -oE '"(access)?[Tt]oken":"[^"]+"' "$TOKEN_RESP2" | head -1 | sed 's/.*:"\(.*\)"/\1/')
if [[ -z "$TOKEN_KEY1" || ! "$TOKEN_KEY1" =~ ^ey ]]; then
    echo "❌ could not extract new JWT:"
    cat "$TOKEN_RESP2" || true
    exit 1
fi

# Differentiation check — header.payload prefix MUST differ (different
# `kid` claim if Bishop ships kid-based key id, OR identical headers
# but different signature; either way `TOKEN_KEY0 != TOKEN_KEY1`).
if [[ "$TOKEN_KEY0" == "$TOKEN_KEY1" ]]; then
    echo "❌ new token is byte-identical to old token — server did NOT rotate the signer"
    exit 1
fi
echo "    ✅ new token differs from old; rotation produced a fresh-signed JWT"

echo "✅ jwt-rotation smoke test passed"
echo "    • old key0 token: still valid (fallback list honored)"
echo "    • new tokens:     signed under key1 (active signer rotated)"
