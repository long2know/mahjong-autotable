# Apone — Phase K Wave 23 inbox memo

**Wave:** Phase K Wave 23 — Apone (DevOps) bring-up.
**Base:** `stlong/phase-k-wave-23-bringup` (created from
main `a472566` post-W22).
**Version triple:** CHANGELOG `[0.32.0]` + `mobile/package.json`
0.31.0 → 0.32.0 (root `package.json` does not exist in this
repo; CHANGELOG header is the anchor — per W22 inbox memo).

## 6 deliverables landing apone-lane W23

### 1. Kyverno W23 audit-mode launch — 4th batch

* `infra/k8s/base/kyverno-policies/require-readonly-rootfs.yaml`
  (NEW). `ClusterPolicy` requiring
  `securityContext.readOnlyRootFilesystem: true` on every
  container in every `mahjong-prod` Pod. Audit-mode +
  `failurePolicy: Ignore` launch with a 5-WAVE grace window
  (W23 → W28 earliest enforce-flip).
* `infra/k8s/base/kyverno-policies/require-runas-non-root.yaml`
  (NEW). `ClusterPolicy` requiring EXPLICIT
  `securityContext.runAsNonRoot: true` at Pod-spec level.
  Closes the W15-rule gap where the `=()` conditional-anchor
  pattern only enforces value-when-present; W23 hardens to
  require field-presence so a future root-default base-image
  swap surfaces as an admission denial rather than silently
  regressing.
* Pre-W22 candidate audit search: all four existing
  policies in `infra/k8s/base/kyverno-policies/` are
  already at Enforce post-W22 (`disallow-host-paths`,
  `disallow-lateral-movement`, `require-network-policy`,
  `require-resource-limits`). The directive's "if
  insufficient candidates exist" fallback applies — W23
  adds 2 NEW audit-mode rules rather than flipping
  existing ones.
* Audit window extended from W19/W21's 5-DAY grace to a
  5-WAVE grace because (a) coverage applies to every
  container in every prod Pod (W19/W21 narrower scope —
  hostNetwork / hostPort / hostPath only), and (b) the
  composition risk surface against 5 existing enforce-mode
  rules warrants longer audit-window data collection.
* `docs/kyverno-w23-additional-rules.md` (NEW) covers the
  rule rationale + composition contract with W15 + 5-wave
  audit-window snapshot procedure + expected-outcomes
  per-wave checkpoint table + W28 enforce-flip cutover
  diff + rollback path + W19-lineage cadence summary +
  W23 → W24 hand-off (W24 SRE runs day-2 + day-3 + day-4
  + day-5 snapshots).
* **Out-of-band by design** (per W19/W21 pattern):
  NOT registered in `base/kustomization.yaml`. The
  K8sManifestSanity test (`BaseKustomization_IncludesAllResources`)
  enumerates only TOP-LEVEL `base/*.yaml` files; subdir
  files under `base/kyverno-policies/` are cluster-bootstrap
  and excluded by the test's `EnumerateFiles` call (no
  recursion). Verified via test source-read pre-edit.
* Kustomize `base/` + `prod/` + `staging/` builds byte-
  identical to pre-W23 baseline (the new files don't
  participate in the overlay graph).

### 2. SLSA-3 drift-detection first-run retro

* `docs/slsa-drift-retro.md` (NEW). Phase K Wave 22 shipped
  `.github/workflows/slsa-drift-detection.yml` on Monday
  07:00 UTC cron. W23 lands the FIRST-RUN retro covering:
  * **Status at W23 ship: First run pending.** The W22 PR
    merged on 2027-02-26; the first scheduled cron fires
    on the next Monday ≥ merge-day. W23 bring-up runs
    BEFORE that window closes, so the retro is the
    runbook FOR analysis rather than a post-hoc capture.
  * **Expected drift indicators** by shape: semver tag /
    major-only / branch / latest / pre-release.
  * **Baseline expectation: 0-9 drift hits** (matches the
    W20 sweep ledger's 9 vasquez-lane carry; any other
    count is a NEW regression).
  * **Remediation flow**: classify each hit into
    vasquez-carry / new-regression / new-carve-out; for
    each new-regression, walk a structured pin-rewrite
    procedure (`gh api` SHA lookup → workflow-file edit →
    actionlint gate → `workflow_dispatch` local validation
    → single-file PR tagged `slsa-drift`).
  * **W24 hand-off**: W24 bring-up owner runs the
    first-run capture once the cron fires; appends the
    `drift-run-<YYYY-MM-DD>.txt` artefact to §5; if
    `New regressions > 0` walks §3.3 + opens a
    `slsa-drift` PR in the same wave.
  * **Long-term graduation**: 4 consecutive GREEN runs
    promote the retro to "sustaining-surface confirmed";
    subsequent RED runs handled in per-wave inbox memo
    rather than appending here.

### 3. Mobile platform CI cross-check workflow

