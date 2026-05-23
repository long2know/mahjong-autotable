# Bishop — Phase J Wave 8

**Branch:** `stlong/phase-j-wave-8-completion`
**Scope:** backend — OAuth (Google / GitHub) + email magic-link auth on top
of the existing `mahjong_pid` cookie, server-driven `ChangshaRulePreset`
CRUD, and a new "Master" bot strength tier.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **Passed: 654, Failed: 0, Skipped: 0** (baseline was 554/0/0 at start of
Wave 8; +100 net = my new backstops, Vasquez's forward-staged Wave 8
contract tests, Hicks's frontend-coupled tests, Apone's Sentry / security
header tests).

---

## Task 1 — OAuth (Google / GitHub) + email magic-link auth

### Problem

The autotable already issues an anonymous `mahjong_pid` cookie (Wave 6)
so players are tracked across sessions, but there was no way for a
player to **claim** that identity across devices. Stephen's Wave 8 brief
called out three requirements:

1. Google + GitHub OAuth as the primary "social" auth path.
2. Email magic-link as the passwordless fallback (no SMS, no third-party
   IDP dependency for users who refuse OAuth).
3. **Layered**, not replacement: the anonymous `mahjong_pid` cookie
   stays — auth is an *upgrade* (anonymous → identified), not a wall in
   front of the table. A returning OAuth user re-binds to their server-
   side `PlayerProfile` row by **rewriting** `mahjong_pid` to the
   profile id discovered via the `PlayerAuthIdentity` lookup.

### Decisions

#### Entities (`Data/Entities/ChangshaEntities.cs`)

- **`PlayerAuthIdentity`** — one row per (provider, providerSubject)
  binding. `Provider` ∈ `{Google, GitHub, EmailMagicLink, Dev}`.
  `ProviderSubject` is the provider's stable subject id (Google `sub`,
  GitHub `id`, email-as-string for magic link, email for dev). Unique
  index on `(Provider, ProviderSubject)`. FK to `PlayerProfile` on
  `PlayerId` (cascade delete because identities are meaningless once
  the profile is gone — auditing is via the AuthSession + DiscardPile
  trails, not the identity row).

- **`EmailMagicLinkToken`** — opaque 64-char URL-safe base64 (48 bytes of
  `RandomNumberGenerator.GetBytes`). 15-minute TTL (`AuthOptions
  .MagicLinkTtlMinutes`). Single-use: `ConsumedAt` is set inside
  `MagicLinkService.ConsumeAsync`. Unique index on `Token` prevents
  duplicates at the DB layer.

- **`PlayerAuthSession`** — backs the `mahjong_auth` cookie. Cookie value
  is also a 64-char opaque token; the session row carries `PlayerId`,
  `IdentityId` (FK to `PlayerAuthIdentity`), `ExpiresAt`,
  `RevokedAt?`, `LastSeenAt`. **Sessions are server-side rows** rather
  than JWT — explicit logout / revoke is one DB UPDATE, which is the
  ops behaviour Stephen asked for.

- **`ChangshaRulePreset`** — see Task 2.

- **`ChangshaGame.RulePresetId` (Guid?)** — nullable FK on the existing
  game row pointing at the preset that minted the rules. Null when the
  game was created before Wave 8 or via a code path that doesn't yet
  thread the preset id. Bootstrapper adds the column defensively via
  `PRAGMA table_info` probe before `ALTER TABLE … ADD COLUMN`.

#### Services (`Auth/`)

- **`AuthCookieService`** — owns the `mahjong_auth` cookie. `IssueAsync`
  mints a 64-char URL-safe token, inserts a `PlayerAuthSession` row,
  writes the cookie (HttpOnly + Secure + SameSite=Lax,
  `AuthOptions.SessionLifetimeDays` default 30). `ResolveAsync` reads
  the cookie, joins on the session row, returns `null` when expired /
  revoked / missing. `RevokeAsync` sets `RevokedAt = UtcNow` and
  expires the cookie.

