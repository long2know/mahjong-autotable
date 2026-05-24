# Argo Rollouts — frontend Canary strategy (W21)

> Phase K Wave 21 — Apone (DevOps).
> Audience: SRE / on-call operator landing the W21 frontend
> Canary template.
> Companion to [`docs/argo-rollouts-backend-bluegreen.md`](./argo-rollouts-backend-bluegreen.md)
> (the W20 backend BlueGreen runbook). The two strategy
> templates coexist — frontend uses Canary, backend uses
> BlueGreen — and share the W19 controller install.

## 1. What landed at W21

A single new manifest plus a runbook entry:

| File | Purpose |
| --- | --- |
| `infra/k8s/base/argo-rollouts/frontend-canary.yaml` | Rollout CR + AnalysisTemplate + preview Service for the frontend |
| `docs/argo-rollouts-frontend-canary.md` (this file) | Operator runbook |

The Rollout shape:

* 4 explicit weight steps: **5% → 25% → 50% → 100%**.
* **10 minute pause** between each step.
* An **AnalysisRun gate** between each pause and the next
  setWeight — error rate must stay below 0.5% across 38 of
  40 samples (15s interval × 40 = 10 minutes).
* Total nominal duration: **30 minutes** from first canary
  pod Ready to 100% promotion.

## 2. Why Canary (not BlueGreen) for the frontend

The frontend's traffic shape is different from the backend's
request-API workload:

| Workload | Strategy | Why |
| --- | --- | --- |
| Backend (request-API) | **BlueGreen** (W20) | Tight cutover; schema-coupled releases; P95-latency-gated; manual review of preview Service before promotion |
| Frontend (static / PWA) | **Canary** (W21) | Asset-shift release; CDN warming benefits from a stepwise ramp; error-rate-gated; the chunk-hash shape from W11 ShaderChunk surgery favours gradual cache-population |

W9 already wired a **backend Canary** alongside the W20
BlueGreen (operator opt-in via the helm chart's
`deploymentStrategy` value). W21 introduces a **frontend
Canary** for symmetry — every workload has at least one
strategy template wired by W21.

## 3. The AnalysisTemplate — `frontend-canary-error-rate`

The analysis polls Prometheus every 15s for 40 iterations
(10 minutes total). The Prometheus query:

```promql
sum(rate(
  nginx_ingress_controller_requests{
    service="mahjong-autotable-frontend-canary",
    status=~"5..|4[0-9][0-9]"
  }[1m]
))
/
sum(rate(
  nginx_ingress_controller_requests{
    service="mahjong-autotable-frontend-canary"
  }[1m]
))
```

Pass criterion: error rate **< 0.005** (0.5%) for **38 of
40 samples**.

Tunables:

* `error-rate-threshold` arg (default `0.005`) — bump via
  a kustomize patch when a known-noisy release is rolling
  (e.g. a deliberate 404 on a deprecated path).
* `failureLimit: 2` — absorbs single noisy intervals.
* `inconclusiveLimit: 4` — tolerates Prometheus NaN
  returns during traffic lulls.

Why 0.5%: the W17 LH13 baseline observed a 0.12% error
rate over a 24h prod-traffic sample. 0.5% gives a ~4×
safety margin while still catching a regression (e.g.
chunk-hash mismatch causing 100% asset failure on the
canary pods would push the rate above 1% within the first
minute — analysis aborts).

## 4. Operator runbook — landing a frontend release

### 4.1 Pre-flight

```bash
# 1. Confirm the W19 controller is healthy:
kubectl -n argo-rollouts get pods
kubectl -n argo-rollouts get deploy argo-rollouts -o jsonpath='{.status.readyReplicas}/{.status.replicas}'

# 2. Confirm the W21 manifest is applied:
kubectl get rollouts mahjong-autotable-frontend -o jsonpath='{.status.phase}'
# Expect: Healthy
```

### 4.2 Trigger a canary roll

```bash
# Bump the image tag — same shape as a normal Deployment.
kubectl argo rollouts set image mahjong-autotable-frontend \
  web=ghcr.io/long2know/mahjong-autotable-frontend:NEW_TAG

# Watch the progress:
kubectl argo rollouts get rollout mahjong-autotable-frontend --watch
```

You will see the rollout transit through:

```
Step 1/7: setWeight 5  → step active   (5% traffic on canary ReplicaSet)
Step 2/7: pause 10m     → paused        (operator can inspect)
Step 3/7: analysis      → running       (AnalysisRun in progress)
Step 4/7: setWeight 25
...
```

### 4.3 Abort a bad canary

```bash
kubectl argo rollouts abort mahjong-autotable-frontend
```

The controller scales the canary ReplicaSet back to 0 after
the `abortScaleDownDelaySeconds` (30s) window. The stable
ReplicaSet retains 100% of traffic.

### 4.4 Force-promote (skip the remaining pauses)

```bash
kubectl argo rollouts promote --full mahjong-autotable-frontend
```

This skips ALL pending pauses + analysis runs. Use only
after manual inspection of the canary pod's logs +
`/index.html` smoke test.

## 5. Cross-references

* [`infra/k8s/base/argo-rollouts/frontend-canary.yaml`](../infra/k8s/base/argo-rollouts/frontend-canary.yaml)
  — the manifest documented here.
* [`infra/k8s/base/argo-rollouts/backend-bluegreen.yaml`](../infra/k8s/base/argo-rollouts/backend-bluegreen.yaml)
  — W20 backend BlueGreen companion.
* [`docs/argo-rollouts-install-runbook.md`](./argo-rollouts-install-runbook.md)
  — W19 controller install (prereq).
* [`docs/argo-rollouts-backend-bluegreen.md`](./argo-rollouts-backend-bluegreen.md)
  — W20 backend runbook (parallel shape).
* [`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml)
  — W8/W9 helm-rendered backend canary (legacy path).

## 6. W21 → W22 hand-off

W22 candidate work:

* Per-tenant canary thresholds — different tenants tolerate
  different error rates (a tier-1 enterprise tenant might
  page on 0.1% while a tier-3 free tenant tolerates 1%).
  Requires per-tenant Service splits — out of scope for W21.
* AnalysisRun history retention — the W19 controller install
  defaulted to 5 runs retained; W22 may bump this to 20 for
  multi-day forensic windows.
* Auto-rollback alert — fire `SignalrChurnCritical`-shape
  alert when the AnalysisRun fails 3-in-a-row. Today the
  operator notices via the Rollouts dashboard or a missing
  promotion in the deploy log.
