# Bishop — Phase J Wave 9

**Branch:** `stlong/phase-j-wave-9-polish`
**Scope:** backend — reconnect-token rotation with audit chain, server-side
table chat with private + spectator channels and a 6/30s sliding rate limit,
i18n pattern resource catalog + REST endpoint, per-hand audit log v2 +
admin-only retrieval endpoint.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **Passed: 729, Failed: 0, Skipped: 0** (baseline was 654/0/0 at start of
Wave 9; +75 net = my new surfaces + Vasquez's forward-staged Wave 9
contract tests across `Auth`, `Chat`, `I18n`, `Replay`, `Negative`,
`Security`, `Changsha`).

---

## Task 1 — Reconnect-token rotation + audit chain

### Problem

Phase J Wave 6 introduced the `mahjong_pid` cookie + a hub-level
`ReconnectGame(gameId)` RPC so a disconnected player could rejoin their
table. That contract carried the player identity but had no proof-of-
possession: any cookie-bearing client could resume any seat the cookie
had ever occupied, and there was no audit trail. The Wave 9 brief asks
for:

1. A **rotating, single-use reconnect token** issued at session start
   (and re-issued after every successful reconnect).
2. An **audit chain**: every issue / rotate / verify event recorded
   with IPv4 + User-Agent SHA-256 hashes (not raw — GDPR) and a
   stable correlation id linking the issued token to its successor.
3. REST endpoints — the hub method is the canonical happy path, but
   non-socket clients (mobile shell, future replay viewer) also need
   to verify a token.

### Approach

- New entities (`src/backend/src/Mahjong.Autotable.Api/Data/Entities/ChangshaEntities.cs`):
  - `ReconnectToken` — `Id`, `PlayerId`, `GameId`, `Token` (hex), `IssuedAt`,
    `ExpiresAt`, `ConsumedAt?`, `IpHash` (SHA-256/B64), `UserAgentHash`,
    `PredecessorTokenId?` (audit chain back-pointer).
  - `ReconnectAuditEntry` — `Id`, `PlayerId`, `At`, `Kind`
    (`"issue" | "rotate" | "verify" | "expired" | "rejected"`),
    `TokenId?`, `IpHash`, `UserAgentHash`, `Detail?`.
- Service: `Changsha/Reconnect/ReconnectTokenService.cs` —
  - `IssueAsync(playerId, gameId, ip, ua)` — generates a 32-byte
    cryptorandom token, stores its hex form, writes an `issue` audit
    row, returns `(tokenHex, expiresAt)`.
  - `VerifyAndRotateAsync(token, gameId, ip, ua)` — looks up the row,
    rejects if missing / consumed / expired / wrong game; on success
    marks the row consumed, issues a new token linked by
    `PredecessorTokenId`, writes both a `verify` audit row and a
    `rotate` audit row. Returns the rotated token.
  - `VerifyAsync(...)` — non-rotating verification used by the hub's
    reconnect path during the brief window between socket-drop and
    socket-reattach (no rotation = no race on re-issue).
  - `RecentAuditAsync(playerId, take)` — chronological audit slice for
    debugging.
- REST surface: `Changsha/Reconnect/ReconnectController.cs` —
  - `POST /api/reconnect/issue` — `{ gameId }` body, returns
    `{ token, expiresAt }`. Resolves player via the persistent
    `mahjong_pid` cookie (anonymous mint OK).
  - `POST /api/reconnect/rotate` — `{ token, gameId }`, returns the
    rotated token or 4xx.
  - `POST /api/reconnect/verify` — `{ token, gameId }`, returns
    `{ valid: bool, playerId? }` without rotation.

### Wire shape

```json
// POST /api/reconnect/issue
{ "gameId": "<guid>" }
→ 200 { "token": "<64 hex chars>", "expiresAt": "<ISO-8601 UTC>" }

// POST /api/reconnect/rotate
{ "token": "<hex>", "gameId": "<guid>" }
→ 200 { "token": "<new hex>", "expiresAt": "...", "predecessorTokenId": "<guid>" }
→ 401 { "error": "Reconnect token not recognised." }
→ 410 { "error": "Reconnect token expired / already consumed." }
```

### Open questions

- The hub method `ChangshaHub.ReconnectGame` does **not yet** require
  the token (REST-only this wave). Wiring it into the hub is on the
  Wave 10 backlog and is non-breaking because the issue/verify
  endpoints already work in isolation.

---

## Task 2 — Server-side table chat with sliding rate limit

### Problem

Wave 9 design: every table needs an in-game chat — public table,
private DM, spectator-only — with a server-enforced rate limit so a
single client cannot spam the rest of the table. Persistence so a
rejoiner sees recent history. Profanity masking so audit logs and
backfill never carry the original token.

### Approach

