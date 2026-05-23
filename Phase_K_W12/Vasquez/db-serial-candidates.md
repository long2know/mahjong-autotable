# Phase K Wave 12 — DbSerial migration candidates (audit hand-off → Bishop)

**Date:** 2026-10-23
**Author:** Vasquez (QA)
**Status:** W10 introduced `Collections/DbSerialCollection.cs`. Most Bishop
test classes that touch `AppDbContext` / `SqliteConnection` are NOT yet
opted into the collection. This file is the W12 audit hand-off: the
candidate list, the rationale for each, the proposed collection name
(`DbSerial` vs the W12-proposed `Reads` / `Writes` split), and a brief
note on each test's contention surface.

The methodology that produced this list is documented in
`docs/test-architecture.md §3.1`. Summary: the candidate set is the
intersection of

  1. test files that call `GetRequiredService<AppDbContext>()` AT LEAST
     ONCE (catches every WAF-based fixture that resolves the live EF
     model), AND
  2. test files that mutate state (`db.SaveChangesAsync`,
     `db.Add(...)`, `db.Remove(...)`) OR open a raw `SqliteConnection`.

The 22 files found in the W12 inventory (see §1 below) are all
**candidates**; Bishop has the final call on each — some may be safe
to leave outside the collection if they use a per-test temp DB file
that's never shared (e.g. `*_TempSqliteDb_*` pattern).

The current `Collections/DbSerialCollection.cs` is left unchanged in
W12 — Bishop applies `[Collection("DbSerial")]` to the candidate
classes in the W12 backend lane. Vasquez ships the audit + the
3-parallel flake-detection methodology + the proposed
`DbSerialReadsCollection` / `DbSerialWritesCollection` split (see §2).

---

## 1. Candidate inventory (W12)

The 22 files below were found by:

```bash
grep -rln "GetRequiredService<AppDbContext>\|new AppDbContext\|SqliteConnection" \
    src/backend/tests/Mahjong.Autotable.Api.Tests/ \
    | grep -v "/bin/\|/obj/"
```

Each row carries a proposed disposition. Bishop should treat this as
the W12 starting point and adjust per his own knowledge of the
fixture topology.

