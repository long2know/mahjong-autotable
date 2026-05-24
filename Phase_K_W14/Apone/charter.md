# Apone — Phase K Wave 14 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/apone/charter.md`. The Phase_K_W14/Apone/
> directory is the W14 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Apone
- **Role:** DevOps / Platform Engineer
- **Wave:** Phase K Wave 14 — DevOps bring-up
- **Branch:** `stlong/phase-k-wave-14-bringup`
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
- `Phase_K_W14/Apone/**`
- `docs/agent-handoff-protocol.md`
- `docs/argo-rollouts-setup.md`
- `docs/cluster-policy-namespace-exclusion.md`
- `docs/container-deployment.md`
- `docs/dr-rehearsal.md`
- `docs/edge-region-probes.md`
- `docs/frontend-pwa-audit.md` (Hicks's doc — Apone-authored
  PWA Builder workflow runtime sections per the W10
  precedent; W14 §12 lands here)
- `docs/helm-charts.md`
- `docs/janus-deployment.md`
- `docs/jwt-rotation-rehearsal.md`
- `docs/jwt-ssm-runbook.md`
- `docs/mobile-release.md`
- `docs/oauth-production-setup.md`
- `docs/phase-l-devops-readiness.md` (NEW W14)
- `docs/prod-cutover.md`
- `docs/production-deployment-runbook.md`
- `docs/redis-cluster.md`
- `docs/regional-eks-bringup.md`
- `docs/retro-2026-*.md` (monthly retros)
- `docs/secret-management.md`
- `docs/secrets-scanning.md`
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

## W14 deliverables (seven)

1. **Regional EKS us-east-1 plan readiness (W13 hand-off)**
   — `docs/regional-eks-bringup.md §2.1` NEW (six
   subsections). The W13 readiness checklist + W14 dry-run
   command + expected ~20-resource plan shape + per-§3.1-gate
   scrutiny checklist + plan-output retention discipline.
   The actual `terraform plan` is operator-driven (requires
   AWS creds + populated state bucket); the W14 deliverable
   is the reviewed plan-readiness narrative.
2. **Terraform CLI 1.10.5 → 1.11.4 quarterly bump (W13
   §6.6 plan)** — `.github/workflows/dr-rehearsal.yml` one-
   line bump + `docs/terraform.md §7` NEW (seven subsections).
   `terraform fmt -recursive -check` + per-env `init +
   validate` clean on 1.11.4. AWS provider lock unchanged.
3. **Redis envFrom flip post-cutover pre-wire (W12+W13
   hand-off)** —
   `infra/k8s/overlays/prod/kustomization.yaml` gains a
   COMMENTED-OUT `patches:` entry for the W13 PR-ready
   `redis-envfrom-required-patch.yaml`. The cutover-day
   enablement becomes a four-line uncomment.
   `docs/prod-cutover.md §6.8` NEW (five subsections)
   covers the enablement procedure + index-pin contract +
   pre-flip invariant + rollback.
4. **JWT rehearsal #3 (W13 hand-off — quarterly cadence)** —
   `docs/jwt-rotation-rehearsal.md §5` NEW Rehearsal #3
   detail; existing §5-§10 renumbered §6-§11. Per-phase
   timing +3 s vs W12 (within noise); §3.3 GREEN budget
   holds. GA-readiness CONFIRMED. First prod rotation
   recommended for end of January 2027.
5. **PWA Builder CI hardening (W11+W13 hand-off)** —
   `.github/workflows/pwa-builder.yml` provenance-tagged
   URL resolution + always-populated `$GITHUB_STEP_SUMMARY`
   + PR-comment-on-skip pattern + success-path preview-URL
   hyperlink. `docs/frontend-pwa-audit.md §12` NEW
   operator runbook for preview URL provisioning.
6. **Phase L DevOps pre-plan (NEW)** —
   `docs/phase-l-devops-readiness.md` NEW. Four surfaces:
   §2.1 TURN cluster scaling 3 waves, §2.2 mobile TestFlight
   + Play Console 2 waves, §2.3 multi-region active-active
   4–5 waves with Aurora-vs-session-affinity decision gate,
   §2.4 container scanning shift-left 1 wave. Preliminary
   10–12 wave estimate with cross-surface dependency graph.
