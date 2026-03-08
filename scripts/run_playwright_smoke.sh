#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_URL="${PLAYWRIGHT_API_URL:-http://localhost:5270}"
CLIENT_URL="${PLAYWRIGHT_BASE_URL:-http://localhost:5299}"

log_dir="${PLAYWRIGHT_SMOKE_LOG_DIR:-$ROOT_DIR/artifacts/playwright-smoke}"
mkdir -p "$log_dir"

migrate_log="$log_dir/migrate.log"
api_log="$log_dir/api.log"
client_log="$log_dir/client.log"

api_pid=""
client_pid=""

rm -f "$migrate_log" "$api_log" "$client_log"

cleanup() {
  local exit_code=$?

  [[ -n "${api_pid}" ]] && kill "${api_pid}" 2>/dev/null || true
  [[ -n "${client_pid}" ]] && kill "${client_pid}" 2>/dev/null || true

  if [[ "$exit_code" -ne 0 ]]; then
    echo
    echo "Smoke run failed. Relevant logs:"
    echo "  migration: ${migrate_log}"
    echo "  api:       ${api_log}"
    echo "  client:    ${client_log}"

    if [[ -f "${migrate_log}" ]]; then
      echo
      echo "--- tail: migration log ---"
      tail -n 80 "${migrate_log}" || true
    fi

    if [[ -f "${api_log}" ]]; then
      echo
      echo "--- tail: api log ---"
      tail -n 80 "${api_log}" || true
    fi

    if [[ -f "${client_log}" ]]; then
      echo
      echo "--- tail: client log ---"
      tail -n 80 "${client_log}" || true
    fi
  fi
}
trap cleanup EXIT

wait_for_http() {
  local url="$1"
  local name="$2"

  for _ in {1..60}; do
    local code
    code="$(curl -s -o /dev/null -w "%{http_code}" "$url" || true)"
    if [[ "$code" != "000" ]]; then
      return 0
    fi
    sleep 1
  done

  echo "Timed out waiting for ${name} at ${url}" >&2
  return 1
}

echo "Smoke logs will be written to: ${log_dir}"
echo "Applying database migrations..."
(
  cd "$ROOT_DIR"
  dotnet ef database update --project src/MahjongClaw.Persistence --startup-project src/MahjongClaw.Api
) >"${migrate_log}" 2>&1

echo "Starting API at ${API_URL}..."
(
  cd "$ROOT_DIR"
  dotnet run --project src/MahjongClaw.Api --launch-profile http
) >"${api_log}" 2>&1 &
api_pid=$!

wait_for_http "$API_URL" "API"

echo "Starting Client at ${CLIENT_URL}..."
(
  cd "$ROOT_DIR"
  dotnet run --project src/MahjongClaw.Client --launch-profile http
) >"${client_log}" 2>&1 &
client_pid=$!

wait_for_http "$CLIENT_URL" "Client"

echo "Installing Playwright dependencies..."
cd "$ROOT_DIR"
npm install --include=dev

echo "Running Playwright smoke suite..."
PLAYWRIGHT_BASE_URL="$CLIENT_URL" PLAYWRIGHT_API_URL="$API_URL" npm run test:e2e
