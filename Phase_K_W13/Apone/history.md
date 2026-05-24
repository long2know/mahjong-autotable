# Apone — Phase K Wave 13 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/apone/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 13 — DevOps bring-up

Branch: `stlong/phase-k-wave-13-bringup`
Bringup-on commit (W12 close): `147d227` (PR #58 — gate
2610/0/0).

### Deliverables (seven)

1. **Regional EKS cluster bring-up readiness docs.**
   `docs/regional-eks-bringup.md` (NEW). Eight sections —
   §1 why-this-doc-exists (W12 retro D5 carry-over: the
   multi-region EDGE surface is half the path; regional
   clusters are the other half), §2 region inventory (four
   regions: us-east-1 primary apex, us-west-2 secondary,
   eu-west-1 trans-atlantic, ap-southeast-1 SEA/DR-cold),
   §3 per-region Cutover-Ready checklists (seven gates per
   region: TF state bucket, EKS cluster ACTIVE, ACM cert per
   region, R53 health-check association, ESO target per
   region, ALB DNS published, probe sweep clean), §4 cross-
   region invariants (DR data-replication direction,
   single-Redis baseline for W13, JWKS region-agnostic,
   image-SHA consistency, health-check IP allow-list), §5
   apply order (W14 us-east-1 first, then us-west-2; W15
   eu-west-1; W15+ ap-southeast-1), §6 failure scenarios +
   recovery, §7 W14+ hand-offs, §8 cross-references.

   Each region's §3 checklist is a 7-item bullet list with
   verification commands (e.g.
   `aws eks describe-cluster --name mahjong-prod-use1
   --region us-east-1 --query 'cluster.status'`); the
   eu-west-1 list adds a GDPR-review pre-flight, the
   ap-southeast-1 list relaxes the probe p99 threshold to
   200 ms (highest-latency-floor region). No code change —
   the W13 deliverable is the doc; the actual apply lands
   in W14+ once Hicks's regional clusters reach ACTIVE.

2. **JWT rotation rehearsal quarterly cadence.**
   `.github/workflows/jwt-rotation-rehearsal-scheduled.yml`
   (NEW). Thin scheduler — `schedule:` block fires
   `0 2 1 */3 *` (02:00 UTC on the 1st of every 3rd month:
   Jan, Apr, Jul, Oct) + a `workflow_dispatch` back-stop
   for manual catch-up. Dispatches the existing W11
   `jwt-rotation-rehearsal.yml` via
   `actions/github-script@v7`'s
   `createWorkflowDispatch` call. Forces `target_env=staging`
   in the dispatched payload — the W11 hard-gate inside the
   inner workflow remains the second-line defence. The inner
   workflow is UNCHANGED.

   `docs/jwt-rotation-rehearsal.md` — NEW §4 "Quarterly
   cadence" (four sub-sections: §4.1 scheduler workflow + cron
   rationale, §4.2 rehearsal report operator-review path,
   §4.3 quarterly run table with W11+W12 rows + scheduler-
   activation row at W13 + pending Q1-Q4 2027 rows, §4.4
   off-cadence-trigger rules). Renumbering pushed §4–§9 down
   to §5–§10; cross-refs inside the file updated. Cross-
   reference added in §10 to the new scheduler workflow.

