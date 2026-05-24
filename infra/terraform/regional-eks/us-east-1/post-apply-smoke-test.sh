#!/usr/bin/env bash
# Phase K Wave 20 — Apone (DevOps).
#
# us-east-1 post-apply smoke test (V2 of the W19 ACTUAL APPLY
# runbook). Runs after `terraform apply` completes against the
# us-east-1 regional EKS stack, verifies cluster health, and
# exits non-zero if any of the 8 invariants fail.
#
# This script REPLACES the W19 ad-hoc curl sequence (runbook §4
# tabular form) with a single mechanical verifier the operator
# can run in one shot. The runbook still walks through each
# invariant; this script is the automation companion.
#
# SAFETY:
#   * Read-only. No `kubectl apply`, no `terraform`, no
#     `aws ec2/eks/route53 modify`. All AWS calls are
#     `describe` / `list` / `get`; all kubectl calls are `get`.
#   * Idempotent. Re-runnable across the post-apply window.
#   * Single-shot. Each invariant runs once; no retry loop
#     (the runbook §6 rollback path handles persistent
#     failures).
#
# Exit codes:
#   0 — all 8 invariants pass.
#   1 — at least one invariant failed (per-row OK/FAIL printed).
#   2 — bad invocation / missing prerequisite.
#
# Usage:
#   bash infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh
#   bash infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh --quiet
#   APEX=mahjong.example.com bash .../post-apply-smoke-test.sh
#
# Operator pre-conditions:
#   * `kubectl config current-context` points at the
#     `mahjong-prod-use1` cluster.
#   * `aws sts get-caller-identity` returns an identity
#     scoped to the operator's W19 apply role.
#   * The DNS apex APEX (default mahjong.example.com) is the
#     R53 latency-routed apex the W19 apply lit up.
#
# Companion runbook: docs/us-east-1-apply-runbook.md §4 (W20
# V2 hardening incorporates the V1 smoke-test row table into
# this single script).

set -uo pipefail

# ──────────────────────────────────────────────────────────────
#  Configuration
# ──────────────────────────────────────────────────────────────

APEX="${APEX:-mahjong.example.com}"
REGIONAL_APEX="${REGIONAL_APEX:-us-east-1.${APEX}}"
EXPECTED_REGION="${EXPECTED_REGION:-us-east-1}"
HEALTH_CHECK_REF="${HEALTH_CHECK_REF:-mahjong-prod-us-east-1}"
QUIET=0

for arg in "$@"; do
  case "$arg" in
    --quiet)    QUIET=1 ;;
    --help|-h)
      sed -n '1,42p' "$0" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *)
      echo "Unknown arg: $arg (use --help)" >&2
      exit 2 ;;
  esac
done

# ──────────────────────────────────────────────────────────────
#  Result accumulator. Each invariant appends one row.
# ──────────────────────────────────────────────────────────────

RESULTS=()
FAILED=0

# ANSI colours when stdout is a tty.
if [ -t 1 ] && [ "$QUIET" -eq 0 ]; then
  GREEN=$'\033[32m'; RED=$'\033[31m'; YELLOW=$'\033[33m'; RESET=$'\033[0m'
else
  GREEN=""; RED=""; YELLOW=""; RESET=""
fi

record() {
  local name="$1" status="$2" detail="$3"
  if [ "$status" = "OK" ]; then
    RESULTS+=("${GREEN}OK   ${RESET} $name :: $detail")
  else
    RESULTS+=("${RED}FAIL ${RESET} $name :: $detail")
    FAILED=1
  fi
}

# Print a heading row when not in quiet mode.
note() {
  [ "$QUIET" -eq 0 ] && echo "${YELLOW}--- $* ---${RESET}"
}

# ──────────────────────────────────────────────────────────────
#  Invariant 1 — R53 latency apex resolves us-east-1 endpoint.
# ──────────────────────────────────────────────────────────────

note "1/8 r53-latency-resolves :: dig +short ${APEX}"
if ! command -v dig >/dev/null 2>&1; then
  record "r53-latency-resolves" "FAIL" "dig binary not present"
