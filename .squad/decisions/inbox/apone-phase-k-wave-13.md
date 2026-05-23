# Apone — Phase K Wave 13 memo

**Branch:** `stlong/phase-k-wave-13-bringup`
**Date:** 2026-11-XX
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Regional EKS cluster bring-up readiness docs, JWT
rotation rehearsal quarterly cadence (scheduled workflow +
doc §4), ClusterPolicy namespace exclusion via PatchTransformer
enumeration, Redis load-test monthly reminder workflow,
PR-ready Redis envFrom `optional: false` flip patch +
prod-cutover §6 hardening narrative, Terraform CLI W14 bump
survey (terraform.md §6.6), CHANGELOG 0.22.0 + retro
2026-11.

---

## Decisions

### D1 — Regional EKS cluster bring-up readiness docs

**Why:** the W12 retro D5 carried forward a "regional
clusters live" line-item. The W12 multi-region EDGE surface
(R53 per-region records, latency-based + health-checked
routing, regional ALB DNS published via the W12 edge module)
is half the path; the regional EKS clusters being ACTIVE +
running the prod overlay is the other half. W13 owns the
docs side of this; the actual cluster apply lands in W14+
once Hicks's regional cluster work reaches ACTIVE state.

**What:** `docs/regional-eks-bringup.md` (NEW). Per-region
Cutover-Ready checklists for us-east-1 (primary apex),
us-west-2 (secondary), eu-west-1 (trans-atlantic),
ap-southeast-1 (SEA / DR-cold). Each region's checklist is
seven gates (TF state bucket per region, EKS cluster ACTIVE
verified via `aws eks describe-cluster`, ACM cert per
region, R53 health-check association, ESO target per region,
ALB DNS published, probe sweep clean). Cross-region
invariants (DR data-replication direction, single-Redis
baseline for W13, JWKS region-agnostic, image-SHA
consistency, health-check IP allow-list). Apply order: W14
us-east-1 first → us-west-2; W15 eu-west-1; W15+
ap-southeast-1. Failure-recovery + W14+ hand-offs + cross-
references close out.

**Not:** apply per-region terraform `regional_endpoints =
[...]` configuration in the prod env stack this wave. That
is W14+ work; the W13 deliverable is the readiness
checklist.

### D2 — JWT rotation rehearsal quarterly cadence

**Why:** the W11 deliverable shipped the manual rehearsal
workflow + operator runbook. The W12 deliverable added the
JWKS-cache pre-warm. The W12 retro D1 ("GA-readiness rec")
flagged a scheduled cadence as the next step. The squad-
wide ops rhythm is quarterly for non-incident-driven
rehearsals; the cron `0 2 1 */3 *` (02:00 UTC on the 1st
of every 3rd month — Jan, Apr, Jul, Oct) matches the W12
retro recommendation and the squad-wide quarterly DR-
rehearsal cadence in `docs/dr-rehearsal.md`.

**What:**
`.github/workflows/jwt-rotation-rehearsal-scheduled.yml`
(NEW). Thin scheduler: `schedule:` block + `workflow_dispatch`
back-stop. Uses `actions/github-script@v7`'s
`createWorkflowDispatch` to dispatch the existing W11
`jwt-rotation-rehearsal.yml` with `target_env=staging` forced
in the dispatched payload. The inner workflow is UNCHANGED
— the W11 hard-gate inside the inner workflow remains the
second-line defence. `docs/jwt-rotation-rehearsal.md`
extended with NEW §4 "Quarterly cadence" (scheduler workflow,
report operator-review path, run-table including the W13
scheduler-activation row + pending Q1–Q4 2027 placeholder
rows, off-cadence trigger rules). Renumbering applied to
§4→§10; cross-refs updated.

**Not:** wire the scheduler against a `prod` target. The
rehearsal contract is staging-only by W11/W12 design; the
scheduler honours that.

### D3 — ClusterPolicy namespace exclusion via PatchTransformer
enumeration

**Why:** the W12 retro D7 ("kustomize ClusterPolicy quirk")
documented that the W12 NamespaceTransformer stamped
`metadata.namespace: mahjong-prod` onto cluster-scoped
resources (ClusterPolicy, ClusterRoleBinding, etc.), which
the API server tolerates with a warning but which is
semantically incorrect and trips downstream policy-as-code
diff tools. The W12 deliverable scope did not include the
fix; W13 closes the item.

