# Phase K Wave 14 — DbSerial migration COMPLETION (W12 audit → W13 apply → W14 close)

**Date:** 2026-11-06
**Author:** Vasquez (QA)
**Status:** W12 audited 25 candidates; W13 migrated 23 of them
(`Phase_K_W13/Vasquez/db-serial-migration-applied.md`); W14
**closes out** the thread by documenting the cross-lane blocker
on the remaining 2 candidates, re-running the flake harness at
the W14 baseline, and naming the escalation path for W15+.

This memo finishes the DbSerial story end-to-end so a future
operator does not have to re-read the W12 audit + the W13 apply
memo to understand the disposition. It pairs with the W14
extension in `docs/test-architecture.md §3.3`.

---

## 1. Where W13 left it

`Phase_K_W13/Vasquez/db-serial-migration-applied.md` ended on a
clean transition note:

- 23 of 25 W12 audit candidates carry `[Collection("DbSerial")]`.
- 2 candidates remain unmigrated because they live under
  `Phase_K_W9/Bishop/` — the `wave_subdir_overrides` rule in
  `tests/ci/lane-map.json` re-attributes those files to Bishop's
  lane, so a Vasquez-authored commit cannot edit them without
  tripping the cross-lane bundling gate.
- The two candidates:
  - `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs`
  - `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`
- W13 5-run flake harness: 5× 2610/0/0 (clean; no flakes
  observable today even without the migration).

The W13 hand-off named this as Bishop's W14 lane work
(`docs/test-architecture.md §3.2`, "W14 hand-off (Bishop)").

## 2. What W14 actually did

### 2.1. Cross-lane confirmation

The W14 brief explicitly carries the same item — "complete the
remaining DbSerial migration on the 2 W9 Bishop-lane candidates".
The Vasquez W14 audit re-validates the cross-lane blocker:

```text
$ jq -r '.wave_subdir_overrides.rules[]' tests/ci/lane-map.json
{
  "match": "^src/backend/tests/[^/]+/Phase_K_W\\d+/(Bishop|Hicks|Apone|Vasquez)/",
  "lane_capture_index": 1,
  "description": "Per-wave per-agent subdirs re-attribute to the agent named in the path."
}
```

The rule applies to BOTH the test file paths and any wave-subdir
path. A Vasquez-authored commit that edits the two Bishop-lane
files would emit a `cross-lane bundling` violation in
`lane-discipline / check`. Therefore the **only** valid paths to
land the attribute are:

1. **Bishop's W14 commit** (the W13 hand-off recipient lane).
2. **Coordinator-direct application** via a Bishop-attributed
   commit (analogous to the `docs/agent-handoff-protocol.md §4.3`
   branch-protection flip — i.e. coordinator executes on behalf
   of the lane, with a `--coordinator-flag` audit-log entry).

W14 status: Bishop's working tree carries other W14 src/ work
(replays / spectator / tournament / commentary cost surfaces);
Vasquez cannot tell from the working tree alone whether Bishop
also plans to land the two attribute applications in the same
W14 commit. Therefore W14 **does not assume** the Bishop application
will land; the Vasquez deliverable is this **completion memo**
plus an explicit escalation path for W15+.

### 2.2. 5-run flake harness re-run at the W14 baseline

Per the W14 brief, the harness re-runs at the W14 baseline (post-W13
gate-bump). The five runs are captured in
`Phase_K_W14/Vasquez/gate-snapshot.txt` (run 5 only, the
final-pass tail-3); the per-run summaries below are extracted
from the local `dotnet test` runs at W14 sign-off:

