# Apone — Phase K Wave 12 memo

**Branch:** `stlong/phase-k-wave-12-bringup`
**Date:** 2026-10-XX
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Prod cutover readiness runbook
(`docs/prod-cutover.md` NEW), prod kustomization wire-up of the
W11 hand-offs (Redis ESO + Argo Rollouts ingress) plus new W12
Argo Rollouts NetworkPolicy hardening — all unblocked by a
cross-namespace kustomize pattern (`NamespaceTransformer +
unsetOnly: true`), Redis load-test re-baseline against the prod
shape, per-region R53 records in the EDGE module, second JWT
rotation rehearsal documentation (W12 39 % faster than W11),
CHANGELOG bump to **0.21.0** + retro 2026-10.

---

## Decisions

### D1 — Single-pane prod cutover runbook at `docs/prod-cutover.md`

**Why:** the W10 → W11 progression accumulated cutover-related
notes across `docs/redis-cluster.md §11`,
`docs/argo-rollouts-setup.md §5`,
`docs/edge-region-probes.md §3`,
`docs/production-deployment-runbook.md` (W7), and the file
headers on the W11 ESO + Ingress manifests. Operators trying to
sequence the actual cutover had to grep across all of them. W12
consolidates into a single chronological doc.

**Shape:** five sections — 1. terraform plan readiness, 2.
kustomization wire-up, 3. cutover-ready checklist (gated by
agent lane), 4. cross-namespace kustomize pattern rationale, 5.
rollback playbook (application / infrastructure / edge).

**Convention for future cutovers:** mirror the five-section
shape. Each cutover gets its own `docs/<cutover>-cutover.md`
doc rather than appending sections to existing runbooks. The
existing runbooks back-reference into the cutover doc; the
cutover doc is the FORWARD-pointing source of truth.

### D2 — Cross-namespace kustomize pattern via `NamespaceTransformer + unsetOnly: true`

**Why:** the W11 hand-off manifests
`redis-connection-string-secret.yaml` (in-namespace
`mahjong-prod`) + `argo-rollouts-ingress-auth.yaml` (cross-
namespace `argo-rollouts`) need to land in the prod overlay's
`resources:` list. The W11 baseline used a top-level
`namespace: mahjong-prod` directive that REWRITES every
resource's namespace — works for the in-namespace ESO, BREAKS
the cross-namespace Ingress.

**Decision:** swap `namespace: mahjong-prod` for a
`transformers:` entry pointing at a new
`namespace-transformer.yaml` file (inline
`NamespaceTransformer` with `unsetOnly: true`). The transformer
only sets `metadata.namespace` if it's currently empty —
resources with a pre-declared namespace keep their declared
value.

**Alternatives rejected:**

* **JSON6902 patch with `op: replace path: /metadata/namespace`** —
  patches run AFTER the namespace transformer in kustomize's
  pipeline; the patch sees the rewritten `mahjong-prod` already
  applied + has to re-rewrite. Brittle.
* **Strategic merge patch with explicit `metadata.namespace`** —
  same ordering problem.
* **Kustomize Component (`apiVersion: kustomize.config.k8s.io/v1alpha1
  kind: Component`)** — components do NOT escape the parent
  kustomization's namespace transformer. Verified empirically.
* **Sub-base inclusion** — same: namespace transformer
  propagates through bases.
* **`replacements:` directive on a ConfigMap with the target
  namespace value** — works, but introduces an unwanted
  ConfigMap into the cluster + the wire is non-obvious.
* **Split into TWO kustomizations** (one per namespace) — rejected
  for ergonomic reasons. The current single `kustomize build
  infra/k8s/overlays/prod/` command is referenced in every
  operator runbook; splitting forces two apply commands +
  ordering gates.

**Convention for future cross-namespace fan-out:** the
`NamespaceTransformer + unsetOnly: true` pattern is the
canonical solution. Documented in
`docs/prod-cutover.md §4` so the next operator hitting the
constraint doesn't re-discover the technique.

### D3 — W11 file headers flipped IN-BAND, body unchanged

**Why:** the W11 file headers on
`redis-connection-string-secret.yaml` and
`argo-rollouts-ingress-auth.yaml` carried "OUT-OF-BAND
TEMPLATE — NOT in any kustomization.yaml resources list"
notices. W12 wires both into the prod overlay; the notices
are now stale.

