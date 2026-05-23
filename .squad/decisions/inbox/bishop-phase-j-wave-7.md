# Bishop — Phase J Wave 7

**Branch:** `stlong/phase-j-wave-7-polish`
**Scope:** backend polish — avatar-colour palette alignment, replay snapshot persistence
+ REST surface, `/health` JSON detail, spec-drift sweep on `docs/rules/changsha-spec.md`.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **Passed: 554, Failed: 0, Skipped: 0** (baseline was 456/0/0; +98 net = my new
backstops + Vasquez's forward-staged Wave 7 contract tests already in the working tree).

---

## Task 1 — Avatar colour palette alignment with frontend

### Problem

Wave 5 shipped `PlayerProfile.AvatarColor` defaulting to literal grey `#808080`
on the class initializer and `PlayerProfileService.DefaultAvatarColor()`
returning a hashed pick from a 16-entry HSL-spaced palette. Neither matched
Hicks's frontend palette (`src/frontend/autotable-src/src/profile.ts`:
`AVATAR_COLOR_PRESETS`), which is the 8-entry "flat-UI" set the lobby chip
picker actually surfaces. Net effect: a profile created by an autotable-WS
client (no `POST /api/identity` round-trip) showed up as grey on the chip,
and a profile created by a SignalR client could land on a hashed colour that
isn't in the frontend picker, so the user couldn't "find" their own colour
in the swatch grid.

Vasquez's pre-staged `AvatarColorPaletteTests.cs` (6 tests, Wave 7 forward
work) pinned the contract: `DefaultAvatarColor` must only emit palette
members, and the class-init default must be `palette[0]`.

### Fix

1. `PlayerProfile.AvatarColor` class-init default → `#c0392b` (palette index 0).
   Exposed the canonical constant as
   `PlayerProfile.DefaultPaletteAvatarColor` so callers (admin tools,
   factory methods) don't drift again.
2. `PlayerProfileService.DefaultAvatarColor(string playerId)` palette
   trimmed from the legacy 16-entry HSL set to the canonical 8-entry
   Hicks palette:
   ```csharp
   string[] palette = {
       "#c0392b", "#e67e22", "#f1c40f", "#2ecc71",
       "#16a085", "#2980b9", "#8e44ad", "#34495e",
   };
   ```
   Order matches `AVATAR_COLOR_PRESETS` so `palette[i]` on the backend and
   `AVATAR_COLOR_PRESETS[i]` on the frontend point at the same chip
   colour for any deterministic i. FNV hash → `palette[hash % 8]` pick
   stays deterministic per-player-id.
3. Existing `PlayerProfileServiceTests.GetOrCreate_CreatesNewProfile_WithDeterministicDefaults`
   asserted `^#[0-9A-F]{6}$` (uppercase). The palette is lowercase by
   convention (matches frontend), so the regex was relaxed to
   `^#[0-9A-Fa-f]{6}$`. The deterministic-default subassertion still
   holds.

### Touch-points
- `src/.../Players/PlayerProfile.cs` — class-init default + `DefaultPaletteAvatarColor` const.
- `src/.../Players/PlayerProfileService.cs` — 8-entry palette.
- `tests/.../Players/PlayerProfileServiceTests.cs` — regex case-insensitive,
  added Wave 7 backstop assertion (default colour is a palette member).

---

## Task 2 — Replay snapshot persistence + REST surface

### Endpoint contract

`GET /api/games/{gameId}/replay`

- `gameId` must parse as a `Guid`. Malformed → `400 {"error":"gameId must be a GUID."}`.
- No persisted row for the id (unknown game OR game still in progress —
  Wave 7 persists at GameCompleted emission only) → `404 {"error":"Replay not found.","gameId":"…"}`.
- `200` body:
  ```json
  {
    "gameId": "9b3a7f01-…",
    "createdAt": "2026-05-24T04:32:11.812Z",
    "events": [
      {
        "turn": 1,
        "phase": "Setup",
        "actor": -1,
        "action": "game-created",
        "tilesJson": "[]",
        "timestampUtc": "2026-05-24T04:21:00.000Z"
      },
      // …one entry per state.EventLog row, **sorted by `turn` ascending**.
    ]
  }
  ```
- Rate limit: opted into `token-bucket-api` policy via `[EnableRateLimiting(…)]`
  on the controller. Read-only and large by nature (an end-game replay can
  run into hundreds of KB), so the existing per-IP burst cap is appropriate.

**Sort guarantee.** Vasquez's `GameReplayEndpointTests.GameReplay_Events_AreOrderedByTurnAscending`
pins this contract: regardless of how rows landed in `EventsJson` (the
runtime writer appends in chronological/sequence order — admin import or
partial merge may not), the endpoint **always** hands the frontend
scrubber a monotonic `turn` sequence. Tiebreak is stable on serialisation
order. Implemented in the controller, not the writer, so the controller
normalises for any future writer too.