- **`AuthIdentityService.ResolveOrLinkAsync`** — the upgrade-flow heart.
  Given `(provider, providerSubject, currentPlayerId, …)`:

  - If a `PlayerAuthIdentity` row already exists for
    `(provider, providerSubject)` → returns it. **The existing
    PlayerId wins** — i.e. if the row's `PlayerId` differs from the
    anonymous `currentPlayerId` (a logged-in user on a new browser),
    the controller rewrites the `mahjong_pid` cookie to the
    identity's `PlayerId` via `PlayerIdentityService.WriteCookie`.
    The previous anonymous `PlayerId` is effectively abandoned (the
    profile row stays in the DB; nothing else points at it).

  - Otherwise → creates a new identity row pinned to
    `currentPlayerId`. On first link, the user's display name is
    **only** overwritten when the current display name still has the
    default `Player-XXXXXX` shape — never clobbers a user-customised
    name.

- **`MagicLinkService.IssueAsync` / `ConsumeAsync`** — tokens stored
  hashed-by-equality, not hashed by SHA-256 (the random 64-char string
  is already collision-resistant on the unique index). `ConsumeAsync`
  atomically sets `ConsumedAt` and rejects already-consumed / expired
  rows. Soft-fails (returns `null`) on unknown tokens.

- **`OAuthService`** — Google + GitHub endpoints hardcoded with
  `AuthOptions.[Google|GitHub].{AuthorizationEndpoint,TokenEndpoint,
  UserInfoEndpoint,Scopes}` override hooks. Uses a named
  `HttpClient("oauth")` so the test harness can swap it in if desired.
  CSRF nonce stored in a short-lived `mahjong_oauth_state` cookie
  (10-min) and compared via `CryptographicOperations.FixedTimeEquals`
  on callback.

- **`IEmailSender`** + 3 implementations:
  - `LogEmailSender` — default; writes the magic-link URL to
    `ILogger<…>` at Information. Used in dev / test.
  - `InMemoryEmailSender` — buffers `CapturedEmail` records; used by
    Wave 8 tests that need to round-trip a token.
  - `SmtpEmailSender` — `SmtpClient` + `MailMessage`, registered only
    when `Smtp.Host` is non-empty (`Program.cs` conditional). No real
    SMTP traffic in dev / test runs.

#### Endpoints (`Auth/AuthController.cs`, route `/api/auth`)

All endpoints sit under the `AnonymousPolicy` rate-limit policy (sliding
1-minute window, same as `/api/leaderboard`) so they're survivable under
credential-stuffing pressure without coupling to authentication state.