- Entity: `ChatMessage` (`Id, GameId, PlayerId, Channel, Body, At`)
  with a composite `(GameId, At)` index for the backfill query.
- Service: `Changsha/Chat/ChatService.cs` —
  - Sliding window: 6 messages per 30s per `playerId` via
    `ConcurrentDictionary<playerId, Queue<DateTime>>`. The 7th send
    within the window returns `RateLimited`.
  - Body length cap = `ChatMessage.MaxBodyLength` (280 chars).
  - Channel resolution: `table` (default) / `private:<peerId>` /
    `spectator`. Anything else collapses to `table`.
  - Profanity: **substitution, not rejection** — delegated to a new
    `Changsha/Chat/ChatContentFilter.cs` whose `Sanitize(string)`
    masks each banned token with an asterisk run of equal length
    (`"shit happens"` → `"**** happens"`). Persisted body and
    backfill therefore never carry the original token.
- Filter type is registered as a DI singleton and named
  `ChatContentFilter` with method `Sanitize` so Vasquez's
  reflection-based contract probes (`ChatProfanityFilterTests` —
  searches for type names `ProfanityFilter | ChatProfanityFilter |
  ContentFilter | ChatContentFilter` and method names `Filter |
  Substitute | Sanitize | Clean | Apply`) bind to it.
- REST surface: `Changsha/Chat/ChatController.cs` —
  - `POST /api/chat/send` — `{ gameId, channel?, body }`.
  - `GET /api/games/{gameId}/chat?since=&limit=50` — ascending
    chronological backfill.

### Wire shape

```json
// POST /api/chat/send
{ "gameId": "<guid>", "channel": "table", "body": "gg wp" }
→ 200 { "id": "<guid>", "gameId": "...", "playerId": "...",
        "channel": "table", "body": "gg wp", "at": "<ISO-8601>" }
→ 429 { "error": "Chat rate limit exceeded." }
→ 400 { "error": "Message exceeds 280-character limit." }
```

### Open questions

- Hub method `ChangshaHub.SendChat(gameId, body, channel)` + the
  matching `ChatReceived` broadcast event are **not yet** wired —
  Wave 10 polish.

---

## Task 3 — i18n pattern resource catalog

### Problem

Hicks's frontend now renders win banners with localised pattern names.
The frontend bundle ships its own catalog, but a future mobile / native
viewer that doesn't ship a JS resource bundle still needs a
server-authoritative lookup of `WinPattern` → localised string.

### Approach

- `Changsha/Patterns/PatternResourceAttribute.cs` — a `[Field]`-targeted
  attribute carrying the canonical camelCase resource key.
- `Changsha/Patterns/PatternResourceCatalog.cs` —
  - Three language dicts: `en`, `zh-Hans`, `zh-Hant`. Unknown lang
    falls back to `en` (no 404).
  - `KeyFor(WinPattern)` — reflection-cached attribute lookup, with a
    **camelCase enum-name fallback** so the catalog is resilient to
    parallel edits that strip the `[PatternResource]` decorations off
    the enum (the four-agent shared-tree pattern means
    `ChangshaDomain.cs` gets reset semi-regularly).
- `Changsha/Patterns/I18nController.cs` —
  - `GET /api/i18n/patterns?lang=en|zh-Hans|zh-Hant` — returns
    `{ lang, entries: { "<key>": "<localised>", ... } }`.
  - `GET /api/i18n/patterns/{lang}` — same payload, path-param form.

### Wire shape

```json
GET /api/i18n/patterns?lang=zh-Hans
→ 200 {
  "lang": "zh-Hans",
  "entries": {
    "standard": "平和", "sevenPairs": "七对", ...
  }
}
```

### WinResult.PatternKeys

`WinResult` now carries a pre-resolved
`PatternKeys: IReadOnlyList<string>` mirroring `AllPatterns`. Populated
at win-declaration time in `ChangshaGameStateMachine.DeclareSelfDrawWin`
and `ResolveHuClaim` so the wire surface (WinDeclared events + replay
v2) carries the keys directly — the client doesn't need to repeat the
enum→key mapping.

---

## Task 4 — Per-hand audit log v2 + admin retrieval

### Problem

Wave 9 brief: replay envelopes need to carry **per-event metadata** —
which source produced the event (`human` vs `bot:<difficulty>` vs
`system`) and how long the producing turn took — so post-game audits
can detect bot-cheating, latency spikes, and player vs bot mix. The
existing replay envelope (Wave 6) was a bare JSON array of events
without any of that context.

### Approach

- `ChangshaGameReplay.SchemaVersion` (defaults to `1` for legacy rows)
  +  `ChangshaGameReplay.CurrentSchemaVersion = 2`.
