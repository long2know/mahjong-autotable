# Vasquez — Phase J Wave 6 Memo

**Owner:** Vasquez (Senior Test Engineer)
**Branch:** `stlong/phase-j-wave-6-completion`
**Baseline:** Phase J Wave 5 merge @ `3e7db66` (445 passed / 0 failed / 0 skipped).
**Gate (post-Wave-6):** **456 passed / 0 failed / 0 skipped** (+11 net new facts; zero-skip streak holds — 10 consecutive waves green: I.1 → I.2 → I.3 → I.4 → J.1 → J.2 → J.3 → J.4 → J.5 → J.6).

## Scope completed

### Backend test suite (3 new files, 11 facts)

1. **`tests/Players/PersistentPlayerIdTests.cs` (4 facts)** — pins Bishop's persistent-id cookie + `POST /api/identity` + hub-side resolution contract.
   - `PostIdentity_NoCookie_MintsNewPlayer_AndSetsCookie` — first POST without a cookie: 200 OK, body envelope `{ playerId, displayName, avatarColor, createdAt, lastSeenAt }`, `playerId` is 32-char lowercase hex (the Wave-6 cookie format — `Guid.NewGuid().ToString("N")`), Set-Cookie header carries `mahjong_pid=<id>; HttpOnly; SameSite=Lax; Max-Age=31536000; Path=/`. Pins Bishop's `PlayerIdentityService.Mint()` shape + `MintOrRefresh` controller wiring.
   - `PostIdentity_WithExistingCookie_ReturnsSameProfile` — second POST with the Cookie header echoed: 200 OK, body's `playerId` equals the first call's, Set-Cookie on the response still carries the same id (Max-Age slides forward). Pins `PlayerIdentityService.ResolveOrMint(HttpContext)`'s read-then-write order — without this assertion an "always mint and overwrite" regression would silently invalidate every reconnect.
   - `HubConnection_ReadsPlayerIdFromCookie` — SignalR client connects to `/hubs/changsha` with a Cookie header carrying a synthetic 32-hex id; ChangshaHub's `OnConnectedAsync` reads the cookie via `PlayerIdentityExtensions.ResolveOrMint`, stashes it on `Context.Items["playerId"]`, and broadcasts `ProfileLoaded` keyed by the same id. LongPolling transport (TestServer doesn't ship a working WS upgrade in this assembly).
   - `ReconnectAfterDisconnect_PreservesProfile` — disconnect + reconnect with the same cookie returns the same `playerId` on both `ProfileLoaded` broadcasts. Pins Bishop's reconnect-across-transport-hops invariant — `PlayerProfile` survives client restart, browser refresh, and tab close-reopen.

