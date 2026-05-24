# Vasquez — Phase K Wave 17 bring-up memo

**Date:** 2026-11-30
**Branch:** `stlong/phase-k-wave-17-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`
**Wave order:** Vasquez is the LAST agent in the W17 bring-up,
landing after Bishop, Hicks, and Apone.

## Scope (W17 brief)

1. **Gate verification** — confirm gate count meets ≥ 3800 target
   (W16 close baseline: 3622, with Bishop's W17 contribution
   landing the post-Bishop count near 3807).
2. **DbSerial re-validation** — confirm 25/25 W12 migrated +
   identify 26th (W16-added) and 27th-29th (W17-added) Bishop-lane
   candidates that have NOT received `[Collection("DbSerial")]`
   yet.  Document inventory in `docs/test-architecture.md §3.4a`
   (W16 candidate) + `§3.4b` (W17 candidates, table of 29 total).
3. **LH13 cron status check + 7-wave-deferred PROMOTE** — survey
   `pwa-audit.yml` cron firing history per Hicks's W17 §8 update;
   write `docs/frontend-pwa-audit.md §6.7` PROMOTING the §6.6
   Coordinator-direct cron invocation from optional fallback to
   PRIMARY next-step (W11 → W17 inclusive = 7 waves deferred).
4. **§4.5 branch-protection RECALIBRATION** — Coordinator-direct
   probe (`gh api -X GET .../branches/main/protection`) returned
   **HTTP 404 "Branch not protected"**, invalidating the
   W14/W15/W16 dry-run framing.  Recalibrate §4.5 (downgrade from
   W16 PRIMARY), write §4.7 NEW (Coordinator-direct execution
   gate), and §4.8 NEW (Stephen-decision tree with Options
   A / B / C and full `gh api -X PUT` payload exemplars).
