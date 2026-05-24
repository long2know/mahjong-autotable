# Argo Rollouts post-install verification — W23 runbook

> Phase K Wave 23 — Apone (DevOps).
> Audience: SRE / on-call operator AFTER executing the
> [`docs/argo-rollouts-install-runbook.md`](./argo-rollouts-install-runbook.md)
> install steps, BEFORE running the first production
> rollout. Companion to:
>
> * [`docs/argo-rollouts-install-runbook.md`](./argo-rollouts-install-runbook.md)
>   (W19 — install steps).
> * [`docs/argo-rollouts-backend-bluegreen.md`](./argo-rollouts-backend-bluegreen.md)
>   (W20 — backend BlueGreen strategy).
> * [`docs/argo-rollouts-frontend-canary.md`](./argo-rollouts-frontend-canary.md)
>   (W21 — frontend canary strategy).
> * [`docs/argo-rollouts-setup.md`](./argo-rollouts-setup.md)
>   (W10 — staging install reference).

## 1. Scope

The W19 install runbook lands the controller; W20 +
W21 define the rollout strategies; this W23 runbook is
the **gate** between install and first-rollout. It
captures the "did everything land in the shape the
strategies need?" sanity checks that have historically
been embedded in the W19 runbook's §2.3 / §3 / §4
sections — but the install runbook is a STEP-BY-STEP
artefact, and a checklist-style verification doc
serves a different audience (the operator coming back
the morning after the install to confirm the cluster
is ready for traffic).

**Do NOT run a production rollout until every check
in §3 is GREEN.** Failures route to §5 remediation.

## 2. Pre-conditions

| # | Pre-condition                                                  | Verify                                                                                |
| - | -------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| 1 | The W19 install runbook §2.1 → §2.4 completed                  | The PR documenting the install is merged + the install PR's checklist is 100% green.  |
| 2 | Operator kubeconfig points at the prod cluster                 | `kubectl config current-context` returns `mahjong-prod-use1`.                         |
| 3 | Operator holds at least `mahjong-autotable-rollouts-reader`    | `kubectl auth can-i list rollouts.argoproj.io --all-namespaces` returns `yes`.        |
| 4 | The W20/W21 helm chart values are committed at the install SHA | `git log --oneline helm/mahjong/values-prod.yaml \| head -1` matches the install SHA. |

## 3. Post-install verification checklist

Walk each row top-to-bottom. Each row carries a
verification command + an expected output. ANY failure
HALTS the rollout window — return to §5.

### 3.1 Controller pod health

```bash
kubectl -n argo-rollouts get pods
# Expect (READY 1/1, STATUS Running, RESTARTS 0):
#   argo-rollouts-XXXXXXXX-YYYYY   1/1   Running   0   <age>
```

Failure shapes:

* `0/1 CrashLoopBackOff` — controller image pull or
  RBAC issue. Walk §5.1.
* `1/1 Running, RESTARTS > 0` — controller restarted
  unexpectedly. Inspect logs (§5.2) before promoting.

### 3.2 CRDs registered

```bash
kubectl get crd \
   | grep argoproj.io \
   | sort
# Expect exactly 5:
#   analysisruns.argoproj.io
#   analysistemplates.argoproj.io
#   clusteranalysistemplates.argoproj.io
#   experiments.argoproj.io
#   rollouts.argoproj.io
```

Any missing CRD ⇒ §5.3 (helm install partial-failure).

### 3.3 Admission webhook registered

```bash
kubectl get validatingwebhookconfigurations \
   | grep argo-rollouts
# Expect:
#   argo-rollouts-validating-webhook  <pod-count>  <age>
```

Missing webhook ⇒ Rollout creation will succeed without
admission validation — a silent regression. Walk §5.4.

### 3.4 W11 auth-aware ingress applied

```bash
kubectl -n argo-rollouts get ingress
# Expect at least one Ingress with the
# `mahjong-autotable-argo-rollouts-dashboard` name.
```

Missing ⇒ the dashboard is unreachable. Re-apply per
W19 §3.

### 3.5 W12 NetworkPolicy applied

```bash
kubectl -n argo-rollouts get networkpolicies
# Expect at least:
#   argo-rollouts-default-deny
#   argo-rollouts-allow-controller-egress
#   argo-rollouts-allow-dashboard-ingress
```

