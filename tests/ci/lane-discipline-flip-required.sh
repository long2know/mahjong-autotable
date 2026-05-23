#!/usr/bin/env bash
# Phase K Wave 13 — Vasquez (QA).
#
# Canonical executable for flipping the `lane-discipline / check`
# status check on `main` from *advisory* to *required-for-merge*.
#
# The §4.1 hand-off in `docs/agent-handoff-protocol.md` has been
# standing since Phase K Wave 4 (~9 weeks). The W13 escalation
# proposal in §4.2 codifies this script so the coordinator (or
# Stephen, on authorisation) can flip the gate in a single
# idempotent invocation.
#
# Usage:
#   tests/ci/lane-discipline-flip-required.sh [--dry-run|--rollback|--coordinator-flag]
#
# Flags:
#   --dry-run             Print the would-be PATCH payload + skip the API call.
#   --rollback            Remove `lane-discipline / check` from required checks
#                         (use only if a false-positive blocks legitimate work).
#   --coordinator-flag    Coordinator-direct mode (W14 escalation path); records
#                         caller as coordinator in the audit log.
#   --reason "<text>"     Free-text reason for the audit log (recommended for
#                         --coordinator-flag and --rollback).
#
# Exit codes:
#   0 — flip (or rollback) succeeded; round-trip verification passed.
#   1 — flip failed (API error, verification mismatch).
#   2 — pre-flight failed (missing gh, no admin scope, etc.).
#   3 — invalid invocation (bad flag combination).
#
# Author identity: Vasquez (QA). The script lives under
# `tests/ci/` which is in Vasquez's lane (per
# `tests/ci/lane-map.json`); coordinator-direct invocation does
# NOT change the lane-discipline classification of edits to this
# file.

set -euo pipefail

REPO_OWNER="long2know"
REPO_NAME="mahjong-autotable"
REPO="${REPO_OWNER}/${REPO_NAME}"
TARGET_BRANCH="main"
LANE_CHECK="lane-discipline / check"
AUDIT_LOG="docs/audits/branch-protection-flips.md"

MODE="apply"
COORDINATOR_FLAG=0
REASON=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run)
      MODE="dry-run"
      shift
      ;;
    --rollback)
      MODE="rollback"
      shift
      ;;
    --coordinator-flag)
      COORDINATOR_FLAG=1
      shift
      ;;
    --reason)
      shift
      [[ $# -gt 0 ]] || { echo "ERR: --reason requires a value" >&2; exit 3; }
      REASON="$1"
      shift
      ;;
    --help|-h)
      sed -n '2,32p' "$0"
      exit 0
      ;;
    *)
      echo "ERR: unknown flag: $1" >&2
      exit 3
      ;;
  esac
done

# ─── Pre-flight ──────────────────────────────────────────────────────

if ! command -v gh >/dev/null 2>&1; then
  echo "ERR: gh CLI not on PATH. Install gh or use \$PATH including its bin dir." >&2
  exit 2
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "ERR: jq not on PATH. The script uses jq for response parsing." >&2
  exit 2
fi

if [[ "$MODE" != "dry-run" ]]; then
  # Confirm gh has an auth token AND that token has admin scope.
  if ! gh auth status >/dev/null 2>&1; then
    echo "ERR: gh is not authenticated. Run 'gh auth login' first." >&2
    exit 2
  fi
fi

# ─── Read current state ──────────────────────────────────────────────

echo "→ Reading current branch protection for ${REPO}:${TARGET_BRANCH}…"
CURRENT_JSON=""
if [[ "$MODE" != "dry-run" ]]; then
  if ! CURRENT_JSON=$(gh api "repos/${REPO}/branches/${TARGET_BRANCH}/protection" 2>/dev/null); then
    echo "ERR: failed to read branch protection. Confirm \$GH_TOKEN has repo:admin scope." >&2
    exit 2
  fi
fi

# Compute contexts list — preserve existing checks + add lane-discipline,
# OR remove lane-discipline (rollback mode).
if [[ -n "$CURRENT_JSON" ]]; then
  CURRENT_CTX=$(printf '%s' "$CURRENT_JSON" | jq -r '.required_status_checks.contexts // [] | .[]' 2>/dev/null || true)
else
  CURRENT_CTX=""
fi

NEW_CTX=()
HAVE_LANE=0
while IFS= read -r ctx; do
  [[ -z "$ctx" ]] && continue
  if [[ "$ctx" == "$LANE_CHECK" ]]; then
    HAVE_LANE=1
    if [[ "$MODE" == "rollback" ]]; then
      continue
    fi
  fi
  NEW_CTX+=("$ctx")
