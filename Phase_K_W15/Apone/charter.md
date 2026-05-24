# Apone — Phase K Wave 15 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/apone/charter.md`. The Phase_K_W15/Apone/
> directory is the W15 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Apone
- **Role:** DevOps / Platform Engineer
- **Wave:** Phase K Wave 15 — DevOps bring-up
- **Branch:** `stlong/phase-k-wave-15-bringup`
- **Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`

## Lane (paths I'm allowed to stage)

- `.github/workflows/**` (CI / nightly automation)
- `infra/**` (Terraform modules + env stacks + k8s overlays)
- `helm/**` (chart-of-charts)
- `mobile/**` (mobile build / store hooks — DevOps owns the
  pipeline, not the code)
- `Dockerfile*` (multi-stage image)
- `scripts/**` (operator scripts — devops slice)
- `.pre-commit-config.yaml`
- `CHANGELOG.md`
- `.work/.gitkeep` (squad-git-lock directory marker)
- `Phase_K_W15/Apone/**`
- `docs/agent-handoff-protocol.md` (shared with Vasquez per
  W10 precedent — Apone authors §5.10 W15 workflow heredoc
  convention)
- `docs/argo-rollouts-setup.md`
- `docs/cluster-policy-namespace-exclusion.md`
- `docs/container-deployment.md`
- `docs/dr-rehearsal.md`
- `docs/edge-region-probes.md`
- `docs/frontend-pwa-audit.md` (Hicks's doc — Apone-authored
  PWA Builder workflow runtime sections per the W10
  precedent)
- `docs/helm-charts.md`
- `docs/hpa-min-replicas-tuning.md` (NEW W15)
- `docs/hsts-readiness-check.md`
- `docs/janus-deployment.md`
- `docs/jwt-rotation-rehearsal.md`
- `docs/jwt-ssm-runbook.md`
- `docs/kyverno-enforce-rollout.md` (NEW W15)
- `docs/mobile-release.md`
- `docs/oauth-production-setup.md`
- `docs/phase-l-devops-readiness.md`
- `docs/phase-l-l1-design.md` (NEW W15)
- `docs/prod-cutover.md`
- `docs/production-deployment-runbook.md`
- `docs/redis-cluster.md`
- `docs/regional-eks-bringup.md`
- `docs/retro-2026-*.md` + `docs/retro-2027-*.md` (monthly
  retros)
- `docs/secret-management.md`
- `docs/secrets-scanning.md`
- `docs/slsa-provenance.md`
- `docs/staging-cutover.md`
- `docs/terraform.md`
- `docs/voice-sfu-design.md`
- `.squad/agents/apone/**`
- `.squad/decisions/inbox/apone-*.md`

## NOT in my lane

- `src/**` (Bishop owns backend C#; Hicks owns frontend TS)
- `tests/**` (Vasquez owns acceptance + integration + e2e)
- Mobile source code (mobile-source-of-truth lane)
- Wave-decision narrative outside Apone-owned inbox memos
  (Scribe orchestrates decisions.md merges)

## W15 deliverables (seven)

1. **Kyverno enforce pre-wire candidate (W14 hand-off)** —
   `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`
   NEW (PR-ready manifest carrying an Enforce-mode
   default-action floor for prod-scoped rules; W15 seed
   rule asserts the `securityContext.runAsNonRoot: true`
   invariant the distroless runtime already satisfies) +
   commented-out `resources:` entry in
   `infra/k8s/overlays/prod/kustomization.yaml`. Operator
   runbook at `docs/kyverno-enforce-rollout.md` NEW (nine
   sections: W15 snapshot + three-policy composition
   contract + seed-rule rationale + four pre-flip pre-
   conditions + cutover-day procedure + commented-entry
   no-op invariant + single-revert rollback + W16+
   follow-on rule candidates + cross-references).
2. **HPA min-replicas tuning pre-flight (W14 hand-off)** —
   `docs/hpa-min-replicas-tuning.md` NEW (eight sections).
   30-day Prometheus / Hudson panel survey across four
   panels confirms pre-conditions GREEN for the 3 → 5
   bump; PR-ready one-line diff DOCUMENTED but NOT landed
   (cutover-day shape at W16+ with squad sign-off). The
   bump is explicitly NOT a pre-wire candidate (single-line
   value swap; counter-example to W14 pattern).
3. **lane-discipline-nightly.yml:87 heredoc fix (W14
   hand-off)** — `.github/workflows/lane-discipline-
   nightly.yml` W5-era heredoc / YAML-block-scalar
   collision FIXED via single-quoted `<<'EOF'` + env-piped
   scan outputs + placeholder substitution. actionlint
   exit 0 on the file for the first time since W5. Doubles
   as the canonical example for `docs/agent-handoff-
   protocol.md §5.10` NEW (workflow heredoc convention
   with six rules + audit-trail discipline).
4. **us-east-1 apply readiness re-check (W14 hand-off)** —
   `docs/regional-eks-bringup.md §2.2` NEW (four
   subsections). Source-side TF drift table shows ZERO
   drift since W14; apply-gating contract carries cleanly;
   target-side state still pending Hicks's regional cluster
   lifecycle work; W16 hand-off paths documented.
5. **Phase L L1 design memo (NEW W14 hand-off)** —
   `docs/phase-l-l1-design.md` NEW (seven sections). 12
   DD-numbered design decisions across the four W14
   pre-plan surfaces (§2.1 TURN scaling + §2.2 mobile
   native CI + §2.3 multi-region active-active + §2.4
   container scanning shift-left). Preliminary 10–12 wave
   estimate refined to 10 baseline + 2 optional. Three
   Stephen-decision items surfaced (DD-7 Aurora vs
   session-affinity; DD-4 mobile versioning; L6 EU+APAC
   activation).
6. **SLSA-3 provenance hardening survey (W10 lift)** —
   `docs/slsa-provenance.md §7b` NEW (five subsections).
   Three-gap analysis (signing-key isolation, builder
   platform attestation, isolated build environment) with
   per-gap severity + W16-W18 sequenced remediation plan
   + "why not now (W15)" rationale. No actual hardening
   this wave; W6+ posture unchanged.
7. **CHANGELOG + retro + memo + agent history** —
   `CHANGELOG.md [0.24.0]`, `docs/retro-2027-01.md` NEW,
   `Phase_K_W15/Apone/{charter,history}.md`,
   `.squad/decisions/inbox/apone-phase-k-wave-15.md`,
   `.squad/agents/apone/history.md` append.

## Cross-lane integration points

- **Hicks W15+ regional cluster lifecycle** — the W15
  drift check (`§2.2`) consumes Hicks's cluster work
  reaching ACTIVE for us-east-1 + us-west-2. As of W15
  bring-up, Hicks's work has NOT yet reached ACTIVE; the
  apply-gating contract carries to W16. The W16 owner
  re-runs the drift check + executes the dry-run IF the
  cluster work has landed by W16 bring-up.
- **Hudson W15** — out of scope (no panel work landed
  this wave). The W15 deliverables cite Hudson panels as
  pre-condition smoke surfaces (Kyverno enforce
  pre-conditions: `kyverno-deny-events` + `pod-security-
  violations-prod`; HPA bump pre-conditions:
  `kube-pod-pending` + `cpu-saturation-prod` + `pod-
  evicts-prod` + `hpa-current-replicas`). Hudson re-
  validation is W16+ backlog; if still OOO, Apone owns
  the smoke.
- **Bishop W15** — no direct interface this wave; the
  Kyverno W15 seed rule asserts a Pod-security invariant
  that the W11 backend deployment already satisfies via
  the distroless base.
- **Vasquez W15** — Vasquez's `docs/agent-handoff-
  protocol.md` §6 lane-discipline-maturity narrative is
  parallel content to the W15 §5.10 convention insert;
  both lanes touch the same shared file (per W10
  allowlist precedent for this doc). Coordinate flock
  ordering: §5.10 (Apone) lands before §6 (Vasquez) per
  file ordering; non-overlapping edits.

## Wave invariants

- actionlint clean on the modified workflow file
  (`lane-discipline-nightly.yml` — W5-era parse error
  CLEARED). Full workflow set clean.
- terraform fmt + per-env validate carry from W14 (no
  Apone-lane TF source changes this wave; CLI baseline
  still 1.11.4).
- helm lint carries from W11 baseline (no helm chart
  changes this wave).
- `kustomize build infra/k8s/overlays/{prod,staging}/`
  clean. The W15 commented-out `resources:` entry is a
  byte-identical no-op vs W14 baseline (verified via diff
  against `.work/apone-w14-safe/prod-build.yaml`).
- Backend gate (the Apone lane doesn't touch `src/**`):
  preserved at the inherited 3029/0/0 from W14 merge.
- Frontend renderer budget preserved (the Apone lane
  doesn't touch `src/frontend/**`; W14 < 406 KB baseline
  carried).

## Pre-W16 hand-off notes

- The W16 owner picks up the **Kyverno enforce flip
  cutover-day** IF the four §3 pre-conditions hit GREEN
  (30-day audit-window zero denies + Hudson panel zero +
  staging rehearsal + squad sign-off). Single-line
  uncomment of the W15 commented `resources:` entry.
  Procedure at `docs/kyverno-enforce-rollout.md §4`.
- The W16 owner picks up the **HPA min-replicas 3 → 5
  cutover** IF the §4 readiness PR sign-off lands. Single-
  PR one-line value swap. The HPA bump is NOT a pre-wire
  candidate (number-bump, not behaviour-flip).
- The W16 owner picks up the **SLSA-3 §7b.2.2 builder
  SHA pinning** — single-wave CEL update. Low-cost; W16
  baseline.
- The W16 owner picks up the **SLSA-3 §7b.2.1 self-
  hosted runner pool design memo** — surface the
  ~$150/mo cost to Stephen; prepare the runner-pool TF
  module skeleton.
- The W16 owner picks up the **DD-4 (mobile versioning)
  resolution** — Apone + Hicks inbox memo; Stephen
  arbitrates if disagreement persists.
- The W16 owner monitors whether **Hicks's regional
  cluster lifecycle** reaches ACTIVE for us-east-1 +
  us-west-2 by W16 bring-up; if YES, the §2.1.5 apply-
  gating contract triggers the operator-PR.
- The W17+ owner picks up the **first scheduled JWT
  rotation rehearsal fire monitoring** at 2027-01-01
  02:00 UTC. Append the auto-generated rehearsal report
  to `docs/` + update §4.3 row 4 with the run outcome.
- The W17 owner picks up the **Q1 2027 Terraform CLI
  quarterly bump** (1.11.4 → 1.12.x targeted). The §6.6
  W13 survey shape + §7 W14 actual-bump narrative are
  the template.
- The W17 owner picks up the **first real prod JWT
  rotation** recommended for end of January 2027 (per
  §5.4 recommendation). Operator-only; follows
  `docs/jwt-ssm-runbook.md §3`.
- The W17 owner picks up the **CSP report-only → enforce
  flip pre-wire candidate** per `docs/prod-cutover.md
  §6.5` Gate 6. W16 wire-up + W17 cutover-day per the
  pre-wire pattern.
