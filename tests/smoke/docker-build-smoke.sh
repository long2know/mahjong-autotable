#!/usr/bin/env bash
# Phase J Wave 3 — Docker build smoke test (Vasquez).
#
# End-to-end verification of Apone's multi-stage Dockerfile single-image
# deployment surface: builds the image, starts a container, polls /health
# until it responds, and asserts the four expected fields are present in
# the JSON body. Always tears down the container + image on completion.
#
# Apone's Wave-3 layout ships the Dockerfile at infra/docker/Dockerfile
# with a `runtime-autotable` target (matching docker-compose.yml). The
# script auto-detects whichever location is present so this remains
# robust if the Dockerfile is later promoted to the repo root.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

IMAGE_TAG="mahjong-autotable:smoke-$$"
CONTAINER_NAME="mahjong-smoke-$$"
PORT=18080
LOG_DIR="$REPO_ROOT/tests/smoke/.run-$$"
mkdir -p "$LOG_DIR"
BUILD_LOG="$LOG_DIR/docker-build.log"
HEALTH_JSON="$LOG_DIR/health.json"

cleanup() {
    docker stop "$CONTAINER_NAME" >/dev/null 2>&1 || true
    docker rm "$CONTAINER_NAME" >/dev/null 2>&1 || true
    docker rmi "$IMAGE_TAG" >/dev/null 2>&1 || true
    rm -rf "$LOG_DIR" 2>/dev/null || true
}
trap cleanup EXIT

# Locate Apone's Dockerfile — Apone's `.dockerignore` declares the canonical
# Dockerfile lives at the repo root (and excludes the pre-built frontend
# bundle so Stage 1 rebuilds it from source). We prefer that when present
# and fall back to the legacy `infra/docker/Dockerfile` only when the root
# file is absent, so this script survives both layouts cleanly.
BUILD_ARGS=(-t "$IMAGE_TAG")
if [ -f "Dockerfile" ]; then
    : # default `docker build .` will pick up ./Dockerfile
elif [ -f "infra/docker/Dockerfile" ]; then
    BUILD_ARGS+=(-f "infra/docker/Dockerfile" --target runtime-autotable)
else
    echo "❌ no Dockerfile found at repo root or infra/docker/Dockerfile" >&2
    exit 1
fi
BUILD_ARGS+=(.)

echo "==> [1/4] Building $IMAGE_TAG (docker build ${BUILD_ARGS[*]})..."
if ! docker build "${BUILD_ARGS[@]}" > "$BUILD_LOG" 2>&1; then
    echo "❌ docker build failed"
    tail -30 "$BUILD_LOG"
    exit 1
fi
echo "✅ build succeeded"

echo "==> [2/4] Starting container..."
docker run -d --name "$CONTAINER_NAME" -p "$PORT:8080" "$IMAGE_TAG" > /dev/null

echo "==> [3/4] Waiting for /health..."
HEALTH_OK=0
for i in $(seq 1 30); do
    if curl -fsS "http://localhost:$PORT/health" > "$HEALTH_JSON" 2>/dev/null; then
        echo "✅ /health responding after ${i}s"
        cat "$HEALTH_JSON"
        echo
        HEALTH_OK=1
        break
    fi
    sleep 1
done

if [ "$HEALTH_OK" -ne 1 ]; then
    echo "❌ /health did not respond within 30 seconds"
    docker logs "$CONTAINER_NAME" 2>&1 | tail -50
    exit 1
fi

echo "==> [4/4] Assert response shape..."
SHAPE_OK=1
for field in status buildSha uptime version; do
    if grep -q "\"$field\"" "$HEALTH_JSON"; then
        echo "  ✅ $field field present"
    else
        echo "  ❌ $field field MISSING"
        SHAPE_OK=0
    fi
done

if [ "$SHAPE_OK" -ne 1 ]; then
    echo "❌ /health response shape is incomplete — see body above"
    exit 1
fi

echo
echo "🎯 Docker smoke test PASSED"
