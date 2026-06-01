# Drake — PlayerProfiles.PlayerId UNIQUE constraint hotfix

**Author:** Drake (Persistence Hotfix Engineer)
**Date:** 2026-05-29
**Branch:** `fix/playerprofiles-unique-constraint`
**Commit on `main`:** `2df2e75`
**Severity:** Runtime crash hit by Stephen in live play.

## Symptom

Stephen hit this exception during a live play session:

```
CLR/Microsoft.EntityFrameworkCore.DbUpdateException
  Innermost exception: Microsoft.Data.Sqlite.SqliteException
  SQLite Error 19: 'UNIQUE constraint failed: PlayerProfiles.PlayerId'
  Stack starts at:
    Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync
    Mahjong.Autotable.Api.Players.PlayerProfileService.GetOrCreateAsync
```

## Root cause

Classic **SELECT-then-INSERT race condition** in
`PlayerProfileService.GetOrCreateAsync`:

```csharp
var profile = await db.PlayerProfiles.FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
if (profile is null)
{
    profile = new PlayerProfile { ... };
    db.PlayerProfiles.Add(profile);
    db.PlayerStats.Add(new PlayerStats { PlayerId = playerId });
}
await db.SaveChangesAsync(ct);
```

When two concurrent requests for the **same persistent player id** arrived
(e.g. `POST /api/identity` from `_identity.ResolveOrMint(HttpContext)` racing
the `ChangshaHub.OnConnectedAsync` "ensure profile on first connect" call —
or two browser tabs onboarding together) both observed
`FirstOrDefault → null` and both called `db.PlayerProfiles.Add`. The losing
`SaveChangesAsync` threw the unique-PK violation.

Three sibling methods shared the same shape and the same bug:
`UpdateDisplayNameAsync`, `UpdateAvatarColorAsync` (both create-on-miss when
called before `GetOrCreate`).

**This is different from my earlier `c369c54` `PlayerStats.LastGameAt` fix.**
That one was a hand-rolled `DatabaseBootstrapper` `CREATE TABLE` shadowing
the EF model. This one is a runtime-level race in the service layer — the
schema is correct, the writes were just unsafe under concurrency.

## Fix

Single-file change to
`src/backend/src/Mahjong.Autotable.Api/Players/PlayerProfileService.cs`:

1. **Factored a private `UpsertProfileAsync(playerId, onCreate, onExisting,
   ct)` helper** that wraps the SELECT-then-INSERT-or-UPDATE in a
   2-attempt retry loop:
   - Attempt 0: SELECT; if null, build + Add(profile) + Add(stats unless
     `AnyAsync(stats)` shows the other path already won that race);
     otherwise apply the existing-row mutator.
   - `SaveChangesAsync` in a try/catch that ONLY recovers from a
     unique-constraint violation. On hit, drop the scope (discards the
     dangling tracked Add() entries) and loop once. The retry takes the
     "existing row" branch with the row the winning caller just
     committed.
   - Exactly one retry. If even the post-retry SELECT misses (unlikely —
     would require a delete in flight), the next save bubbles the
     exception so a genuine schema problem isn't masked.
2. **Cross-provider unique-violation predicate** `IsUniqueViolation`
   walks the inner-exception chain checking:
   - `Microsoft.Data.Sqlite.SqliteException.SqliteErrorCode == 19`
   - `Npgsql.PostgresException.SqlState == "23505"`
   - `Microsoft.Data.SqlClient.SqlException.Number == 2627 or 2601`

   Covers every provider this codebase ships against (see
   `Persistence/ServiceCollectionExtensions.cs`). Direct type pattern
   matches (no reflection) — all three packages are referenced in the
   API `.csproj`.
3. `GetOrCreateAsync`, `UpdateDisplayNameAsync`, `UpdateAvatarColorAsync`
   all rewritten as thin shells that delegate to `UpsertProfileAsync`
   with the right pre-validation + mutator callbacks. Behaviour is
   identical on the happy path; only the race semantics change.
4. `GetStatsAsync` and `RecordGameCompletedAsync` left alone — they
   write to `PlayerStats`, not `PlayerProfiles`, and Stephen's symptom
   was specifically the `PlayerProfiles.PlayerId` PK. `RecordGameCompletedAsync`
   already wraps everything in try/catch+log, which is sufficient for
   its rare-game-completion writes.