3. **ClusterPolicy namespace exclusion via PatchTransformer
   enumeration.** `infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml`
   (NEW, despite the misleading filename — see §3 of the
   companion doc for why "fieldSpecs" doesn't work in
   kustomize v5.4.3). Eight `PatchTransformer` documents,
   one per cluster-scoped Kind (`ClusterPolicy`,
   `ClusterRole`, `ClusterRoleBinding`,
   `CustomResourceDefinition`,
   `MutatingWebhookConfiguration`, `PersistentVolume`,
   `StorageClass`, `ValidatingWebhookConfiguration`); each
   strips `/metadata/namespace` via `op: remove`.

   `infra/k8s/overlays/prod/kustomization.yaml` —
   `transformers:` list extended to reference the new file
   AFTER `namespace-transformer.yaml` (order matters; the
   strip MUST follow the stamp). `infra/k8s/overlays/prod/namespace-transformer.yaml`
   — header annotated with the W13 closure rationale; the
   inline fieldSpecs left UNCHANGED (the W13 fix is the
   downstream stripper, not the upstream transformer — see
   §2.1 of `docs/cluster-policy-namespace-exclusion.md` for
   the empirical reproduction that drove this design choice).

   `docs/cluster-policy-namespace-exclusion.md` (NEW). Eight
   sections — §1 the W4 → W12 bug history, §2 why W12's
   NamespaceTransformer caused it (with §2.1 capturing the
   v5.4.3 empirical reproduction showing fieldSpecs `kind:`
   filter is IGNORED), §3 the W13 fix design, §4
   `kustomization.yaml` wire-up (order MATTERS), §5
   verification (before vs after `kustomize build` Kind/ns
   diff), §6 cross-namespace invariant preserved, §7 future-
   proofing (W14 stretch pre-commit lint script), §8 cross-
   references including the upstream kustomize issue
   tracker.

   Verification (W13):

   ```
   $ kustomize build infra/k8s/overlays/prod/ \
       | awk '/^kind:/{kind=$2} /^  namespace:/{print kind,$2}' \
       | sort -u
   ConfigMap mahjong-prod
   Deployment mahjong-prod
   ExternalSecret mahjong-prod
   HorizontalPodAutoscaler mahjong-prod
   Ingress argo-rollouts
   Ingress mahjong-prod
   Job mahjong-prod
   NetworkPolicy argo-rollouts
   NetworkPolicy mahjong-prod
   PersistentVolumeClaim mahjong-prod
   Secret mahjong-prod
   Service mahjong-prod
   ```

   `ClusterPolicy` absent from the list (the namespace field
   is stripped; the resource is still emitted with all
   spec/metadata bar `metadata.namespace`). Cross-namespace
   targeting preserved — `Ingress` + `NetworkPolicy` split
   across `argo-rollouts` + `mahjong-prod` per W12 contract.

4. **Load-test reminder workflow.**
   `.github/workflows/redis-load-test-reminder.yml` (NEW).
   Two jobs:
   * `open-reminder` — fires on `schedule:` (cron
     `0 14 1 * *` — 14:00 UTC on the 1st of every month) or
     `workflow_dispatch`. Opens an issue titled `Monthly
     Redis load-test reminder — YYYY-MM` with body covering
     the W12 SLO baseline (1000 RPS sustained, p99 lookup
     < 5 ms, p99 write < 8 ms, error rate < 0.1 %), step-
     by-step apply commands (`kubectl -n load-test apply
     -f infra/load-tests/redis-load-test.yml` etc.),
     stale-close convention, cross-references. Idempotent
     — a same-month re-fire matches the existing issue by
     title and no-ops.
   * `stale-close` — fires on the same triggers. Paginates
     open issues with the workflow's label set
     (`ops,redis,load-test,reminder`); comments + closes
     any > 7 days old with `state_reason=not_planned`.

   `docs/redis-cluster.md` — NEW §4.6 "Monthly cadence —
   reminder workflow" sub-section (three sub-sub-sections:
   §4.6.1 cadence table, §4.6.2 operator responsibilities,
   §4.6.3 why a reminder not an auto-applier — captures the
   three rationales: prod-impact blast-radius, operator
   coordination with Hudson burn-rate windows, audit-trail
   preference for issue comments over workflow logs).

   Placement note: the user-facing task wording said "§5
   Monthly cadence" but the existing §5 is "Customer-managed
   KMS key (optional)". Adding the section as a sub-section
   of §4 (the existing "Load-test methodology") avoids
   renumbering eight downstream sections (multiple cross-refs
   to §9, §11, §11.2, §11.4, §12 across the codebase would
   break). The new sub-section sits at §4.6, immediately
   after the §4.5 "Observability hooks" sub-section — the
   semantically-correct home.

