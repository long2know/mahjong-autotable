#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

FORMAT="text"
if [[ "${1:-}" == "--json" ]]; then
  FORMAT="json"
fi

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

if [[ "$FORMAT" == "json" ]]; then
  printf '{\n'
  printf '  "timestamp_utc": "%s",\n' "$timestamp_utc"
  printf '  "gate_status": "%s",\n' "$gate_status"
  printf '  "gate_message": "%s",\n' "${gate_raw//\"/\\\"}"
  printf '  "branches": [\n'

  for i in "${!CANDIDATES[@]}"; do
    branch="${CANDIDATES[$i]}"
    remote_ref="origin/${branch}"

    if git show-ref --verify --quiet "refs/remotes/${remote_ref}"; then
      counts="$(git rev-list --left-right --count "origin/main...${remote_ref}")"
      behind="$(awk '{print $1}' <<<"$counts")"
      ahead="$(awk '{print $2}' <<<"$counts")"
      printf '    {"ref":"%s","state":"PRESENT","behind":%s,"ahead":%s}' "$remote_ref" "$behind" "$ahead"
    else
      printf '    {"ref":"%s","state":"MISSING"}' "$remote_ref"
    fi

    if [[ "$i" -lt $((${#CANDIDATES[@]} - 1)) ]]; then
      printf ','
    fi
    printf '\n'
  done

  printf '  ]\n'
  printf '}\n'
  exit 0
fi

printf 'Branch retention status snapshot (%s)\n' "$timestamp_utc"
printf 'Gate: %s\n' "$gate_status"
printf '%s\n' "$gate_raw"

for branch in "${CANDIDATES[@]}"; do
  remote_ref="origin/${branch}"
  if git show-ref --verify --quiet "refs/remotes/${remote_ref}"; then
    counts="$(git rev-list --left-right --count "origin/main...${remote_ref}")"
    behind="$(awk '{print $1}' <<<"$counts")"
    ahead="$(awk '{print $2}' <<<"$counts")"
    printf '%s: behind %s / ahead %s\n' "$remote_ref" "$behind" "$ahead"
  else
    printf '%s: MISSING\n' "$remote_ref"
  fi
done
