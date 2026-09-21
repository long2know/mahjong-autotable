#!/usr/bin/env bash
# Phase K Wave 2 — Apone (DevOps).
#
# PWA service-worker smoke. Boots the production Docker image on port
# 18093 (unique in the smoke port allocation; see history.md) and runs
# a Playwright (chromium-only) probe that asserts:
#   1. `GET /autotable/` returns 200.
#   2. `GET /autotable/sw.js` returns 200 with a JS content-type.
#   3. The actual /autotable/ worker is registered and activated.
#   4. After `page.reload()`, `navigator.serviceWorker.controller !=
#      null` (the canonical "SW took control" assertion).
#
# Smoke port allocation: docker-build=18080, auth=18081, chat=18082,
# token-rotation=18083, csp-report=18084, multi-arch-runtime
# (amd64)=18091 / (arm64)=18092, PWA=18093.
set -euo pipefail

IMAGE="${IMAGE:-mahjong-autotable:pwa-smoke}"
HOST_PORT="${HOST_PORT:-18093}"
BOOT_TIMEOUT_S="${BOOT_TIMEOUT_S:-60}"
CONTAINER="${CONTAINER:-mahjong-autotable-pwa-smoke-$$}"
CONTAINER_ID=""

cleanup() {
  if [ -n "$CONTAINER_ID" ]; then
    docker rm -f "$CONTAINER_ID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "[pwa-smoke] booting $IMAGE on host port $HOST_PORT…"
CONTAINER_ID="$(docker run -d --rm \
  --name "$CONTAINER" \
  -p "127.0.0.1:${HOST_PORT}:8080" \
  -e ASPNETCORE_URLS="http://0.0.0.0:8080" \
  -e Authentication__JwtSigningKeys__0="$(openssl rand -base64 48)" \
  "$IMAGE")"

deadline=$(( $(date +%s) + BOOT_TIMEOUT_S ))
while [ "$(date +%s)" -lt "$deadline" ]; do
  if curl -fsS -m 3 "http://localhost:${HOST_PORT}/health" >/dev/null 2>&1; then
    echo "[pwa-smoke] container ready"
    break
  fi
  sleep 2
done

if ! curl -fsS -m 3 "http://localhost:${HOST_PORT}/health" >/dev/null 2>&1; then
  echo "::error::container did not return /health within ${BOOT_TIMEOUT_S}s"
  docker logs "$CONTAINER" >&2 || true
  exit 1
fi

# Ensure Playwright Chromium driver is installed. We reuse the
# autotable-src node_modules (Hicks's E2E suite already installs it)
# rather than spinning up a fresh dep tree.
DRIVER_ROOT="src/frontend/autotable-src/node_modules/playwright"
if [ ! -d "$DRIVER_ROOT" ]; then
  echo "[pwa-smoke] Playwright not yet installed under $DRIVER_ROOT — installing"
  (cd src/frontend/autotable-src && npm ci && npx playwright install --with-deps chromium)
fi

# Run the JS probe. It exits with a non-zero status on assertion fail.
echo "[pwa-smoke] running Playwright probe…"
PWA_SMOKE_BASE_URL="http://localhost:${HOST_PORT}" \
  node tests/smoke/pwa-smoke.js

echo "[pwa-smoke] ✅ all assertions passed"
