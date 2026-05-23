# Bishop — Phase J Wave 6 backend memo

**Branch:** `stlong/phase-j-wave-6-completion`
**Baseline gate:** 445/0/0 (entering this wave; from Wave 5 merge).
**Final gate:** 445/0/0 — no regressions; every existing test was
preserved, the runtime-call-shape changes were absorbed by named-arg
updates in 9 test files.

This wave decouples **PlayerId** from **ConnectionId** so a returning
player keeps the same profile + career stats across reconnects,
refreshes, and the ChangshaHub ⇄ autotable-WS transport hop.

---

## 1. New surfaces

### `POST /api/identity`

Idempotent mint/refresh of the persistent identity cookie. Response:

```json
{
  "playerId":    "<32-hex>",
  "displayName": "Player-XXXXXX",
  "avatarColor": "#RRGGBB",
  "createdAt":   "2026-…Z",
  "lastSeenAt":  "2026-…Z"
}
```

Set-Cookie response header (every call rewrites — Max-Age slides
forward one year):

```
Set-Cookie: mahjong_pid=<32-hex>;
            HttpOnly;
            Secure;            (IsHttps only)
            SameSite=Lax;
            Max-Age=31536000;
            Path=/
```

The cookie is marked `IsEssential` so the GDPR/consent filter doesn't
strip it. The token is an opaque random `Guid.NewGuid().ToString("N")`
— no JWT, no signing key. Worst case theft = impersonation of an
anonymous player; the game has no privileged operations that would
make that worse than a stolen session cookie.

**Frontend usage (Hicks):** call this **once** at app boot, before
opening the SignalR hub or the autotable-WS socket, so a long-lived
cookie is pinned to the browser. Without it, the hub still mints an
in-memory id for the lifetime of the connection (session-scoped
fallback) — but the id won't survive a reload.

### `GET /api/leaderboard`

Joined view over `PlayerStats` + `PlayerProfile`.

```
GET /api/leaderboard
  ?sort=<gamesWon|totalScore|winRate|longestStreak|highestScore>
  &limit=<1..100>     default 50
  &offset=<0..>       default 0
  &minGames=<0..>     default 5
```

Response:

```json
{
  "total": 47,
  "rows": [
    {
      "rank": 1,
      "playerId":               "<32-hex>",
      "displayName":            "Alice",
      "avatarColor":            "#1E88E5",
      "gamesPlayed":            128,
      "gamesWon":               43,
      "winRate":                0.336,
      "totalScore":             10240,
      "highestSingleGameScore": 320,
      "longestWinStreak":       7
    },
    …
  ]
}
```

`total` is paging-independent (the filtered population size), so the
frontend can render "Page 1 of N" without re-querying. `winRate` is a
fraction in `[0,1]`; render as a percentage. Sort parsing is
case-insensitive on the camelCase wire value.

Defaults are constants on `LeaderboardService`
(`DefaultLimit=50`, `MaxLimit=100`, `DefaultMinGames=5`) — keep these
documented if the frontend renders the controls.

---

## 2. Identity model — what changed

### Before (Wave 5)

```text
SignalR connect  → Context.ConnectionId is used as PlayerId everywhere:
                  seat.PlayerId        = ConnectionId
                  state.CreatorPlayerId = ConnectionId   (host check)
                  PlayerProfile.PlayerId = ConnectionId
SignalR reconnect → fresh ConnectionId → fresh "player" → fresh profile.
Autotable WS     → connection.PlayerId is a random per-socket token
                  (never the same twice, never matches anything).
```

### After (Wave 6)

```text
HTTP/SignalR negotiate / WS upgrade
  ↓
PlayerIdentityService reads `mahjong_pid` cookie (or mints+writes a
fresh one). The persistent playerId is stashed on
HubCallerContext.Items["playerId"] / AutotableConnection.PlayerId.
  ↓
Runtime calls now take BOTH a persistent playerId AND a transport
connectionId. seat.PlayerId / state.CreatorPlayerId / stats keying
all use playerId; SignalR group membership + per-connection sends
use connectionId.
```

### `Context.GetPlayerId()` helper

Lives in `Mahjong.Autotable.Api.Players.PlayerIdentityExtensions`:

```csharp
public static string GetPlayerId(this HubCallerContext ctx);
public static string? GetPlayerIdOrNull(this HttpContext ctx);

public const string PlayerIdItemKey = "playerId";
```

Resolution order in the hub helper: `Context.Items["playerId"]` →
`HttpContext.Items["playerId"]` → cookie → `Context.ConnectionId`
(last-resort defensive fallback so the hub never NREs even if the
connect handshake failed to stash an id).

---

## 3. Runtime signature changes

