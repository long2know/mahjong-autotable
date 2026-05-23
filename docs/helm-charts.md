# Helm chart-of-charts — `mahjong-autotable`

> Phase K Wave 7 — Apone (DevOps).

This document covers the W7 Helm chart-of-charts at
[`helm/mahjong/`](../helm/mahjong/) — the umbrella + three
subcharts that together deploy the API runtime plane.

The chart **runs in parallel** with the existing Kustomize tree
at `infra/k8s/base/` + `infra/k8s/overlays/{staging,prod}/`.
Both paths ship the SAME runtime contract; pick one per
deployment site.

## 1. Layout

```
helm/mahjong/
├── Chart.yaml                          # umbrella, 3 aliased deps
├── values.yaml                         # umbrella defaults
├── values-staging.yaml                 # staging overrides
├── values-prod.yaml                    # prod overrides
└── charts/
    ├── mahjong-api/                    # API Deployment + Service + Ingress + HPA + ConfigMap + PVC + migration Job
    ├── mahjong-coturn/                 # coturn 4.6 (HMAC mode, NetworkPolicy, ExternalSecret, AZ-spread)
    └── mahjong-postgres-sidecar/       # optional in-cluster Postgres StatefulSet (staging soak only)
```

### 1.1 The alias quirk

Helm routes umbrella `values.yaml` to subcharts **by chart
NAME** unless `alias:` is set on the dependency. Our subcharts
are named `mahjong-api`, `mahjong-coturn`, `mahjong-postgres-sidecar`
but the umbrella values use the short keys `api`, `coturn`,
`postgresSidecar` for readability. The umbrella `Chart.yaml`
declares an `alias:` for each dependency:

```yaml
dependencies:
  - name: mahjong-api
    alias: api
    version: 0.1.0
    repository: file://./charts/mahjong-api
  - name: mahjong-coturn
    alias: coturn
    version: 0.1.0
    repository: file://./charts/mahjong-coturn
  - name: mahjong-postgres-sidecar
    alias: postgresSidecar
    version: 0.1.0
    repository: file://./charts/mahjong-postgres-sidecar
    condition: postgresSidecar.enabled
```

Without the aliases, umbrella overrides under `api.*` /
`coturn.*` / `postgresSidecar.*` are silently ignored — the
subcharts keep their own defaults. The W7 implementation hit
this trap once (PVC rendered in prod despite
`api.persistence.enabled: false`); the alias wiring is the
documented fix.

## 2. Quick-start

### 2.1 Local render (no cluster needed)

```bash
# Render staging.
helm template mahjong helm/mahjong/ \
    -f helm/mahjong/values-staging.yaml \
    > rendered-staging.yaml

# Render prod.
helm template mahjong helm/mahjong/ \
    -f helm/mahjong/values-prod.yaml \
    > rendered-prod.yaml

# Lint (CI parity).
helm lint helm/mahjong/
```

The repo's `.tool-helm/helm` is the pinned helm binary used by
the W7 verification gate (v3.16.4). Use whichever recent
helm v3.x is on your `$PATH` — the chart targets no
helm-version-specific features.

### 2.2 Install order

The chart-of-charts assumes the following are **already
provisioned**:

1. **The target namespace** — `mahjong-prod` / `mahjong-staging`.
   The chart does NOT create the namespace (helm best-practice;
   namespace lifecycle is operator-owned).
2. **External Secrets Operator** — installed cluster-wide. The
   chart's ESO templates assume `external-secrets.io/v1beta1`
   CRDs are available.
3. **The omnibus + JWT secrets** (`mahjong-autotable`,
   `mahjong-jwt-keys`, `mahjong-jwt-rsa-keys`) — materialised
   either by the Kustomize overlays' ExternalSecret manifests
   OR by the chart's own ExternalSecret templates (umbrella
   value `externalSecrets.enabled` — defaults `true`).
4. **Ingress controller** — nginx-ingress installed cluster-wide.
5. **cert-manager** — for the Ingress TLS issuer
   (`cluster-issuer-letsencrypt-prod` / `-staging`).
6. **(prod only) coturn STUN/TURN reachability** — DNS A record
   + UDP/3478 NAT pinhole (W6 setup; see
   [`docs/turn.md`](turn.md)).

Install:

```bash
# Staging.
helm upgrade --install mahjong helm/mahjong/ \
    -n mahjong-staging --create-namespace \
    -f helm/mahjong/values-staging.yaml

# Prod.
helm upgrade --install mahjong helm/mahjong/ \
    -n mahjong-prod \
    -f helm/mahjong/values-prod.yaml
```

The chart's `Job` template runs DB migrations **pre-rollout**
(annotation `helm.sh/hook: pre-upgrade,pre-install`). The hook
is a separate object that helm reaps on success.

## 3. Helm vs Kustomize — when to use which

Both paths ship in this repo, in parallel. The decision matrix:

| Concern | Kustomize (`infra/k8s/`) | Helm (`helm/mahjong/`) |
|---|---|---|
| Day-1 operator tooling | `kubectl apply -k` — zero extra installs | `helm` — extra binary on the operator workstation |
| Templating power | Patch-based, no conditionals | Go templates, conditionals, ranges |
| Overlay isolation | One overlay = one directory | One values file per environment |
| Subchart composition | Manual via remote bases | First-class via `dependencies:` |
| Hook ordering (pre-install migrations, etc.) | None native — operator runs scripts | `helm.sh/hook` annotations |
| Rollback semantics | `kubectl apply -k` of prior commit | `helm rollback <release> <revision>` |
| Diff before apply | `kustomize build | diff` | `helm diff upgrade` (plugin) |

**Recommended split:**

* **Kustomize** is the SoT for the CI-deploy path
  (`docs/production-deployment-runbook.md` — the W4/W5/W6
  pipeline runs `kubectl apply -k`).
* **Helm** is the SoT for operator-driven point-installs +
  third-party / partner deploys that prefer helm tooling.

Both render the same Deployment, Service, ConfigMap, ExternalSecret,
Ingress, HPA, PVC, and coturn objects. The W7 acceptance gate is
**parity** — see §4.

## 4. Parity matrix (W7 — chart-of-charts vs Kustomize)

The two paths produce equivalent (NOT byte-identical — name
prefixes + labels differ) manifests. The matrix below lists
each object type + the conditions under which each path
renders it:

| Kind | Kustomize | Helm | Notes |
|---|---|---|---|
| Namespace | external (`kubectl create`) | external (`kubectl create` or `--create-namespace`) | Neither path manages namespace lifecycle. |
| Deployment | base + overlay patches | `mahjong-api/templates/deployment.yaml` | Same image, same envFrom mounts. |
| Service | base | `mahjong-api/templates/service.yaml` | Same selector. |
| Ingress | base + overlay patches | `mahjong-api/templates/ingress.yaml` | Same `cert-manager.io/cluster-issuer` annotation. |
| HorizontalPodAutoscaler | base + overlay patches | `mahjong-api/templates/hpa.yaml` | Helm conditional on `api.autoscaling.enabled`. |
| ConfigMap | `configMapGenerator` | `mahjong-api/templates/configmap.yaml` | Helm renders the same `Persistence__Provider` / `Cors__AllowedOrigins__0` keys. |
| PersistentVolumeClaim | conditional patch | `mahjong-api/templates/pvc.yaml` (conditional on `api.persistence.enabled`) | Staging-only by default; prod uses RDS. |
| Job (migration) | external (operator runs) | `mahjong-api/templates/job-migrate.yaml` (helm pre-upgrade hook) | Helm gets the pre-rollout hook for free. |
| ExternalSecret (omnibus) | `secret-template.yaml` (overlay resource) | `mahjong-api/templates/external-secret.yaml` (`externalSecrets.enabled`) | Both produce `mahjong-autotable` Secret. |
| ExternalSecret (HS256 JWT) | `jwt-keys-secret.yaml` (overlay resource — out-of-band in prod) | `mahjong-api/templates/external-secret.yaml` | `mahjong-jwt-keys` / `-staging`. |
| ExternalSecret (RS256 JWT) | `jwt-rsa-keys-secret.yaml` (overlay resource — out-of-band in prod) | `mahjong-api/templates/external-secret.yaml` | `mahjong-jwt-rsa-keys` / `-staging`. |
| coturn Deployment | base + overlay patches | `mahjong-coturn/templates/deployment.yaml` (`coturn.enabled`) | Prod-only. |
| coturn Service (UDP 3478) | base | `mahjong-coturn/templates/service.yaml` | LoadBalancer in prod. |
| coturn NetworkPolicy | base | `mahjong-coturn/templates/networkpolicy.yaml` | Allows IANA ephemeral relay range. |
| coturn ExternalSecret | overlay resource | `mahjong-coturn/templates/external-secret.yaml` | `mahjong-coturn-secret` (HMAC static-auth). |
| Postgres StatefulSet | NOT in Kustomize (staging uses Sqlite by default) | `mahjong-postgres-sidecar/templates/statefulset.yaml` (`postgresSidecar.enabled`) | Helm-only; staging soak convenience. |
| Postgres Service | — | `mahjong-postgres-sidecar/templates/service.yaml` | — |