No EF migration / model snapshot / entity-config changes — the schema is
correct. No `DatabaseBootstrapper` changes either.

## Regression test

Added `GetOrCreate_IsRaceSafe_WhenCalledConcurrently_WithSameId` in
`tests/Mahjong.Autotable.Api.Tests/Players/PlayerProfileServiceTests.cs`:

- 8 parallel `GetOrCreateAsync(samePlayerId)` via `Task.Run` + `Task.WhenAll`.
- Asserts all 8 succeed (no exception leak) and exactly one row each lands
  in `PlayerProfiles` and `PlayerStats`.

**Verified the test FAILS without the fix** by stashing the production
change and re-running — the failure produces the EXACT exception Stephen
reported (`Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync`
→ `Microsoft.Data.Sqlite.SqliteException`, "UNIQUE constraint failed:
PlayerProfiles.PlayerId"). Restored the fix; test passes deterministically.

## Test results

```
dotnet test ... --filter "PlayerProfile|PlayerIdentity|PersistentPlayerId"
→ Passed: 15, Failed: 0, Skipped: 0, Duration: 5s
```

## Live runtime probe

Fresh `/tmp/mat-drake-fix.db` SQLite, backend booted to port 8088:

| Probe | Result |
|---|---|
| `/health` | 200 OK |
| First `POST /api/identity` (no cookie) | 200, fresh PID minted |
| Second `POST /api/identity` (same cookie) | 200, idempotent (same PID, `lastSeenAt` advanced) |
| **20 parallel `POST /api/identity` with the SAME fresh-but-never-created cookie value (worst-case race)** | **All 20 → 200 OK; exactly 1 row in `PlayerProfiles`, 1 in `PlayerStats`** |
| EF logged race hits (`fail: Microsoft.EntityFrameworkCore.Update`) | 5 of 20 lost the race |
| Drake debug "lost an insert race" lines | 5 (all 5 recovered via the retry branch) |
| `Unhandled exception` lines | **0** |
| Non-200 `POST /api/identity` responses (53 total across all probes) | **0** |

Backend log evidence preserved at `/tmp/mat-drake-fix.log` until cleanup.

## Lane discipline

Touched ONLY:
- `src/backend/src/Mahjong.Autotable.Api/Players/PlayerProfileService.cs`
  (production fix)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Players/PlayerProfileServiceTests.cs`
  (regression test)
- `.squad/agents/drake/history.md` (this memo's twin entry)
- `.squad/decisions/inbox/drake-playerprofiles-unique-fix.md` (this file)

Did NOT touch: Changsha runtime, Bot / Scoring (Frost lane), Autotable WS
(Bishop lane), frontend (Hicks lane), test infrastructure (Vasquez lane),
DevOps / CI (Apone lane).

## Pattern locked in (carries forward)

`PlayerProfileService` is now the canonical reference for **race-safe
upsert** in this codebase:

1. Wrap SELECT-then-INSERT in a 2-attempt loop.
2. Use one fresh `IServiceScope` per attempt so the EF change tracker
   drops with the losing scope.
3. Catch `DbUpdateException` only when `IsUniqueViolation(ex)` returns
   true — never swallow other DB failures.
4. Use cross-provider error-code checks (SQLite 19, Postgres 23505,
   SqlServer 2627/2601) so the fix lands once and works on every
   provider this codebase ships against.

The same pattern should be applied any time a "natural-key-PK" upsert
shows up: `MatchHistory` `(PlayerId, GameId)`, `PlayerSeasonStats`
`(PlayerId, Season)`, `TournamentParticipants` `(TournamentId, PlayerId)`,
etc. all have unique indices and would race the same way under concurrent
first-write. **Not changing those in this hotfix** (Stephen's symptom was
specifically `PlayerProfiles.PlayerId` and the brief says "precise,
surgical"), but flagging for whichever agent owns the next race report.

## Cross-reference

- Previous Drake fix: `c369c54` — `PlayerStats.LastGameAt must be nullable
  across all providers (squash)`. That memo:
  `.squad/decisions/inbox/drake-playerstats-lastgameat-fix.md`.
- Stephen's symptom pattern: schema/model mismatch surfaces as
  `SqliteException 19`. Two distinct root causes can produce the same
  innermost exception — last time it was a NOT-NULL constraint; this
  time it was a UNIQUE PK. Always check BOTH the EF migration chain and
  the runtime write path when this error class shows up.
