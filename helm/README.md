# `helm/` — Helm chart-of-charts (Phase K Wave 7)

> Phase K Wave 7 — Apone (DevOps).

`helm/mahjong/` is a Helm 3 umbrella chart that parallels the
existing Kustomize tree under `infra/k8s/base/` + `infra/k8s/overlays/`.
**Both paths are supported in parallel — Helm is NOT a migration
away from Kustomize.** Operator picks the workflow that fits the
shop; the chart-of-charts mirrors the same runtime contract.

See [`docs/helm-charts.md`](../docs/helm-charts.md) for the full
reference + parity matrix.

## Quick-start

```bash
# Lint the chart (CI-equivalent).
helm lint helm/mahjong

# Dry-run the staging install (no cluster contact).
helm template mahjong helm/mahjong \
    -n mahjong-staging \
    -f helm/mahjong/values-staging.yaml

# Real install — staging.
helm upgrade --install mahjong helm/mahjong \
    -n mahjong-staging --create-namespace \
    -f helm/mahjong/values-staging.yaml

# Real install — production.
helm upgrade --install mahjong helm/mahjong \
    -n mahjong-prod --create-namespace \
    -f helm/mahjong/values-prod.yaml
```

The chart references three subcharts under `charts/`:

| Subchart | What it ships | Default state |
|---|---|---|
| `mahjong-api` | .NET API Deployment + Service + Ingress + HPA + ConfigMap + (optional) PVC + (optional) pre-rollout migration Job | enabled |
| `mahjong-coturn` | coturn 4.6 (HMAC mode) Deployment + NLB Service + NetworkPolicy + (optional) ExternalSecret | enabled |
| `mahjong-postgres-sidecar` | Single-replica Postgres StatefulSet (**staging soak only — NEVER prod**) | **disabled** |

## Subchart toggles

Each subchart is gated by a top-level `condition:` field in
`Chart.yaml`:

```yaml
api.enabled:              true   # toggle the API surface
coturn.enabled:           true   # toggle the TURN server
postgresSidecar.enabled:  false  # toggle the staging Postgres
```

Disable a subchart and `helm template` simply omits its
resources — no orphaned objects, no half-rendered manifests.

## Parity matrix with Kustomize

Documented in `docs/helm-charts.md` §3. The W7 invariant: **any
patch in `infra/k8s/overlays/<env>/kustomization.yaml` MUST have
a matching values key in `helm/mahjong/values-<env>.yaml`**. The
two trees ship the same runtime contract.

## What this chart does NOT manage

Out-of-scope for the umbrella (managed elsewhere):

* **Cluster add-ons** — ESO, cert-manager, AWS LBC, Kyverno. These
  are cluster-bootstrap concerns; install them via their own
  charts following the runbook in
  [`docs/kubernetes.md`](../docs/kubernetes.md) BEFORE installing
  `mahjong/`.
* **Kyverno policies** — `infra/k8s/policies/` ships
  ClusterPolicies that gate image signatures + SLSA attestations
  cluster-wide. Apply via `kubectl apply -f infra/k8s/policies/`
  alongside the Helm install — they are NOT part of the umbrella.
* **DR + replication topology** — `infra/terraform/modules/dr-replication/`
  manages cross-region RDS replication + ECR mirror + Route 53
  failover records. Stays on the Terraform side.
* **Edge surface** — `infra/terraform/modules/edge/` (W7) wires
  Route 53 + ACM + WAFv2 + CloudFront. Stays on the Terraform
  side. The chart's Ingress is the cluster-side endpoint; the
  edge module is what fronts it on the public internet.

## Linting + CI

`helm lint helm/mahjong` is the W7 gate (PASS-required on every
PR that touches `helm/`).

`helm template` PARSES every subchart's manifests through
`yaml.safe_load_all`; CI runs both as part of the standard
build invariants (Apone's W7 build invariant list).
