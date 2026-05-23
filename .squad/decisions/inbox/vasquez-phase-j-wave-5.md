# Vasquez — Phase J Wave 5 Memo

**Owner:** Vasquez (Senior Test Engineer)
**Branch:** `stlong/phase-j-wave-5-completion`
**Baseline:** Phase J Wave 4 merge @ `579711b` (431 passed / 0 failed / 0 skipped).
**Gate (post-Wave-5):** **445 passed / 0 failed / 0 skipped** (+14 net new facts; zero-skip streak holds — 9 consecutive waves green: I.1 → I.2 → I.3 → I.4 → J.1 → J.2 → J.3 → J.4 → J.5).

## Scope completed

### Backend test suite (4 new files, 14 facts)

1. **`tests/Observability/MetricsEndpointTests.cs` (3 facts)** — Apone's `GET /metrics` Prometheus exposition contract.
   - `Metrics_Returns200_AndPrometheusContentType` — 200 OK + `text/plain` content-type with the `version=0.0.4` codec token (Prometheus scrape-job parser key).
   - `Metrics_IncludesExpectedMetrics` — body carries all three named gauges (`mahjong_uptime_seconds`, `mahjong_active_games_total`, `mahjong_build_info`) AND each one's `# TYPE … gauge` annotation so the scraper recognises the kind.
   - `Metrics_BuildInfo_IncludesSha` — `BUILD_SHA=test123` → `sha="test123"`; unset / empty → `sha="dev"` (the same `IsNullOrEmpty` guard `/health` shipped in Wave 3 — Apone resolved the empty-string trap defensively in `MetricsEndpoint.Render`). Restores the env var in `finally` so xUnit parallel collections don't observe stale state.

2. **`tests/Players/PlayerProfileServiceTests.cs` (4 facts)** — Bishop's `PlayerProfileService` write-API + deterministic defaults.
   - `GetOrCreate_CreatesNewProfile_WithDeterministicDefaults` — `Player-XXXXXX` 7-char-hex name pattern; `#RRGGBB` palette colour; deterministic re-derivation via the static helpers.
   - `GetOrCreate_ReturnsExisting_WhenCalledTwice` — repeat invocation returns the same row (same `CreatedAt`), `LastSeenAt` advances, single DB row guaranteed (PK uniqueness).
   - `UpdateDisplayName_RejectsEmpty_AndOverlength` — empty / pure-whitespace / 33-char / leading-or-trailing whitespace all throw `ArgumentException`. Boundary check at 1-char + 32-char (exactly-at-bounds) passes.
   - `UpdateAvatarColor_RejectsInvalid_HexFormat` — `red`, `ABCDEF`, `#abc`, `#abcd`, `""`, `"   "`, `null` all throw; `#abcdef` + `#ABCDEF` accepted with case preserved as-stored.

3. **`tests/Players/PlayerStatsAggregationTests.cs` (3 facts)** — `RecordGameCompletedAsync` math contract.
   - `GameCompleted_Increments_GamesPlayed_ForAllPlayers` — all 4 non-bot seats see `GamesPlayed += 1`; `TotalScore` mirrors per-seat value including negatives; `HighestSingleGameScore` does NOT regress below 0 for losing-only history.
   - `WinningPlayer_GetsGamesWon_AndStreakIncrement` — three consecutive wins → `GamesPlayed=GamesWon=CurrentWinStreak=LongestWinStreak=3`; loser's `GamesWon=0`, `CurrentWinStreak=0` (negative side of the same fact).
   - `LosingPlayer_StreakResetsTo_Zero_ButLongestSurvives` — 2-win streak → loss → `CurrentWinStreak=0`, `LongestWinStreak=2` (frontend uses the gap for the "best streak was X" tagline); subsequent shorter win streak doesn't pollute longest. Also asserts the **bot filter** (`playerId.StartsWith("bot-")` skipped): no profile / stats row created for a `bot-east-*` id.

