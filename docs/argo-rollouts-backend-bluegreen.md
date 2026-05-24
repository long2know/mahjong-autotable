# Argo Rollouts — Backend BlueGreen strategy runbook (W20)

> Phase K Wave 20 — Apone (DevOps).
> Audience: SRE / on-call operator promoting a release of the
> `mahjong-autotable` backend via the BlueGreen strategy.
> Companion to:
> [`docs/argo-rollouts-install-runbook.md`](./argo-rollouts-install-runbook.md)
> (the W19 controller install runbook — prereq for this doc),
> [`docs/argo-rollouts-setup.md`](./argo-rollouts-setup.md)
> (the W10 install-on-staging runbook), and
> [`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml)
> (the W9 Canary alternative strategy this BlueGreen path
> sits alongside).

W10 → W19 brought the Argo Rollouts controller from
"scaffolded helm install" to "operator-runbook-ready, prod
namespace + RBAC stubs pre-created". W20 adds the **BlueGreen
strategy template** for the backend Deployment, alongside
the existing W9 Canary strategy. The squad now has TWO
complementary promotion shapes:

* **Canary** (W9) — gradual traffic shift (5% → 25% → 50% →
  100%) gated on 3 Prometheus AnalysisTemplates (success-rate,
  p99-latency, error-budget). Use when the workload tolerates
  partial traffic on the new version.
* **BlueGreen** (W20) — instant 0% → 100% cutover after a 60s
  pre-promotion Prometheus analysis. Use when partial-traffic
  is unsafe (schema migrations, sticky-session shape changes,
  human-review-required releases).

## 1. When to choose BlueGreen over Canary

| Scenario                                                                           | Pick      |
| ---------------------------------------------------------------------------------- | --------- |
| Release adds a NEW endpoint, no schema change                                      | Canary    |
| Release performs an `ALTER TABLE` the OLD pods cannot SELECT through               | BlueGreen |
| Release shifts the SignalR sticky-session shape (W19 example)                      | BlueGreen |
| Release changes the auth token-encoding shape (would break in-flight tokens)       | BlueGreen |
| Release is a bug-fix-only patch with no schema / contract change                   | Canary    |
| Release needs operator review of `/build-info` before any user traffic            | BlueGreen |
| Release is a security patch the squad wants live ASAP, accepts canary risk         | Canary    |
| Operator wants a single "approve" gesture rather than a 4-step traffic-shift gate  | BlueGreen |

The default remains **Canary** — that's the W9 chart shape.
The BlueGreen path is an *opt-in*: the operator applies the
W20 Rollout manifest (`backend-bluegreen.yaml`) for the
specific release, then reverts to the Canary path on the
next ordinary release.

## 2. The Rollout shape

The W20 manifest lives at
[`infra/k8s/base/argo-rollouts/backend-bluegreen.yaml`](../infra/k8s/base/argo-rollouts/backend-bluegreen.yaml)
and declares three resources:

| # | Resource                | Name                                  | Purpose                                                          |
| - | ----------------------- | ------------------------------------- | ---------------------------------------------------------------- |
| 1 | `Rollout`               | `mahjong-autotable`                   | BlueGreen strategy CR (supersedes the W2 base Deployment)        |
| 2 | `AnalysisTemplate`      | `backend-bluegreen-prepromotion`      | 60s Prometheus P95-latency gate (pre + post promotion)           |
| 3 | `Service`               | `mahjong-autotable-preview`           | Preview Service (Rollout controller patches selector at runtime) |

### 2.1 Rollout strategy block

```yaml
strategy:
  blueGreen:
    activeService: mahjong-autotable                    # production traffic
    previewService: mahjong-autotable-preview           # green-side smoke
    autoPromotionEnabled: false                         # MANUAL review gate
    scaleDownDelaySeconds: 30                           # blue drain window
    scaleDownDelayRevisionLimit: 2                      # rollback depth
    abortScaleDownDelaySeconds: 30                      # abort symmetric
    prePromotionAnalysis:                               # 60s gate BEFORE promote
      templates:
        - templateName: backend-bluegreen-prepromotion
      args:
        - name: service-name
          value: mahjong-autotable-preview
    postPromotionAnalysis:                              # 60s gate AFTER promote
      templates:
        - templateName: backend-bluegreen-prepromotion
      args:
        - name: service-name
          value: mahjong-autotable
```

### 2.2 The 60s analysis run

The `backend-bluegreen-prepromotion` AnalysisTemplate polls
Prometheus every 10s for 6 iterations (60s wall-clock total):

* **Query:** `histogram_quantile(0.95, sum by (le) (rate(http_request_duration_seconds_bucket{service="{{args.service-name}}"}[1m]))) * 1000`
* **Pass criterion:** P95 ≤ 300ms (tunable via kustomize patch)
* **failureLimit: 1** — allows ONE blip; two failures abort
* **Pre-promotion target:** `mahjong-autotable-preview` Service
* **Post-promotion target:** `mahjong-autotable` Service

The threshold (300ms) is lower than the W9 Canary p99 500ms
threshold deliberately: BlueGreen sees 100% of traffic at
promote time, so the latency budget is tighter than the
canary's partial-traffic window.

## 3. Operator runbook

### 3.1 Pre-conditions

| # | Pre-condition                                            | Verify                                                            |
| - | -------------------------------------------------------- | ----------------------------------------------------------------- |
| 1 | Argo Rollouts controller is installed                    | `kubectl -n argo-rollouts get deployment argo-rollouts` is Ready  |
| 2 | The base Deployment is NOT actively managing pods        | `kubectl -n mahjong-prod get deployment prod-mahjong-autotable -o jsonpath='{.spec.replicas}'` → flip to `0` BEFORE Rollout apply |
| 3 | Operator holds the `rollouts-manager` ClusterRole        | `kubectl auth can-i patch rollouts.argoproj.io/v1alpha1 -n mahjong-prod` returns `yes` |
| 4 | Prometheus endpoint reachable from the analysis pod      | `kubectl -n mahjong-prod run --rm -it curl --image=curlimages/curl -- curl -sf http://prometheus.monitoring.svc.cluster.local:9090/-/healthy` |
| 5 | `kubectl-argo-rollouts` plugin installed on workstation  | `kubectl argo rollouts version`                                   |

### 3.2 Apply the BlueGreen manifest

```bash
# 1. Scale the W2 base Deployment to 0 (lets the Rollout
#    take over the pod-label selector). The Service stays
#    intact; the Rollout will patch its selector at apply
#    time.
kubectl -n mahjong-prod scale deployment prod-mahjong-autotable --replicas=0

# 2. Apply the Rollout CR + AnalysisTemplate + preview Service.
kubectl -n mahjong-prod apply -f infra/k8s/base/argo-rollouts/backend-bluegreen.yaml

# 3. Watch the rollout reach Healthy on the initial apply.
kubectl argo rollouts -n mahjong-prod get rollout mahjong-autotable --watch
```

Expected initial state — the Rollout creates the GREEN
ReplicaSet (revision 1), runs prePromotionAnalysis against
the preview Service, then PAUSES awaiting manual promote:

```
Name:            mahjong-autotable
Namespace:       mahjong-prod
Status:          ॥ Paused
Strategy:        BlueGreen
Replicas:        2/2/2 desired/current/available
...
ReplicaSets:
  ↳ mahjong-autotable-<hash>  active  preview  2/2/2  Healthy
```

### 3.3 Trigger a release

Push a new image to GHCR (the W2 pipeline). Patch the Rollout
to the new image:

```bash
kubectl argo rollouts -n mahjong-prod set image mahjong-autotable \
    api=ghcr.io/long2know/mahjong-autotable:v0.29.0
```

The controller:

1. Creates a NEW (GREEN) ReplicaSet with the new image.
2. Patches the `mahjong-autotable-preview` Service selector to
   point at the new ReplicaSet's pod-template-hash.
3. Runs the 60s `prePromotionAnalysis`.
4. If analysis passes: PAUSES; waits for manual promote.
5. If analysis fails: aborts; old ReplicaSet stays active.

### 3.4 Smoke-test the preview Service

```bash
# Port-forward to the preview Service.
kubectl -n mahjong-prod port-forward svc/mahjong-autotable-preview 8080:80 &

# Smoke 1: /health
curl -sS http://localhost:8080/health
# Expect: {"status":"healthy", ...}

# Smoke 2: /build-info — verify the new image's commit SHA
curl -sS http://localhost:8080/build-info | jq .sha
# Expect: "<the new release's commit SHA>"

# Smoke 3: SignalR negotiate
curl -sS -X POST http://localhost:8080/hubs/changsha/negotiate?negotiateVersion=1 | jq .availableTransports[0].transport
# Expect: "WebSockets"
```

### 3.5 Manual promote

```bash
# Promote the green ReplicaSet → active.
kubectl argo rollouts -n mahjong-prod promote mahjong-autotable

# Watch the cutover.
kubectl argo rollouts -n mahjong-prod get rollout mahjong-autotable --watch
```

The controller:

1. Patches the `mahjong-autotable` (active) Service selector
   to point at the green ReplicaSet.
2. Runs the 60s `postPromotionAnalysis` against the
   now-active Service.
3. Waits `scaleDownDelaySeconds` (30s) for in-flight requests
   on the blue ReplicaSet to drain.
4. Scales the blue ReplicaSet to 0.

### 3.6 Rollback (`undo`)

If post-promotion analysis surfaces a regression:

```bash
# Roll back to the previous (blue) ReplicaSet.
kubectl argo rollouts -n mahjong-prod undo mahjong-autotable
```

The controller patches the active Service back to the blue
ReplicaSet's pod-template-hash; the cluster returns to the
pre-promote state within ~5s (Service-selector flip).

The blue ReplicaSet was retained for
`scaleDownDelayRevisionLimit: 2` revisions, so up to 2
previous revisions are rollback-eligible.

### 3.7 Abort (mid-analysis)

If the operator wants to abort BEFORE promote:

```bash
kubectl argo rollouts -n mahjong-prod abort mahjong-autotable
```

The green ReplicaSet scales to 0 after
`abortScaleDownDelaySeconds` (30s). The blue ReplicaSet
continues serving 100% traffic.

## 4. Operator override — skip analysis (escalation only)

If Prometheus is unreachable and the operator wants to
promote anyway (e.g. an urgent security patch during a
monitoring outage):

```bash
# DANGEROUS — bypasses the 60s P95 latency gate.
kubectl argo rollouts -n mahjong-prod promote mahjong-autotable --full
```

The `--full` flag instructs the controller to skip all
remaining analysis runs and promote immediately. Audit-log
the action in the wave's PR description with rationale.

## 5. Coexistence with the Canary path

The W9 Canary strategy and the W20 BlueGreen strategy are
**mutually exclusive at any given moment** — the cluster
can only have ONE of {Deployment, Rollout} owning the
backend pods at a time.

The transition between strategies is operator-managed:

```bash
# Switch from Canary (W9) to BlueGreen (W20):
kubectl -n mahjong-prod delete rollout mahjong-autotable    # if a canary Rollout existed
kubectl -n mahjong-prod scale deployment prod-mahjong-autotable --replicas=0
kubectl -n mahjong-prod apply -f infra/k8s/base/argo-rollouts/backend-bluegreen.yaml

# Switch from BlueGreen back to Canary:
kubectl -n mahjong-prod delete -f infra/k8s/base/argo-rollouts/backend-bluegreen.yaml
kubectl -n mahjong-prod scale deployment prod-mahjong-autotable --replicas=3
# (helm upgrade then re-applies the Canary chart shape)
```

## 6. Why NOT wired into base/kustomization.yaml

The W20 BlueGreen manifest lives at
`infra/k8s/base/argo-rollouts/backend-bluegreen.yaml` —
NOT listed in `infra/k8s/base/kustomization.yaml` deliberately.
Rationale:

* The default cluster posture remains the W2 Deployment +
  W9 Canary Rollout (when actually invoked). Wiring BlueGreen
  into base/ would push it into BOTH the prod and staging
  overlays — and BlueGreen-mode requires an operator-managed
  cutover from Deployment-mode (the §3.2 step `scale
  deployment ... --replicas=0`).
* The W20 BlueGreen path is operator-opt-in. Pre-mounting it
  into the base kustomize graph would risk a `kubectl apply
  -k` invocation creating two competing pod owners.

The operator applies the manifest directly via the §3.2
runbook when promoting via the BlueGreen path; the next
ordinary release reverts to Canary.

## 7. Cross-references

- [`infra/k8s/base/argo-rollouts/backend-bluegreen.yaml`](../infra/k8s/base/argo-rollouts/backend-bluegreen.yaml)
  — the W20 Rollout + AnalysisTemplate + preview Service.
- [`docs/argo-rollouts-install-runbook.md`](./argo-rollouts-install-runbook.md)
  — W19 controller install runbook (prereq).
- [`docs/argo-rollouts-setup.md`](./argo-rollouts-setup.md)
  — W10 install-on-staging runbook.
- [`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml)
  — W9 Canary alternative strategy.
- [`infra/k8s/base/deployment.yaml`](../infra/k8s/base/deployment.yaml)
  — the base Deployment this Rollout supersedes when BlueGreen
  mode is active.
- [`infra/k8s/base/service.yaml`](../infra/k8s/base/service.yaml)
  — the base (active) Service the Rollout patches at promote
  time.
