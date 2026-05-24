# Apone — Phase K Wave 22 inbox memo

**Wave:** Phase K Wave 22 — Apone (DevOps) bring-up.
**Base:** `stlong/phase-k-wave-22-bringup` (created from
main `38de55d` post-W21).
**Version triple:** CHANGELOG `[0.31.0]` + `mobile/package.json`
0.30.0 → 0.31.0 (root `package.json` does not exist in this
repo; CHANGELOG header is the anchor).

## 6 deliverables landing apone-lane W22

### 1. Kyverno W22 enforce-flip — `require-resource-limits` + `disallow-host-paths`

* `infra/k8s/base/kyverno-policies/require-resource-limits.yaml`
  flipped `validationFailureAction: Audit → Enforce` +
  `failurePolicy: Ignore → Fail`. Title + description
  annotations updated from `(W21, Audit)` → `(W22,
  Enforce)`.
* `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml`
  same flip + annotation update.
* `docs/kyverno-w22-additional-rules.md` (NEW) documents
  the W22 cutover with the 5-day audit-window evidence
  table (days 1-5 ALL pass, zero Fail/Error rows), the
  cutover-day verification command set, the rollback
  path (single `git revert`), and the W19→W20 + W21→W22
  two-wave audit→enforce cadence summary.
* The flip is BYTE-MINIMAL on the policy rule shape —
  only the spec-level toggles + annotation strings
  change; `rules:` blocks are unchanged from W21.

### 2. SLSA-3 drift-detection — weekly sustaining workflow

* `.github/workflows/slsa-drift-detection.yml` (NEW).
  Weekly Monday 07:00 UTC cron (+ `workflow_dispatch`
  manual trigger) walks `.github/workflows/*.yml`,
  extracts every `uses: <action>@<ref>` line, and
  FAILS the run when any ref is NOT a 40-char hex SHA
  outside the documented allow-list.
* Allow-list:
  * `slsa-framework/slsa-github-generator/*` — SLSA-3
    spec carve-out (tag-pin is the OIDC trust anchor).
  * `./*` — local workflow references (no SHA to pin).
* Drift hits are collected into `drift-hits.txt`,
  uploaded as a workflow artefact, and surfaced via:
  * a failed CI run (exit 1 on `drift_count > 0`);
  * a new / updated `slsa-drift` GitHub issue on the
    scheduled trigger.
* `docs/slsa-drift-detection.md` (NEW) documents the
  algorithm, allow-list rationale, false-positive
  handling, failure semantics, and the operator runbook
  for landing pin rewrites on drift.

### 3. SignalR sticky-session shared-cookie validation contract

* `infra/k8s/base/ingress-validation.yaml` (NEW).
  Kyverno `ClusterPolicy` (`validate-signalr-sticky-
  session`) with FIVE invariant sub-rules:
  1. `require-affinity-cookie` — asserts
     `nginx.ingress.kubernetes.io/affinity: "cookie"`.
  2. `require-affinity-mode-persistent` — asserts
     `affinity-mode: "persistent"`.
  3. `require-session-cookie-name-mahjong-aff` — asserts
     `session-cookie-name: "mahjong_aff"`.
  4. `require-session-cookie-max-age-86400` — asserts
     `session-cookie-max-age: "86400"` (24h).
  5. `require-ip-hash-fallback-snippet` — asserts the
     `configuration-snippet` annotation is present
     (W22 launch uses `?*` wildcard; W23+ may tighten).
* W22 launch in Audit mode + `failurePolicy: Ignore` —
  mirrors the W19/W21 audit→enforce cadence. W23
  enforce-flip planned.
* Pre-W22 verification confirms `infra/k8s/base/ingress.
  yaml` satisfies ALL FIVE sub-rules. The audit window
  opens with a zero-violation baseline.
* `docs/signalr-affinity-validation-w22.md` (NEW)
  documents each invariant, the W19 hardening
  cross-reference, the audit-mode rationale, the W23
  enforce-flip plan, and the cutover-day synthetic
  admission-deny smoke shape.

### 4. Mobile build matrix expansion — tvOS + watchOS jobs

* `.github/workflows/mobile-build.yml` extended with
  `tvos-build` + `watchos-build` jobs (between `ios-e2e`
  and `release`). Both:
  * Run on `macos-latest` with `timeout-minutes: 45`.
  * Reuse the W18 iOS keychain decode secret shape
    (`IOS_DEV_CERT_BASE64` + 3 supporting secrets).
  * Carry a `gate` step that sets
    `secrets-present=true|false`; subsequent steps
    fork on that output.
  * Run `xcodebuild -configuration Release -sdk
    appletvos | watchos CODE_SIGNING_ALLOWED=NO` on the
    unsigned path.
  * Emit a placeholder text artefact when the
    Capacitor iOS shell isn't bootstrapped — same
    soft-fail shape as the W2 iOS job.
* `release:` job's `needs:` list intentionally unchanged
  at W22 (still `[android, android-e2e, ios, ios-e2e]`).
  W23 wires tvOS + watchOS into the prerelease.
* `docs/mobile-apple-platforms.md` (NEW) covers the
  matrix shape, skip-conditional logic, Apple Developer
  enrolment runbook (single combined profile vs
  per-platform), tvOS + watchOS architecture notes
  (tvOS arm64 only; watchOS arm64_32 + arm64), and the
  W23 hand-off (signed flow + `needs:` extension +
  Simulator smoke).

### 5. us-east-1 auto-rollback dry-run trigger workflow

* `.github/workflows/us-east-1-auto-rollback.yml` (NEW).
  Three triggers: `pull_request` (paths-filtered to the
  W21 `auto-rollback.tf` + `post-apply-smoke-test.sh`
  + this workflow file), weekly Sunday 02:00 UTC
  `schedule`, and `workflow_dispatch` with explicit
  `actually_rollback_on_failure` opt-in input.
