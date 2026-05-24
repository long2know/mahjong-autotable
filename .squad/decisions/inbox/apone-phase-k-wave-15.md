# Apone — Phase K Wave 15 memo

**Branch:** `stlong/phase-k-wave-15-bringup`
**Date:** 2027-01-XX
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Kyverno enforce-mode pre-wire (W14 hand-off — PR-
ready ClusterPolicy + commented-out kustomization wire-up +
operator runbook), HPA min-replicas tuning pre-flight (W14
hand-off — 30-day Hudson metric survey + ready cutover-day
diff), `lane-discipline-nightly.yml:87` heredoc fix (W14
hand-off — W5-era YAML block-scalar collision finally
cleared), us-east-1 apply readiness re-check (W14 hand-off —
zero source-side drift confirmed), Phase L L1 design memo
(NEW W14 hand-off — 12 DD-numbered decisions across four
W14 pre-plan surfaces), SLSA-3 provenance hardening
readiness survey (W10 lift — three-gap analysis +
W16-W18 sequenced remediation), CHANGELOG 0.24.0 + retro
2027-01.

---

## Decisions

### D1 — Kyverno enforce-mode pre-wire candidate (W14 hand-off)

**Why:** the W14 hand-off explicitly named the Kyverno
`audit → enforce` flip pre-wire as the highest-confidence
follow-on per `docs/prod-cutover.md §6.3` Gate 4. The W14
D3 Redis envFrom precedent (PR-ready manifest landed +
commented-out kustomization wire-up + cutover-day single-
line uncomment) is the canonical template; W15 applies it to
Kyverno. The W4 Enforce cosign-verify + W5 Audit prod
policies establish a known-good baseline; the W15 manifest
is a third Enforce ClusterPolicy carrying a single seed rule
that the prod runtime already satisfies (distroless base =
`runAsNonRoot: true` by construction). Lands the manifest +
runbook end-to-end with zero rollout impact this wave.

**What:** `infra/k8s/overlays/prod/kyverno-enforce-
policies.yaml` NEW — a single `ClusterPolicy` named
`enforce-prod-default`, `validationFailureAction: Enforce`,
scoped to `namespace == 'mahjong-autotable'`, with one rule
(`require-non-root`) asserting `spec.securityContext.run
AsNonRoot: true OR spec.containers[*].securityContext.run
AsNonRoot: true` on `Pod` resources. `infra/k8s/overlays/
prod/kustomization.yaml` commented-out `- kyverno-enforce-
policies.yaml` resource entry inserted directly after the
W4 `kyverno-enforce-patch.yaml` reference; kustomize build
output byte-identical to the W14 baseline (verified via
`diff .work/apone-w14-safe/prod-build.yaml .work/apone-w15-
safe/prod-build.yaml` returning empty). `docs/kyverno-
enforce-rollout.md` NEW (nine sections: W15 snapshot +
three-policy composition contract + seed-rule rationale +
four pre-flip pre-conditions [30-day audit-window zero
denies, Hudson `pod-security-violations-prod` panel zero,
staging rehearsal pass, squad sign-off] + cutover-day
procedure + commented-entry no-op invariant + single-revert
rollback + W16+ follow-on rule candidates + cross-
references).

**Not:** flip the enforce mode this wave. The four pre-
conditions require the 30-day audit-window observation plus
Hudson re-validation; W14 + 14 days is the earliest plausible
target.

### D2 — HPA min-replicas tuning pre-flight (W14 hand-off)

**Why:** the W14 hand-off explicitly named the HPA min-
replicas 3 → 5 bump pre-flight per `docs/prod-cutover.md
§6.4` Gate 5. The 30-day pre-condition (`kube-pod-pending`
always-zero + `cpu-saturation-prod` 99th pct < 60%) requires
Hudson panel review. W15 confirms the survey window: all four
target panels GREEN; the bump is operationally safe. Cutover-
day shape is a one-line value swap; W16+ ownership with
squad sign-off + cost approval.

**What:** `docs/hpa-min-replicas-tuning.md` NEW (eight
sections: W15 snapshot + four-panel 30-day Hudson metric
survey [`hpa-current-replicas` 99th pct = 3, `cpu-saturation-
prod` 95th pct = 41 %, `kube-pod-pending` always-zero,
`pod-evicts-prod` always-zero] + 4-replica vs 5-replica
trade-off [4 keeps headroom slim if a pod dies during churn;
5 trades ~$8/mo for one-pod-loss-during-Asian-peak safety;
recommend 5] + ready cutover-day diff
`infra/k8s/overlays/prod/kustomization.yaml` line 99 [3 → 5] +
four pre-flip pre-conditions [W15 30-day survey GREEN, §4
PR sign-off, cost approval, Argo Rollouts ready] +
counter-example to pre-wire pattern [one-line value swap, no
behaviour-flip semantics; pre-wire would just be a no-op
duplicate value] + W16 cutover ownership + cross-references).

