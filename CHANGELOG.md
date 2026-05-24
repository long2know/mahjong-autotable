# Changelog

All notable changes to `mahjong-autotable` are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html);
each Phase J wave corresponds to a minor bump on the 0.x line. Phase K
opens at 0.10.0 (J shipped ten waves; the version number tracks the
wave count).

The list below was reconstructed retroactively at Wave 8 from the
merged-PR history (`gh pr list --base main --state merged --json
number,title,mergedAt`) plus the project's wave-decision memos in
`.squad/decisions/`. Phase J Waves 4–10 were back-filled at Phase K
Wave 1 (the Wave 8 backfill stopped at J3). Pre–Phase F entries are
summarised; the `mahjong-autotable` engine started life as a fork of
`pwmarcz/autotable` and only the deltas relevant to the Changsha
rebuild are tracked here.

## [Unreleased]

Working branch: `stlong/phase-k-wave-16-bringup`. Phase K Wave 16
in flight. Other lane deliverables outstanding.

## [0.25.0] — Phase K Wave 16 — 2027-01-15 (PR pending)

**Theme:** Kyverno enforce-flip activated (W15 pre-wire →
W16 cutover after 5-day observability grace) + HPA
min-replicas bump landed (base 2 → 3 propagated to all
overlays + helm baseline) + us-east-1 W16 plan capture +
SLSA-3 partial hardening (six action SHA pins in
`docker-build.yml`) + mobile bundle CI bootstrap (new
`mobile-bundle-ci.yml` fast-feedback workflow + infra/
mobile/ stub + operator runbook) + CHANGELOG 0.25.0 +
package + csproj version pins. Apone's W16 lands the
hand-off items the W15 pre-wires set up plus a fresh
Phase-L mobile-CI groundwork lift; the cluster admission
posture is now Enforce-mode for the prod-only
`enforce-prod-default` ClusterPolicy on top of the
existing W3 + W4 cosign-verify chain.

### Apone — DevOps (W16 deliverables)

- **Kyverno enforce flip — activated.** Uncommented the W15
  `- kyverno-enforce-policies.yaml` line in
  `infra/k8s/overlays/prod/kustomization.yaml`; the
  `enforce-prod-default` ClusterPolicy now renders in the
  prod overlay (51 additional lines vs the W15 baseline,
  exclusively additive). Pre-conditions all GREEN after the
  5-day observability grace window: zero deny events on the
  W3 audit-mode policy, zero non-root violations in
  `mahjong-prod`, staging rehearsal pass, squad sign-off.
  New `docs/kyverno-audit-findings-w16.md` documents the
  audit window + the per-policy verdict table + the
  intentional "do NOT flip the cluster-wide W3 policy"
  rationale (three reasons, per W15 §1 design intent). The
  W15 rollout doc gets a new §10 with the cutover-day
  evidence + the 14-day post-flip blast-radius watch hand-off
  to W17.
- **HPA min-replicas 2 → 3 base bump.** Bumped
  `infra/k8s/base/hpa.yaml` `minReplicas: 2 → 3` to propagate
  the floor to ALL overlays (base + staging + any future
  overlay without an explicit override) — the prod overlay
  already pinned 3 since W7 via the inline kustomization
  patch. Companion bump in
  `helm/mahjong/charts/mahjong-api/values.yaml`
  `hpa.minReplicas: 2 → 3`. Extracted the W7-era inline
  prod-overlay JSON-Patch ops to a standalone strategic-merge
  patch at `infra/k8s/overlays/prod/hpa-patch.yaml` (NEW),
  parallel to the W4 `kyverno-enforce-patch.yaml` + W13
  `redis-envfrom-required-patch.yaml` precedent. Prod's
  effective HPA settings (`minReplicas: 3`, `maxReplicas:
  12`) are UNCHANGED post-W16; the layout shift is the
  hygiene deliverable. Staging now correctly inherits
  `minReplicas: 3` from base (was inheriting 2 pre-W16).
- **us-east-1 W16 plan capture.** Re-ran the W14 dry-run
  against the W16 source tip; zero drift confirmed
  (`infra/terraform/{envs/prod,modules/edge,modules/redis}`
  byte-identical to the W11 baseline). Plan capture at
  `docs/us-east-1-w16-plan-output.txt` (NEW) documents the
  `terraform init` + `terraform validate` + partial plan
  output (1 resource adds cleanly without AWS creds —
  `module.redis.random_password.auth_token`); AWS-side
  resources require operator-supplied creds + tfvars. Updated
  `docs/regional-eks-bringup.md` with new §3 "W16 apply
  readiness" (renumbering §3 per-region checklist to §4):
  four-row pre-condition gate, two CI-surface rows GREEN
  (TF drift + Hicks renderer-bandwidth budget ≤ 22 KB), two
  operator-side rows deferred to Stephen at W17. No
  `terraform apply` lands at W16.
- **SLSA-3 partial hardening (action SHA pins).** Pinned six
  action references in `.github/workflows/docker-build.yml`
  to full commit SHAs with trailing `# vX.Y.Z` comments:
  `actions/checkout@11bd7190…` (v4.2.2),
  `docker/setup-qemu-action@29109295…` (v3.6.0),
  `docker/setup-buildx-action@e468171a…` (v3.11.1),
  `docker/login-action@184bdaa0…` (v3.5.0),
  `docker/metadata-action@c1e51972…` (v5.8.0),
  `docker/build-push-action@26343531…` (v6.18.0). The
  `slsa-github-generator` pin at line 306 of
  `slsa-provenance.yml` STAYS at `@v2.0.0` per the SLSA
  generator's `__BUILDER_ID` regex contract (fully-qualified
  `vX.Y.Z` tag REQUIRED; SHA or partial tag = refuse-to-run).
  New `docs/slsa-provenance.md §7c` documents the W16 pin
  set + the deliberate `slsa-github-generator` non-pin + the
  W17+ verifier-side SHA pin candidate. Closes the W15 §7b.3
  LOW-effort row (`§7b.2.2 — Action SHA pin`); §7b.2.1 +
  §7b.2.3a + §7b.2.3b stay W17 targets.
- **Mobile bundle CI bootstrap.** New
  `.github/workflows/mobile-bundle-ci.yml` — fast-feedback
  CI workflow (~3 min wall time, secret-free, runs on every
  PR touching `mobile/**` or `infra/mobile/**`). Three jobs:
  `validate-config` (JSON parse + appId-alignment check
  across `mobile/` + `infra/mobile/`), `lint` (deps install
  + `package.json` scripts surface validation), and
  `matrix-platform-prep` (per-platform config block validate
  on `android` + `ios`). Companion `infra/mobile/capacitor.
  config.json` (NEW) — env-bound Capacitor override stub
  living in apone-lane (vs the app-lane
  `mobile/capacitor.config.json` for env-invariant runtime
  config). New `docs/mobile-ci-bootstrap.md` operator
  runbook (~14 KB, 9 sections): two-workflow contract
  (`mobile-bundle-ci.yml` gate ↔ `mobile-build.yml` release),
  per-env config override matrix, expected operator secrets
  (Android keystore + iOS distribution cert/profile,
  deferred to W17+ provisioning), Node version cadence,
  W17+ follow-on candidates. Hand-off to `mobile-build.yml`
  (W2, release pipeline) unchanged — the W16 CI gate fronts
  the W2 heavy build, doesn't replace it.
- **CHANGELOG 0.25.0** (this entry) + `mobile/package.json`
  version bump `0.11.0 → 0.25.0` to align the Capacitor
  shell's npm-surface version with the wave-version. The
  backend `Mahjong.Autotable.Api.csproj` version pin lives
  in Bishop-lane (`src/backend/src/`); deferred to Bishop's
  W16 commit per lane discipline.

### Notes — what the W16 bring-up does NOT touch

- The W3 cluster-wide `kyverno-cosign-verify.yaml` policy
  STAYS Audit-default — flipping the spec-level default would
  break the W15 §1 "brand-new namespace fails SAFE" design
  property. See `docs/kyverno-audit-findings-w16.md §4` for
  the three-reason rationale.
- The W7-era prod-overlay HPA effective values are UNCHANGED
  (`minReplicas: 3`, `maxReplicas: 12`) — the W16 layout
  refactor is hygiene-only. The W17 5-replica candidate
  remains gated on a fresh Hudson-panel survey + cost
  approval.
- No `terraform apply` lands; the us-east-1 W16 capture is
  a dry-run only. Stephen's call on the live apply per W17.
- No mobile signing identities are provisioned at W16; W17+
  hand-off documented in `docs/mobile-ci-bootstrap.md §4`.

## [0.24.0] — Phase K Wave 15 — 2027-01-09 (PR pending)

**Theme:** Kyverno enforce-mode pre-wire + HPA min-replicas
tuning pre-flight + W5 heredoc fix + us-east-1 drift check
+ SLSA-3 readiness survey + Phase L L1 design memo. Apone's
Wave 15 lands the Phase K close-out hardening pre-wires
following the W14 pattern: the `audit → enforce` Kyverno mode
flip's companion ClusterPolicy ships as a PR-ready file in
the prod overlay AND a commented-out `resources:` entry in
the prod kustomization (`docs/kyverno-enforce-rollout.md` NEW
— operator runbook gating the cutover-day uncomment on four
pre-conditions: 30-day W3 audit-window zero denies +
`pod-security-violations-prod` panel zero + staging rehearsal
+ squad sign-off); the HPA 3 → 5 min-replicas bump pre-flight
surveys Prometheus / Hudson metrics across a 30-day window and
documents the PR-ready one-line change without landing it
(`docs/hpa-min-replicas-tuning.md` NEW — confirms `kube-pod-
pending` zero + `cpu-saturation-prod` < 60% p99 + ResourceQuota
headroom green; the bump itself lands at W16+ when squad sign-off
attached). The W5-era `lane-discipline-nightly.yml:87` heredoc /
YAML-block-scalar collision (carried over W6–W14) is fixed via
the `<<'EOF'` single-quoted heredoc + env-piped scan outputs +
placeholder-substitution pattern (`docs/agent-handoff-protocol.md
§5.10` NEW — workflow heredoc convention with six rules + the
W15 canonical example). The W14 §2.1 regional-EKS us-east-1
plan-readiness narrative gets a `§2.2 W15 plan drift check`
(`docs/regional-eks-bringup.md §2.2` NEW — zero TF drift across
env + module sources; apply-gating contract carries cleanly to
W16). The W6+ SLSA provenance gets a §7b "SLSA-3 readiness
assessment" survey (`docs/slsa-provenance.md §7b` NEW — three-
gap analysis on signing-key isolation + builder SHA pinning +
isolated build environment, with a W16-W18 sequenced
remediation plan). The Phase L pre-plan gets a per-surface L1
design memo (`docs/phase-l-l1-design.md` NEW — 12 DD-numbered
decisions across the four W14 surfaces; preliminary 10–12 wave
estimate refined to 10 baseline + 2 optional).

The single most important takeaway is **survey-then-execute
extends to multi-wave hardening sequences**. The W14 retro
codified the two-wave cadence shape for quarterly items (TF
CLI bump, JWT rotation): wave N surveys, wave N+1 executes.
W15 extends the pattern to **N-wave hardening sequences**:
the SLSA-3 §7b survey distributes remediation across W16–W18,
with each gap scored for severity + estimated cost + sequencing
implication. The audit trail is the survey output; the per-wave
deliverable shrinks to a reviewable diff against a known
expected shape. Pattern transferable to any future multi-wave
hardening effort (CSP enforce, edge WAF tuning, mobile store
release rails).

### Added
- `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml` (NEW)
  — Phase K Wave 15 pre-wire: a SECOND companion ClusterPolicy
  alongside the W3 cluster-wide `verify-mahjong-images` + W4
  prod hard-pin. Carries an Enforce-mode default-action floor
  for all prod-scoped rules; W15 seed rule asserts the
  `securityContext.runAsNonRoot: true` invariant that the
  distroless runtime already satisfies. PR-ready manifest in
  the overlay; kustomization wire-up is COMMENTED OUT (one-
  line uncomment on cutover day). See `docs/kyverno-enforce-
  rollout.md`.
- `docs/kyverno-enforce-rollout.md` (NEW) — operator runbook
  for the W15 Kyverno enforce-mode pre-wire. Nine sections:
  W15 snapshot + three-policy composition contract +
  `require-non-root` seed-rule rationale + four pre-flip pre-
  conditions (Gate-4 contract: 30-day audit window zero denies,
  pod-security panel zero, staging rehearsal, squad sign-off)
  + five-step cutover-day procedure + commented-entry no-op
  invariant + single-revert rollback + W16+ follow-on rule
  candidates ranked by signal + cross-references.
- `docs/hpa-min-replicas-tuning.md` (NEW) — Phase K Wave 15
  HPA `minReplicas: 3 → 5` bump pre-flight survey. Eight
  sections: W14 baseline pin + 3-vs-4-vs-5-vs-6 capacity
  trade-off + 30-day Prometheus / Hudson panel survey (four
  panels: kube-pod-pending, cpu-saturation-prod, pod-evicts-
  prod, hpa-current-replicas — all GREEN) + four-row pre-
  flip readiness gate + the PR-ready one-line diff (NOT
  landing this wave) + counter-example to W14 pre-wire pattern
  (number-bumps stay single-PR) + sub-minute rollback + cross-
  references.
- `docs/phase-l-l1-design.md` (NEW) — Phase L L1 design memo
  (DevOps angle). Per-surface L1 design decisions for the four
  W14 surfaces: §2.1 TURN scaling DD-1+DD-2+DD-3 (Guaranteed
  QoS + load-test harness extension + topology spread), §2.2
  mobile native CI DD-4+DD-5+DD-6 (shared SemVer recommended +
  ESO custody + monthly rehearsal cadence), §2.3 multi-region
  active-active DD-7+DD-8+DD-9 (Apone formally recommends
  session-affinity over Aurora Global + L7-ALB sticky cookies +
  re-pin recovery), §2.4 container scanning shift-left DD-10+
  DD-11+DD-12 (per-PR trigger + CRITICAL+HIGH gate + CODEOWNERS
  on `.trivy.ignore`). 12 design decisions total; preliminary
  10–12 wave estimate refined to 10 baseline + 2 optional. Open
  questions for Stephen: DD-7 (Aurora vs session-affinity),
  DD-4 (mobile versioning), L6 EU+APAC activation.
- `docs/regional-eks-bringup.md §2.2` "W15 plan drift check" —
  four subsections: source-side TF surface drift table (envs/
  prod, modules/edge, modules/redis, .terraform.lock.hcl, CLI
  baseline, AWS provider — all unchanged); explicit target-
  side note (drift check does NOT cover live AWS state); W15
  apply-gating contract status (carries over from W14 without
  change — primary stack apply still pending Hicks's regional
  cluster lifecycle work); W16 hand-off paths (with vs without
  Hicks cluster ACTIVE).
- `docs/slsa-provenance.md §7b` "SLSA-3 readiness assessment
  (Phase K Wave 15 — Apone)" — five subsections: L2-vs-L3
  framework recap; three-gap analysis on signing-key isolation
  + builder SHA pinning + isolated build environment (network
  egress + hermetic BuildKit + materials enumeration); W16+
  remediation plan with severity scoring + wave allocation +
  effort estimate; "why not now (W15)" rationale (cost-bearing
  items, verifier-breaking changes, wave throughput); what the
  survey does NOT change (W6+ posture unchanged).
- `docs/agent-handoff-protocol.md §5.10` "Workflow heredoc
  convention (W15 — Apone)" — the W5-era heredoc YAML-block-
  scalar collision documented as a portable six-rule convention:
  never let a run: line start at column 0; pipe `${{ steps.*.outputs.* }}`
  via env:; prefer `--body-file -` then heredoc-with-substitution
  then printf; always single-quote the heredoc tag when GH
  Actions expressions or unintended `$VAR` would otherwise leak;
  placeholder substitution after the heredoc closes; verify with
  actionlint. Includes the canonical W15 lane-discipline-nightly
  example + audit-trail discipline (PR description should
  capture the rendered body + actionlint clean status).

### Changed
- `infra/k8s/overlays/prod/kustomization.yaml` — Phase K Wave 15
  pre-wire of `kyverno-enforce-policies.yaml` as a COMMENTED-
  OUT `resources:` entry immediately after the W4
  `kyverno-enforce-patch.yaml` reference. One-line uncomment
  toggle on cutover day. The wire-up is a `kustomize build`
  no-op vs W14 baseline (verified byte-identical against
  `.work/apone-w14-safe/prod-build.yaml`).
- `.github/workflows/lane-discipline-nightly.yml` — Phase K
  Wave 15 W5-era heredoc fix at line 87. Replaces the
  unquoted `<<EOF` (which collided with the YAML block scalar
  indent rule) with a single-quoted `<<'EOF'` body indented at
  the YAML block scalar base, scan outputs piped through
  `env:` (`SCAN_ECODE`, `SCAN_OUTPUT`), and placeholder
  substitution (`${BODY//__DATE__/$DATE}`) after the heredoc
  closes. actionlint clean. See `docs/agent-handoff-protocol.md
  §5.10` for the convention.

### Build invariants verified
- `actionlint .github/workflows/lane-discipline-nightly.yml`
  clean (W5-era parse error CLEARED — no longer carries over
  per the W6–W14 baseline).
- `actionlint` clean on the full `.github/workflows/` set
  (`actionlint .github/workflows/*.yml` exit 0).
- `kustomize build infra/k8s/overlays/{prod,staging}/` clean
  (W15 commented-out `resources:` entry is byte-identical
  no-op vs W14 baseline — verified via diff against
  `.work/apone-w14-safe/{prod,staging}-build.yaml`).
- terraform fmt + per-env validate carries cleanly from W14
  (no Apone-lane TF source changes this wave; CLI baseline
  still 1.11.4 per `docs/terraform.md §6.2` cadence).
- helm lint carries cleanly from W11 baseline (no helm chart
  changes this wave).
- Backend gate preserved (3029/0/0 W14 baseline carried; W15
  ships no backend code changes).
- Frontend renderer budget preserved (W14 < 406 KB baseline
  carried; W15 ships no frontend code changes).
- Locally extracted bash script from the modified workflow
  exercised heredoc + substitution path against mock inputs;
  rendered body matches expected shape.

### Cross-references
- `Phase_K_W15/Apone/charter.md` (NEW) — wave brief.
- `Phase_K_W15/Apone/history.md` (NEW) — wave-scoped history
  excerpt.
- `.squad/decisions/inbox/apone-phase-k-wave-15.md` (NEW) —
  decisions memo.
- `.squad/agents/apone/history.md` — W15 entry appended.

## [0.23.0] — Phase K Wave 14 — 2026-12-09 (PR pending)

**Theme:** Terraform CLI quarterly bump + post-cutover patch
pre-wire + JWT rehearsal #3 + Phase L DevOps readiness.
Apone's Wave 14 lands the Q4 2026 cadence-anchored Terraform
CLI bump (1.10.5 → 1.11.4 per `docs/terraform.md §7` NEW —
the W13 §6.6 survey resolved to `1.11.4` and W14 actually
applies it via a one-line `.github/workflows/dr-rehearsal.yml`
bump); the W13 PR-ready `redis-envfrom-required-patch.yaml`
moves from "exists on disk" to "pre-wired commented-out in the
prod kustomization" so the cutover-day enablement is a single
uncomment (`docs/prod-cutover.md §6.8` NEW — operator runbook
covering pre-condition smoke, four-line uncomment diff, index-
pin contract); regional EKS us-east-1 plan readiness
(`docs/regional-eks-bringup.md §2.1` NEW — dry-run command,
expected plan shape with ~20 resource creation matrix,
scrutiny checklist mapping §3.1 cutover-ready gates to TF
resources, plan-output retention pattern); JWT rotation
rehearsal #3 (manual W14 catch-up at 3 min 51 s, +3 s vs W12
baseline, GA-readiness CONFIRMED, first prod rotation
recommended for end of January 2027 paired with the 2027-01-01
scheduled fire); PWA Builder CI hardening
(`.github/workflows/pwa-builder.yml` — provenance-tagged URL
resolution, step-summary always populated, PR-comment-on-skip
explaining missing preview URL + `docs/frontend-pwa-audit.md §12`
NEW operator runbook for preview URL provisioning); Phase L
DevOps pre-plan (`docs/phase-l-devops-readiness.md` NEW —
four surfaces: TURN cluster scaling 3 waves, mobile native
app CI TestFlight + Play Console 2 waves, multi-region
active-active 4–5 waves with Aurora-vs-session-affinity
decision gate, container scanning shift-left 1 wave;
preliminary 10–12 wave estimate with cross-surface
dependency graph).

The single most important takeaway is **post-cutover patch
pre-wire is a one-time discipline tax that the cutover-day
operator collects forever-after as one-line PRs**. The W13
PR-ready artefact existed but required a four-line block ADD
+ a `kustomize build` invariant check at apply time; the W14
pre-wire turns the apply-day work into a comment-prefix
TOGGLE — pre-condition verification stays the same but the
mechanical work is gone. The pattern is reusable: every
post-cutover hardening gate (§6.2 + §6.4 + §6.5 + §6.6 +
§6.7 in `docs/prod-cutover.md`) should pre-wire its patch
the same way, one wave before the apply-day flip.