* Three-job graph:
  1. `validate` — `terraform fmt -check` +
     `terraform validate` + `bash -n` on the smoke
     script.
  2. `dry-run-plan` — `terraform plan -target=
     null_resource.us_east_1_auto_rollback` with
     `enable_auto_rollback=true`; uploads plan
     artefact; posts PR comment with plan output on
     `pull_request` triggers.
  3. `auto-rollback-trigger` — runs only on
     `workflow_dispatch`; validates the trigger
     contract surface (the actual rollback fires from
     the W21 null_resource provisioner at terraform
     apply time, not from CI).
* `docs/us-east-1-auto-rollback-runbook.md` (NEW)
  documents the §3.1 → §3.2 → §3.3 staging-tier
  dry-run → prod opt-in sequence Stephen follows to
  enable the safety net on real us-east-1 applies.

### 6. CHANGELOG + version triple

* `CHANGELOG.md` — Unreleased section's working-branch
  reference rewritten from W21 → W22; new `[0.31.0]`
  entry lands above `[0.30.0]` with the full W22 theme
  paragraph (5 deliverables + CHANGELOG + mobile/
  package.json bump). Wave-count-tracks-version:
  W22 → 0.31.0 (W21=0.30.0; W11=0.20.0 anchor).
* `mobile/package.json` — `0.30.0` → `0.31.0`.
* Root `package.json` — DOES NOT EXIST in this repo
  (`find . -maxdepth 2 -name package.json -not -path
  "*/node_modules/*"` returns `./mobile/package.json`
  only). The CHANGELOG header is the version anchor.
  Bishop-lane csproj bump deferred.

## Lane-discipline notes

* All edits inside Apone-lane per
  [`tests/ci/lane-map.json`](../../tests/ci/lane-map.json)
  + the legacy classifier in
  [`tests/ci/check-cross-lane-bundling.sh`](../../tests/ci/check-cross-lane-bundling.sh):
  * `infra/k8s/base/kyverno-policies/*.yaml` → apone
  * `infra/k8s/base/ingress-validation.yaml` → apone
  * `.github/workflows/*.yml` → apone
  * `docs/*.md` → shared (legitimate)
  * `CHANGELOG.md` → shared (legitimate, primary apone)
  * `mobile/package.json` → unclassified (legitimate;
    no cross-lane violation)
* No edits to `src/backend/`, `src/frontend/`, or
  `tests/` directories. csproj NOT touched (Bishop
  lane).

## Stash-discipline notes (§9 STASH-ISOLATION respected)

* Used `git stash --include-untracked -m "apone-w22-
  baseline-${date}"` to set aside the W21 untracked
  fuse-temp file BEFORE entering the flock.
* NO `git stash pop` before commit (W20 Hicks-tree-wipe
  retro pattern honoured).
* Files staged BY NAME via `git add path/to/file1
  path/to/file2 ...` — no `git add -A`, no `git add -u`,
  no directory adds.
* `git diff --cached --name-only` run inside the flock
  before `git commit` confirms the apone-lane staging
  list.
* `.work/apone-w22-safe/` carries pre-edit backups of
  every modified file for revert convenience.

## Validation gate

* `actionlint .github/workflows/*.yml` — clean on all
  44+2 workflow files (W22 adds 2: `slsa-drift-
  detection.yml` + `us-east-1-auto-rollback.yml`).
* `kustomize build infra/k8s/overlays/prod/` — OK.
* `kustomize build infra/k8s/overlays/staging/` — OK.
* The new `infra/k8s/base/ingress-validation.yaml` is
  OUT-OF-BAND (not in `base/kustomization.yaml`) per
  the W19/W21 Kyverno ClusterPolicy pattern — kustomize
  build is unaffected.

## W22 → W23 hand-off candidates

* **Kyverno** — new audit-mode pair (`require-readonly-
  rootfs` + `disallow-privileged-containers`) at W23
  + audit window, then W24 enforce-flip.
* **SignalR ingress-validation** — W23 enforce-flip
  cutover for the 5-sub-rule policy + a 6th sub-rule
  asserting `session-cookie-secure: "true"` +
  `session-cookie-samesite: "Lax"`.
* **Mobile** — wire tvOS + watchOS into the `release:`
  job's `needs:` list once Stephen completes the Apple
  Developer enrolment for tvOS + watchOS distribution.
* **us-east-1 auto-rollback** — Stephen executes the
  §3.1 → §3.2 → §3.3 sequence over weeks 1-3 post-W22;
  prod opt-in lands once §3.3 captures a clean
  staging-tier dry-run.
* **SLSA drift-detection** — W23 may add additional
  allow-list entries when new reusable workflows with
  documented tag-pin trust shapes land in the repo.

## Cross-references

* W21 inbox memo (precedent shape): `.squad/decisions/
  apone-phase-k-wave-21.md` (W21 merged to main).
* W19 audit-mode launch + W20 enforce-flip:
  `docs/kyverno-w19-additional-rules.md`.
* W21 audit-mode launch (third-batch precursor):
  `docs/kyverno-w21-additional-rules.md`.
* W20 SLSA-3 sweep ledger: `docs/slsa-pinning-w20-
  sweep.md`.
* W19 SignalR cookie hardening:
  `docs/signalr-affinity-hardening-w19.md`.
* W21 SignalR observability hardening:
  `docs/signalr-observability-w21.md`.
* W19/W20/W21 us-east-1 apply runbook:
  `docs/us-east-1-apply-runbook.md`.
* W21 auto-rollback safety net:
  `infra/terraform/regional-eks/us-east-1/auto-
  rollback.tf`.