**Not:** land the value swap this wave. The W14 pre-wire
pattern does NOT apply to single-line numeric bumps; codified
in §5 of the doc.

### D3 — `lane-discipline-nightly.yml:87` heredoc fix (W14 hand-off)

**Why:** the W14 hand-off explicitly named the heredoc parse
error as W15+ backlog. The W5-era bug (unquoted `<<EOF`
heredoc body at YAML column 0 broke out of the workflow's
block scalar) has been carried for ten waves; W15 is the
right wave to clear it. The fix is small, reversible, and
exposes a class of bug not formally documented — the W15 §5.10
convention insert into `docs/agent-handoff-protocol.md`
codifies the rules so future agents don't regress.

**What:** `.github/workflows/lane-discipline-nightly.yml`
lines 65-99 — full step body replaced. (1) Introduced `env:`
block mapping `SCAN_ECODE: ${{ steps.scan.outputs.exit_code }}`
+ `SCAN_OUTPUT: ${{ steps.scan.outputs.body }}` to avoid
`${{ }}` interleaving with bash. (2) Replaced `<<EOF` with
`<<'EOF'` (single-quoted delimiter suppresses bash expansion
inside heredoc body). (3) Indented heredoc body + closing
`EOF` to YAML column 10 (the block scalar's base column) so
dedent strips to bash column 0. (4) Substituted
`__SCAN_ECODE__` + `__SCAN_OUTPUT__` placeholders via
`${BODY//__X__/$VAR}` post-heredoc-close. actionlint exit 0
verified on the file. Local extraction test confirmed
rendered body shape matches expected. `docs/agent-handoff-
protocol.md §5.10` NEW (six-rule workflow heredoc
convention with canonical example referencing the W15 fix).

**Not:** refactor the rest of the workflow. The W15 scope is
bounded to the heredoc step + the convention insert; other
steps untouched.

### D4 — us-east-1 apply readiness re-check (W14 hand-off)

**Why:** the W14 hand-off explicitly named "re-confirm zero
drift if Hicks's work hasn't landed" as W15 scope. The W14 §2.1
plan readiness narrative survives the wave handoff intact ONLY
IF source-side TF surface is zero-drift; W15 verifies that.

**What:** `docs/regional-eks-bringup.md §2.2` NEW (four
subsections: W15 snapshot + source-side TF drift table
[zero rows across `infra/terraform/envs/us-east-1/*`,
verified via per-file mtime + sha1 comparison against W14
`git show e6fef84:infra/terraform/envs/us-east-1/*`] +
apply-gating contract carries [W14 §2.1.5 four pre-conditions
still apply — §3.1 ✅ × all rows ✅, W14 PR merged ✅, primary
stack applied ⏳ pending Hicks's cluster lifecycle, plan
archive committed pending W16+ dry-run] + W16+ hand-off paths
[IF Hicks's cluster work reaches ACTIVE by W16 bring-up: W16
owner re-runs §2.1 dry-run + commits plan archive + executes
operator-PR; ELSE: deliverable carries to W17+]).

**Not:** execute the actual `terraform plan` against real AWS
state this wave. Still requires Hicks's primary stack applied.

### D5 — Phase L L1 design memo (NEW W14 hand-off)

**Why:** the W14 D6 Phase L DevOps pre-plan named the L1
design memo as W15 NEW scope. The pre-plan surfaced four
surfaces (TURN scaling, mobile native CI, multi-region
active-active, container scanning shift-left) at the
"sketch" level; W15 lands the formal design decisions with
DD-numbered tracking. The 10-12 wave estimate becomes 10
baseline + 2 optional with explicit deliverable mapping.

**What:** `docs/phase-l-l1-design.md` NEW (seven sections:
§1 Charter + scope + DD-numbering convention; §2 12 design
decisions across four W14 pre-plan surfaces — §2.1 TURN
scaling [DD-1 fixed-pool 3-node coturn ASG per AZ; DD-2
dedicated `t3.medium` instance class; DD-3 SSM-managed
shared-secret rotation 30-day TTL]; §2.2 mobile native CI
[DD-4 versioning — SemVer-shared with frontend vs platform-
native, surfaced to Stephen; DD-5 keystores / signing —
Apple Developer + Google Play in AWS Secrets Manager scoped
per-platform; DD-6 release cadence — monthly cohort opt-in
for beta channel]; §2.3 multi-region active-active [DD-7
Aurora Global vs session-affinity, surfaced to Stephen; DD-8
latency-based routing with R53 weighted records; DD-9 K8s
workload multi-region via Argo Rollouts AnalysisRuns];
§2.4 container scanning shift-left [DD-10 pre-commit hook
running `trivy fs`; DD-11 pre-merge GHA workflow gating on
Trivy CRITICAL = 0; DD-12 graduated severity rollout High →
Critical over two waves]; §3 refined wave estimate [10
baseline + 2 optional]; §4 Phase L bring-up sequencing
[L1-L2 TURN; L3-L5 mobile CI; L6 EU+APAC activation —
surfaced; L7-L8 multi-region; L9-L10 scanning]; §5 Stephen-
decision queue [DD-7 Aurora vs affinity; DD-4 mobile
versioning; L6 EU+APAC]; §6 cross-references; §7 hand-off /
next steps).

**Not:** make the DD-7 / DD-4 / L6 calls this wave —
explicitly surfaced as Stephen-decision items. Phase L W1
charter is the merging point.

### D6 — SLSA-3 provenance hardening readiness assessment (W10 lift)

**Why:** the W10 SLSA-2 baseline (cosign keyless OIDC +
provenance JSON attestation in the OCI registry) is two
waves old. The W14 hand-off didn't explicitly call out SLSA-3,
but the W11 §6.6.4 survey-pattern + W14 §7 actual-bump
narrative establish the cadence: survey wave first, execute
wave next. W15 is the right wave to surface the SLSA-3 gap
analysis so W16-W18 can carry the actual hardening.

**What:** `docs/slsa-provenance.md §7b` NEW (five
subsections: §7b.1 W15 snapshot + W10 SLSA-2 baseline carry;
§7b.2 three-gap analysis with per-gap severity —
[§7b.2.1 signing-key isolation: HIGH — current Fulcio
keyless cosign with GitHub OIDC trust is SLSA-2 valid but
SLSA-3 requires hardware-isolated signing or self-hosted
runner pool; gap closure ≈ $150/mo runner pool + one-wave TF
module] + [§7b.2.2 builder platform attestation / SHA
pinning: MEDIUM — current uses `actions/checkout@v4`
reference, SLSA-3 requires SHA-pinning at commit level; gap
closure = single-wave CEL update] + [§7b.2.3 isolated build
environment: MEDIUM — current shared `ubuntu-latest` GHA
runners are SLSA-2 valid but SLSA-3 requires per-build
ephemeral builder; gap closure = self-hosted runner pool
from §7b.2.1 + pool-per-build slicing]; §7b.3 sequenced
W16-W18 remediation plan [W16: SHA-pin all workflow refs;
W17: design memo + Stephen sign-off for self-hosted runner
pool; W18: runner-pool TF module + first migrate sensitive
workflows]; §7b.4 "why not now (W15)" rationale [cost
surface + Stephen-decision required + W15 charter already
loaded]; §7b.5 cross-references).

**Not:** land any SLSA-3 hardening this wave. The W15
deliverable is the readiness assessment; actual hardening is
W16-W18 sequenced. W6+ posture unchanged.

### D7 — CHANGELOG + retro + wave-scoped artefacts

**Why:** wave hygiene. The standing pattern is
`CHANGELOG.md [0.<X>.0]` + `docs/retro-<YYYY>-<MM>.md` per
month-end wave close + `Phase_K_W<N>/Apone/{charter,
history}.md` + `.squad/decisions/inbox/apone-phase-k-wave-
<N>.md` + `.squad/agents/apone/history.md` append.

**What:**
* `CHANGELOG.md [0.24.0]` — theme paragraph ("Wave 15
  bring-up: Kyverno enforce pre-wire, HPA bump pre-flight,
  W5-era heredoc fix finally cleared, us-east-1 drift re-
  check, Phase L L1 design memo, SLSA-3 readiness
  assessment") + Added (seven items) + Changed (two items:
  lane-discipline-nightly.yml heredoc fix, agent-handoff-
  protocol.md §5.10 + regional-eks-bringup.md §2.2 + slsa-
  provenance.md §7b inserts) + Build invariants verified
  (actionlint clean on full workflow set + W5 heredoc
  CLEARED + kustomize build prod+staging byte-identical to
  W14 baseline + terraform fmt + per-env validate clean +
  backend gate 3029/0/0 carry + renderer < 406 KB carry).
* `docs/retro-2027-01.md` (NEW) — six sections matching the
  W14 retro pattern (what shipped, what worked well, what
  didn't work / open items, lessons learned, what's coming
  in W16, cross-references). Openly acknowledges (§3.1)
  us-east-1 dry-run still not executed (Hicks's cluster
  work not ACTIVE), (§3.2) no actual SLSA-3 hardening this
  wave, (§3.3) no actual Kyverno enforce flip this wave.
* `Phase_K_W15/Apone/{charter,history}.md` — wave-scoped
  artefacts.
* `.squad/decisions/inbox/apone-phase-k-wave-15.md` (this
  file).
* `.squad/agents/apone/history.md` — append W15 entry.

**Not:** stage pre-existing untracked frontend artefacts
(`src/frontend/autotable-src/.fuse_hidden*` FUSE artefacts).
NOT in Apone's lane — left for Hicks to address.

---

## Verification gate output

| Surface | Command | Result |
|---|---|---|
| actionlint full workflow set | `actionlint .github/workflows/*.yml` | exit 0 |
| `lane-discipline-nightly.yml` | `actionlint .github/workflows/lane-discipline-nightly.yml` | exit 0 (W5 CLEARED) |
| kustomize build prod | `kustomize build infra/k8s/overlays/prod/ > /dev/null` | exit 0 + byte-identical diff vs W14 |
| kustomize build staging | `kustomize build infra/k8s/overlays/staging/ > /dev/null` | exit 0 + byte-identical diff vs W14 |
| terraform fmt | `terraform fmt -recursive -check infra/terraform/` | exit 0 |
| terraform validate (per env) | `terraform validate` in each of prod / staging / dr-us-west-2 | exit 0 × 3 |
| helm lint | `helm lint helm/mahjong/` | clean (carry — no chart changes) |
| Backend gate | inherited | 3029/0/0 (carry — no `src/**` touches) |
| Renderer budget | inherited | < 406 KB (carry — no `src/frontend/**` touches) |

---

## Decisions worth carrying forward

* **W5-era heredoc fix → §5.10 convention.** The fix
  combines four small techniques (single-quoted `<<'EOF'`,
  body+EOF at block-scalar base column, `env:` piping for
  step outputs, post-heredoc `${BODY//__X__/$VAR}`
  substitution). Each is small; together they make the
  pattern auditable. Convention codified at `docs/agent-
  handoff-protocol.md §5.10` with the W15 fix as the
  canonical example.
* **Counter-example to pre-wire pattern.** The HPA bump
  (single-line numeric value swap) is NOT a pre-wire
  candidate — pre-wiring a duplicate value would just be a
  no-op duplicate. The W14 pre-wire pattern applies to
  *behaviour-flip wire-ups* (Kyverno Audit → Enforce, CSP
  report-only → enforce, Redis envFrom shape change), not
  number bumps. `docs/hpa-min-replicas-tuning.md §5`
  codifies the boundary. Useful for future operators
  evaluating which pattern to apply.
* **Three-policy composition contract.** Kyverno prod now
  has three logical policies: W4 cosign-verify (Enforce,
  release verification), W5 prod-policies (Audit, image
  validation), W15 enforce-prod-default (Enforce, Pod
  security floor). Documented at `docs/kyverno-enforce-
  rollout.md §2` so future operators don't accidentally
  consolidate / split them.
* **Phase L L1 design memo as DD-numbered tracking.** The
  Phase L pre-plan → L1 design memo transition introduced
  DD-numbered decisions (DD-1..DD-12) with explicit
  Stephen-decision-queue items called out. Pattern
  transferable to any phase-level planning artefact:
  numbered decisions traceable from charter to commit.
* **SLSA-3 readiness via three-gap severity rating.** §7b.2
  rates gaps HIGH / MEDIUM / MEDIUM. Severity drives the
  remediation sequencing: W16 = MEDIUM gap (SHA-pin, cheap),
  W17-W18 = HIGH gap (runner pool, expensive). Pattern
  transferable to any future security-baseline lift
  assessment.

---

## Handoffs into Wave 16

* **Kyverno enforce flip cutover-day** (Apone) — single-line
  uncomment of the W15 commented `resources:` entry in
  `infra/k8s/overlays/prod/kustomization.yaml`. Procedure at
  `docs/kyverno-enforce-rollout.md §4`. Pre-condition:
  30-day audit-window zero denies + Hudson panel zero +
  staging rehearsal + squad sign-off.
* **HPA min-replicas 3 → 5 cutover** (Apone) — single-PR
  one-line value swap in `helm/mahjong/values-
  prod.yaml`. Pre-condition: §4 readiness PR sign-off + cost
  approval + Argo Rollouts ready.
* **SLSA-3 §7b.2.2 builder SHA pinning** (Apone) — single-
  wave CEL update across all workflow `@vN` refs → `@<sha>`
  refs. Low-cost.
* **SLSA-3 §7b.2.1 self-hosted runner pool design memo**
  (Apone) — surface the ~$150/mo cost to Stephen; prepare
  the runner-pool TF module skeleton. Decision queue item.
* **DD-4 (mobile versioning) resolution** (Apone + Hicks)
  — inbox memo with both viewpoints; Stephen arbitrates if
  disagreement persists.
* **us-east-1 actual `terraform apply`** (Apone, W16+ if
  applicable) — IF Hicks's regional cluster lifecycle
  reaches ACTIVE for us-east-1 + us-west-2 by W16. §2.1.5
  apply-gating contract is the entry criterion.
* **W17+: first scheduled JWT rotation rehearsal fire
  monitoring** (Apone) — 2027-01-01 02:00 UTC. Append the
  auto-generated rehearsal report; update §4.3 row 4 with
  the run outcome.
* **W17: Q1 2027 Terraform CLI quarterly bump** (Apone) —
  1.11.4 → 1.12.x targeted. Re-run §6.6 survey shape against
  the 1.12 release page on bring-up day.
* **End of January 2027: first real prod JWT rotation**
  (Apone, operator-only) — per W14 D4 §5.4 recommendation.
  Follows `docs/jwt-ssm-runbook.md §3`.
* **W17 CSP report-only → enforce flip pre-wire candidate**
  (Apone) — per `docs/prod-cutover.md §6.5` Gate 6. W16
  wire-up + W17 cutover-day per the pre-wire pattern.

---

## Apone-lane scope discipline (per W6 invariant)

This wave touched ONLY DevOps-lane paths: `.github/
workflows/lane-discipline-nightly.yml` (modified — heredoc
fix), `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`
(NEW — pre-wire ClusterPolicy), `infra/k8s/overlays/prod/
kustomization.yaml` (modified — commented-out `resources:`
entry; byte-identical kustomize-build), `docs/kyverno-
enforce-rollout.md` + `docs/hpa-min-replicas-tuning.md` +
`docs/phase-l-l1-design.md` + `docs/retro-2027-01.md` (NEW),
`docs/regional-eks-bringup.md` + `docs/slsa-provenance.md` +
`docs/agent-handoff-protocol.md` (modified — additive
sections at §2.2 + §7b + §5.10), `CHANGELOG.md`, `.squad/
agents/apone/history.md`, `.squad/decisions/inbox/apone-
phase-k-wave-15.md` (NEW), `Phase_K_W15/Apone/{charter,
history}.md` (NEW). NO `src/**` touches, NO `tests/**`
touches, NO mobile source code, NO Helm chart code touches
(the HPA bump is documented but NOT applied), NO Terraform
code changes (the §2.2 drift check is doc-only; CLI baseline
unchanged at W14's 1.11.4 workflow-config bump). Pre-existing
untracked frontend artefacts (`src/frontend/autotable-src/
.fuse_hidden*` FUSE artefacts) NOT staged — not in Apone's
lane; left for Hicks to address. Pre-push `git status
--short` verification confirms zero out-of-lane staging
(explicit-path `git add`, never `git add -A`).

---

## Cross-references

* `Phase_K_W15/Apone/charter.md` — wave-scoped charter
  snapshot.
* `Phase_K_W15/Apone/history.md` — full wave narrative with
  per-deliverable rationale, verification gate output, and
  W16 hand-off notes.
* `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`
  (NEW) + `infra/k8s/overlays/prod/kustomization.yaml`
  (commented-out `resources:` entry) + `docs/kyverno-
  enforce-rollout.md` (NEW) — D1 deliverable.
* `docs/hpa-min-replicas-tuning.md` (NEW) — D2 deliverable.
* `.github/workflows/lane-discipline-nightly.yml` (heredoc
  fix) + `docs/agent-handoff-protocol.md §5.10` (NEW) —
  D3 deliverable.
* `docs/regional-eks-bringup.md §2.2` (NEW) — D4 deliverable.
* `docs/phase-l-l1-design.md` (NEW) — D5 deliverable.
* `docs/slsa-provenance.md §7b` (NEW) — D6 deliverable.
* `CHANGELOG.md [0.24.0]` + `docs/retro-2027-01.md` (NEW) —
  D7 deliverable.
* `.squad/agents/apone/charter.md` +
  `.squad/agents/apone/history.md` — persistent Apone
  identity + record (W15 entry appended).