2. **`tests/Leaderboard/LeaderboardEndpointTests.cs` (4 facts)** — pins Bishop's `GET /api/leaderboard?sort&limit&offset&minGames` envelope.
   - `Leaderboard_ReturnsTopByGamesWon_ByDefault` — 10 players seeded with strictly-increasing `GamesWon` (5..14) and `GamesPlayed=20`; sort query omitted → rows monotonically descending by `gamesWon`; top row has `gamesWon=14` + `rank=1`; envelope's `total=10`. Wire-shape contract asserts every field the frontend's `leaderboard.ts:normalizeRow` reads — `rank`, `playerId`, `displayName`, `avatarColor`, `gamesPlayed`, `gamesWon`, `winRate`, `totalScore`, `highestSingleGameScore`, `longestWinStreak` — plus the envelope's `total`.
   - `Leaderboard_FiltersOut_PlayersBelowMinGames` — seed 4 players with `GamesPlayed = 2, 4, 6, 10`; default `minGames=5` returns exactly the 6-game + 10-game seeds; `total=2` (not 4); `minGames=0` surfaces everyone (proves the filter, not some accidental cap, is what's hiding rows).
   - `Leaderboard_SortBy_WinRate_OrdersCorrectly` — A: 8/10 wins (winRate=0.8), B: 6/10 wins (winRate=0.6); `?sort=winRate` returns A first with the 0.8/0.6 projection within 0.0001 epsilon. Pins Bishop's SQL-side `(double)GamesWon / GamesPlayed` projection — a refactor that rounds or re-types `winRate` would surface here.
   - `Leaderboard_RespectsLimitAndOffset` — 60 seeds (each with `GamesPlayed=200, GamesWon=100..159`); `?limit=10&offset=20` returns 10 rows starting at `rank=21`, ending at `rank=30`, with `total=60` still reflecting the full filtered population (NOT capped by `limit`). The top row has `gamesWon=139` (60 - 21 + 100); monotonic-strict-decreasing across the slice. Pins offset's 1-based interpretation — without it the frontend would render wrong rank numbers.

3. **`tests/RateLimiting/RateLimitingTests.cs` (3 facts)** — pins Apone's middleware contract under `Production` + `RateLimiting:Enabled=true`.
   - `PostIdentity_RapidBurst_TriggersRateLimit` — 60 rapid POSTs to `/api/identity` with `X-Forwarded-For: 10.1.1.1` (stable partition key); at least one returns 429; first 429 carries `Retry-After` header + body `{"error":"too_many_requests"}` (Apone's compact form, NOT generic ProblemDetails). Pins Apone's `OnRejected` callback shape.
   - `ApiLeaderboard_ExceedsTokenBucket_Returns429` — same policy proven to travel with every `MapControllers` route (not just `/api/identity`). 60x `/api/leaderboard` → at least one 429 + at least one 200 (proves the bucket isn't broken-at-zero on startup). Body shape re-checked against the canonical compact form.
   - `Health_NotRateLimited_AcceptsBurst` — 100x `/health` + 100x `/api/health` all return 200. Pins `.DisableRateLimiting()` on the probe surface (`Program.cs` L173 + L193) so Docker / k8s liveness probes stay green under the limiter. Operational requirement — a 429 on a probe means the container gets killed.

### Frontend selector documentation

**`src/frontend/autotable-src/tests/selectors.md`** — appended two new Phase J Wave 6 sections additively (no edits to existing Wave 1-5 tables):

- **Onboarding** — 9 selectors from Hicks's `src/identity.ts` first-visit onboarding card (`onboarding-card`, `onboarding-display-name-input`, `onboarding-display-name-error`, `onboarding-avatar-presets`, `onboarding-avatar-color-preset-{0..N}`, `onboarding-avatar-color-custom`, `onboarding-preview-avatar`, `onboarding-continue`, `onboarding-skip`). 7 are `data-testid`; 2 are `id` (the display-name error label + the avatar-presets host, both required for inline `aria-live` / `radiogroup` semantics — same pattern as the Wave-5 Profile drawer).
- **Leaderboard** — 11 selectors from Hicks's `src/leaderboard.ts` lobby leaderboard pane (`lobby-leaderboard-tab`, `lobby-leaderboard-section`, `leaderboard-sort-select`, `leaderboard-min-games-input`, `leaderboard-error`, `leaderboard-loading`, `leaderboard-empty`, `leaderboard-table`, `leaderboard-prev-page`, `leaderboard-paging-summary`, `leaderboard-next-page`) + 1 templated row testid (`leaderboard-row-{0..N}`, cardinality 0..limit, also carries `data-rank` + `data-player-id` for content-based scoping). 9 are `data-testid`; 3 are `id` (the three status placeholders — `leaderboard-error` / `leaderboard-loading` / `leaderboard-empty` — all need stable `id` for `aria-live` references from the table region).

Each entry is sourced with file + line citation; the section docnotes pin the backend contract to the Wave-6 test files above (`PersistentPlayerIdTests`, `LeaderboardEndpointTests`, `RateLimitingTests`).

## Production code touched

**None.** All three backend test files are additive in fresh subdirectories (`Players/`, `Leaderboard/`, `RateLimiting/`); the selectors.md change is a pure-append two-section insert before the `## Stability contract` footer. Bishop's Wave 6 backend (`PlayerIdentityService`, `PlayerIdentityController`, `PlayerIdentityExtensions`, `LeaderboardController`, `LeaderboardService`, `ChangshaHub` cookie-resolution edits, `AutotableWsEndpoint` cookie-resolution edits, `Program.cs` DI + middleware additions) was already on origin at memo-write time (`21515fe`); Apone's Wave 6 middleware (`RateLimitingExtensions`, `Program.cs` `UseRateLimiter` + `RequireRateLimiting(ApiPolicy)` wiring) was also on origin (`408e0d1`). Hicks's frontend Wave 6 (`identity.ts`, `leaderboard.ts`, plus the `index.html` / `lobby.ts` / `main.css` consumer surfaces) was in the working tree but uncommitted at memo-write time.

## Wire contracts pinned (from Bishop's memo + code)

- **Cookie name:** `mahjong_pid`; value: 32-char lowercase hex (`Guid.NewGuid().ToString("N")`); flags: `HttpOnly; Secure(IsHttps); SameSite=Lax; Max-Age=31536000; Path=/; IsEssential`. Sourced from `PlayerIdentityService.cs:54-69, CookieName, CookieMaxAge`.
- **Identity envelope:** `{ playerId: string, displayName: string, avatarColor: "#RRGGBB", createdAt: ISO-8601, lastSeenAt: ISO-8601 }`. Sourced from `PlayerIdentityController.MintOrRefresh`.
- **PlayerId validation:** `[A-Za-z0-9_-]{1,128}` — pinned in `PlayerIdentityService.IsValidPlayerId` (used by autotable WS endpoint's cookie-trust check so a forged cookie can't inject SQL wildcards).
- **Hub item key:** `Context.Items["playerId"]` (constant `PlayerIdentityExtensions.PlayerIdItemKey`); resolution order on connect: `HubCallerContext.Items` → `HttpContext.Items` → cookie → `ConnectionId` fallback (when no cookie ever issued — pre-Wave-6 clients).
- **Leaderboard defaults:** `DefaultLimit=50, MaxLimit=100, DefaultMinGames=5`. Sort axes (case-insensitive, unknown → `gamesWon`): `gamesWon` (default) | `totalScore` | `winRate` | `longestStreak` | `highestScore`.
- **Leaderboard row shape:** 10 fields — `rank` (1-based, paging-shifted), `playerId`, `displayName`, `avatarColor`, `gamesPlayed`, `gamesWon`, `winRate` (double, 0..1 — `GamesPlayed > 0 ? (double)GamesWon / GamesPlayed : 0.0`), `totalScore` (long, signed), `highestSingleGameScore`, `longestWinStreak`.
- **Leaderboard envelope:** `{ total: int (paging-independent post-filter count), rows: LeaderboardRow[] }`.

## Apone middleware contract pinned

- **Policies registered:** `AnonymousPolicy = "fixed-window-anonymous"` (10/min/IP fixed window), `ApiPolicy = "token-bucket-api"` (30-token bucket, 5/sec replenishment, ~300 req/min/IP steady state with 30 burst).
- **Gate:** `RateLimiting:Enabled` config key. `false` in `appsettings.json` (Development + xUnit harness), `true` in `appsettings.Production.json`. Middleware only registers when gated `true` — `AddMahjongRateLimiting` returns `false` and `app.UseRateLimiter()` is skipped otherwise.
- **Endpoint surface:**
  - **ApiPolicy** applied via `app.MapControllers().RequireRateLimiting(ApiPolicy)` (covers `/api/identity` + `/api/leaderboard` + `/api/matchmaking/*` + any future controllers); also applied to `/api/system/persistence` + `/api/changsha/pattern-ordering` (minimal-API endpoints).
  - **Off-policy via `.DisableRateLimiting()`:** `/api/health`, `/health`, `/metrics`.
  - **Off-policy by transport nature (no `.RequireRateLimiting`):** `/hubs/changsha` (SignalR), `/autotable/ws` (raw WebSocket). The handshake would be the only thing the middleware sees on a long-lived transport; bypassing for the upgrade keeps semantics honest.
- **Rejection shape:** HTTP 429 + `Content-Type: application/json` + body `{"error":"too_many_requests"}` + `Retry-After: <int seconds>` header (sourced from `MetadataName.RetryAfter`).
- **Partition key:** `X-Forwarded-For` first segment when set, else `Connection.RemoteIpAddress.ToString()`, else literal `"unknown"`. This makes the policy correct behind nginx / Apache reverse proxies AND in tests (TestServer always reports loopback; `X-Forwarded-For: 10.x.x.x` gives each test its own partition).

## Methodology — what worked

- **WebApplicationFactory<Program> over the real Program — same pattern from Waves 3/4/5.** Per-test temp SQLite (`mahjong-leaderboard-<guid>.db`, `mahjong-ratelimit-<guid>.db`, `mahjong-identity-<guid>.db`) + `Configure<ChangshaRuntimeOptions>(PersistSnapshots=false)` + `IAsyncLifetime.DisposeAsync()` for cleanup. Zero new scaffolding.
- **Manual Cookie header forwarding instead of `CookieContainer`.** TestServer's host is `localhost`; RFC-6265-compliant containers may reject cookies whose `Domain` attribute is absent (Bishop's cookie has no `Domain` attribute because `Path=/` + same-origin is the production case). Manually reading `Set-Cookie` from the first response and attaching it as a `Cookie` header on the second is unambiguous + makes the assertions explicit (the test asserts a value, not a side-effect of a container's internal logic).
- **LongPolling transport for SignalR cookie tests.** TestServer does not support WS upgrade in this assembly version (the SignalR client tries `/hubs/changsha/negotiate` then falls back); explicit `Transports = HttpTransportType.LongPolling` short-circuits the brittle WS attempt. The `WebSocketFactory` throws to make the intent obvious if a future refactor accidentally re-enables WS.
- **`X-Forwarded-For` for rate-limit test isolation.** TestServer always reports the same loopback `RemoteIpAddress`; without `X-Forwarded-For: 10.x.x.x` per test, the three rate-limit tests would share a single partition and the second test would inherit the first's depleted bucket. Each test uses a stable XFF (`10.1.1.1`, `10.2.2.2`, `10.3.3.3`) so partitions are disjoint AND deterministic.
- **`Production` + `RateLimiting:Enabled=true` is the only on-combination.** Either knob alone is a no-op: `appsettings.Production.json` flips the flag, but a Development host never reads that file; `UseSetting` overrides the flag at any environment, but the limiter services are still keyed off `Enabled == true` in the extension. Both knobs together is the contract.
- **`JsonDocument` + `JsonValueKind` over typed deserialise for wire-shape assertions.** Typed deserialise would silently drop unknown / missing fields; explicit `GetProperty("...").ValueKind` checks catch a field rename or a `null` regression on the first assertion that touches the bad property.

## Surprises / blind spots flagged

- **Apone's `AnonymousPolicy` is registered but unattached.** The `fixed-window-anonymous` 10/min/IP policy was meant to protect the future low-volume mutating surfaces (Apone's docstring on `RateLimitingExtensions.AnonymousPolicy` explicitly calls out "the future POST /api/identity profile-create surface"). Bishop's `POST /api/identity` shipped through `MapControllers().RequireRateLimiting(ApiPolicy)` and inherits the looser 30-token bucket. **Not a defect** — both policies are in-scope; the bucket is generous but still protects against abuse — but if /api/identity becomes a known fraud / abuse target, Bishop or Apone needs to add `[EnableRateLimiting(RateLimitingExtensions.AnonymousPolicy)]` to `PlayerIdentityController` to force the tighter window. My `PostIdentity_RapidBurst_TriggersRateLimit` test pins the actual production behaviour (token-bucket 429 at 30+ rapid POSTs); changing the policy on the controller would require updating the burst threshold but not the test's intent.
- **HotSeatSwap_PlayerToPlayer_PreservesGameState is a pre-existing flake.** From Hicks's Wave 1 work; not in Wave 6 scope. Did not surface in the Wave-6 final gate (456/0/0), but in parallel runs it sporadically fails with what looks like a race condition in the swap → in-flight-discard → re-deal sequence. Worth opening a Hicks follow-up issue; not a blocker for Wave 6.
- **TestServer SignalR LongPolling cookie pass-through works, but the path is non-obvious.** The `opts.Headers.Add("Cookie", ...)` extension on `HttpConnectionOptions` is what plumbs the cookie all the way to the hub's `HttpContext.Request.Cookies`; without it, the cookie set on a parallel HTTP client doesn't reach the hub since SignalR builds its own `HttpMessageHandler` chain on top of the TestServer. Documented in `PersistentPlayerIdTests:HubConnection_ReadsPlayerIdFromCookie` so the pattern can be copied into Wave-7 hub tests.
- **`PlayerProfile.AvatarColor` default is `#808080`** (mid-grey). The frontend's preset palette doesn't include this colour — first-paint of a freshly minted profile shows a grey chip until the user picks a preset or the bootstrap mints one. Not a defect (Bishop's intent is the user MUST pick), but if Hicks's onboarding flow ever defaults to skip + no-pick, the grey chip is what they get. Worth deciding whether the bootstrap should auto-pick from the palette on first-mint.
- **Parallel-agent volatility (process, same as Wave 5).** Bishop's `Leaderboard/` + `Players/` directories disappeared and re-appeared 3-4 times during my Wave 6 work as he iterated on the controller signature and the cookie issuer's `Mint()` vs `ResolveOrMint(HttpContext)` split. Worked around with the same ~6-minute settle-then-edit cycles documented in my Wave 5 memo. The polling-loop log (`.git/poll-log.txt`) provides the wall-clock cadence; consider promoting it to a shared `.squad/state/upstream-cadence.log` so the next wave can tune its settle window without re-deriving the rhythm.

## Stability

- **Phase J Wave 6 filter (`--filter "Wave=Phase-J-6"`):** 11 passed / 0 failed / 0 skipped (4 + 4 + 3).
- **Full suite:** **456 passed / 0 failed / 0 skipped**. Zero-skips streak preserved (10 consecutive waves green).
- **No production code changed** (`src/backend/src/**` untouched on this commit).

## Cross-agent coordination

- **Bishop** landed `21515fe` (persistent player ids + leaderboard endpoint, 20 files, +886/-90) and `81beb15` (memo + history) on `stlong/phase-j-wave-6-completion` ahead of my test commit.
- **Apone** landed `408e0d1` (rate limiting + CORS + reverse-proxy / systemd / log-rotation guides) and `c3289eb` (Wave 6 journal memo) on the same branch ahead of my work.
- **Hicks** had `identity.ts` + `leaderboard.ts` + `index.html` + `lobby.ts` + `main.css` + `profile.ts` + the e2e specs (`lobby-flow.spec.ts`, `replay.spec.ts`, `sound-toggle.spec.ts`) in the working tree but uncommitted at memo-write time — same parallel-volatility footprint as Wave 5. My selectors.md additions cite the testids he actually shipped in the HTML / TS so the contract is forward-compatible whenever he commits.
- Strict-disjoint lanes preserved across all four agents (Bishop = identity + leaderboard backend, Apone = rate limiting + ops docs, Hicks = onboarding + leaderboard frontend, Vasquez = tests + selectors). Test files: `tests/Players/PersistentPlayerIdTests.cs`, `tests/Leaderboard/LeaderboardEndpointTests.cs`, `tests/RateLimiting/RateLimitingTests.cs`. Docs: `src/frontend/autotable-src/tests/selectors.md`.