## 5. Subchart toggles

The umbrella `values.yaml` ships a sane default. The environment
files override only the deltas. Significant toggles:

| Key | Default | Staging | Prod | Purpose |
|---|---|---|---|---|
| `api.replicaCount` | 1 | 1 | 3 | Min replicas. |
| `api.autoscaling.enabled` | false | false | true | HPA on/off. |
| `api.autoscaling.minReplicas` | 1 | — | 3 | HPA floor (matches Deployment replicas). |
| `api.autoscaling.maxReplicas` | 4 | — | 12 | HPA ceiling. |
| `api.persistence.enabled` | false | true | false | Sqlite PVC for staging; RDS for prod. |
| `api.ingress.hsts.preload` | false | false | true | HSTS preload header on the Ingress. |
| `api.podAntiAffinity` | none | none | required | Spread API replicas across AZs in prod. |
| `coturn.enabled` | false | false | true | Skip coturn in staging (mahjong sessions don't need TURN in soak). |
| `postgresSidecar.enabled` | false | true | false | In-cluster Postgres for staging only. |
| `externalSecrets.enabled` | true | true | true | ESO ExternalSecret rendering. |
| `externalSecrets.jwtRsaSecretName` | `mahjong-jwt-rsa-keys` | `mahjong-jwt-rsa-keys-staging` | `mahjong-jwt-rsa-keys` | Wave 7 RS256 secret. |

## 6. Verification — pre-merge gate

The W7 acceptance gate is:

```bash
# 1. Lint umbrella + all subcharts.
helm lint helm/mahjong/

# 2. Render both overlays.
helm template mahjong helm/mahjong/ \
    -f helm/mahjong/values-staging.yaml > .work/helm-staging.yaml
helm template mahjong helm/mahjong/ \
    -f helm/mahjong/values-prod.yaml    > .work/helm-prod.yaml

# 3. YAML safe-load (catches templating typos that render
#    invalid YAML).
python3 -c "
import yaml
for p in ['.work/helm-staging.yaml', '.work/helm-prod.yaml']:
    list(yaml.safe_load_all(open(p)))
    print('OK', p)
"
```

All three steps MUST be green before a chart PR merges. CI
parity ride-along is a W8 follow-up.

## 7. Cross-references

* [`helm/mahjong/Chart.yaml`](../helm/mahjong/Chart.yaml) — umbrella + aliased dependencies.
* [`helm/mahjong/values.yaml`](../helm/mahjong/values.yaml) — umbrella defaults.
* [`helm/mahjong/values-staging.yaml`](../helm/mahjong/values-staging.yaml) — staging overrides.
* [`helm/mahjong/values-prod.yaml`](../helm/mahjong/values-prod.yaml) — prod overrides.
* [`helm/README.md`](../helm/README.md) — chart-side README + quick-start.
* [`infra/k8s/base/`](../infra/k8s/base/) — Kustomize SoT (deploy path).
* [`infra/k8s/overlays/staging/`](../infra/k8s/overlays/staging/) — Kustomize staging overlay.
* [`infra/k8s/overlays/prod/`](../infra/k8s/overlays/prod/) — Kustomize prod overlay.
* [`docs/production-deployment-runbook.md`](production-deployment-runbook.md) — CI deploy runbook (Kustomize path).
* [`docs/turn.md`](turn.md) — coturn STUN/TURN setup.
* [`docs/jwt-rotation.md`](jwt-rotation.md) §8 — RS256 JWT provisioning.