| # | Path | Proposed | Reason |
|---|------|----------|--------|
| 1 | `Audit/AuditPruningContractTests.cs` | `[Collection("DbSerial")]` | Writes audit rows + reads back via verify-scope `AppDbContext`. Cross-test contention on EF model cache observed in W9 retro under high parallelism. |
| 2 | `Audit/AuditPruningServiceTests.cs` | `[Collection("DbSerial")]` | Same `AuditPruningService` surface as #1. Five separate `GetRequiredService<AppDbContext>()` resolutions per test method — high SaveChanges footprint. |
| 3 | `Auth/ReconnectAuditTests.cs` | `[Collection("DbSerial")]` | Three scopes, two `GetRequiredService<AppDbContext>()` resolutions, writes `Audit` entity. Resolves `typeof(DbContext).GetMethods()` reflection across the EF model. |
| 4 | `Changsha/Acceptance/HydrationOnStartupTests.cs` | `[Collection("DbSerial")]` | Persists a Changsha snapshot, recycles host, hydrates from disk — multi-scope EF interaction. |
| 5 | `Changsha/ChangshaReplayEndpointTests.cs` | `[Collection("DbSerial")]` | Writes replay row through real EF, then exercises `/api/games/{id}/replay`. |
| 6 | `Changsha/ChangshaReplayPersistenceTests.cs` | `[Collection("DbSerial")]` | Same `EfChangshaReplayStore` write/read path as #5 + retention sweep. |
| 7 | `Changsha/GameCompletionLifecycleTests.cs` | `[Collection("DbSerial")]` | Opens raw `SqliteConnection` against a temp file — process-wide SQLite connection-pool contention. |
| 8 | `Chat/ChatMessageTests.cs` | `[Collection("DbSerial")]` | W9 `ChatMessage` entity write-then-read. EF model cache hot path. |
| 9 | `Leaderboard/LeaderboardEndpointTests.cs` | `[Collection("DbSerial")]` | Seeds finalised games via `AppDbContext` and asserts leaderboard envelope shape. |
| 10 | `Persistence/DbProviderSwitchingTests.cs` | `[Collection("DbSerial")]` | Boots multiple `AppDbContext` instances with different providers in sequence — provider-pool process state at risk. |
| 11 | `Phase_K_W2/MatchHistoryCsvStreamingTests.cs` | `[Collection("DbSerial")]` | Streams DB-backed CSV; writes seed rows beforehand. |
| 12 | `Phase_K_W2/SeasonRolloverDeferralTests.cs` | `[Collection("DbSerial")]` | Heavy write surface (`DbSet<def>` enumeration + season rollover). |
| 13 | `Phase_K_W5/TestShimSanityTests.cs` | **Split candidate** — `[Collection("Reads")]` | Pure shim sanity; reads only. Suggest the `Reads` group (see §2) so it can run parallel with other `Reads` but not with any `Writes`. |
| 14 | `Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs` | `[Collection("DbSerial")]` | The W9-retro canonical example. Already documented in `docs/test-architecture.md §3.1` as the motivating case. **Highest priority for opt-in.** |
| 15 | `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs` | `[Collection("DbSerial")]` | EF-backed `IdempotencyStore` write/read with `Idempotency-Key`. W11 added `RedisIdempotencyStore` but the EF backplane remains for SQLite-mode tests. |
| 16 | `Players/GamesHistoryEndpointTests.cs` | `[Collection("DbSerial")]` | Seeds games, exercises history endpoint. |
| 17 | `Players/PlayerProfileServiceTests.cs` | `[Collection("DbSerial")]` | Profile service writes + reads. |
| 18 | `Players/PlayerStatsAggregationTests.cs` | `[Collection("DbSerial")]` | Aggregation over multiple games — heavy read footprint with model cache touch. |
| 19 | `Replay/ChangshaGameReplayV2Tests.cs` | **Split candidate** — `[Collection("Reads")]` | Replay V2 normaliser; mostly reads. |
| 20 | `Replay/GameReplayEndpointTests.cs` | **Split candidate** — `[Collection("Reads")]` | Reads existing replay rows. |
| 21 | `Replay/ReplayV2NormaliserTests.cs` | **Split candidate** — `[Collection("Reads")]` | Pure normaliser; reflection-defensive, no writes. |
| 22 | `Tournaments/PlayerRatingServiceTests.cs` | `[Collection("DbSerial")]` | Rating service writes Elo deltas via EF. |
| 23 | `Tournaments/RatingsLeaderboardEndpointTests.cs` | `[Collection("DbSerial")]` | Seeds finalised tournament rounds + reads leaderboard. |
| 24 | `Tournaments/SeasonRolloverIntegrationTests.cs` | `[Collection("DbSerial")]` | Season rollover end-to-end — multi-table write. |
| 25 | `Tournaments/TournamentForfeitServiceTests.cs` | `[Collection("DbSerial")]` | Forfeit service writes match + emits audit row. |

Total: 25 candidate test classes; 22 propose `[Collection("DbSerial")]`
(full opt-in), 3 propose the read-only `[Collection("Reads")]` group
(see §2). Three Shims-resident classes (`TestShimSanityTests` etc.)
land in the split.

> **Note for Bishop:** `Auth/PlayerAuthIdentityModelTests.cs` and
> `Auth/ReconnectAuditTests.cs` BOTH appear in the audit. The former
> uses reflection on `AppDbContext.cs` source text (no EF runtime
> touch) and is **NOT** a candidate; only the latter is.

## 2. Proposed `DbSerial` collection split (W12+ optional refinement)

The current `[Collection("DbSerial")]` is a binary opt-in — every
member runs single-threaded against every other member. That's the
right minimum surface for W10, but at the W12 candidate count (25
classes) the serial queue starts dominating the wall-clock duration.

**W12 proposal (Vasquez):** split into TWO collections:

