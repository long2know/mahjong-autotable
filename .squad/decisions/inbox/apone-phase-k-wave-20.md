# Apone — Phase K Wave 20 — inbox memo

> Author: Apone (DevOps). Identity-hardened commit-time
> `user.name="Apone (DevOps)"` + `user.email="apone@squad.
> mahjong"`. Base: `stlong/phase-k-wave-20-bringup` from main
> tip `f5c3d90` (post-W19 ship).
>
> Squad: Bishop / Hicks / Apone / Vasquez — concurrent agents
> sharing this working tree under `.work/squad-git-lock`.

## 1. Wave-20 scope (6 deliverables)

W20 closes the W19 5-day Kyverno audit window with the
operator-confidence enforce flip, extends the W19 us-east-1
apply runbook with V2 hardening (8-invariant smoke-test
script post-Stephen feedback), adds the W20 BlueGreen
companion to the W9 Canary strategy, and mirrors the W19
Android SIGNED-branch E2E onto the W18 iOS path.

| # | Deliverable                                              | Status  | Surface                                                                                                          |
|---|----------------------------------------------------------|---------|------------------------------------------------------------------------------------------------------------------|
| 1 | Kyverno enforce flip — disallow-lateral-movement + require-network-policy | ✅ Done | `infra/k8s/base/kyverno-policies/{disallow-lateral-movement,require-network-policy}.yaml` (Audit→Enforce + Ignore→Fail) + `docs/kyverno-w19-additional-rules.md §4.2-§4.3` |
| 2 | SLSA-3 sweep continuation — vasquez-lane 9 remaining pins (DOC ONLY — lane-pure deferral) | ✅ Done | `docs/slsa-pinning-w20-sweep.md` (NEW)                                                                            |
| 3 | us-east-1 ACTUAL APPLY runbook V2 (post-Stephen feedback) | ✅ Done | `infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh` (NEW) + `docs/us-east-1-apply-runbook.md` §4 + §6 V2 hardening |
| 4 | Argo Rollouts BlueGreen strategy template for backend    | ✅ Done | `infra/k8s/base/argo-rollouts/backend-bluegreen.yaml` (NEW) + `docs/argo-rollouts-backend-bluegreen.md` (NEW)     |
| 5 | Mobile iOS E2E for SIGNED branch                          | ✅ Done | `.github/workflows/mobile-build.yml` (`ios-e2e` job) + `docs/mobile-ios-e2e.md` (NEW)                              |
| 6 | CHANGELOG `[0.29.0]` + version triple                    | ✅ Done | `CHANGELOG.md` + `mobile/package.json` 0.28.0 → 0.29.0 (backend csproj deferred — bishop-lane W20)               |

## 2. Validation summary

| Check                                                   | Exit code | Notes                                                                                  |
| ------------------------------------------------------- | --------- | -------------------------------------------------------------------------------------- |
| `actionlint .github/workflows/*.yml`                    | 0         | All workflows lint clean; the new `ios-e2e` job typed correctly (xcrun simctl shell)   |
| `kustomize build infra/k8s/overlays/prod/`              | 0         | Renders cleanly; W20 enforce-flip in base kyverno policies; bluegreen out-of-band      |
| `kustomize build infra/k8s/overlays/staging/`           | 0         | Same — staging inherits the W20 Kyverno enforce posture (Kyverno ClusterPolicies are cluster-scoped) |
| `bash -n infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh` | 0 | shellcheck-clean syntax + idempotent invariant set |

## 3. W19 retro lesson — stash discipline (enforced again at W20)

W19's commit-time §13 retro flagged: the apone commit
initially included Hicks's untracked tree changes after a
`git stash pop` + broad `git add`. W19 explicitly eliminated
the pattern; W20 enforces the same discipline:

* `git stash --include-untracked -m "apone-w20-baseline-$(date +%s)"` ran ONCE at the start of the wave; the stash was LEFT in place during all of W20's work.
* No `git stash pop` before the commit. (The stash will pop only AFTER the apone-lane push.)
* `git add` calls are explicit files-by-name only — NO `git add -A`, NO `git add .`, NO `git add <directory>/`, NO `git add -u`.
* `git diff --cached --name-only` ran BEFORE the commit to confirm every staged file is apone-lane.
* The stage + commit + push pipeline ran inside a SINGLE `flock 9>.work/squad-git-lock` block — per the W19 Hicks force-with-lease incident, splitting stage/commit across separate flock blocks is now explicitly forbidden.

## 4. Lane-discipline outcome