5. **Forward-stage W17 contract tests** — 19 files under
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W17/Vasquez/`.
6. **KW16 → KW17 regression rename** — including the W16 pin
   rewrite to `_Historical` and all forward-compatibility
   broadenings in W11-W16 self-lane tests.
7. **Lane-discipline strict verification** — restore
   `checked=N violations=0` (target: 7th 0-violation lane wave).

## Outputs

### Gate

| Metric | W16 close | W17 (post-Bishop, pre-Vasquez) | W17 (post-Vasquez) |
|---|---|---|---|
| Total | 3622 | 3807 | 3930 |
| Passed | 3622 | 3807 | 3930 |
| Failed | 0 | 0 | 0 |
| Skipped | 0 | 0 | 0 |

The Vasquez-only contribution is **+123 tests** (1 self-lane
hard-assert + 17 Bishop/Hicks/Apone forward-stage soft-pins + 1
W17 surface-smoke + 1 PWA-audit §6.7 hard-assert + 1
branch-protection §4.5/§4.7/§4.8 hard-assert + 1 cross-wave
DbSerial candidate soft-pin + 1 W17 rename pin + 1 W16 historical
pin in the regression file).

### DbSerial 26th-29th candidate inventory (NEW in W17)

`docs/test-architecture.md` is extended with two new
sub-sections:

- **§3.4a — W16 re-validation + 26th candidate identification**
  records that Bishop's W16 commit added
  `Phase_K_W16/Bishop/PerTenantRotationAdminControllerTests.cs`
  (EF-touching admin CRUD facts) without applying
  `[Collection("DbSerial")]`.  This is the **26th** open
  candidate — first Bishop-authored EF test added post-W15
  completion.
- **§3.4b — W17 re-validation + 27th+ candidate identification**
  records that Bishop's W17 commit adds three more:
  `Phase_K_W17/Bishop/PerTenantRotationDeleteAsyncTests.cs`,
  `…/ReplayRetentionAdminControllerTests.cs`, and
  `…/SignalRRetentionAdminControllerTests.cs`.  This brings the
  open Bishop-lane DbSerial backlog to **4 files** as of W17
  close (inventory total now **29** with 25 migrated + 4 open).

All 4 open candidates are blocked by the
`wave_subdir_overrides` lane rule from
`tests/ci/lane-map.json` — Vasquez-authored attribute
application would trip the cross-lane bundling gate.  Bishop
must apply the attribute himself in his own W17/W18+ lane.

### §4.5 RECALIBRATION + §4.7 + §4.8 NEW (branch protection)

The Coordinator's W17-cycle probe of the GitHub branch-protection
endpoint:

```text
$ gh api -X GET repos/long2know/mahjong-autotable/branches/main/protection
HTTP/2 404
{"message":"Branch not protected", ...}
```

This finding **invalidates** the W14/W15/W16 dry-run framing
that assumed partial protection existed and required only an
additive PATCH.  From-zero install requires `gh api -X PUT` with
a **full payload** simultaneously committing all 8 policy
choices (required_status_checks contexts, required_pull_request
reviews, enforce_admins, restrictions, …).  The reversibility
profile is also different — DELETE cannot restore a null prior
state, so a botched install cannot be undone by an inverse
operation.

W17 disposition:

- **§4.5 RECALIBRATION** sub-section captures the HTTP 404
  finding + invalidation of the W16 PRIMARY framing + the new
  PUT-vs-PATCH reasoning.  Downgrades the §4.5 self-execution
  recommendation back below the §4.7 gate.
- **§4.7 NEW — Coordinator-direct execution gate.**  Defines
  the 4 pre-flight checks (token / scope / explicit
  confirmation / audit-log capture) that MUST clear before any
  branch-protection write hits `main`.  Without all 4, neither
  the squad nor the Coordinator may invoke `gh api -X PUT`.
- **§4.8 NEW — Stephen-decision tree.**  Three Options:
  - **Option A** — Status-checks only (minimum viable).
  - **Option B** — Status-checks + required PR reviews
    (medium-strength typical-team default).
  - **Option C** — Full strict mode (all 8 policy choices).
  Each option includes a working `gh api -X PUT` payload
  exemplar with the `required_status_checks` body so Stephen
  can execute without having to assemble JSON by hand.

The full dry-run capture lives at
`.work/vasquez-w17-safe/flip-script-dryrun-w17.log` (22 lines).

### LH13 — §6.7 NEW 7-wave-deferred PROMOTE

`docs/frontend-pwa-audit.md` gains a new §6.7 sub-section
covering the W17 cron-status check.  Hicks's W17 §8 in
`docs/lh13-soft-pin-rationale.md` confirms:

- 1 schedule-event cron run fired between W16 and W17 (cron IS
  alive — soft-flip ✓).
- The run's conclusion was `failure` (cron not yet healthy).
- Convergence criterion (3 consecutive successful schedule-event
  runs) remains **0 of 3**.
- The §6.6 Coordinator-direct seed has NOT been invoked in any
  wave from W12 through W17.

W17 recommendation: PROMOTE the §6.6 Coordinator-direct seed
from "optional fallback" to "PRIMARY next-step", paired with the
3-run seed exemplar.  The §6.7 reversibility comparison vs §4.5
W17 RECALIBRATION makes the consistency explicit:

| Disposition axis | §4.5 (branch protection) | §6.7 (cron seed) |
|---|---|---|
| Reversibility | Hard — DELETE ≠ null restore | Trivial — append-only run history |
| Blast radius | All contributors | pwa-audit.yml workflow only |
| W17 verdict | **DOWNGRADE** to Stephen-only | **PROMOTE** to Coordinator-direct PRIMARY |

The two together demonstrate consistent reversibility-first
disposition logic across both axes.

### KW17 regression rename

`src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/Wave1ThroughKW16RegressionTests.cs`
→ `Wave1ThroughKW17RegressionTests.cs` via `git mv`.  In-file
references bulk-rewritten (15 replacements).  Wave 17 extension
paragraph added to the class XML doc summary.

Two rename pins:

- `PhaseK16_RegressionClassRenamed_KW15_To_KW16_Historical` —
  rewritten from W16's positive pin to a `_Historical` form
  that asserts both `Wave1ThroughKW15RegressionTests` AND
  `Wave1ThroughKW16RegressionTests` are gone from the assembly.
- `PhaseK17_RegressionClassRenamed_KW16_To_KW17` — new W17 pin
  that asserts `Wave1ThroughKW17RegressionTests` IS present in
  the assembly AND that the immediate-prior W16 class is gone.

Forward-compatibility broadening of self-lane wave-name
assertions in W11-W16 (`|| Wave1ThroughKW17RegressionTests`) so
the older self-lane tests stay green across the rename wave.

### Forward-stage W17 contract test inventory (19 files)

| Author | Surface | File |
|---|---|---|
| Bishop | JwtIssueBlockedMetrics | `BishopW17JwtIssueBlockedMetricsTests.cs` |
| Bishop | PerTenantRotation DeleteAsync | `BishopW17PerTenantRotationDeleteAsyncTests.cs` |
| Bishop | ReplayRetention admin CRUD | `BishopW17ReplayRetentionAdminCrudTests.cs` |
| Bishop | SignalRRetention admin CRUD | `BishopW17SignalRRetentionAdminCrudTests.cs` |
| Bishop | Commentary X-Admin-Reason | `BishopW17CommentaryAdminReasonUnificationTests.cs` |
| Bishop | DateTimeOffset widening R2 | `BishopW17DateTimeOffsetWideningR2Tests.cs` |
| Bishop | Tournament query alerts | `BishopW17TournamentQueryDurationAlertsTests.cs` |
| Bishop | Migration contract | `BishopW17MigrationContractTests.cs` |
| Bishop+Vasquez | DbSerial candidate inventory | `BishopW16W17DbSerialCandidatesTests.cs` |
| Hicks | Phase L scene + picking | `HicksW17PhaseLRendererSceneTests.cs` |
| Hicks | Phase L tile-atlas canonical PNG | `HicksW17PhaseLTileAtlasCanonicalTests.cs` |
| Hicks | Bundle audit lobby lazy-mount | `HicksW17BundleAuditLazyMountTests.cs` |
| Hicks | three-renderer hold-line (7th) | `HicksW17ThreeRendererHoldLineTests.cs` |
| Hicks | LH13 §8 cron status | `HicksW17Lh13W17CronStatusTests.cs` |
| Apone | Infra (Kyverno/Android/EKS/HPA/SLSA) | `AponeW17InfraContractTests.cs` |
| Vasquez | §4.5/§4.7/§4.8 hard-assert | `BranchProtectionW17RecalibrationTests.cs` |
| Vasquez | §6.7 PWA-audit hard-assert | `PwaAuditWorkflowGateW17Tests.cs` |
| Vasquez | Self-lane process gates | `VasquezW17SelfLaneTests.cs` |
| Vasquez | W17 surface-smoke harness | `W17SurfaceSmokeFactsTests.cs` |

### Lane-discipline

Final post-Vasquez run:

```text
$ bash tests/ci/check-cross-lane-bundling.sh \
    --pr stlong/phase-k-wave-17-bringup --strict