### EF entity + migration

```csharp
public sealed class ChangshaGameReplay {
    public Guid Id           { get; set; } = Guid.NewGuid();
    public Guid GameId       { get; set; }  // NOT FK — see decision note below
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string EventsJson  { get; set; } = "[]";
}
```

- DbSet on `AppDbContext` → `ChangshaGameReplays`.
- Migration: `Persistence/Migrations/20260524000000_AddChangshaGameReplay.cs`
  (manual — multi-context project means `dotnet ef migrations add` would
  scaffold under provider-specific folders. See decision below).
- `Data/DatabaseBootstrapper.cs` extended with `EnsureSqliteReplayTablesAsync`
  for in-memory test harnesses that bypass migrations.

**Decision — no FK on `GameId`.** First pass had
`FK_ChangshaGameReplays_ChangshaGames_GameId ON DELETE CASCADE`. Dropped
for two reasons:

1. **Test harness compatibility.** The Changsha test fixture runs with
   `ChangshaGameRuntimeOptions.PersistSnapshots = false` (we don't want
   to write parent `ChangshaGames` rows during 200+ unit tests). With
   the FK in place, replay inserts would fail and silently swallow
   (the runtime `PersistReplayAsync` catches exceptions).
2. **Historical artifact, not relational.** Replays should outlive their
   parent game row — if a tournament admin purges old completed games,
   the replay JSONs are still useful (analytics, video review, dispute
   resolution). FK + cascade would erase them; a soft reference by
   `GameId` (still queryable) preserves them.

The `EventsJson` payload is self-contained, so consumers don't need to
join back to `ChangshaGames`.

### Runtime hook

`ChangshaGameRuntime.PersistReplayAsync(ChangshaGameInstance instance)`
called at the **end of `EmitGameCompletedAsync`** (line ~1820). Serializes
`state.EventLog` into the documented per-event shape:

```json
{
  "turn": <int>,         // event.TurnNumber
  "phase": "<bucket>",   // Setup / Deal / Discard / Claim / Hu / Other — see ReplayPhaseBucket
  "actor": <int>,        // event.SeatIndex; -1 for system events
  "action": "<string>",  // raw event.EventType (e.g. "tile-discarded")
  "tilesJson": "[…]",    // JSON-encoded int[] (single TileId → "[id]", null → "[]")
  "timestampUtc": "…"    // ISO-8601 UTC
}
```

`ChangshaGameRuntime.ReplayPhaseBucket(string eventType)` is **public
static** (not internal) so Vasquez's contract test can call it without
`InternalsVisibleTo`. Buckets:

| Bucket | EventTypes |
|--------|------------|
| Setup | game-created, game-started, banker-rotated |
| Deal | dice-rolled, manual-deal-begun, tiles-dealt, tiles-picked-up, tile-drawn, kong-replacement-drawn, wall-exhausted |
| Discard | tile-discarded |
| Claim | claim-window-open, claim-resolved, claim-passed, concealed-kong, added-kong-declared, added-kong |
| Hu | win-declared, scoring-complete, draw-hand, false-hu-penalty |
| Other | (fallback for unknown / future types) |

### Touch-points
- `src/.../Data/Entities/ChangshaEntities.cs` — `ChangshaGameReplay` class.
- `src/.../Data/AppDbContext.cs` — DbSet + entity config (no FK).
- `src/.../Data/DatabaseBootstrapper.cs` — `EnsureSqliteReplayTablesAsync`.
- `src/.../Persistence/Migrations/20260524000000_AddChangshaGameReplay{,.Designer}.cs` — manual migration.
- `src/.../Persistence/Migrations/AppDbContextModelSnapshot.cs` — entity in snapshot.
- `src/.../Changsha/Runtime/ChangshaGameRuntime.cs` — `PersistReplayAsync` +
  `ReplayPhaseBucket`, wired from `EmitGameCompletedAsync`.
- `src/.../Changsha/Runtime/ChangshaReplayController.cs` (new) —
  controller, sort, malformed-JSON fallback.
- `tests/.../Changsha/ChangshaReplayEndpointTests.cs` (new — Bishop).
- `tests/.../Changsha/ChangshaReplayPersistenceTests.cs` (new — Bishop).

---

## Task 3 — `/health` JSON detail + back-compat

### Wire shape

Default `GET /health` now returns:

```json
{
  "status": "healthy",                  // or "degraded" when db probe fails
  "service": "Mahjong.Autotable.Api",
  "version": "1.0.0",
  "timestamp": "2026-05-24T04:32:11.812Z",
  "db": {
    "connected": true,
    "latencyMs": 3
  },
  "activeGames": 7
}
```

- **`db.connected`** comes from a `SELECT 1` round-trip on the resolved
  `AppDbContext` DB connection. On failure: `connected: false`,
  `latencyMs: <elapsed>`, and `status` flips to `"degraded"` — the
  endpoint still returns HTTP 200 so the container stays "alive" for
  k8s. Liveness probes that want strict-200 should use `?simple=1`.