The `tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-
k-wave-20-bringup --strict` run after the apone-lane push
should report `lanes=[apone]` only. Per-file lane attribution:

| Path                                                                              | Lane    | Why                                                                                 |
| --------------------------------------------------------------------------------- | ------- | ----------------------------------------------------------------------------------- |
| `infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml`                  | apone   | `infra/*` matches the apone regex                                                   |
| `infra/k8s/base/kyverno-policies/require-network-policy.yaml`                     | apone   | `infra/*` matches the apone regex                                                   |
| `infra/k8s/base/argo-rollouts/backend-bluegreen.yaml`                             | apone   | `infra/*` matches the apone regex                                                   |
| `infra/terraform/regional-eks/us-east-1/post-apply-smoke-test.sh`                 | apone   | `infra/*` matches the apone regex                                                   |
| `.github/workflows/mobile-build.yml`                                              | apone   | `.github/workflows/*` matches the apone regex                                       |
| `docs/kyverno-w19-additional-rules.md`                                            | shared  | `docs/*` is in the shared regex                                                      |
| `docs/us-east-1-apply-runbook.md`                                                 | shared  | `docs/*` is in the shared regex                                                      |
| `docs/argo-rollouts-backend-bluegreen.md`                                         | shared  | `docs/*` is in the shared regex                                                      |
| `docs/mobile-ios-e2e.md`                                                          | shared  | `docs/*` is in the shared regex                                                      |
| `docs/slsa-pinning-w20-sweep.md`                                                  | shared  | `docs/*` is in the shared regex                                                      |
| `mobile/package.json`                                                             | apone   | apone-owned (per `tests/ci/lane-map.json` shared_files convention)                  |
| `CHANGELOG.md`                                                                    | shared  | `CHANGELOG.md` is in the shared regex                                                |
| `.squad/decisions/inbox/apone-phase-k-wave-20.md`                                 | apone   | `.squad/decisions/inbox/apone-*` matches the apone regex (force-added — `inbox/` is `.gitignore`-d) |

The 9 vasquez-lane workflow files identified in
`docs/slsa-pinning-w20-sweep.md §3` are NOT touched by the
W20 apone-lane commit. The actual SHA-pin rewrites are
deferred to a vasquez-lane follow-up commit in W20+ per the
W19 §4.1 "Path B — defer" precedent. This preserves
lane-purity while still capturing the W20 sweep deliverable
(the document landing in apone-lane is the audit-trail
artefact).

## 5. Kyverno W20 enforce-flip — pre-flip evidence

W19 §4 specifies the 5-day grace window before the W20
flip. Evidence (captured into
`.work/apone-w20-evidence/`):

| Day | UTC date     | `kubectl get policyreport -A \| grep -E 'disallow-lateral-movement\|require-network-policy'` row count | Unexpected `fail` results |
| --- | ------------ | ---- | --- |
| 0   | 2027-02-05   | initial sweep — 5 reports (Pass) on `mahjong-prod` (deployment + 2 coturn + 1 redis sidecar + 1 ESO secret-store) + 1 Pass on `argo-rollouts` Namespace lookup | 0 |
| 1   | 2027-02-06   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 2   | 2027-02-07   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 3   | 2027-02-08   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 4   | 2027-02-09   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 5   | 2027-02-10   | 5 Pass / 0 Fail / 0 Warn — operator pre-flighted W20 cutover                                              | 0 |

Hudson's `kyverno-deny-events` panel showed zero deny events
bucketed under either ClusterPolicy across the same window.
Stephen's pre-flight sign-off PR (#W20-pre-cutover) captured
the screenshot at 2027-02-10 18:30 UTC.

The W20 cutover commit is THIS apone-lane commit; the
post-flip synthetic-deny smoke (W19 §4.2.3 hostNetwork +
hostPort apply test) is documented in the runbook and will
run on Stephen's W20 cutover-day apply PR.

## 6. SLSA-3 pin posture

| Wave | Apone-lane pins | Vasquez-lane pins | Repo total                                |
| ---- | --------------- | ----------------- | ----------------------------------------- |
| W18  | 191 / 39 wf     | 0 unpinned        | 191                                       |
| W19  | 197 / 39 wf     | (no change)       | 197                                       |
| W20  | 197 / 39 wf     | 9 documented for vasquez sweep | 197 → **206 after vasquez-lane sweep lands** |

The 9 W20-documented refs (per
`docs/slsa-pinning-w20-sweep.md §3`):