5. **`optional: false` envFrom flip post-cutover prep.**
   `infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml`
   (NEW). PR-ready JSON6902 patch (`op: replace` on
   `/spec/template/spec/containers/0/envFrom/4/secretRef/optional`,
   value `false`). NOT wired into `kustomization.yaml` —
   the patch is an artefact for W14+ to apply once the
   four cutover-steady-state pre-conditions hold (file
   header has the full pre-condition list + apply
   procedure + rollback path + index-pin caveat).

   The header documents the W12 envFrom array index mapping
   (0=configMap, 1=mahjong-autotable, 2=mahjong-jwt-keys,
   3=mahjong-jwt-rsa-keys, 4=mahjong-redis-prod) so the W14
   operator can audit the index hasn't shifted before
   applying. Future-proofing note pinned to upstream
   kustomize#3625 (path-by-name JSON-Patch); migrate when
   it ships.

   `docs/prod-cutover.md` — NEW §6 "Post-cutover hardening"
   section (seven sub-sections: §6.1 tightening calendar
   table with six gates W14–W16, §6.2 gate 1 detail
   referencing the patch file, §6.3 gate 4 Kyverno enforce
   mode, §6.4 gate 5 HPA min-replicas bump, §6.5 gate 6 CSP
   enforce mode, §6.6 per-gate rollback, §6.7 per-gate
   observability table mapping each gate to a Hudson panel).
   Table of contents updated with §6 entry.

6. **Terraform CLI W14 bump survey.** `docs/terraform.md`
   — NEW §6.6 "Version bump planning — W14 (1.10.5 →
   1.11.x)" sub-section (five sub-sub-sections: §6.6.1
   candidate baselines + HashiCorp release-page tracking
   inputs, §6.6.2 pre-emptive migration risks table
   covering required_version floor, provider compat, HCL
   syntax, plan-output diffing, lock-file behaviour, DR
   rehearsal workflow pin, moved-blocks, the new
   `removed` block in 1.11, §6.6.3 recommended W14 target
   pin `1.11.4` provisional, §6.6.4 bump-PR shape, §6.6.5
   bump-PR rollback). No actual bump this wave — the §6.2
   quarterly cadence pins the bump to W14.

   Placement note: the user-facing task wording said
   "`docs/terraform.md §3 'Version bump planning'`" but
   §3 is "GitHub-OIDC role" and §6 is the natural home
   (the existing "Version policy" section). Adding as
   §6.6 fits the existing §6.1–§6.5 structure
   contiguously.

7. **CHANGELOG + retro + memo + agent history.**
   * `CHANGELOG.md` `[0.22.0]` Phase K Wave 13 entry added
     above `[0.21.0]`. `[Unreleased]` working branch flipped
     to `stlong/phase-k-wave-13-bringup`. Theme paragraph +
     "Added" + "Changed" subsections.
   * `docs/retro-2026-11.md` (NEW). Six sections: 1. What
     shipped (W13 deliverables + cross-lane handoffs +
     gates); 2. What worked well (cron-scheduler-delegate
     pattern, kustomize fieldSpecs empirical reproduction,
     PR-ready patch artefacts, per-region readiness
     checklist template, single-issue-per-month idempotent
     reminder); 3. What didn't work / open items (kustomize
     fieldSpecs `kind:` filter ignored in v5.4.3, per-
     section-number coordination across docs is fragile,
     Hudson absent in W13 widened DevOps scope unexpectedly,
     `redis-envfrom-required-patch.yaml` index-pin is
     fragile pending upstream kustomize#3625); 4. Lessons
     learned; 5. What's coming in W14; 6. Cross-references.
   * `Phase_K_W13/Apone/charter.md` (this dir).
   * `Phase_K_W13/Apone/history.md` (this file).
   * `.squad/decisions/inbox/apone-phase-k-wave-13.md`
     (NEW) — W13 memo with seven decisions matching the
     seven deliverables.
   * `.squad/agents/apone/history.md` append — W13 entry.

### Verification gates

* `terraform fmt -recursive -check infra/terraform/`: clean.
* `terraform validate` against `infra/terraform/envs/prod/`,
  `envs/staging/`, `envs/dr-us-west-2/`, `modules/redis/`,
  `modules/github-oidc/`: clean. The standalone
  `modules/edge/` validate hits a pre-existing
  `configuration_aliases` constraint (not a W13 regression).
