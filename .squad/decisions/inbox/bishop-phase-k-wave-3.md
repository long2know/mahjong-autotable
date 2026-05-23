# Bishop — Phase K Wave 3

**Branch:** `stlong/phase-k-wave-3-bringup`
**Scope:** backend — production bring-up wave 3. Seven cross-lane
dependencies surfaced by Wave 2 (PR #48) needed to close before the
Apone / Hicks / Vasquez Wave-3 surfaces could go green: per-table
`VoiceEnabled` toggle, VoiceHub per-table auth, TURN HMAC mint
endpoint, Microsoft OAuth provider, onboarding-status persistence,
tournament seed admin endpoint, plus the five Vasquez contract-gap
fixes on `PlayerSeasonRolloverDeferral`
(`FromSeason→FromSeasonId`, `ToSeason→ToSeasonId`,
`DrainedAtUtc→ResolvedAtUtc`) and the pre-existing Wave-2 schema
drift on `ReconnectAuditEntries.Detail`.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo
--no-build -- xUnit.MaxParallelThreads=2` → **Passed: 1149, Failed: 0,
Skipped: 0**. Baseline at start of Wave 3 was 1062/0/0 (Wave 2
closeout); +87 net = Vasquez's 8 pre-staged contract-test files under
`tests/Phase_K_W3/` (~73 reflection-defensive facts) plus the
cross-wave regression facts in `Wave1ThroughKW3RegressionTests.cs`
(which replaces the deleted W2 regression file). Every Wave-3
deliverable binds to an existing contract test in Vasquez's
pre-staged suite.

> **Concurrency note.** Default xUnit parallelism flaked once on the
> regression test's `InitializeAsync` (WebApplicationFactory startup
> contention against a shared SQLite tempfile). Reducing
> `MaxParallelThreads` to 2 stabilises the run; the test passes
> isolated. Captured for the harness lane.

---

## Task 1 — Per-table `VoiceEnabled` toggle

### Problem

Wave-2 VoiceHub was wide-open: any anonymous client could broadcast
SDP into any table id. Vasquez's contract probe asserts the hub
rejects clients when the game's `VoiceEnabled` column is false (or
the row is missing). The brief also wants the host to flip the toggle
at runtime via `POST /api/games/{id}/settings/voice`.

### Approach

- `ChangshaGame.VoiceEnabled` (bool, default `false`) and
  `ChangshaGame.OwnerPlayerId` (string?, max 128) added to the
  entity. `OwnerPlayerId` is mirrored from `ChangshaGameState.CreatorPlayerId`
  inside `ChangshaGameRuntime.PersistSnapshotAsync` on every create + update.
- `POST /api/games/{id:guid}/settings/voice` accepts
  `VoiceSettingsBody { Enabled: bool }`; gates: 401 when no
  `mahjong_pid` cookie, 403 unless caller is the owner OR has
  `Role == "admin"`, 404 when the game row is missing. Persists the
  column and returns `{ id, voiceEnabled }`.
- Migration adds both columns + a backwards-compat path in
  `DatabaseBootstrapper.EnsureSqlitePhaseK3TablesAsync` so existing
  Wave-2 SQLite dbs upgrade without a restore.

---

## Task 2 — VoiceHub per-table auth

### Problem

`VoiceHub.JoinVoice(tableId)` previously trusted any connection. The
contract probe requires a three-step gate before signalling can flow:
the caller must (1) carry a `mahjong_pid` cookie, (2) the table's
`VoiceEnabled` flag must be true, (3) the caller must be seated at
the table OR be its owner.

### Approach

`VoiceHub` was rewritten around `IPlayerIdentityService.ResolveFromCookie(HttpContext)`,
a scoped `AppDbContext`, and `IChangshaGameRuntime.TryGetSnapshot`.
Failures raise canonical `HubException` codes:

- `voice-join-unauthorized` — no cookie / anonymous caller.
- `voice-disabled-for-table` — flag false or row missing.
- `voice-not-seated` — caller isn't owner and not in `state.Seats[]`.

Non-GUID `tableId` strings (legacy lobby tags) soft-pass the gate
so existing telemetry harnesses keep working. All three relay paths
(`SendOffer`, `SendAnswer`, `SendIceCandidate`) now record into
`VoiceHubMetricsService` for the 60-second rolling counter; audit
rows prefer the resolved persistent `PlayerId` over `Context.ConnectionId`.

### Surprise

The brief said "Seat" was a first-class entity. There isn't one in
this codebase — seats live inside `ChangshaGameState.Seats[]` serialised
into `ChangshaGame.StateJson`. Decision: walk the in-memory runtime
snapshot rather than reach into JSON. This is what the contract probe
expects.

---

## Task 3 — `VoiceHubMetricsService`

Singleton (`Voice/VoiceHubMetricsService.cs`) with a per-connection
`ConcurrentDictionary<string, Queue<DateTime>>`. `RecordRelay(connId)`
trims entries older than 60s before appending. `GetRelayCountInWindow(connId)`
returns the queue length. Used by Wave-3 frontend "voice activity"
indicator; the contract probe asserts the type exists with both
methods.

---

## Task 4 — TURN HMAC mint endpoint

### Problem

Wave 2's `/api/turn` returned static credentials embedded in
`appsettings.json` — anyone could lift them. The brief wants the
canonical ephemeral-credential pattern: server signs a `username`
that bakes in an expiry and the caller's player id; the TURN server
verifies via shared HMAC secret.

### Approach

- `VoiceOptions.TurnSharedSecret` (string?) and
  `VoiceOptions.TurnCredentialTtlSeconds` (default 3600) added.
- `POST /api/turn/credentials` (auth-gated via
  `AuthCookieService.ResolveAsync`) mints:
  - `username = "{unix_ttl}:{playerId}"` where
    `unix_ttl = now + TurnCredentialTtlSeconds`.
  - `credential = Base64(HMACSHA1(TurnSharedSecret, username))`.
  - Response: `{ username, credential, ttl, expiresAt, urls, iceServers }`.
- Returns 503 when `TurnSharedSecret` is unset (defends against a
  silent zero-key signing in dev). Returns 401 when no session.
- **Anon `/api/turn` behaviour change.** The legacy unauthenticated
  endpoint now strips `username`/`credential` from its response shape
  — STUN-only. The static-credential fallback is gone. Operators who
  still need static TURN must configure the new mint path.

---

## Task 5 — Microsoft OAuth provider

### Problem

Brief asks for a third provider alongside Google and the magic-link
flow, hitting Entra ID's v2.0 OIDC endpoints with multi-tenant
support (default tenant = `common`).

### Approach

- `AuthOptions.Microsoft` (new `OAuthProviderOptions`) + new
  `TenantId` property on the shared `OAuthProviderOptions` (default
  `"common"`). Keeps the switch in `OAuthService` provider-agnostic.
- `OAuthService.ResolveProviderEndpoints` adds a `microsoft` arm
  that substitutes `{tenant}` in
  `https://login.microsoftonline.com/{tenant}/v2.0/...` URLs.
  `ParseUserInfo` prefers `oid` (Entra immutable id) → `sub` (OIDC) →
  `id` (Graph); email precedence `email` → `mail` →
  `userPrincipalName`; display name `name` → `displayName`. Treats
  the returned email as **unverified** pending magic-link.
- `OAuthDiscoveryService.FetchMicrosoftAsync` mirrors
  `FetchGoogleAsync` against
  `https://login.microsoftonline.com/{tenant}/v2.0/.well-known/openid-configuration`.
  The internal payload class was renamed `GoogleDiscoveryPayload` →
  `OidcDiscoveryPayload` and is now reused by both providers.
- `OAuthProviderHealthCheck.ProbeAllAsync` includes a Microsoft probe
  honouring the configured tenant.
- `AuthController.ListProviders` + `NormaliseProvider` accept
  `microsoft` as the third arm. Nonce binding extends to Microsoft
  for id_token validation.

### Discovery refresh-seconds knob

`OAuthDiscoveryOptions.RefreshIntervalSeconds` added with precedence
over the existing `RefreshIntervalHours` when `> 0`. The
`OAuthDiscoveryRefreshService` background loop honours seconds first,
then falls back to hours. Lets ops shorten the cache during incident
response without flipping a hours-grained knob.

---

## Task 6 — `PlayerOnboardingStatus` endpoints

### Problem

Hicks's onboarding tour needs server-side persistence of the user's
step counter + a `completed` flag so the tour resumes correctly on a
fresh device. Brief wants a one-way `completed` flip and a
monotonic step counter (server clamps `step` to
`max(current, requested)` so two parallel POSTs can't regress the
state).

### Approach

- `PlayerOnboardingStatus` entity (PK = `PlayerId`, `Step`,
  `Completed`, `UpdatedAtUtc`). Anon-cookie-scoped (no
  authentication needed; ties to `mahjong_pid`).
- `GET /api/players/me/onboarding-status` → 200 with the row,
  initialising a default `{ step: 0, completed: false }` shape when
  the row is absent.
- `POST /api/players/me/onboarding-status` accepts
  `{ step?: int, completed?: bool }`. Step is clamped monotonic.
  `Completed` is one-way `false→true`; once true it stays true. Both
  fields optional — partial update keeps the unmodified field.

---

## Task 7 — Tournament seed admin endpoint

### Problem

Vasquez's tournament workflow needs the bracket to be seeded post-
registration but pre-start. The contract probe requires a `POST
/api/tournaments/{id}/seed` admin endpoint that accepts
`{ assignments: [{ playerId, seed }] }` and applies them atomically.

### Approach

- `TournamentService.SeedAsync(tournamentId, IReadOnlyList<TournamentSeedAssignment>, ct)`
  — gated to `Status` in (draft, open). Unknown player ids are
  silently skipped (matches the contract probe's "partial accept"
  expectation).
- `TournamentController.Seed` — admin-only (`session.Role ==
  "admin"`), 401 when no session, 403 when not admin, 409 when the
  tournament is past `open` status. Body shape:
  `SeedBody { Assignments: List<SeedEntry { PlayerId, Seed }> }`.

---

## Vasquez contract-gap closures

Five rename fixes that pin Wave-2 deferral surfaces to the names the
W3 contract probes expect:

| Old name        | New name           |
| --------------- | ------------------ |
| `FromSeason`    | `FromSeasonId`     |
| `ToSeason`      | `ToSeasonId`       |
| `DrainedAtUtc`  | `ResolvedAtUtc`    |

Touched: `ChangshaEntities.PlayerSeasonRolloverDeferral`,
`AppDbContext` indices, all `SeasonRolloverService` references,
SQLite bootstrap `ALTER TABLE ... RENAME COLUMN` path, and the EF
migrations for all three providers.

### Pre-existing Wave-2 drift: `ReconnectAuditEntries.Detail`

`AppDbContext` referenced a `Detail` column that no Wave-2 migration
ever added; only the model snapshot knew. The Wave-3 migration adds
the missing `AddColumn<string> Detail` for all three providers and
the SQLite bootstrap covers it via `PRAGMA table_info` probe so
existing dbs catch up. This is correct cleanup, not collateral
noise.

---

## Migrations × 3 providers

`Phase_K_W3_VoiceAndOnboardingSchema` landed under each
`Persistence/Migrations/{Sqlite,Postgres,SqlServer}/` sub-tree.
Each:

1. Renames the three deferral columns + rebuilds the affected
   indices.
2. Adds `OwnerPlayerId` (string?, 128) and `VoiceEnabled` (bool,
   default `false`) to `ChangshaGames`.
3. Adds `Detail` (string?) to `ReconnectAuditEntries` (the Wave-2
   drift fix).
4. Creates `PlayerOnboardingStatuses` (PK = `PlayerId`).
5. Updates the model snapshot.

Timestamps: Sqlite `20260523112245`, Postgres `20260523112259`,
SqlServer `20260523112308`.

---

## SQLite bootstrap fallback

`DatabaseBootstrapper.EnsureSqlitePhaseK3TablesAsync` invoked after
`EnsureSqlitePhaseK1TablesAsync`. Idempotent: probes
`PRAGMA table_info` before each `ALTER TABLE`. Covers the same
shape changes the migration does, so air-gapped Wave-2 dbs upgrade
without an EF run. SQLite ≥ 3.25 supports `ALTER TABLE ... RENAME
COLUMN`; EF Core 10 pins ≥ 3.35 so the rename arm is safe.

---

## appsettings.json — untouched

Wave 3 binds without explicit keys (Microsoft provider defaults
`Enabled = false`; `TurnSharedSecret` null → 503 from
`/api/turn/credentials`). For production ops, add:

```jsonc
"Voice": {
  "TurnSharedSecret": "<base64>",
  "TurnCredentialTtlSeconds": 3600
},
"Authentication": {
  "Discovery": { "RefreshIntervalSeconds": 0 },
  "Microsoft": {
    "Enabled": true,
    "ClientId": "...",
    "ClientSecret": "...",
    "TenantId": "common"
  }
}
```

---

## Surprises & hand-offs

- **The 1 regression flake.** Default xUnit parallelism collides
  on `Wave1ThroughKW3RegressionTests.InitializeAsync` (shared SQLite
  tempfile / WebApplicationFactory port race). `MaxParallelThreads=2`
  stabilises. Hand-off to Hudson if they want the harness lane to
  isolate per-class.
- **Static TURN credentials no longer leak from `/api/turn`.** Any
  consumer who relied on the Wave-2 anonymous credential response
  must move to the new HMAC mint or switch to STUN-only. Captured
  for the frontend in the test catalogue.
- **`OwnerPlayerId` is best-effort populated.** Pre-existing rows
  carry `null`; the column fills in on next persist. Tournament
  rollover code should treat `null` as "no host bypass" — the
  VoiceHub gate already does.
- **`PlayerOnboardingStatuses` is anon-cookie scoped, not account
  scoped.** A user who clears cookies starts the tour over. Acceptable
  for a tour, but flag for Hicks if account-linked persistence is
  ever wanted.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo --no-build -- xUnit.MaxParallelThreads=2` →
**Passed: 1149, Failed: 0, Skipped: 0** (+87 over Wave-2 closeout
baseline of 1062; Vasquez's eight Wave-3 contract-test files under
`tests/Phase_K_W3/` plus the new cross-wave regression facts in
`Wave1ThroughKW3RegressionTests.cs` all green).
