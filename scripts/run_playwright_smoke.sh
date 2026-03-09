#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

pick_free_port() {
  python3 - <<'PY'
import socket
s = socket.socket()
s.bind(("127.0.0.1", 0))
print(s.getsockname()[1])
s.close()
PY
}

API_PORT="${PLAYWRIGHT_API_PORT:-$(pick_free_port)}"
CLIENT_PORT="${PLAYWRIGHT_CLIENT_PORT:-$(pick_free_port)}"
API_URL="${PLAYWRIGHT_API_URL:-http://127.0.0.1:${API_PORT}}"
CLIENT_URL="${PLAYWRIGHT_BASE_URL:-http://127.0.0.1:${CLIENT_PORT}}"

log_dir="${PLAYWRIGHT_SMOKE_LOG_DIR:-$ROOT_DIR/artifacts/playwright-smoke}"
mkdir -p "$log_dir"

migrate_log="$log_dir/migrate.log"
api_log="$log_dir/api.log"
client_log="$log_dir/client.log"

api_pid=""
client_pid=""
client_appsettings="$ROOT_DIR/src/MahjongClaw.Client/wwwroot/appsettings.json"
client_appsettings_backup="$log_dir/appsettings.backup.json"

rm -f "$migrate_log" "$api_log" "$client_log"

cleanup() {
  local exit_code=$?

  [[ -n "${api_pid}" ]] && kill "${api_pid}" 2>/dev/null || true
  [[ -n "${client_pid}" ]] && kill "${client_pid}" 2>/dev/null || true

  if [[ -f "$client_appsettings_backup" ]]; then
    mv "$client_appsettings_backup" "$client_appsettings"
  fi

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
  local pid="$3"

  for _ in {1..60}; do
    if ! kill -0 "$pid" 2>/dev/null; then
      echo "${name} process exited before becoming ready (${url})" >&2
      return 1
    fi

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
  ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/MahjongClaw.Persistence --startup-project src/MahjongClaw.Api
) >"${migrate_log}" 2>&1

echo "Starting API at ${API_URL}..."
(
  cd "$ROOT_DIR"
  ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MahjongClaw.Api --no-launch-profile --urls "$API_URL"
) >"${api_log}" 2>&1 &
api_pid=$!

wait_for_http "$API_URL" "API" "$api_pid"

echo "Configuring client API base URL (${API_URL})..."
cp "$client_appsettings" "$client_appsettings_backup"
python3 - <<'PY' "$client_appsettings" "$API_URL"
import json, sys
path, api_url = sys.argv[1], sys.argv[2]
with open(path, 'r', encoding='utf-8') as f:
    data = json.load(f)
data['ApiBaseUrl'] = api_url
with open(path, 'w', encoding='utf-8') as f:
    json.dump(data, f, indent=2)
    f.write('\n')
PY

echo "Starting Client at ${CLIENT_URL}..."
(
  cd "$ROOT_DIR"
  dotnet run --project src/MahjongClaw.Client --no-launch-profile --urls "$CLIENT_URL"
) >"${client_log}" 2>&1 &
client_pid=$!

wait_for_http "$CLIENT_URL" "Client" "$client_pid"

cd "$ROOT_DIR"
if [[ "${PLAYWRIGHT_SKIP_NPM_INSTALL:-0}" == "1" ]]; then
  echo "Skipping npm dependency install (PLAYWRIGHT_SKIP_NPM_INSTALL=1)."
else
  echo "Installing Playwright dependencies..."
  if [[ -f "package-lock.json" ]]; then
    npm ci --include=dev
  else
    npm install --include=dev
  fi
fi

echo "Running Playwright smoke suite..."
PLAYWRIGHT_CMD="${PLAYWRIGHT_E2E_COMMAND:-npx playwright test}"
PLAYWRIGHT_BASE_URL="$CLIENT_URL" PLAYWRIGHT_API_URL="$API_URL" bash -lc "$PLAYWRIGHT_CMD"