```csharp
[CollectionDefinition("DbSerial",       DisableParallelization = true)]
public sealed class DbSerialCollection { }

[CollectionDefinition("DbSerialReads",  DisableParallelization = false)]
public sealed class DbSerialReadsCollection { }   // pure-read fixtures

[CollectionDefinition("DbSerialWrites", DisableParallelization = true)]
public sealed class DbSerialWritesCollection { }  // mutate the DB
```

The naming intent:

- `DbSerial` — the W10 collection (kept for back-compat; alias of
  `DbSerialWrites`). Bishop's W12 migration prefers `DbSerialWrites`
  for new opt-ins.
- `DbSerialReads` — read-only fixtures that can run parallel with
  each other but must NOT run concurrent with a write (xUnit doesn't
  support exclusive-vs-shared in a single collection definition, so
  we lean on per-fixture isolation here — see §2.1).
- `DbSerialWrites` — write-mutating fixtures. Disables parallel.

### 2.1. Why not a `[Collection("Reads")]` / `[Collection("Writes")]` split today?

xUnit's collection model is "all-members-share-one-fixture-OR-no-fixture";
it doesn't model the SQL reader/writer-lock semantics directly. The
W12 proposal asks Bishop to:

1. **Lift the W10 `DbSerial` definition** into the new namespace
   `Mahjong.Autotable.Api.Tests.Collections` (already at the canonical
   location), AND
2. **Add `DbSerialWrites` and `DbSerialReads`** as the two future
   migration targets.

The W12 audit categorises every candidate as one or the other so the
W13 split is mechanical (find-and-replace).

### 2.2. Out-of-scope for the W12 split

- `RedisSerial` (mentioned in §3.3 of the architecture doc as a
  future possibility) is OUT of W12. The W11 Testcontainers-based
  Redis tests use a fresh container per fixture, so the
  process-wide contention surface is small.
- `JanusSerial` is also out of W12. The fake Janus probe runs in a
  per-test in-memory HTTP server.

## 3. Audit methodology (documented in `docs/test-architecture.md §3.1`)

The W12 audit uses three signals to detect a DbSerial candidate:

1. **Static grep:** `GetRequiredService<AppDbContext>` OR
   `new AppDbContext` OR `SqliteConnection` in the test file.
2. **Parallel-stress run (3-parallel + 5-parallel):**
   ```bash
   # 3-parallel — mild contention probe
   for i in 1 2 3; do
     dotnet test src/backend/Mahjong.Autotable.slnx \
       --logger:console --nologo > .work/dbserial-run-$i.log 2>&1 &
   done
   wait
   grep -E "^(Failed|Skipped):" .work/dbserial-run-*.log
   ```
   Any class that appears in the failure tail of one run but not
   another is a confirmed candidate.
3. **Manual review:** the failure tail can be misleading — sometimes
   a test that fails is a victim of a different class's leak. Bishop
   has the final say.

**Important:** the 3-parallel harness uses three separate `dotnet test`
invocations against the same compiled assembly, NOT three workers in
one invocation. xUnit's intra-assembly parallelism is independently
controlled by the `Mahjong.Autotable.Api.Tests.csproj`'s
`<ParallelizeAssembly>` setting (defaults to `false` for `dotnet test`
runs) and `xunit.runner.json` (absent → defaults).

The W12 run as documented here detected **zero new flakes** at the
2403/0/0 baseline — but the run was at the default xUnit parallelism
(test classes parallel within an assembly). The audit's static-grep
candidate list remains the canonical W12 → W13 hand-off so Bishop can
opt the classes in even before the next process-state leak surfaces.

## 4. Hand-off to W13

Once Bishop has applied `[Collection("DbSerial")]` to the candidates
above (or the W12 split into `DbSerialReads` / `DbSerialWrites`),
Vasquez's W13 lane will:

1. Re-run the 3-parallel harness as a CI cron (weekly).
2. Promote any *false-negative* (candidate not yet opted-in that
   exhibits flake) to a hard-asserted W13 gap-fill.
3. If the W13 cron passes 5x in a row, propose **removing the
   `DbSerial` collection entirely** for any class still inside it —
   the EF / SQLite contention surface may have shrunk enough that
   serialisation is no longer worth the wall-clock cost.

Sign-off: Vasquez (QA), 2026-10-23, Phase K Wave 12.
