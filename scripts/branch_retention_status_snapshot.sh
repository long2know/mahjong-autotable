#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

CANDIDATES=(
  "pr/01-scaffold-solution"
  "pr/02-domain-rules-core"
  "pr/07-tests-docs"
)

printf 'Branch retention status snapshot (%s)\n' "$(date -u '+%Y-%m-%d %H:%M:%SZ')"

if ./scripts/check_branch_retention_approval.sh; then
  gate_status="APPROVED"
else
  gate_status="PENDING_OR_BLOCKED"
fi

printf 'Gate: %s\n' "$gate_status"

git fetch --all --prune >/dev/null 2>&1

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
