#!/usr/bin/env bash
set -euo pipefail

SOAK_API_URL="${SOAK_API_URL:-http://127.0.0.1:5270}"
SOAK_HANDS="${SOAK_HANDS:-3}"
SOAK_MAX_SECONDS_PER_HAND="${SOAK_MAX_SECONDS_PER_HAND:-45}"
SOAK_POLL_INTERVAL_SECONDS="${SOAK_POLL_INTERVAL_SECONDS:-0.2}"
SOAK_START_API="${SOAK_START_API:-1}"
SOAK_SKIP_DB_MIGRATIONS="${SOAK_SKIP_DB_MIGRATIONS:-0}"
SOAK_LOG_DIR="${SOAK_LOG_DIR:-artifacts/soak}"

mkdir -p "${SOAK_LOG_DIR}"

API_PID=""

cleanup() {
  if [ -n "${API_PID}" ] && kill -0 "${API_PID}" >/dev/null 2>&1; then
    kill "${API_PID}" >/dev/null 2>&1 || true
    wait "${API_PID}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

json_field() {
  local field_path="$1"
  python3 -c '
import json
import sys

field_path = sys.argv[1].split(".")
payload = json.loads(sys.stdin.read())
current = payload
for segment in field_path:
    if isinstance(current, dict):
        current = current.get(segment)
    else:
        current = None
        break

if current is None:
    sys.exit(1)

print(current)
' "$field_path"
}

wait_for_api() {
  local attempts=0
  local max_attempts=120
  until curl -fsS "${SOAK_API_URL}/" >/dev/null 2>&1; do
    attempts=$((attempts + 1))
    if [ "${attempts}" -ge "${max_attempts}" ]; then
      echo "API did not become ready at ${SOAK_API_URL} within ${max_attempts}s." >&2
      return 1
    fi
    sleep 1
  done
}

ensure_dotnet_ef() {
  if dotnet ef --version >/dev/null 2>&1; then
    return 0
  fi

  if dotnet tool restore >/dev/null 2>&1 && dotnet ef --version >/dev/null 2>&1; then
    return 0
  fi

  dotnet tool install --global dotnet-ef >/dev/null 2>&1
}

if [ "${SOAK_START_API}" = "1" ]; then
  if [ "${SOAK_SKIP_DB_MIGRATIONS}" != "1" ]; then
    ensure_dotnet_ef
    dotnet restore MahjongClaw.slnx >"${SOAK_LOG_DIR}/migrate.log" 2>&1
    ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
      --project src/MahjongClaw.Persistence \
      --startup-project src/MahjongClaw.Api >>"${SOAK_LOG_DIR}/migrate.log" 2>&1
  fi

  dotnet run --project src/MahjongClaw.Api -c Release --no-build --urls "${SOAK_API_URL}" >"${SOAK_LOG_DIR}/api.log" 2>&1 &
  API_PID=$!
  wait_for_api
fi

total_elapsed_seconds=0
printf "Running %s soak hand(s) against %s\n" "${SOAK_HANDS}" "${SOAK_API_URL}"

for hand_index in $(seq 1 "${SOAK_HANDS}"); do
  create_payload=$(cat <<JSON
{"hostName":"Bot SoakHost${hand_index}","mode":"HumanVsBot","botSeatCount":3,"ruleset":"Changsha258"}
JSON
)

  create_response=$(curl -fsS \
    -X POST "${SOAK_API_URL}/api/game" \
    -H "Content-Type: application/json" \
    -d "${create_payload}")

  game_id=$(printf '%s' "${create_response}" | json_field "id")
  if [ -z "${game_id}" ]; then
    echo "Failed to parse game id for soak hand ${hand_index}" >&2
    exit 1
  fi

  curl -fsS -X POST "${SOAK_API_URL}/api/game/${game_id}/start" >/dev/null

  started_at=$(date +%s)
  deadline=$((started_at + SOAK_MAX_SECONDS_PER_HAND))
  final_revision=""
  completion_reason=""
  last_revision=""
  stagnant_ticks=0
  max_stagnant_ticks=150

  while true; do
    state_response=$(curl -fsS "${SOAK_API_URL}/api/game/${game_id}/state")
    state_status=$(printf '%s' "${state_response}" | json_field "status")
    final_revision=$(printf '%s' "${state_response}" | json_field "stateRevision" || true)
    wall_total=$(printf '%s' "${state_response}" | json_field "wallTotal")
    tiles_in_play=$(printf '%s' "${state_response}" | json_field "tilesInPlay")
    wall_remaining=$((wall_total - tiles_in_play))

    if [ "${state_status}" = "Completed" ]; then
      completion_reason="win"
      break
    fi

    if [ "${wall_remaining}" -le 0 ]; then
      completion_reason="wall-exhausted"
      break
    fi

    if [ -n "${final_revision}" ] && [ "${final_revision}" = "${last_revision}" ]; then
      stagnant_ticks=$((stagnant_ticks + 1))
    else
      stagnant_ticks=0
      last_revision="${final_revision}"
    fi

    if [ "${stagnant_ticks}" -ge "${max_stagnant_ticks}" ]; then
      echo "Soak hand ${hand_index} stalled with no stateRevision changes (game ${game_id})." >&2
      exit 1
    fi

    if [ "$(date +%s)" -ge "${deadline}" ]; then
      echo "Soak hand ${hand_index} timed out after ${SOAK_MAX_SECONDS_PER_HAND}s (game ${game_id})." >&2
      exit 1
    fi

    curl -fsS -X POST "${SOAK_API_URL}/api/game/${game_id}/bot/turn" >/dev/null 2>&1 || true
    sleep "${SOAK_POLL_INTERVAL_SECONDS}"
  done

  ended_at=$(date +%s)
  elapsed_seconds=$((ended_at - started_at))
  total_elapsed_seconds=$((total_elapsed_seconds + elapsed_seconds))

  printf "Hand %s/%s resolved in %ss (%s, stateRevision=%s)\n" \
    "${hand_index}" "${SOAK_HANDS}" "${elapsed_seconds}" "${completion_reason:-unknown}" "${final_revision:-unknown}"
done

average_seconds=$(python3 - <<PY
total_seconds = ${total_elapsed_seconds}
hand_count = ${SOAK_HANDS}
print(f"{(total_seconds / hand_count):.2f}")
PY
)

summary_line="SOAK_RESULT hands=${SOAK_HANDS} total_seconds=${total_elapsed_seconds} average_seconds=${average_seconds}"
printf "%s\n" "${summary_line}" | tee "${SOAK_LOG_DIR}/summary.txt"