Missing default-deny ⇒ controller can talk to anywhere
on the cluster network — a supply-chain risk. Walk
§5.5 immediately.

### 3.6 RBAC bindings landed

```bash
# Verify the canary-promoter binding exists for at
# least one operator identity.
kubectl get clusterrolebinding \
   -o jsonpath='{range .items[*]}{.metadata.name}\n{end}' \
   | grep 'canary-promoter'

# Verify the rollouts-reader namespace binding.
kubectl -n argo-rollouts get rolebinding \
   -o jsonpath='{range .items[*]}{.metadata.name}\n{end}' \
   | grep 'rollouts-reader'
```

Missing bindings ⇒ the W19 RBAC stubs are inert. The
controller works, but no operator can promote/abort.
Re-bind per W19 §2.4.

### 3.7 AnalysisTemplate gates admitted

```bash
kubectl -n mahjong-prod get analysistemplates \
   -o jsonpath='{range .items[*]}{.metadata.name}\n{end}' \
   | sort
# Expect 3 (W9 chart-shipped):
#   canary-error-budget
#   canary-p99-latency
#   canary-success-rate
```

Any missing template ⇒ the W21 canary strategy CANNOT
gate promotion. Walk §5.6 (helm-template re-render).

### 3.8 BlueGreen + Canary Rollouts dry-render

```bash
# Dry-render the prod helm values against the cluster.
helm template mahjong helm/mahjong/ \
   --values helm/mahjong/values-prod.yaml \
   | kubectl apply --dry-run=server -f -

# Expect:
#   rollout.argoproj.io/mahjong-autotable-backend  created (dry run)
#   rollout.argoproj.io/mahjong-autotable-frontend created (dry run)
#   (etc.)
```

Any "Error from server" ⇒ admission rejects the
render. Walk §5.7 (admission-error catalogue).

### 3.9 Hudson panels return data

```bash
# Verify the W20 + W21 Rollouts metrics scrape lands.
# (Operator runs this AGAINST Hudson's prometheus — not
# the prod cluster).
curl -s "https://hudson.long2know.com/api/v1/query?query=argo_rollouts_controller_info" \
   | jq -e '.data.result | length > 0'

# Expect: `true` (controller info series present).
```

No data ⇒ Hudson's ServiceMonitor missed the install
window. Walk §5.8 (Hudson coord).

### 3.10 Pre-flight: rollout-readiness with NO traffic

Before sending a real rollout, exercise the controller
with a **no-op rollout** — bump an annotation that
re-renders the Rollout template without changing the
image. The controller should re-render the ReplicaSet
without traffic shift.

```bash
# Patch the Rollout's pod template annotation with a
# timestamp. The image stays the same; the ReplicaSet
# rotates.
kubectl -n mahjong-prod patch rollout mahjong-autotable-backend \
   --type=merge \
   --patch "{\"spec\":{\"template\":{\"metadata\":{\"annotations\":{\"verification-noop\":\"$(date -u +%FT%TZ)\"}}}}}"

# Watch the rollout step through (BlueGreen — the
# noop should land in the Active service immediately
# since the ReplicaSet matches).
kubectl argo rollouts -n mahjong-prod get rollout \
   mahjong-autotable-backend --watch
```

Expect:

* Rollout status `Healthy` at end-state.
* The PromotionAuto gate (BlueGreen) returns Successful.
* The W20 §6 AnalysisRun completes Successful.

Any failure ⇒ DO NOT proceed to a real rollout.
Walk §5.9.

## 4. Sign-off

Once §3.1 → §3.10 all GREEN, the cluster is ready for
first production rollout. Document the sign-off in the
W19 install PR with a comment:

> **Post-install verification GREEN.** §3.1 → §3.10
> all pass; cluster cleared for first production
> rollout at `<UTC timestamp>`. Sign-off by
> `<operator>`.

The first production rollout follows the W20 BlueGreen
runbook ([`docs/argo-rollouts-backend-bluegreen.md`](./argo-rollouts-backend-bluegreen.md))
for the backend Deployment, OR the W21 Canary runbook
([`docs/argo-rollouts-frontend-canary.md`](./argo-rollouts-frontend-canary.md))
for the frontend.

## 5. Remediation paths

Each §5.N below maps to a §3.N failure.