**What:** initial attempt was to add Kind-filtered
`fieldSpecs` to the NamespaceTransformer
(`fieldSpecs: [{kind: Deployment, path: metadata/namespace,
create: true}, {kind: Service, ...}, ...]` enumerating only
namespaced Kinds). Empirical reproduction against kustomize
v5.4.3 showed the `kind:` filter is IGNORED — even with
`kind: Deployment` as the only entry, ALL Kinds still get
the namespace. Decision flipped to the inverse design: a
SECOND transformer (`infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml`
NEW) holding eight `PatchTransformer` documents — one per
cluster-scoped Kind — that REMOVE `/metadata/namespace`
AFTER the NamespaceTransformer has run. Wired into
`infra/k8s/overlays/prod/kustomization.yaml`'s `transformers:`
list at the position immediately AFTER the
`namespace-transformer.yaml` reference (order matters; the
`op: remove` must run AFTER the stamp or it fails with
"missing value"). Full rationale, empirical reproduction,
verification diff, and future-proofing notes in the new
`docs/cluster-policy-namespace-exclusion.md`.

**Not:** change the inline `NamespaceTransformer` in
`namespace-transformer.yaml` (the W12 design is preserved;
the W13 fix is the downstream stripper). NOT pin the
kustomize binary to a newer version (the upstream issue
tracker shows the v5.4.3 behaviour as a known bug with no
fix in the v5.x series; the workaround is the canonical
solution until v6).

### D4 — Load-test monthly reminder workflow

