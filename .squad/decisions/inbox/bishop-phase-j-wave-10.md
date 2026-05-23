# Bishop — Phase J Wave 10

**Branch:** `stlong/phase-j-wave-10-completion`
**Scope:** backend — final polish wave. Replay v1→v2 read-path
normaliser, audit-table pruning hosted service, tournament mode
(entities + REST + pairing + match-advancement hook), DB-introspection
fields on `/health`, and a `BotDecision` reasoning surface threaded
through all four strategy tiers into the replay envelope's
`debugScore` field.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **Passed: 820, Failed: 0, Skipped: 0**. Baseline at start of Wave 10
was 729/0/0; +91 net = my new surfaces + Vasquez's forward-staged
Wave-10 contract tests across `Audit`, `Tournaments`, `Replay`,
`ChangshaServices/BotDecisionReasoningTests`, `Api/HealthCheckJsonTests`.

---

## Task 1 — Replay v1→v2 read-path normaliser

### Problem

Wave 9 added the v2 replay envelope (`{schemaVersion: 2, events:[...]}`
with per-event `source`, `durationMs`) but the read path
(`GET /api/games/{gameId}/replay`) returned the literal stored JSON,
forcing clients to branch on shape. Legacy v1 rows (bare events array,
no per-event metadata) produced events lacking those fields entirely.
Vasquez's Wave-10 brief asked the controller to normalise legacy v1
rows into the v2 envelope so the client never needs to branch.

### Approach

- `ChangshaReplayController.NormaliseLegacyEvent(JsonElement)` (new
  helper at bottom of the file). Builds a `JsonNode` clone of each
  event with three synthesised fields:
  - `source = "unknown"` when absent (v1 rows have no source axis).
  - `durationMs = null` when absent.
  - `debugScore = null` when absent (Wave-10 placeholder; populated
    on the write path for bot-source events — see Task 5).
- The sorted-projection in the read endpoint now pipes every event
  through the normaliser, so v1 + v2 envelopes both surface the
  uniform v2 wire shape.
- Soft-pass branches removed from `ChangshaGameReplayV2Tests.cs`
  (the Wave-9 stubs that accepted either `JsonValueKind.Array` or
  `JsonValueKind.Object`); replaced with hard assertions.
- New tests:
  - `ReplayV1_LegacyEventsAreNormalisedToV2Envelope`
  - `ReplayV2_PreservesExistingEnvelopeFields`

### Verification

- `dotnet test --filter "Category=Replay"` — 12/0/0 pass.
- Vasquez's `Replay/ReplayV2NormaliserTests.cs` — green.

---

## Task 2 — Audit-table pruning hosted service

### Problem

`ReconnectAuditEntries` (Wave 9) and `CspViolations` (Apone, Wave 9)
are append-only tables. Without retention they grow unboundedly in
prod. The Wave 10 brief asks for a configurable background sweeper.

### Approach

- New options POCO: `Changsha/Audit/AuditPruningOptions.cs`
  - `ReconnectRetentionDays = 30`
  - `CspRetentionDays = 90`
  - `PruneIntervalMinutes = 1440` (daily)
  - `Enabled = true` (false in test/dev appsettings)
- New `BackgroundService`: `Changsha/Audit/AuditPruningService.cs`
  - `PruneOnceAsync(CancellationToken) → AuditPruneReport` (public)
  - Uses `ExecuteDeleteAsync` (no in-memory load) against both tables.
  - 30s startup settle delay to avoid fighting EF warmup.
- DI: singleton factory pattern so tests can resolve via DI for direct
  `PruneOnceAsync` calls without the timer kicking in.
- `appsettings.json` adds `"Audit": { ..., "Enabled": false }`;
  `appsettings.Production.json` opts in with `"Enabled": true`.

### Wire shape

No new endpoints. The service is internal.

### Verification

- `tests/Audit/AuditPruningServiceTests.cs` — 5/0/0 pass:
  reconnect retention, csp retention, idempotency, fresh rows kept,
  empty tables.

---

## Task 3 — Tournament mode (largest surface)

### Problem

Wave 10 brief: support multi-game competitive structures with three
pairing formats (single-elimination, round-robin, swiss), creator-only
start gating, GameCompleted-driven match advancement, and a
buchholz-tiebreaker leaderboard.

### Approach

- **Entities** (`Data/Entities/ChangshaEntities.cs`):
  - `Tournament` — `Id`, `Name`, `Format`, `Status`,
    `CreatedByPlayerId`, `MaxPlayers`, `GamesPerMatch`, `CreatedAt`,
    `StartedAt?`, `CompletedAt?`.
  - `TournamentRegistration` — `Id`, `TournamentId`, `PlayerId`,
    `Seed`, `RegisteredAt`.
  - `TournamentMatch` — `Id`, `TournamentId`, `Round`, `Player1Id`,
    `Player2Id`, `Player3Id?`, `Player4Id?`, `WinnerPlayerId?`,
    `GameIdsJson`, `Status`, `CreatedAt`, `CompletedAt?`.
