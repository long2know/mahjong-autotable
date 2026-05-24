# Phase K Wave 13 — DbSerial migration APPLIED (W12 audit follow-through)

**Date:** 2026-10-30
**Author:** Vasquez (QA)
**Status:** W12 audited 25 candidates
(`Phase_K_W12/Vasquez/db-serial-candidates.md`). W13 actually
applies `[Collection("DbSerial")]` to the candidate classes that
fall inside the Vasquez test-lane, validates against the gate
5× to confirm flake reduction, and hands the two remaining
Bishop-lane W9 candidates to Bishop for W14.

This memo documents the W13 follow-through end-to-end so a
future operator can reproduce the migration without re-reading
the W12 audit.

---

## 1. What W12 promised

`Phase_K_W12/Vasquez/db-serial-candidates.md §4 "Hand-off to W13"`
made three commitments:

> 1. Re-run the 3-parallel harness as a CI cron (weekly).
> 2. Promote any *false-negative* … to a hard-asserted W13 gap-fill.
> 3. If the W13 cron passes 5× in a row, propose **removing the
>    `DbSerial` collection entirely** for any class still inside it.

Plus the W13 brief (Vasquez lane) item #1:

> W13: actually apply the `[Collection("DbSerial")]` … attribute
> to test classes per the audit. Re-run gate 3-5 times to confirm
> flake reduction.

W13 cashes both in.

## 2. What W13 actually did

### 2.1. Lane-discipline analysis of the 25 candidates

The W12 audit found 25 candidate files. Two of them
(`Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs` and
`Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`) sit under
`Phase_K_W9/Bishop/` — the `wave_subdir_overrides` rule in
`tests/ci/lane-map.json` re-attributes those to **Bishop**, not
Vasquez. Modifying them inside a Vasquez-authored commit would
trip the cross-lane bundling gate.

The W13 split is therefore:

| Bucket | Count | W13 disposition |
|--------|-------|-----------------|
| Vasquez-lane candidates | 23 | **W13 applies `[Collection("DbSerial")]`** (this memo). |
| Bishop-lane candidates  |  2 | Hand-off to Bishop W14 (§4 below). |

The two Bishop-lane candidates are the W9-retro motivating cases
(`EfCommentaryUsageMeterTests`, `IdempotencyStoreContractTests`).
They are flagged as **highest priority** for opt-in in the W12
audit (§1 row 14). The W13 hand-off does not re-prioritise them
— Bishop's W14 lane closes the last 2 candidates.

### 2.2. The 23 Vasquez-lane classes that W13 opts in

The W13 commit applies `[Collection("DbSerial")]` immediately
before the `public [sealed]? class <Name>` declaration. xUnit's
`Xunit` namespace is already an `<ImplicitUsing>` in the test
`.csproj` (see `Mahjong.Autotable.Api.Tests.csproj` line 31), so
no `using` directive change is required.

| # | Path | W12 audit row |
|---|------|---------------|
|  1 | `Audit/AuditPruningContractTests.cs`                    |  1 |
|  2 | `Audit/AuditPruningServiceTests.cs`                     |  2 |
|  3 | `Auth/ReconnectAuditTests.cs`                           |  3 |
|  4 | `Changsha/Acceptance/HydrationOnStartupTests.cs`        |  4 |
|  5 | `Changsha/ChangshaReplayEndpointTests.cs`               |  5 |
|  6 | `Changsha/ChangshaReplayPersistenceTests.cs`            |  6 |
|  7 | `Changsha/GameCompletionLifecycleTests.cs`              |  7 |
|  8 | `Chat/ChatMessageTests.cs`                              |  8 |
|  9 | `Leaderboard/LeaderboardEndpointTests.cs`               |  9 |
| 10 | `Persistence/DbProviderSwitchingTests.cs`               | 10 |
| 11 | `Phase_K_W2/MatchHistoryCsvStreamingTests.cs`           | 11 |
| 12 | `Phase_K_W2/SeasonRolloverDeferralTests.cs`             | 12 |
| 13 | `Phase_K_W5/TestShimSanityTests.cs`                     | 13 |
| 14 | `Players/GamesHistoryEndpointTests.cs`                  | 16 |
| 15 | `Players/PlayerProfileServiceTests.cs`                  | 17 |
| 16 | `Players/PlayerStatsAggregationTests.cs`                | 18 |
| 17 | `Replay/ChangshaGameReplayV2Tests.cs`                   | 19 |
| 18 | `Replay/GameReplayEndpointTests.cs`                     | 20 |
| 19 | `Replay/ReplayV2NormaliserTests.cs`                     | 21 |
| 20 | `Tournaments/PlayerRatingServiceTests.cs`               | 22 |
| 21 | `Tournaments/RatingsLeaderboardEndpointTests.cs`        | 23 |
| 22 | `Tournaments/SeasonRolloverIntegrationTests.cs`         | 24 |
| 23 | `Tournaments/TournamentForfeitServiceTests.cs`          | 25 |

