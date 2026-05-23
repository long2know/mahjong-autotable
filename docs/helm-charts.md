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

## 3. Canary deploys (Phase K Wave 8 — Apone)

W8 adds a canary deployment strategy on top of the W7 chart-of-
charts. When `canary.enabled = true`, the umbrella template
[`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml)
renders an [Argo Rollouts](https://argoproj.github.io/argo-rollouts/)
`Rollout` resource that progressively shifts traffic from the
stable to the canary ReplicaSet through operator-defined steps
with automated promotion gated on a Prometheus success-rate
metric.

### 3.1 Why Argo Rollouts (not Flagger)

| Concern | Argo Rollouts (W8 choice) | Flagger |
|---|---|---|
| K8s-native | `Rollout` CRD is a drop-in for `Deployment` | Operates ALONGSIDE Deployment via `Canary` CRD |
| Service-mesh dependency | None — replica-based + nginx-canary plugin | Requires Istio / Linkerd / nginx-ingress-canary |
| CLI tooling | `kubectl argo rollouts <verb>` | Flagger annotations + manual `kubectl edit` |
| Alignment | Same vendor as Argo CD (Phase L candidate) | Distinct vendor |
| Rollback verb | `kubectl argo rollouts undo` | Re-deploy prior Deployment |

Argo Rollouts also supports nginx-ingress traffic-splitting via
its nginx plugin — the chart's baseline is replica-based
(simpler), the doc below covers the traffic-split upgrade path.

### 3.2 Values surface

```yaml
canary:
  enabled: false                    # off by default

  # Advanced: keep the static Deployment AND the Rollout alive
  # in parallel. ONLY for staging soak.
  coexistWithDeployment: false

  revisionHistoryLimit: 3           # `kubectl argo rollouts undo` history
  scaleDownDelaySeconds: 30         # drain delay before old pods terminate

  # Canary step sequence — W8 baseline: 5 → 20 → 50 → 100 %.
  steps:
    - setWeight: 5
    - pause: { duration: "2m" }
    - analysis: true
    - setWeight: 20
    - pause: { duration: "5m" }
    - analysis: true
    - setWeight: 50
    - pause: { duration: "10m" }
    - analysis: true
    - setWeight: 100

  # Prometheus URL the AnalysisTemplate hits. Empty string =
  # analysis steps are no-ops (duration-only progression).
  metricEndpoint: ""                # e.g. http://prometheus-server.monitoring.svc.cluster.local:80

  analysis:
    interval: "30s"                 # poll cadence
    count: 5                        # consecutive successes required
    successThreshold: 0.95          # min success rate (95%)
    failureLimit: 1                 # polls below threshold → abort
```

The W8 baseline matches the spec: 5 → 20 → 50 → 100 % with pause
+ analysis between each step. The success-rate query is
hard-coded to `http_requests_total{service=...,code!~"5.."}` —
override by editing the rendered AnalysisTemplate or by forking
the chart's template.

### 3.3 Step semantics

| Step shape | Behaviour |
|---|---|
| `{ setWeight: <int 0-100> }` | Set canary traffic % to N |
| `{ pause: { duration: "<go-duration>" } }` | Pause for the duration |
| `{ pause: {} }` | Pause indefinitely; operator runs `kubectl argo rollouts promote <name>` |
| `{ analysis: true }` | Run the success-rate AnalysisTemplate; abort + rollback on failure |

The W8 baseline pairs every weight bump with a pause + analysis.
Operator can shorten the pauses for hotfix rollouts (e.g. set
`pause.duration: "30s"`) by overriding `canary.steps` in
values-prod.yaml.

### 3.4 Co-existence guard

The Rollout's `selector` matches the SAME labels as the subchart
Deployment (`app.kubernetes.io/name=mahjong-autotable` +
`app.kubernetes.io/component=api`). When both `api.enabled = true`
AND `canary.enabled = true`, two controllers manage the same
selector — the static Deployment + the Rollout race over pod
ownership, ReplicaSets churn, and the staged rollout collapses.

The chart errors loudly on render:

```
Error: execution error at (mahjong/templates/canary-deployment.yaml:54:4):
  canary.enabled = true AND api.enabled = true — two controllers
  managing the same ReplicaSet selector. Set api.enabled = false,
  OR (advanced) set canary.coexistWithDeployment = true.
```

Production usage MUST flip `api.enabled = false` when enabling
the canary path. The override `canary.coexistWithDeployment = true`
is reserved for staging soak scenarios where the operator wants
to compare static + canary surfaces side-by-side.

### 3.5 AnalysisTemplate gates (Phase K Wave 9 — Apone)
<a name="canary-analysis"></a>

W8 shipped a single `success-rate` AnalysisTemplate as the only
automated gate between canary steps. The W8 retro flagged this as
**under-instrumented for production**: a canary that doesn't
regress on the 5xx rate but ships a 2× latency increase or burns
through the error budget would pass the W8 gate and promote.

W9 retargets the W8 single template into **three independent
templates**, each evaluating a distinct production signal. The
Rollout's `analysis` step references all three; **any one failing**
aborts the rollout (Argo Rollouts evaluates templates in parallel
and the union of `failureLimit` trips short-circuits).

| Template | Signal | Default threshold | Tunable via |
|---|---|---|---|
| `…-canary-success-rate` | Non-5xx response fraction (rolling) | `result[0] >= 0.99` | `canary.analyses.successRate.threshold` |
| `…-canary-p99-latency` | p99 request latency (ms, rolling) | `result[0] <= 500` | `canary.analyses.p99Latency.threshold` |
| `…-canary-error-budget` | Burn rate against SLO (5xx / `sloErrorRate`) | `result[0] < 14.4` | `canary.analyses.errorBudget.threshold` |

The default window is 5m (`count: 10` × `interval: 30s`); the
default `failureLimit` is 1 (a single window below threshold
trips the abort).

**Success-rate** — same shape as W8, with the metric series
configurable:

```promql
sum(rate({{ .metric }}{service="{{ canaryService }}",code!~"5.."}[{{ window }}]))
/
sum(rate({{ .metric }}{service="{{ canaryService }}"}[{{ window }}]))
```

Threshold `>= 0.99` means the canary must serve at LEAST 99% non-
5xx over the 5m window. Production overrides may raise this to
`>= 0.995` for high-SLO services.

**p99 latency** — uses Prometheus `histogram_quantile`:

```promql
histogram_quantile(0.99,
  sum by (le)(
    rate({{ .metric }}{service="{{ canaryService }}"}[{{ window }}])
  )
) * 1000
```

The metric series defaults to `http_request_duration_seconds_bucket`
(ASP.NET Core OpenTelemetry exports this name when the
`AspNetCoreInstrumentation` is configured with the default
histogram). The `* 1000` converts seconds to ms so the threshold
in the values file is human-readable in ms.

Threshold `<= 500` is 500 ms. Override via
`canary.analyses.p99Latency.threshold` per environment.

**Error budget burn rate** — Google SRE multi-window fast-burn
pattern:

```promql
sum(rate({{ .metric }}{service="{{ canaryService }}",code=~"5.."}[{{ window }}]))
/
sum(rate({{ .metric }}{service="{{ canaryService }}"}[{{ window }}]))
/
{{ sloErrorRate }}
```

The `sloErrorRate` is the operator's defined acceptable 5xx rate
(default `0.01` = 99% availability SLO). The query produces the
**burn rate**: how fast the canary is consuming the error budget
relative to the SLO.

Threshold `< 14.4` is the Google SRE recommended "fast-burn"
alert threshold (2% of monthly budget burned in 1h, at SLO =
99%). Crossing 14.4 inside a 5m window means continuing the
rollout would exhaust the budget faster than the SRE team can
respond — abort.

**Tuning playbook for operators:**

| Symptom | Adjust |
|---|---|
| Canary aborts too often during normal traffic | Increase `count` (longer window) or `failureLimit` (more tolerant) |
| Latency gate trips on cold-start | Add a warm-up step before the analysis step (e.g. `setWeight: 5` + `pause: 2m` BEFORE the analysis-gated step) |
| Different metric name in your Prometheus | Override `canary.analyses.<name>.metric` |
| Different success criterion (median, p95) | Edit the template directly — values surface intentionally narrow, advanced criteria are a fork-and-modify path |

**Disabling a single template** (e.g. you don't have latency
histogram instrumentation yet):

```yaml
canary:
  analyses:
    p99Latency:
      enabled: false   # success-rate + error-budget still gate
```

The Rollout `analysis.templates[]` is computed at render time —
only enabled templates contribute. At least one MUST be enabled
or the analysis step is no-op (chart does not currently hard-
error in this case; rendering an empty `templates: []` is treated
as "no gate" by Argo Rollouts).

### 3.6 Operator runbook

#### 3.6.1 Cluster prereq — install Argo Rollouts (once)

```bash
kubectl create namespace argo-rollouts
kubectl apply -n argo-rollouts \
    -f https://github.com/argoproj/argo-rollouts/releases/latest/download/install.yaml

# Optional: CLI for live status.
curl -fsSL -o ~/.local/bin/kubectl-argo-rollouts \
    https://github.com/argoproj/argo-rollouts/releases/latest/download/kubectl-argo-rollouts-linux-amd64
chmod +x ~/.local/bin/kubectl-argo-rollouts
```

#### 3.6.2 Cut a canary release

```bash
# Enable canary in the values overlay (or use --set):
helm upgrade --install mahjong helm/mahjong/ \
    -n mahjong-prod \
    -f helm/mahjong/values-prod.yaml \
    --set canary.enabled=true \
    --set api.enabled=false \
    --set canary.metricEndpoint=http://prometheus-server.monitoring.svc.cluster.local:80 \
    --set api.image.tag=v0.17.1

# Watch progression:
kubectl argo rollouts get rollout mahjong-autotable-canary -n mahjong-prod --watch

# If a pause step is { pause: {} } (no duration), manually promote:
kubectl argo rollouts promote mahjong-autotable-canary -n mahjong-prod

# Abort + rollback:
kubectl argo rollouts abort mahjong-autotable-canary -n mahjong-prod
kubectl argo rollouts undo  mahjong-autotable-canary -n mahjong-prod
```

#### 3.6.3 Health metric — wire to your Prometheus

Set `canary.metricEndpoint` to the in-cluster Prometheus URL:

```yaml
canary:
  metricEndpoint: "http://prometheus.monitoring.svc.cluster.local:9090"
```

W9 retargets the W8 single AnalysisTemplate into three (see
§3.5). The default Prometheus queries are documented in §3.5;
each query inherits `canary.metricEndpoint` and the per-template
`metric` / `window` settings.

Override the metric name (e.g. you use `request_count_total`
instead of `http_requests_total`):

```yaml
canary:
  analyses:
    successRate:
      metric: request_count_total
```

#### 3.6.4 Aborting + rollback

Argo Rollouts auto-aborts when the AnalysisTemplate's
`failureLimit` is hit (`successCondition` false N times). The
Rollout immediately scales the canary ReplicaSet to 0, shifts all
traffic back to stable, and emits an `Event` (visible via
`kubectl get events`).

Manual abort (operator):

```bash
kubectl argo rollouts abort mahjong-autotable-canary -n mahjong-prod
```

To recover after abort:

```bash
# Undo to the prior known-good revision (last in
# revisionHistoryLimit):
kubectl argo rollouts undo mahjong-autotable-canary -n mahjong-prod

# Or re-run the upgrade with a known-good image tag:
helm upgrade mahjong helm/mahjong/ ... --set api.image.tag=<known-good-tag>
```

### 3.7 Cross-references

* [`helm/mahjong/templates/canary-deployment.yaml`](../helm/mahjong/templates/canary-deployment.yaml) — the Rollout + 3 AnalysisTemplates + stable/canary Services.
* [`helm/mahjong/values.yaml`](../helm/mahjong/values.yaml) — `canary.*` defaults including `analyses.{successRate,p99Latency,errorBudget}`.
* [`helm/mahjong/values-prod.yaml`](../helm/mahjong/values-prod.yaml) — production overrides (99% success / 500 ms p99 / 14.4 burn rate).
* [Argo Rollouts — Canary strategy](https://argoproj.github.io/argo-rollouts/features/canary/)
* [Argo Rollouts — AnalysisTemplate](https://argoproj.github.io/argo-rollouts/features/analysis/)
* [Google SRE Workbook — Burn-rate alerts](https://sre.google/workbook/alerting-on-slos/)

## 4. Helm vs Kustomize — when to use which

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

## 5. Parity matrix (W7 — chart-of-charts vs Kustomize)
<a name="parity-matrix"></a>

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

## 6. YAML anchor pattern in values files (W9 — Apone)
<a name="yaml-anchor-pattern"></a>

W8 retro flagged a maintenance smell in `values-staging.yaml` and
`values-prod.yaml`: each environment's hostname / TLS-secret /
CORS-origin / env-name appears in **5–7 distinct keys**
(ingress hosts, ingress TLS, ingress annotations, environment
config, externalSecrets refresh interval — and now W9 added
canary `metricEndpoint`). Editing the hostname required hunting
down every occurrence; the W4 staging-cutover retrospective
called out one such miss (the externalSecrets refresh interval
was updated, but the ingress annotation lagged).

W9 introduces a **YAML anchor convention** to centralise these
shared scalars at the top of each values file. The pattern uses
a top-level key `x-anchors:` to declare anchors and reference
them with `*name` throughout the rest of the file.

**Why `x-anchors:` and not a normal key:**

Helm's templating engine merges the YAML into the chart `.Values`
object, where extra top-level keys are silently ignored (they
become accessible as `.Values.x-anchors.staging-host` but no
template references them). Helm has no formal "ignored prefix"
convention — but `x-*` is the de-facto convention from
OpenAPI / docker-compose / GitHub Actions for "extension /
ignored / for-humans-only" keys. Using `x-anchors:` reads as
documentation, parses cleanly via PyYAML (the W7 verification
script's `safe_load_all` accepts it), and survives `helm template`
without rendering.

**Anchor convention:**

```yaml
# At the TOP of values-prod.yaml (after any header comments):
x-anchors:
  - &prod-host             "play.mahjong.example.com"
  - &prod-tls-secret       "mahjong-prod-tls"
  - &prod-env-name         "production"
  - &prod-cors-origin      "https://play.mahjong.example.com"
  - &prod-prometheus       "http://prometheus.monitoring.svc.cluster.local:9090"
  # Add new anchors here; reference via *name throughout the file.

# Later in the same file:
api:
  ingress:
    hosts:
      - host: *prod-host
        paths: [...]
    tls:
      - hosts: [*prod-host]
        secretName: *prod-tls-secret
  env:
    ASPNETCORE_ENVIRONMENT: *prod-env-name
    Cors__AllowedOrigins__0: *prod-cors-origin
canary:
  metricEndpoint: *prod-prometheus
```

**Doc cross-references are symbolic, not numeric:**

Earlier values files referenced sections like "see §3.5 of
docs/helm-charts.md". When `helm-charts.md` was renumbered in W8
(adding §3 canary deploys shifted §3-§7 → §4-§8), every numeric
reference in the values files broke silently. W9 switches to
**symbolic anchors** in the values-file docstring:

```yaml
# See:
#   docs/helm-charts.md §parity-matrix      — staging vs prod deltas
#   docs/helm-charts.md §subchart-toggles   — toggle table
#   docs/helm-charts.md §canary-analysis    — AnalysisTemplate details
#   docs/helm-charts.md §yaml-anchor-pattern — this convention
```

The doc's `<a name="parity-matrix"></a>` anchors (added W9)
provide a stable target — section renumbering doesn't break the
reference, and a `Ctrl-F` in the doc by anchor name finds the
intended section.

**When NOT to use anchors:**

- **Subchart values that need per-overlay distinct typing.** If
  the staging file passes a string and the prod file passes a
  list, an anchor flattening confuses readers more than it helps.
- **Single-occurrence values.** No DRY benefit; the anchor
  declaration is pure overhead.
- **Inside subchart values** (`charts/<subchart>/values.yaml`).
  The umbrella merge semantics interact poorly with anchors that
  point into subchart scope — keep anchors at the overlay level.

**Verification:**

```bash
# Anchors must round-trip cleanly through PyYAML safe_load_all:
python3 -c "
import yaml
for p in ['helm/mahjong/values-staging.yaml', 'helm/mahjong/values-prod.yaml']:
    docs = list(yaml.safe_load_all(open(p)))
    print('OK', p, 'docs:', len(docs))
"

# And helm must accept them (anchors resolved at parse time):
./.tool-helm/helm template mahjong helm/mahjong/ \
    -f helm/mahjong/values-prod.yaml > .work/render.yaml
grep "play.mahjong.example.com" .work/render.yaml | head -3
# Should show the anchor's value resolved into 5+ places.
```

Both checks land in the W9 acceptance gate (§8 below).

## 7. Subchart toggles
<a name="subchart-toggles"></a>

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

## 8. Verification — pre-merge gate

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

## 9. Cross-references

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