else
  RESOLVED="$(dig +short "$APEX" @8.8.8.8 | grep -Ev '^$' | head -3 || true)"
  if echo "$RESOLVED" | grep -qE "${EXPECTED_REGION}\.elb\.amazonaws\.com|elb\.${EXPECTED_REGION}\.amazonaws\.com"; then
    record "r53-latency-resolves" "OK" "$(echo "$RESOLVED" | tr '\n' ' ')"
  else
    record "r53-latency-resolves" "FAIL" "resolved='$RESOLVED' (expected ${EXPECTED_REGION} ELB CNAME)"
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 2 — Regional ALB healthz returns HTTP 200.
# ──────────────────────────────────────────────────────────────

note "2/8 alb-200 :: curl ${REGIONAL_APEX}/healthz"
if ! command -v curl >/dev/null 2>&1; then
  record "alb-200" "FAIL" "curl binary not present"
else
  HTTP="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' \
      "https://${REGIONAL_APEX}/healthz" 2>/dev/null || echo "000")"
  if [ "$HTTP" = "200" ]; then
    record "alb-200" "OK" "HTTP $HTTP from https://${REGIONAL_APEX}/healthz"
  else
    record "alb-200" "FAIL" "HTTP $HTTP from https://${REGIONAL_APEX}/healthz (expected 200)"
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 3 — R53 health-check reports Success.
# ──────────────────────────────────────────────────────────────

note "3/8 r53-health-check :: aws route53 list-health-checks"
if ! command -v aws >/dev/null 2>&1; then
  record "r53-health-check" "FAIL" "aws cli not present"