**Decision:** update file headers to reflect the W12 wire-in
("IN-BAND. Wired into the prod overlay via `kustomization.yaml`
… See `docs/prod-cutover.md §2`"). Body of each manifest
UNCHANGED — no risk of behavioural drift.

**Convention:** when a hand-off ships with an "OUT-OF-BAND"
qualifier in its file header, the next wave that wires it in-
band MUST update the header. Stale headers are worse than no
headers — operators trust file headers as a quick sanity
check.

### D4 — `optional: true` on the new Redis envFrom mount

**Why:** the W12 wire-in adds a deployment patch with
`envFrom: secretRef: name: mahjong-redis-prod optional: true`.
The `optional` flag preserves the cutover-safe fall-through:
if ESO hasn't yet hydrated the `mahjong-redis-prod` Secret
(e.g. on a fresh cluster bring-up where the AWS Secrets
Manager IAM role hasn't propagated yet), the container starts
WITHOUT the env var rather than CrashLoopBackOff-ing.

**Fallback chain:** the in-process omnibus
`mahjong-autotable` Secret carries
`Idempotency:Redis:ConnectionString` as a fallback (W4
omnibus structure). The in-memory idempotency provider remains
the default until `Idempotency:Provider=Redis` is flipped via
the configmap. Layered defaults — cutover is a sequence of
flips, not a big-bang.

**Convention:** every envFrom mount for a Secret materialised
by an ExternalSecret SHOULD use `optional: true` for the
duration of the cutover window. Once the cluster is steady-
state (ESO health is monitored, the IAM trust is stable), a
follow-up wave can flip `optional: false` to make the runtime
require the secret. Tracked as an open item for W13+.

### D5 — Per-region R53 records gated by EMPTY default

**Why:** the W12 EDGE module surface adds per-region health
checks + per-region ALIAS records + a latency-based apex RR
set, but the regional EKS clusters that would back these
records are blocked on a Hicks W12+ deliverable. Operators
who run `terraform plan` against the prod env in the W12
window should see ZERO diff vs the W11 baseline.

**Decision:** the new `variable "regional_endpoints"`
defaults to an empty list. The W12 resources have
`for_each = { for r in var.regional_endpoints : r.region =>
r }` which produces an empty map when the var is empty —
no resources planned, no apex-record change. The W7 single-
region apex `aws_route53_record.apex` keeps `count = 1` via
the `!local.use_latency_apex` guard (empty list → flag is
false → count is 1).

**Cutover sequence:** when regional EKS clusters are stood
up, operators populate `regional_endpoints` in
`terraform.tfvars` and re-plan; the diff should show ONLY the
W12 resources being CREATED + the W7 single-region apex
being DESTROYED. Captured in
`docs/edge-region-probes.md §3.2` (cutover from "same root
URL" to region-anchored hostnames) +
`docs/prod-cutover.md §5.3` (rollback path).

**Convention:** all additive infra surface that depends on
out-of-lane work (e.g. another agent's cluster lifecycle)
should ship with an EMPTY default so the dependency is
opt-in. The W7 → W12 transition is the reference; future
multi-region work (e.g. multi-region RDS replication,
multi-region S3 replication) should mirror the pattern.

### D6 — Argo Rollouts NetworkPolicy split into three (vs one mega-policy)

**Why:** the W11 auth-aware ingress closed the IDENTITY
loop (oauth2-proxy + dex OIDC chain). The NETWORK loop is
still open — any pod in any namespace can reach the argo-
rollouts dashboard or controller via the CNI. W12 closes the
network loop with a NetworkPolicy set in the `argo-rollouts`
namespace.

**Decision:** three policies, not one.

* `argo-rollouts-dashboard-ingress` — ingress allow-list
  (`ingress-nginx` ns + `auth` ns).
* `argo-rollouts-controller-egress` — egress allow-list
  (kube-apiserver + `monitoring` ns + kube-dns).
* `argo-rollouts-dashboard-egress` — egress allow-list
  (kube-apiserver + kube-dns).

The controller + dashboard have DISTINCT egress profiles:
the controller scrapes Prometheus for analysis-template
metric queries; the dashboard does NOT. A mega-policy would
have to allow Prometheus egress for both pods (wider than
necessary for the dashboard) or list the controller's egress
twice (DRY violation).

**Convention:** prefer multiple narrow NetworkPolicies over
one wide policy. Reviewers can audit each policy's allow-
list independently; chart upgrades that add a new
workload in the namespace become explicit (the new workload
won't have a policy until one is added).

### D7 — W12 JWT rotation rehearsal recommendation: GA-ready

**Why:** the W11 first rehearsal recorded 6 min 12 s. The
W12 second rehearsal recorded 3 min 48 s — a 39 % speedup
downstream of Bishop's W12 JWKS-cache pre-warm. Two
successful runs (one before and one after a runtime change)
gives the squad empirical confidence that the workflow is
stable.

**Decision:** the rehearsal harness is GA-ready. Promote
the cadence from "ad-hoc operator-triggered" to "scheduled
monthly via `schedule: cron`" in a follow-up PR (W13 or
beyond — not in the W12 scope to keep the W12 wave
boundary tight).

**Target timing scale for future runs:** < 4 min green, 4–6
min yellow, > 6 min red. Documented in
`docs/jwt-rotation-rehearsal.md §3.3`. The W11 timing
(6 min 12 s) would now be a YELLOW signal — the W12
baseline tightens the budget so the squad catches runtime-
side regressions in Bishop's auth code path.

**Convention:** every rehearsal harness (DR, secret-
rotation, certificate-rotation, dependency-bump) should
land with at least TWO runs documented before promotion to
scheduled cadence. The first run validates the harness; the
second run validates that the harness is REPEATABLE.

---

## Open items (for W13+)

1. **Regional EKS cluster provisioning** (Hicks W12+ lead) —
   blocker on the W12 multi-region EDGE surface going live.
2. **Scheduled JWT rotation rehearsal** (Apone W13 lead) —
   add `schedule: cron` block to the workflow.
3. **ClusterPolicy namespace exclusion** (Apone, small) —
   one-line `fieldSpecs:` exclusion to the W12
   `NamespaceTransformer` so cluster-scoped Kinds don't
   pick up the default namespace.
4. **Load-test reminder workflow** (Hudson W13+ lead) —
   close the cadence-automation gap; the W12 manifest is
   operator-triggered, the cadence rules are narrative.
5. **`optional: false` flip on the Redis envFrom mount**
   (Apone post-cutover) — once the prod cluster is
   steady-state, flip the flag so the runtime requires
   the dedicated secret.

---

## Files touched

**NEW:**
* `infra/terraform/modules/edge/r53-regional-records.tf`
* `infra/load-tests/redis-load-test.yml`
* `infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`
* `infra/k8s/overlays/prod/namespace-transformer.yaml`
* `docs/prod-cutover.md`
* `docs/retro-2026-10.md`
* `Phase_K_W12/Apone/charter.md`
* `Phase_K_W12/Apone/history.md`
* `.squad/decisions/inbox/apone-phase-k-wave-12.md` (this file)

**MODIFIED:**
* `infra/terraform/modules/edge/{variables,outputs}.tf` —
  `regional_endpoints` variable, `regional_*` outputs,
  `apex_fqdn` fall-through.
* `infra/terraform/modules/edge/main.tf` — `apex` count
  guarded by `local.use_latency_apex`.
* `infra/terraform/envs/prod/{variables,main.tf}` —
  `regional_endpoints` plumbed through.
* `infra/k8s/overlays/prod/kustomization.yaml` — namespace
  transformer swap + resources additions + deployment
  patch.
* `infra/k8s/overlays/prod/redis-connection-string-secret.yaml`
  — file header IN-BAND status.
* `infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`
  — file header IN-BAND status.
* `docs/redis-cluster.md` — new §4 load-test methodology +
  renumber §4–§12 → §5–§13 + internal cross-ref updates.
* `docs/argo-rollouts-setup.md` — new §6 NetworkPolicy +
  renumber §6–§9 → §7–§10 + new cross-refs.
* `docs/jwt-rotation-rehearsal.md` — new §3 rehearsal
  history + renumber §3–§8 → §4–§9.
* `docs/edge-region-probes.md` — §3 in-place update with
  W12 R53 record delivery.
* `CHANGELOG.md` — `[0.21.0]` Phase K Wave 12 entry +
  `[Unreleased]` working-branch flip.
* `.squad/agents/apone/history.md` — W12 entry append.

## Gates

* `terraform fmt -recursive -check infra/terraform/`: clean.
* `terraform validate` against `envs/{prod,staging,dr-us-west-2}`
  + `modules/{redis,github-oidc}`: clean. Standalone
  `modules/edge/` validate hits a pre-existing
  `configuration_aliases` quirk (not W12-caused).
* `kustomize build infra/k8s/overlays/{prod,staging}/`: clean.
  Cross-namespace assertion: argo-rollouts ingress +
  NetworkPolicies preserve `namespace: argo-rollouts`; all
  other resources pick up `mahjong-prod`.
* `actionlint .github/workflows/`: clean (W12 didn't change
  workflows).
* `helm lint helm/mahjong/`: clean (W12 didn't change the
  chart).
* Backend xUnit gate **2403/0/0** preserved (Apone lane
  doesn't touch `src/`).