* `kustomize build infra/k8s/overlays/{prod,staging}/`:
  clean. ClusterPolicy-namespace-strip assertion verified
  (per W13 §3 verification block above).
* `actionlint .github/workflows/jwt-rotation-rehearsal-scheduled.yml
  .github/workflows/redis-load-test-reminder.yml`: clean.
  (The pre-existing
  `.github/workflows/lane-discipline-nightly.yml:87`
  YAML-parse error is W5-era and not touched by W13.)
* `helm lint helm/mahjong/`: clean (W13 didn't change the
  chart).
* Backend xUnit gate **2610/0/0** preserved (Apone lane
  doesn't touch `src/`).

### Lane discipline

* All commit-staged paths fall in the Apone lane regex per
  `.squad/agents/apone/charter.md`. New paths
  `docs/regional-eks-bringup.md` and
  `docs/cluster-policy-namespace-exclusion.md` added to the
  W13 charter allowlist + tracked in this history.
* Git identity via per-command `-c user.name=... -c
  user.email=...` env, NEVER `git config`. All commit /
  push wrapped in `flock -w 120 9 ... 9>.work/squad-git-lock`.
* Co-author trailer mandatory on every commit.
* `git status --short` reviewed before each `git add` —
  cross-lane work from other agents present in the working
  tree is NEVER staged.

### Cross-lane integration points (W13)

* **Hicks W13+ regional cluster lifecycle** — the W13
  `docs/regional-eks-bringup.md` documents the readiness
  gates that the Hicks-owned cluster work unblocks. The
  W14+ owner consumes the gates as the Cutover-Ready
  signal before populating `regional_endpoints` in the
  prod env stack.
* **Hudson W13 (absent)** — the redis-load-test reminder
  workflow was originally tagged Hudson-lane in the W12
  retro D5; W13 ownership moved to DevOps when Hudson was
  rolled out of the W13 scope. The narrative cadence
  convention in `docs/redis-cluster.md §4.6` remains
  squad-wide.
* **Bishop W13** — no direct interface; the W13 JWT
  rotation scheduler delegates to the W11 inner workflow
  which already exercises Bishop's W12 JWKS-cache pre-warm.
* **Vasquez W13** — no direct interface; the per-region
  readiness checklist (§3 of the regional-EKS doc) cross-
  references Vasquez's PWA service worker per-region
  rollout in `docs/cdn-cache-strategy.md` indirectly.

### Pre-W14 hand-off notes

* The first scheduled JWT rotation rehearsal fires
  **2027-01-01 02:00 UTC** (W17+ wave likely owns it). The
  scheduler workflow has been validated by `actionlint`
  but has not yet had a live cron fire — the W14 owner
  should watch for the first run and confirm the
  dispatched payload reaches the inner workflow.
* The W14 owner picks up the regional-EKS-bringup
  checklist per region (us-east-1 first); confirmation
  that Hicks's W13+ clusters reached ACTIVE state is the
  signal to start.
* The W14 owner runs the Terraform CLI bump per the W13
  §6.6 plan; pin the actual 1.11.x patch number at W14
  bring-up time. Survey HashiCorp's release page on the
  W14 bring-up day to confirm `1.11.4` is still the right
  recommendation (HashiCorp may have shipped 1.11.5+ by
  then).
* The W14 owner reviews the Redis envFrom required patch
  (`redis-envfrom-required-patch.yaml`) and applies it
  ONLY when the four §6.2 pre-conditions hold. Do NOT
  apply prematurely — the W13 prep deliberately did not
  wire the patch into `kustomization.yaml` for this
  reason.
* The W13 ClusterPolicy stripper enumerates eight
  cluster-scoped Kinds; if a future helm chart upgrade
  introduces a new cluster-scoped Kind, add a new
  `PatchTransformer` document at the end of
  `cluster-scoped-fieldspecs.yaml` (the file's header has
  the convention).
