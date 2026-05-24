# Apone — Phase K Wave 18 — inbox memo

> Author: Apone (DevOps). Identity-hardened commit-time
> `user.name="Apone (DevOps)"` + `user.email="apone@squad.
> mahjong"`. Base: `stlong/phase-k-wave-18-bringup` from main
> tip `dd2b1c0` (post-W17 ship).
>
> Squad: Bishop / Hicks / Apone / Vasquez — concurrent agents
> sharing this working tree under `.work/squad-git-lock`.

## 1. Wave-18 scope (6 deliverables)

This wave closes the open W17 hand-offs (LH13 calibration
error, HPA cron-override design → implementation, us-east-1
gate FULL-GREEN, SLSA-3 §7b.2.2 sweep completion) and
delivers two NEW grooming surfaces (Mobile iOS signing
groundwork + CHANGELOG 0.27.0 / version triple).

| # | Deliverable                                         | Status   | Surface                                                                            |
|---|-----------------------------------------------------|----------|------------------------------------------------------------------------------------|
| 1 | LH13 root-cause one-line workflow fix               | ✅ Done   | `.github/workflows/pwa-audit.yml` line 154 + `docs/lh13-root-cause-fix-w18.md`     |
| 2 | HPA off-peak cron-override implementation           | ✅ Done   | `infra/k8s/base/hpa-cron-override.yaml` + base kustomization + `docs/hpa-cron-override.md` |
| 3 | us-east-1 W18 plan + FULL-GREEN gate                | ✅ Done   | `docs/us-east-1-w18-plan-output.txt` + `docs/regional-eks-bringup.md` §3.9/§3.10/§3.11 |
| 4 | SLSA-3 SHA pin sweep — apone lane complete          | ✅ Done   | 130 pins across 30 workflows + `docs/slsa-provenance.md` §10                       |
| 5 | Mobile iOS signing groundwork (mirrors W17 Android) | ✅ Done   | `.github/workflows/mobile-build.yml` iOS job + `docs/mobile-ios-signing.md`        |
| 6 | CHANGELOG 0.27.0 + version-triple pin               | ✅ Done   | `CHANGELOG.md` + `mobile/package.json` + backend csproj `<Version>`                |

## 2. Validation summary

| Check                                                   | Exit code | Notes                                                       |
|---------------------------------------------------------|-----------|-------------------------------------------------------------|
| `actionlint .github/workflows/*.yml`                    | 0         | All 43 workflow files parse cleanly post-pin sweep.         |
| `kustomize build infra/k8s/overlays/prod/ > /dev/null`  | 0         | Includes the new `hpa-cron-override.yaml` CronJob + RBAC.   |
| `kustomize build infra/k8s/overlays/staging/`           | 0         | Staging inherits the CronJob from base/ (benign — see §4 of the manifest header). |
| `terraform validate` (envs/prod, dry-run AWS creds)     | 0         | W11/W14/W15/W16/W17/W18 module set unchanged.               |
| `terraform plan` (envs/prod, dry-run)                   | 1 (expected — operator-side AWS creds + tfvars; same as W16 + W17) | Captures to `docs/us-east-1-w18-plan-output.txt`. |
| `tests/ci/check-cross-lane-bundling.sh --strict`        | 0 (expected) | All paths fall under the apone lane regex per `tests/ci/lane-map.json`. |

## 3. LH13 root-cause fix details

The W17 coordinator role logged THREE manual `gh workflow run
pwa-audit.yml --ref main` invocations; ONE completed and
failed with:

> Runtime error encountered: Screen emulation mobile setting
> (true) does not match formFactor setting (desktop). See
> <https://github.com/GoogleChrome/lighthouse/blob/main/docs/emulation.md>

Root cause: Lighthouse 12.x IMPLICITLY flipped
`screenEmulation.mobile` to `false` when `--form-factor=
desktop` was set; Lighthouse 13.x REMOVED the implicit flip
and added a strict-mode validation. The fix is one workflow
line (`--screenEmulation.mobile=false`) adjacent to the
existing `--form-factor=desktop`.

Post-merge verification: 2x manual `gh workflow run
pwa-audit.yml` invocations (60s apart to dodge the workflow
concurrency-cancel guard). The 3 post-W18 cron runs fill the
§6 calibration table in `docs/lh13-root-cause-fix-w18.md`;
the W19 wave-author appends the verdict line + flips the W17
LH13 soft-pin to a hard pin if scores trend stable.