7. **CHANGELOG + retro + memo + agent history** —
   `CHANGELOG.md [0.23.0]`, `docs/retro-2026-12.md` NEW,
   `Phase_K_W14/Apone/{charter,history}.md`,
   `.squad/decisions/inbox/apone-phase-k-wave-14.md`,
   `.squad/agents/apone/history.md` append.

## Cross-lane integration points

- **Hicks W14+ regional cluster lifecycle** — the W14
  regional-eks-bringup §2.1 plan-readiness narrative
  consumes Hicks's cluster work reaching ACTIVE state
  for us-east-1 + us-west-2. The actual `terraform apply`
  lands in a separate operator-PR after the §3.1
  cutover-ready checklist hits ✅ × all rows.
- **Hudson W14** — out of scope (no panel work landed);
  the W14 retros + `docs/prod-cutover.md §6.8.2` operator
  runbook cite the existing W12 panel set
  (`kube-pod-not-ready`, `eso-sync-failures-prod`,
  `auth-failure-rate-prod`, `jwks-publish-latency`). Hudson
  re-validation is W15+ backlog.
- **Bishop W14** — no direct interface this wave; the W14
  JWT rotation rehearsal #3 exercises Bishop's W12 JWKS-
  cache pre-warm (already in steady-state).
- **Vasquez W14** — no direct interface this wave. The
  Vasquez §11.5 hand-off from W13 (visual-regression spec
  setContent-without-goto bug) is on Vasquez's W14 backlog,
  independent of Apone's lane.

## Wave invariants

- actionlint clean on the two modified workflow files
  (`pwa-builder.yml`, `dr-rehearsal.yml`). Pre-existing
  `lane-discipline-nightly.yml:87` heredoc parse error
  carries over from W5.
- terraform fmt + validate clean across all modules + envs
  on 1.11.4.
- helm lint clean (no helm chart changes this wave; W11
  baseline preserved).
- `kustomize build infra/k8s/overlays/{prod,staging}/`
  clean. The W14 pre-wire is a comment-only kustomization.yaml
  edit; build output identical to W13 baseline (verified
  per `docs/prod-cutover.md §6.8.4` invariant check).
- Backend gate (the Apone lane doesn't touch `src/**`):
  preserved at the inherited 2789/0/0 from the W13 merge.
- Frontend renderer budget preserved (the Apone lane
  doesn't touch `src/frontend/**`).

## Pre-W15 hand-off notes

- The W15 owner picks up the **Kyverno `audit → enforce`
  flip pre-wire candidate** per `docs/prod-cutover.md §6.3`
  Gate 4. The W14 pattern (pre-wire commented-out, cutover-
  day uncomment) is the candidate approach. W15 decides
  whether to adopt the pattern OR land the flip as a
  single-PR cutover-day change.
- The W15 owner picks up the **HPA min-replicas 3 → 5 bump
  pre-flight** per `docs/prod-cutover.md §6.4` Gate 5. The
  30-day pre-condition (`kube-pod-pending` 100% + `cpu-
  saturation-prod` < 60% p99) requires Hudson panel review;
  W14 + 14 days is the earliest plausible target.
- The W15 owner takes over the **lane-discipline-nightly.yml
  line 87 parse error** (W5-era heredoc YAML); fix is a
  heredoc indent change. On the W15+ backlog.
- The W15 owner monitors whether **Hicks's regional
  cluster lifecycle** reaches ACTIVE for us-east-1 +
  us-west-2 by W15 bring-up; if YES, the §2.1.5 apply-
  gating contract triggers the operator-PR
  (`terraform apply us-east-1.tfplan`) at Stephen's call.
- The W15+ owner monitors the **first scheduled JWT rotation
  rehearsal fire** at 2027-01-01 02:00 UTC. Append the auto-
  generated rehearsal report to `docs/` (per §4.2 operator
  procedure) + update §4.3 row 4 to mark the run outcome.
- The W17+ owner picks up the **Q1 2027 Terraform CLI
  quarterly bump** (1.11.x → 1.12.x targeted). The §6.6 W13
  survey shape + §7 W14 actual-bump narrative are the
  template.
- The W17+ owner picks up the **first real prod JWT
  rotation** recommended for end of January 2027 (per §5.4
  recommendation). Operator-only; follows
  `docs/jwt-ssm-runbook.md §3`.
