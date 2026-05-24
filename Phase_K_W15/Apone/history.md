# Apone — Phase K Wave 15 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/apone/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 15 — DevOps bring-up

Branch: `stlong/phase-k-wave-15-bringup`
Bring-up-on commit (W14 close): `e6fef84` (PR #60 — gate
3029/0/0).

### Deliverables (seven)

1. **Kyverno enforce-mode pre-wire candidate.**
   `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`
   NEW carrying a PR-ready Enforce-mode `ClusterPolicy`
   (`enforce-prod-default`) with a single seed rule
   (`require-non-root`) asserting `securityContext.run
   AsNonRoot: true` on prod-scoped Pods. The
   `kyverno-cosign-verify` ClusterPolicy (W4 Enforce) +
   `kyverno-prod-policies` (W5 Audit) are unchanged; the
   W15 manifest is the third leg of the composition
   contract documented at `docs/kyverno-enforce-rollout.md
   §2`. Pre-wired into `infra/k8s/overlays/prod/
   kustomization.yaml` as a COMMENTED-OUT `resources:`
   entry — kustomize-build output is byte-identical to
   the W14 baseline (verified via `diff
   .work/apone-w14-safe/prod-build.yaml
   .work/apone-w15-safe/prod-build.yaml` returning empty).
   Operator runbook `docs/kyverno-enforce-rollout.md` NEW
   (nine sections: W15 snapshot + three-policy composition
   contract + seed-rule rationale + four pre-flip pre-
   conditions [30-day audit-window zero denies, Hudson
   `pod-security-violations-prod` panel zero, staging
   rehearsal pass, squad sign-off] + cutover-day procedure
   [uncomment one line + tag-bump + apply + 24-hour soak] +
   commented-entry no-op invariant + single-revert
   rollback [`git revert`] + W16+ follow-on rule candidates
   [`disallow-privileged`, `require-resource-requests`,
   `forbid-host-namespace`, `enforce-image-digest`] +
   cross-references). Rationale: the W14 hand-off explicitly
   surfaced "pre-wire candidate" as the highest-confidence
   follow-on; the W15 manifest carries the operator
   contract end-to-end with zero rollout impact this wave.

2. **HPA min-replicas tuning pre-flight.**
   `docs/hpa-min-replicas-tuning.md` NEW (eight sections:
   W15 snapshot + four-panel 30-day Hudson metric survey
   [`hpa-current-replicas` 99th pct = 3, `cpu-saturation-
   prod` 95th pct = 41 %, `kube-pod-pending` always-zero,
   `pod-evicts-prod` always-zero] + 4-replica vs 5-replica
   trade-off [4 keeps headroom slim if a pod dies during
   churn; 5 trades ~$8/mo for one-pod-loss-during-Asian-
   peak safety; recommend 5] + ready cutover-day diff
   `infra/k8s/overlays/prod/kustomization.yaml` line 99 [3 → 5]
   + four pre-flip pre-conditions [W15 30-day survey GREEN,
   §4 PR sign-off, cost approval, Argo Rollouts ready] +
   counter-example to pre-wire pattern [one-line value
   swap, no behaviour-flip semantics; pre-wire would just
   be a no-op duplicate value] + W16 cutover ownership +
   cross-references). PR-ready diff DOCUMENTED but NOT
   landed (squad sign-off required); rationale: the W14
   hand-off explicitly named the 3 → 5 bump as W15 pre-
   flight scope, with cutover at W16 after PR review.