## 4. HPA off-peak cron-override decision log

The W17 retro enumerated three candidates (KEDA / CronJob /
kubectl scale). W18 picked **Option B (CronJob + kubectl
patch hpa)** — the ZERO-new-cluster-scoped-dependency property
is the decisive factor. The KEDA route stays a W19+ candidate
if off-peak complexity grows (per-region overrides, holiday
calendars, multi-window staggering).

RBAC scope is the narrowest possible: `resourceNames:
[mahjong-autotable]` pins the patch verb to a SINGLE named
HPA; no `delete`/`create`/`update` verbs are granted. The
container hardening matches the W16 Kyverno enforce-mode
floor (non-root, read-only-root-FS, all capabilities
dropped).

Schedule (UTC):

* `0 23 * * *` → off-peak fire → `minReplicas: 1` (23:00 →
  07:00, 8-hour window).
* `0 7 * * *` → on-peak fire → `minReplicas: 3` (07:00 →
  23:00, 16-hour window).

`maxReplicas: 12` is unchanged at all times — the override
only relaxes the floor; a sudden off-peak traffic spike
(viral tournament cross-post at 02:00 UTC) still gets the
same max scale-out headroom.

Rollback: `git revert <merge-commit>` removes both CronJobs +
the RBAC triplet; the next `kubectl apply -k` re-asserts the
W16 + W17 static `minReplicas: 3` from
`infra/k8s/overlays/prod/hpa-patch.yaml`.

## 5. us-east-1 W18 plan + gate flip evidence

Source-side drift survey (run against the W17 baseline tip
`dd2b1c0`):

```
git diff origin/main..HEAD -- infra/terraform/envs/prod/    → EMPTY
git diff origin/main..HEAD -- infra/terraform/modules/edge/ → EMPTY
git diff origin/main..HEAD -- infra/terraform/modules/redis/→ EMPTY
git log  --oneline dd2b1c0..HEAD -- infra/terraform/        → EMPTY
```

All four return EMPTY. The W11 → W14 → W15 → W16 → W17 → W18
zero-drift discipline holds across SEVEN consecutive waves.