* `.github/workflows/mobile-platform-cross-check.yml`
  (NEW). Fires on `workflow_run` after `mobile-build` +
  `workflow_dispatch` with explicit `run_id` input. Job
  graph:
  1. `Resolve upstream run ID` — branches on
     `workflow_dispatch` vs `workflow_run` event shape.
  2. `Download all mobile-build artefacts` — `gh run
     download` per leg (`android-artefacts`,
     `ios-artefacts`, `tvos-artefacts`,
     `watchos-artefacts`); soft-fail per leg with a
     `::warning::` so the verification step gets to
     classify MISSING vs OUT_OF_BAND.
  3. `Verify all 4 platforms produced artefacts` —
     `BANDS` array maps leg → (min-bytes, max-bytes);
     iterates each leg, counts files + sums bytes,
     classifies failures into MISSING (zero files) +
     OUT_OF_BAND (bytes outside band); fails the job
     with a structured `$GITHUB_STEP_SUMMARY` and
     `::error::` annotations.
  4. `Upload cross-check summary` — `if: always()` so
     the bundle is forensically available for both
     pass + fail.
* Closes the W22 silent-regression window where
  `if-no-files-found: warn` in `mobile-build.yml` lets a
  leg drop its artefact without failing the build.
* `workflow_run` fires on BOTH success + failure of
  upstream — explicitly so the cross-check surfaces
  "android passed but tvos silently dropped" as a
  cross-check failure even when upstream went green.
* Band lower bound = 1 byte (permissive on content
  shape since W22 emits placeholder text files for
  tvos/watchos on non-bootstrapped Capacitor shells);
  upper bound = 500 MiB (generous absorbs future
  real-build growth without false positives).
* actionlint clean on the new workflow.

### 4. CHANGELOG + version triple

* `CHANGELOG.md` — `[Unreleased]` flipped from W22 → W23
  branch; new `[0.32.0]` entry lands above `[0.31.0]`
  with the full W23 theme paragraph (6 deliverables +
  CHANGELOG + `mobile/package.json` bump). Wave-count-
  tracks-version: W23 → 0.32.0 (W22=0.31.0; W11=0.20.0
  anchor).
* `mobile/package.json` — `0.31.0` → `0.32.0`.
* Root `package.json` — DOES NOT EXIST in this repo
  (per W22 inbox memo); CHANGELOG header is the version
  anchor. Bishop-lane csproj bump deferred (separate
  lane).

### 5. us-east-1 V3 post-rollback drift reconciliation runbook

* `docs/us-east-1-v3-runbook.md` (NEW). Extends the
  W20 V2 rollback section with a structured
  post-rollback reconciliation path:
  * **§2 — 8 drift surfaces catalogued**: EKS aws-auth
    ConfigMap (clean), R53 latency policy (clean), ESO
    `mahjong-jwt-rsa-keys` (Apone reconciles), Kyverno
    ClusterPolicies (Apone reconciles partial-destroy),
    Kyverno PolicyReports (rebuilds), Argo Rollouts
    AnalysisRun (Hicks-coord), coturn DH params (Apone
    reconciles), Hudson metric backfill (Hudson coord).
  * **§3 — Reconciliation steps in execution order**:
    §3.1 verify rollback completed; §3.2 SSM param
    audit + re-provision per `docs/jwt-ssm-runbook.md
    §3`; §3.3 Kyverno ClusterPolicy CRD ghost-object
    reconciliation loop (W23 baseline: 9 policies);
    §3.4 Argo Rollouts AnalysisRun cleanup coordinated
    with Hicks; §3.5 coturn Secret rebuild via the W6
    ExternalSecret; §3.6 Hudson panel-gap annotation
    for >1hr outages.
  * **§4 — V3-specific pre-flight rows**: 2 NEW rows
    extending V2's 8-row checklist (drift-reconciled
    blocking + hudson-annotated warning).
  * **§5 — V3-specific failure shapes**: 2 NEW shapes
    (SSM destroyed, Kyverno CRD ghost) catalogued with
    remediation.
  * **§7 — V4 hand-off candidates**: fold §3
    reconciliation steps into the W21 `null_resource`
    provisioner as post-destroy steps; add a
    `terraform apply` precondition that refuses to
    re-apply when §3.1 → §3.5 reconciliation hasn't
    been logged.

### 6. Argo Rollouts post-install verification runbook

* `docs/argo-rollouts-post-install-verification.md`
  (NEW). 10-row checklist between W19 install
  completion + first production rollout:
  * §3.1 controller pod health (`1/1 Running`,
    `RESTARTS 0`).
  * §3.2 CRDs registered (5 expected: analysisruns,
    analysistemplates, clusteranalysistemplates,
    experiments, rollouts).
  * §3.3 admission webhook registered.
  * §3.4 W11 auth-aware ingress applied.
  * §3.5 W12 NetworkPolicy applied (default-deny
    + allow-controller-egress + allow-dashboard-
    ingress).
  * §3.6 W19 RBAC bindings landed (canary-promoter
    + rollouts-reader).
  * §3.7 W9 AnalysisTemplate gates admitted (3
    expected: canary-error-budget,
    canary-p99-latency, canary-success-rate).
  * §3.8 BlueGreen + Canary helm dry-render.
  * §3.9 Hudson panels return data.
  * §3.10 No-op rollout — exercises controller
    without real traffic via timestamp-annotation
    patch; expects `Healthy` end-state +
    PromotionAuto Successful + AnalysisRun
    Successful.
  * **§4 sign-off** documented as a comment on the
    W19 install PR.
  * **§5 remediation paths** map 1:1 to §3 failure
    rows.
  * **§6 audit-trail anchors** via the no-op
    rollout's `verification-noop` annotation
    timestamp + Hudson's
    `argo-rollouts-controller-info`
    `installed_at` label.

