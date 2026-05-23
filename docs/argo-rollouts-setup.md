# Argo Rollouts — cluster install runbook

> Phase K Wave 10 — Apone (DevOps).
> Audience: SRE / on-call installing Argo Rollouts on the
> Mahjong-Autotable EKS clusters so the W9 Helm canary
> `AnalysisTemplate` resources actually do something.
> Companion: [`docs/helm-charts.md` §3.5](./helm-charts.md) (the
> AnalysisTemplate surface that depends on this CRD).

The Wave-9 canary retarget ships **three independent Helm
`AnalysisTemplate` gates** (`canary-success-rate`,
`canary-p99-latency`, `canary-error-budget`). Those templates
are **inert** until Argo Rollouts is installed on the cluster —
the CRDs (`Rollout`, `AnalysisTemplate`, `AnalysisRun`) and the
`argo-rollouts` controller must be present for the chart's
canary path to mean anything. This runbook is the prerequisite
to flipping `canary.enabled = true` in `helm/mahjong/values-prod.yaml`.

## 1. Prerequisites

| Tool       | Required             | Verify                 |
| ---------- | -------------------- | ---------------------- |
| `helm`     | ≥ 3.12               | `helm version`         |
| `kubectl`  | ≥ 1.27               | `kubectl version --client` |
| Argo CLI   | optional (dashboard) | `kubectl argo rollouts version` |

The operator's `kubeconfig` MUST point at the target cluster
(staging or prod) and the operator MUST hold
`cluster-admin`-equivalent permission for the install (the
controller installs CRDs at cluster scope).

## 2. Install — Helm

The squad uses the upstream `argoproj` Helm chart pinned to a
known version. Default install lives in the dedicated
`argo-rollouts` namespace; do NOT install into
`mahjong-autotable` (separation of concerns — the controller is
a cluster-wide service, the app is one consumer).

```bash
# 1. Add the upstream repo.
helm repo add argo https://argoproj.github.io/argo-helm
helm repo update

# 2. Install — pinned version.
helm install argo-rollouts argo/argo-rollouts \
    --namespace argo-rollouts \
    --create-namespace \
    --version 2.37.7 \
    --set installCRDs=true \
    --set dashboard.enabled=true \
    --set dashboard.service.type=ClusterIP \
    --wait \
    --timeout 5m
```

Pinning rationale: 2.37.x is the W10 baseline (1.7.x controller
binary + Kubernetes 1.27-30 supported). Bump the pin in a
follow-up runbook step — do NOT chase HEAD; the controller's CRD
schema occasionally evolves and a mid-canary controller upgrade
is high-risk.

Successful install — `kubectl get pods -n argo-rollouts`
shows `argo-rollouts-…` Running and `argo-rollouts-dashboard-…`
Running (the dashboard pod ships when `dashboard.enabled=true`).

## 3. Install — kubectl plugin (operator workstation)

The `kubectl argo rollouts` plugin is the operator-side CLI for
manual rollout inspection + abort + promote actions:

```bash
# Linux amd64; pick the asset matching your platform.
curl -sLO https://github.com/argoproj/argo-rollouts/releases/download/v1.7.2/kubectl-argo-rollouts-linux-amd64
chmod +x kubectl-argo-rollouts-linux-amd64
sudo mv kubectl-argo-rollouts-linux-amd64 /usr/local/bin/kubectl-argo-rollouts

# Smoke test.
kubectl argo rollouts version
```

## 4. Dashboard access

The dashboard ships as a ClusterIP service. Three access shapes:

### 4.1 `kubectl port-forward` (operator-local, no ingress)

```bash
kubectl port-forward -n argo-rollouts svc/argo-rollouts-dashboard 3100:3100
# Browse to http://localhost:3100/rollouts/
```

This is the W10 baseline — no public surface, no auth concerns,
just for SRE-on-call hands-on inspection.

### 4.2 `kubectl argo rollouts dashboard` (operator-local, plugin)

```bash
kubectl argo rollouts dashboard
# Opens a browser tab at http://localhost:3100/rollouts/
```

Wraps §4.1 with a port-forward.

### 4.3 Ingress (cluster-fronted — opt-in)

Only flip to ingress-fronted dashboard when the squad has an
auth-aware proxy in front (Cloudflare Access, OIDC sidecar,
etc.). The dashboard has **no built-in auth**; exposing it
behind the cluster's nginx-ingress directly is equivalent to a
public unauthenticated endpoint.