done <<<"$CURRENT_CTX"

if [[ "$MODE" == "apply" ]] && [[ $HAVE_LANE -eq 0 ]]; then
  NEW_CTX+=("$LANE_CHECK")
fi

# Build PATCH payload (-F contexts[]=... is the canonical idempotent form).
PATCH_FLAGS=()
for c in "${NEW_CTX[@]}"; do
  PATCH_FLAGS+=("-F" "contexts[]=${c}")
done

echo "→ Mode: ${MODE}"
echo "→ Have lane-discipline currently: $([[ $HAVE_LANE -eq 1 ]] && echo yes || echo no)"
echo "→ Resulting contexts:"
for c in "${NEW_CTX[@]}"; do
  echo "    - $c"
done

# ─── Dry-run: stop here ──────────────────────────────────────────────

if [[ "$MODE" == "dry-run" ]]; then
  echo "→ Dry-run: would execute:"
  echo "    gh api -X PATCH repos/${REPO}/branches/${TARGET_BRANCH}/protection/required_status_checks ${PATCH_FLAGS[*]}"
  exit 0
fi

# ─── Apply (or rollback) ─────────────────────────────────────────────

if [[ "$MODE" == "apply" ]] && [[ $HAVE_LANE -eq 1 ]]; then
  echo "→ No-op: lane-discipline / check is already required. Exiting 0."
  exit 0
fi

if [[ "$MODE" == "rollback" ]] && [[ $HAVE_LANE -eq 0 ]]; then
  echo "→ No-op: lane-discipline / check was already not required. Exiting 0."
  exit 0
fi

echo "→ Issuing PATCH…"
if ! gh api -X PATCH \
    "repos/${REPO}/branches/${TARGET_BRANCH}/protection/required_status_checks" \
    "${PATCH_FLAGS[@]}" >/dev/null 2>&1; then
  echo "ERR: PATCH failed. Re-running with verbose output:" >&2
  gh api -X PATCH \
      "repos/${REPO}/branches/${TARGET_BRANCH}/protection/required_status_checks" \
      "${PATCH_FLAGS[@]}" >&2 || true
  exit 1
fi

# ─── Verify ──────────────────────────────────────────────────────────

echo "→ Verifying round-trip…"
ROUNDTRIP=$(gh api "repos/${REPO}/branches/${TARGET_BRANCH}/protection/required_status_checks" \
    --jq '.contexts[]' 2>/dev/null || true)

ROUND_HAS_LANE=0
while IFS= read -r ctx; do
  [[ -z "$ctx" ]] && continue
  if [[ "$ctx" == "$LANE_CHECK" ]]; then
    ROUND_HAS_LANE=1
  fi
done <<<"$ROUNDTRIP"

case "$MODE" in
  apply)
    if [[ $ROUND_HAS_LANE -ne 1 ]]; then
      echo "ERR: post-flip verification failed: lane-discipline / check NOT in round-trip." >&2
      exit 1
    fi
    echo "✓ lane-discipline / check is now REQUIRED for merge on ${TARGET_BRANCH}."
    ;;
  rollback)
    if [[ $ROUND_HAS_LANE -ne 0 ]]; then
      echo "ERR: post-rollback verification failed: lane-discipline / check STILL in round-trip." >&2
      exit 1
    fi
    echo "✓ lane-discipline / check is now ADVISORY (rolled back) on ${TARGET_BRANCH}."
    ;;
esac

# ─── Append audit log ────────────────────────────────────────────────

mkdir -p "$(dirname "$AUDIT_LOG")"
if [[ ! -f "$AUDIT_LOG" ]]; then
  cat > "$AUDIT_LOG" <<EOF
# Branch protection flip audit log

Each row records a run of \`tests/ci/lane-discipline-flip-required.sh\`.
The log is append-only — never rewrite history; correct a mistaken
entry with a follow-up entry.

| Timestamp (UTC) | Mode | Caller | Coordinator? | Reason | Verified |
|-----------------|------|--------|--------------|--------|----------|
EOF
fi

CALLER="$(gh api user --jq '.login' 2>/dev/null || echo unknown)"
TS="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
COORD_TAG=$([[ $COORDINATOR_FLAG -eq 1 ]] && echo yes || echo no)
REASON_CELL="${REASON:-—}"
{
  echo "| ${TS} | ${MODE} | ${CALLER} | ${COORD_TAG} | ${REASON_CELL} | yes |"
} >> "$AUDIT_LOG"

echo "→ Audit log appended: ${AUDIT_LOG}"
echo "Done."
exit 0