- **DbContext** wires three `DbSet`s + `OnModelCreating` with unique
  `(TournamentId, PlayerId)` on registrations, cascading FKs from
  registration + match → tournament, indexed `(TournamentId, Round)`
  on matches.
- **EF migrations**: `AddTournaments` added to all three provider sets
  (`Persistence/Migrations/{Sqlite,Postgres,SqlServer}`).
- **SQLite bootstrap**: `DatabaseBootstrapper.EnsureSqliteWave10TablesAsync`
  — idempotent `CREATE TABLE IF NOT EXISTS` so existing dev SQLite DBs
  pick up the new tables on boot without an out-of-band migration
  update.
- **Pairing** (`Tournament/TournamentPairing.cs`):
  - `RoundRobin(seeded)` — circle method, emits `n*(n-1)/2` 2-player
    pairings spread across `n-1` rounds.
  - `SingleEliminationFirstRound(seeded)` — 1-vs-N, 2-vs-(N-1) etc.
  - `SwissFirstRound(seeded)` — half-and-half seed match (top vs bot).
  - `BuchholzScore(matchPointsByPlayer, opponents)` — Swiss tiebreaker.
- **Service** (`Tournament/TournamentService.cs`):
  - CRUD + lifecycle (`CreateAsync`, `RegisterAsync`,
    `UnregisterAsync`, `StartAsync`, `GetAsync`, `ListAsync`).
  - `AdvanceMatchAsync(gameId, winnerPlayerId)` — flips matching
    pending-match row to `complete` + winner; for elim/Swiss
    schedules next round when current round is fully complete; for
    round-robin marks the tournament complete when all rounds are done.
  - `LeaderboardAsync` — win count + buchholz tiebreaker, ordered
    by (wins desc, buchholz desc, playerId asc).
- **Controller** (`Tournament/TournamentController.cs`) — all under
  `[Route("api/tournaments")]`:
  - `GET    /api/tournaments[?status=]`
  - `GET    /api/tournaments/{id}`
  - `POST   /api/tournaments`              (auth → 401)
  - `POST   /api/tournaments/{id}/register` (auth → 401)
  - `DELETE /api/tournaments/{id}/register` (auth → 401)
  - `POST   /api/tournaments/{id}/start`    (auth → 401, creator → 403)
  - `GET    /api/tournaments/{id}/leaderboard`
- **GameCompleted hook**: `ChangshaGameRuntime.AdvanceTournamentMatchAsync`
  resolves the per-game top-score player and invokes the service.
  Best-effort: a tournament-service hiccup never breaks the completion
  hot path.

### Verification

- `dotnet test --filter "FullyQualifiedName~Tournaments"` — 26/0/0 pass
  across Vasquez's six test files (`TournamentCrudTests`,
  `TournamentPairingTests`, `TournamentStartTests`,
  `TournamentLeaderboardTests`, `TournamentAdvancementTests`,
  + harness).

---

## Task 4 — DB-introspection fields on /health

### Problem

`/health` exposed `db = { connected, latencyMs }` (Wave 7). Wave 10
brief: extend with provider name, query-ability readback, and
migrations-applied count so the operator dashboard can tell
SQLite-bootstrap deploys from Postgres/SqlServer migrated deploys.

### Approach

- Three new fields on the `db` sub-object:
  - `providerName` — `db.Database.ProviderName` (e.g. `"Sqlite"`,
    `"Npgsql.EntityFrameworkCore.PostgreSQL"`, `"Microsoft.EntityFrameworkCore.SqlServer"`).
  - `canQuery` — boolean readback of the smoke `SELECT 1`. Mirrors
    `connected` for now but distinct because a future provider could
    have an open pool member with a degraded data plane.
  - `migrationsApplied` — count of `__EFMigrationsHistory` rows.
    Swallows the no-such-table exception for SQLite-bootstrap DBs
    (where the table is empty by design) → stays 0.
- Wire shape `db = { connected, latencyMs, providerName, canQuery, migrationsApplied }`.

### Verification

- Updated `HealthCheckJsonTests.HealthDetailed_DbObject_ExposesWave10Shape`
  to pin the 5-key contract.
- Vasquez's `Api/DatabaseHealthDetailTests.cs` — green.

---

## Task 5 — BotDecision + reasoning surface

### Problem

Hicks's Wave 9 admin audit-replay tab surfaces `debugScore` as the
per-event "why did the bot do that" drilldown. Wave 9 left the field
as a placeholder. Wave 10 brief: thread a reasoning structure all the
way from every strategy tier into the replay envelope's `debugScore`
field, with a strong contract on Master's safety-analysis line.