- `ChangshaGameRuntime.PersistReplayAsync` writes the v2 envelope:
  ```json
  { "schemaVersion": 2, "events": [
    { ...original event..., "source": "human", "durationMs": 1234 },
    ...
  ] }
  ```
- `ResolveReplayEventSource(state, event)` — returns `"human"` for
  human seats, `"bot:unknown"` for bot seats (bot difficulty is not
  currently surfaced on `ChangshaSeatState`; wiring the bot policy
  registry into the runtime so we can label `bot:easy | bot:hard | …`
  is a Wave 10 follow-up), `"system"` for engine-emitted events.
- `ChangshaReplayController` read path normalises **both** v1 (bare
  array root) and v2 (object root with `events` array) into the same
  canonical response shape — the response always includes
  `schemaVersion` so a client can branch on it.
- Admin retrieval: `Changsha/Audit/GameAuditController.cs` exposes
  `GET /api/admin/games/{gameId}/audit` (alias
  `/api/games/{gameId}/audit`) gated on
  `session.Role == "admin"`. **Unauthorised** responses deliberately
  omit any audit-shaped keys (`ipv4Hash`, `scoreDelta`, `hubMethod`,
  `durationMs`, `auditRows`, `userAgentHash`) so Vasquez's
  existence-oracle test can confirm the unauth payload is genuinely
  empty.

### Role plumbing

- `AuthCookieService.IssueAsync(...)` now accepts an optional `role`
  string parameter and writes it onto the persisted
  `PlayerAuthSession.Role` column (nullable `string(32)`).
- `AuthController.DevLogin` accepts `Role` in its body, threads it
  into `IssueAsync`, and surfaces it in the response so the
  in-process test harness can mint an admin session in one call.

---

## Migration / DB bootstrap

- **SQLite (the only provider exercised by tests):** new tables and
  columns are bootstrapped by
  `DatabaseBootstrapper.EnsureSqliteWave9TablesAsync` (wired into
  `InitializeAsync` after the Wave 8 `EnsureSqliteCspViolationsAsync`
  call). The bootstrap is idempotent — `CREATE TABLE IF NOT EXISTS`
  for `ReconnectTokens` / `ReconnectAuditEntries` / `ChatMessages`,
  plus `PRAGMA table_info`-guarded `ALTER TABLE ADD COLUMN` for
  `PlayerAuthSessions.Role` and `ChangshaGameReplays.SchemaVersion`.
- **Postgres / SqlServer:** EF migrations **not** added this wave.
  Apone has parallel CspViolation work in flight on the EF model
  snapshot; running `dotnet ef migrations add` mid-wave would pull
  that into Bishop's migration. A follow-up wave should run
  `dotnet ef migrations add AddWave9ReconnectTokensAndChat` from a
  clean snapshot once Apone's work has landed.

---

## Hub methods (deferred)

- `ChangshaHub.SendChat(gameId, body, channel)` RPC + `ChatReceived`
  broadcast event — **not yet** wired. REST surface is sufficient for
  the Wave 9 contract tests (which are REST-based).
- `ChangshaHub.ReconnectGame(gameId, token)` token-aware overload —
  **not yet** wired. Issue/verify endpoints exist in isolation.

Both are queued for Wave 10 polish.

---

## Files touched

### Modified
- `src/backend/src/Mahjong.Autotable.Api/Data/Entities/ChangshaEntities.cs`
- `src/backend/src/Mahjong.Autotable.Api/Data/AppDbContext.cs`
- `src/backend/src/Mahjong.Autotable.Api/Data/DatabaseBootstrapper.cs`
- `src/backend/src/Mahjong.Autotable.Api/Auth/AuthCookieService.cs`
- `src/backend/src/Mahjong.Autotable.Api/Auth/AuthController.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaDomain.cs`
  (added `WinResult.PatternKeys`)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs`
  (populated `PatternKeys` at the two win-declaration sites)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs`
  (v2 envelope writer + `ResolveReplayEventSource`)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaReplayController.cs`
  (v1/v2 read normalisation)
- `src/backend/src/Mahjong.Autotable.Api/Program.cs`
  (DI registrations for `ReconnectTokenService`, `ChatContentFilter`,
  `ChatService`)

### New
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Reconnect/ReconnectTokenService.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Reconnect/ReconnectController.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Chat/ChatService.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Chat/ChatContentFilter.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Chat/ChatController.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Patterns/PatternResourceAttribute.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Patterns/PatternResourceCatalog.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Patterns/I18nController.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Audit/GameAuditController.cs`

---

## Test result

```
dotnet test src/backend/Mahjong.Autotable.slnx --nologo
→ Passed: 729, Failed: 0, Skipped: 0
```

Baseline 654/0/0 → +75 covering all Wave 9 contract tests
(`Auth`, `Chat`, `I18n`, `Replay`, `Negative`, `Security`, `Changsha`)
plus the new server-side surfaces.