## Lane-discipline notes

* All edits inside Apone-lane per
  [`tests/ci/lane-map.json`](../../tests/ci/lane-map.json)
  + the legacy classifier in
  [`tests/ci/check-cross-lane-bundling.sh`](../../tests/ci/check-cross-lane-bundling.sh):
  * `infra/k8s/base/kyverno-policies/*.yaml` → apone
  * `.github/workflows/*.yml` → apone
  * `docs/*.md` → shared (legitimate)
  * `CHANGELOG.md` → shared (legitimate, primary apone)
  * `mobile/package.json` → unclassified (legitimate;
    no cross-lane violation; per W22 inbox memo)
* No edits to `src/backend/`, `src/frontend/`, or
  `tests/` directories. csproj NOT touched (Bishop
  lane).

## Stash-discipline notes (§9 STASH-ISOLATION respected)

* Used `git stash --include-untracked` OUTSIDE the
  flock to set aside two `.fuse_hidden*` FUSE artefact
  files BEFORE entering the flock (FUSE filesystem
  generates these spontaneously during the session;
  not in Apone's lane).
* NO `git stash pop` of any other agent's work (W20
  Hicks-tree-wipe retro pattern honoured).
* Files staged BY NAME via `git add path/to/file1
  path/to/file2 ...` — no `git add -A`, no `git add
  -u`, no directory adds.
* `git diff --cached --name-only` run inside the flock
  before `git commit` confirms the apone-lane staging
  list.

## Validation gate

* `actionlint .github/workflows/*.yml` — clean on all
  46+1 workflow files (W23 adds 1:
  `mobile-platform-cross-check.yml`).
* `kustomize build infra/k8s/base/` — OK.
* `kustomize build infra/k8s/overlays/prod/` — OK.
* `kustomize build infra/k8s/overlays/staging/` — OK.
* The two new `infra/k8s/base/kyverno-policies/*.yaml`
  files are OUT-OF-BAND (not in
  `base/kustomization.yaml`) per the W19/W21 Kyverno
  ClusterPolicy pattern — kustomize build is
  unaffected.
* `bash tests/ci/check-cross-lane-bundling.sh --pr
  stlong/phase-k-wave-23-bringup --strict` —
  violations=0.

## W23 → W24 hand-off candidates

* **Kyverno** — W23 audit-window day-2 → day-5
  snapshot procedure execution; build per-wave
  audit-window evidence + the W24 mid-window check
  in `docs/kyverno-w23-additional-rules.md §3`.
* **SLSA drift-detection** — first-run capture once
  the cron fires (W24 or W25 depending on bring-up
  timing); append to
  `docs/slsa-drift-retro.md §4.1 + §5`.
* **Mobile cross-check** — confirm the new workflow
  fires correctly on the first mobile-build run
  after merge; tune size bands once real-build data
  is observed (likely tighten upper bounds).
* **us-east-1 V3** — IF Stephen executes a real
  rollback at some point, V4 replaces §5
  hypotheticals with capture-from-actual-rollback
  data + folds §3 reconciliation into the W21
  `null_resource` provisioner.
* **Argo Rollouts** — IF Stephen completes the W19
  install, the W23 verification checklist becomes
  the gate; capture the §3.10 no-op rollout output
  into the install PR comment.

## Cross-references

* W22 inbox memo (precedent shape):
  `.squad/decisions/inbox/apone-phase-k-wave-22.md`.
* W19 / W21 audit-mode launch precedent:
  `docs/kyverno-w19-additional-rules.md` +
  `docs/kyverno-w21-additional-rules.md`.
* W20 / W22 enforce-flip retro pattern:
  `docs/kyverno-w22-additional-rules.md`.
* W22 SLSA drift-detection workflow:
  `.github/workflows/slsa-drift-detection.yml` +
  `docs/slsa-drift-detection.md`.
* W22 mobile-build matrix expansion:
  `.github/workflows/mobile-build.yml` (tvos-build +
  watchos-build jobs at lines 776, 895).
* W19 + W20 us-east-1 runbooks:
  `docs/us-east-1-apply-runbook.md`.
* W22 auto-rollback workflow:
  `docs/us-east-1-auto-rollback-runbook.md`.
* W19 Argo Rollouts install:
  `docs/argo-rollouts-install-runbook.md`.
* W20 BackendBlueGreen + W21 FrontendCanary:
  `docs/argo-rollouts-backend-bluegreen.md` +
  `docs/argo-rollouts-frontend-canary.md`.