### Approach

- **New struct** (`Changsha/Bot/BotDecision.cs`):
  ```csharp
  public readonly record struct BotDecision(
      BotAction Action,
      int? Tile,
      int Score,
      IReadOnlyList<string> Reasoning)
  {
      public static BotDecision FromAction(BotAction action) => ...;
  }
  ```
- **Interface** (`IChangshaBotStrategy.cs`): added default-interface-method
  `DecideWithReasoning(state, seatIndex)` that wraps `DecideAction`
  with empty reasoning, so any out-of-tree strategy still surfaces a
  valid (if uninformative) `BotDecision`.
- **Strategy overrides**: all four shipped strategies override
  `DecideWithReasoning` with tier-tagged reasoning:
  - First line is always `"strategy:{tier}"`.
  - Easy / Medium / Hard / Master all surface their primary score
    (`shanten-primary`, `keep-score`) as a reasoning line.
  - **Master** mandatorily emits a `"safety analysis: ..."` line —
    this is the Master-only opponent-discard tier-breaker over Hard
    and is what Vasquez's
    `BotDecisionReasoningTests.DecideWithReasoning_Master_IncludesSafetyAnalysis`
    pins.
- **Runtime wiring** (`Changsha/Runtime/ChangshaGameRuntime.cs`):
  - Both `_strategy.DecideAction` call sites (turn driver + claim
    window) now invoke a new
    `ChangshaBotEngine.DecideWithReasoningWithTimeoutAsync` helper
    that returns a `BotDecision`. The decision is stashed in
    `ChangshaGameInstance.LastBotDecisions[seatIndex]`.
  - `PersistReplayAsync` enriches per-event `debugScore` with the
    last seat-decision's reasoning + score for bot-source events;
    non-bot events leave the field null.
  - `ResolveReplayEventSource` now returns `"bot:{difficulty}"` (vs
    the Wave 9 `"bot:unknown"`) using `_strategy.Difficulty`.
- **Backwards compat**: `DecideAction` stays on the interface;
  unmodified callers (legacy code paths, `ChangshaBotPolicy` shim)
  remain unaffected.

### Verification

- Vasquez's
  `ChangshaServices/BotDecisionReasoningTests.cs` — 19/0/0 pass
  including the Master safety-analysis fact + the tier-discriminator
  + the read-only contract on `Reasoning`.

---

## Wire-shape contract deltas (operator-facing)

| Endpoint | Added | Removed |
|---|---|---|
| `GET /health` | `db.providerName`, `db.canQuery`, `db.migrationsApplied` | — |
| `GET /api/games/{gameId}/replay` | Per-event `debugScore` for bot rows | — |
| `GET /api/tournaments` | NEW | — |
| `GET /api/tournaments/{id}` | NEW | — |
| `POST /api/tournaments` | NEW (auth → 401) | — |
| `POST /api/tournaments/{id}/register` | NEW (auth → 401) | — |
| `DELETE /api/tournaments/{id}/register` | NEW (auth → 401) | — |
| `POST /api/tournaments/{id}/start` | NEW (creator → 403) | — |
| `GET /api/tournaments/{id}/leaderboard` | NEW | — |

## EF migrations

- `AddTournaments` (`20260523080532` Sqlite / `20260523080545` Postgres
  / `20260523080551` SqlServer).
- SQLite bootstrap: `EnsureSqliteWave10TablesAsync`.

## Hosted services added

- `Changsha.Audit.AuditPruningService` (BackgroundService).

## Bot strategy contract delta

- `IChangshaBotStrategy.DecideWithReasoning(state, seatIndex)` added
  (default-interface-method; concrete strategies override).
- Master MUST emit a reasoning line containing `"safety"` /
  `"defen[ce|sive]"` / `"opponent"` (case-insensitive) — gated by
  `BotDecisionReasoningTests.DecideWithReasoning_Master_IncludesSafetyAnalysis`.

## Files touched

- New: `Changsha/Audit/{AuditPruningOptions,AuditPruningService}.cs`,
  `Changsha/Bot/BotDecision.cs`,
  `Tournament/{TournamentService,TournamentController,TournamentPairing}.cs`,
  `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/...AddTournaments...`.
- Modified: `Program.cs`, `appsettings*.json`,
  `Changsha/Bot/{IChangshaBotStrategy,Easy,Medium,Hard,Master}Strategy.cs`,
  `Changsha/Bot/ChangshaBotEngine.cs`,
  `Changsha/Runtime/{ChangshaGameInstance,ChangshaGameRuntime,ChangshaReplayController}.cs`,
  `Data/{AppDbContext,DatabaseBootstrapper}.cs`,
  `Data/Entities/ChangshaEntities.cs`.

## Test gate

- Baseline (Wave 9 ship): **729/0/0**
- Final (Wave 10 ship): **820/0/0** (+91)
