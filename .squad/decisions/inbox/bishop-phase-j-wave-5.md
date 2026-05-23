# Bishop — Phase J Wave 5 backend memo

**Branch:** `stlong/phase-j-wave-5-completion`
**Baseline gate:** 431/0/0 (entering this wave; from PR #40).
**Final gate (Bishop-scope filter, excludes Apone's still-uncommitted
Observability tests):** 435/0/0. Full-suite run shows 431 pre-existing +
4 new Hudson Players-suite tests = 435 passing; the remaining
`Observability.MetricsEndpointTests` 3 fails are Apone's runtime code
not-yet-committed, **out of Bishop's scope**.

**Touch-points:**

| File | Kind |
| --- | --- |
| `src/.../Changsha/ChangshaDomain.cs` | Schema (IsPublic/PublicName/CreatorPlayerId on ChangshaGameState) |
| `src/.../Changsha/Runtime/ChangshaGameRuntime.cs` | Runtime matchmaking + host transfer + stats hookup |
| `src/.../Changsha/ChangshaHub.cs` | SignalR RPCs: SetGamePublic / JoinRandom / UpdateProfile + ProfileLoaded on connect |
| `src/.../Data/AppDbContext.cs` | DbSet<PlayerProfile> / DbSet<PlayerStats> + OnModelCreating |
| `src/.../Data/DatabaseBootstrapper.cs` | EnsureSqlitePlayerTablesAsync defensive CREATE TABLE |
| `src/.../Players/PlayerProfile.cs` | EF entity (PK PlayerId, DisplayName, AvatarColor) |
| `src/.../Players/PlayerStats.cs` | EF entity (cascade FK to PlayerProfile) |
| `src/.../Players/PlayerProfileService.cs` | Service (singleton + scoped DbContext) |
| `src/.../Matchmaking/MatchmakingService.cs` | Lobby projection + SetGamePublic / JoinRandom passthrough |
| `src/.../Matchmaking/MatchmakingController.cs` | GET /api/matchmaking/lobby |
| `src/.../Persistence/Migrations/AddPlayerProfileAndStats.cs` | EF migration (canonical schema source) |
| `src/.../Program.cs` | DI for PlayerProfileService / MatchmakingService + AddControllers + MapControllers |

---

## Wire contracts

### REST — Matchmaking lobby

```
GET /api/matchmaking/lobby
→ 200 OK
  {
    "games": [
      {
        "gameId": "<guid>",
        "publicName": "Bishop's Game" | null,
        "creatorDisplayName": "Player-AABBCC" | null,
        "seatedCount": 1,
        "maxSeats": 4,
        "variant": "Changsha",
        "createdAt": "2026-05-23T03:12:06.000Z"
      },
      ...
    ]
  }
```

Cap **50** entries, newest-first by `CreatedAt`. Only games with
`IsPublic == true` **and** `Phase == Seating` appear. The
`creatorDisplayName` is resolved via `PlayerProfileService` (default name
applied when no profile row exists yet).

### SignalR — Matchmaking RPCs

```
SetGamePublic(gameId: string, isPublic: bool, publicName?: string)
  → { success: true, isPublic: <bool>, publicName: <string?> }
  → throws HubException if caller is not the original host
    (state.CreatorPlayerId mismatch) or Phase != Seating.
  → publicName trimmed; null or empty → cleared; capped at 64 chars.

JoinRandom(variant?: string)
  → { matched: true,  gameId: <guid>, seatIndex: <int 0-3> }
  → { matched: false }                       // no candidate
  → variant defaults to "Changsha"; any non-match returns matched=false.
  → matches a public Seating-phase game with at least one free non-bot
    seat. On race (last seat lost between candidate-pick and seat-take)
    returns matched=false — frontend should retry.
```

### SignalR — Profile RPCs + event

```
OnConnectedAsync → server emits "ProfileLoaded" to caller:
  {
    profile: {
      playerId: <connectionId>,
      displayName: <string>,
      avatarColor: "#RRGGBB",
      createdAt: <iso>,
      lastSeenAt: <iso>
    },
    stats: {
      gamesPlayed: <int>,
      gamesWon: <int>,
      totalScore: <long>,
      highestSingleGameScore: <int>,
      longestWinStreak: <int>,
      currentWinStreak: <int>,
      lastGameAt: <iso?>
    }
  }

UpdateProfile(displayName: string, avatarColor?: string)
  → same DTO shape as ProfileLoaded.
  → displayName: trimmed; 1-32 chars; throws on whitespace-only / leading
    or trailing whitespace.
  → avatarColor: optional, must match ^#[0-9A-Fa-f]{6}$.
```

### `GameCompleted` (already shipped Wave 2 — payload unchanged)

Bishop's Wave 5 addition is server-side only: the existing
`GameCompleted` emit now also persists career stats via
`PlayerProfileService.RecordGameCompletedAsync`. Bots
(`playerId.StartsWith("bot-")`) are filtered. Winner set = all PlayerIds
tied at the top cumulative score (handles 2-way splits). Wrapped in
try/catch so a DB failure cannot break game completion.

---

## Schema / migration

Single migration: `AddPlayerProfileAndStats`. Because this is the **first**
EF migration in the project, the migration also includes the existing
`ChangshaGames` / `ChangshaGameEvents` tables — that's intentional, it's
the canonical schema baseline going forward.

Existing SQLite installs come up via `DatabaseBootstrapper`:
`EnsureCreatedAsync` + `EnsureSqlitePlayerTablesAsync` (CREATE TABLE IF
NOT EXISTS), so no out-of-band `dotnet ef database update` is required.
**Postgres / SqlServer deploys MUST run `dotnet ef database update`** in
CI — the bootstrap only fires for SQLite.

---

## Notes for downstream agents

- **Hicks (frontend):** lobby data is REST (`GET /api/matchmaking/lobby`);
  refresh on a timer. Mutations are SignalR (`SetGamePublic`,
  `JoinRandom`). Display name + colour from `ProfileLoaded`.
- **Vasquez (testing):** Hudson's Players suite already covers
  `PlayerProfileService` + stats aggregation. Lobby endpoint + SignalR
  RPC integration tests welcome.
- **Apone (DevOps):** migration runs offline at startup for SQLite; no CI
  step. For Postgres / SqlServer please add `dotnet ef database update`
  to the deploy pipeline.

---

## PlayerId / reconnect limitation (v1)

`PlayerId` is the SignalR `Context.ConnectionId` at first connect — a
reconnect (or a different browser tab) gets a fresh profile + zeroed
stats. Phase K candidate: cookie / auth-token-derived stable id so a
returning player resumes their career stats.

---

## Commit

| commit | description |
| --- | --- |
| `64aac5c` | feat(backend): Phase J Wave 5 — public matchmaking lobby + player profile + career stats |