- **`activeGames`** = `IChangshaGameRuntime.GameCount` at probe time.
- **`?simple=1` legacy fallback.** Returns the Wave-3 4-field shape
  (`status / service / version / timestamp`) for any liveness probe
  config that doesn't want to deal with the larger JSON. Recommended
  for `livenessProbe`; the detail shape suits `readinessProbe`.

### Touch-points
- `src/.../Program.cs` — `/health` MapGet expanded; `using Microsoft.EntityFrameworkCore`
  added for `Database.GetDbConnection()`.
- `tests/.../Api/HealthEndpointTests.cs` — two Wave 7 backstops (detailed
  shape, `?simple=1` legacy).

---

## Task 4 — Spec-drift sweep (`docs/rules/changsha-spec.md`)

Spec was at v1.2 (2026-05-13). Bumped to **v1.3 (2026-05-24)** to record
that the six special-context Big Wins (天和, 地和, 海底捞月, 河底捞鱼,
杠上开花, 抢杠胡) shipped in Phase H Wave 2 + Phase I Wave 1, even though
§4.3 still listed them as "deferred to v2". Moved them into a new
**§4.2.2 Special-Context Big Wins** with an engine-hook table
(`WinResult.IsHeavenlyHand`, etc.) and source-file references. The only
remaining draw-based deferral is **杠上炮 (Kong on Cannon)** — its
"discarder pays both sides on a post-kong discard win" plumbing is genuine
new state-machine work, not just a context flag.

Updated the §4 intro pattern list (5 → 6 categories), §4.3 deferred-list
header text, and the v1.3 changelog row. No engine code touched — pure
documentation reconciliation.

---

## Notes for downstream

### Vasquez
- All six forward-staged Wave 7 test files now pass end-to-end:
  `AvatarColorPaletteTests`, `HealthCheckJsonTests`,
  `Persistence/DbProviderSwitchingTests` (Apone's contract, untouched
  by this wave), `Replay/GameReplayEndpointTests`,
  `Deploy/*` (Apone's contract — untouched).
- `ChangshaGameRuntime.ReplayPhaseBucket(string)` is `public static`;
  feel free to call from any future replay-format contract test
  without `InternalsVisibleTo`.
- The `tilesJson` field on each replay event is itself a JSON-encoded
  `int[]` string (per Stephen's wire-shape brief), not a structured
  array. The frontend deserialises it.
- Replay rows are NOT cleaned up when their parent `ChangshaGames` row
  is deleted. If a future retention policy needs to GC them, the
  `CreatedAt` column is indexed-friendly (DateTime UTC).

### Hicks
- `POST /api/identity` and the lobby chip swatch grid now agree on
  exactly 8 colours. Profiles minted before this wave (in the wild)
  may still hold a 16-palette hex; the frontend should accept any
  `#RRGGBB` value rather than only palette members (defensive).
- `GET /api/games/{gameId}/replay` is ready to wire to the replay
  player UI. Events arrive sorted by turn ascending; tiebreak is
  serialisation order. Empty/missing games surface as 404.
- `GET /health` default returns the richer JSON suitable for a small
  readiness widget. `GET /health?simple=1` is the boring 4-field
  legacy shape.

### Apone
- The provider-specific contexts (`PostgresAppDbContext` /
  `SqliteAppDbContext` / `SqlServerAppDbContext` and design-time
  factories) and `ServiceCollectionExtensions.cs` rework were
  already on disk uncommitted at the start of this wave — I left
  them alone. Wave 7's new migration
  (`20260524000000_AddChangshaGameReplay`) was scaffolded against
  the **base `AppDbContext`** only (manually) to avoid creating
  three sibling migrations under `Migrations/{Sqlite,Postgres,SqlServer}/`.
  When you ship the multi-context migration story, you'll likely
  want to regenerate this migration under each provider's folder
  using `dotnet ef migrations add … --context Sqlite/Postgres/SqlServer`.
- Vasquez's `DbProviderSwitchingTests.AddPersistence_PostgreSqlWithoutConnectionString_ThrowsOnResolve`
  currently passes when run in isolation but flakes in a full
  parallel suite (test interleaving against the EF Core option-lambda
  capture). Worth a parallelism review on your end.

---

## Breaking changes

**None.** All three surfaces are additive:
- Avatar palette change keeps the same wire type (`string`), just a
  different default set. Pre-existing profiles keep their stored colour.
- Replay endpoint is a brand-new path.
- `/health` default response is a strict superset of the Wave-3 4-field
  shape; `?simple=1` preserves the exact legacy shape for any liveness
  probe that pinned it.

## Test gate

```
Passed: 554, Failed: 0, Skipped: 0, Total: 554, Duration: 14 s
```