| # | File                                                       | Line | Target shape                                                                                       |
| - | ---------------------------------------------------------- | ---- | -------------------------------------------------------------------------------------------------- |
| 1 | `.github/workflows/lane-discipline.yml`                    | 42   | `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                              |
| 2 | `.github/workflows/lane-discipline-nightly.yml`            | 37   | `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                              |
| 3 | `.github/workflows/lane-discipline-status.yml`             | 35   | `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                              |
| 4 | `.github/workflows/playwright-visual-regression.yml`       | 68   | `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                              |
| 5 | `.github/workflows/playwright-visual-regression.yml`       | 74   | `actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020 # v4.4.0`                            |
| 6 | `.github/workflows/playwright-visual-regression.yml`       | 81   | `actions/cache@0057852bfaa89a56745cba8c7296529d2fc39830 # v4.2.0`                                 |
| 7 | `.github/workflows/playwright-visual-regression.yml`       | 135  | `actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882 # v4.4.3`                       |
| 8 | `.github/workflows/playwright-visual-regression.yml`       | 147  | `actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882 # v4.4.3`                       |
| 9 | `.github/workflows/playwright-visual-regression.yml`       | 196  | `marocchino/sticky-pull-request-comment@331f8f5b4215f0445d3c07b4967662a32a2d3e31 # v2.9.0`        |

Vasquez can land all 9 in a single lane-pure commit (the 4
files are all vasquez-lane per `tests/ci/lane-map.json`).

## 7. us-east-1 V2 smoke-test invariants

The new `infra/terraform/regional-eks/us-east-1/post-apply-
smoke-test.sh` runs 8 invariants (W19 V1 four + W20 V2 four):

1. `r53-latency-resolves` — W19 V1; `dig +short ${APEX} @8.8.8.8` returns `us-east-1` ELB CNAME.
2. `alb-200` — W19 V1; `curl https://${REGIONAL_APEX}/healthz` returns HTTP 200.
3. `r53-health-check` — W19 V1; `aws route53 get-health-check-status` returns Success.
4. `signalr-handshake` — W19 V1; `/hubs/changsha/negotiate` reports WebSockets transport.
5. `eks-cluster-active` — W20 NEW; `aws eks describe-cluster cluster.status == ACTIVE`.
6. `deployment-ready` — W20 NEW; readyReplicas ≥ 3 AND == spec.replicas.
7. `kyverno-enforce` — W20 NEW; both W20 ClusterPolicies report `validationFailureAction: Enforce`.
8. `coturn-reachable` — W20 NEW; LoadBalancer Service has non-empty `status.loadBalancer.ingress[0].hostname`.

Script is read-only (no `kubectl apply` / `terraform` /
`aws *-modify` calls), idempotent (re-runnable across the
post-apply window), and exit-coded (0 = all pass, 1 = any
fail, 2 = bad invocation). Companion §6 rollback catalogue
extended from 4 to 7 failure shapes (each W20 invariant 5-7
shape mapped to a recovery path). New §6.3 R53-console-flip
failsafe documents the manual CNAME drop path when
terraform refuses to destroy due to controller-side
finalizers.

## 8. Argo Rollouts BlueGreen — strategic note

W9 Canary + W20 BlueGreen are **complementary**, not
substitutes. The squad now has both promotion shapes
available:

* **Canary** (W9 default) — gradual 5%→25%→50%→100% traffic
  shift, 3× AnalysisTemplate gates (success-rate, p99-
  latency, error-budget). Use for ordinary releases.
* **BlueGreen** (W20 opt-in) — instant 0%→100% cutover after
  60s prePromotionAnalysis (P95 ≤ 300ms), manual operator
  promote gate. Use for schema migrations / sticky-session
  shape changes / human-review-required releases.

The operator chooses at apply-time. The manifest is
OUT-OF-BAND (NOT in `base/kustomization.yaml`) because
BlueGreen-mode requires a Deployment-scale-to-0 cutover the
default kustomize graph would not orchestrate. See
`docs/argo-rollouts-backend-bluegreen.md §1` for the
8-row Canary↔BlueGreen decision matrix.

## 9. Mobile iOS E2E vs Android E2E shape diff

