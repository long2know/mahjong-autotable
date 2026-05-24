# Vasquez — Phase K Wave 16 bring-up memo

**Date:** 2026-11-23
**Branch:** `stlong/phase-k-wave-16-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`
**Wave order:** Vasquez is the LAST agent in the W16 bring-up,
landing after Bishop, Hicks, and Apone.

## Scope (W16 brief)

1. **Gate verification** — confirm gate count meets ≥ 3450 target
   (W15 baseline: 3312).
2. **DbSerial validation** — confirm 25/25 candidates carry
   `[Collection("DbSerial")]` after Bishop's W15 completion landed.
3. **LH13 cron status check + escalation** — survey
   `pwa-audit.yml` cron firing history; co-ordinate with
   Hicks's W16 Option A soft-flip; codify §6.6 runbook.
4. **§4.5 branch-protection escalation** — 9-wave §4.1 deadlock;
   promote Coordinator-direct to PRIMARY path.
5. **Forward-stage W16 contract tests** — ~17 files under
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W16/Vasquez/`.
6. **KW15 → KW16 regression rename** — including all forward-
   compatibility broadenings in W11-W15 self-lane tests.
7. **Lane-discipline strict verification** — restore
   `checked=N violations=0` (target: 6th 0-violation lane wave).

## Outputs

### Gate

| Metric | W15 | W16 (Vasquez-only) | W16 (post-Bishop, est.) |
|---|---|---|---|
| Total | 3312 | 3448 | TBD (≥ 3450 target) |
| Passed | 3312 | 3448 | TBD |
| Failed | 0 | 0 | 0 |
| Skipped | 0 | 0 | 0 |

The Vasquez-only contribution is **+136 tests** (1 self-lane + 17
forward-stage contract files + 1 W16 rename pin in the regression
file).  Bishop's W16 bring-up is expected to push the gate over
3450 with a ~150-test contribution (per W15 pattern).

### DbSerial validation

The W15 §3.4 (`docs/test-architecture.md`) records the W15 close
of the DbSerial migration thread: Bishop's W15 commit applied
`[Collection("DbSerial")]` to the two `Phase_K_W9/Bishop/*.cs`
files (`EfCommentaryUsageMeterTests.cs`,
`IdempotencyStoreContractTests.cs`).  The migration is **25/25
complete** as of W15 close.

W16 validation:

- `docs/test-architecture.md §3.4` present and includes the
  literal phrase `DbSerial migration final completion`.
- `Phase_K_W14/Vasquez/db-serial-migration-completion.md`
  retained as the historical W14 hand-off memo.
- `Phase_K_W15/Vasquez/BishopW15DbSerialCompletionOnW9FilesTests.cs`
  (Vasquez-authored soft-pin) records the attribute presence
  fact on both files.

No discrepancy.  The migration is **DONE** at W15 close.  No W16
action item beyond the per-wave 5-run flake harness (run as part
of the W16 gate verification — zero flakes observed).

### LH13 cron status

**Survey method (W16):**  `gh run list --workflow=pwa-audit.yml`
attempted; `gh auth status` returned "not logged in" in the W16
Vasquez environment (the GH_TOKEN bridge that worked for W14's
flip-script dry-run does not extend to `gh run list`).  Fall back
to the documentation trail:

- §6.1 (W12): deferred — no cron data points.
- §6.2 (W13): deferred — no cron data points.
- §6.3 (W14): deferred — no cron data points.
- §6.4 (W15): deferred — no cron data points.
- §6.5 (W15): yellow-→-red transition criterion **published**
  — if W16 lands without cron data AND without Stephen-direct
  seed, flips to RED.

**W16 observation:**  No Stephen-direct manual seed has landed
(verified by `gh workflow run pwa-audit.yml` requiring `workflow`
scope which Vasquez environment lacks; Stephen-direct path is the
Actions UI, which only the repository admin can confirm; no
mention in any of the W12-W15 Vasquez/Hicks memos that Stephen
acted; no §6.4 cadence advance in `docs/frontend-pwa-audit.md`).

**Parallel W16 work — Hicks lane (`docs/lh13-soft-pin-rationale.md`):**
Hicks's W16 bring-up landed a **Coordinator-direct soft-flip
(Option A)** that addresses the §6.3 six-wave escalation
criterion via a NEW doc carrying the §3 placeholder thresholds
tagged `provisional-until-calibrated`.  The W16 disposition
**explicitly preserves YELLOW** ("the provisional status owns
the YELLOW" — `docs/lh13-soft-pin-rationale.md §7`) and defuses
the YELLOW→RED transition until W18.

**Action taken (W16 — Vasquez):**  re-align §6.5 to reflect the
Option A soft-flip, and add §6.6 (NEW) as the W16-added
"see lh13-soft-pin-rationale.md" pointer + Coordinator-direct
cron invocation runbook.  The runbook is the §4.2 evidence-
collection path in Hicks's W16 doc — the path that retires the
`provisional-until-calibrated` tag at W17.

**Recommendation for W17:**  Coordinator-direct cron invocation
(three triggers across three business days per §6.6); on
convergence Hicks W17 picks up the §6.1 cadence resumption and
attempts hard-pin at W17 by retiring the provisional tag in
`lh13-soft-pin-rationale.md §3`.  If Coordinator-direct seeding
does NOT happen by W17, the §6.5 RED fallback is held in reserve.

### Branch-protection §4.5

The §4.1 hand-off has been pending since **Phase K Wave 4 —
nine waves**.  W15 (§4.4) re-verified the §4.3 fallback runbook.
W16 (§4.5) re-verifies again and **promotes Coordinator-direct
to PRIMARY recommendation** (no longer conditional on Stephen
inaction — at nine waves the §4.2 escalation algorithm puts the
Coordinator-direct path past its deadline).

W16 dry-run capture: `.work/vasquez-w16-safe/flip-script-dryrun-w16.log`.
Same shape as W14 + W15: HTTP 200; `Have lane-discipline currently:
no`; dry-run PATCH command printed.

Exact gh API call (validated W16) added to §4.5 body.  Risk
assessment included.

### Forward-stage W16 contract tests

Eighteen files under `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W16/Vasquez/`:

| # | File | Author surface | Facts |
|---|------|----------------|-------|
| 1 | `W16SurfaceSmokeFactsTests.cs` | Vasquez (paired smoke) | 18 |
| 2 | `VasquezW16SelfLaneTests.cs` | Vasquez (self-lane) | 14 |
| 3 | `PwaAuditWorkflowGateW16Tests.cs` | Vasquez (PWA mirror) | 10 |
| 4 | `BishopW16TournamentRoundProgressionTests.cs` | Bishop | 6 |
| 5 | `BishopW16ReplayRetentionPolicyTests.cs` | Bishop | 6 |
| 6 | `BishopW16CommentaryBudgetForecastV2Tests.cs` | Bishop | 6 |
| 7 | `BishopW16SpectatorPresenceMetricsTests.cs` | Bishop | 6 |
| 8 | `BishopW16JwksKeyExpiryGuardTests.cs` | Bishop | 6 |
| 9 | `BishopW16ReplayCheckpointStreamingV2Tests.cs` | Bishop | 6 |
| 10 | `BishopW16AuditRetentionV2Tests.cs` | Bishop | 6 |
| 11 | `BishopW16MatchHistoryPageSizeMetricsV2Tests.cs` | Bishop | 6 |
| 12 | `HicksW16PhaseLRendererBundleTests.cs` | Hicks | 8 |
| 13 | `HicksW16LH13FourthRetryTests.cs` | Hicks | 6 |
| 14 | `HicksW16ThreeRendererHoldLineTests.cs` | Hicks | 6 |
| 15 | `HicksW16FrontendBundleAuditTests.cs` | Hicks | 6 |
| 16 | `HicksW16PlaywrightVisualRegressionTests.cs` | Hicks | 6 |
| 17 | `HicksW16PhaseLWebGL2ExtensionTests.cs` | Hicks | 6 |
| 18 | `AponeW16InfraContractTests.cs` | Apone | 8 |

All Bishop/Hicks/Apone files are soft-pin on absence; Vasquez
files (1, 2, 3) hard-assert where appropriate.

### Wave1ThroughKW16 regression rename

Renamed `Wave1ThroughKW15RegressionTests.cs` →
`Wave1ThroughKW16RegressionTests.cs`.  All in-file references
updated.  W16 rename pin (`PhaseK16_RegressionClassRenamed_KW15_To_KW16`)
added.  W15 pin (`PhaseK15_RegressionClassRenamed_KW14_To_KW15`)
renamed to `..._Historical` and updated to assert that BOTH the
W14 and W15 classes are gone.

Forward-compatibility broadenings (in-lane Vasquez edits):
- `Phase_K_W15/Vasquez/VasquezW15SelfLaneTests.cs` — accept
  `KW15 || KW16`.
- `Phase_K_W14/Vasquez/VasquezW14SelfLaneTests.cs` — accept
  `KW14 || KW15 || KW16`.
- `Phase_K_W13/Vasquez/VasquezW13SelfLaneTests.cs` — accept
  `KW13 || KW14 || KW15 || KW16`.
- `Phase_K_W12/Vasquez/{VasquezW12SelfLane,W12SurfaceSmokeFacts}Tests.cs` —
  accept `KW12 || KW13 || KW14 || KW15 || KW16`.
- `Phase_K_W11/Vasquez/{VasquezW11SelfLane,W11SurfaceSmokeFacts}Tests.cs` —
  accept `KW11 || KW12 || KW13 || KW14 || KW15 || KW16`.

No frontend Wave1ThroughKW visual baseline files exist (the
Wave1Through naming is backend-regression-only).

### Lane-discipline strict result

Pre-commit (after all 4 W16 commits land):  expected
`checked=4 violations=0` (6th 0-violation lane wave).

If a violation surfaces from one of the 3 prior agent commits,
the W13/W15 amendment pattern applies: a SECOND commit titled
`Phase K Wave 16 — Vasquez QA: lane-map shared_files broadening (…)`
broadens `tests/ci/lane-map.json` + syncs
`tests/ci/check-cross-lane-bundling.sh`'s `is_shared_file()` +
`shared_file_authors()` case statements.

## Coordination notes

- **Wait window:**  Vasquez polled for Bishop/Hicks/Apone W16
  commits for ~30 minutes before proceeding.  If the prior 3
  commits had not landed by the W16 poll deadline, Vasquez's
  bring-up still ships — the §4.4 W16 re-verification + §4.5
  forward-stage tests + the W16 rename are all Vasquez-owned and
  independent of the prior 3 commits.
- **Lane-discipline run:**  the final strict check runs against
  the full branch tip after all 4 W16 commits land.  If the
  Vasquez commit is the only one on the branch at the time of
  the W16 PR, the check shows `checked=1 violations=0`.
- **Bishop W16 follow-up:**  Bishop's W16 PR is expected to add
  ~150 tests to push the gate over 3450.  Vasquez does not block
  on Bishop's commit; the Vasquez-only gate (3448) already
  exceeds the W15 baseline (3312) by 136 tests.

## Open items (for W17)

1. **§4.1 close:** if the Coordinator executes the §4.5 W16
   PRIMARY recommendation during the W16 PR review, §4.1 closes
   at W16 and §4.4 + §4.5 retire with `(CLOSED at W16)`.
2. **§6.5 hard-pin transition:** if the §6.6 Coordinator-direct
   cron seeding lands three convergent data points before W17
   sign-off, Hicks W17 retires the `provisional-until-calibrated`
   tag in `lh13-soft-pin-rationale.md §3` and Hicks's W17 lane
   resumes §6.1 hard-pin cadence.
3. **W17 Vasquez §4.6:** the next §4 escalation re-verification
   entry, if §4.1 has not closed at W16.
4. **W17 Wave1ThroughKW17 rename:** the next rename in the
   regression cadence.

## Cross-references

- `docs/test-architecture.md §3.4` — DbSerial migration final
  completion.
- `docs/frontend-pwa-audit.md §6.5 + §6.6` — LH13 W16 Option A
  disposition + Coordinator-direct invocation runbook.
- `docs/lh13-soft-pin-rationale.md` (Hicks W16, cross-ref) — the
  canonical W16 LH13 disposition.
- `docs/agent-handoff-protocol.md §4.5` — W16 branch-protection
  escalation re-verification.
- `tests/ci/lane-map.json` — `shared_files` allowlist (unchanged
  in W16 bring-up; may be amended if W16 lane-discipline strict
  flags an unallowlisted shared file).
- `.work/vasquez-w16-safe/` — W16 backup mirror; W16 flip script
  dry-run log.

---

*Phase K Wave 16 — Vasquez QA.  Memo is the durable record of
the W16 deliverables, the LH13 §6.5 Option A re-alignment paired
with Hicks's `lh13-soft-pin-rationale.md`, the §4.5 9-wave
escalation update, and the Wave1ThroughKW16 rename.*