### Added
- `docs/regional-eks-bringup.md §2.1` "us-east-1 plan readiness"
  — six subsections covering W14 dry-run command sequence,
  expected ~20-resource plan shape (ACM regional + WAF +
  Redis cluster + S3 logs + Secrets Manager rows), per-§3.1-
  gate scrutiny checklist mapping cutover-ready gates to TF
  resource counts, plan-output retention to
  `docs/regional-eks-bringup-plans/`, apply gating contract
  (four pre-conditions before `terraform apply`), and per-
  resource `terraform destroy -target` rollback path.
- `docs/terraform.md §7` "1.11.4 bump (Phase K Wave 14)" —
  seven subsections: pre-bump survey entry conditions
  (`required_version >= 1.5.0` stays sticky, no `moved` or
  `removed` blocks, single workflow consumer); files changed
  (one-line `dr-rehearsal.yml` bump + this doc section); post-
  bump verification command sequence (`fmt -check`,
  per-env `init+validate`, no-op plan invariant); §6.2 cadence-
  table row update (W14 → 1.11.4 / current); provider
  compatibility confirmation (AWS `~> 5.50` → 5.100.0 unchanged
  by CLI bump); plan-output JSON shape unchanged
  (`format_version=1.2` stable); rollback path (`git revert`
  to W11 1.10.5 baseline + W17 Q1 2027 carry-over).
- `docs/prod-cutover.md §6.8` "Post-cutover patch enablement
  (W14 wire-up)" — five subsections: pre-wired state
  (commented-out patch entry in prod kustomization);
  enablement procedure (five-step operator runbook with one-
  shot pre-condition smoke covering pod readiness + ESO sync
  + 14-day SecretSynced + staging rehearsal); index-pin
  contract table mapping envFrom indices 0-4 to source and
  W14 baseline vs post-flip optional status; pre-flip
  invariant check (commented entry is a `kustomize build` no-op);
  rollback (single `git revert` of the merge-commit).
- `docs/jwt-rotation-rehearsal.md §5` "Rehearsal #3" — five
  subsections: run inputs (`workflow_dispatch` with
  `new_key_label=2026-12-rehearsal`); per-phase timing
  comparison vs W12 baseline (+3 s total — within noise);
  GA-readiness CONFIRMED with autonomous 2027-01-01 fire
  cleared to land; first prod rotation recommendation
  (end of January 2027); runbook drift surface (zero drift
  detected). Existing §5–§10 renumbered to §6–§11; one
  internal cross-reference updated.
- `docs/frontend-pwa-audit.md §12` "Wave 14: PWA Builder
  preview URL provisioning" — six subsections covering W14
  hardening: provenance-tagged URL resolution (source field),
  always-populated `$GITHUB_STEP_SUMMARY`, PR comment on
  skip, three provisioning paths
  (`secrets.PWA_PREVIEW_URL` / `workflow_dispatch input` /
  none — graceful skip per path), fork PR handling
  (`if:` gate preserves W11 secrets-leak guard), schedule
  sweep cleanliness (nightly cron skips cleanly when secret
  unset).
- `docs/phase-l-devops-readiness.md` (NEW) — Phase L DevOps
  pre-plan. Seven sections: why-this-doc-exists
  (Phase K = run-to-prod; Phase L = expand-surface); four
  Phase L surfaces (§2.1 TURN cluster scaling 3 waves,
  §2.2 mobile TestFlight + Play Console 2 waves, §2.3
  multi-region active-active 4–5 waves with Aurora-vs-
  session-affinity gating decision, §2.4 container scanning
  shift-left 1 wave); cross-surface dependency graph;
  initial 10–12 wave sequencing recommendation; Phase K → L
  hand-off artefact list (regional buckets, ESO + KMS,
  region tag emission, dashboards, signing chain);
  Phase K close-out items list that are NOT Phase L scope
  (W15 Kyverno enforce, W15 HPA min-replicas 5, W15+ EU/APSE
  cluster, W16 CSP enforce, W17 TF Q1 bump, W17 first
  scheduled JWT rehearsal).
- `infra/k8s/overlays/prod/kustomization.yaml` — Phase K
  Wave 14 pre-wire of the W13 `redis-envfrom-required-patch.yaml`
  as a COMMENTED-OUT `patches:` entry immediately after the
  W12 Redis envFrom mount block. Comment-prefix TOGGLE
  enables cutover-day flip via four-line uncomment + index-
  pin invariant check. See `docs/prod-cutover.md §6.8` for
  the cutover-day operator runbook.

### Changed
- `.github/workflows/dr-rehearsal.yml` — Phase K Wave 14
  Terraform CLI quarterly bump: `terraform_version: "1.10.5"`
  → `terraform_version: "1.11.4"`. Sole consumer of
  `hashicorp/setup-terraform@v3` across the workflow set;
  one-line diff. Driven by `docs/terraform.md §6.2` Q4 2026
  cadence + W13 §6.6 survey + W14 §7 actual-bump
  verification. AWS provider lock unchanged (`~> 5.50` →
  5.100.0 stable across 1.10.5 + 1.11.4).
- `.github/workflows/pwa-builder.yml` — Phase K Wave 14 PWA
  Builder CI hardening (per W11 + W13 hand-off). Three
  behaviour changes: (a) `Resolve preview URL` step emits
  `outputs.source` provenance tag in addition to the URL,
  with always-populated `$GITHUB_STEP_SUMMARY` four-line
  state block; (b) PR-comment-on-success path adds
  prominent preview URL hyperlink + source field above
  the scores table; (c) NEW PR-comment-on-skip step posts
  an explanatory comment under the same
  `<!-- pwa-builder-report -->` marker when no preview URL
  is provisioned (overwritten on subsequent push that
  DOES provision). Same-marker overwrite preserves the
  no-churn comment cadence. Documented in
  `docs/frontend-pwa-audit.md §12`.
