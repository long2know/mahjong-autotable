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
auth-aware proxy in front. **W11 (Phase K) ships the canonical
auth-aware proxy** — see §5 below.

The pre-W11 placeholder example (Cloudflare Access / generic
OIDC sidecar) is retained for completeness:

```yaml
# Pre-W11 placeholder — DO NOT ship this without the auth-aware
# proxy upstream. W11 supersedes this with §5 + the
# infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml
# manifest.
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
that doesn't need always-on access. **W11 ships an opt-in
auth-aware ingress** (§5) for the squad members who want a
stable URL gated by the existing OIDC chain.

## 5. Auth-aware ingress (Phase K Wave 11)

W10 shipped the cluster install + port-forward access patterns
above. W11 ships a **production-grade auth-aware ingress** so
the squad can reach the dashboard via a stable URL gated by the
existing `oauth2-proxy` + dex OIDC chain (the same chain that
fronts the production app — see `docs/oauth-production-setup.md`
§4). No new identity provider; the chain already covers
@squad.mahjong + the allow-listed external observers.

### 5.1 Manifest

The canonical W11 ingress lives at:

* [`infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`](../infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml)

It is **out-of-band** (NOT in any `kustomization.yaml`
`resources:` list at W11) — the operator applies it manually
once the cluster bootstrap completes:

```bash
kubectl -n argo-rollouts apply -f \
    infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml
```

### 5.2 Annotations — the auth-request subrequest

The two nginx-ingress annotations that gate the dashboard:

```yaml
nginx.ingress.kubernetes.io/auth-url:    "https://auth.mahjong.example.com/oauth2/auth"
nginx.ingress.kubernetes.io/auth-signin: "https://auth.mahjong.example.com/oauth2/start?rd=$escaped_request_uri"
```

Flow:

1. Client `GET /argo-rollouts/` → nginx-ingress.
2. nginx-ingress hits `auth.mahjong.example.com/oauth2/auth`
   with the inbound cookies (auth-url subrequest).
3. 2xx → original request proxies to the dashboard Service
   (rewrite-target strips `/argo-rollouts` so the SPA serves
   from `/` upstream).
4. 401 → 302-redirect to
   `auth.mahjong.example.com/oauth2/start?rd=<original URL>`
   (auth-signin). The user lands on dex, signs in, comes back
   with the cookie set, retries the original request.

The dashboard itself has NO built-in auth (per the §4.3
warning); this Ingress IS the auth boundary. Bypassing the
nginx-ingress (e.g. via a direct Service hit from inside the
cluster) is still possible — that's a `NetworkPolicy` concern,
not an ingress concern. The W11 deliverable does NOT ship a
companion NetworkPolicy; the deferred follow-up is a W12
candidate.

### 5.3 Path rewrite — `/argo-rollouts/*` → `/*`

The dashboard image expects to serve from `/` (it emits
absolute paths in its SPA bundle). To expose it under
`/argo-rollouts/` without forking the image:

```yaml
annotations:
  nginx.ingress.kubernetes.io/rewrite-target: "/$2"
  nginx.ingress.kubernetes.io/use-regex: "true"

spec:
  rules:
    - host: mahjong.example.com
      http:
        paths:
          - path: /argo-rollouts(/|$)(.*)
            pathType: ImplementationSpecific
            backend:
              service:
                name: argo-rollouts-dashboard
                port:
                  number: 3100
```

The capture group `$2` is the dashboard-internal path; the
leading `/argo-rollouts` is stripped. The `(/|$)` ensures bare
`/argo-rollouts` (no trailing slash) also matches.

### 5.4 TLS / HSTS hygiene

```yaml
nginx.ingress.kubernetes.io/ssl-redirect:       "true"
nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
```

HSTS itself is set at the parent ingress (the prod app's
`hsts-patch.yaml`); the auth-aware ingress inherits the
ingress-class-wide HSTS header. The wildcard ACM cert
(provisioned by the W11 prod edge stack —
`infra/terraform/envs/prod/main.tf`) covers
`mahjong.example.com` and is mounted via the `mahjong-tls-cert`
Secret on the parent `mahjong-prod` ingress.

### 5.5 Validation

```bash
# 1. Unauthenticated request → 302 to auth-signin URL.
curl -sI https://mahjong.example.com/argo-rollouts/ \
    | head -1
# Expected: HTTP/2 302
curl -sI https://mahjong.example.com/argo-rollouts/ \
    | grep -i 'location:'
# Expected: location: https://auth.mahjong.example.com/oauth2/start?rd=...

# 2. Authenticated request (with the oauth2-proxy cookie set)
#    → 200 from the dashboard SPA.
curl -sI -b "_oauth2_proxy=<cookie>" \
    https://mahjong.example.com/argo-rollouts/ \
    | head -1
# Expected: HTTP/2 200

# 3. The SPA assets load — the rewrite-target strips correctly.
curl -sI -b "_oauth2_proxy=<cookie>" \
    https://mahjong.example.com/argo-rollouts/static/index.js \
    | head -1
# Expected: HTTP/2 200
```

### 5.6 Rollback

Reverting to port-forward-only:

```bash
kubectl -n argo-rollouts delete ingress argo-rollouts-dashboard
```

The dashboard service is unaffected; port-forward (§4.1) keeps
working. No data loss — the dashboard reads cluster state
directly and is stateless.

## 6. NetworkPolicy hardening (Phase K Wave 12)

> Phase K Wave 12 — Apone (DevOps). The W11 auth-aware ingress
> closed the **identity** loop (only squad members can reach the
> dashboard). W12 closes the **network** loop: a NetworkPolicy
> set in `argo-rollouts` ns that pins both the controller and the
> dashboard to a minimal allow-list — no lateral access to / from
> arbitrary workloads in the cluster.

### 6.1 The three policies

[`infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`](../infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml)
defines three `NetworkPolicy` objects in the `argo-rollouts`
namespace:

| Policy | Pod selector | Direction | Allow-list |
|---|---|---|---|
| `argo-rollouts-dashboard-ingress` | `app.kubernetes.io/component=dashboard` | Ingress | `ingress-nginx` ns (the W11 ingress controller) + `auth` ns (the oauth2-proxy auth-url subrequest path — see W11 §5 for the flow) |
| `argo-rollouts-controller-egress` | `app.kubernetes.io/component=rollouts-controller` | Egress | kube-apiserver (CRD reconcile loop), Prometheus (`monitoring` ns — Hudson's W11 metrics scrape), kube-dns (UDP 53 to `kube-system`) |
| `argo-rollouts-dashboard-egress` | `app.kubernetes.io/component=dashboard` | Egress | kube-apiserver (the dashboard reads CRD state directly), kube-dns (UDP 53) |

Default-deny is the **starting position**: each policy declares
`policyTypes: [Ingress, Egress]` so any traffic NOT in the
allow-list is dropped at the CNI layer. The cluster MUST run a
NetworkPolicy-aware CNI (Calico, Cilium, or AWS VPC CNI with
the network-policy add-on enabled) for these to take effect —
verify with:

```bash
kubectl -n kube-system get pods -l k8s-app=calico-node \
    -o jsonpath='{.items[0].status.phase}'
# Expected: Running (or equivalent for cilium-node).
```

### 6.2 Why split policies (vs one mega-policy)?

The default `argo-rollouts` Helm chart ships TWO distinct
workloads in the same namespace:

- **`rollouts-controller`** — the operator pod (long-lived, reads
  CRDs via the apiserver watch, writes events back, scrapes
  Prometheus for analysis-template metric queries).
- **`argo-rollouts-dashboard`** — the SPA-serving pod (short-
  lived from a network-traffic POV; only proxies the
  /api/v1/rollouts read path to the apiserver).

Their egress profiles are different (the dashboard does NOT
need Prometheus access; the controller does), and splitting
the policies keeps each one's allow-list minimal — easier to
audit + easier to update when a new Helm version adds a new
endpoint. The ingress policy is ONLY on the dashboard because
the controller has no ingress callers (its only inbound traffic
is the kube-apiserver's webhook callbacks via the ValidatingAdmissionWebhook
that the chart installs — that's a cluster-scoped path managed
by the apiserver and is not subject to NetworkPolicy).

### 6.3 Wire-in

The W12 prod overlay's `kustomization.yaml` references the
file in its `resources:` list. The cross-namespace pinning
(`argo-rollouts` rather than `mahjong-prod`) is preserved via
the W12 `namespace-transformer.yaml` (`unsetOnly: true`) — see
[`docs/prod-cutover.md §4`](./prod-cutover.md#4-argo-rollouts-dashboard-cross-namespace-pattern)
for the design rationale.

### 6.4 Validation

After `kustomize build … | kubectl apply -f -`:

```bash
# 1. All three policies present.
kubectl -n argo-rollouts get netpol
# Expected:
#   prod-argo-rollouts-controller-egress
#   prod-argo-rollouts-dashboard-egress
#   prod-argo-rollouts-dashboard-ingress

# 2. Dashboard reachable from ingress-nginx (negative test —
#    from a pod in a NON-allowed namespace, the request must
#    fail).
kubectl -n default run netpol-probe --rm -i --tty --image=curlimages/curl:8.10.0 -- \
    curl -s --max-time 5 http://argo-rollouts-dashboard.argo-rollouts.svc.cluster.local:3100/
# Expected: curl exit code 28 (timeout) — the default ns is NOT in the ingress allow-list.

# 3. Dashboard reachable from ingress-nginx (positive test).
kubectl -n ingress-nginx exec deploy/ingress-nginx-controller -- \
    curl -s --max-time 5 -o /dev/null -w '%{http_code}' \
    http://argo-rollouts-dashboard.argo-rollouts.svc.cluster.local:3100/
# Expected: 200 (or 302 to /argo-rollouts/ — the dashboard's SPA root).

# 4. Controller still talks to the apiserver (positive test —
#    reconcile loop still running).
kubectl -n argo-rollouts logs deploy/argo-rollouts --tail=20 \
    | grep -Ei "reconcile|sync" | head -5
# Expected: recent reconcile lines (every ~30s).
```

### 6.5 Updating the policies

When upgrading the argo-rollouts Helm chart:

1. Diff the new chart's `templates/` against the prior version
   for any NEW network egress (e.g. a new metrics endpoint, a
   new sidecar).
2. If any new egress is added, append a matching rule to the
   relevant policy file.
3. Re-run the validation steps above.

Chart upgrades that ADD a new pod or workload to the
`argo-rollouts` namespace WILL be blocked by these policies
until a matching NetworkPolicy is added — this is intentional
default-deny behaviour. Read the chart's release notes for any
"new workload" callouts before bumping.

### 6.6 Rollback

If the policies cause an outage (e.g. dashboard unreachable due
to a missing allow-list entry):

```bash
# Fast revert — delete all three policies.
kubectl -n argo-rollouts delete netpol \
    prod-argo-rollouts-dashboard-ingress \
    prod-argo-rollouts-dashboard-egress \
    prod-argo-rollouts-controller-egress

# Re-apply once the missing rule is identified.
kustomize build infra/k8s/overlays/prod/ | kubectl apply -f -
```

The policies are pure-additive — removing them returns the
namespace to the default no-policy posture (all pod-to-pod
traffic allowed within the cluster). The W11 ingress + W11
oauth2-proxy chain still gates external access; rolling back
the NetworkPolicies degrades the lateral-access posture but
does NOT open the dashboard to the public internet.

## 7. Validation

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

## 8. Wiring to the Mahjong chart

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

## 9. Rollback procedure

### 8.1 Rolling back an in-flight canary

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

### 8.2 Uninstalling the controller entirely

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

## 10. Cross-references

- [`docs/helm-charts.md` §3.5](./helm-charts.md) — the
  `AnalysisTemplate` gates this controller evaluates.
- [`docs/production-deployment-runbook.md`](./production-deployment-runbook.md) — the prod-deploy runbook that
  references this install as a prerequisite (§8 "Continuous
  health probes" cross-link added in W10).
- [`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml) — the chart-side resource that needs this controller.
- [`infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`](../infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml) — Phase K Wave 12 NetworkPolicy hardening (§6).
- [`infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`](../infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml) — Phase K Wave 11 auth-aware ingress manifest (§5).
- [`docs/prod-cutover.md`](./prod-cutover.md) §4 — cross-namespace kustomize pattern (W12 wire-in for §5 + §6).
- [`docs/oauth-production-setup.md`](./oauth-production-setup.md) §4 — the oauth2-proxy + dex OIDC chain that fronts §5.
- Upstream docs: <https://argoproj.github.io/argo-rollouts/>
- Upstream chart: <https://github.com/argoproj/argo-helm/tree/main/charts/argo-rollouts>