3. **lane-discipline-nightly.yml:87 heredoc fix.**
   `.github/workflows/lane-discipline-nightly.yml` lines
   65-99 (full step body). W5-era `<<EOF` heredoc with
   unquoted delimiter + body at YAML column 0 broke out
   of the workflow's block scalar — actionlint flagged
   "did not find expected alphabetic or numeric character".
   Fix: (1) introduced `env:` block mapping `SCAN_ECODE:
   ${{ steps.scan.outputs.exit_code }}` + `SCAN_OUTPUT:
   ${{ steps.scan.outputs.body }}` to avoid `${{ }}`
   interleaving with bash; (2) replaced `<<EOF` with
   `<<'EOF'` (single-quoted delimiter suppresses bash
   expansion inside the heredoc body); (3) indented the
   heredoc body + closing `EOF` to YAML column 10 (the
   block scalar's base column) so dedent strips to bash
   column 0; (4) substituted `__SCAN_ECODE__` +
   `__SCAN_OUTPUT__` placeholders via `${BODY//__X__/
   $VAR}` post-heredoc-close to preserve template-style
   readability while keeping the heredoc body completely
   literal. actionlint exit 0 on the file for the first
   time since W5 (verified via `actionlint .github/
   workflows/lane-discipline-nightly.yml`). Local
   extraction test confirmed the rendered body shape
   matches expected (verified via python `yaml.safe_load`
   → step bash extraction → executed against fake env
   vars → output diffed against expected GitHub PR body).
   `docs/agent-handoff-protocol.md §5.10` NEW (workflow
   heredoc convention with six rules + canonical example
   referencing the W15 fix as the audit-trail anchor +
   "why this matters" rationale tied to the W5-W14
   parser-confusion debt window).

4. **us-east-1 apply readiness re-check.**
   `docs/regional-eks-bringup.md §2.2` NEW (four
   subsections: W15 snapshot + source-side TF drift table
   [zero rows across `infra/terraform/envs/us-east-1/*`,
   verified via per-file mtime + sha1 comparison against
   W14 `git show e6fef84:infra/terraform/envs/us-east-1/
   *`] + apply-gating contract carries [W14 §2.1.5 four
   pre-conditions still apply — §3.1 ✅ × all rows ✅, W14
   PR merged ✅, primary stack applied ⏳ pending Hicks's
   cluster lifecycle, plan archive committed pending W16+
   dry-run] + W16+ hand-off paths [IF Hicks's cluster
   work reaches ACTIVE by W16 bring-up: W16 owner re-runs
   §2.1 dry-run + commits plan archive + executes
   operator-PR; ELSE: deliverable carries to W17+]).
   Rationale: the W14 hand-off explicitly named the
   "re-confirm zero drift if Hicks's work hasn't landed"
   task as W15 scope. Zero source-side drift confirms the
   plan readiness work survives the wave handoff intact.

5. **Phase L L1 design memo.**
   `docs/phase-l-l1-design.md` NEW (seven sections:
   §1 Charter + scope + DD-numbering convention; §2 12
   design decisions across four W14 pre-plan surfaces —
   §2.1 TURN scaling [DD-1 fixed-pool 3-node coturn ASG
   per AZ; DD-2 dedicated `t3.medium` instance class;
   DD-3 SSM-managed shared-secret rotation 30-day TTL];
   §2.2 mobile native CI [DD-4 versioning: SemVer-shared
   with frontend vs platform-native — surfaced to Stephen;
   DD-5 keystores / signing: Apple Developer + Google
   Play in AWS Secrets Manager scoped per-platform; DD-6
   release cadence: monthly cohort opt-in for beta channel];
   §2.3 multi-region active-active [DD-7 Aurora Global vs
   session-affinity — surfaced to Stephen; DD-8 latency-
   based routing with R53 weighted records; DD-9 K8s
   workload multi-region via Argo Rollouts AnalysisRuns];
   §2.4 container scanning shift-left [DD-10 pre-commit
   hook running `trivy fs` against the Dockerfile;
   DD-11 pre-merge GHA workflow gating on Trivy CRITICAL
   = 0; DD-12 graduated severity rollout High → Critical
   over two waves]; §3 refined wave estimate [10 baseline
   + 2 optional = 10-12 total; mapped to deliverables];
   §4 Phase L bring-up sequencing [L1-L2 TURN; L3-L5
   mobile CI; L6 EU+APAC activation — surfaced; L7-L8
   multi-region; L9-L10 scanning]; §5 Stephen-decision
   queue [3 items: DD-7, DD-4, L6]; §6 cross-references;
   §7 hand-off / next steps. Rationale: the W14 hand-off
   explicitly named "Phase L L1 design memo (NEW W15
   scope)" — this delivers the first concrete planning
   artefact for Phase L.

6. **SLSA-3 provenance hardening readiness assessment.**
   `docs/slsa-provenance.md §7b` NEW (five subsections:
   §7b.1 W15 snapshot + W10 SLSA-2 baseline carry; §7b.2
   three-gap analysis with per-gap severity rating —
   [§7b.2.1 signing-key isolation: HIGH — current Fulcio
   keyless cosign with GitHub OIDC trust is SLSA-2 valid
   but SLSA-3 requires hardware-isolated signing or self-
   hosted runner pool; gap closure ≈ $150/mo runner pool +
   one-wave TF module] + [§7b.2.2 builder platform
   attestation / SHA pinning: MEDIUM — current uses
   `actions/checkout@v4` reference, SLSA-3 requires SHA-
   pinning at commit level; gap closure = single-wave
   CEL update across all workflow refs] + [§7b.2.3
   isolated build environment: MEDIUM — current shared
   `ubuntu-latest` GHA runners are SLSA-2 valid but
   SLSA-3 requires per-build ephemeral builder; gap
   closure = self-hosted runner pool from §7b.2.1 +
   pool-per-build slicing]; §7b.3 sequenced W16-W18
   remediation plan [W16: SHA-pin all workflow refs
   [§7b.2.2]; W17: design memo + Stephen sign-off for
   self-hosted runner pool [§7b.2.1, §7b.2.3]; W18:
   runner-pool TF module + first migrate sensitive
   workflows]; §7b.4 "why not now (W15)" rationale [cost
   surface + Stephen-decision required + W15 charter
   already loaded]; §7b.5 cross-references). Rationale:
   the W10 SLSA-2 baseline is two waves old; W15 is the
   right wave to formally re-assess. No actual hardening
   this wave; W6+ posture (cosign + provenance JSON
   attestation in the OCI registry) unchanged.

7. **CHANGELOG + retro + memo + agent history.**
   `CHANGELOG.md [0.24.0]` entry (theme paragraph + Added
   [7 items: kyverno enforce pre-wire, HPA pre-flight,
   heredoc fix, drift check, Phase L L1 design, SLSA-3
   readiness, retro] + Changed [2 items: agent-handoff-
   protocol §5.10, regional-eks-bringup §2.2, slsa-
   provenance §7b] + Build invariants verified). Working
   branch label updated. `docs/retro-2027-01.md` NEW (six
   sections matching W14 retro pattern: month / wave /
   what shipped / what worked / what to carry / hand-off).
   `Phase_K_W15/Apone/{charter,history}.md` written as
   wave-scoped artefacts.
   `.squad/decisions/inbox/apone-phase-k-wave-15.md` NEW
   (D1-D7 decisions + verification gate output + decisions
   worth carrying forward + handoffs into W16 + lane-
   discipline scope discipline + cross-references).
   `.squad/agents/apone/history.md` appended with the W15
   chronological entry below the existing W14 entry.

### Build invariants

| Surface | W14 close | W15 close | Status |
|---|---|---|---|
| actionlint workflow set | exit 0 | exit 0 | ✅ W5 heredoc CLEARED |
| `lane-discipline-nightly.yml` parse | parse error (W5–W14) | exit 0 | ✅ FIXED |
| kustomize-build prod | 1028 lines | 1028 lines | ✅ byte-identical |
| kustomize-build staging | 849 lines | 849 lines | ✅ byte-identical |
| terraform fmt -recursive -check | exit 0 | exit 0 | ✅ |
| terraform validate prod / staging / dr | exit 0 | exit 0 | ✅ |
| helm lint chart | clean | clean | ✅ (carry — no chart changes) |
| Backend gate | 3029 / 0 / 0 | 3029 / 0 / 0 | ✅ (carry — no `src/**` changes) |
| Frontend renderer budget | < 406 KB | < 406 KB | ✅ (carry — no `src/frontend/**` changes) |

### Cross-lane integration

- **Hicks.** No active hand-off this wave. The
  `docs/regional-eks-bringup.md §2.2` drift-check
  deliverable monitors whether Hicks's regional cluster
  lifecycle reaches ACTIVE for us-east-1 + us-west-2; if
  YES by W16 bring-up, the §2.1.5 apply-gating contract
  triggers the operator-PR sequence.
- **Hudson.** No active hand-off this wave. The Kyverno
  enforce pre-flight (`docs/kyverno-enforce-rollout.md
  §3`) + HPA bump pre-flight (`docs/hpa-min-replicas-
  tuning.md §2`) cite Hudson panels as monitoring
  surfaces. If Hudson is still OOO at W16 cutover-day,
  Apone owns the smoke.
- **Bishop.** No interface this wave (the Kyverno W15
  seed rule asserts a Pod-security invariant the W11
  distroless backend already satisfies).
- **Vasquez.** Shared edit on `docs/agent-handoff-
  protocol.md`. Apone owns §5.10 (workflow heredoc
  convention); Vasquez owns §6 (lane-discipline maturity
  narrative). §5.10 inserted before §6 to preserve file
  ordering. Both lanes touch the same file but non-
  overlapping line ranges — per W10 allowlist precedent.

### Lane discipline (files staged)

- `.github/workflows/lane-discipline-nightly.yml`
- `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`
- `infra/k8s/overlays/prod/kustomization.yaml`
- `docs/kyverno-enforce-rollout.md`
- `docs/hpa-min-replicas-tuning.md`
- `docs/phase-l-l1-design.md`
- `docs/regional-eks-bringup.md`
- `docs/slsa-provenance.md`
- `docs/agent-handoff-protocol.md`
- `docs/retro-2027-01.md`
- `CHANGELOG.md`
- `Phase_K_W15/Apone/charter.md`
- `Phase_K_W15/Apone/history.md`
- `.squad/decisions/inbox/apone-phase-k-wave-15.md`
- `.squad/agents/apone/history.md`

NO files staged outside the Apone allowlist. Verified via
`git status --short` pre-stage + `git diff --cached
--name-only` post-stage.

### Pre-W16 hand-off notes

(See `Phase_K_W15/Apone/charter.md` final section for the
canonical hand-off list. Highlights: Kyverno enforce flip
cutover + HPA bump cutover + SLSA-3 §7b.2.2 SHA-pin +
SLSA-3 §7b.2.1 runner pool design memo + DD-4 mobile
versioning resolution + Hicks regional cluster lifecycle
status check + W17+ JWT rotation rehearsal monitoring +
Q1 2027 Terraform CLI quarterly bump + first prod JWT
rotation + CSP report-only → enforce flip pre-wire.)

### Patterns locked in W15

- **Workflow heredoc convention.** `docs/agent-handoff-
  protocol.md §5.10` six rules: (1) single-quoted
  `<<'EOF'` to suppress bash expansion; (2) heredoc body
  + closing `EOF` at YAML block-scalar base column; (3)
  use `env:` to inject step outputs / context, never
  `${{ }}` interpolation inside heredoc body; (4) post-
  heredoc placeholder substitution via `${BODY//__X__/
  $VAR}` for template-style readability; (5) actionlint
  on the file post-edit as the gate; (6) audit-trail
  comment when fixing a W5-era heredoc bug. Canonical
  example: the W15 lane-discipline-nightly.yml fix.
- **Counter-example to pre-wire pattern.** Single-line
  value swaps (e.g., HPA min-replicas 3 → 5) are NOT
  pre-wire candidates — the W14 pattern applies to
  behaviour-flip wire-ups (Kyverno Audit → Enforce, CSP
  report-only → enforce), not number bumps. `docs/hpa-
  min-replicas-tuning.md §5` codifies the boundary.
- **Survey-then-execute cadence reinforced.** Three of
  the seven W15 deliverables are surveys / pre-flights /
  readiness assessments (HPA, drift check, SLSA-3);
  three are net-new design / wire-up artefacts (Kyverno
  pre-wire, Phase L L1, heredoc fix); one is meta
  (CHANGELOG / retro / memo). The cadence preserves the
  W14 "no flips this wave" discipline while landing
  concrete progress on every W14 hand-off item.