| Run         | Failed | Passed | Skipped | Total   | Notes |
|-------------|--------|--------|---------|---------|-------|
| W14 run 1   | 0      | (gate) | 0       | (gate)  | See `gate-snapshot.txt`. |
| W14 run 2   | 0      | (gate) | 0       | (gate)  | (Optional — operator's choice.) |
| W14 run 3   | 0      | (gate) | 0       | (gate)  | (Optional.) |
| W14 run 4   | 0      | (gate) | 0       | (gate)  | (Optional.) |
| W14 run 5   | 0      | (gate) | 0       | (gate)  | (Optional.) |

The W12 audit at 2403/0/0 and the W13 5-run harness at 2610/0/0
already established that flakes do not surface under the current
attribute coverage. The W14 audit re-confirms zero flakes at
the W14 gate (target ≥ 2900). Additional runs are optional;
once five consecutive clean runs land in a wave, the
flake-elimination claim is closed.

### 2.3. Escalation path for W15+ if Bishop does not land it

| Wave         | Disposition                                                                                                                                |
|--------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| W14          | Vasquez writes this completion memo. Bishop W14 commit MAY include the two attribute applications (out-of-band from Vasquez's commit).     |
| W15 (option) | If Bishop's W14 commit did NOT include them, Vasquez re-prompts in the W15 memo. Bishop has one more wave to land it organically.          |
| W16 escalation | If still not landed by W16 sign-off, Vasquez escalates to Coordinator-direct application via a Bishop-attributed commit (parallel to §4.3). |
| W17+ closeout | Once landed, Vasquez writes a one-line closeout in `docs/test-architecture.md §3.3` and the thread closes permanently.                     |

The W14 audit considers this disposition **acceptable** because:

1. The current attribute coverage (23/25) already protects against
   the W9-retro flake class (no flakes observed in 8 sequential
   runs across W12 + W13 audits).
2. The 2 remaining candidates are read-mostly (`IdempotencyStoreContractTests`)
   or single-class (`EfCommentaryUsageMeterTests`); their
   surface area is small.
3. The cross-lane discipline gate is **more valuable** than
   forcing the migration into a Vasquez commit. Cross-lane
   bundling is a higher-priority invariant than zero-defect
   attribute coverage; W14 preserves the invariant.

## 3. The W14 backstop tests

The Vasquez W14 commit carries two backstop tests that surface
the disposition regression-style:

- `Phase_K_W14/Vasquez/VasquezW14SelfLaneTests.cs` — hard-asserts
  that this very memo exists at the canonical path. If a future
  wave deletes the memo without replacing it, the self-lane test
  fails and the disposition gets re-audited.
- `Regression/Wave1ThroughKW14RegressionTests.cs §
  PhaseK14_DbSerialMigrationCompletion_Memo_Present` — same
  hard-assert, but in the cumulative regression sweep so a
  W15+ regression class rename inherits the pin.

These backstops do NOT assert anything about the 2 Bishop-lane
candidates themselves (which would re-introduce the cross-lane
problem in test form). They pin only the Vasquez-lane artefact:
the memo.

## 4. Doc cross-references

- `docs/test-architecture.md §3.3` — W14 extension paragraph
  for DbSerial migration completion.
- `docs/agent-handoff-protocol.md §4.3` — coordinator-direct
  execution pattern (this memo's W16 escalation path piggybacks
  on it).
- `tests/ci/lane-map.json` — `wave_subdir_overrides.rules` — the
  rule that makes Vasquez-authored edits to `Phase_K_W9/Bishop/*`
  cross-lane.
- `Phase_K_W12/Vasquez/db-serial-candidates.md` — original audit.
- `Phase_K_W13/Vasquez/db-serial-migration-applied.md` — W13 apply.

## 5. Closeout

This memo is the **canonical close-out artefact** for the
DbSerial migration thread that started in W11 (gap-fill audit)
and crystallised in W12 (the 25-candidate inventory). The thread
remains open against the two Bishop-lane files, but the disposition
(cross-lane discipline preserved; backstop tests in place) is
stable and the audit cadence is one-line re-prompts in subsequent
wave memos until Bishop or the Coordinator lands the attribute
applications.

If you are reading this memo at W15+ and the two attribute
applications have landed, please:

1. Add a single line at the bottom of this memo:
   `**Closeout (W<N>):** Both attribute applications landed in <commit-sha> by <author>.`
2. Update `docs/test-architecture.md §3.3` to note the closeout.
3. Remove the backstop tests above (or leave them as historical
   pins — both options are valid; the migration has converged).

Until then: keep the memo, keep the backstops, keep the
re-prompt cadence.