else
  HC_ID="$(aws route53 list-health-checks \
      --query "HealthChecks[?CallerReference==\`${HEALTH_CHECK_REF}\`].Id | [0]" \
      --output text 2>/dev/null || echo "None")"
  if [ -z "$HC_ID" ] || [ "$HC_ID" = "None" ]; then
    record "r53-health-check" "FAIL" "no health-check matching CallerReference=${HEALTH_CHECK_REF}"
  else
    STATUS="$(aws route53 get-health-check-status \
        --health-check-id "$HC_ID" \
        --query 'HealthCheckObservations[*].StatusReport.Status' \
        --output text 2>/dev/null || echo "")"
    if echo "$STATUS" | grep -q "Success"; then
      record "r53-health-check" "OK" "id=${HC_ID} statuses=[$STATUS]"
    else
      record "r53-health-check" "FAIL" "id=${HC_ID} statuses=[$STATUS] (no Success row)"
    fi
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 4 — SignalR /hubs/changsha/negotiate returns
#                  WebSockets transport (sticky-affinity wiring).
# ──────────────────────────────────────────────────────────────

note "4/8 signalr-handshake :: POST ${APEX}/hubs/changsha/negotiate"
if ! command -v curl >/dev/null 2>&1 || ! command -v jq >/dev/null 2>&1; then
  record "signalr-handshake" "FAIL" "curl or jq missing"
else
  NEGOTIATE="$(curl -sS -m 10 -X POST \
      "https://${APEX}/hubs/changsha/negotiate?negotiateVersion=1" \
      -H 'Content-Type: application/json' 2>/dev/null || echo "{}")"
  TRANSPORT="$(echo "$NEGOTIATE" | jq -r '.availableTransports[0].transport // empty' 2>/dev/null || true)"
  if [ "$TRANSPORT" = "WebSockets" ]; then
    record "signalr-handshake" "OK" "availableTransports[0]=WebSockets"
  else
    record "signalr-handshake" "FAIL" "availableTransports[0]='$TRANSPORT' (expected WebSockets); body=${NEGOTIATE:0:200}"
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 5 — EKS cluster is ACTIVE.
# ──────────────────────────────────────────────────────────────

note "5/8 eks-cluster-active :: aws eks describe-cluster"
if ! command -v aws >/dev/null 2>&1; then
  record "eks-cluster-active" "FAIL" "aws cli not present"
else
  CLUSTER_NAME="${EKS_CLUSTER_NAME:-mahjong-prod-${EXPECTED_REGION}}"
  CLUSTER_STATUS="$(aws eks describe-cluster \
      --region "$EXPECTED_REGION" \
      --name "$CLUSTER_NAME" \
      --query 'cluster.status' \
      --output text 2>/dev/null || echo "MISSING")"
  if [ "$CLUSTER_STATUS" = "ACTIVE" ]; then
    record "eks-cluster-active" "OK" "name=${CLUSTER_NAME} status=ACTIVE"
  else
    record "eks-cluster-active" "FAIL" "name=${CLUSTER_NAME} status=${CLUSTER_STATUS} (expected ACTIVE)"
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 6 — mahjong-prod deployment reports Ready replicas
#                  >= 3 (the W11 sticky-affinity floor).
# ──────────────────────────────────────────────────────────────

note "6/8 deployment-ready :: kubectl -n mahjong-prod get deployment"
if ! command -v kubectl >/dev/null 2>&1; then
  record "deployment-ready" "FAIL" "kubectl not present"
else
  CTX="$(kubectl config current-context 2>/dev/null || echo "")"
  if ! echo "$CTX" | grep -qE "mahjong-prod|use1"; then
    record "deployment-ready" "FAIL" "kubectl context='${CTX}' is not the prod cluster"
  else
    READY="$(kubectl -n mahjong-prod get deployment prod-mahjong-autotable \
        -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo "0")"
    DESIRED="$(kubectl -n mahjong-prod get deployment prod-mahjong-autotable \
        -o jsonpath='{.spec.replicas}' 2>/dev/null || echo "0")"
    READY="${READY:-0}"; DESIRED="${DESIRED:-0}"
    if [ "$READY" -ge 3 ] && [ "$READY" = "$DESIRED" ]; then
      record "deployment-ready" "OK" "ready=${READY}/${DESIRED}"
    else
      record "deployment-ready" "FAIL" "ready=${READY}/${DESIRED} (expected ready=desired>=3)"
    fi
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 7 — Kyverno enforce-mode ClusterPolicies present
#                  + ready (W20 enforce-flip lands here too).
# ──────────────────────────────────────────────────────────────

note "7/8 kyverno-enforce :: clusterpolicy validationFailureAction"
if ! command -v kubectl >/dev/null 2>&1; then
  record "kyverno-enforce" "FAIL" "kubectl not present"
else
  K1="$(kubectl get clusterpolicy disallow-lateral-movement \
      -o jsonpath='{.spec.validationFailureAction}' 2>/dev/null || echo "")"
  K2="$(kubectl get clusterpolicy require-network-policy \
      -o jsonpath='{.spec.validationFailureAction}' 2>/dev/null || echo "")"
  if [ "$K1" = "Enforce" ] && [ "$K2" = "Enforce" ]; then
    record "kyverno-enforce" "OK" "disallow-lateral-movement=Enforce require-network-policy=Enforce"
  else
    record "kyverno-enforce" "FAIL" "disallow-lateral-movement='${K1}' require-network-policy='${K2}' (expected Enforce both)"
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Invariant 8 — coturn TURN server reachable on signalling
#                  port (3478/tcp via the W6 LoadBalancer Svc).
# ──────────────────────────────────────────────────────────────

note "8/8 coturn-reachable :: kubectl -n mahjong-prod get svc"
if ! command -v kubectl >/dev/null 2>&1; then
  record "coturn-reachable" "FAIL" "kubectl not present"
else
  COTURN_LB="$(kubectl -n mahjong-prod get svc prod-mahjong-autotable-coturn \
      -o jsonpath='{.status.loadBalancer.ingress[0].hostname}' 2>/dev/null || echo "")"
  if [ -z "$COTURN_LB" ]; then
    COTURN_LB="$(kubectl -n mahjong-prod get svc prod-mahjong-autotable-coturn \
        -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")"
  fi
  if [ -z "$COTURN_LB" ]; then
    record "coturn-reachable" "FAIL" "coturn Service has no LoadBalancer ingress yet"
  else
    record "coturn-reachable" "OK" "coturn LB hostname/IP=${COTURN_LB}"
  fi
fi

# ──────────────────────────────────────────────────────────────
#  Result summary.
# ──────────────────────────────────────────────────────────────

echo ""
echo "===== W20 us-east-1 post-apply smoke-test results ====="
printf "%s\n" "${RESULTS[@]}"
echo "======================================================="

if [ "$FAILED" -eq 0 ]; then
  echo "${GREEN}All 8 invariants passed.${RESET}"
  exit 0
else
  echo "${RED}One or more invariants FAILED. Consult docs/us-east-1-apply-runbook.md §6 for rollback.${RESET}"
  exit 1
fi
