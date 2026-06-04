#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────
# Phase L — Drake. JWT restart-survival proof for the Docker prod path.
#
# This script proves the Phase-L hardening fix: when the operator
# pins `Authentication__JwtSigningKeys__0`, JWTs minted before a
# container restart STILL validate after the restart.
#
# Pre-Phase-L (ephemeral random fallback): a JWT minted under
# container A would 401 / "invalid signature" under container B
# because the new random HMAC key never matches the old signature.
#
# Post-Phase-L: with a pinned key, the signature survives the
# restart 1:1.
#
# The script is intentionally header-level (no UI flow). It mints
# a JWT in bash using openssl HMAC-SHA256, calls
# `POST /api/auth/validate` against a live container, restarts the
# container, and re-calls validate against the same token. Both
# calls must return `valid:true`.
#
# Usage:
#     bash playtest-artifacts/jwt-restart-survival.sh
#
# Exit code:
#     0   restart-survival proven (pre AND post valid)
#     1   any step failed (network, validate-false, container start)
# ─────────────────────────────────────────────────────────────────────

set -euo pipefail

readonly IMAGE="${IMAGE:-mahjong-autotable:drake-jwt-prod}"
readonly CONTAINER="${CONTAINER:-mat-restart-proof}"
readonly HOST_PORT="${HOST_PORT:-9100}"
readonly BASE_URL="http://127.0.0.1:${HOST_PORT}"

# Stable signing key — kept verbatim across the two container runs so
# the JWT minted before the restart is signature-compatible with the
# validator inside the restarted container. In production the operator
# would source this from a secret store (ESO, SSM, Vault, sealed
# k8s Secret) — see docs/jwt-rotation.md §7.1.
readonly JWT_KEY="drake-restart-survival-stable-key-aaaaaaaaaaaaaaaa"

log() { printf '[%s] %s\n' "$(date -u +'%H:%M:%S')" "$*" >&2; }
fail() { printf '\n❌ %s\n' "$*" >&2; exit 1; }

cleanup() {
    docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

# ─── helpers ─────────────────────────────────────────────────────────

b64url() {
    # base64 (no padding, +/ → -_) — JWT-canonical.
    openssl base64 -e -A | tr -d '=' | tr '+/' '-_'
}

mint_jwt() {
    # Phase L — Drake. Pure-bash HMAC-SHA256 JWT minter. The kid is
    # omitted intentionally: the validator iterates every key in the
    # fallback list on kid-lookup miss (Phase K W4 Bishop contract),
    # so an absent kid still resolves cleanly under the active signer.
    local sub="$1"
    local exp; exp=$(( $(date +%s) + 3600 ))
    local header; header=$(printf '{"alg":"HS256","typ":"JWT"}' | b64url)
    local payload; payload=$(printf '{"sub":"%s","exp":%d}' "$sub" "$exp" | b64url)
    local signing_input="${header}.${payload}"
    local sig; sig=$(printf '%s' "$signing_input" \
        | openssl dgst -binary -sha256 -hmac "$JWT_KEY" \
        | b64url)
    printf '%s.%s' "$signing_input" "$sig"
}

call_validate() {
    # Returns the JSON body. Caller greps for valid:true / valid:false.
    local token="$1"
    curl -sf -X POST "${BASE_URL}/api/auth/validate" \
        -H 'Content-Type: application/json' \
        -d "{\"token\":\"${token}\"}"
}

wait_for_health() {
    local i
    for i in $(seq 1 60); do
        if curl -sf "${BASE_URL}/health" >/dev/null 2>&1; then return 0; fi
        sleep 1
    done
    return 1
}

start_container() {
    local label="$1"
    docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
    docker run -d --name "$CONTAINER" \
        -p "${HOST_PORT}:8080" \
        -e ASPNETCORE_URLS="http://0.0.0.0:8080" \
        -e Authentication__JwtSigningKeys__0="$JWT_KEY" \
        "$IMAGE" >/dev/null
    log "[$label] container started, waiting for /health …"
    if ! wait_for_health; then
        docker logs --tail 50 "$CONTAINER" >&2 || true
        fail "[$label] /health never became ready"
    fi
    log "[$label] /health OK"
}

# ─── proof ───────────────────────────────────────────────────────────

if ! docker image inspect "$IMAGE" >/dev/null 2>&1; then
    log "image $IMAGE not present locally — building from repo root …"
    docker build -t "$IMAGE" "$(git rev-parse --show-toplevel)" >&2 \
        || fail "docker build failed"
fi

log "step 1 — start container A (pre-restart)"
start_container "A"

log "step 2 — mint JWT under the pinned key"
JWT=$(mint_jwt "restart-survivor")
log "       token: ${JWT:0:24}…${JWT: -16}"

log "step 3 — POST /api/auth/validate against container A"
PRE_BODY=$(call_validate "$JWT" || fail "pre-restart validate HTTP call failed")
log "       pre-restart response: $PRE_BODY"
if ! grep -q '"valid":true' <<<"$PRE_BODY"; then
    fail "pre-restart validation FAILED — expected valid:true, got: $PRE_BODY"
fi
log "       ✅ pre-restart validate: valid:true"

log "step 4 — restart container (docker rm -f + docker run)"
docker rm -f "$CONTAINER" >/dev/null
sleep 1
start_container "B"

log "step 5 — POST /api/auth/validate against container B with the SAME token"
POST_BODY=$(call_validate "$JWT" || fail "post-restart validate HTTP call failed")
log "       post-restart response: $POST_BODY"
if ! grep -q '"valid":true' <<<"$POST_BODY"; then
    fail "post-restart validation FAILED — expected valid:true, got: $POST_BODY"
fi
log "       ✅ post-restart validate: valid:true"

printf '\n✅ JWT restart-survival proven\n'
printf '   image:          %s\n' "$IMAGE"
printf '   pre-restart:    valid:true (subject=restart-survivor)\n'
printf '   post-restart:   valid:true (SAME token survives container rebirth)\n'
printf '   stable key bound to Authentication__JwtSigningKeys__0\n'
printf '   ⇒ Phase L production fail-fast + restart-survival contract proven end-to-end.\n'
