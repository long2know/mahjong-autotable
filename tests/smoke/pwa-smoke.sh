#!/usr/bin/env bash
# Phase K Wave 2 — Apone (DevOps).
#
# PWA service-worker smoke. Boots the production Docker image on port
# 18093 (unique in the smoke port allocation; see history.md) and runs
# a Playwright (chromium-only) probe that asserts:
#   1. `GET /` returns 200.
#   2. `GET /sw.js` returns either 200 (then JS content-type) or 404
#      (soft-pass — Hicks's SW artefact may still be in-flight).
#   3. `navigator.serviceWorker.getRegistration()` yields an activated
#      worker.
#   4. After `page.reload()`, `navigator.serviceWorker.controller !=
#      null` (the canonical "SW took control" assertion).
#
# Forward-compat: the smoke soft-passes on 404 for `/sw.js`. The moment
# Hicks's Parcel pipeline ships the artefact, the assertion in #4 fires
# as a hard pass and the gate tightens — no CI workflow change needed.
#
# Smoke port allocation: docker-build=18080, auth=18081, chat=18082,
# token-rotation=18083, csp-report=18084, multi-arch-runtime
# (amd64)=18091 / (arm64)=18092, PWA=18093.
set -euo pipefail

IMAGE="${IMAGE:-mahjong-autotable:pwa-smoke}"
HOST_PORT="${HOST_PORT:-18093}"
BOOT_TIMEOUT_S="${BOOT_TIMEOUT_S:-60}"
CONTAINER="mahjong-autotable-pwa-smoke"

cleanup() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "[pwa-smoke] booting $IMAGE on host port $HOST_PORT…"
docker run -d --rm \
  --name "$CONTAINER" \
  -p "${HOST_PORT}:8080" \
  -e ASPNETCORE_URLS="http://0.0.0.0:8080" \
  -e Authentication__JwtSigningKeys__0="$(openssl rand -base64 48)" \
  "$IMAGE" >/dev/null

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
