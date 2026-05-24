# Argo Rollouts controller — install runbook (W19)

> Phase K Wave 19 — Apone (DevOps).
> Audience: Stephen + on-call SRE installing Argo Rollouts on
> the production EKS cluster for the first time. Companion to
> [`docs/argo-rollouts-setup.md`](./argo-rollouts-setup.md)
> (the W10 install runbook for staging; this W19 runbook
> mirrors the shape but adds prod-specific pre-conditions +
> the W19 RBAC + namespace stubs).

W10 documented the helm-install path for the Argo Rollouts
controller; W11 added the auth-aware ingress; W12 added the
NetworkPolicy hardening; W9 wired the canary AnalysisTemplate
gates into the `helm/mahjong/values-prod.yaml` chart. **None of
those W9-W12 deliverables matter until the controller is
actually installed in prod.** Stephen has not yet performed
the install. W19 packages the prod-side install runbook +
pre-creates the namespace + RBAC stubs so the install is
mechanical.

This runbook does NOT run the install. **W19 ships the
runbook + the pre-install artefacts; the actual `helm install`
is Stephen's call** — same shape as the W19 us-east-1 apply
runbook.

## 1. Pre-conditions

| # | Pre-condition                                            | Verify                                                                 | Owner   |
| - | -------------------------------------------------------- | ---------------------------------------------------------------------- | ------- |
| 1 | Operator's kubeconfig points at the prod cluster         | `kubectl config current-context` returns `mahjong-prod-use1`           | Stephen |
| 2 | Operator holds cluster-admin                             | `kubectl auth can-i '*' '*' --all-namespaces` returns `yes`            | Stephen |
| 3 | The `argo-rollouts` namespace does NOT already exist     | `kubectl get ns argo-rollouts` returns `Error from server (NotFound)`  | Stephen |
| 4 | helm CLI v3.12+                                          | `helm version`                                                         | Stephen |
| 5 | kubectl ≥ 1.27                                           | `kubectl version --client`                                             | Stephen |
| 6 | Apone-lane helm repo cache fresh                         | `helm repo update`                                                     | Stephen |

If pre-condition 3 fails (the namespace already exists), the
W19 prereq apply path (§2.1) is skipped and the operator goes
directly to §2.2; the W19 namespace.yaml's labels can be
re-applied via `kubectl label namespace argo-rollouts ...`
without recreating the namespace.

## 2. Install — step-by-step

### 2.1 Apply the W19 pre-install artefacts

```bash
# Pre-create the namespace + RBAC stubs (mahjong-managed
# labels + canary-promoter ClusterRole + rollouts-reader
# namespace Role). The helm install picks up the existing
# namespace via --namespace argo-rollouts (W10 §2 step 2).
kubectl apply -f infra/k8s/base/argo-rollouts-prereqs/namespace.yaml
kubectl apply -f infra/k8s/base/argo-rollouts-prereqs/rbac.yaml

# Verify the namespace landed with the expected labels.
kubectl get namespace argo-rollouts -o jsonpath='{.metadata.labels}' \
    | jq .
# Expect:
#   {
#     "kubernetes.io/metadata.name": "argo-rollouts",
#     "mahjong-autotable.io/managed-by": "apone",
#     "pod-security.kubernetes.io/enforce": "baseline",
#     "pod-security.kubernetes.io/audit": "restricted",
#     "pod-security.kubernetes.io/warn": "restricted"
#   }

# Verify the RBAC stubs landed.
kubectl get clusterrole mahjong-autotable-canary-promoter
kubectl get role -n argo-rollouts mahjong-autotable-rollouts-reader
```

### 2.2 Helm install

Same shape as the W10 staging install, with one prod-specific
flag (the dashboard is NOT enabled — W11's auth-aware ingress
provides cluster-fronted access only after the operator opts
in):