### 5.1 Controller CrashLoopBackOff

```bash
kubectl -n argo-rollouts logs -l app.kubernetes.io/name=argo-rollouts --tail=200
```

Common shapes: image pull (registry-auth), RBAC denied
(the controller's SA can't list rollouts.argoproj.io),
admission-config invalid (helm-values typo). Fix +
`helm upgrade`.

### 5.2 Unexpected restart

```bash
kubectl -n argo-rollouts logs -l app.kubernetes.io/name=argo-rollouts --previous --tail=200
```

If the previous-container log shows OOM or webhook
timeout, increase resources via `helm upgrade --set
resources.limits.memory=512Mi` before promoting.

### 5.3 CRD missing

The helm install missed a CRD apply. Re-run:

```bash
helm upgrade --install argo-rollouts argo/argo-rollouts \
   --namespace argo-rollouts \
   --version <pinned-version> \
   --reuse-values \
   --wait
```

Verify §3.2 again.

### 5.4 Webhook missing

The `--skip-crds` or `--no-hooks` flag was set during
install. Re-run `helm upgrade --install` without those
flags; verify §3.3.

### 5.5 NetworkPolicy default-deny missing

**HIGH PRIORITY.** Apply immediately:

```bash
kubectl apply -f infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml
```

Audit Hudson's `kube_network_policy_count` panel for
the `argo-rollouts` namespace; the count should bump.

### 5.6 AnalysisTemplate missing

The helm install rendered without the `analysis.enabled
= true` value, or the chart version is below the W9
seed. Re-render:

```bash
helm template mahjong helm/mahjong/ \
   --values helm/mahjong/values-prod.yaml \
   | grep -A0 'kind: AnalysisTemplate' | head

# If empty, check values-prod.yaml:
#   rollouts.analysis.enabled: true
# Then upgrade:
helm upgrade --install mahjong helm/mahjong/ \
   --values helm/mahjong/values-prod.yaml
```

### 5.7 Admission-error catalogue

| Error shape | Cause | Fix |
| ----------- | ----- | --- |
| `"strategy" must be one of "canary","blueGreen"` | Rollout missing strategy block. | Add to helm values. |
| `Rollout.spec.template.spec.containers must contain at least one container` | Empty pod template. | Helm values regression — bisect. |
| `AnalysisTemplate not found` | §3.7 failed — re-walk. | Re-render. |

### 5.8 Hudson missing data

Coordinate with Hudson. ServiceMonitor target may need
re-labelling against the controller pod's labels.
Apply the W17 ServiceMonitor patch and re-verify.

### 5.9 No-op rollout fails

The §3.10 no-op should ALWAYS pass on a clean install.
A failure indicates either:

* The image referenced in the Rollout doesn't exist
  (registry-side regression).
* The cluster's PodSecurity / Kyverno admission stack
  rejects the template (W15/W23 rules — check
  PolicyReport rows).
* Pod scheduling pressure (HPA capped, node count
  insufficient).

Walk each in order; do not promote until §3.10 passes.

## 6. Audit-trail anchors

* The verification sign-off comment in §4 is the
  canonical install-to-rollout handoff anchor.
* The §3.10 no-op rollout's `verification-noop`
  annotation timestamp lives on the Rollout resource
  in etcd; `kubectl get rollout -o yaml` surfaces it
  on any future investigation.
* The W17 Hudson `argo-rollouts-controller-info`
  metric carries an install-time `installed_at`
  label that anchors the prometheus side.

## 7. Cross-references

* [`docs/argo-rollouts-install-runbook.md`](./argo-rollouts-install-runbook.md)
  — W19 install.
* [`docs/argo-rollouts-backend-bluegreen.md`](./argo-rollouts-backend-bluegreen.md)
  — W20 BlueGreen.
* [`docs/argo-rollouts-frontend-canary.md`](./argo-rollouts-frontend-canary.md)
  — W21 Canary.
* [`docs/argo-rollouts-setup.md`](./argo-rollouts-setup.md)
  — W10 staging-install reference.
* [`infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`](../infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml)
  — W11 ingress.
* [`infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`](../infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml)
  — W12 NetworkPolicy.
* [`helm/mahjong/values-prod.yaml`](../helm/mahjong/values-prod.yaml)
  — W9 helm values + AnalysisTemplate config.