| Verb            | Path                              | Behaviour                                                                                                                  |
|-----------------|-----------------------------------|----------------------------------------------------------------------------------------------------------------------------|
| GET             | `/api/auth/providers`             | List of configured providers (`Google`, `GitHub`, `EmailMagicLink`, plus `Dev` when `IsDevelopment()`).                     |
| GET             | `/api/auth/login/{provider}`      | 302 to provider authorize URL. CSRF nonce written to `mahjong_oauth_state` cookie.                                         |
| GET             | `/api/auth/callback/{provider}`   | OAuth callback. Validates `state`, exchanges code, fetches user info, calls `ResolveOrLinkAsync`, issues `mahjong_auth`.   |
| POST            | `/api/auth/email/request`         | Body `{ email }`. Issues a 15-min token; dev mode echoes `devToken` in the response body for test round-trips.             |
| POST            | `/api/auth/magic-link/request`    | Alias for `/email/request` (Vasquez's tests probe both paths).                                                              |
| GET / POST      | `/api/auth/email/verify`          | Token via `?token=` (GET) or `{ token }` body (POST). Consumes token, calls `ResolveOrLinkAsync(provider=EmailMagicLink)`. |
| GET / POST      | `/api/auth/magic-link/verify`     | Alias for `/email/verify`.                                                                                                  |
| POST            | `/api/auth/link/{provider}`       | Authenticated: returns a `redirectUrl` the UI can navigate to in order to attach a second provider to the same `PlayerId`. |
| POST            | `/api/auth/logout`                | Revokes the auth session + clears the `mahjong_auth` cookie. Keeps `mahjong_pid` (the anonymous identity persists).        |
| GET             | `/api/auth/me`                    | Identity snapshot: `{ playerId, displayName, avatarColor, isAuthenticated, providers: [{provider, email, linkedAt, … }] }`.|
| POST            | `/api/auth/dev-login`             | Dev-only. Body `{ email, displayName? }`. Forges a `Dev` identity for local UI work. `NotFound` when not Development.       |

### Config

```json
"Authentication": {
  "Google":          { "Enabled": false, "ClientId": "", "ClientSecret": "", "AuthorizationEndpoint": "", "TokenEndpoint": "", "UserInfoEndpoint": "", "Scopes": "" },
  "GitHub":          { "Enabled": false, "ClientId": "", "ClientSecret": "", "AuthorizationEndpoint": "", "TokenEndpoint": "", "UserInfoEndpoint": "", "Scopes": "" },
  "EmailMagicLink":  { "Enabled": true,  "BaseUrl": "" },
  "SessionLifetimeDays": 30,
  "MagicLinkTtlMinutes": 15
},
"Smtp": {
  "Host": "", "Port": 587, "User": "", "Pass": "",
  "From": "no-reply@mahjong-autotable.local", "UseSsl": true
}
```

Empty `Smtp.Host` → `LogEmailSender` (the dev / test default). Real SMTP
delivery requires `Smtp__Host` + `Smtp__User` + `Smtp__Pass` env vars
(see `docs/secrets.md` for the k8s secret layout).

---

## Task 2 — Server-driven `ChangshaRulePreset` CRUD

### Problem

Rule variants (number of hands, max score/hand, allow-flowers, kong-
robbing on/off, etc.) were per-instance fields on `ChangshaGame` —
clients had to know the matrix and pass them at game-create time. There
was no way to **share** a rule set across games or expose the bundled
"canonical Changsha" defaults.

### Decisions

- **Entity** `ChangshaRulePreset` (Id, Name [unique], Description,
  HandLimit, MaxScorePerHand, AllowWashout, AllowKongRobbing,
  AllowConcealedKongPromotion, AllowSevenPairs, AllowChow,
  BotDecisionTimeoutMs, CreatorPlayerId, CreatedAt, UpdatedAt).

- **Seeded "Classic Changsha"** preset id =
  `00000000-0000-0000-0000-000000000001` (exposed as
  `ChangshaRulePreset.ClassicPresetId`). Idempotent seed in
  `DatabaseBootstrapper.SeedClassicChangshaPresetAsync`; runs on every
  provider after schema-up. **Cannot be deleted** —
  `RulePresetController.Delete` short-circuits to `BadRequest` on this
  id because the runtime falls back to it when a game row's
  `RulePresetId` is `null`.

- **Controller** at `Rules/RulePresetController.cs`, route
  `/api/rule-presets`, `ApiPolicy` rate limit (token-bucket, same as
  other `/api/*` surfaces).

| Verb   | Path                       | Auth     | Behaviour                                                          |
|--------|----------------------------|----------|--------------------------------------------------------------------|
| GET    | `/api/rule-presets`        | none     | Lists all presets (newest-first by Name).                          |
| GET    | `/api/rule-presets/{id}`   | none     | Fetches one preset by id.                                          |
| POST   | `/api/rule-presets`        | required | Body = `RulePresetBody` (subset of fields, all optional bar Name). |
| PUT    | `/api/rule-presets/{id}`   | creator  | Partial update. Non-creator → 403. Unique Name conflict → 409.     |
| DELETE | `/api/rule-presets/{id}`   | creator  | Non-creator → 403. Classic preset id → 400.                         |

Wire-up to the runtime (i.e. resolving a preset and feeding `HandLimit`
into `state.MaxHands`) is **deferred to Wave 9** — the contract surface
+ persistence shape is what unblocks Hicks's UI work, and the runtime
already supports per-instance overrides via `ChangshaGameRuntime
.CreateGameAsync`'s existing parameters.

---

## Task 3 — "Master" bot tier

### Problem

The bot ladder was Easy / Medium / Hard. Hard already runs a rigorous
shanten-greedy discard with a defensive bias for self-discarded tiles,
which is close to optimal-without-table-state. Stephen's Wave 8 brief
asked for a tier above Hard that pushes harder on the *opponent-aware*
side without spending the per-decision budget on full Monte-Carlo.

### Decision

`MasterStrategy` (`Changsha/Bot/MasterStrategy.cs`) uses **Hard's exact
discard primary + secondary ordering**, then layers a tertiary tie-
breaker that fires only when shanten AND keep-score both tie:

> **Opponent-safety tie-breaker.** When a logical id has been discarded
> by *an opponent* (not just by anyone) it's strictly safer to release
> — the opponent has proven they don't need it, so it's less likely to
> feed a Pung / Chow claim.

This is a *superset* of Hard: in every position Master makes a
decision, Hard would have made the same decision or one that ties with
it; the tertiary tie-breaker only fires inside Hard's tie bracket.
That's the invariant that lets the `Master_NotWorseThan_Hard_OnSeedSweep`
test in Vasquez's `MasterBotTests.cs` pass — Master scores at least 50%
of Hard's per-seat average on the 12-seed sweep.

Engine registration: `ChangshaBotEngine.Resolve("master")` returns the
singleton instance. Unknown difficulty strings continue to fall back to
Medium (the documented default).

**Deliberate non-decisions:**
- No full 2-ply Monte-Carlo. The wall is opaque (no observed tile
  counts), so simulation degrades to noise; the latency budget
  (`BotDecisionTimeoutMs`, default 2000ms) is also at risk on dense
  hands.
- No suit-purity flush bias. Initial prototype tried this and
  regressed below Hard's win-rate on the seed sweep — flush-shooting
  is too risky without observed wall state. Removed.

---

## Touch-points / pre-existing collisions

- **Apone's Wave 8 work** (Sentry + security headers + CDN cache) is in
  `Observability/SentryConfiguration.cs`, `SentryHubFilter.cs`,
  `SecurityHeadersMiddleware.cs`. These were **untracked WIP** when I
  pulled the branch — Apone's pushed commit (`fbedff6`) edits
  `Program.cs` to reference them but doesn't include the .cs files
  themselves. **I'm shipping them as part of Wave 8** so the build is
  green when the merge lands. The .cs files match Sentry 6.5.0's API
  surface verbatim (`SetBeforeBreadcrumb`, 5-arg `Breadcrumb` ctor,
  `Sentry.Extensibility.RequestSize.None`) — no edits beyond what
  Apone wrote.

- **Vasquez's forward-staged Wave 8 contract tests** in
  `tests/.../Auth/`, `tests/.../RulePresets/`, `tests/.../Security/`,
  `tests/.../Observability/`, plus
  `tests/.../Changsha/Acceptance/MasterBotTests.cs` and
  `tests/.../Negative/NegativeWave8Tests.cs` — all picked up
  unchanged. One small fix: `EmailMagicLinkTests.cs` needed `using
  Microsoft.Extensions.DependencyInjection.Extensions;` for the
  `RemoveAll` extension method (Vasquez's import was missing).

- **EF migrations** named `AddAuthAndRulePresets` were generated for
  all three providers (`Sqlite/20260523054453`, `Postgres/054504`,
  `SqlServer/054509`). `DatabaseBootstrapper` continues to use
  `EnsureCreatedAsync` for Sqlite + `EnsureSqliteWave8TablesAsync`
  (CREATE-IF-NOT-EXISTS) for test harnesses that bypass migrations.

---

## Files added

```
src/backend/src/Mahjong.Autotable.Api/Auth/AuthCookieService.cs
src/backend/src/Mahjong.Autotable.Api/Auth/AuthController.cs
src/backend/src/Mahjong.Autotable.Api/Auth/AuthIdentityService.cs
src/backend/src/Mahjong.Autotable.Api/Auth/AuthOptions.cs
src/backend/src/Mahjong.Autotable.Api/Auth/EmailSender.cs
src/backend/src/Mahjong.Autotable.Api/Auth/MagicLinkService.cs
src/backend/src/Mahjong.Autotable.Api/Auth/OAuthService.cs
src/backend/src/Mahjong.Autotable.Api/Rules/RulePresetController.cs
src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/MasterStrategy.cs
src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/{Sqlite,Postgres,SqlServer}/20260523054*_AddAuthAndRulePresets{,.Designer}.cs
```

## Files modified

```
src/backend/src/Mahjong.Autotable.Api/Data/AppDbContext.cs                — DbSets + entity config
src/backend/src/Mahjong.Autotable.Api/Data/DatabaseBootstrapper.cs        — EnsureSqliteWave8TablesAsync + SeedClassicChangshaPresetAsync
src/backend/src/Mahjong.Autotable.Api/Data/Entities/ChangshaEntities.cs   — RulePresetId on ChangshaGame + 4 new entities
src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/ChangshaBotEngine.cs   — register MasterInstance + "master" arm
src/backend/src/Mahjong.Autotable.Api/Program.cs                          — DI wiring for Auth* + IEmailSender
src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/{...}/*ModelSnapshot.cs — regenerated for new tables
src/backend/tests/Mahjong.Autotable.Api.Tests/Auth/EmailMagicLinkTests.cs — missing using Microsoft.Extensions.DependencyInjection.Extensions;
```

— Bishop