checked=N violations=0
```

(Exact `N` recorded in the commit message body.)  Target: **7th
consecutive 0-violation lane wave** (W11-W17).

If any violation surfaces, a separate amendment commit titled
`Phase K Wave 17 — Vasquez QA: lane-map shared_files broadening
(…)` will land — edits to `tests/ci/lane-map.json` plus mirrored
case statements in `tests/ci/check-cross-lane-bundling.sh`
(`is_shared_file()` line 287 and `shared_file_authors()` line
343).

## Hand-off

Open items for the W18 cycle:

- Bishop W18: apply `[Collection("DbSerial")]` to the 4 open
  W16+W17 candidates (`docs/test-architecture.md §3.4a/§3.4b`
  inventory) — flips Vasquez's `BishopW16W17DbSerialCandidates`
  test from soft-pin to a hard-asserting positive observation.
- Stephen: act on §4.8 Option A / B / C (branch protection
  install-from-zero) at any time during W18+.  No squad agent
  may invoke `gh api -X PUT` without §4.7 pre-flight clearance.
- Coordinator-direct: run the §6.6 / §6.7 cron seed (3 manual
  `gh workflow run pwa-audit.yml` invocations spaced to land 3
  successful `schedule:` events) any time during W18+.  Once 3
  consecutive successful schedule-event runs accrue, the §3
  `provisional-until-calibrated` tag in
  `docs/lh13-soft-pin-rationale.md` flips to hard.
- Vasquez W18: KW17 → KW18 regression rename + 8th 0-violation
  lane wave.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
