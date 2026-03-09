#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

FORMAT="text"
OUT_PATH=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)
      FORMAT="json"
      shift
      ;;
    --out)
      if [[ $# -lt 2 ]]; then
        echo "ERROR: --out requires a path" >&2
        exit 1
      fi
      OUT_PATH="$2"
      shift 2
      ;;
    *)
      echo "ERROR: unrecognized argument '$1'" >&2
      exit 1
      ;;
  esac
done

CANDIDATES=(
  "pr/01-scaffold-solution"
  "pr/02-domain-rules-core"
  "pr/07-tests-docs"
)

timestamp_utc="$(date -u '+%Y-%m-%d %H:%M:%SZ')"

if gate_raw="$(./scripts/check_branch_retention_approval.sh 2>&1)"; then
  gate_status="APPROVED"
else
  gate_status="PENDING_OR_BLOCKED"
fi

git fetch --all --prune >/dev/null 2>&1

snapshot_output=""
if [[ "$FORMAT" == "json" ]]; then
  snapshot_output+="{\n"
  snapshot_output+="  \"timestamp_utc\": \"${timestamp_utc}\",\n"
  snapshot_output+="  \"gate_status\": \"${gate_status}\",\n"
  snapshot_output+="  \"gate_message\": \"${gate_raw//\"/\\\"}\",\n"
  snapshot_output+="  \"branches\": [\n"

  for i in "${!CANDIDATES[@]}"; do
    branch="${CANDIDATES[$i]}"
    remote_ref="origin/${branch}"

    if git show-ref --verify --quiet "refs/remotes/${remote_ref}"; then
      counts="$(git rev-list --left-right --count "origin/main...${remote_ref}")"
      behind="$(awk '{print $1}' <<<"$counts")"
      ahead="$(awk '{print $2}' <<<"$counts")"
      snapshot_output+="    {\"ref\":\"${remote_ref}\",\"state\":\"PRESENT\",\"behind\":${behind},\"ahead\":${ahead}}"
    else
      snapshot_output+="    {\"ref\":\"${remote_ref}\",\"state\":\"MISSING\"}"
    fi

    if [[ "$i" -lt $((${#CANDIDATES[@]} - 1)) ]]; then
      snapshot_output+=","
    fi
    snapshot_output+="\n"
  done

  snapshot_output+="  ]\n"
  snapshot_output+="}\n"
else
  snapshot_output+="Branch retention status snapshot (${timestamp_utc})\n"
  snapshot_output+="Gate: ${gate_status}\n"
  snapshot_output+="${gate_raw}\n"

  for branch in "${CANDIDATES[@]}"; do
    remote_ref="origin/${branch}"
    if git show-ref --verify --quiet "refs/remotes/${remote_ref}"; then
      counts="$(git rev-list --left-right --count "origin/main...${remote_ref}")"
      behind="$(awk '{print $1}' <<<"$counts")"
      ahead="$(awk '{print $2}' <<<"$counts")"
      snapshot_output+="${remote_ref}: behind ${behind} / ahead ${ahead}\n"
    else
      snapshot_output+="${remote_ref}: MISSING\n"
    fi
  done
fi

printf '%b' "$snapshot_output"

if [[ -n "$OUT_PATH" ]]; then
  mkdir -p "$(dirname "$OUT_PATH")"
  printf '%b' "$snapshot_output" > "$OUT_PATH"
fi