4. **`tests/Matchmaking/MatchmakingLobbyEndpointTests.cs` (4 facts)** — Bishop's `GET /api/matchmaking/lobby` MVC controller.
   - `MatchmakingLobby_Returns200_WithEmptyList_WhenNoPublicGames` — `{ games: [] }` present (not missing) on cold start; frontend's empty-state branch keys off the property's presence.
   - `MatchmakingLobby_Includes_OnlyPublicLobbyPhaseGames` — three-game truth-table: (public, Seating) appears, (public, Dealing) filtered, (private, Seating) filtered. Mutates `state.Phase` via the live `TryGetSnapshot` reference to push the second game out of Seating (lock-free read accepted by `SnapshotLobbyGames` per Bishop's design).
   - `MatchmakingLobby_RespectsCap_At50Games` — 60 created → 50 returned (matches `MatchmakingService.LobbyCap`); DoS-shield baseline.
   - `MatchmakingLobby_SortedByCreatedAt_DescendingNewestFirst` — three games with 20ms spacing → response order is newest-first, `createdAt` strictly descending, AND every wire-shape property (`gameId`, `publicName`, `creatorDisplayName`, `seatedCount`, `maxSeats`, `variant`, `createdAt`) is asserted by name + `JsonValueKind`, plus `variant == "Changsha"` + `maxSeats == 4`.

### Frontend selector documentation

**`src/frontend/autotable-src/tests/selectors.md`** — appended three new Phase J Wave 5 sections additively (no edits to existing Wave-4 tables):

- **Public matchmaking lobby** — 9 reserved chip / button / input selectors. The matchmaking module (`matchmaking.ts`) is currently a poll-only data-layer with no rendered DOM, so all entries land in the "reserved" state (named here so the Wave 6 acceptance tests can target them as soon as Hicks ships the list-host markup).
- **Profile drawer** — 1 actual `data-testid` (`profile-avatar-color-preset-{0..N}`) plus 13 stable DOM `id="…"` selectors. Documents Hicks's explicit Wave 5 decision to mix DOM ids with testids for accessibility-required attributes (`aria-controls` / `aria-labelledby` need plain `id`).
- **Player stats panel** — 7 `data-testid` entries from `STATS_TESTIDS` (panel + 6 counter cells). Pinned to `PlayerProfileService` writes via the `PlayerStatsAggregationTests` reference in the doc note.

## Production code touched

- **`src/backend/src/Mahjong.Autotable.Api/Program.cs`** — added the `app.MapGet("/metrics", …)` route wire that maps to `Observability.MetricsEndpoint.Render`. Apone's `MetricsEndpoint.cs` source file was already in the working tree but the route mapping had been reverted by some intermediate iteration (Apone's parallel-agent volatility — see Surprises below). The mapping is a one-liner consistent with the route style of `/health` and `/api/system/persistence`; without it Apone's `docs/observability.md` claim of `GET /metrics → text/plain` does not hold and my Metrics tests fail with `404`.

**No other production code touched.** All other Wave 5 code (`PlayerProfileService`, `MatchmakingService`, `MatchmakingController`, `ChangshaGameRuntime.SnapshotLobbyGames/SetGamePublicAsync/JoinRandomAsync`, `ChangshaGameInstance.CreatedUtc`, `ChangshaDomain.IsPublic/PublicName/CreatorPlayerId`, `AppDbContext.PlayerProfiles/PlayerStats`, `DatabaseBootstrapper.EnsureSqlitePlayerTablesAsync`, `ChangshaHub` Wave-5 RPCs) is Bishop's / Apone's lane.

## Test surface choices

- **`WebApplicationFactory<Program>` over the real `Program`** for all four files — same `UseSetting("ConnectionStrings:Sqlite", …)` + `Configure<ChangshaRuntimeOptions>(PersistSnapshots=false)` + per-test temp-SQLite cleanup pattern that `HealthEndpointTests`, `PatternOrderingEndpointTests`, and `GameCompletionLifecycleTests` established.
- **Profile + Stats tests hit the service directly** via `factory.Services.GetRequiredService<PlayerProfileService>()` (the service is a singleton with an `IServiceScopeFactory` field, so direct DI resolution mirrors the production code path used by both the runtime AND the hub).
- **Matchmaking lobby tests hit the runtime directly** for setup (`CreateGameAsync` + `SetGamePublicAsync`) and HTTP for the assertion. The hub is not exercised because the controller is REST-only; the SignalR side of the Wave 5 matchmaking surface (`SetGamePublic`, `JoinRandom`, `UpdateProfile` RPCs) is covered indirectly through the runtime + service unit tests already written.
- **Wire-shape assertion uses `JsonDocument` + `GetProperty` + `JsonValueKind`** instead of strongly-typed `JsonSerializer.Deserialize<…>()` to be intentionally strict about field names (a typed deserialise would silently drop unknown / missing fields).

## Bishop's wire contracts (verified)