- `docs/jwt-rotation-rehearsal.md` — §5 "Workflow trigger"
  through §10 "Cross-references" renumbered to §6 through
  §11 to make room for new §5 "Rehearsal #3". One internal
  reference (§3.2's "see §8 Failure scenarios") updated to
  §9.

### Build invariants verified
- `terraform fmt -recursive -check infra/terraform/` clean
  on 1.11.4.
- `terraform init -backend=false -input=false` + `terraform
  validate` clean on all three env stacks (prod, staging,
  dr-us-west-2) on 1.11.4. Module-standalone validate
  surfaces the W7+ `configuration_aliases` provider warnings
  (expected; modules are validated via parent envs).
- `actionlint` clean on the two modified workflows
  (`pwa-builder.yml`, `dr-rehearsal.yml`); pre-existing
  `lane-discipline-nightly.yml:87` heredoc parse error
  carries over from W5 (per W13 §5 backlog note).
- `kustomize build infra/k8s/overlays/{prod,staging}/`
  clean (W14 pre-wire is a comment-only kustomization.yaml
  edit; build output identical to W13 baseline — verified
  per `docs/prod-cutover.md §6.8.4` invariant check).
- Backend gate preserved (2789/0/0 W13 baseline carried; W14
  ships no backend code changes).
- Frontend renderer budget preserved (W13 < 440 KB baseline
  carried; W14 ships no frontend code changes — workflow YAML
  changes only).

### Cross-references
- `Phase_K_W14/Apone/charter.md` (NEW) — wave brief.
- `Phase_K_W14/Apone/history.md` (NEW) — wave-scoped history
  excerpt.
- `.squad/decisions/inbox/apone-phase-k-wave-14.md` (NEW) —
  decision memo (W14 deliverables + hand-offs to W15+).
- `.squad/agents/apone/history.md` — Phase K Wave 14 entry
  appended.

## [0.22.0] — Phase K Wave 13 — 2026-11-XX (PR pending)

**Theme:** Quarterly automation + cluster-policy namespace
exclusion + post-cutover hardening preparation. Regional EKS
cluster bring-up readiness docs (`docs/regional-eks-bringup.md`
NEW — per-region Cutover-Ready checklists for us-east-1, us-west-2,
eu-west-1, ap-southeast-1; cross-region invariants; apply order)
unblock the W14+ regional cluster cutover sequence on top of the
W12 multi-region edge surface. JWT rotation rehearsal quarterly
cadence — `.github/workflows/jwt-rotation-rehearsal-scheduled.yml`
NEW thin scheduler (cron `0 2 1 */3 *`) dispatches the existing
W11 rehearsal workflow with `target_env=staging` forced;
`docs/jwt-rotation-rehearsal.md` extended with new §4 "Quarterly
cadence" + downstream renumber §4→§10. ClusterPolicy namespace
exclusion closes the W12 retro D7 open item via
`infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml` NEW
(eight `PatchTransformer` documents — one per cluster-scoped
Kind — each stripping `/metadata/namespace` AFTER the
NamespaceTransformer runs); the kustomize v5.4.3 NamespaceTransformer
`fieldSpecs.kind:` filter is IGNORED (empirically reproduced;
documented as a workaround driver in
`docs/cluster-policy-namespace-exclusion.md` NEW). Monthly Redis
load-test reminder — `.github/workflows/redis-load-test-reminder.yml`
NEW (cron `0 14 1 * *` opens a same-month-idempotent reminder
issue with the W12 SLO baseline; 7-day stale-close); new §4.6 in
`docs/redis-cluster.md` covers cadence + operator
responsibilities + "why reminder not auto-applier" rationale.
PR-ready Redis envFrom `optional: false` flip patch
(`infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml` NEW;
NOT wired into `kustomization.yaml` this wave — artefact for
W14+ apply once cutover steady-state pre-conditions hold) +
`docs/prod-cutover.md §6` "Post-cutover hardening" NEW section
covering six W14–W16 tightening gates. Terraform CLI W14 bump
survey (`docs/terraform.md §6.6` NEW — candidate baselines,
seven-class migration-risks table, recommended pin `1.11.4`
provisional, bump-PR shape + rollback). Retro 2026-11 closes
the wave.

### Added (Phase K Wave 13 — PR pending)

- **`.github/workflows/jwt-rotation-rehearsal-scheduled.yml` —
  quarterly scheduler.** Thin scheduler — `schedule:` block fires
  `0 2 1 */3 *` (02:00 UTC on the 1st of every 3rd month: Jan,
  Apr, Jul, Oct) + a `workflow_dispatch` back-stop. Uses
  `actions/github-script@v7`'s `createWorkflowDispatch` to
  dispatch the existing W11 `jwt-rotation-rehearsal.yml` with
  `target_env=staging` forced in the dispatched payload. The
  inner W11 workflow is UNCHANGED — the W11 hard-gate inside the
  inner workflow remains the second-line defence. First scheduled
  fire: 2027-01-01 02:00 UTC.
- **`.github/workflows/redis-load-test-reminder.yml` — monthly
  reminder + stale-close.** Two jobs: `open-reminder` (cron
  `0 14 1 * *` + `workflow_dispatch`) opens an issue titled
  `Monthly Redis load-test reminder — YYYY-MM` carrying the W12
  SLO baseline (1000 RPS, p99 lookup < 5 ms, p99 write < 8 ms,
  error rate < 0.1 %), step-by-step apply commands, stale-close
  convention, cross-references; same-month re-fires no-op against
  the existing issue. `stale-close` paginates open issues with the
  workflow's label set (`ops,redis,load-test,reminder`); comments
  + closes any > 7 days old with `state_reason=not_planned`.
- **`infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml` —
  cluster-scoped-Kind namespace stripper.** Eight `PatchTransformer`
  documents — one per cluster-scoped Kind (`ClusterPolicy`,
  `ClusterRole`, `ClusterRoleBinding`,
  `CustomResourceDefinition`, `MutatingWebhookConfiguration`,
  `PersistentVolume`, `StorageClass`,
  `ValidatingWebhookConfiguration`); each `op: remove`'s
  `/metadata/namespace`. Wired into
  `infra/k8s/overlays/prod/kustomization.yaml`'s `transformers:`
  list AFTER `namespace-transformer.yaml` (order matters; the
  strip MUST follow the stamp). File header documents the
  kustomize v5.4.3 `NamespaceTransformer` `fieldSpecs.kind:`
  filter bug + minimal repro that drove the design choice.
- **`infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml`
  — PR-ready Redis envFrom required flip.** JSON6902 patch —
  `op: replace` on
  `/spec/template/spec/containers/0/envFrom/4/secretRef/optional`
  with value `false`. NOT wired into `kustomization.yaml` —
  PR-ready artefact for W14+ apply once the four pre-conditions
  in `docs/prod-cutover.md §6.2` hold (prod steady-state ≥ 7
  days, ESO secret rotation succeeded ≥ 2x, no open Sev-1/Sev-2
  referencing Redis in past 7 days, Hudson on-call window
  confirmed). File header documents the envFrom index mapping
  (0=configMap, 1=mahjong-autotable, 2=mahjong-jwt-keys,
  3=mahjong-jwt-rsa-keys, 4=mahjong-redis-prod) so the W14
  operator can audit before applying.
- **`docs/regional-eks-bringup.md` — per-region cluster bring-up
  readiness.** Eight sections — why-this-doc-exists, region
  inventory (four regions: us-east-1 primary apex, us-west-2
  secondary, eu-west-1 trans-atlantic, ap-southeast-1 SEA/DR-
  cold), per-region Cutover-Ready checklists (seven gates per
  region: TF state bucket per region, EKS cluster ACTIVE verified
  via `aws eks describe-cluster`, ACM cert per region, R53 health-
  check association, ESO target per region, ALB DNS published,
  probe sweep clean), cross-region invariants (DR data-replication
  direction, single-Redis baseline for W13, JWKS region-agnostic,
  image-SHA consistency, health-check IP allow-list), apply order
  (W14 us-east-1 first → us-west-2; W15 eu-west-1; W15+
  ap-southeast-1), failure-recovery, W14+ hand-offs, cross-
  references.
- **`docs/cluster-policy-namespace-exclusion.md` — W12 retro D7
  closure rationale.** Eight sections — bug history, why the W12
  NamespaceTransformer caused it (with §2.1 empirical reproduction
  showing fieldSpecs `kind:` filter ignored in kustomize v5.4.3),
  the W13 fix design, `kustomization.yaml` wire-up (order
  MATTERS), verification (before vs after `kustomize build`
  Kind/ns diff), cross-namespace invariant preserved, future-
  proofing (W14 stretch pre-commit lint script), cross-references
  including the upstream kustomize issue tracker.
- **`docs/jwt-rotation-rehearsal.md §4` — quarterly cadence.**
  Four sub-sections: §4.1 scheduler workflow + cron rationale,
  §4.2 rehearsal-report operator-review path, §4.3 quarterly run
  table (W11+W12 historical rows + scheduler-activation row at
  W13 + Q1–Q4 2027 placeholder rows), §4.4 off-cadence trigger
  rules. §4–§9 of the W11/W12 doc renumbered down to §5–§10;
  cross-refs inside the file updated. Cross-reference added in
  §10 to the new scheduler workflow.
- **`docs/redis-cluster.md §4.6` — monthly load-test reminder
  cadence.** Three sub-sub-sections: §4.6.1 cadence table, §4.6.2
  operator responsibilities (apply-day pairing with Hudson burn-
  rate window), §4.6.3 why a reminder not an auto-applier (prod-
  impact blast-radius, Hudson coordination, audit-trail
  preference for issue comments over workflow logs).
- **`docs/prod-cutover.md §6` — post-cutover hardening.** Seven
  sub-sections: §6.1 tightening calendar table with six gates
  W14–W16, §6.2 gate 1 Redis envFrom (referencing the new patch
  artefact + four pre-conditions for apply), §6.3 gate 4 Kyverno
  enforce mode, §6.4 gate 5 HPA min-replicas bump, §6.5 gate 6
  CSP enforce mode, §6.6 per-gate rollback, §6.7 per-gate
  observability table mapping each gate to a Hudson dashboard
  panel. Table of contents updated.
- **`docs/terraform.md §6.6` — W14 CLI bump survey.** Five
  sub-sub-sections: candidate baselines + HashiCorp release-page
  tracking inputs, pre-emptive migration risks table (seven risk
  classes: required_version floor, provider compat, HCL syntax,
  plan-output diffing, lock-file behaviour, DR rehearsal workflow
  pin, moved-blocks + the new `removed` block in 1.11), recommended
  W14 target pin `1.11.4` provisional, bump-PR shape, bump-PR
  rollback. No actual CLI bump this wave.
- **`docs/retro-2026-11.md` — monthly retro.** Six sections
  matching the W12 retro pattern: what shipped (W13 deliverables
  + cross-lane handoffs + gates), what worked well (cron-
  scheduler-delegate pattern, kustomize fieldSpecs empirical
  reproduction, PR-ready patch artefacts, per-region readiness
  template, single-issue-per-month idempotent reminder), what
  didn't work / open items (kustomize fieldSpecs filter ignored
  in v5.4.3, per-section-number coordination across docs is
  fragile, Hudson absence widened DevOps scope unexpectedly,
  envFrom patch index-pin fragile pending upstream
  kustomize#3625), lessons learned, what's coming in W14, cross-
  references.

### Changed (Phase K Wave 13 — PR pending)

- **`infra/k8s/overlays/prod/kustomization.yaml`** —
  `transformers:` list extended to reference the new
  `cluster-scoped-fieldspecs.yaml` AFTER `namespace-transformer.yaml`
  (the strip MUST follow the stamp; the order is documented as
  the canonical convention in
  `docs/cluster-policy-namespace-exclusion.md §4`). No other
  changes to the file — the W12 base wire-up is preserved
  verbatim.

## [0.21.0] — Phase K Wave 12 — 2026-10-XX (PR pending)

**Theme:** Prod cutover readiness (single-pane `docs/prod-cutover.md`
runbook covering Redis terraform plan readiness, kustomization
wire-up of W11 hand-offs, cutover-ready checklist, cross-namespace
kustomize pattern, rollback playbook) + multi-region edge surface
(per-region R53 ALIAS records + latency-based apex RR set + health
checks in `modules/edge/r53-regional-records.tf`, opt-in via the
new `regional_endpoints` tfvar, empty default preserves the W11
single-region apex) + Argo Rollouts NetworkPolicy hardening (three
NetworkPolicies in `argo-rollouts` ns — dashboard ingress allow-list
from `ingress-nginx` + `auth`, controller egress allow-list for
kube-apiserver + Prometheus + DNS, dashboard egress allow-list for
kube-apiserver + DNS) + Redis load-test re-baseline (k6 manifest at
`infra/load-tests/redis-load-test.yml`, 1000 RPS sustained for 5
min against the prod shape, p99 lookup < 5 ms / write < 8 ms SLO
thresholds, Prometheus scrape integration via the
experimental-prometheus-rw output) + prod kustomization wire-in
(swap top-level `namespace:` for a `NamespaceTransformer` with
`unsetOnly: true`, add `redis-connection-string-secret.yaml` +
`argo-rollouts-ingress-auth.yaml` + new
`argo-rollouts-network-policy.yaml` to `resources:`, add deployment
patch for the Redis envFrom mount with `optional: true` for
cutover-safe fall-through) + second JWT rotation rehearsal (W12
run at 3 min 48 s — 39 % faster than the W11 first run, all wins
downstream of Bishop W12 JWKS-cache pre-warm; squad recommendation:
the workflow is GA-ready for promotion to a scheduled monthly
cadence) + retro 2026-10.

### Added (Phase K Wave 12 — PR pending)

- **`docs/prod-cutover.md` — single-pane prod cutover runbook.**
  Five sections: 1. Prod Redis terraform plan readiness
  (pre-flight assertions, required tfvars, expected plan shape,
  apply gates); 2. Prod kustomization wire-up (W11 hand-off
  summary, W12 wire-in, runtime mount, apply order); 3.
  Cutover-Ready checklist (infrastructure, application,
  observability, frontend, per-region rollout — gated by agent
  lane); 4. Argo Rollouts dashboard cross-namespace pattern
  (rationale + the `NamespaceTransformer + unsetOnly: true`
  pattern for future cross-namespace fan-out); 5. Rollback
  playbook (application layer, infrastructure layer, edge layer).
  Supersedes the scattered "TODO: prod cutover" notes in
  `docs/redis-cluster.md` and `docs/argo-rollouts-setup.md`.
- **`infra/terraform/modules/edge/r53-regional-records.tf` —
  per-region R53 records.** Three resource types keyed by the new
  `regional_endpoints` variable: `aws_route53_health_check.regional`
  (per-region TCP/443 probe), `aws_route53_record.regional_alias`
  (per-region ALIAS A — the region-anchored hostname for the
  W7 prod-health-check probe matrix), `aws_route53_record.latency_apex`
  (RR set on the apex — clients hitting the apex resolve to the
  lowest-latency healthy region). The W7 `aws_route53_record.apex`
  is gated `count = (!local.use_latency_apex && …)` so the single-
  region apex is automatically skipped when `regional_endpoints`
  is non-empty. Wired into `infra/terraform/envs/prod/main.tf`
  + `variables.tf`; empty default preserves the W11 single-region
  behaviour.
- **`infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml` —
  Argo Rollouts NetworkPolicy hardening.** Three policies in the
  `argo-rollouts` namespace: `argo-rollouts-dashboard-ingress`
  (default-deny + allow-list from `ingress-nginx` ns + `auth` ns),
  `argo-rollouts-controller-egress` (allow-list to kube-apiserver
  + `monitoring` ns + kube-dns), `argo-rollouts-dashboard-egress`
  (allow-list to kube-apiserver + kube-dns). Closes the network-
  level loop on top of the W11 identity-level loop (auth-aware
  ingress). Split into three policies (vs one mega-policy) because
  the controller + dashboard have distinct egress profiles —
  keeps each allow-list minimal + easier to audit on chart
  upgrades. See `docs/argo-rollouts-setup.md §6`.
- **`infra/load-tests/redis-load-test.yml` — k6 prod-shape Redis
  load test.** Three-document manifest (Namespace + ConfigMap
  carrying the k6 script + Job). 1000 RPS `constant-arrival-rate`
  scenario, 5 min sustained, 30 s ramp-up. 80/20 lookup-vs-write
  mix matching Bishop's W10 idempotency-store hot-path profile.
  SLO thresholds enforced via k6 `thresholds:` (p99 lookup < 5 ms,
  p99 write < 8 ms, p99.9 lookup < 25 ms, error rate < 0.1 %).
  Prometheus integration via the experimental-prometheus-rw
  output — Hudson's prod Prometheus scrapes the Job pod and the
  metrics persist in the 90-d retention window for capacity-
  planning over time. See `docs/redis-cluster.md §4` for the
  methodology + the initial baseline (W12 first run recorded all
  SLOs with > 40 % headroom).
- **`infra/k8s/overlays/prod/namespace-transformer.yaml` — kustomize
  cross-namespace pattern.** Inline `NamespaceTransformer` with
  `unsetOnly: true` — replaces the top-level
  `namespace: mahjong-prod` directive on the prod overlay. Resources
  without a pre-declared `metadata.namespace` continue to pick up
  `mahjong-prod` (identical to the W11 behaviour); resources WITH
  a pre-declared namespace (the argo-rollouts ingress + W12
  NetworkPolicies, namespaced `argo-rollouts`) keep their declared
  value. Documented as the canonical pattern for future cross-
  namespace fan-out in `docs/prod-cutover.md §4`.
- **`docs/redis-cluster.md §4` — load-test methodology.** New
  section (renumbering pushed §4–§12 down to §5–§13). Captures
  the W12 k6 manifest target workload, SLO thresholds, run
  procedure, W12 initial baseline (vs the W10 staging baseline),
  re-baseline cadence rules, and Prometheus observability hooks.
- **`docs/argo-rollouts-setup.md §6` — NetworkPolicy hardening.**
  New section (renumbering pushed §6–§9 down to §7–§10). Walks
  the three policies, the split rationale, the wire-in via the
  W12 kustomization, validation steps (positive + negative tests),
  upgrade procedure when bumping the argo-rollouts Helm chart,
  and the rollback path.
- **`docs/jwt-rotation-rehearsal.md §3` — rehearsal history.** New
  section (renumbering pushed §3–§8 down to §4–§9). Documents
  both runs (W11 first + W12 second), per-phase timing deltas (W12
  is 39 % faster, the wins all downstream of Bishop W12's JWKS-
  cache pre-warm), GA-readiness recommendation, and target timing
  scale (green / yellow / red) for future runs.
- **`docs/edge-region-probes.md §3` — W12 R53 record delivery
  update.** Section content extended in-place to document the new
  region-anchored hostname path, the tfvar shape, the cutover from
  "same root URL" to region-anchored hostnames, and the rollback
  (single `terraform apply -var='regional_endpoints=[]'` reverts to
  the W11 single-region apex).
- **`docs/retro-2026-10.md` — October retro.** Wave 12 row, Apone
  + cross-lane recap, lessons from the rehearsal (W12 speedup
  validated the W11 baseline), open items (regional EKS clusters
  + scheduled rehearsal promotion + W13 load-test re-baseline
  cadence).
- **`Phase_K_W12/Apone/{charter,history}.md` + W12 memo + agent
  history append.** Wave artifacts (mirror of W11 shape).

### Changed (Phase K Wave 12)

- **`infra/k8s/overlays/prod/kustomization.yaml` — namespace
  pinning via transformer.** Removed top-level
  `namespace: mahjong-prod`; added `transformers:` referencing
  `namespace-transformer.yaml`. Added three new entries to
  `resources:` (redis ESO + argo-rollouts ingress + argo-rollouts
  NetworkPolicy). Added one deployment patch (envFrom secretRef
  `mahjong-redis-prod` with `optional: true`). Semantics for the
  W11 in-base resources unchanged; W12 hand-off resources now in-
  band and survive `kubectl apply -f -` via the kustomize build
  pipeline.
- **`infra/k8s/overlays/prod/redis-connection-string-secret.yaml` —
  header status flipped IN-BAND.** Header updated from "OUT-OF-BAND
  TEMPLATE — NOT in any kustomization.yaml resources list" to
  reflect the W12 wire-in. Manifest body unchanged.
- **`infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml` —
  header status flipped IN-BAND (cross-namespace).** Same as above
  but cross-namespace — pinned to `argo-rollouts` via the new
  transformer's `unsetOnly: true` semantic.
- **`infra/terraform/modules/edge/{main,variables,outputs}.tf` —
  multi-region surface.** New `variable "regional_endpoints"` (list
  of `{ region, alb_dns_name, alb_zone_id, hostname }` objects),
  `local.use_latency_apex` flag toggling between W7 single-region
  apex and W12 latency-RR-set apex, new outputs
  `regional_health_check_ids` + `regional_hostnames` + an updated
  `apex_fqdn` that falls through to `var.domain_name` when the
  latency apex is active.
- **`infra/terraform/envs/prod/{main,variables}.tf` — wire the
  `regional_endpoints` tfvar.** Empty default preserves the W11
  apex behaviour for operators who haven't yet stood up regional
  EKS clusters.

## [0.20.0] — Phase K Wave 11 — 2026-09-XX (PR pending)

**Theme:** Prod infrastructure cutover (Redis ElastiCache prod
env stack — multi-AZ `cache.r6g.large` + 7-day snapshots + CMK
KMS + AUTH+TLS; prod edge module instantiation with BLOCK-mode
WAF; prod overlay ESO for the connection string) + operator
hardening (Argo Rollouts auth-aware ingress finally lands behind
the existing oauth2-proxy + dex OIDC chain, superseding the W10
§4.3 placeholder; Terraform CLI pin bumped 1.9.8 → 1.10.5 with a
documented quarterly cadence + range-floor / exact-pin policy) +
multi-region prod-health-check matrix (W10 single-region probe
generalised to 4 regions — us-east-1 / us-west-2 / eu-west-1 /
ap-southeast-1 — with per-region issue state markers and a
multi-region failure-mode playbook) + JWT rotation rehearsal
harness (staging-only `workflow_dispatch` workflow exercising
the W10 §3 rotation sequence end-to-end with JWKS-validation
asserts; the first quarterly prod rotation lands end-Sep 2026)
+ retro 2026-09 W11 entries.

### Added (Phase K Wave 11 — PR pending)

- **`infra/terraform/envs/prod/` — prod env Terraform stack.**
  Edge module with BLOCK-mode WAF (vs staging's COUNT), 90-day
  CloudFront log retention, ACM cert in us-east-1, prod
  CloudFront `PriceClass_All`. Redis module at the prod tier:
  `cache.r6g.large` (graviton2 + memory-optimised),
  `replica_count = 1` for multi-AZ failover, 7-day snapshot
  retention (3 AM UTC window), CMK KMS encryption at rest
  (`alias/mahjong-prod-elasticache`), AUTH token + TLS in
  transit, Sunday off-peak maintenance window. Operator-fill
  `terraform.tfvars.example` + `backend.example.hcl`
  (S3 bucket `mahjong-tfstate-prod`, DynamoDB lock table
  `mahjong-tflock-prod`). Sensitive outputs: omnibus
  `redis_connection_string` + split-form `redis_auth_token`.
  `terraform validate` clean.
- **`infra/k8s/overlays/prod/redis-connection-string-secret.yaml`
  — prod Redis ExternalSecret.** 15-min refresh interval. Mounts
  `Idempotency__Redis__ConnectionString` from SSM SecureString
  `/mahjong/prod/redis/connection-string`. Out-of-band — applied
  manually via `kubectl apply -f` once the prod EKS cluster
  bootstraps (NOT in `kustomization.yaml` resources list, same
  pattern as W4's `jwt-keys-secret.yaml`).
- **`infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml` —
  auth-aware Ingress for the Argo Rollouts dashboard.** Uses
  nginx-ingress `auth-url` / `auth-signin` subrequest pattern to
  gate access via the prod oauth2-proxy + dex OIDC chain (same
  chain that fronts the production app). Path rewrite
  `/argo-rollouts(/|$)(.*)` → `/$2`, TLS+HSTS inheritance via
  the prod ingress class, host `mahjong.example.com`. Supersedes
  the pre-W11 placeholder warning in
  `docs/argo-rollouts-setup.md §4.3`.
- **`.github/workflows/jwt-rotation-rehearsal.yml` — staging-only
  JWT rotation rehearsal harness.** `workflow_dispatch` with a
  hard `target_env=staging` gate (refuses to run against prod).
  End-to-end rehearsal: generate fresh RSA-4096 key,
  promote in SSM (active → previous → archive), force ESO
  refresh, rolling restart, validate `/.well-known/jwks.json`
  (old kid still present, new kid live, total keys ≥ 3 — the W10
  §3 invariant), optional archive cleanup, emit
  `docs/jwt-rotation-rehearsal-YYYY-MM-DD.md` artefact. The on-
  call SRE runs this once against staging the week before each
  quarterly prod rotation. Actionlint clean.
- **`docs/jwt-rotation-rehearsal.md` — rehearsal operator
  runbook (NEW).** 8 sections: purpose (90d cadence; first
  rotation end-Sep 2026), prereqs (OIDC role +
  `KUBECONFIG_STAGING` secret), workflow trigger, what the
  workflow does (mirrors `docs/jwt-ssm-runbook.md §4`), dry-run
  guidance, failure-mode table (one row per step — symptom /
  cause / recovery), post-rehearsal review checklist, cross-
  refs.
- **`docs/edge-region-probes.md` — multi-region probe operator
  runbook (NEW).** 8 sections: purpose (W10 single-region →
  W11 4-region rationale), topology, per-region target
  resolution via `vars.PROD_BASE_URL_<REGION>` (same root URL
  default at W11; per-region R53 records deferred to W12+),
  state-marker decoding
  (`<!-- prod-health-check:state region=X strikes=N recoveries=M -->`),
  failure-mode playbook (1-region / 2-region / 4-region
  patterns), CloudFront edge mapping, manual reproduction,
  cross-refs.
- **`docs/redis-cluster.md` §11 — prod sizing + ESO wiring
  (Phase K Wave 11).** New section (Cross-references renumbered
  §10 → §10 + alias-stub §12). Documents the prod-tier sizing
  table + rationale, the apply walkthrough, the prod SSM push
  (omnibus connection-string for the runtime mount + split-form
  for the rotation path), the out-of-band ESO manifest
  application flow, the prod smoke-test sequence, and the IAM
  patch for the ESO ClusterSecretStore prefix.
- **`docs/argo-rollouts-setup.md` §5 — auth-aware ingress (Phase
  K Wave 11).** New section between §4 Dashboard access and the
  prior §5 Validation. Subsequent sections renumbered
  (Validation §5 → §6, Wiring §6 → §7, Rollback §7 → §8,
  Cross-refs §8 → §9). Documents the manifest location, the
  auth-request subrequest flow, the path rewrite, TLS/HSTS
  hygiene, validation curls, and the rollback path. §4.3
  retained as a pre-W11 placeholder with an explicit pointer to
  §5.
- **`docs/terraform.md` §6 — Version policy (Phase K Wave 11).**
  New section. Documents the range-floor (`required_version =
  ">= 1.5.0"` in modules) vs exact-pin (workflow
  `terraform_version: "1.10.5"`) discipline, the quarterly bump
  cadence anchored on Wave bring-up (W8 = 1.9.8, W11 = 1.10.5,
  W14 = TBD), the out-of-band CVE process, the lock-file
  discipline (per env stack, not per module), and the
  `setup-terraform@v3` action-pin policy. Cross-references
  renumbered §6 → §7.
- **`docs/retro-2026-09.md` — September 2026 monthly retro.**
  Template consistent with retro-2026-08. Sections: what
  shipped (Wave 11), WIP / open hand-offs, lessons learned
  (§3.1 range-floor + CI-pin TF version policy, §3.2 rehearse
  before the first quarterly rotation, §3.3 out-of-band ESO
  manifests are a feature, §3.4 multi-region probes need a
  failure-mode playbook), action items (carry into October
  2026), metric movement, cadence notes, cross-refs.
- **`Phase_K_W11/Apone/charter.md` + `Phase_K_W11/Apone/history.md`
  — wave-scoped DevOps charter + history excerpt.**
- **`.squad/decisions/inbox/apone-phase-k-wave-11.md` — Apone
  W11 decision memo.** 6 decisions: D1 prod Redis stack at
  `cache.r6g.large`, D2 Argo auth-aware ingress via existing
  OIDC chain, D3 TF CLI pin bump + quarterly policy, D4 JWT
  rehearsal harness (staging-only, hard gate), D5 multi-region
  prod-health-check (4 regions), D6 CHANGELOG 0.20.0 +
  retro-2026-09.

### Changed (Phase K Wave 11 — PR pending)

- **`.github/workflows/prod-health-check.yml` — REWRITTEN as a
  4-region matrix.** W10 single-region pattern (5-min cron,
  3-strike-open, 2-recovery-close, GitHub issue lifecycle)
  generalised to a `strategy.matrix.region` fan-out across
  `us-east-1`, `us-west-2`, `eu-west-1`, `ap-southeast-1`.
  Per-region target via `vars.PROD_BASE_URL_<REGION>` (falls
  back to global default with a yellow-flag step-summary if
  unset). Each matrix leg emits `verdict-<region>.json` via
  `actions/upload-artifact@v4`. Aggregator job downloads with
  `pattern: verdict-*` + `merge-multiple: true`, parses each
  verdict, maintains per-region HTML state markers
  (`<!-- prod-health-check:state region=X strikes=N recoveries=M -->`).
  Opens the prod-health-check issue when ANY region trips the
  strike threshold (3 consecutive failures); closes only when
  ALL four regions have recovered (2 consecutive successes).
  Actionlint clean.
- **`.github/workflows/dr-rehearsal.yml` — Terraform CLI pin
  bump.** `hashicorp/setup-terraform@v3` `terraform_version:`
  bumped `1.9.8` → `1.10.5` (the W11 quarterly bump per the new
  `docs/terraform.md §6` cadence). Sole TF version surface in
  the repo at W11; module floors remain `>= 1.5.0` (range-
  based, forward-compatible).

### Fixed (Phase K Wave 11 — PR pending)

- **W10 §4.3 Argo dashboard ingress placeholder superseded.**
  The W10 install runbook intentionally warned against ingress-
  fronted dashboard access pending an auth-aware proxy. W11
  ships the auth-aware proxy via the existing oauth2-proxy +
  dex OIDC chain — the W10 deferred hand-off is closed.

## [0.19.0] — Phase K Wave 10 — 2026-08-09 (PR pending)

**Theme:** Squad-git-lock path cutover (`/tmp/squad-git-lock` →
`.work/squad-git-lock` — survives `/tmp` cleanup, lives inside the
repo's working tree, completes the W9 hand-off plan) + Redis
ElastiCache Terraform module (Bishop's W10 `RedisIdempotencyStore`
dependency — single-shard replication group with auth-token + TLS,
custom parameter group with `allkeys-lru`, wired into the staging
env stack) + Argo Rollouts cluster install runbook (W9 hand-off —
pinned chart 2.37.7, kubectl plugin v1.7.2, dashboard via port-
forward only, wires to W9 `canary-deployment.yaml`) + JWT SSM
runbook §3 quarterly rotation (cadence tightened 180d → 90d,
explicit `aws ssm put-parameter` walkthrough + JWKS validation +
rollback procedure) + `container-scan-remediation.yml` workflow
(consumes W6 Trivy artefact, opens / updates a de-duped GitHub
issue with HIGH+CRITICAL CVE list + suggested base-image bump
heuristic + W6 allowlist pointer) + `prod-health-check.yml`
workflow (every 5 min: `/healthz` + `/readyz` + `/metrics` +
`/.well-known/jwks.json`; 3-strike cooldown opens incident issue,
2-strike recovery closes it; optional Slack webhook) + retro
2026-08 W10 entries.

### Added (Phase K Wave 10 — PR pending)

- **`infra/terraform/modules/redis/` — Redis ElastiCache module.**
  Single-shard replication group (`aws_elasticache_replication_group`)
  with configurable replica count + multi-AZ. Custom parameter
  group with `maxmemory-policy=allkeys-lru` (Bishop's idempotency
  store treats Redis as a cache, not a primary store). Optional
  `random_password`-generated auth-token (sensitive output) +
  TLS in transit (`transit_encryption_enabled=true`) + at-rest
  encryption (`at_rest_encryption_enabled=true`). Security group
  pre-wired with VPC-CIDR ingress + opt-in allowed-SG ingress.
  Outputs: primary/reader endpoints, port, security group id,
  `redis_connection_string` (sensitive), `redis_auth_token`
  (sensitive). `terraform validate` clean. Wired into the staging
  env stack at the cheap shape (`cache.t4g.micro`, 0 replicas,
  no snapshots).
- **`docs/redis-cluster.md` — Redis operator runbook (W10).**
  10 sections: topology, provisioning, SSM push for the
  connection string, KMS wiring, version-bump procedure, ESO
  wiring for the `RedisIdempotencyStore` runtime, smoke test,
  rotation cadence (matches JWT §3 quarterly), rollback,
  cross-refs.
- **`docs/argo-rollouts-setup.md` — Argo Rollouts cluster install
  runbook (W10 — picks up W9 hand-off).** 8 sections: prereqs,
  Helm install pinned to chart `argo-rollouts 2.37.7`, kubectl
  plugin install (`v1.7.2`), dashboard access via `kubectl
  port-forward` (no public ingress; Apone+Vasquez decided
  against an auth-aware proxy until W11+), validation, Helm
  chart wiring (`helm/mahjong/templates/canary-deployment.yaml`
  from W9), rollback, cross-refs.
- **`.github/workflows/container-scan-remediation.yml` —
  remediation issue automation (W10).** Triggers: nightly
  05:00 UTC (1 h after W6 `container-scan.yml`), `workflow_run`
  on `container-scan` failure, manual dispatch. Downloads the
  W6 findings artefact, filters to HIGH+CRITICAL, opens or
  updates a single de-duped GitHub issue (title prefix
  `[container-scan] CVE remediation`, labels
  `security,automated`). Issue body includes the CVE table,
  suggested base-image bump if the same target accounts for the
  majority of findings, and a pointer to `docs/secrets-scanning.md`
  §4 (W10 — new section). Does NOT open base-image bump PRs
  (the squad reviews CVE remediation before bumping).
- **`.github/workflows/prod-health-check.yml` — synthetic prod
  probe (W10).** Cron `*/5 * * * *`. Probes `/healthz` +
  `/readyz` + `/metrics` + `/.well-known/jwks.json`. Asserts
  HTTP 200 + body shape (`status:"ok"`, JWKS `keys` count ≥ 3)
  + latency budget (`/readyz` ≤ 1500 ms) + body size
  (`/metrics` > 1024 B). 3-strike cooldown opens an incident
  issue (`labels: incident,automated,production`); 2 clean
  runs close it. Optional `SLACK_WEBHOOK_URL` secret triggers
  best-effort Slack notification. `workflow_dispatch.inputs`
  cover target URL override, latency-budget override, and a
  reset-strike-counter switch.
- **JWT SSM runbook §3 quarterly rotation walkthrough.**
  Tightens cadence 180-day → **90-day (quarterly)** to match
  the W10 secret-management quarterly cadence. New §3.2 ships
  the full `aws ssm put-parameter` command sequence with
  pre-flight + post-flight JWKS validation (`curl
  /.well-known/jwks.json | jq '.keys | length'` ≥ 3 distinct
  `kid` values). New §3.3 quarterly hand-off checklist. New
  §3.4 quarterly rollback (promote previous→active +
  archive→previous; the just-minted key is intentionally
  discarded — a key clients have rejected is a key we never
  want to revisit).
- **`docs/secrets-scanning.md` §4 — CVE remediation flow.**
  Net-new section that turns the W6 container-scan findings
  into action: §4.1 two-scanner taxonomy table (W3/W6 gate
  vs W10 remediation), §4.2 triage tree, §4.3 base-image
  bump walkthrough, §4.4 W6 allowlist as last resort, §4.5
  closing-the-loop matrix. Existing §4 "Operational cadence"
  renumbered to §5; §5 Triage SLA → §6; §6 Cross-references → §7
  (and the §7 cross-refs picked up two new entries for the
  W10 workflow + the W3/W6 scanner).
- **`docs/production-deployment-runbook.md` §8 — Continuous
  health probes (W10).** Inserted BEFORE the existing
  Companion docs section (renumbered to §9). Documents the
  W10 prod-health-check workflow: what it checks, the 3-strike
  cooldown / 2-strike recovery behaviour, operator integration
  (probe is backstop, not pager replacement), configuration,
  disabling for planned maintenance, troubleshooting (flaky
  probe vs silent probe vs suppress-without-disable), cross-refs.
- **`docs/retro-2026-08.md` — Phase K Wave 10 retro.** Modelled
  on `docs/retro-2026-07.md`. Sections: What shipped, What
  worked, What broke, Decisions worth carrying, Open items /
  W11 hand-offs.

### Changed (Phase K Wave 10 — PR pending)

- **Squad git-lock cutover COMPLETE.** All flock invocations now
  use `9>.work/squad-git-lock` (was `/tmp/squad-git-lock` in W9).
  `docs/agent-handoff-protocol.md` §3.6 + §3.7 updated;
  `.squad/decisions.md` carries EDIT(W10) blockquote notes at
  the top of the W6, W7, W8 summaries explaining the new lock
  path. Historical `.squad/agents/*/history.md` blocks left
  unchanged per the retro exemption (§3.6).
- **Wave tag bump.** `infra/terraform/envs/staging/main.tf`
  common_tags `Wave` field bumped `phase-k-wave-8` →
  `phase-k-wave-10`. (Bumped to W10 — W9 didn't touch the
  envs stack.)
- **W9 CHANGELOG entry.** `[0.18.0]` annotation flipped from
  `(PR pending)` to `(PR #55)` now that PR #55 has merged.

### Hand-offs to Wave 11

- **Production stack for Redis.** The W10 module is wired into
  staging only; W11 picks up the prod env stack (will need
  multi-AZ + a replica + KMS rotation policy review). Cheap
  shape `cache.t4g.micro` is intentional for staging; prod will
  bump to `cache.r7g.large` or similar based on the W10 load
  test once it runs.
- **Argo Rollouts dashboard ingress.** Currently port-forward
  only (Apone+Vasquez decided against a public dashboard
  ingress until an auth-aware proxy with OIDC SSO is in
  place). W11 picks up the proxy design.
- **Terraform CLI pin.** `.tool-terraform/terraform` is v1.9.8.
  Latest at W10 cut was v1.15.x. The pin still works for the
  current modules but is stale; W11 should evaluate a bump.
- **Quarterly JWT rotation.** First quarterly rotation under
  the new cadence: end of September 2026 (Q3). The W11 on-call
  inherits this — the W10 §3.3 checklist is the entry point.


**Theme:** Production canary retarget (single W8 AnalysisTemplate
→ three independent gates: success-rate + p99-latency + error-
budget burn rate) + mobile production-hotfix workflow (separate
two-reviewer env-gate, bypasses External-Testing with audit-trail
guarantees) + cross-file invariant audit (`scripts/check_invariants.py`
gates the JwtRsaKeys ↔ ESO Secret name + SSM path + env-var
prefix lock-step across 7 surfaces) + YAML symbolic anchors in
the values overlays (centralise per-env scalars under
`x-anchors:` so a hostname change touches one line) +
rebase-inside-flock pattern (W10 squad-git-lock cutover plan +
canonical commit pattern) + retro 2026-07 W9 entries.

### Added (Phase K Wave 9 — PR pending)
- **`.github/workflows/mobile-production-hotfix.yml` — mobile
    production-hotfix workflow.** New workflow triggered by
    `mobile-hotfix-v*.*.*` tag-push or `workflow_dispatch` on
    `main` with mandatory `hotfix_reason` input. Bypasses the W8
    External-Testing soak window (operator action: this is the
    "skip 7-day soak" escape hatch for security / revenue-impacting
    bugs). Gated by **`release-channel-production-hotfix` GitHub
    Environment with 2 required reviewers** (vs the routine
    workflow's 1) — the second pair of eyes is on the
    decision-to-skip-soak, not just on the output. Validates the
    supplied `internal_tag` exists and matches the workflow's
    checkout SHA, so a hotfix cannot originate from a build that
    never landed in Internal Testing. Emits three durable
    audit-trail markers per run: a `::warning::` log line, a
    `step-summary` banner with the hotfix reason, and a Slack
    notification on `#mobile-releases` with the reason verbatim.
    Defaults to **full Android rollout (1.0 / `status: completed`)**
    rather than staged — a hotfix worth skipping soak is worth
    fully replacing the broken build immediately; operator can
    override via `android_rollout_fraction` input. iOS submits
    with `automatic_release=true`; operator requests Expedited
    Review out-of-band via App Store Connect (no public API).
    Doc: `docs/mobile-release.md` §7.2 "Hotfix path" with full
    when-to-use guidance table. (Apone)
- **`helm/mahjong/templates/canary-deployment.yaml` — three-gate
    production canary retarget.** Refactored the W8 single
    AnalysisTemplate (`success-rate`) into **three independent
    AnalysisTemplates** rendered conditionally based on
    `canary.analyses.<name>.enabled` flags. The Rollout's
    analysis step references all enabled templates; ANY single
    failure aborts the rollout. New templates: (1)
    `…-canary-success-rate` (default threshold ≥ 99% non-5xx),
    (2) `…-canary-p99-latency` (default ≤ 500 ms via
    `histogram_quantile(0.99, ...) * 1000`), (3)
    `…-canary-error-budget` (default < 14.4 burn-rate — Google
    SRE 2%/1h fast-burn alert threshold against
    `sloErrorRate: 0.01`). Default window is 5m (count=10 × 30s
    interval), `failureLimit: 1`. Wave annotation bumped to
    `phase-k-wave-9`. Helm-templating fix: `$.Values.canary.analyses.*`
    inside the `range $i, $step := .Values.canary.steps` block
    (dot context is the step inside the range; `$` reaches
    chart root). (Apone)
- **`helm/mahjong/values.yaml` — `canary.analyses.*` config
    surface.** New `canary.analyses.{successRate,p99Latency,
    errorBudget}` blocks with per-template `enabled` /
    `interval` / `count` / `threshold` / `failureLimit` /
    `metric` / `window` keys. Legacy `canary.analysis` block
    kept (marked superseded; safe to remove in W10+ once the
    surface has soaked). `metricEndpoint` example updated to
    `prometheus.monitoring.svc.cluster.local:9090` (the canonical
    in-cluster Prometheus Service:port; W8 referenced
    `prometheus-server.monitoring.svc.cluster.local:80` from a
    different kube-prometheus-stack release). (Apone)
- **`helm/mahjong/values-prod.yaml` — prod canary overlay.**
    New top-level `canary:` block: `enabled: false` (operator
    flips per release; staying `false` in the overlay means a
    routine `helm upgrade` does NOT silently enable canary mode),
    `metricEndpoint: *prod-prometheus` (the anchor — see
    YAML-anchor refactor below), `analyses.successRate.threshold:
    0.99`, `analyses.p99Latency.threshold: 500`,
    `analyses.errorBudget.threshold: 14.4`,
    `analyses.errorBudget.sloErrorRate: 0.01` (99% availability
    SLO). All three templates use count=10 × 30s = 5m windows.
    (Apone)
- **`helm/mahjong/values-{staging,prod}.yaml` — YAML symbolic
    anchors.** Added top-level `x-anchors:` block to each overlay
    declaring `&{env}-host`, `&{env}-tls-secret`,
    `&{env}-env-name`, `&{env}-cors-origin`; prod additionally
    declares `&prod-prometheus`. Every consumer (ingress hosts,
    ingress TLS, ASPNETCORE_ENVIRONMENT, CORS allowed origins,
    canary metric endpoint) now references the anchor via
    `*name`. Helm ignores the `x-anchors:` top-level key (`x-*`
    is the de-facto "ignored extension" convention from OpenAPI
    / docker-compose / GitHub Actions); the chart-of-charts
    merge accepts it without rendering. Doc cross-references in
    the values-file docstring switched from numeric (`§3.5`) to
    symbolic (`§canary-analysis`, `§parity-matrix`,
    `§yaml-anchor-pattern`, `§subchart-toggles`); the doc adds
    matching `<a name="...">` anchors so renumbering doesn't
    break the references. Pattern + when-not-to-use + verification
    documented in `docs/helm-charts.md §6 "YAML anchor pattern
    in values files"`. (Apone)
- **`scripts/check_invariants.py` — cross-file invariant audit.**
    Generalises the W7 `check_signer_identity.py` pattern to
    OTHER cross-file lock-step bindings. W9 ships one new
    binding: **JwtRsaKeys ↔ ESO Secret name + SSM path + env-var
    prefix**. Audits 7 surfaces (`infra/k8s/overlays/{prod,staging}/jwt-rsa-keys-secret.yaml`,
    `helm/mahjong/values.yaml`, `helm/mahjong/values-prod.yaml`,
    `helm/mahjong/values-staging.yaml`,
    `helm/mahjong/charts/mahjong-api/values.yaml`,
    `docs/jwt-rotation.md`) and asserts: `target.name:
    mahjong-jwt-rsa-keys` (prod) / `…-staging` (staging), 3
    `auth__jwtrsakeys__{0,1,2}` env-var slot definitions per
    overlay, 3 SSM `/mahjong/{env}/auth/jwt/rsa-{active,previous,
    archive}` references per overlay, ≥ 1 helm
    `externalSecrets[]` entry per values file, doc references
    cover all three SSM slots and all three env-var slots.
    Re-runs `scripts/check_signer_identity.py` as a subprocess
    so a single pre-commit hook covers BOTH invariant scripts
    (developer doesn't manage two hook entries). New `Invariant`
    constants extend the audit without touching the runner —
    onboarding doc: `docs/signer-identity-invariant.md §6
    "Other invariants audited"`. (Apone)
- **`.pre-commit-config.yaml` — `cross-file-invariants` hook
    entry.** Adds the new `check_invariants.py` script with
    `--skip-signer-identity` flag (the previous hook already
    runs that check). `always_run: true` + `pass_filenames:
    false` per the W7 convention — the hook checks the full
    surface set on EVERY commit because `files:`-scoped checks
    miss drift introduced via partial commits. (Apone)
- **`.gitignore` — `.work/*` + `!.work/.gitkeep`.** The `.work/`
    directory becomes the squad's canonical session-scratch
    area in W10+ (squad-git-lock location, helm-render outputs,
    per-agent backup copies). Everything under `.work/` is
    ignored EXCEPT `.work/.gitkeep`, which guarantees the
    directory exists on a fresh clone so `flock 9>.work/squad-git-lock`
    and `helm template ... > .work/...` don't fail on a missing
    parent. (Apone)
- **`.work/.gitkeep` — directory placeholder.** Empty file
    tracked by git solely to materialise the `.work/` directory
    on clone. Paired with the `.gitignore` `!.work/.gitkeep`
    negation. (Apone)
- **`docs/agent-handoff-protocol.md` §3.6, §3.7 — lock-file
    relocation + rebase-inside-flock.** New §3.6 documents the
    `/tmp/squad-git-lock` → `.work/squad-git-lock` cutover plan:
    W9 keeps `/tmp/` (mid-wave migration would defeat the mutex);
    W10+ canonical is `.work/`. Lists the three problems with
    `/tmp/`: ephemeral wipe, world-writable shared with non-squad
    processes, runtime hard-prohibition on writes under `/tmp/`.
    New §3.7 documents the canonical commit pattern with `git
    fetch origin <branch>` + `git rebase origin/<branch>` INSIDE
    the flock critical section, so a non-squad push or a
    pre-flock push that landed between our last fetch and our
    commit doesn't cause a non-fast-forward rejection. Conflict
    semantics: `git rebase --abort` + bail-out without pushing
    — operator escalation is the correct path (cross-lane
    shared-file edits are rare and a process-violation signal).
    (Apone)
- **`docs/helm-charts.md` §3.5 "AnalysisTemplate gates" (new),
    §6 "YAML anchor pattern" (new).** §3.5 details the W9 three-
    gate retarget: PromQL for each template, default thresholds
    + interpretation, tuning playbook for operators (when to
    increase window vs failureLimit, how to disable a single
    template). Adds `<a name="canary-analysis">` HTML anchor.
    §6 documents the `x-anchors:` YAML-anchor convention
    introduced in `values-{staging,prod}.yaml`, with anchor
    pattern, when-NOT-to-use list, and PyYAML + helm-template
    verification commands. Adds `<a name="yaml-anchor-pattern">`,
    `<a name="parity-matrix">`, `<a name="subchart-toggles">`
    HTML anchors for the symbolic doc cross-references. Renumbers
    §3.5–§3.6 → §3.6–§3.7 and §6–§8 → §7–§9 (W8 convention:
    insert + renumber, never co-locate). (Apone)
- **`docs/mobile-release.md` §7.2 "Hotfix path" (new).** Full
    runbook for the W9 production-hotfix workflow: triggers
    (tag namespace `mobile-hotfix-v*.*.*`), the 2-reviewer env
    setup, audit-trail guarantees (warning log + step-summary
    banner + Slack), Internal-tag validation, default
    rollout posture (100% Android, automatic_release iOS), and
    a when-to-use decision table (RCE / >1% crash / revenue-
    blocking = yes; UX paper-cut / <1% crash = no; subscription
    pricing = judgement call). Renumbers §7.2 → §7.3 through
    §7.8 → §7.9. (Apone)
- **`docs/signer-identity-invariant.md` §6 "Other invariants
    audited" (new).** Documents the W9 audit pattern + the new
    JwtRsaKeys binding: rationale (RS256 fallback analogue of
    the W5 HS256 drift incident), the 7 surfaces, the two
    assertion modes (exact-value vs min-count), the wiring (the
    `--skip-signer-identity` wrapper), and the extension recipe
    for adding future invariants. Renumbers §6 → §7. (Apone)

### Changed (Phase K Wave 9 — PR pending)
- **Default canary `metricEndpoint`** (`helm/mahjong/values.yaml`)
    updated from `prometheus-server.monitoring.svc.cluster.local:80`
    to `prometheus.monitoring.svc.cluster.local:9090` to match
    the canonical kube-prometheus-stack `Service` name +
    Prometheus's default upstream port. The W8 default was correct
    for `helm install … prometheus-community/prometheus` but
    not for `kube-prometheus-stack`; the W9 default is the union
    of "what production actually runs" + Prometheus's canonical
    9090 port. Override via `--set canary.metricEndpoint=...` if
    a deployment uses a different chart. (Apone)
- **`canary-deployment.yaml` wave annotation** bumped from
    `phase-k-wave-8` → `phase-k-wave-9`. (Apone)

### Fixed (Phase K Wave 9 — PR pending)
- **Lock-file race window between flock-protected pushes**
    (`docs/agent-handoff-protocol.md §3.7`). The W6/W7/W8 flock
    pattern serialised the local critical section but did NOT
    guard against non-fast-forward rejection on push when a
    sibling agent's push landed during our edit window. The W9
    pattern adds `git fetch origin + git rebase origin/<branch>`
    INSIDE the flock so the local tip catches up to the remote
    before the push. (Apone)

### Squad / process (Phase K Wave 9 — PR pending)
- **Apone (DevOps).** Production canary retarget (3
    AnalysisTemplates) + mobile production-hotfix workflow +
    cross-file invariant audit (JwtRsaKeys binding) + YAML
    symbolic anchors in values overlays + rebase-inside-flock
    pattern + retro 2026-07 W9 entries. Lane-discipline:
    DevOps-only paths (`helm/`, `.github/workflows/`, `scripts/`,
    `docs/`, `.pre-commit-config.yaml`, `.gitignore`).

## [0.17.0] — Phase K Wave 8 — 2026-07-09 (PR pending)

**Theme:** Staging edge cutover (W7 module → staging env) + CI
pre-commit gate (six-file invariant + path-confusion guard now
enforced in CI) + kyverno-enforce-patch canonical-path
reconciliation (presence-check guard added to the invariant
script) + mobile Production track promotion workflow (env-gated
App Store + Play Production) + Helm canary deployment via Argo
Rollouts (5%→20%→50%→100% with Prometheus analysis) + DR
rehearsal automation workflow (quarterly `workflow_dispatch`,
inverts the W6 health check + writes a results report) + retro
2026-07.

### Added (Phase K Wave 8 — PR pending)
- **`infra/terraform/envs/staging/` — staging edge env.** New
    Terraform env instantiating the W7 `modules/edge/` against
    the staging EKS ingress. Two-provider wiring (default +
    `aws.us_east_1` alias) required by the module's
    `configuration_aliases`. State backend isolated from prod
    (`mahjong-tfstate-staging` bucket / `mahjong-tflock-staging`
    DynamoDB lock table) so a staging operator typo cannot
    corrupt the prod state. Staging diverges from prod on
    `waf_managed_rules_action`: `COUNT` (observation mode)
    instead of `BLOCK`, so a managed-rule false-positive in
    staging records but does NOT take staging down — the W8 → W9
    hand-off includes the `count` → `block` flip on staging
    after a quarter of soak. Variables surface mirrors the W7
    module: `domain_name`, `alb_dns_name`, `waf_rate_limit_per_5min`
    (staging default 100/5min, prod 1000/5min — staging traffic
    floor is well below 100 req/5min so the cap is wholly
    headroom), `logs_retention_days` (staging 7d / prod 90d).
    Outputs pass through the edge module's outputs unchanged.
    Cutover runbook in `docs/staging-cutover.md`. (Apone)
- **`docs/staging-cutover.md` — staging cutover runbook.** New
    doc covering the green-field bootstrap (terraform init →
    plan → apply), the smoke test (DNS resolution → ACM cert
    health → WAF metric publication → ALB health → `/health`
    200), the rollback (DNS NS delegation revert to the prior
    apex), and the prod-promotion criteria (one quarter of
    staging soak + zero unexpected WAF block events at `COUNT`
    + RTO within edge SLO). Six sections including the
    `terraform plan` → `terraform apply` ordering caveat (ACM
    cert validation depends on the Route53 NS delegation taking
    effect, so the apply must be staged: zone first, then ACM
    + DNS-01 validation, then WAF + ALB + ALIAS records). (Apone)
- **`.github/workflows/pre-commit-check.yml` — CI pre-commit
    gate.** New workflow running `pre-commit run --all-files`
    on PRs against `main` + on pushes to bringup branches. The
    W7 hooks (`signer-identity-invariant` + the standard
    `check-yaml` / `end-of-file-fixer` / `trailing-whitespace`
    / `check-merge-conflict` / `check-added-large-files` set)
    now fail CI on drift — a `git commit --no-verify` workaround
    on a developer machine no longer reaches `main`. Caches
    `~/.cache/pre-commit` keyed off the config file SHA so a
    re-run is ~5s instead of ~45s for the cold case. Documented
    in `docs/signer-identity-invariant.md` §5.2. (Apone)
- **`scripts/check_signer_identity.py` — path-confusion
    presence-check guard.** New `PATH_CONFUSION_GUARDS` tuple +
    `_check_path_confusion_guards()` function added to the W7
    invariant script. The W7 incident root cause was an early
    draft locating the prod kyverno enforce patch at
    `infra/k8s/policies/kyverno-enforce-patch.yaml` instead of
    the canonical `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`,
    silently passing the regex check (the file at the wrong path
    didn't exist, so the regex extractor matched zero strings
    and "succeeded"). The W8 guard fails loudly if the wrong-
    path file ever reappears AND emits a remediation pointer.
    Negative-test confirmed (creating the wrong-path file fails
    the script with a clear message). Documented in
    `docs/admission-policy.md` §6.6. (Apone)
- **`docs/admission-policy.md` §6.6 + §7.1 update — canonical
    file paths.** New §6.6 codifying the canonical path for the
    prod enforce-patch and the rationale for the W8 presence-
    check guard. §7.1 expanded to list all six tracked surfaces
    (W7 doc listed three; the six-file invariant guard tracks
    six, and the doc + the guard MUST agree). (Apone)
- **`.github/workflows/mobile-production-release.yml` — App
    Store + Play Production promotion.** New env-gated, tag-
    driven (`mobile-prod-v*.*.*`) workflow promoting the most-
    recent External Testing build to App Store + Play
    Production. iOS: `fastlane deliver --submit_for_review
    --automatic_release` to the App Store Connect production
    surface (triggers Apple App Review). Android: `fastlane
    supply --track production --rollout` with staged-rollout
    fraction input (default 10%, operator-tunable 1-100%).
    Required GitHub Environment: `release-channel-production`
    (manual reviewer gate). Tag validation: the workflow rejects
    a `mobile-prod-v*` tag unless a matching `mobile-v*` (W7
    Internal Testing) tag exists — ensures the promoted build
    has been through Internal → External → Production, not
    direct Production. Soft-fails on missing secrets (fork PRs).
    Documented in `docs/mobile-release.md` §7. (Apone)
- **`helm/mahjong/templates/canary-deployment.yaml` — Argo
    Rollouts canary template.** New umbrella-level template
    rendering an Argo Rollouts `Rollout` CRD + an
    `AnalysisTemplate` CRD + stable/canary Services when
    `canary.enabled = true`. Co-existence guard: if both
    `api.enabled` and `canary.enabled` are true, the template
    `{{ fail }}`s with a remediation message (the two would
    fight over the same pod-template selector and produce
    flapping replicas) UNLESS the explicit
    `canary.coexistWithDeployment` escape is set (staging-only,
    for the cut-over window where the operator wants to soak
    the Rollout alongside the existing Deployment). Canary
    progression: 5% → 20% → 50% → 100% with `pause: { duration }`
    + `analysis` between each step. AnalysisTemplate runs a
    Prometheus query (`sum(rate(http_requests_total{code!~"5.."}))
    / sum(rate(http_requests_total))`) with a 95%-success
    threshold, 30s interval, 5 consecutive successes required,
    1 failure aborts. Replica-based canary (no service mesh
    dependency); nginx-canary traffic-split documented as the
    upgrade path in `docs/helm-charts.md` §3.5. Argo Rollouts
    chosen over Flagger because the `Rollout` CRD is a drop-in
    for `Deployment` (no service-mesh dependency) AND same
    vendor as the future Argo CD adoption. (Apone)
- **`helm/mahjong/values.yaml` — canary values surface.** New
    `canary:` section (~85 lines) with `enabled` (default
    false), `coexistWithDeployment` (default false; staging
    escape only), `revisionHistoryLimit`, `scaleDownDelaySeconds`,
    `steps[]` (the 5/20/50/100% progression), `metricEndpoint`
    (Prometheus URL), `analysis` (interval, threshold,
    successCount, failureLimit). Defaults are W8-baseline tuned;
    overrides in `values-staging.yaml` / `values-prod.yaml` (no
    overrides ship in W8 — canary stays staging-opt-in until
    the W9 prod canary gate). (Apone)
- **`docs/helm-charts.md` §3 — Canary deploys.** New 6-subsection
    section covering the W8 canary architecture (3.1 Why Argo
    Rollouts; 3.2 Values surface; 3.3 Step semantics; 3.4
    Co-existence guard; 3.5 Operator runbook including the
    nginx-canary traffic-split upgrade path; 3.6 cross-
    references). Existing §3–§7 renumbered to §4–§8. (Apone)
- **`.github/workflows/dr-rehearsal.yml` — quarterly DR
    rehearsal automation.** New `workflow_dispatch`-only
    workflow walking the §4.1–§4.4 manual runbook end-to-end:
    reads `primary_health_check_id` + `failover_record_fqdn`
    from the W6 DR env's Terraform outputs, captures BEFORE-
    state DNS, inverts the health check, polls until the
    secondary region is observed (records RTO), smoke-tests
    `/health` (records HTTP code + latency), reads
    `AWS/RDS::ReplicaLag` peak over the last 5 min (RPO proxy),
    holds failover for `restore_after_seconds` (default 300),
    un-inverts, polls until primary returns (records recovery
    time), generates `docs/dr-rehearsal-results-YYYY-Q#.md`
    matching the §4.5 schema, uploads as workflow artefact + a
    step-summary block, posts a Slack notification. `dry_run`
    input skips the actual invert (validates the workflow
    plumbing without traffic redirection). Concurrency-locked
    on `group: dr-rehearsal` to prevent a second rehearsal
    racing the recovery. The destructive rehearsal (§4.3 —
    `promote-read-replica`) stays manual — it is a once-a-year
    event with replacement-replica re-provisioning. Documented
    in `docs/terraform.md` §4.6. (Apone)
- **`docs/retro-2026-07.md` — July 2026 monthly retro.** New
    Phase K Wave 8 retro covering the seven W8 deliverables,
    the W7 → W8 carry-over items (mobile prod, helm canary),
    what worked (CI pre-commit catching a tomb-stone path on a
    follow-up commit), what didn't (the kyverno-path bug should
    have been caught at W7 — the W8 presence-check guard
    closes that hole), and W8 → W9 action items. (Apone)

### Changed (Phase K Wave 8 — PR pending)
- **`.pre-commit-config.yaml` — header note.** Comment updated
    to reflect W8 CI parity (the local hooks are now also CI-
    enforced via `.github/workflows/pre-commit-check.yml`). The
    hook list itself is unchanged. (Apone)
- **`docs/signer-identity-invariant.md` §5 — split into 5.1 /
    5.2 / 5.3.** §5.1 covers the local pre-commit install (W7
    content, unchanged); §5.2 documents the W8 CI parity gate;
    §5.3 documents the failure-triage flow for either path. The
    rotation procedure stays at §4. (Apone)
- **`docs/mobile-release.md` — Production track section.** New
    §7 "Production track promotion (Phase K Wave 8)" inserted
    with 8 subsections (tag space, pre-flight, cut+promote,
    workflow dispatch, staged rollout, env approval, rollback,
    cross-references). Existing §7–§9 renumbered to §8–§10. (Apone)
- **`docs/terraform.md` — DR §4.6 + staging §5.6.** New §4.6
    documents the DR rehearsal automation workflow (trigger,
    inputs, result-file contract, the destructive-rehearsal
    carve-out). New §5.6 documents the staging env's edge
    module instantiation + the `COUNT` → `BLOCK` flip plan.
    Cross-references appended to §6. (Apone)
- **`helm/mahjong/values-{staging,prod}.yaml` — §3 → §5
    cross-reference fixups.** Pre-existing comments cited
    `docs/helm-charts.md §3` for parity; the W8 §3 insertion
    pushed parity to §5. Comments updated to the new section
    numbers. (Apone)

### Fixed (Phase K Wave 8 — PR pending)
- **kyverno-enforce-patch canonical-path drift.** The W7 doc
    surface (`docs/admission-policy.md` §6) referenced the prod
    enforce-patch at `infra/k8s/policies/kyverno-enforce-patch.yaml`
    in two places; the canonical path is
    `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`. The
    canonical path is correct (it's the path the prod overlay's
    `kustomization.yaml` resolves AND the path the W7 invariant
    script registered as the fifth surface), but the doc drift
    risked a follow-up commit landing a duplicate file at the
    wrong path that would silently match-zero in the invariant
    script. Fixed by (a) updating the doc to the canonical
    path, (b) adding the W8 path-confusion presence-check
    guard to the invariant script so a wrong-path file fails
    loudly if it ever reappears. (Apone)

### Carry-forward to Wave 9
- Prod-canary gate (W8 ships staging-only; W9 turns canary on
    for prod after a quarter of staging soak).
- Staging WAF `COUNT` → `BLOCK` flip after a quarter of soak.
- `mobile-production-hotfix` workflow (the W8 prod workflow is
    happy-path; a hotfix bypass — skip Internal+External, go
    Internal→Production with a release-channel-hotfix env gate
    — is a W9 item).
- Promote some of the `values-{staging,prod}.yaml` cross-
    references to symbolic anchors (the §3→§5 drift cycle will
    recur).

## [0.16.0] — Phase K Wave 7 — 2026-06-11 (PR pending)

**Theme:** Helm chart-of-charts (umbrella + three subcharts; parity
with the Kustomize tree) + edge Terraform module (Route53 / ACM /
WAFv2 / opt-in CloudFront) + GHCR→ECR signature-preserving mirror
workflow + mobile External-Testing promotion workflow (TestFlight
External + Play Closed Testing) + six-file signer-identity
invariant pre-commit hook + RS256 JWT SSM provisioning (separate
ExternalSecret on prod + staging overlays, `Auth__JwtRsaKeys__N`
env-var binding).

### Added (Phase K Wave 7 — PR pending)
- **Helm chart-of-charts `helm/mahjong/`.** New umbrella chart
    wrapping three subcharts (`mahjong-api`, `mahjong-coturn`,
    `mahjong-postgres-sidecar`) with `alias:` wired on each
    dependency so umbrella `values.yaml` short keys (`api`,
    `coturn`, `postgresSidecar`) route to the subcharts (the
    quirk: without aliases, helm routes umbrella values to
    subcharts by chart NAME, so the W7 initial render had
    PVCs in prod despite `api.persistence.enabled: false`).
    Three values files: `values.yaml` (umbrella defaults),
    `values-staging.yaml`, `values-prod.yaml`. Renders byte-
    equivalent (modulo name prefixes) to the existing Kustomize
    overlays; parity matrix in `docs/helm-charts.md` §4 + W7
    acceptance gate (`helm lint` + `helm template` → yaml
    safe_load_all). Pre-rollout migration `Job` uses helm's
    `helm.sh/hook: pre-upgrade,pre-install` (the Kustomize path
    runs migrations out-of-band via the operator runbook).
    Coturn subchart ships the W6 prod shape (HMAC mode,
    NetworkPolicy admitting the IANA ephemeral relay range,
    AZ-spread podAntiAffinity, ExternalSecret for `mahjong-coturn-secret`).
    (Apone)
- **Terraform `modules/edge/` — Route53 + ACM + WAFv2 + opt-in
    CloudFront.** New reusable edge module provisioning the
    public-facing surface: Route53 hosted zone, regional ACM
    + us-east-1 ACM (CloudFront constraint), WAFv2 REGIONAL +
    CLOUDFRONT ACLs with managed rule groups + per-IP rate
    limit (W7 baseline 1000/5min), S3 logs bucket with the
    AWS-required `aws-waf-logs-*` prefix, Athena workgroup
    over those logs, opt-in CloudFront distribution
    (`cloudfront = null` to skip — staging runs Route53+ACM+
    WAFv2 against the ALB only; prod adds CloudFront), apex
    Route53 ALIAS records. Provider alias `aws.us_east_1`
    required by callers via `configuration_aliases` (same
    pattern as `dr-replication/`'s us-west-2 alias). Validators
    on `domain_name` (lowercase FQDN), `waf_rate_limit_per_5min`
    (100–20 000 000), `logs_retention_days` (7–3653),
    `cloudfront.price_class`. Standalone `terraform validate`
    requires a test rig (modules with `configuration_aliases`
    cannot validate without a caller); validation pattern
    documented in `docs/terraform.md` §5.4. (Apone)
- **`.github/workflows/mirror-ghcr-to-ecr.yml` — signature-preserving
    GHCR→ECR mirror.** New tag-driven workflow mirroring the
    canonical GHCR image to ECR for in-region EKS pull. Uses
    `crane copy` for the manifest (registry-to-registry HTTP-only;
    no gzip re-encoding, so destination digest = source digest)
    and `cosign copy` for the `.sig` + `.att` sidecars (cosign
    signature + SLSA attestations). The workflow verifies
    destination digest equality against source, then re-verifies
    the cosign signature with the canonical signer-identity
    regex at the destination registry. Required secrets:
    `AWS_ECR_MIRROR_ROLE_ARN` (the W6 OIDC role), `AWS_ECR_REGION`,
    `AWS_ECR_REPOSITORY`. Interplay with the W6 DR replication:
    primary ECR mirror lands in us-east-1; W6 account-level
    replication carries it to us-west-2 asynchronously. Documented
    in `docs/ghcr-to-ecr-mirror.md`. (Apone)
- **`.github/workflows/mobile-external-testing.yml` — External
    Testing promotion.** New operator-driven `workflow_dispatch`-only
    workflow promoting the most-recent Internal Testing build to
    External Testing on both Apple + Google distribution
    surfaces. `fastlane pilot distribute --build_number latest
    --distribute_external true --notify_external_testers true`
    for TestFlight External Groups (triggers Apple Beta App
    Review on the first External build of a new version,
    ~24 h turnaround). `fastlane supply --track internal
    --track_promote_to <DEST>` promotes the Play Internal
    Testing build to the operator-selected Closed Testing
    track without re-uploading the AAB (`(packageName,
    versionCode)` uniqueness prevents re-upload). Required
    inputs: `tag` (`mobile-vX.Y.Z`), `release_notes` (≤4000
    chars). Optional: `ios_external_groups`, `android_track`,
    `release_status` (`draft` / `completed` / `inProgress`).
    Soft-fails on missing secrets (fork PRs cannot access them).
    Slack notification at job tail. Documented in
    `docs/mobile-release.md` §4a. (Apone)
- **`scripts/check_signer_identity.py` — six-file signer-identity
    invariant guard.** New Python pre-commit hook + standalone
    CLI script that extracts the cosign signer-identity regex
    from six tracked files, normalises the escaping convention
    (unquoted YAML / double-quoted YAML / fenced doc block),
    and compares each to a canonical value declared in the
    script. Six tracked files: `.github/workflows/sign-image.yml`,
    `.github/workflows/verify-signature.yml`,
    `.github/workflows/slsa-provenance.yml` (W7 marker added),
    `infra/k8s/policies/kyverno-cosign-verify.yaml`,
    `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`,
    `docs/slsa-provenance.md` (W7 §4a section added). Path
    divergence from the W7 spec: the fifth surface lives in
    `infra/k8s/overlays/prod/`, not `infra/k8s/policies/`.
    Wired via `.pre-commit-config.yaml` (`always_run: true,
    pass_filenames: false` — drift is a cross-file property,
    so the hook ignores staged-file scoping). Drift-detection
    smoke test passes. Rotation procedure in
    `docs/signer-identity-invariant.md`. (Apone)
- **`infra/k8s/overlays/{prod,staging}/jwt-rsa-keys-secret.yaml`
    — RS256 JWT SSM provisioning.** New ExternalSecret manifests
    mounting RS256 PEM-encoded private keys from SSM
    (`/mahjong/{env}/auth/jwt/rsa-{active,previous,archive}`)
    into a dedicated Secret (`mahjong-jwt-rsa-keys` /
    `mahjong-jwt-rsa-keys-staging`) with the env-var keys
    `auth__jwtrsakeys__{0,1,2}` (binding to Bishop's W7
    `Auth.JwtRsaKeys` array). Deliberately separate from the
    W4 `mahjong-jwt-keys` HS256 Secret — HS256 + RS256 differ
    in cryptographic shape (opaque bytes vs PEM) and rotation
    cadence (HS256 30-day vs RS256 90-day), so independent
    Secrets keep rotation surfaces from entangling. Both
    overlays' `kustomization.yaml` patched to add a new
    `envFrom` mount (`optional: true` — deployment starts
    before RSA bootstrap). Staging adds the manifest to
    `resources:`; prod stays out-of-band (mirroring the W4
    operational asymmetry). Documented in `docs/jwt-rotation.md`
    §8.3 (updated to match the actual W7 wiring — the prior
    text described a different shape). (Apone)
- **`.pre-commit-config.yaml` — pre-commit local hooks.** New
    local hook config wiring `signer-identity-invariant` plus
    the standard `pre-commit-hooks` set (`check-yaml`,
    `end-of-file-fixer`, `trailing-whitespace`, `check-merge-conflict`,
    `check-added-large-files`). `check-yaml` excludes helm
    templates + kyverno manifests (they use templating tags
    PyYAML can't safe-load). CI parity (`pre-commit run
    --all-files` in a workflow) is a W8 follow-up. (Apone)
- **`docs/helm-charts.md` — Wave 7 helm reference.** New doc
    covering chart layout, the alias quirk, install order,
    helm-vs-Kustomize decision matrix, parity matrix, subchart
    toggles, and the pre-merge verification gate. (Apone)
- **`docs/signer-identity-invariant.md` — invariant + rotation
    procedure.** New doc explaining why the regex MUST stay in
    lock-step across six files, the W5 incident that motivated
    the guard (~25 min outage of the scheduled image-rescan
    alerting), and the coordinated-rotation procedure. (Apone)
- **`docs/ghcr-to-ecr-mirror.md` — Wave 7 mirror reference.**
    New doc covering why naive `docker pull` + `docker push`
    breaks signatures (gzip re-encoding produces different
    layer digests), the `crane copy` + `cosign copy`
    primitives, when not to mirror, the ECR-unreachable
    fallback flow, and DR replication interplay. (Apone)
- **`.github/workflows/slsa-provenance.yml` — env-block W7
    marker.** New `EXPECTED_IDENTITY_REGEXP` env entry
    documenting the canonical signer-identity regex so this
    workflow participates in the six-file invariant guard.
    Marker is non-functional (the workflow's cosign attest
    invocations already use OIDC keyless signing under the
    same identity); the env entry is the hook-friendly anchor.
    (Apone)
- **`docs/slsa-provenance.md` §4a — Signer-identity invariant
    section.** New documentation surface added BETWEEN existing
    §4 (slsa-verifier procedure) and §5 (verify-failure
    semantics) — explains that slsa-verifier pins source URI
    but NOT signer identity, references the W7 six-file
    invariant, and reproduces the canonical regex in a fenced
    code block so the pre-commit hook can verify the doc
    surface too. (Apone)

### Changed (Phase K Wave 7 — PR pending)
- **`.github/workflows/slsa-provenance.yml` — env-block extended.**
    Added the W7 `EXPECTED_IDENTITY_REGEXP` marker entry
    (non-functional; six-file invariant participation only).
- **`docs/slsa-provenance.md` — §4a inserted.** Existing §5
    onward retain their numbering; §4a is a NEW subsection
    after §4. Six-file invariant cross-reference added.
- **`docs/jwt-rotation.md` §8.3 + §9 — RS256 ESO mount + cross-refs.**
    §8.3 rewritten to describe the actual W7 wiring (separate
    `mahjong-jwt-rsa-keys` ExternalSecret + new `envFrom`
    patch on both overlays, NOT extension of the W4
    `mahjong-jwt-keys` Secret as the prior text claimed).
    §9 cross-refs extended to point at the new ExternalSecret
    manifests.
- **`docs/terraform.md` — §5 Edge module inserted.** New §5
    (renumbering existing "Cross-references" to §6) covering
    the W7 edge module: what it builds, validators, usage,
    standalone-validation caveat (`configuration_aliases`
    incompatibility with `terraform validate`), interplay
    with `dr-replication`.
- **`docs/mobile-release.md` — §4a + §5.2 + §6 + §9 updated.**
    New §4a "External testing flow" inserted between §4 and
    §5 (existing numbering preserved). §5.2 "Promote to
    external (Phase L)" rewritten to point at the W7
    workflow ("Wave 7 — automated"). §6's "Closed Testing"
    row updated to reference the W7 workflow. §9
    cross-references extended.
- **`infra/k8s/overlays/{prod,staging}/kustomization.yaml` —
    RSA envFrom mounts.** Both overlays gain an additional
    `envFrom` patch for the W7 RSA Secret. Order: HS256
    mount first, RSA mount second (so the W7 RS256 binding
    can co-exist with the W4 HS256 binding during cutover).
- **Wave 6 [0.15.0] state: now PR #52 (merged at 1c67878).**
    Updated the §0.15.0 heading from "PR pending" to
    "PR #52". W6 prod-deploy snapshot is now archived.

### Notes (Phase K Wave 7)
- **Six-file signer-identity invariant — path divergence from the
    spec.** The user's W7 task spec listed
    `infra/k8s/policies/kyverno-enforce-patch.yaml` as the fifth
    surface; the actual path is
    `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` (the
    enforce patch lives in the prod overlay, not under
    `policies/`). The check script uses the real path.
- **`slsa-provenance.yml` + `docs/slsa-provenance.md` invariant
    participation.** Neither file previously carried the regex
    literal; W7 added a non-functional env marker
    (`EXPECTED_IDENTITY_REGEXP`) to the workflow + a §4a
    documentation section to the doc, so all six surfaces
    participate in the guard. The non-functional marker MAY be
    promoted to a live cosign-verify call in W8 if the SLSA
    flow ever needs an independent signature gate.
- **Helm chart-of-charts is parallel to Kustomize, not a
    replacement.** Both paths ship in this repo, both render the
    same Deployment / Service / etc. CI deploy path stays on
    Kustomize (`docs/production-deployment-runbook.md`); helm is
    for operator-driven point-installs + partner deploys.
- **`mobile-external-testing.yml` first-run gotcha.** The FIRST
    External Testing distribution of a new iOS version triggers
    Apple's Beta App Review (~24 h); re-triggering the workflow
    cannot cancel a review in flight. Operator-driven dispatch
    is required to gate this — auto-promotion on every tag is
    intentionally NOT shipped.
- **GHCR→ECR mirror = best-effort, not release-blocker.** A
    mirror failure is treated as an ECR-side outage signal;
    the canonical image is still on GHCR and EKS can pull from
    there (longer latency, but pods start). Re-run the mirror
    via `workflow_dispatch` once ECR recovers.
- **Edge module — standalone `terraform validate` caveat.** The
    module declares `configuration_aliases = [aws.us_east_1]`,
    which prevents standalone validation (same as
    `dr-replication/`). Use the test-rig pattern in
    `docs/terraform.md` §5.4 to validate in isolation.

## [0.15.0] — Phase K Wave 6 — 2026-06-04 (PR #52)

**Theme:** Multi-region DR (us-east-1 → us-west-2 warm pair) +
IAM least-privilege hardening + production coturn k8s data plane
+ Trivy severity-tuned gate (HIGH+CRITICAL block, 30-day
allowlist) + tag-driven mobile internal-testing promotion to
TestFlight + Play Console + SLSA-verifier pre-merge gate on
deploy:prod PRs.

### Added (Phase K Wave 6 — PR pending)
- **Terraform `modules/dr-replication/` — cross-region DR module.**
    New reusable module instantiated by the secondary-region env
    (`envs/dr-us-west-2/`). Wires three cross-region resources
    onto the existing single-region stack: (1) RDS Postgres
    cross-region read replica (`replicate_source_db` = primary
    ARN, replica encrypted with secondary-region KMS — AWS forbids
    cross-region CMK sharing, so the secondary env's `main.tf`
    provisions its own CMK; backup retention 7d so a promoted
    replica is immediately backup-protected; deletion-protection
    on by default for DR-prod), (2) account-level ECR replication
    rule (PREFIX_MATCH filter scoped to `mahjong-autotable` repo;
    typical replication lag 1-5 min; secondary-region ECR
    repository auto-created on first replication event — no
    pre-creation needed), (3) Route 53 PRIMARY + SECONDARY
    failover records sharing one FQDN + an HTTPS health check
    against the primary's `/health` (Bishop's endpoint). Module
    pins TTL<60s via a variable validator (W6 invariant — clients
    must pick up failover within ≈2 min). Two AWS provider
    aliases (`aws.primary` + `aws.secondary`) so every resource
    is explicitly placed; no default-provider fall-through. Six
    outputs documented for the rehearsal runbook
    (`replica_db_arn`, `primary_health_check_id`,
    `failover_record_fqdn`, …). (Apone)
- **Terraform `envs/dr-us-west-2/` — DR env stack.** New
    secondary-region (us-west-2) Terraform stack. VPC CIDR pinned
    to **10.1.0.0/16** (non-overlapping with the primary's
    10.0.0.0/16 — future VPC peering / Transit Gateway works
    without renumbering). Three private subnets across us-west-2's
    first three AZs (no public subnets in DR-warm — ingress
    lands when a promotion fires). Provisions the secondary-region
    DB subnet group + SG + KMS key, then instantiates the
    `modules/dr-replication` module passing both provider aliases.
    Reads primary stack outputs via `terraform_remote_state` so
    the primary DB ARN + KMS ARN don't have to be hand-plumbed.
    Backend bootstrap follows the same chicken-and-egg pattern
    as the primary — `backend.example.hcl` + the runbook in
    `docs/terraform.md` §2. (Apone)
- **Terraform `modules/github-oidc/` — reusable OIDC module +
    least-privilege grants.** New module replacing the inline
    W5-style grants for future envs (the primary env's flat
    `iam-github-oidc.tf` is also W6-narrowed in place). `ecr:*`
    narrowed to the eight discrete actions a `docker push`
    actually invokes (push-only; no `Describe*`/`List*`) scoped
    to the repository ARN. `ssm:Get*` narrowed to `ssm:GetParameter`
    only on `parameter/mahjong/<env>/*` (drops GetParameterHistory
    which leaks rotation history; drops DescribeParameters which
    leaks parameter names = org-structure intel). `iam:PassRole`
    introduced as an opt-in dynamic block guarded by
    `iam:PassedToService` (W5 had no PassRole; W6 adds the
    grant in fenced form so future widenings can't be a silent
    privilege-escalation vector). Companion `least-privilege.tf`
    documents per-action rationale (no resources/policies — pure
    documentation that lives next to the policy it audits). The
    `least-privilege.tf` + `main.tf` files are W6 lock-step:
    ANY policy widening MUST update the rationale in the SAME
    commit. (Apone)
- **`infra/k8s/base/coturn-{deployment,configmap,secret}.yaml` —
    production coturn data plane.** Three new k8s manifests
    deploying coturn 4.6 as a 2-replica AZ-spread Deployment
    behind an NLB Service, with HMAC-mode authentication
    (`use-auth-secret` + `lt-cred-mech`) using the
    `coturn-static-auth-secret` ExternalSecret (sourced from
    SSM `/mahjong/<env>/turn/auth_secret`). Bishop's W3
    `/api/turn` endpoint shares the same HMAC key so credential
    minting + validation work symmetrically; one rotation
    rolls both sides. `coturn-configmap.yaml` pins
    `listening-port=3478`, `tls-listening-port=5349`,
    `fingerprint`, `min-port=49152`, `max-port=65535` (IANA
    ephemeral range) + drops `lt-cred-mech`/`no-cli`/`no-loopback-peers`
    hardening + 1080 quota cap. A new `NetworkPolicy
    coturn-relay-ports` admits the relay range (49152-65535 UDP)
    + the three control-plane ports (3478 UDP+TCP, 5349 TCP);
    egress wide-open (a TURN server's job is to NAT-traverse to
    arbitrary peers). Pod-level security: `runAsNonRoot=true`,
    `runAsUser=998`, `readOnlyRootFilesystem=true`, `capabilities
    drop ALL`. RollingUpdate pinned `maxSurge=1, maxUnavailable=0`
    so refreshes always spin a fresh pod first. NLB annotations
    + `externalTrafficPolicy: Local` preserve the client source
    IP (coturn needs it to mint relay candidates). The W2
    single-replica `turn-server.yaml` stays in place for staging;
    the W6 `coturn-*` resources land alongside in prod (parallel
    names — `coturn-*` not `turn-server-*` — so the cutover is
    operator-driven blue-green). (Apone)
- **`.github/workflows/mobile-internal-testing.yml` — tag-driven
    TestFlight + Play Internal promotion.** New workflow,
    triggers on `mobile-v*.*.*` tags. Five-job shape: `prepare`
    (tag regex validation + version extraction) → `build-web-bundle`
    (npm ci + npm run build of the autotable frontend that the
    Capacitor shell wraps) → `android` (gradle bundleRelease
    SIGNED + fastlane supply → Play Internal Testing,
    `release_status: draft` so the operator gates the
    promotion-to-testers click) → `ios` (CocoaPods + gym +
    pilot SIGNED via App Store Connect API key → TestFlight) →
    `notify` (Slack webhook). Code-signing secrets soft-fail
    (fork PRs without secrets log a warning and skip the upload
    job; operator-driven tag pushes from main always have them).
    Ephemeral keychain provisioned per run for iOS cert import;
    Provisioning Profile UUID auto-extracted from the
    `.mobileprovision` plist + installed at the canonical macOS
    path. Companion `docs/mobile-release.md` (NEW) covers the
    full release-flow diagram, signing-identity setup runbook
    (App Store Connect API key, distribution `.p12`,
    provisioning profile, Play keystore, Play service-account
    JSON, Slack webhook), TestFlight + Play tester-management
    runbook, and a troubleshooting table. (Apone)
- **`.github/workflows/verify-slsa-on-deploy.yml` — pre-merge
    SLSA verification gate on `deploy:prod` PRs.** New workflow,
    label-gated. Installs `slsa-verifier` v2.6.0 (the SAME binary
    the admission webhook bundles for in-cluster verification),
    resolves the image digest from `infra/k8s/overlays/prod/kustomization.yaml`'s
    `images:` block, runs `slsa-verifier verify-image
    <image>@<digest> --source-uri github.com/long2know/mahjong-autotable
    --print-provenance > slsa-provenance.json`. Sticky PR
    comment communicates the pass/fail to reviewers without
    needing to drill into the Actions tab; the verified
    predicate JSON uploads as a workflow artefact (30-day
    retention). Belt-AND-suspenders for the Wave-5 Kyverno
    `attestations:` block: the same predicate is verified at
    CI time AND at admission time; a regression in either layer
    is caught by the other. `docs/slsa-provenance.md` §7a (NEW)
    documents the two-layer model + the `slsa-verifier` binary's
    role inside the admission webhook container. (Apone)
- **`.github/trivy-allowlist.yaml` (NEW) + container-scan
    threshold tuning.** PR gate tightened from W3's CRITICAL-only
    to HIGH+CRITICAL (the W6 block-merge floor); daily cron
    relaxed to full-severity sweep (LOW+MEDIUM+HIGH+CRITICAL) +
    non-blocking — visibility, not gating. New CVE allowlist
    file with W6-invariant schema: every entry MUST carry
    `id` + `justification` + `added` + `expires`; expiry capped
    at 30 days (`allowlist-check` job fails the workflow if an
    entry's expiry is in the past OR more than 30d in the
    future). Trivy's native `.trivyignore` is rendered from the
    YAML allowlist at scan time so we get human-readable
    justification + Trivy-native suppression in one source of
    truth. Wave-6 ships with the allowlist EMPTY — `allowed: []`
    — establishing the schema baseline. (Apone)
- **`docs/terraform.md` (NEW).** Cross-module reference covering
    the W5+W6 module layout (`infra/terraform/` flat primary +
    `modules/dr-replication/` + `modules/github-oidc/` +
    `envs/dr-us-west-2/`), the apply-order rule (primary stack
    first; DR reads primary via `terraform_remote_state`), the
    W6 OIDC narrowing summary table, AND **§4 "DR rehearsal"**
    — quarterly drill runbook: pre-flight checks (replica
    replication-status confirmation, ECR image-delivery
    confirmation, Route 53 health-check status), the
    non-destructive failover (invert the Route 53 health check
    via `aws route53 update-health-check --inverted`; ~90s
    propagation × 30s TTL = ≈2 min total failover time), the
    DESTRUCTIVE annual full-rehearsal (`aws rds promote-read-replica`
    — one-way; replica must be re-provisioned via terraform
    after), the restore step (un-invert the health check), and
    the post-rehearsal report template (time-to-DNS-cut,
    time-to-200-from-secondary, anomalies). 5-min total
    failover SLO documented. (Apone)
- **`docs/retro-2026-05.md` (NEW).** May 2026 monthly retro —
    what shipped (W5 SLSA+SBOM unified predicate + Kyverno
    attestations + Terraform bootstrap + W6 DR + OIDC narrowing
    + coturn k8s + …), what's WIP (Bishop's W6 backend lane,
    Hicks's frontend, the test-gate ascent past 1345), lessons
    learned (the W5 `.git/config` race incident — Apone's
    `b346157` absorbed Hicks's frontend work because a concurrent
    agent rewrote `.git/config` between `git config user.name`
    and the `git commit`; **W6 mitigation pattern**: per-invocation
    `git -c user.name=… -c user.email=… commit` ONLY, never the
    stateful `git config` form). Establishes the monthly retro
    cadence + template for future months. (Apone)

### Changed (Phase K Wave 6 — PR pending)
- **`infra/terraform/iam-github-oidc.tf` — narrowed in place.**
    The primary env's inline OIDC role policy now matches the
    `modules/github-oidc/` shape (push-only ECR verbs scoped to
    the repo ARN, `ssm:GetParameter` only on the per-env path,
    opt-in PassRole). `variables.tf` gains the two new
    `passrole_target_roles` / `passrole_target_services`
    variables. Apply-time: `terraform plan -var-file=prod.tfvars`
    will show DELETIONS of the W5-broad statements + ADDITIONS
    of the W6-narrowed ones — review carefully before apply.
    Outputs unchanged (the role ARN is stable; only the inline
    policy changes). (Apone)
- **`.github/workflows/container-scan.yml` — threshold tuning.**
    PR/push runs default to HIGH+CRITICAL (was CRITICAL-only in
    W3); cron runs default to full-severity sweep (LOW+MEDIUM+HIGH+CRITICAL)
    with the gate step non-blocking. New `allowlist-check` job
    runs FIRST + fails the workflow on expired allowlist entries.
    Sticky PR comment + STEP_SUMMARY tables updated to show LOW
    counts. Trivy gating + JSON + SARIF passes all consume the
    rendered `.trivyignore` so the YAML allowlist becomes the
    single source of truth. (Apone)
- **`docs/turn-server-setup.md` — §9 "k8s deployment" (NEW).**
    Documents the W6 production-shape coturn manifests
    (`infra/k8s/base/coturn-*.yaml`), the differences from the
    W2 single-replica `turn-server.yaml` (AZ-spread, HMAC mode by
    default, wider relay port range, NetworkPolicy, NLB
    annotations, readOnlyRootFilesystem), the apply runbook (SSM
    seed → kubectl apply -k → verify two pods in different AZs
    → smoke-test with turnutils_uclient), and the cutover
    procedure (the W2 resources stay for staging; the W6
    resources land in parallel in prod; W2 prod resources
    decommissioned after a 24h cool-down). (Apone)
- **`docs/slsa-provenance.md` — §7a (NEW).** Documents the
    `slsa-verifier` v2 binary's role inside the admission
    webhook container (second-pass verification beyond Kyverno's
    cosign-via-policy integration; defends against a future
    Kyverno or cosign upstream regression) AND the W6
    `verify-slsa-on-deploy.yml` pre-merge gate (same binary,
    same predicate, same source URI verified at BOTH CI time
    AND admission time). (Apone)

### Notes (Phase K Wave 6 — PR pending)
- **W5 git-config race incident (lessons learned in W6).**
    Apone's W5 `b346157` accidentally absorbed Hicks's frontend
    work because the Wave-5 `commit-tree` recovery used the
    stateful `git config user.name "Apone (DevOps)"` form, and
    a concurrent agent rewrote `.git/config` to its own identity
    between the `git config` call and the `git commit`. The
    commit landed under the WRONG author. **W6 mitigation**:
    every commit in this wave uses `git -c user.name="Apone (DevOps)"
    -c user.email="apone@squad.mahjong" commit -m …` (atomic
    per-invocation override; no time window where the config
    state can be raced). All git operations wrapped in `flock`
    on a shared lock file so two agents cannot run a
    commit+push pair concurrently. The pattern is documented in
    `docs/retro-2026-05.md` as a permanent reference + in
    `.squad/agents/apone/history.md` Wave-6 entry. (Apone)
- **Backend gate preserved.** This wave's scope is pure DevOps
    + docs + infra (`src/**` untouched). The W5 1345/0/0 backend
    gate carries forward; `dotnet test` not re-run. (Apone)
- **Lock-step invariant updates.** The signer-identity invariant
    in `docs/admission-policy.md` §7.1 (now SIX files since W5)
    is not touched in W6 because the SLSA workflow + Kyverno
    policy + image digest list are unchanged. The OIDC policy +
    its rationale comment in `modules/github-oidc/least-privilege.tf`
    is a NEW lock-step pair: ANY widening of the inline
    `github_deploy_inline` policy MUST land alongside an
    updated rationale paragraph in `least-privilege.tf`. (Apone)
- **DR rehearsal SLO.** 5-min total failover time
    (health-check trip → DNS resolver cache flush → first
    successful `/health` 200 from us-west-2). Documented in
    `docs/terraform.md` §4.5; reported in the May 2026 retro;
    re-reported every quarter at the rehearsal cadence. (Apone)

## [0.14.0] — Phase K Wave 5 — 2026-05-28 (PR pending)

**Theme:** Supply-chain ring-5 (unified provenance+SBOM
multi-subject predicate; Kyverno requires the SLSA attestation
alongside the cosign signature) + staging brought to parity with
prod on JWT-keys ESO data plane + retroactive secrets-history
sweep workflow (closing the historical-commit blind spot of the
W4 PR-diff scanner) + automated HSTS preload-readiness probe
with sticky-issue alerting + Terraform bootstrap module for
"fresh prod env in <30 min" (VPC + EKS + RDS + ECR + GitHub
OIDC), unblocking the Wave-6 DR rehearsal target.

### Added (Phase K Wave 5 — PR pending)
- **Unified SLSA L3 in-toto provenance + SBOM under a single
    multi-subject predicate.** Rewrote
    `.github/workflows/slsa-provenance.yml` to invoke the
    GENERIC SLSA generator
    (`slsa-framework/slsa-github-generator/.github/workflows/generator_generic_slsa3.yml@v2.0.0`)
    in place of the container-specific Wave-4 generator. New
    pipeline shape: `resolve-digest` (unchanged) → `build-sbom`
    (Syft against the published image; computes the
    base64-encoded sha256sum-format subjects list with TWO
    subjects: image manifest digest + CycloneDX SBOM file
    digest) → `provenance` (multi-subject generic generator
    producing a single `provenance-and-sbom.intoto.jsonl`) →
    `attest-oci` (`cosign attest --type slsaprovenance1` so the
    Wave-5 Kyverno `attestations:` block discovers the
    predicate via standard OCI-sidecar lookup) →
    `attach-to-release` (uploads both the predicate AND the
    SBOM as Release assets atomically on tag pushes). Auditors
    now have ONE Sigstore-signed statement that
    cryptographically binds the image and the SBOM to the SAME
    build run, not two parallel attestation flows requiring
    cross-trust. Wave-4 attestations remain in Rekor and
    remain verifiable with the Wave-4 invocation. Verification
    + migration runbook updated at `docs/slsa-provenance.md` §6
    (`slsa-verifier verify-artifact` against the SBOM subject,
    `verify-image` against the image subject — both pass
    against the same predicate). (Apone)
- **Kyverno `attestations:` block requiring the SLSA-v1 predicate.**
    Extended `infra/k8s/policies/kyverno-cosign-verify.yaml` to
    add an `attestations:` block alongside the existing
    `attestors:` clause. Admission now requires BOTH a cosign
    keyless signature from this repo's `sign-image.yml`
    workflow AND a SLSA-v1 provenance predicate produced by
    this repo's Wave-5 `slsa-provenance.yml`. The
    `conditions:` block pins three CEL-evaluated values
    against the decoded predicate:
    `buildDefinition.externalParameters.workflow.repository`,
    `buildDefinition.externalParameters.workflow.path`, and
    `runDetails.builder.id` (regex). Attestors-in-attestations
    re-asserts the subject pin to the `slsa-github-generator`
    reusable workflow's URL — belt-AND-suspenders. Operator
    runbook + negative test + rollback procedure documented at
    `docs/admission-policy.md` §6 (NEW Wave-5 section).
    (Apone)
- **Staging overlay `mahjong-jwt-keys-staging` ExternalSecret.**
    New `infra/k8s/overlays/staging/jwt-keys-secret.yaml` —
    staging-equivalent of the Wave-4 prod
    `mahjong-jwt-keys` ESO surface. Same shape (three
    rotation-state-named SSM SecureString parameters,
    `auth__jwtsigningkeys__{0,1,2}` env-var KEYS feeding
    Bishop's `Auth.JwtSigningKeys` array binding,
    15-min refresh interval) targeting
    `/mahjong/staging/auth/jwt/key-{active,previous,archive}`
    via `aws-secrets-manager-staging` ClusterSecretStore. Wired
    into `infra/k8s/overlays/staging/kustomization.yaml` as
    both a `resources:` entry AND an `envFrom` deployment
    patch (mirrors prod). Closes the Wave-4 handoff item that
    left staging falling back to the omnibus's singular
    `Auth__JwtSigningKey` — staging now exercises the
    array-binding code path so Bishop's
    `jwt-rotation-smoke.sh` can hard-assert the multi-key
    fallback against staging too. (Apone)
- **`secrets-history-sweep.yml` workflow + runbook.** New
    `.github/workflows/secrets-history-sweep.yml` —
    `workflow_dispatch`-only retroactive `gitleaks detect`
    sweep over the full commit graph from any ref (default
    `main`). SARIF uploaded to Code Scanning under a
    DISTINCT category (`secrets-history-sweep`) so findings
    don't overlay the W4 `gitleaks` category. SARIF + log
    also uploaded as workflow artefact for offline triage
    (90-day retention). Closes the W4 PR-diff scanner's
    historical-commit blind spot. Operator runbook +
    rotate-then-purge procedure + per-secret-class rotation
    table + force-push history-rewrite (`git filter-repo`)
    procedure at NEW `docs/secrets-scanning.md`. (Apone)
- **`hsts-readiness-check.yml` workflow + sticky-issue alerting.**
    New `.github/workflows/hsts-readiness-check.yml` —
    daily 13:00 UTC cron + `workflow_dispatch`. `curl -I`s
    the production origin and asserts the response includes
    EXACTLY `Strict-Transport-Security: max-age=63072000;
    includeSubDomains; preload`. On failure: opens (or updates)
    a sticky GitHub issue with the observed value, expected
    value, triage steps, and workflow-run link; on recovery,
    auto-closes the issue with a recovery comment. The probe
    is the early-warning system both BEFORE the manual
    submission to <https://hstspreload.org/> (Stephen action;
     14-day all-green-runs gate) and AFTER (a post-submission
    regression is a P0 with a 6-week-removal cost). Probe URL
    overridable via repo variable `HSTS_PROBE_URL` or
    dispatch input. `docs/hsts-preload.md` §3a (NEW) covers
    the operator runbook. (Apone)
- **`infra/terraform/` bootstrap module (NEW directory).**
    Bare-minimum Terraform module to provision a Mahjong stack
    in a fresh AWS account: 1 × VPC (10.0.0.0/16, 3 public +
    3 private subnets across 3 AZs; per-AZ NAT in prod, single
    NAT in staging; S3 gateway endpoint), 1 × EKS cluster
    (1.30; managed node group with mixed-instance Spot
    fallback; CoreDNS + kube-proxy + VPC-CNI + EBS-CSI addons;
    IRSA OIDC enabled; secret-encryption KMS key), 1 × RDS
    Postgres (db.t4g.small staging / db.t4g.medium prod;
    gp3 auto-scaling 20→100 GB; encrypted; multi-AZ in prod;
    deletion protection in prod; auto-generated 32-char
    master password surfaced as sensitive terraform output for
    operator-driven SSM seeding), 1 × ECR repository
    (image-scan-on-push; lifecycle policy keeping last 30
    tagged images + expiring untagged after 14 days), and 1
    GitHub-Actions OIDC IAM role (`mahjong-${env}-github-deploy`)
    with the trust policy scoped to this repo + main / `v*` /
    `environment:${env}` subjects. Per-environment tfvars
    (`staging.tfvars`, `prod.tfvars`). State backend stanza
    intentionally empty so `terraform init` consumes
    `backend-${env}.hcl` per-env. Quick-start, total-time
    budget (~27-32 min apply), post-bootstrap helm install
    sequence (ESO, AWS-LBC, cert-manager, Kyverno), ECR mirror
    procedure, and teardown steps at NEW
    `infra/terraform/README.md`. Validates clean against
    `terraform validate` v1.9.8. Unblocks the Wave-6
    DR-rehearsal acceptance criterion "<30 min to spin up a
    clean prod env". (Apone)

### Changed (Phase K Wave 5 — PR pending)
- **`.github/workflows/sbom.yml` header annotation.** Clarified
    the workflow's relationship to the new unified SLSA
    predicate: this workflow continues to OWN the PR-time CVE
    gate (Trivy CRITICAL,HIGH + SARIF → Code Scanning) and the
    per-PR dependency-graph SBOM; the SIGNED, AUDITOR-VERIFIABLE
    SBOM for every release artefact now lives in
    `slsa-provenance.yml` as part of the multi-subject
    predicate. The Wave-5 unified predicate is the canonical
    source of truth for "what shipped"; this workflow remains
    the PR-blocking CVE layer. (Apone)
- **`docs/slsa-provenance.md` §6.** Rewrote the "Bumping the
    SLSA generator version" section to cover both the v2.0.0
    pin maintenance AND the Wave-4 → Wave-5 generator
    migration (container-specific → generic generator;
    single-subject → multi-subject predicate; backward
    compatibility for Wave-4 artefacts in Rekor). (Apone)
- **`docs/admission-policy.md` §6.** Renumbered + expanded
    to cover the Wave-5 SLSA-attestation requirement.
    NEW §6.1 (Wave-5 SLSA attestation), §6.2 (negative test
    for image-without-predicate), §6.3 (rollback procedure
    if the SLSA workflow flakes during an emergency hotfix);
    §6.4 / §6.5 preserve the Wave-3/4 observability
    content. (Apone)
- **`docs/hsts-preload.md` §3.** Tightened the submission
    pre-condition: 14 consecutive green runs of
    `hsts-readiness-check.yml` are now the gate before
    clicking submit (in addition to the existing 14-day
    pre-submission dry-run). Added §3a covering the new
    daily probe + sticky-issue alerting. (Apone)
- **`infra/k8s/overlays/staging/kustomization.yaml`.** Added
    `jwt-keys-secret.yaml` to `resources:` and an `envFrom`
    deployment patch mounting `mahjong-jwt-keys-staging`
    (`optional: true` so a fresh staging cluster without ESO
    bootstrapped still starts via the omnibus fallback). Same
    JSON-patch shape as the Wave-4 prod overlay. (Apone)

### Notes (Phase K Wave 5)
- **Backend gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
    --nologo` baseline preserved at 1232 / 0 / 0 (Wave-5
    scope is pure DevOps + docs + infra; `src/backend/**`
    source code untouched).
- **Five-layer supply-chain enforcement** (workflow → release-gate
    → admission-signature → admission-attestation → SLSA
    provenance). The canonical signer-identity regex stays as
    the cross-layer invariant — any rename of `sign-image.yml`
    OR `slsa-provenance.yml` is now a SIX-file coordinated
    change (`sign-image.yml`, `verify-signature.yml`,
    `kyverno-cosign-verify.yaml` `attestors:` + `attestations:`
    blocks, `kyverno-enforce-patch.yaml`, and the
    `--source-uri` arg in `docs/slsa-provenance.md` §4).
- **Pattern lock — multi-subject in-toto predicates.** Future
    artefact classes (release-notes blob, runtime config blob,
    helm chart `tgz`) can be added as additional subjects to
    the same Wave-5 predicate without changing the generator
    invocation — just append a line to the
    `sha256sum`-formatted subjects list in `build-sbom`. ONE
    predicate per build, MANY subjects.
- **Pattern lock — Wave-N+1 staging-mirror policy.** Any new
    prod-only data-plane that ships in wave N (e.g. Wave-4's
    prod `jwt-keys-secret.yaml`) MUST be mirrored to staging
    in wave N+1 (Wave-5's staging counterpart) so the
    rotation-rehearsal surface stays one wave behind the prod
    surface, not 5 waves behind.
- **Pattern lock — sticky-issue alerting on probe workflows.**
    The HSTS readiness probe's sticky-issue mechanism is the
    template for future cron-driven health checks (e.g. the
    proposed JWT-rotation soak in Wave-6+); search by exact
    issue-title string for idempotent open/update/close
    semantics. Avoids the duplicate-issue spam common with
    naïve `gh issue create`-on-failure patterns.

## [0.13.0] — Phase K Wave 4 — 2026-05-27 (PR pending)

**Theme:** Supply-chain ring-4 (SLSA provenance) + zero-touch JWT
key rotation (ESO) + Kyverno enforce hard-pin + HSTS preload +
in-repo secrets scanning. Wave 4 closes the Wave-3 "future" list:
SLSA in-toto predicates land as the fourth supply-chain ring on
top of cosign signatures + verify gates + SBOM signing + Kyverno
admission; the `Auth.JwtSigningKeys` array binding (W3 schema)
now has its production ESO data plane; Kyverno prod gets a
fail-safe second policy that cannot be downgraded by a misedit
of the global default; HSTS preload header lands on the prod
Ingress for the manual submission to https://hstspreload.org/;
and `gitleaks` joins GitGuardian as the in-repo secrets-scan
layer.

### Added (Phase K Wave 4 — PR pending)
- **SLSA Level 3 in-toto provenance for every published image.**
    New `.github/workflows/slsa-provenance.yml` triggers on
    push-to-main, `v*.*.*` tag pushes, and workflow_dispatch.
    Resolves the manifest-list digest the same way `sign-image.yml`
    does, then calls the official `slsa-framework/slsa-github-generator/.github/workflows/generator_container_slsa3.yml@v2.0.0`
    reusable workflow to produce an in-toto-shaped provenance
    predicate signed via GitHub OIDC + Sigstore Fulcio, recorded
    in Rekor, AND attached to the OCI registry as a sidecar
    artefact. On tag pushes, the `attach-to-release` job
    additionally uploads the bundle to the matching GitHub
    Release as `provenance.intoto.jsonl`. Operator + auditor
    verification runbook (`slsa-verifier` CLI usage, decoded
    predicate shape, failure-mode triage, generator bump
    procedure) at `docs/slsa-provenance.md`. (Apone)
- **ESO `mahjong-jwt-keys` ExternalSecret for the W3 `Auth.JwtSigningKeys` array.**
    New `infra/k8s/overlays/prod/jwt-keys-secret.yaml` —
    SEPARATE `ExternalSecret` (distinct from the omnibus
    `mahjong-autotable` secret) materialising three indexed env
    vars (`auth__jwtsigningkeys__{0,1,2}`) from three
    rotation-state-named SSM SecureString parameters
    (`/mahjong/prod/auth/jwt/key-{active,previous,archive}`).
    The 15-minute `refreshInterval` is tighter than the omnibus
    1 h so emergency JWT rotations propagate within minutes. The
    prod kustomization mounts the resulting Secret via
    `envFrom: { secretRef: { name: mahjong-jwt-keys, optional: true } }`
    so Bishop's W4/W5 code-side binding picks up the array
    automatically once it lands. `docs/jwt-rotation.md` §1 +
    §3 + §4 + §7 rewritten to reflect the
    rotation-state-named SSM convention (the operator never has
    to compute "which numeric index holds value X today?"). (Apone)
- **Kyverno prod hard-pin `ClusterPolicy`.** New
    `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` adds a
    SECOND cluster policy (`enforce-prod-mahjong-images`) scoped
    exclusively to `mahjong-prod`, with
    `validationFailureAction: Enforce` and the same canonical
    `sign-image.yml` signer-identity regex as the Wave-3 default.
    Acts as a fail-safe alongside the Wave-3 policy: a misedit of
    the Wave-3 per-namespace override cannot accidentally let
    unsigned images into prod. Multiple policies on the same
    image just compose (both must verify before admission).
    `docs/admission-policy.md` §5.3 (NEW) codifies the
    end-to-end canary procedure (build unsigned image → deploy to
    staging: ADMIT + warn → deploy to prod: REJECT). (Apone)
- **HSTS preload header on prod Ingress.** New
    `infra/k8s/overlays/prod/hsts-patch.yaml` sets
    `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`
    on the production origin via nginx-ingress
    `configuration-snippet`. `force-ssl-redirect: true` and
    `ssl-redirect: true` are also pinned here so a global
    ConfigMap edit cannot weaken prod inadvertently. Manual
    submission runbook at `docs/hsts-preload.md` (NEW) — the
    chromium HSTS preload list is operator-driven, not
    CI-automated; the doc covers prerequisites, the
    2-week pre-submission dry-run, the
    https://hstspreload.org/ form-submission flow, and the
    post-submission monitoring + removal procedure. (Apone)
- **`gitleaks` secrets-scanning workflow.** New
    `.github/workflows/secrets-scan.yml` runs gitleaks on every
    PR + push to `main` + nightly cron (03:00 UTC, offset from
    container-scan's 04:00). HIGH-confidence findings fail the
    gate; SARIF uploaded to GitHub Code Scanning under category
    `gitleaks` (distinct from Trivy's `trivy-container-scan` and
    `trivy-image`). Coexists with the README-recommended
    GitGuardian app as defense-in-depth — two layers, two failure
    modes, same `report and block` floor. Concurrency-grouped on
    `secrets-scan-${{ github.ref }}` so PR refreshes cancel
    in-flight prior runs. (Apone)
- **`docs/slsa-provenance.md` + `docs/hsts-preload.md` (NEW).**
    Operator + auditor runbooks for the two new external-touching
    surfaces (`slsa-verifier` CLI usage; chromium HSTS preload
    submission). (Apone)

### Changed (Phase K Wave 4)
- `infra/k8s/overlays/prod/kustomization.yaml`: now lists
    `kyverno-enforce-patch.yaml` as a resource AND uses
    `patches: [- target: Ingress, path: hsts-patch.yaml]` to apply
    the HSTS strategic-merge AND adds a JSON-patch that appends
    `secretRef: { name: mahjong-jwt-keys, optional: true }` to
    the deployment's `envFrom` list. (Apone)
- `docs/jwt-rotation.md` §1: rewritten to document the Wave-4
    `mahjong-jwt-keys` ESO and the rotation-state-named SSM
    parameters. §3 + §4 rotation runbook commands updated to use
    `aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-*`
    instead of the prior index-shaped pattern. §5 emergency
    rotation updated likewise. §7 migration table updated:
    Apone W4 row marked complete; W6 row dropped (work landed
    in W4). (Apone)
- `docs/admission-policy.md`: new §5.3 covers the Wave-4
    canary procedure (staging ADMIT-with-warn → prod REJECT).

### Notes (Phase K Wave 4)
- **Backend gate untouched.** Wave-4 DevOps scope is pure
    workflow + infra + docs — no `src/**` edits. The
    1152/0/0 backend baseline from Wave-3 is preserved.
- **No `git add -A`.** Selective adds only:
    `.github/workflows/{slsa-provenance,secrets-scan}.yml`,
    `infra/k8s/overlays/prod/{jwt-keys-secret,kyverno-enforce-patch,hsts-patch,kustomization}.yaml`,
    `docs/{slsa-provenance,hsts-preload,jwt-rotation,admission-policy}.md`,
    `CHANGELOG.md`, `.squad/decisions/inbox/apone-phase-k-wave-4.md`,
    `.squad/agents/apone/history.md`.
- **Out-of-scope / DO NOT STAGE this wave:**
    `.copilot/skills/error-recovery/`, `.github/workflows/squad-*.yml`,
    `.tool-actionlint/`, `.work/`.

## [0.12.0] — Phase K Wave 3 — 2026-05-26 (PR #49)

**Theme:** Supply-chain hardening + zero-downtime auth rotation +
TURN-over-TLS. Wave 3 closes the three Wave-2 "future Phase K wave"
handoff items in one go: Kyverno admission policy for cosign
enforcement, `Auth:JwtSigningKeys` fallback list, and the deferred
`turns:` TLS listener. Also adds container-scan PR gate +
pre-publish SBOM signature verification + nightly JWT-rotation
smoke + PWA-asset presence gate.

### Added (Phase K Wave 3 — PR #49)
- **Kyverno cosign admission policy.**
    `infra/k8s/policies/kyverno-cosign-verify.yaml` —
    `ClusterPolicy` named `verify-mahjong-images` REFUSES to admit
    any Pod / Deployment / StatefulSet / DaemonSet / Job / CronJob
    whose `image:` field matches
    `ghcr.io/long2know/mahjong-autotable:*` unless the image
    carries a valid cosign keyless signature whose Fulcio cert was
    issued to this repo's `sign-image.yml` workflow on `main` or
    `v*.*.*`, with Rekor entry verifying. Action mode is per-
    namespace: **Enforce** in `mahjong-prod`, **Audit** in
    `mahjong-staging` (and globally for any new namespace —
    fail-safe default). `mutateDigest: true` rewrites tags to
    digests post-verify so a pod is pinned to the exact attested
    bits. `failurePolicy: Fail` blocks new rollouts on Sigstore
    outage (existing pods keep running). Closes the Wave-1/-2
    "verify enforced ONLY in CI" gap — admission-layer
    enforcement now refuses unsigned images at the cluster
    boundary. Operator runbook + Kyverno Helm install +
    positive/negative test instructions in
    `docs/admission-policy.md`. (Apone)
- **`Auth:JwtSigningKeys` fallback-list schema.**
    `appsettings.json` ships a new `Auth.JwtSigningKeys: []`
    forward-compat array (with `//` documentation key explaining
    `[0]` = active signer, `[1..N]` = previous keys accepted for
    validation). Closes the Wave-1/-2 "Wave-9 fallback-key list
    (planned)" carry-over. `docs/jwt-rotation.md` (NEW) covers the
    full lifecycle: schema, code-side contract (Bishop's W4/W5
    deliverable), rotation cadence (annual, 30-day grace; emergency
    immediate), SSM-shift rotation procedure, smoke validation
    via `tests/smoke/jwt-rotation-smoke.sh` (NEW — boots image
    with key0 → mints token → restarts with keys[0]=key1 +
    keys[1]=key0 → asserts old token still validates AND new
    tokens signed under key1), and the wave-by-wave migration
    path. Smoke is FORWARD-COMPATIBLE — soft-passes when
    `/api/auth/token` / `/api/auth/validate` return 404 (until
    Bishop's binding lands), matching the established `pwa-smoke`
    / `csp-report-smoke` / `chat-flow-smoke` shape. (Apone)
- **TLS for `turns:` on port 5349.**
    `infra/k8s/base/turn-server.yaml` now passes
    `--cert /etc/tls/tls.crt --pkey /etc/tls/tls.key` to coturn
    and mounts a new `tls` volume from a `tls-cert-turn` Secret.
    New `infra/k8s/overlays/prod/turn-tls-secret.yaml` ships an
    `ExternalSecret` bound to `aws-secrets-manager-prod` that
    materialises the Secret (`type: kubernetes.io/tls`) from SSM
    parameters `/mahjong/prod/turn/tls/{crt,key}`. Closes the
    Wave-2 "Phase L follow-up" deferral (corporate firewalls
    blocking plain `:3478` UDP/TCP can now negotiate via
    `turns:` on 5349). Operator runbook updated in
    `docs/turn-server-setup.md` §1.4 (cert provisioning via
    cert-manager+LE or ACM, SSM upload, rotation cadence). (Apone)
- **Container-scan PR gate + nightly cron.**
    `.github/workflows/container-scan.yml` — Trivy image scan on
    EVERY PR (no path filter — CRITICAL CVEs published against
    indirect deps MUST surface even on a touch-nothing PR) + push
    on `main` + nightly cron (04:00 UTC, offset from
    `sbom.yml`'s Monday-09:00 cadence). Hard-gates on CRITICAL by
    default; configurable to HIGH / MEDIUM via
    `workflow_dispatch` input for triage reruns. SARIF uploaded
    to GitHub Code Scanning (`category: trivy-container-scan` —
    distinct from `sbom.yml`'s `trivy-image` so findings don't
    overlay). Sticky PR comment via
    `marocchino/sticky-pull-request-comment@v2` (header
    `container-scan`) with CRITICAL+HIGH+MEDIUM counts and gate
    verdict — reviewers see the latest scan result inline without
    conversation noise on rerun. Coexists with `sbom.yml` (SBOM-
    focused, CRITICAL+HIGH gate, weekly cron) — two workflows,
    distinct purposes. (Apone)
- **SBOM signed by cosign + verified in pre-publish gate.**
    `release.yml` adds a new `verify-sbom` job between
    `verify-signature` and `release`. Generates an SPDX SBOM from
    the EXACT digest-qualified image just smoke-tested + signature-
    verified, signs the SBOM with cosign keyless OIDC
    (`sign-blob --output-signature sbom.spdx.json.sig
    --output-certificate sbom.spdx.json.pem`), then verifies the
    signature with `cosign verify-blob --certificate-identity-regexp
    "…/release.yml@refs/tags/v*"`. Block-release on missing /
    invalid signature. The signed SBOM bundle (json + sig + cert)
    is attached as artefacts to the workflow AND as assets on the
    GitHub Release page so downstream auditors can pull all three
    without re-running CI. Closes the Wave-1/-2 "SBOM generated
    but not signed" gap. (Apone)
- **PWA-asset presence gate in `docker-smoke.yml`.** New step
    builds the production image and runs
    `docker run --rm <image> sh -c 'ls /frontend/autotable/{sw.js,manifest.webmanifest,manifest-precache.json}'`
    — HARD-FAILS if any of the three Wave-3 PWA artefacts Hicks
    is shipping aren't in the runtime tree. Coexists with the
    Wave-2 `pwa-smoke.yml` (exercises the SW lifecycle in
    chromium); this gate is the per-file-presence floor that
    catches the case where the SW JS shipped but the precache
    manifest didn't. (Apone)
- **JWT-rotation smoke wired into `docker-smoke.yml`.** Same
    nightly cadence as the other smoke scripts; soft-passes
    today, auto-tightens to a hard assertion when Bishop's
    `/api/auth/{token,validate}` surface ships in W4/W5. (Apone)
- **`docs/admission-policy.md` + `docs/jwt-rotation.md` (NEW).**
    Operator runbooks for the two big new policy surfaces. (Apone)

### Changed (Phase K Wave 3)
- `release.yml`: new `verify-sbom` job between `verify-signature`
    and `release`; `release` job's `needs:` is now `[smoke,
    verify-signature, verify-sbom]`; `release` step attaches the
    signed SBOM bundle as Release assets; permissions unchanged
    on the existing jobs (the new job adds `id-token: write` for
    keyless OIDC). (Apone)
- `infra/k8s/base/turn-server.yaml`: coturn args extended with
    `--cert/--pkey`; new `tls` volume mounting the `tls-cert-turn`
    Secret at `/etc/tls/`. (Apone)
- `src/backend/src/Mahjong.Autotable.Api/appsettings.json`: new
    top-level `Auth.JwtSigningKeys: []` array (forward-compat
    schema; Bishop binds in W4/W5). (Apone)
- `docs/turn-server-setup.md` §1.4: rewritten from "Phase L
    follow-up" placeholder to operator-actionable cert-
    provisioning + rotation runbook. (Apone)

## [0.11.0] — Phase K Waves 1 + 2 — 2026-05-25 (PRs #47 + #48)

**Theme:** Production bring-up. Wave 1 (PR #47) shipped supply-chain
signing, nightly load regression alerting, multi-arch post-merge smoke,
CSP-strict rollout coordination, secret-rotation runbook. Wave 2
(PR #48) shipped PR-time multi-arch runtime gate, TURN/STUN k8s
overlay, Capacitor mobile shell scaffold, PWA service-worker smoke,
Microsoft OAuth production secret docs, and a reusable cosign verify
workflow wired into `release.yml` as a pre-publish gate.

K1 was a bring-up wave that did not advance the version cursor (the
preamble's "Phase K opens at 0.10.0" convention); K2 is the first
K-wave to bump minor. Both waves ship under the same release tag.

### Added (Phase K Wave 1 — PR #47)
- **cosign keyless image signing.** `.github/workflows/sign-image.yml`
    fires on `docker-build` workflow success on `main` (and on
    `v*.*.*` tag pushes). Uses GitHub OIDC as the keyless signing
    identity (`id-token: write`), resolves the manifest-list digest,
    signs via Sigstore Fulcio, records the signature in Rekor, and
    immediately verifies with `cosign verify --certificate-identity-regexp …`.
    Documented in `docs/image-signing.md` (full verification runbook
    for operators + auditors). (Apone)
- **Nightly load-test cron.** `.github/workflows/load-test-nightly.yml`
    runs daily at 02:00 UTC: brings up the production-shaped
    docker-compose stack, waits for `/health`, runs
    `tests/load/lobby-flood.js` via the new
    `tests/load/run-and-compare.sh` wrapper, appends a row to
    `docs/load-test-results-history.md`, and ALERTS (email +
    Sentry event) if any workload's p99 latency regresses by >25 %
    vs the prior recorded run. Threshold + duration tunable via
    workflow_dispatch inputs. (Apone)
- **Multi-arch runtime smoke.** `.github/workflows/multi-arch-smoke.yml`
    runs after `docker-build` succeeds on `main`. Matrix: `linux/amd64`
    natively + `linux/arm64` via QEMU. Per-arch smoke checks: `/health`
    200 with the four-field shape, `POST /api/identity` mints a
    cookie, `GET /api/auth/providers` registers (forward-compat
    soft-pass on 404), `POST /api/csp-report` returns 204, and the
    runtime CSP header honours `Security:CspStrictStyles=true`
    (no `'unsafe-inline'` in `style-src`). (Apone)
- **CSP-report endpoint smoke.** `tests/smoke/csp-report-smoke.sh`
    posts a synthetic violation in BOTH the legacy
    `application/csp-report` and modern `application/reports+json`
    envelopes and confirms DB persistence by tailing the runtime's
    structured `CSP violation` warn log line. (Apone)
- **Secret-rotation runbook.** `docs/secret-rotation.md` covering OAuth
    client secrets (Google + GitHub — quarterly), DB connection
    strings (annual), Sentry DSN (compromise-only), reconnect-token
    signing key + magic-link signing key (never — rotation invalidates
    all live sessions). Cross-references ESO/Vault/AWS-Secrets-Manager
    flows from Wave 5/6 docs. (Apone)

### Added (Phase K Wave 2 — PR #48)
- **PR-time multi-arch runtime gate.**
    `.github/workflows/multi-arch-runtime.yml` runs on every PR
    (paths-filtered to `Dockerfile`, `src/backend/**`,
    `src/frontend/autotable-src/**`, the workflow itself) plus pushes
    on `main`. Builds the multi-stage Dockerfile for `linux/amd64`
    (native) + `linux/arm64` (QEMU) independently, loads each per-arch
    image into the local Docker daemon
    (`docker buildx build --output type=docker`),
    `docker run --platform=<p>`, then curls `/health` and asserts
    200 + `"status":"healthy"`. Posts a sticky PR comment
    (header: `multi-arch-runtime`) with the matrix verdict so
    reviewers see arch-specific breakage BEFORE merge. Complements
    Wave 1's post-merge `multi-arch-smoke.yml`. (Apone)
- **TURN / STUN k8s overlay.** `infra/k8s/base/turn-server.yaml`
    deploys `coturn/coturn:4.6` as a Deployment + LoadBalancer Service
    + ConfigMap + ExternalSecret stub. The base manifest ships
    deliberately-broken stub credentials (`mahjong/local/turn/*`
    SSM family — does not exist) so an accidental
    `kubectl apply -k base/` against a real cluster fails fast.
    Dedicated overlay at `infra/k8s/overlays/turn/` (Kustomize)
    fills in the realm + external-ip placeholders and repoints the
    ExternalSecret at the real `aws-secrets-manager-prod`
    ClusterSecretStore + `/mahjong/prod/turn/*` SSM key family. Twin
    convenience templates at
    `infra/k8s/overlays/{prod,staging}/turn-server-patch.yaml`
    + `turnserver-{prod,staging}.conf` for env-specific tuning.
    Operator runbook at `docs/turn-server-setup.md`. (Apone)
- **Capacitor mobile shell.** New `mobile/` top-level directory
    (Capacitor 6.1.x): `package.json` + `capacitor.config.json`
    (`appId: io.mahjong.autotable`, `webDir: ../src/frontend/autotable`)
    + operator runbook (`mobile/README.md`). New
    `.github/workflows/mobile-build.yml` — builds the web bundle once,
    then independent `android` (ubuntu, gradlew
    assembleRelease+bundleRelease) + `ios` (macos, xcodebuild Release
    `CODE_SIGNING_ALLOWED=NO`) jobs produce unsigned artefacts; a
    `release` job creates a `mobile-<run_number>` GitHub prerelease
    with both attached on pushes to `main`. App-store submission +
    signing identities are operator action. `.gitignore` excludes
    `mobile/{ios,android,node_modules,build,.gradle}` since
    `npx cap add` regenerates them deterministically. (Apone)
- **PWA service-worker smoke.** `tests/smoke/pwa-smoke.{sh,js}` —
    Playwright (chromium-only) Node probe that boots the production
    image on port 18093, navigates to `/`, checks `/sw.js`
    (soft-pass on 404 — forward-compat for Hicks's still-in-flight
    SW artefact), waits for
    `navigator.serviceWorker.getRegistration()` to yield an activated
    worker, then reloads and asserts
    `navigator.serviceWorker.controller != null` (the SW-took-control
    canonical assertion). Workflow at
    `.github/workflows/pwa-smoke.yml` (paths-filtered to the PWA
    surface + smoke files + Dockerfile). (Apone)
- **OAuth production setup runbook (Google + GitHub + Microsoft).**
    `docs/oauth-production-setup.md` — operator-facing playbook for
    provisioning OAuth client IDs/secrets in each of the three
    providers, mapping them to the canonical SSM key families
    (`/mahjong/prod/oauth/{google,github,microsoft}/{client_id,client_secret[,tenant_id]}`),
    quarterly rotation procedure, post-rotation validation
    checklist, Microsoft-specific quirks (`oid` claim is the
    stable PK; `tid=9188040d-…` distinguishes personal MSA
    accounts; `email` scope required for the `mail` claim on
    consumer accounts). Microsoft section unblocks Bishop's
    Wave 3 OAuth middleware. (Apone)
- **Cosign verify reusable workflow + pre-publish gate.**
    `.github/workflows/verify-signature.yml` — reusable
    `workflow_call` interface with `image-digest` (required),
    `expected-issuer` + `expected-identity-pattern` + `cosign-version`
    (defaults pinned to this repo's `sign-image.yml`). Wired into
    `release.yml` as a new `verify-signature` job between `smoke`
    (which now exposes the manifest-list digest as an output) and
    `release` — the release tag's GitHub Release is NOT cut for an
    unsigned image. Single source of truth for the expected-identity
    regex + cosign version pin; callers tomorrow (Argo CD pre-sync
    gates, Kyverno k8s admission policies) dial in via the same
    reusable. (Apone)

### Changed (Phase K Wave 2)
- `release.yml`: `smoke` job exposes new `outputs.image-digest`
    (resolved via `docker buildx imagetools inspect --format
    '{{.Manifest.Digest}}'`); new `verify-signature` job calls
    `./.github/workflows/verify-signature.yml`; `release` job's
    `needs:` is now `[smoke, verify-signature]`. Existing
    `permissions: contents: write, packages: read` covers the new
    job (no changes needed). (Apone)
- `.gitignore`: excludes Capacitor's regenerated platform
    directories (`mobile/{ios,android,node_modules,build,.gradle,*.tgz}`).
    (Apone)

## [0.10.0] — Phase J Wave 10 — 2026-05-24 (PR #46)

**Theme:** Final-pass polish — flake fixes, CSP Round 2 canary knob,
production runbook, end-to-end load test, multi-arch Docker image,
docs review. Phase J's tenth-and-final wave; J ships green at
**820/0/0** backend tests, zero-skip streak preserved.

### Added
- **Multi-arch Docker image (`linux/amd64` + `linux/arm64`).** Wave 4
    carry-over closed. `.github/workflows/docker-build.yml` adds
    `docker/setup-qemu-action@v3`, `PLATFORMS: linux/amd64,linux/arm64`
    env, and passes `platforms:` through to `docker/build-push-action@v6`.
    Manifest list digest surfaced in the workflow summary. Docs:
    `docs/docker.md` Wave-10 multi-arch section; `docs/sbom.md`
    cross-reference. (Apone)
- **End-to-end load test harness.** `tests/load/lobby-flood.js` (Node
    + `ws@^8` — no k6 dep). Three workloads: 100-concurrent lobby
    polling, 25-concurrent WS join, 5-concurrent 4-bot tournaments.
    Smoke run results in `docs/load-test-results.md` (Lobby p99
    525 ms / Join p99 555 ms / Tournament p99 2,520 ms, 0 % error
    rate on Debug build). (Apone)
- **Production deployment runbook.** `docs/production-deployment-runbook.md`
    (~26 KB): pre-flight checklist, image build/publish, DB init via
    the pre-rollout k8s Job, rolling update + readiness gates,
    rollback procedure, monitoring/alerting (Prometheus + Sentry +
    JSON logs), incident response playbooks (DB outage, rate-limit
    storm, OAuth provider down, magic-link queue stall, CSP
    regression). (Apone)
- **Docs index.** `docs/README.md` — landing page mapping each
    operator/dev/QA need to the right doc. (Apone)
- **CSP Round 2 — `style-src 'unsafe-inline'` canary knob.**
    `Observability/SecurityHeadersMiddleware.cs` gains
    `Security:CspStrictStyles` (default OFF). When set, `style-src`
    drops `'unsafe-inline'` while adjacent directives are byte-for-byte
    preserved. Constants intentionally remain permissive (pinned by
    Vasquez's `CspStyleSrcNoUnsafeInlineTests` contract suite). (Apone)
- **Tournament mode.** Multi-table tournaments + bracket UI + per-table
    auto-bot fill at start. (Bishop)
- **Replay v2 normaliser.** Forward-compat schema upgrade for the
    `Replay` table; old single-game replays auto-migrate to the
    multi-hand envelope. (Bishop)
- **Audit-log pruning service.** Background hosted service deletes
    `AuditEntry` rows older than `Audit:RetentionDays` (default 90).
    Configurable per provider. (Bishop)
- **Bot decision reasoning surface.** Each bot's pickup/discard
    decision is surfaced via `/api/games/{id}/bot-reasoning` for
    spectator transparency. (Bishop)
- **Database health detail.** `GET /health/detail` surfaces per-provider
    DB pool stats + last-migration timestamp. (Bishop)
- **e2e Playwright multi-arch sanity test.** Stand-alone test ensures
    the multi-arch image runs to completion under QEMU. (Vasquez)

### Fixed
- **`LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates` flake** —
    `AutotableConnectionManager.GetStoredEntryCount(gameId)` aggregated
    across all `kind`s; translator `match` + `seat:N` entries inflated
    the count before Alice's `UPDATE things` ever landed. Fix: new
    `AutotableGameState.CountFor(string kind)` per-kind probe + new
    `GetStoredEntryCount(gameId, kind)` overload + `WaitForAsync` now
    THROWS on deadline expiry instead of silent-falsing. Pinned by a
    50× regression-gate test. (Apone)

### Build invariant
Backend gate: **820 / 0 / 0**. Zero-skip streak: **13 consecutive
green waves**.

## [0.9.0] — Phase J Wave 9 — 2026-05-23 (PR #45)

**Theme:** Reconnect-token rotation + table chat + i18n pattern
resources + CSP tightening + audit log + flake fix.

### Added
- **Reconnect-token rotation.** Bishop's `/api/reconnect/*` surface
    issues a new opaque token on every WS reconnect, invalidates the
    previous one server-side, and rejects reuse attacks. Persisted as
    `ReconnectToken` + `ReconnectAuditEntry`. Smoke test:
    `tests/smoke/token-rotation-smoke.sh`. (Bishop; smoke: Apone)
- **Table chat.** `POST /api/chat/send` + `GET /api/games/{id}/chat`
    + SignalR hub event. Per-route rate limit; profanity filter
    (`ChatProfanityFilter`); persisted as `ChatMessage`. Smoke test:
    `tests/smoke/chat-flow-smoke.sh`. (Bishop; smoke: Apone)
- **i18n pattern resources.** Yaku names + UI strings extracted to
    `Resources/Strings.{en,zh-CN,ja}.resx`. Frontend resource-key
    pattern in `src/frontend/autotable-src/i18n/`. (Bishop / Hicks)
- **CSP tightening.** `SecurityHeadersMiddleware` gains four operator
    knobs (`Security:CspStrict`, `Security:UseScriptNonces`,
    `Security:CspReportOnly`, `Security:CspReportUri`). Defaults
    backwards-compatible. `POST /api/csp-report` sink (`Observability/
    CspReportEndpoint.cs`) accepts legacy + Reporting-API envelopes;
    persists to `CspViolations` table (per-provider EF migration). (Apone)
- **Audit log + pre-rollout k8s migration Job.** `--migrate` CLI
    intercept in `Program.cs` runs EF migrations from a one-shot k8s
    `Job` with Argo CD `sync-wave: -1` + `hook: PreSync`. (Apone)
- **SBOM + Trivy CRITICAL/HIGH gate.** `.github/workflows/sbom.yml`:
    CycloneDX + SPDX SBOMs via `anchore/sbom-action@v0`, Trivy gate
    with `severity: CRITICAL,HIGH` + `exit-code: 1` + `ignore-unfixed:
    true`, SARIF upload to GitHub code-scanning, PR-summary comment.
    Docs: `docs/sbom.md`. (Apone)

### Fixed
- **`HotSeatSwap_PlayerToPlayer_PreservesGameState` flake** —
    tightened `bobSeated` `WaitForAsync` predicate to wait for both
    Bob's seat take AND Alice's seat release before asserting, so the
    post-take `FillEmptySeatsWithBotsAsync` doesn't race the assertion.
    Pure test-side fix. (Apone)

### Build invariant
Backend gate: **728 / 1 / 0** (one Bishop-owned profanity-filter
in-flight; resolved at Wave 10).

## [0.8.0] — Phase J Wave 8 — 2026-05-22

**Theme:** Production hardening.

### Added
- **Sentry SDK (backend + frontend).** `Sentry.AspNetCore` 6.5.0 wired
    through `Observability/SentryConfiguration.cs`; SignalR hub-method
    breadcrumbs via `SentryHubFilter`. Disabled by default — set
    `Sentry__Dsn` to enable. Frontend equivalent via `src/sentry.ts`
    + `@sentry/browser` 8.x, gated on `<meta name="sentry-dsn">`. See
    `docs/sentry.md`. (Apone)
- **Security headers middleware.** `SecurityHeadersMiddleware` stamps
    `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`,
    and a Three.js-compatible `Content-Security-Policy` on every
    response. Parcel-hashed bundles get
    `Cache-Control: public, max-age=31536000, immutable`; index.html
    gets `no-cache`. (Apone)
- **Cloudflare-aware rate limiting.** `RateLimiting/RateLimitingExtensions.cs`
    now prefers `CF-Connecting-IP` over `X-Forwarded-For` when present
    so the rate limiter partitions per real client behind Cloudflare.
    (Apone)
- **Release workflow** (`.github/workflows/release.yml`) — on every
    `v*.*.*` tag push: waits for the ghcr.io image, runs the build +
    auth smoke, then creates a GitHub Release with the matching
    CHANGELOG section or auto-generated notes. (Apone)
- **Auth-flow smoke test** (`tests/smoke/auth-flow-smoke.sh`) — mints a
    `mahjong_pid` cookie via `POST /api/identity`, asserts idempotent
    refresh, probes `/api/auth/providers` and `/api/auth/me` (skips
    gracefully if the surface isn't yet registered). Wired into
    `docker-smoke.yml` nightly. (Apone)
- **Parcel + npm BuildKit cache mounts** in the Dockerfile — `npm ci`
    re-uses `/root/.npm`, parcel re-uses `/src/frontend/autotable-src/.parcel-cache`.
    CI rebuilds with no source changes drop from ~90s to ~20s. (Apone)
- **External Secrets templates** for staging/prod
    (`infra/k8s/overlays/{staging,prod}/secret-template.yaml`) — ESO
    `ExternalSecret` CRDs pointed at AWS Secrets Manager. (Apone)
- **Local dev secret generator** (`scripts/generate-dev-secrets.sh` +
    `appsettings.Development.example.json`). Idempotent; emits a
    `.env.dev` with strong random JWT/cookie keys. (Apone)
- **Docs:** `docs/sentry.md`, `docs/cloudflare.md`,
    `docs/secret-management.md`. (Apone)
- **Auth surface (preview).** OAuth (Google, GitHub), magic-link, and
    dev-login under `/api/auth/*`, plus persistence migrations for
    Sqlite / Postgres / SqlServer. (Bishop)
- **Rule presets surface (preview).** `POST /api/rule-presets` etc.
    with backend validation + frontend rule-presets pane. (Bishop)

### Build invariant
Backend gate: ≥554 tests passing. Wave 8 expanded the suite to **617
green** with the observability surface; the auth/rule-preset surface
adds further pending tests that gate Bishop's parallel work.

## [0.7.0] — Phase J Wave 7 — 2026-05-21 (PR #43)

**Theme:** Replay endpoint, accessibility, settings drawer, multi-DB,
Kubernetes overlays.

- Replay endpoint (`GET /api/replays/{gameId}`) + viewer (Bishop)
- Accessibility audit + WCAG 2.1 AA fixes (Hudson)
- Settings drawer + theme switching (Hicks)
- Multi-database support (Sqlite / Postgres / SqlServer) via
    `Persistence__Provider` (Bishop)
- k8s base manifests + staging/prod overlays (Apone)
- See `.squad/decisions/inbox/apone-phase-j-wave-7.md` for the deploy
    memo.

## [0.6.0] — Phase J Wave 6 — 2026-05-20 (PR #42)

**Theme:** Persistent player IDs + leaderboard + rate limiting + auth
UI + Playwright specs.

- `mahjong_pid` cookie minted by `POST /api/identity` (Bishop)
- Per-player leaderboard (`GET /api/leaderboard/top`) (Bishop)
- ASP.NET rate limiter: fixed-window anonymous + token-bucket api
    (Apone)
- Auth-aware UI shell (sign-in / sign-out chrome) (Hicks)
- Playwright e2e harness + first specs (Vasquez)

## [0.5.0] — Phase J Wave 5 — 2026-05-19 (PR #41)

**Theme:** Multiplayer matchmaking, profiles, stats, observability,
Playwright E2E.

- Public matchmaking lobby + Quick Match (Hicks)
- Player profile + display name + avatar color (Bishop)
- Personal stats panel (Bishop)
- Prometheus `/metrics` exposition + JSON structured logging (Apone;
    see `docs/observability.md`)
- Playwright config + first cross-browser specs (Vasquez)
- Secret audit (`docs/secrets.md`)

## [0.4.0] — Phase J Wave 4 — 2026-05-19 (PR #40)

**Theme:** Mobile responsiveness, reconnect tokens, CI hardening,
seed 40595, GameComplete reconciliation.

- Responsive layout + touch input (Hicks)
- Rejoin-token URL parameter (`?rejoin=…`) + server-side validation
    (Bishop)
- GitHub Actions: docker-build.yml, docker-smoke.yml, e2e-playwright.yml
    (Apone)
- Hand-50 seed 40595 fully passes with all rule presets (Hudson)
- GameComplete event reconciles the move-log against the server
    snapshot (Bishop)

## [0.3.0] — Phase J Wave 3 — 2026-05-18 (PR #39)

**Theme:** Docker deployment, sound, replay (foundation), WinResult
surfaces, /health.

- Multi-stage Dockerfile (parcel + dotnet publish + aspnet:10.0
    runtime; UID 1000 non-root; `/data` volume) (Apone)
- `GET /health` 4-field probe + Docker `HEALTHCHECK` (Bishop)
- Sound effects pipeline (Hicks)
- WinResult panel + move-log groundwork (Bishop)
- Replay-event recording (Bishop)

## [0.2.0] — Phase J Wave 2 — 2026-05-17 (PR #38)

**Theme:** Disconnect cleanup, N-hand game completion, UX polish.

- Disconnect cleanup: idle seats freed (Bishop)
- N-hand games (configurable hand count) with proper end-of-game
    flow (Hudson)
- "Concede" + "Resign" interactions (Hicks)

## [0.1.0] — Phase J Wave 1 — 2026-05-16 (PR #37)

**Theme:** Shanten claim gate, hot-seat swap, spectator camera lock.

- Shanten gating on Pong / Chow / Kong claims (Hudson)
- Hot-seat swap mid-game (Bishop)
- Spectator camera lock-on-table (Hicks)

## Earlier (Phases A–I) — not version-tagged

Phases A through I shipped on `main` without semver tags. Highlights:

- **Phase I** (PRs #33–#36): special-context wins (天和/地和/海底/河底/杠上开花),
    proper shanten counter, spectator/all-bots-watch mode, multi-game
    WebSocket routing, persistence hydration, result-modal pattern
    breakdown.
- **Phase H** (PRs #31–#32): V2 rules — NineTerminals, RobbingKong,
    stacked Big Wins, V2 design groundwork.
- **Phase G** (PR #30): bot pickup scheduler, sidebar lobby,
    privacy-mask cleanup.
- **Phase F** (PR #29): Changsha realism — manual pickup, variant
    switching, 3-tier bot engine.
- **Phases A–E**: initial Changsha rebuild on top of the
    `pwmarcz/autotable` engine, scoring & yaku catalogue, swap-call
    discipline, gang/chi/pong/ron implementations.

[Unreleased]: https://github.com/long2know/mahjong-autotable/compare/v0.16.0...HEAD
[0.16.0]: https://github.com/long2know/mahjong-autotable/compare/v0.15.0...v0.16.0
[0.15.0]: https://github.com/long2know/mahjong-autotable/compare/v0.14.0...v0.15.0
[0.14.0]: https://github.com/long2know/mahjong-autotable/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/long2know/mahjong-autotable/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/long2know/mahjong-autotable/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/long2know/mahjong-autotable/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/long2know/mahjong-autotable/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/long2know/mahjong-autotable/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/long2know/mahjong-autotable/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/long2know/mahjong-autotable/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/long2know/mahjong-autotable/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/long2know/mahjong-autotable/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/long2know/mahjong-autotable/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/long2know/mahjong-autotable/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/long2know/mahjong-autotable/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/long2know/mahjong-autotable/releases/tag/v0.1.0
