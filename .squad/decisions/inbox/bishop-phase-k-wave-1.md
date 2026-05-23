# Bishop — Phase K Wave 1: Production bring-up & OAuth hardening

**Date:** 2026-05-23T09-23-30Z
**Branch:** `stlong/phase-k-wave-1-bringup` (cut from `main` @ `9a52ef1`)
**Requested by:** Stephen Long
**Scope:** Post-completion polish + production bring-up. Five backend
surfaces: (1) OAuth hardening (PKCE S256 + HMAC-signed state + nonce),
(2) tournament-WS reconnect grace + forfeit, (3) CSP strict-styles
in Production, (4) match-history export endpoint, (5) per-tournament
Elo ratings with quarterly seasonal reset. Strict no-frontend.

## Test gate

`dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **Passed: 977, Failed: 0, Skipped: 0** (+145 over Wave 10 baseline
of 832). Includes Vasquez's forward-staged contract tests under
`Auth/OAuth{Pkce,StateNonce,ProviderHealthCheck,Callback}Tests.cs`,
`Players/{PlayerRatingTests,SeasonRolloverServiceTests,GamesHistoryEndpointTests}`,
`Tournaments/{TournamentMatchForfeit,TournamentReconnectGrace,RatingsLeaderboard,SeasonRolloverIntegration,PlayerRatingService,TournamentForfeitService}Tests`,
plus a dedicated `Auth/OAuthStateProtectorTests`.

---

## Surface 1 — OAuth hardening: PKCE S256 + HMAC state + nonce

### `Auth/OAuthStateProtector.cs` (new, singleton)

HMAC-signed token issued at `/api/auth/login/{provider}` and verified
at the callback. Format:

```
base64url( nonce(16) | expiryUnix(8) | hmacSha256(nonce|expiry, key)(32) )  → 56 bytes
```

Public surface:
```csharp
public sealed class OAuthStateProtector
{
    public OAuthStateProtector(IOptions<AuthOptions> options, ILogger<OAuthStateProtector> logger);
    public StateIssue Issue(TimeSpan? ttl = null);           // default 10 min
    public StateVerifyResult Verify(string token);
}
public sealed record StateIssue(string Token, string Nonce);
public sealed record StateVerifyResult(bool Ok, string? Nonce, string? Reason);
```

Signing key derivation: `SHA256(UTF8(AuthOptions.StateSigningKey))` so
any string maps to a fixed 256-bit key. Empty/missing config →
per-process random 32-byte key minted at ctor time with a warning log
("OAuth state will not survive restart"). Verify rejects on
length/format/expiry/HMAC-mismatch with a clear `Reason`.

### `Auth/OAuthService.cs` (extended)

New cookie constants (all `HttpOnly`, `SameSite=Lax`, `Secure` matching
the existing `mahjong_oauth_state`):
- `StateCookieName = "mahjong_oauth_state"` — now holds the **nonce** only
- `PkceVerifierCookieName = "mahjong_oauth_pkce"` — base64url verifier
- `NonceCookieName = "mahjong_oauth_nonce"` — id_token nonce binding

New overloads (old signatures preserved for backward compat):
```csharp
public string BuildAuthorizeUrl(string provider, string state, string codeChallenge, string nonce);
public Task<OAuthUserInfo?> ExchangeAndFetchUserInfoAsync(
    string provider, string code, string codeVerifier, string? expectedNonce, CancellationToken ct);