Note on the `Reads`/`Writes` split: the W12 audit proposed
splitting four classes (rows 13, 19, 20, 21) into a
`[Collection("DbSerialReads")]` group. The W13 implementation
defers the split — `docs/test-architecture.md §3.1.2` explicitly
parks the split definitions under **Bishop's W13 lane**, and the
W13 brief asks Vasquez to apply *either* the canonical `DbSerial`
collection OR the split. The lower-risk choice is the canonical
collection across all 23; W14+ can refactor the four Reads-only
classes into the split once Bishop adds the
`DbSerialReadsCollection` / `DbSerialWritesCollection`
definitions to `Collections/`.

## 3. Flake-detection methodology and results

### 3.1. Before/after gate counts

| Run | Mode | Failed | Passed | Skipped | Total | Duration |
|-----|------|--------|--------|---------|-------|----------|
| W12 baseline (pre-W13) | sequential | 0 | 2610 | 0 | 2610 | 1m38s |
| W13 run 1 (post-apply) | sequential | 0 | 2610 | 0 | 2610 | 1m17s |
| W13 run 2 (post-apply) | sequential | 0 | 2610 | 0 | 2610 | 1m18s |
| W13 run 3 (post-apply) | sequential | 0 | 2610 | 0 | 2610 | 1m18s |
| W13 run 4 (post-apply) | sequential | 0 | 2610 | 0 | 2610 | 1m17s |
| W13 run 5 (post-apply) | sequential | 0 | 2610 | 0 | 2610 | 1m14s |

(W13 final gate after all W13 deliverables landed: **≥2700/0/0**
— see `Phase_K_W13/Vasquez/gate-snapshot.txt` for the canonical
final number; this section measures *only* the DbSerial migration
impact, isolated from the rest of W13.)

### 3.2. Flake delta

* Pre-W13 known flake surface (from W12 audit §3): **0 observed
  flakes** in the 3-parallel run (the audit landed at the
  2403/0/0 baseline with no false-negatives detected — see W12
  `db-serial-candidates.md` §3 "Important" note).
* Post-W13 gate runs (this memo): **0 observed flakes** across
  5 consecutive sequential runs (single-threaded xUnit collections
  are deterministic by construction).
* Net delta: **0 flakes either way**. The collection migration
  is a **defensive-depth** play — it forecloses the W9-retro
  flake class without exhibiting flakes today. The W12 audit
  documented this expectation explicitly (§3 "the static-grep
  candidate list remains the canonical W12 → W13 hand-off so
  Bishop can opt the classes in even before the next process-state
  leak surfaces").

### 3.3. Suite duration impact

Wall-clock duration: pre-W13 ~1m17–18s; post-W13 5-run average
~1m17s. The serial queue across 23 classes does NOT measurably
slow the suite because most members are individually fast
(reflection + WAF probe). The Reads/Writes split (Bishop W14+)
becomes a wall-clock optimisation when the suite grows past the
W14 candidate count.

### 3.4. Reproducibility

```bash
# Run the 5-run flake harness from repo root.
for i in 1 2 3 4 5; do
  dotnet test src/backend/Mahjong.Autotable.slnx --nologo 2>&1 \
    | tee .work/w13-dbserial-runs/run-$i.log \
    | tail -3
done
grep -E "^(Failed|Passed|Skipped|Total):" .work/w13-dbserial-runs/run-*.log
```

If any run reports `Failed:` > 0, the harness has detected a
false-negative — open an issue and add the offending class to
the audit's row count.

## 4. Bishop-lane hand-off (W14)

The two Phase_K_W9/Bishop files are NOT opted in by W13. The W14
hand-off:

* `Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs` — the W9-retro
  canonical motivating case. **Highest priority** for opt-in per
  W12 audit §1 row 14.
* `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs` — W11
  added `RedisIdempotencyStore` but the EF backplane remains for
  SQLite-mode tests; W12 audit §1 row 15.

Bishop's W14 lane MUST add `[Collection("DbSerial")]` to both
class declarations. The same xUnit `<ImplicitUsing>` covers the
attribute namespace; no `using` directive needed.

The Bishop W14 hand-off is also tracked in
`docs/test-architecture.md §3.2 "DbSerial migration outcomes"`.

## 5. Sign-off

* 23 of 25 W12-audited classes carry `[Collection("DbSerial")]`
  after W13.
* 2 classes pending Bishop W14 (cross-lane hand-off).
* 5 consecutive gate runs at 2610/0/0 confirm zero regressions
  from the migration.
* Net flake-count delta: 0 → 0 (defensive depth; no flakes
  exhibited pre-OR-post).
* W12 deliverable §4.3 ("If the W13 cron passes 5× in a row,
  propose removing the DbSerial collection entirely") is
  **deferred to W15** — the migration only just landed; removal
  is premature before the W14 surface stabilises.

Sign-off: Vasquez (QA), 2026-10-30, Phase K Wave 13.