```bash
helm repo add argo https://argoproj.github.io/argo-helm
helm repo update

# Prod install — pinned at the W10 baseline version.
helm install argo-rollouts argo/argo-rollouts \
    --namespace argo-rollouts \
    --version 2.37.7 \
    --set installCRDs=true \
    --set dashboard.enabled=false \
    --wait \
    --timeout 5m
```

Key flag differences vs. W10 staging:

| Flag                       | Staging (W10) | Prod (W19) | Rationale |
| -------------------------- | ------------- | ---------- | --------- |
| `--create-namespace`       | `true`        | (omitted)  | W19 §2.1 pre-creates the namespace. |
| `dashboard.enabled`        | `true`        | `false`    | Prod uses W11 auth-aware ingress; the dashboard pod ships separately when the operator opts in. |
| `dashboard.service.type`   | `ClusterIP`   | (n/a)      | No dashboard pod in prod baseline. |
| `--wait`                   | `true`        | `true`     | Both wait for the controller pod to land healthy. |
| `--timeout`                | `5m`          | `5m`       | Same. |

### 2.3 Verify the install landed

```bash
# 1. Controller pod Running.
kubectl -n argo-rollouts get pods
# Expect:
#   NAME                              READY   STATUS    RESTARTS   AGE
#   argo-rollouts-XXXXXXXX-YYYYY      1/1     Running   0          30s

# 2. CRDs registered.
kubectl get crd | grep argoproj
# Expect:
#   analysisruns.argoproj.io
#   analysistemplates.argoproj.io
#   clusteranalysistemplates.argoproj.io
#   experiments.argoproj.io
#   rollouts.argoproj.io

# 3. Webhook + admission registered (controller's admission
#    webhook handles Rollout validation).
kubectl get validatingwebhookconfigurations | grep argo-rollouts
# Expect: argo-rollouts-validating-webhook ...

# 4. The W12 NetworkPolicy applies cleanly.
kubectl -n argo-rollouts get networkpolicies
# Expect: argo-rollouts-default-deny + argo-rollouts-allow-...
```

### 2.4 Bind the W19 ClusterRole + Role to operator identities

The W19 RBAC stubs are INERT without bindings. Stephen binds
them at install time per his operator-workstation identity:

```bash
# Bind the canary-promoter ClusterRole to Stephen's operator
# IAM user (or to a Kubernetes ServiceAccount for the squad's
# automation bot, when the squad adds one).
kubectl create clusterrolebinding mahjong-autotable-canary-promoter-stephen \
    --clusterrole=mahjong-autotable-canary-promoter \
    --user=stephen@long2know.com

# Bind the rollouts-reader Role to the on-call group (when
# the squad's IAM federation lands; for the W19 single-user
# baseline, bind directly to Stephen).
kubectl -n argo-rollouts create rolebinding mahjong-autotable-rollouts-reader-stephen \
    --role=mahjong-autotable-rollouts-reader \
    --user=stephen@long2know.com
```

(The exact user / SA identities are operator-side; the W19
runbook treats them as placeholders. Stephen substitutes the
real IAM identity at install time.)

## 3. Post-install — wire the W11 ingress + W12 NetworkPolicy

The W11 + W12 deliverables are out-of-band manifests that
land AFTER the controller is up. Apply order:

```bash
# 1. W11 auth-aware ingress.
kubectl -n argo-rollouts apply -f \
    infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml

# 2. W12 NetworkPolicy.
kubectl apply -f \
    infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml

# 3. Verify both wired.
kubectl -n argo-rollouts get ingress
kubectl -n argo-rollouts get networkpolicies
```

The W11 + W12 files are part of the `kustomize build infra/
k8s/overlays/prod/` graph already — `kubectl apply -k infra/
k8s/overlays/prod/` would land them alongside the
mahjong-autotable Deployment. The W19 runbook lists the
explicit-apply path as a safer alternative during the
bootstrap window (avoids re-applying the entire prod overlay
mid-install).