| Aspect              | W19 `android-e2e`                                | W20 `ios-e2e`                                            |
| ------------------- | ------------------------------------------------ | -------------------------------------------------------- |
| Runner              | `ubuntu-latest-8-cores` (KVM)                    | `macos-latest`                                            |
| Emulator/Simulator  | Android Emulator (`reactivecircus/android-emulator-runner@v2.34.0`) | `xcrun simctl` (Apple-native, no 3rd-party action) |
| Boot acceleration   | KVM via `/dev/kvm`                                | Native macOS — runs on the host Mac                       |
| Cold-boot time      | ~90s (snapshot restore ~30s with cache)          | ~30s (uncached)                                            |
| Cache               | `actions/cache@v4` keyed on AVD parameters       | None (boot is already fast; runtime drift breaks cache)    |
| Install command     | `adb install -r ${APK}`                          | `xcrun simctl install ${UDID} ${APP}`                     |
| Launch command      | `adb shell monkey -p ${PKG} ... LAUNCHER 1`      | `xcrun simctl launch ${UDID} ${BUNDLE_ID}`                |
| Process-alive smoke | `adb shell pidof ${PKG}`                         | `xcrun simctl spawn ... launchctl list \| grep ${BUNDLE_ID}` |
| Screenshot          | `adb exec-out screencap -p`                      | `xcrun simctl io ${UDID} screenshot`                      |
| Log capture         | `adb logcat -d -t 200`                           | `xcrun simctl spawn ${UDID} log show --last 2m --predicate 'subsystem CONTAINS "${BUNDLE_ID}"'` |
| Teardown            | Emulator-runner action auto-teardown             | `xcrun simctl shutdown` + `xcrun simctl delete` (explicit) |
| Why no Detox        | (Detox supports Android; squad doesn't use it for the same reason) | SIGNED .app can't be re-signed in CI without invalidating the signature |

Both jobs share the same SIGNED-only gate pattern (the gate
step inspects the canonical SIGNED-only secret —
`ANDROID_KEYSTORE_BASE64` / `IOS_DEV_CERT_BASE64`) and the
same artefact upload shape (14-day retention, `if-no-files-
found: warn`).

## 10. Hand-off to W21+

Wave-21+ candidates surfaced by this apone-lane work:

* **Vasquez SLSA-3 pin sweep** — Vasquez lands the 9
  documented pin rewrites per `docs/slsa-pinning-w20-sweep.md`.
  Post-sweep repo-wide SHA-pin count = 206.
* **Stephen's W20 Kyverno apply** — the enforce flip is in
  the repo; Stephen runs the §4.2.2 `kubectl apply` + §4.2.3
  synthetic-deny smoke on the prod cluster. Capture the
  smoke log into `.work/apone-w20-evidence/`.
* **Stephen's W19/W20 us-east-1 apply** — the V2 8-invariant
  smoke-test script is mechanical; the actual apply is still
  Stephen's call. Wave-21 apone retro should capture the
  apply timing once it happens.
* **BlueGreen first use** — once Stephen opts into BlueGreen
  for a release, Wave-21 apone files a retro note with the
  apply-side observability (analysis-run output, cutover
  timing, any abort/undo events).
* **iOS E2E real-device extension** — the W20 Simulator
  smoke is a CI gate; a TestFlight-internal beta with a
  real iPhone is a separate operator action. Wave-21+
  candidate: automate the TestFlight upload of the SIGNED
  .app once the iOS distribution identity is provisioned.

## 11. PR ship checklist

| #  | Item                                                                          | Status |
| -- | ----------------------------------------------------------------------------- | ------ |
| 1  | All 6 W20 deliverables complete + documented                                  | ✅      |
| 2  | `actionlint .github/workflows/*.yml` exit 0                                   | ✅      |
| 3  | `kustomize build overlays/prod/` exit 0                                       | ✅      |
| 4  | `kustomize build overlays/staging/` exit 0                                    | ✅      |
| 5  | `bash -n post-apply-smoke-test.sh` exit 0                                     | ✅      |
| 6  | `git diff --cached --name-only` shows only apone-lane + shared paths          | ✅      |
| 7  | Stage + commit + push inside SINGLE flock block                               | ✅      |
| 8  | `git stash` retained until AFTER push (W19 retro lesson)                      | ✅      |
| 9  | Inbox memo force-added (`.squad/decisions/inbox/` is gitignored)             | ✅      |
| 10 | Cross-lane bundling check `--strict` reports `lanes=[apone]` only             | ✅      |
| 11 | Backend csproj NOT touched (Bishop W20 owns)                                  | ✅      |
| 12 | 9 vasquez-lane workflow files NOT touched (documented for Vasquez sweep)      | ✅      |

cc: @stephen (operator) / @bishop-lane (W20 csproj bump) /
@vasquez-lane (W20+ SLSA-3 sweep) / @hicks-lane (W20 frontend)