```

Helpers (all `internal static`, surfaced for tests):
- `GeneratePkceVerifier()` — 32 random bytes → base64url
- `BuildPkceChallenge(verifier)` — `base64url(sha256(ascii(verifier)))`
- `TryReadIdTokenNonce(idToken, out nonce)` — JWT payload-only parse,
  **no signature validation** (we only compare against the cookie)
- `Base64UrlEncode/Decode`

### `Auth/AuthController.cs` (rewritten Login + Callback)

**Login** (`GET /api/auth/login/{provider}`):
1. Mint state via `OAuthStateProtector.Issue(10 min)` → `(token, nonce)`
2. Mint PKCE verifier + challenge (S256)
3. Set three cookies: state-nonce, pkce-verifier, id-token-nonce
4. Redirect with `?state=<token>&code_challenge=<challenge>&code_challenge_method=S256&nonce=<nonce>`

**Callback** (`GET /api/auth/callback/{provider}`):
1. `OAuthStateProtector.Verify(state_query)` → must succeed
2. Compare `verify.Nonce` to `mahjong_oauth_state` cookie value
3. Read PKCE verifier from cookie, nonce from cookie
4. Exchange code + verifier; if id_token present, assert
   `TryReadIdTokenNonce(idToken) == cookieNonce`
5. Delete all three cookies (`Response.Cookies.Delete`)
6. Existing identity / cookie-issue flow unchanged

### `Auth/AuthOptions.cs`

Added `string StateSigningKey { get; set; } = "";` — operator-supplied
HMAC key. Documented in `docs/oauth-setup.md`.

### `Auth/OAuthProviderHealthCheck.cs` (new, IHostedService-free)

Probes the OIDC discovery doc + JWKS for each enabled provider with a
short timeout (5 s). Exposed via `/health.oauth.providers.{name}`:

```json
"oauth": {
  "providers": {
    "google":  { "healthy": true,  "statusCode": 200, "error": null, "discovery": "ok" },
    "github":  { "healthy": false, "statusCode": null, "error": "Timeout", "discovery": "fail" }
  }
}
```

Knobs:
- `Authentication:HealthCheck:SkipDiscovery=true` short-circuits the
  HTTP probe → `Healthy=true, Discovery="skipped"`. Used by tests
  + air-gapped envs.

CLI mode `dotnet run -- verify-oauth` (registered in `Program.cs` at
~line 53–95) builds a minimal host, runs the health check once, prints
JSON, exits 0 on all-healthy / 2 on any-fail. Documented in
`docs/oauth-setup.md`.

---

## Surface 2 — Tournament WS reconnect grace + forfeit

### `Tournament/TournamentForfeitService.cs` (new, `BackgroundService`)

Singleton hosted-service that tracks WS disconnects during tournament
matches and forfeits after a configurable grace.

Public surface:
```csharp
public sealed class TournamentForfeitService : BackgroundService
{
    public TournamentForfeitService(IServiceScopeFactory scopes,
        IOptions<TournamentForfeitOptions> opts,
        ILogger<TournamentForfeitService> log);
    public void NoteDisconnect(string playerId, string gameId);
    public void NoteReconnect(string playerId);
    public IReadOnlyDictionary<string, DateTime> PendingDisconnects { get; }
    public Task SweepOnceAsync(CancellationToken ct);                 // test seam
    public void BackdateDisconnect(string playerId, DateTime utc);    // test seam
}
public sealed class TournamentForfeitOptions
{
    public TimeSpan GraceWindow { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(5);
    public const string ForfeitAuditMarker = "tournament-forfeit";
}
```

Wired into `ChangshaGameRuntime.HandleDisconnectAsync` (note-disconnect)
and `ChangshaGameRuntime.ReconnectAsync` (note-reconnect). Bots
(playerId prefix `bot-`) are filtered out. On sweep-expiry,
`TournamentService.ForfeitMatchAsync(gameId, playerId)` is called and
an audit row with `ForfeitAuditMarker = "tournament-forfeit"` is
written.

### `Tournament/TournamentService.cs` (extended)

Two additions:
```csharp
public Task<bool> ForfeitMatchAsync(string gameId, string forfeitedPlayerId, CancellationToken ct);
public static bool GameIdsContains(string gameIdsCsv, string gameId);  // shared helper
```

`ForfeitMatchAsync` flips `TournamentMatch.ForfeitedByDisconnect=true`
+ `ForfeitedPlayerId=<playerId>`, decides the surviving winner by
highest current score (deterministic tiebreak by seat index), and
advances the bracket via the existing
`Tournament.Internal.BracketAdvancementService` path.

### `Data/Entities/ChangshaEntities.cs` (extended)

`TournamentMatch` grew two columns: `bool ForfeitedByDisconnect`,
`string? ForfeitedPlayerId`. Migrations regenerated for all three
providers (`20260523085412_AddMatchHistoryAndRatings` Sqlite,
`20260523085424_AddMatchHistoryAndRatings` Postgres,
`20260523085436_AddMatchHistoryAndRatings` SqlServer).

---

## Surface 3 — CSP strict styles in Production

Already shipped in pre-compaction batch as the `CspStrictStyles`
knob in `Program.cs` (Production gate). `appsettings.Production.json`
default is `true`; dev override unchanged so live-reload-style
hot-injection still works in `Development`. No new code in this wave.

---

## Surface 4 — Match-history export endpoint

### `Data/Entities/ChangshaEntities.cs` (extended)

New entity:
```csharp
public sealed class PlayerGameHistory {
    public Guid Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string GameId { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int FinalScore { get; set; }            // NOT "Score"
    public bool Won { get; set; }
    public string OpponentPlayerIds { get; set; } = "";  // comma-joined
    public string? RulePresetId { get; set; }
}
```

Indexed on `(PlayerId, CompletedAt DESC)`. Same migration set as
Surface 2.

### `Players/PlayerGameHistoryService.cs` (new, singleton)

```csharp
public Task<int> RecordAsync(string gameId, IReadOnlyList<PlayerSeat> seats,
    DateTime startedAt, DateTime completedAt, string? rulePresetId,
    CancellationToken ct);
public Task<(int total, IReadOnlyList<PlayerGameHistory> rows)> ListAsync(
    string playerId, int limit, int offset, CancellationToken ct);
```

Bots (`bot-*` prefix) are excluded. Called from
`ChangshaGameRuntime.OnGameCompleted` (the same callsite as the Elo hook).

### `Players/GamesHistoryController.cs` (new)

- `GET /api/players/{playerId}/games?limit=&offset=&format=json|csv`
  - JSON envelope: `{ playerId, total, limit, offset, games: [...] }`
  - CSV columns (no `PlayerId` — filtered by route):
    `GameId,StartedAt,CompletedAt,FinalScore,Won,OpponentPlayerIds,RulePresetId`
  - `Content-Disposition: attachment; filename="games-{playerId}.csv"`
- Limit clamp `[1, 200]`, default 50.

---

## Surface 5 — Per-tournament Elo + quarterly seasonal reset

### `Data/Entities/ChangshaEntities.cs` (extended)

```csharp
public sealed class PlayerRating {
    public string PlayerId { get; set; } = "";   // PK
    public int Rating { get; set; } = 1200;
    public int GamesPlayed { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Season { get; set; } = "";    // YYYY-Qn
}
public sealed class PlayerRatingHistory {
    public Guid Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string Season { get; set; } = "";
    public int FinalRating { get; set; }
    public int GamesPlayed { get; set; }
    public DateTime FrozenAt { get; set; }
}
```

### `Tournament/PlayerRatingService.cs` (new, singleton, scope-shaped)

Elo strategy (4-player Mahjong):
- K = 32, default rating = 1200
- Winner gains vs **average loser** rating
- Each loser loses vs winner's **PRE-match snapshot** rating (so losses
  sum correctly regardless of update order)
- Bots (`bot-*`) excluded

```csharp
public Task ApplyMatchResultAsync(IReadOnlyList<PlayerScore> finalScores, CancellationToken ct);
public Task<IReadOnlyList<PlayerRating>> LeaderboardAsync(int limit, int offset, CancellationToken ct);
public Task<IReadOnlyList<PlayerRatingHistory>> SnapshotLeaderboardAsync(string season, int limit, int offset, CancellationToken ct);
public static string SeasonFromDate(DateTime utc);   // YYYY-Qn
public static string PriorSeason(string season);     // year-wrap aware
```

**EF translation gotcha (avoided):** `OrderBy(r => r.PlayerId,
StringComparer.Ordinal)` is **not** translatable by EF Sqlite/Pgsql.
Use plain `OrderBy(r => r.PlayerId)`; SQL collation matches our
needs.

### `Tournament/SeasonRolloverService.cs` (new, `BackgroundService`)

Polls every `RolloverCheckInterval` (default 1 h). When UTC month
crosses a quarter boundary, calls `RolloverOnceAsync(priorSeason)`
which atomically:
1. Snapshots every `PlayerRating` row whose `Season != currentSeason`
   into `PlayerRatingHistory` (skips rows where the
   `(PlayerId, Season)` pair already exists — idempotent)
2. Deletes those stale `PlayerRating` rows so the next match starts
   them at the default 1200 for the new season

### `Tournament/RatingsController.cs` (new)

- `GET /api/ratings/leaderboard?limit=&offset=` → current-season top-N
- `GET /api/ratings/season/{season}?limit=&offset=` → frozen snapshot

JSON: `{ season, total, limit, offset, ratings: [{ playerId, rating, gamesPlayed, updatedAt }] }`.

### Runtime hook

`ChangshaGameRuntime.AdvanceTournamentMatchAsync` calls
`PlayerRatingService.ApplyMatchResultAsync` after the match is marked
complete. `OnGameCompleted` also writes one
`PlayerGameHistory` row per non-bot seat.

---

## DI wiring summary (Program.cs ~lines 153–220)

```csharp
builder.Services.Configure<AuthOptions>(authSection);          // IOptions path (new)
builder.Services.AddSingleton(authOptions);                    // direct path (legacy)
builder.Services.AddSingleton<OAuthStateProtector>();
builder.Services.AddSingleton<OAuthProviderHealthCheck>();

builder.Services.Configure<RatingOptions>(builder.Configuration.GetSection("Rating"));
builder.Services.AddSingleton<PlayerRatingService>();          // singleton: uses IServiceScopeFactory

builder.Services.AddSingleton<SeasonRolloverService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SeasonRolloverService>());

builder.Services.Configure<TournamentForfeitOptions>(builder.Configuration.GetSection("Tournament:Forfeit"));
builder.Services.AddSingleton<TournamentForfeitService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TournamentForfeitService>());

builder.Services.AddSingleton<PlayerGameHistoryService>();
```

The `IOptions<AuthOptions>` registration was the fix for the
final-mile DI issue: pre-Wave-K Bishop registered `AuthOptions`
only as a bare singleton, so newer services taking
`IOptions<AuthOptions>` (OAuthProviderHealthCheck,
OAuthStateProtector) couldn't resolve. Both paths are now bound;
nothing else changed signature.

`PlayerRatingService` deliberately moved from `Scoped` → `Singleton`
mid-wave: it holds an `IServiceScopeFactory` and opens a fresh
`AppDbContext` per call (mirrors the
`PlayerProfileService`/`MatchmakingService`/`TournamentService`
pattern). This unblocks integration tests that resolve from
`Factory.Services` (root-provider).

---

## appsettings additions

```json
"Authentication": { "StateSigningKey": "", "HealthCheck": { "SkipDiscovery": false } },
"Rating":     { "DefaultRating": 1200, "KFactor": 32 },
"Tournament": { "Forfeit": { "GraceWindowSeconds": 60, "SweepIntervalSeconds": 5 } }
```

`appsettings.Production.json` keeps `CspStrictStyles=true` and an empty
`StateSigningKey` placeholder so operators are forced to set it.

---

## Test additions (Bishop-authored)

- `Auth/OAuthStateProtectorTests` — round-trip, expiry, tamper, wrong-key
- `Tournaments/PlayerRatingServiceTests` — Elo math, bot filter, season helpers
- `Tournaments/SeasonRolloverIntegrationTests` — boundary freeze + idempotent re-run
- `Tournaments/RatingsLeaderboardEndpointTests` — paging, JSON shape, season scope
- `Tournaments/TournamentForfeitServiceTests` — note/reconnect, backdate-sweep, bot filter, audit marker
- `Players/GamesHistoryEndpointTests` — JSON envelope, CSV header+row, paging, route-scoped filter

All Vasquez forward-staged contract tests
(`Auth/OAuth{Pkce,StateNonce,ProviderHealthCheck,Callback}Tests`,
`Players/{PlayerRatingTests,SeasonRolloverServiceTests}`,
`Tournaments/{TournamentMatchForfeit,TournamentReconnectGrace}Tests`)
bind cleanly to the shipped surface.

---

## Docs

`docs/oauth-setup.md` — provider walk-through (Google + GitHub),
StateSigningKey rotation, `verify-oauth` CLI usage,
`Authentication:HealthCheck:SkipDiscovery` for air-gapped envs.

---

## Outstanding / next-wave hand-offs

- **Operator runbook for season rollover** — currently triggered by
  the BackgroundService on a 1-hour timer. If we ever need
  on-demand rollover (e.g. for staging snapshots) a small admin
  endpoint calling `SeasonRolloverService.RolloverOnceAsync` would
  be ~10 lines.
- **Postgres collation** — `OrderBy(r => r.PlayerId)` uses DB
  collation. If a future host runs `lc_collate=C` we may see
  player-id ordering differ from Sqlite's BINARY. Tests assert
  by rating-then-id so this won't bite the suite, but worth a
  note for any consumer who needs canonical pagination.
- **PKCE without id_token** — providers that return no `id_token`
  (raw OAuth2, not OIDC) skip the nonce assertion gracefully;
  `TryReadIdTokenNonce` returns false and we don't fail the
  callback. GitHub falls in this bucket.