```yaml
# infra/k8s/argo-rollouts/dashboard-ingress.yaml — DO NOT ship
# this without the auth-aware proxy upstream.
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: argo-rollouts-dashboard
  namespace: argo-rollouts
  annotations:
    # Cloudflare Access OIDC sidecar — operator-provided.
    nginx.ingress.kubernetes.io/auth-url: "https://access.example.com/auth"
spec:
  ingressClassName: nginx
  rules:
    - host: rollouts.staging.mahjong.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: argo-rollouts-dashboard
                port:
                  number: 3100
```

Squad recommendation: skip §4.3 entirely — port-forward
(§4.1/§4.2) is the production-grade pattern for an SRE tool
that doesn't need always-on access.

## 5. Validation

After install, validate the controller + CRDs are healthy:

```bash
# 1. CRDs registered.
kubectl get crd | grep argoproj.io
# Expected (at least):
#   analysisruns.argoproj.io
#   analysistemplates.argoproj.io
#   clusteranalysistemplates.argoproj.io
#   experiments.argoproj.io
#   rollouts.argoproj.io

# 2. Controller Running.
kubectl get pods -n argo-rollouts -l app.kubernetes.io/name=argo-rollouts
# Expected: argo-rollouts-… 1/1 Running

# 3. Controller logs clean (no CRD-version-skew errors).
kubectl logs -n argo-rollouts deploy/argo-rollouts --tail=50 \
    | grep -Ei "error|fail" | head
# Expected: no output (or transient leader-election noise only).

# 4. Dashboard reachable via port-forward.
kubectl port-forward -n argo-rollouts svc/argo-rollouts-dashboard 3100:3100 &
PF_PID=$!
sleep 2
curl -sf http://localhost:3100/api/v1/rollouts/argo-rollouts | jq '.|keys'
# Expected: ["metadata","spec","status"] or {} (empty rollouts list).
kill $PF_PID
```

## 6. Wiring to the Mahjong chart

Once the controller is healthy:

1. Flip `canary.enabled = true` in the env overlay (e.g.
   `helm/mahjong/values-prod.yaml` for the first prod canary).
2. Helm renders the chart's `Rollout` resource + the three
   `AnalysisTemplate` resources (one per gate).
3. `helm upgrade` against the cluster.
4. Watch the rollout:

   ```bash
   kubectl argo rollouts get rollout mahjong-autotable \
       -n mahjong-autotable --watch
   ```

   The dashboard at §4 mirrors the watch view + adds historical
   AnalysisRun visualisations.

The W9 `helm/mahjong/templates/canary-deployment.yaml` template
references the templates by name; controller-side resolution
happens at `Rollout` reconcile time, so the templates MUST
exist in the same namespace as the Rollout (the chart renders
them alongside).

## 7. Rollback procedure

### 7.1 Rolling back an in-flight canary

```bash
# Halt the in-flight rollout (does NOT undo what's already
# rolled — sets it to pause).
kubectl argo rollouts pause rollout mahjong-autotable \
    -n mahjong-autotable

# Reject (rolls the canary stable back).
kubectl argo rollouts abort rollout mahjong-autotable \
    -n mahjong-autotable
```

The `Rollout` controller falls the canary back to the previous
stable RS (ReplicaSet) on `abort`. The pause-then-abort split
is the on-call's tool to investigate WITHOUT immediately
discarding the canary pods (logs / metrics are still on the
canary pods until abort).

### 7.2 Uninstalling the controller entirely

```bash
helm uninstall argo-rollouts -n argo-rollouts
kubectl delete namespace argo-rollouts
# CRDs survive helm uninstall by design (preserves any existing
# Rollouts in other namespaces from being silently deleted).
# Remove them only if the squad has confirmed no other workload
# depends on the CRDs:
kubectl delete crd \
    rollouts.argoproj.io \
    analysisruns.argoproj.io \
    analysistemplates.argoproj.io \
    clusteranalysistemplates.argoproj.io \
    experiments.argoproj.io
```

Uninstalling the controller while a `Rollout` resource still
exists in the cluster leaves the existing pods running (they're
managed by the controller-created ReplicaSet underneath) — but
no further canary progression happens. The `mahjong-autotable`
Helm chart falls back to a plain `Deployment` when
`canary.enabled = false`; the operator can flip that knob
before uninstall to convert the workload cleanly.

## 8. Cross-references

- [`docs/helm-charts.md` §3.5](./helm-charts.md) — the
  `AnalysisTemplate` gates this controller evaluates.
- [`docs/production-deployment-runbook.md`](./production-deployment-runbook.md) — the prod-deploy runbook that
  references this install as a prerequisite (§8 "Continuous
  health probes" cross-link added in W10).
- [`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml) — the chart-side resource that needs this controller.
- Upstream docs: <https://argoproj.github.io/argo-rollouts/>
- Upstream chart: <https://github.com/argoproj/argo-helm/tree/main/charts/argo-rollouts>