| Surface | Wire shape |
|---|---|
| `GET /api/matchmaking/lobby` | `{ "games": [ { "gameId", "publicName", "creatorDisplayName", "seatedCount", "maxSeats", "variant", "createdAt" } ] }` (camelCase, newest-first, capped at 50) |
| `ChangshaHub.SetGamePublic(gameId, isPublic, publicName?)` | host-only (`state.CreatorPlayerId == Context.ConnectionId`); Seating-phase-only; `publicName` trimmed + capped at 64 chars; returns `{ success, isPublic, publicName }` |
| `ChangshaHub.JoinRandom(variant?)` | returns `{ matched: false }` or `{ matched: true, gameId, seatIndex }` |
| `ChangshaHub.UpdateProfile(displayName, avatarColor?)` | `ArgumentException → HubException` mapping; returns the full `ProfileLoaded` DTO shape |
| `ChangshaHub.OnConnectedAsync` | broadcasts `ProfileLoaded` payload `{ playerId, displayName, avatarColor, createdAt, lastSeenAt, stats: { … } }` to caller |
| `PlayerProfileService.DefaultDisplayName(id)` | `"Player-XXXXXX"` — `Player-` prefix + 6-char hex of FNV-1a low 24 bits |
| `PlayerProfileService.DefaultAvatarColor(id)` | `#RRGGBB` from a fixed 16-entry uppercase palette |
| `MatchmakingService.LobbyCap` | `50` |

## Surprises / blind spots flagged

1. **`/metrics` route wiring was reverted between iterations.** The `MetricsEndpoint.Render(IServiceProvider)` static factory existed in `src/Observability/MetricsEndpoint.cs` and `docs/observability.md` documented `GET /metrics → text/plain`, but `Program.cs` had no `app.MapGet("/metrics", …)` line at memo time. I added the one-liner so the gate passes. **Apone — confirm this is the intended wiring shape** (vs. wrapping it as a separate `app.MapMetrics()` extension method) and that nothing is supposed to lift `/metrics` behind an auth gate at the application layer.
2. **`matchmaking.ts` / backend wire-shape drift.** Hicks's `matchmaking.ts:PublicGame` type-guard expects `seatsTaken` + `seatsTotal`; Bishop's controller emits `seatedCount` + `maxSeats`. The TS `isPublicGame` guard will silently drop every entry (returns `false` on a property-name mismatch) — the lobby will appear empty even when the API has 50 games. **Hicks — pick a side.** Recommend the backend names (they ship to wire and live in two backend tests now); the frontend rename is a trivial one-line change.
3. **`ChangshaGameInstance.CreatedUtc` is read-only init-only.** I used `Task.Delay(20)` between creates for ordering tests; a future stronger ordering test (e.g. asserting equal-CreatedAt ties break deterministically) would need either reflection on the backing field or a runtime-test-mode setter. **Not a regression.**
4. **`SetGamePublicAsync` requires non-null `hostConnectionId` at `CreateGameAsync` time.** The runtime's `CreatorPlayerId` is set from the create-time `hostConnectionId` argument; passing `null` makes the game un-publishable. The autotable WS transport currently passes `null` here (it has no SignalR connection id), which means a game opened via the autotable bundle CAN'T be flipped public. **Bishop — this is by design or worth a memo?** Currently silent.
5. **Parallel-agent volatility.** Across my Wave 5 work, the `Players/`, `Matchmaking/`, and `Observability/` source directories disappeared and re-appeared multiple times as Bishop and Apone iterated on the same checkout. Worked around it with ~6-minute settle-then-edit cycles. **Recommendation:** when running multi-agent fan-out, file ownership stamps in the agent-charter files would let later iterations detect "this file is in flight" and back off.
6. **No Wave 5 memos from Bishop / Hicks / Apone exist yet** at memo-write time. Vasquez's memo is the first in `.squad/decisions/inbox/` for Wave 5; the wire contracts captured here will need cross-checking against the other agents' memos when they land.

## Stability

- **Phase J Wave 5 filter (`--filter "Wave=Phase-J-5"`):** 14 passed / 0 failed / 0 skipped (3 + 4 + 3 + 4).
- **Full suite (`dotnet test src/backend/Mahjong.Autotable.slnx`):** 445 passed / 0 failed / 0 skipped. **+14** from Wave 4's 431.
- Zero-skip streak: 9 consecutive waves green.

## Cross-agent coordination

- Test files are strict-additive (no edits to existing tests).
- `selectors.md` change is additive (3 new sections inserted before `## Stability contract`).
- `Program.cs` is the single Apone-lane file I touched (one-liner `/metrics` route map); if Apone's parallel commit re-wires `/metrics` differently, my edit needs to be re-conciled. Default behaviour (no map at all) makes my Metrics tests fail; the one-liner is the minimum viable fix.

— Vasquez
