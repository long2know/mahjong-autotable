#!/usr/bin/env bash
set -euo pipefail

REQUEST_FILE="${REQUEST_FILE:-BRANCH_RETENTION_CONFIRMATION_REQUEST.md}"

if [[ ! -f "$REQUEST_FILE" ]]; then
  echo "BLOCKED: request file not found: $REQUEST_FILE"
  exit 1
fi

status_line="$(grep -E '^Status:' "$REQUEST_FILE" | head -n1 || true)"
decision_line="$(grep -E '^Decision:' "$REQUEST_FILE" | head -n1 || true)"
requested_line="$(grep -E '^Timestamp:' "$REQUEST_FILE" | head -n1 || true)"

status="${status_line#Status: }"
decision="${decision_line#Decision: }"
requested="${requested_line#Timestamp: }"

status="$(echo "$status" | xargs || true)"
decision="$(echo "$decision" | xargs || true)"
requested="$(echo "$requested" | xargs || true)"

if [[ "$status" != "APPROVED" ]]; then
  if [[ -n "$requested" ]]; then
    now_epoch="$(date +%s)"
    req_epoch="$(date -d "$requested" +%s 2>/dev/null || echo "")"
    if [[ -n "$req_epoch" ]]; then
      age_hours=$(( (now_epoch - req_epoch) / 3600 ))
      echo "PENDING: stakeholder decision not recorded (requested $requested, age ${age_hours}h)"
    else
      echo "PENDING: stakeholder decision not recorded (requested $requested)"
    fi
  else
    echo "PENDING: stakeholder decision not recorded"
  fi
  exit 1
fi

if [[ -z "$decision" ]]; then
  echo "BLOCKED: status APPROVED but Decision is empty"
  exit 1
fi

case "$decision" in
  "APPROVE ALL"|"APPROVE NONE"|APPROVE:*)
    echo "APPROVED: $decision"
    exit 0
    ;;
  *)
    echo "BLOCKED: unrecognized Decision '$decision'"
    exit 1
    ;;
esac