## 4. Smoke test — the W9 canary AnalysisTemplate gates

The W9 helm chart ships three AnalysisTemplate resources
(`canary-success-rate`, `canary-p99-latency`,
`canary-error-budget`). With the controller installed, the
templates become live admission contracts. Smoke test:

```bash
# 1. Render the helm chart against the prod values.
helm template mahjong helm/mahjong/ \
    --values helm/mahjong/values-prod.yaml \
    | kubectl apply --dry-run=server -f -

# 2. Confirm the AnalysisTemplate resources are admitted by
#    the controller's webhook.
kubectl -n mahjong-prod get analysistemplates
# Expect the three W9 templates:
#   canary-success-rate
#   canary-p99-latency
#   canary-error-budget
```

Until `canary.enabled = true` is flipped in the prod values,
the templates are inert (no AnalysisRun resources spawn). The
flip is a separate hand-off — see `docs/helm-charts.md §3.5`.

## 5. Rollback

If the install fails partway:

```bash
# 1. Helm rollback (or uninstall if no prior release).
helm uninstall argo-rollouts -n argo-rollouts

# 2. Clean up CRDs (CRDs are NOT removed by helm uninstall
#    by design — they would orphan any existing Rollout
#    resources; if there are no Rollouts, manual cleanup is
#    safe).
kubectl get rollouts -A
# Expect: empty.
kubectl delete crd \
    analysisruns.argoproj.io \
    analysistemplates.argoproj.io \
    clusteranalysistemplates.argoproj.io \
    experiments.argoproj.io \
    rollouts.argoproj.io

# 3. Drop the W19 prereq artefacts.
kubectl delete role -n argo-rollouts mahjong-autotable-rollouts-reader
kubectl delete clusterrole mahjong-autotable-canary-promoter
kubectl delete namespace argo-rollouts
```

The W11 + W12 manifests are namespace-scoped to
`argo-rollouts`; dropping the namespace removes them
alongside the controller.

## 6. Post-install checklist (W19 retro hand-off)

| #  | Action                                                                   | Owner          |
| -- | ------------------------------------------------------------------------ | -------------- |
| 1  | Capture `helm install` log in `docs/argo-rollouts-prod-install-log.txt`. | Stephen        |
| 2  | Bind the W19 ClusterRole + Role to the operator identities (§2.4).      | Stephen        |
| 3  | Wire the W11 ingress + W12 NetworkPolicy (§3).                          | Stephen        |
| 4  | Run the §4 smoke test against the prod overlay.                         | Stephen        |
| 5  | Flip `docs/argo-rollouts-setup.md` baseline note to "INSTALLED IN PROD". | Apone (W20)    |
| 6  | Plan the `canary.enabled = true` flip (separate hand-off).               | Stephen + Apone |

## 7. Cross-references

- [`docs/argo-rollouts-setup.md`](./argo-rollouts-setup.md) —
  W10 staging install runbook (the canonical shape this
  W19 runbook mirrors).
- [`docs/helm-charts.md`](./helm-charts.md) §3.5 — W9 canary
  AnalysisTemplate gates (inert until this install lands).
- [`infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`](../infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml)
  — W11 auth-aware ingress (post-install §3 step 1).
- [`infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`](../infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml)
  — W12 NetworkPolicy (post-install §3 step 2).
- [`infra/k8s/base/argo-rollouts-prereqs/namespace.yaml`](../infra/k8s/base/argo-rollouts-prereqs/namespace.yaml)
  — W19 pre-install namespace stub.
- [`infra/k8s/base/argo-rollouts-prereqs/rbac.yaml`](../infra/k8s/base/argo-rollouts-prereqs/rbac.yaml)
  — W19 pre-install RBAC stubs (canary-promoter ClusterRole
  + rollouts-reader Role).
- Argo Rollouts upstream docs —
  <https://argoproj.github.io/argo-rollouts/installation/>.