| Method | Wave 5 | Wave 6 |
| --- | --- | --- |
| `CreateGameAsync` | `(seed, bots, hostConnectionId, ct)` | `(seed, bots, hostPlayerId, hostConnectionId, ct)` |
| `TakeSeatAsync` | `(gameId, connectionId, seatIndex?, ct)` | `(gameId, playerId, connectionId, seatIndex?, ct)` |
| `ReconnectAsync` | `(gameId, seatIndex, connectionId, ct)` | `(gameId, seatIndex, playerId, connectionId, ct)` |
| `HandleDisconnectAsync` | `(connectionId, ct)` | `(playerId, connectionId, ct)` |
| `JoinRandomAsync` | `(connectionId, variant, ct)` | `(playerId, connectionId, variant, ct)` |

Implementation rules:

* `seat.PlayerId = playerId` (persistent), `SeatConnections[seat] = connectionId` (transport).
* `state.CreatorPlayerId = hostPlayerId` (set at CreateGame time only; null when no host present).
* `HandleDisconnectAsync` releases SeatConnections by transport id match (so a player holding the same seat from another tab is unaffected), and host-promotion / auto-destroy compares `state.CreatorPlayerId == playerId`.
* `JoinRandomAsync` forwards `(playerId, connectionId)` through to `TakeSeatAsync`.

### Vasquez's Wave-5 blind spot #4 — closed

The autotable-WS bridge now passes a non-null `hostPlayerId` (from the
cookie) into `CreateGameAsync`, so `state.CreatorPlayerId` is populated
on autotable games. `MatchmakingService.SetGamePublicAsync` keys off
that field, which means **autotable-WS games can now be toggled public**
provided the same cookie is presented at SetGamePublic time. Hicks's
"flip table public" UI should now work for both transports.

---

## 4. ChangshaHub changes

* New `PlayerIdentityService _identity` dep.
* `OnConnectedAsync`: reads cookie → mints fallback → stashes on `Context.Items["playerId"]` → loads profile + stats → broadcasts `ProfileLoaded` (Wave-5 behaviour preserved; just rekeyed).
* Every RPC reads identity via `Context.GetPlayerId()` and keeps `Context.ConnectionId` for transport. Affected: `CreateGame`, `TakeSeat`, `ReconnectGame`, `SetGamePublic`, `JoinRandom`, `UpdateProfile`, `OnDisconnectedAsync`.

**Note on cookie mint at OnConnectedAsync:** we deliberately do NOT
write a Set-Cookie header from `OnConnectedAsync` because the SignalR
negotiate response headers have already been flushed by the time it
runs. The mint path produces a session-scoped id only. **Frontend must
call POST /api/identity first** to pin a real cookie. This is in the
contract above.

---

## 5. AutotableWsEndpoint changes

* `MapAutotableWs` resolves the cookie BEFORE `AcceptWebSocketAsync` so a `Set-Cookie` header can ride the upgrade response. New connections without a cookie are minted + the cookie is written immediately.
* `AutotableConnectionManager.HandleConnectionAsync(ws, query, playerId, ct)` — playerId is now a required arg.
* `AutotableConnection.PlayerId` flipped from `{ get; }` to `{ get; init; }` with the same `Guid("N").Substring(0,8)` default as a fallback for unit-test harnesses that construct the type directly.
* `EnsureRuntimeBoundAsync(relayGameId, hostPlayerId, ct)` — host id propagated to `CreateGameAsync`.
* `TryHandleSeatTakeAsync` and `ReleaseRuntimeSeatAsync` pass both ids to the runtime. Transport key is `connection.Id.ToString("N")` (the connection's own GUID).

---

## 6. Test scaffolding patterns for cookie-bearing clients

`WebApplicationFactory<Program>` + a per-test `CookieContainer`
(`HttpClientHandler`-based) is the simplest way to assert
cookie-mediated persistence. For SignalR, attach the same cookie
container to `HubConnectionBuilder.WithUrl(opts => opts.Cookies = …)`.
Example (sketch — pulled out of `.work/PersistentPlayerIdTests.cs.draft`):

```csharp
var handler = new HttpClientHandler { CookieContainer = new() };
using var client = _factory.CreateDefaultClient(handler);
var first  = await client.PostAsync("/api/identity", null);
var second = await client.PostAsync("/api/identity", null);
// Both responses must carry the same playerId; the cookie is now sticky.
```

For autotable-WS tests, build a `ClientWebSocket` with
`ws.Options.Cookies = handler.CookieContainer` and connect to
`ws://…/autotable/ws?gameId=…` — the same cookie will ride the upgrade
handshake.

---

## 7. Hand-off

* **Hicks (frontend):** the contract above is stable. Call `POST /api/identity` once at app boot; subsequent hub/WS connects pick up the cookie automatically. Render `GET /api/leaderboard` with the sort/limit/offset/minGames query controls.
* **Hudson (tests, future wave):** scaffolding pattern is in `.work/PersistentPlayerIdTests.cs.draft` if/when a dedicated cookie/persistence test suite is wanted.
* **Apone (DevOps):** no infra changes required. CORS already allows credentials. Rate limiting policies don't need a new bucket for `/api/identity` / `/api/leaderboard`; they inherit the default `RateLimitingExtensions.ApiPolicy`.
* **Vasquez:** blind spot #4 closed — autotable-WS games can now be flipped public.
