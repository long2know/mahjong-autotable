# Apone — Phase K Wave 12 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/apone/charter.md`. The Phase_K_W12/Apone/
> directory is the W12 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Apone
- **Role:** DevOps / Platform Engineer
- **Wave:** Phase K Wave 12 — DevOps bring-up
- **Branch:** `stlong/phase-k-wave-12-bringup`
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
- `Phase_K_W12/Apone/**`
- `docs/agent-handoff-protocol.md`
- `docs/argo-rollouts-setup.md`
- `docs/container-deployment.md`
- `docs/dr-rehearsal.md`
- `docs/edge-region-probes.md`
- `docs/helm-charts.md`
- `docs/jwt-rotation-rehearsal.md`
- `docs/jwt-ssm-runbook.md`
- `docs/oauth-production-setup.md`
- `docs/prod-cutover.md` (NEW W12)
- `docs/production-deployment-runbook.md`
- `docs/redis-cluster.md`
- `docs/retro-2026-*.md` (monthly retros)
- `docs/secret-management.md`
- `docs/secrets-scanning.md`
- `docs/staging-cutover.md`
- `docs/terraform.md`
- `.squad/agents/apone/**`
- `.squad/decisions/inbox/apone-*.md`

## NOT in my lane

- `src/**` (Bishop owns backend C#; Hicks owns frontend TS)
- `tests/**` (Vasquez owns acceptance + integration + e2e)
- Mobile source code (mobile-source-of-truth lane)
- Wave-decision narrative outside Apone-owned inbox memos
  (Scribe orchestrates decisions.md merges)

## W12 deliverables (seven)

1. **Prod Redis terraform plan readiness assessment** —
   the W10/W11 Redis ElastiCache surface confirmed
   `terraform plan`-ready against a vanilla operator
   workstation. Surfaced as §1 of the NEW
   `docs/prod-cutover.md` runbook (pre-flight assertions,
   required tfvars including the W12 `regional_endpoints`
   addition, expected plan shape, apply gates).
2. **Prod kustomization wire-up** —
   `infra/k8s/overlays/prod/kustomization.yaml` swaps the
   top-level `namespace: mahjong-prod` for a
   `NamespaceTransformer + unsetOnly: true` (NEW file
   `namespace-transformer.yaml`). Adds three entries to
   `resources:` (W11 Redis ESO + W11 argo-rollouts ingress +
   NEW W12 NetworkPolicy file). Adds one deployment patch
   (Redis envFrom secretRef with `optional: true` for cutover-
   safe fall-through). W11 file headers on
   `redis-connection-string-secret.yaml` +
   `argo-rollouts-ingress-auth.yaml` flipped IN-BAND.
3. **Prod Redis load-test re-baseline** —
   `infra/load-tests/redis-load-test.yml` (NEW). k6 manifest
   (Namespace + ConfigMap-script + Job) running 1000 RPS for
   5 min against Bishop's W10 `RedisIdempotencyStore` runtime.
   SLO thresholds wired into k6 `thresholds:` (p99 lookup <
   5 ms, p99 write < 8 ms, p99.9 lookup < 25 ms, error rate <
   0.1 %). New §4 of `docs/redis-cluster.md` (Load-test
   methodology) walks the artifact + the W12 initial baseline
   numbers + the re-baseline cadence rules.
4. **Per-region R53 records** —
   `infra/terraform/modules/edge/r53-regional-records.tf`
   (NEW). Three resource types keyed by the new
   `regional_endpoints` tfvar: per-region TCP/443 health check,
   per-region ALIAS A record, latency-based RR set on the apex.
   Gates the W7 single-region apex via the new
   `local.use_latency_apex` flag — empty
   `regional_endpoints` preserves W11 behaviour exactly. Wired
   through to `infra/terraform/envs/prod/{main,variables}.tf`.
   `docs/edge-region-probes.md §3` updated in-place to document
   the new region-anchored hostname path + cutover sequence +
   rollback.
5. **Argo Rollouts NetworkPolicy hardening** —
   `infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`
   (NEW). Three NetworkPolicies in the `argo-rollouts`
   namespace: dashboard ingress allow-list from
   `ingress-nginx` + `auth`, controller egress to
   kube-apiserver + `monitoring` + kube-dns, dashboard egress
   to kube-apiserver + kube-dns. Closes the network-level
   loop on top of the W11 identity-level loop. New §6 of
   `docs/argo-rollouts-setup.md` (NetworkPolicy hardening)
   walks the three policies, the split rationale, the wire-in
   via the W12 kustomization, validation, upgrade procedure,
   and the rollback path.
6. **Second JWT rotation rehearsal documentation** —
   `docs/jwt-rotation-rehearsal.md §3` (NEW section) captures
   both W11 first run + W12 second run, per-phase timing
   deltas (W12 is 39 % faster — wins all downstream of Bishop
   W12 JWKS-cache pre-warm), GA-readiness recommendation
   (promote cadence from operator-triggered to scheduled
   monthly), target timing scale (green / yellow / red) for
   future runs.
7. **CHANGELOG + retro + memo + agent history** —
   `CHANGELOG.md` `[0.21.0]` Phase K Wave 12 entry +
   `[Unreleased]` working branch flipped to W12 branch;
   `docs/retro-2026-10.md` (NEW); wave hand-off artefacts
   under `Phase_K_W12/Apone/`;
   `.squad/decisions/inbox/apone-phase-k-wave-12.md` (NEW);
   `.squad/agents/apone/history.md` append.

## Identity discipline (per W6 invariant)

- Per-command git env: `git -c user.name="Apone (DevOps)" -c
  user.email="apone@squad.mahjong" ...`
- NEVER `git config user.name` / `git config user.email`.
- All commit / push wrapped in `flock -w 120 9 ...
  9>.work/squad-git-lock`.
- Co-author trailer mandatory.

## Targets

- `terraform validate` clean across all modules + env stacks
  touched.
- `terraform fmt -recursive -check infra/terraform/` clean.
- `kustomize build` clean on `infra/k8s/overlays/{prod,staging}`
  (W12-relevant assertion: cross-namespace argo-rollouts
  resources keep `namespace: argo-rollouts`).
- `actionlint` clean (W12 didn't change workflows; verify
  baseline preserved).
- `helm lint helm/mahjong/` clean (W12 didn't touch the chart).
- Backend xUnit gate **2403/0/0** preserved (Apone lane
  doesn't touch `src/`).
- Zero out-of-lane staging (`git add` only with explicit path
  list).
- Single commit, single push, single PR-ready branch.