**Why:** the W12 retro D5 hand-off list ("monthly Redis load-
test reminder workflow") was originally tagged Hudson-lane.
Hudson is absent in W13; DevOps owns the squad-wide ops
cadence cross-surfaces, so W13 picks up this deliverable.
The intent is operator coordination — the load-test impacts
the prod Redis cluster (cluster-scoped IOPS pressure for ~5
min); a reminder issue forces a deliberate Hudson-burn-
rate-window pairing rather than a calendar surprise.

**What:**
`.github/workflows/redis-load-test-reminder.yml` (NEW). Two
jobs: `open-reminder` (cron `0 14 1 * *` — 14:00 UTC on
the 1st of every month + `workflow_dispatch`) opens an
issue titled `Monthly Redis load-test reminder — YYYY-MM`
carrying the W12 SLO baseline (1000 RPS, p99 lookup < 5
ms, p99 write < 8 ms, error rate < 0.1 %), step-by-step
apply commands, stale-close convention, cross-references;
idempotent against same-month re-fires. `stale-close`
paginates open issues with the workflow's label set
(`ops,redis,load-test,reminder`); comments + closes any >
7 days old with `state_reason=not_planned`. New §4.6 in
`docs/redis-cluster.md` (sub-section under §4 "Load-test
methodology") covers cadence table, operator
responsibilities, and the "why a reminder not an auto-
applier" rationale (prod-impact blast-radius, Hudson burn-
rate coordination, audit-trail preference for issue
comments over workflow logs).

**Not:** auto-apply the load-test job. The blast-radius
rationale is documented in §4.6.3; the auto-applier
remains explicitly out of scope.

### D5 — `optional: false` envFrom flip post-cutover prep

**Why:** the W12 prod kustomization wired the Redis
connection-string secret with `optional: true` to preserve
cutover-safe fall-through (containers start WITHOUT the env
var if ESO hasn't yet hydrated the secret; runtime falls back
to the in-process omnibus secret). Post-cutover, the
fall-through has zero operational value and weakens the
contract — an ESO regression silently demotes the Redis
mode to the in-process fallback instead of crash-looping the
pod (the latter is the correct signal). The W13 prep
deliverable is the patch artefact, not the apply.

**What:** `infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml`
(NEW). JSON6902 patch — `op: replace` on
`/spec/template/spec/containers/0/envFrom/4/secretRef/optional`
with value `false`. Index 4 verified empirically against
the current built deployment (envFrom = [configMapRef,
mahjong-autotable, mahjong-jwt-keys, mahjong-jwt-rsa-keys,
mahjong-redis-prod]); file header documents the index
mapping so the W14 operator can audit before applying.
File NOT wired into `kustomization.yaml` — the file is a
PR-ready artefact, not an applied patch this wave. Apply
gating is documented in `docs/prod-cutover.md §6.2` (four
pre-conditions: (a) prod cutover has been steady-state ≥
7 days; (b) ESO secret rotation has succeeded ≥ 2x; (c) no
open Sev-1/Sev-2 referencing Redis in the past 7 days; (d)
Hudson on-call window confirmed for the apply hour). Full
post-cutover hardening narrative as NEW §6 of
`docs/prod-cutover.md` (seven sub-sections: tightening
calendar table with six gates W14–W16, four detailed gate
sections, per-gate rollback, per-gate observability).

**Not:** apply the patch this wave. The W14 owner picks it
up only when the four pre-conditions hold.

### D6 — Terraform CLI W14 bump survey

**Why:** the W11 `docs/terraform.md §6` version policy
pinned the CLI to 1.10.5 with a quarterly cadence rule. W14
is the next quarterly window. Surveying the candidate W14
bump now lets the W14 operator land the bump-PR with the
risk-assessment already in hand.

**What:** new `docs/terraform.md §6.6` "Version bump
planning — W14 (1.10.5 → 1.11.x)". Five sub-sub-sections:
candidate baselines + HashiCorp release-page tracking
inputs, pre-emptive migration risks table covering seven
risk classes (required_version floor, provider compat, HCL
syntax, plan-output diffing, lock-file behaviour, DR
rehearsal workflow pin, moved-blocks + the new `removed`
block in 1.11), recommended W14 target pin `1.11.4`
provisional, bump-PR shape, bump-PR rollback. No actual
bump this wave — the §6.2 quarterly cadence pins the bump
to W14.

**Not:** change the actual pinned version this wave. The
existing `.tool-versions`, CI workflow `terraform-version:
1.10.5` pin, and `required_version` constraints in all five
modules + env stacks remain UNCHANGED.

### D7 — CHANGELOG 0.22.0 + retro 2026-11

**Why:** standard wave-close paperwork. The W12 retro
2026-10 captured the W12 deliverables + retro D-items; W13
retro continues the monthly cadence. The CHANGELOG
`[0.22.0]` entry rolls up the W13 deliverables for
downstream consumers.

**What:**
* `CHANGELOG.md` — `[0.22.0]` Phase K Wave 13 entry above
  `[0.21.0]`; `[Unreleased]` working branch flipped to
  `stlong/phase-k-wave-13-bringup`. Theme paragraph +
  "Added" + "Changed" subsections covering the seven W13
  deliverables.
* `docs/retro-2026-11.md` (NEW) — six sections matching
  the W12 retro pattern (what shipped, what worked well,
  what didn't work / open items, lessons learned, what's
  coming in W14, cross-references). Tags the kustomize
  fieldSpec `kind:` filter ignored behaviour as the
  learnt-the-hard-way moment of the wave.
* `Phase_K_W13/Apone/{charter,history}.md` — wave-scoped
  artefacts.
* `.squad/decisions/inbox/apone-phase-k-wave-13.md` (this
  file).
* `.squad/agents/apone/history.md` — append W13 entry.

**Not:** rebuild the `[Unreleased]` section across the
whole CHANGELOG file. W13 follows the W11/W12 pattern of
flipping the `[Unreleased]` branch line + inserting the
new versioned entry above the prior one.

---

## Cross-references

* `Phase_K_W13/Apone/charter.md` — wave-scoped charter
  snapshot.
* `Phase_K_W13/Apone/history.md` — full wave narrative with
  per-deliverable rationale, verification gate output, and
  W14 hand-off notes.
* `docs/regional-eks-bringup.md` — D1 deliverable.
* `.github/workflows/jwt-rotation-rehearsal-scheduled.yml`
  + `docs/jwt-rotation-rehearsal.md §4` — D2 deliverable.
* `infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml`
  + `infra/k8s/overlays/prod/kustomization.yaml`
  (`transformers:` block extension) +
  `docs/cluster-policy-namespace-exclusion.md` — D3
  deliverable.
* `.github/workflows/redis-load-test-reminder.yml` +
  `docs/redis-cluster.md §4.6` — D4 deliverable.
* `infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml`
  + `docs/prod-cutover.md §6` — D5 deliverable.
* `docs/terraform.md §6.6` — D6 deliverable.
* `CHANGELOG.md [0.22.0]` + `docs/retro-2026-11.md` — D7
  deliverable.
* `.squad/agents/apone/charter.md` + `.squad/agents/apone/history.md`
  — persistent Apone identity + record (W13 entry
  appended).