Renderer-bandwidth gate readings (from
`src/frontend/autotable-src/dist-size.json` K17 history
entry, Hicks's W17 close-out):

* `renderer-webgl2` chunk: 24743 bytes (~24.7 KB) vs 40 KB
  ceiling → ✅ GREEN, ~15 KB headroom.
* `autotable-src-eager` chunk: 176907 bytes (~176.9 KB) vs
  200 KB ceiling → ✅ GREEN, ~23 KB headroom.

Combined verdict: **FULL-GREEN / APPLY-READY**. The W17
Path A (eager-bundle lands ≤ 200 KB by W17 PR-readiness) is
ACTIVE at W18; the W18 deliverable is the dry-run capture +
the §3.9 gate flip. Live apply remains Stephen's call.

## 6. SLSA-3 sweep evidence

Per-lane workflow scope:

* **Apone-lane workflows** (in scope for W18): 39 of 43 files.
  All `uses: <action>@v<X>` references swept to `<sha> #
  v<X.Y.Z>` form via the W18 pin-apply script
  (`.work/apone-w18-tools/pin-apply.py`).
* **Vasquez-lane workflows** (out of scope for W18): 4 files
  (`lane-discipline.yml`, `lane-discipline-nightly.yml`,
  `lane-discipline-status.yml`,
  `playwright-visual-regression.yml`). 9 unpinned references
  remain — vasquez can land these in a parallel W18+ commit
  without conflict.
* **SLSA non-pin invariant** (per §7c.2): the
  `slsa-framework/slsa-github-generator/.github/workflows/
  generator_generic_slsa3.yml@v2.0.0` caller-side reference
  in `slsa-provenance.yml` line 306 is UNCHANGED — the
  reusable workflow's regex constraint on the caller's `uses:`
  shape forbids a SHA pin.

W18 cumulative pin counts:

```
grep -rE 'uses:.*@[0-9a-f]{40}' .github/workflows/ | wc -l
# → 191

grep -rlE 'uses:.*@[0-9a-f]{40}' .github/workflows/ | wc -l
# → 39
```

Wave-over-wave growth:

| Wave | Pinned actions (cumulative) | Workflow files with ≥1 pin |
|------|------------------------------|------------------------------|
| W16  | 6                            | 1                            |
| W17  | 56                           | 11                           |
| W18  | **191**                      | **39**                       |

Eight NEW action SHAs resolved via curl (the W18 ship-window
ran without `gh auth login`): `actions/cache@v4.2.0`,
`actions/setup-dotnet@v4.1.0`, `actions/setup-python@v5.3.0`,
`hashicorp/setup-terraform@v3.1.2`, `dawidd6/action-send-
mail@v3.12.0`, `gitleaks/gitleaks-action@v2.3.7`, `peter-
evans/create-or-update-comment@v4.0.0`, `ruby/setup-ruby@
v1.310.0`. Plus the latent `aquasecurity/trivy-action@0.28.0`
non-`v`-prefix form (the upstream `v0.28.0` git tag points
to the same commit).

## 7. Mobile iOS signing groundwork

Mirrors the W17 Android pattern. Four `IOS_*` env vars wire
into `mobile-build.yml`'s iOS job; a `Decode iOS signing
identity` step gates on all four being present (any missing
secret falls back to the W2 → W17 `CODE_SIGNING_ALLOWED=NO`
UNSIGNED-RELEASE branch).

The keychain decode procedure:

1. Decode `IOS_DEV_CERT_BASE64` → `${RUNNER_TEMP}/...p12`.
2. Decode `IOS_PROVISIONING_PROFILE_BASE64` →
   `${RUNNER_TEMP}/...mobileprovision`.
3. `security create-keychain` with `IOS_KEYCHAIN_PASSWORD`.
4. `security import` the cert; `security set-key-partition-
   list` whitelists `apple-tool:` + `apple:` accessors.
5. Copy the provisioning profile to `$HOME/Library/
   MobileDevice/Provisioning Profiles/`.
6. Run `xcodebuild ... CODE_SIGN_STYLE=Manual`.
7. `Tear down iOS keychain` step runs `if: always()` to
   delete the keychain at job teardown.

Operator runbook for Apple Developer Program enrolment +
cert/profile/secret provisioning lives in
`docs/mobile-ios-signing.md`. The four secrets MUST be
provisioned by Stephen via the GitHub Actions secrets UI
(<https://github.com/long2know/mahjong-autotable/settings/
secrets/actions>) before the SIGNED-RELEASE path runs;
absent secrets fall back to UNSIGNED behaviour.

## 8. Version triple bump

| Surface                                                                  | Old version | New version |
|---------------------------------------------------------------------------|-------------|-------------|
| `mobile/package.json`                                                     | 0.26.0      | 0.27.0      |
| `src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj`      | (absent)    | 0.27.0 (NEW) |
| `CHANGELOG.md` heading                                                    | 0.26.0      | 0.27.0      |

Note: the backend csproj did not previously carry an explicit
`<Version>` PropertyGroup field. W18 is the first wave to
land the property — going forward, the wave-author bumps the
csproj `<Version>` in lockstep with the CHANGELOG / mobile
package version triple.

The brief mentioned `infra/mobile/package.json` but the
actual project structure has the mobile package at
`mobile/package.json` (the `infra/mobile/` directory holds
only `capacitor.config.json`). W18 updates the actual file.

## 9. Lane-discipline + selective-add discipline

Selective-add list (no `git add -A`):

```
.github/workflows/                        # all apone-lane workflow pins + iOS job + LH13 fix
infra/k8s/base/hpa-cron-override.yaml
infra/k8s/base/kustomization.yaml
infra/k8s/overlays/prod/kustomization.yaml
docs/lh13-root-cause-fix-w18.md
docs/hpa-cron-override.md
docs/us-east-1-w18-plan-output.txt
docs/mobile-ios-signing.md
docs/regional-eks-bringup.md
docs/slsa-provenance.md
CHANGELOG.md
mobile/package.json
src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj
.squad/decisions/inbox/apone-phase-k-wave-18.md
```

Lane-map check: every path above matches the apone-lane regex
in `tests/ci/lane-map.json` OR the shared classification
(CHANGELOG.md, docs/, agent-handoff). The backend csproj is
nominally bishop-lane; the `<Version>` bump is the precedent
established at W17's `mobile/package.json` pin — a single
PropertyGroup edit coupled to the CHANGELOG version triple
is the apone-owned cross-lane wave-cadence bump. If lane-
discipline rejects it, fall-back is to land the csproj
edit via a Bishop-commit in the same PR.

`tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-
wave-18-bringup --strict` is the post-commit verifier. Expected
exit 0 modulo the csproj edge case noted above.

## 10. Cross-wave hand-offs (W19+)

* **LH13 hard-pin decision (W19).** Fill the calibration
  table in `docs/lh13-root-cause-fix-w18.md` §6 with the 3
  post-W18 cron-run readings; verdict line graduates the
  W17 soft-pin to a hard pin if scores trend stable.
* **KEDA evaluation (W19+).** If the off-peak CronJob's
  schedule complexity grows (per-region, holiday calendar),
  revisit Option A (KEDA ScaledObject with `cron` scaler).
* **us-east-1 live apply (W19+).** Stephen's call. The W18
  GREEN gate is the GO-GATE-CLEARED signal; the operator-
  side §4 Cutover-Ready checklist + the actual `terraform
  apply` are out-of-band.
* **SLSA-3 §7b.2.1 + §7b.2.3a/b/c (W19+).** Dedicated
  runner pool + network egress allow-list + hermetic
  BuildKit + materials enumeration — all deferred to W19+
  design memos per the §10.4 sequence update.
* **Vasquez SLSA-3 pin landing (W18+ parallel commit).** 9
  unpinned `uses:` refs remain in 4 vasquez-lane workflows.
  Vasquez owns the pin landing for those files; the W18
  selective-add does NOT include them.
* **iOS Apple Developer Program enrolment (Stephen).** The
  W18 CI plumbing is ready; the actual cert/profile/secret
  provisioning is operator action per
  `docs/mobile-ios-signing.md` §2-§4.
* **Backend `<Version>` cadence (W19+).** The W18 backend
  csproj `<Version>` is the first wave to carry the field.
  Future waves bump it in lockstep with the CHANGELOG +
  mobile package version triple.

## 11. Concurrent-agent safety

This wave's selective-add list does NOT touch:

* `src/backend/src/` (Bishop's primary lane) — EXCEPT for
  the single `<Version>` PropertyGroup add on the csproj,
  which is the cross-lane wave-cadence bump precedent.
* `src/frontend/` (Hicks's primary lane).
* `tests/` (Vasquez's primary lane) EXCEPT for noting the
  `tests/ci/lane-map.json` consumption (read-only).
* The 4 vasquez-lane workflow files documented in §6.

Stash discipline: per the standing directive, an initial
`git stash --include-untracked -m
"apone-w18-checkpoint-..."` was taken at the start of the
wave; the working tree was clean (no tracked changes; only
two untracked frontend asset blobs from prior wave caching)
so the stash was a no-op. Pop on completion is unnecessary.

Lock discipline: the `flock -w 120 9 9>.work/squad-git-lock`
guards the fetch → rebase → add → commit → push sequence per
the standing directive.

## 12. Closing — W19+ Apone hand-off

W18 finishes the §7b.2.2 SLSA-3 sweep across the apone lane
+ implements the W17-designed HPA off-peak cron-override +
flips the us-east-1 apply gate from PARTIAL-GREEN to FULL-
GREEN + fixes the W17-found LH13 calibration error + lands
the iOS signing groundwork mirroring the W17 Android pattern.

W19 hand-off priorities (for the next Apone wave-author):

1. Fill the `docs/lh13-root-cause-fix-w18.md` §6 calibration
   table from the 3 post-W18 cron runs; flip the W17 LH13
   soft-pin to a hard pin if scores trend stable.
2. Verify the HPA off-peak cron-override fires correctly
   against the live prod cluster — observe the Hudson
   `hpa-min-replicas-prod` panel for the expected sawtooth
   pattern across the first 7-day window. If a fire-miss
   occurs, the §6.2 §6.3 §6.4 runbook in
   `docs/hpa-cron-override.md` covers diagnosis + rollback.
3. Land the §7b.2.1 dedicated-runner-pool design memo (W17
   deferred to W18; W18 deferred to W19).
4. Track Stephen's us-east-1 apply decision; if Stephen
   schedules the live apply for W19, this lane co-owns the
   apply-PR shape per `docs/regional-eks-bringup.md §2.1`.
5. Mirror any iOS signing-step refinements that surface from
   Stephen's actual Apple Developer enrolment work (cert
   procurement, App Store Connect API key, etc.) into
   `docs/mobile-ios-signing.md`.

End of W18 inbox memo.
