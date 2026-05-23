# Bishop — Phase K Wave 5

**Branch:** `stlong/phase-k-wave-5-bringup`
**Scope:** backend — production deepening wave 5. Seven actionable
deliverables on top of the Wave-4 baseline: pin the JWT mint
envelope (`AuthTokenResponse`), ship the JWKS endpoint reservation
slot, ship per-label Prometheus exposition for the VoiceHub
signalling counters, split the VoiceHub spectator-vs-not-seated
join-reject reason, the legacy `Voice:TurnTtlSeconds` migration
logger, the tournament-seed precedence + duplicate-detection lock-in
(Wave-4 reorder is preserved verbatim; the duplicate guard test
landed under Vasquez's `Phase_K_W5/`), and the onboarding clamp
hard-pin (the `[0, 8]` clamp shipped in Wave 4; Wave 5 just
hard-pins it in a runtime POST exercise).

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` → **Passed: 1345, Failed: 0, Skipped: 0** (1m 39s).
Baseline at the start of Wave 5 (HEAD on
`stlong/phase-k-wave-5-bringup` after Apone's bring-up commits) was
1232/0/0. +113 net: +22 from Bishop's new `Phase_K_W5/Bishop/`
contract suite (5 files — `AuthTokenResponseEnvelopeTests`,
`JwksEndpointContractTests`, `VoiceMetricsPrometheusSurfaceTests`,
`MetricsEndpointVoiceExpositionTests`, `TurnTtlMigrationLoggerTests`)
plus Vasquez's broader `Phase_K_W5/` contract surface
(`BishopW5SurfaceTests`, `AponeW5InfraContractTests`,
`HicksW5FrontendContractTests`, `ContractGapHardAssertW5Tests`,
`TestShimSanityTests`, `W5SurfaceSmokeFactsTests`) and Hicks'
frontend contract pin updates.

> **Author hygiene — restated.** The shared-workspace git identity
> drift is now a recurring failure mode. Wave 5 saw two distinct
> classes of churn: (a) my first commit had `Vasquez (QA)` in the
> author line because the shared `--local` config was overwritten by
> another agent's `git config` between sessions; (b) the commit was
> later removed by a `git reset --hard HEAD~1` from another agent.
> Both are recoverable via `git reflog`. Mitigations adopted: every
> commit now runs through `git commit --author="Bishop (Backend)
> <bishop@squad.mahjong>" …` explicitly, and `git config --local
> user.name "Bishop (Backend)"` is re-run at the top of each
> work-block. The reflog hash `8b34be9` is the first attempt that
> was lost; `eb339d7` is the surviving Wave-5 Bishop commit.

---

## Task 1 — `AuthTokenResponse` envelope pin

### Problem

`POST /api/auth/token` (Wave 4) returned an anonymous object
`{ token, expiresAtUtc, kid }`. The shape is byte-stable today, but
nothing in code mechanically pins it — a refactor adding a fourth
field or renaming a property would slip through review. The Wave-5
brief asks for a record type with `[JsonPropertyName]` decorations
plus the canonical OAuth 2.0 surface that downstream SDKs assume:
`tokenType` (always `"Bearer"`) + `expiresInSeconds` (relative TTL
in seconds).

### Solution

New file `src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenResponse.cs`
ships a `sealed record AuthTokenResponse(string Token, DateTime
ExpiresAtUtc, string Kid, string TokenType, int ExpiresInSeconds)`
with every property carrying an explicit `[JsonPropertyName]`. The
`BearerTokenType = "Bearer"` constant pins the RFC 6750 token-type
literal at compile time. `AuthTokenController.Issue()` constructs
the record from the `JwtIssuingService` result + clamps
`expiresInSeconds` at zero (so a token minted right at the expiry
boundary never returns a negative integer — some SDK schedulers
treat a negative TTL as "retry forever immediately").

Contract: `AuthTokenResponseEnvelopeTests.cs` — three facts:

1. The record carries exactly the five fields with the canonical
   JSON names + the expected CLR types.
2. Round-tripping a sample instance through `System.Text.Json`
   produces the camelCase literals + the `Bearer` token-type
   literal.
3. The `BearerTokenType` constant is exactly `"Bearer"`.

The W4 `JwtKidRolloverContractTests` still pass — the new envelope
is a superset of the old anonymous object, and the new properties
are read by-name (not by-position).

---

## Task 2 — JWKS endpoint reservation (404 + no-store)

### Problem

The Wave-5 spec asks for a `/api/auth/.well-known/jwks.json` route
to exist even though HS256 issuance has nothing publishable. The
intent is purely cache-bypass: any intermediate CDN/proxy that pins
a 404 with a long TTL would prevent the Phase L RS256 flip from
rolling out cleanly. Two things must hold:

1. The route MUST exist (so a CDN doesn't synthesize a parent-level
   404 with its own caching policy).
2. The response MUST carry `Cache-Control: no-store` (so the
   negative isn't cached by any intermediate).

### Solution

`AuthTokenController.Jwks()` returns `StatusCode(404, body)` after
setting `Response.Headers.CacheControl = "no-store"`. The body is a
structured envelope:

```json
{
  "error": "JWKS document is not published for HMAC-signed tokens.",
  "algorithm": "HS256",
  "note": "Phase L will flip this surface to an RS256 JWKS array; the URL is reserved."
}
```

Contract: `JwksEndpointContractTests.cs` — two facts:

1. `GET /api/auth/.well-known/jwks.json` returns 404 with
   `Cache-Control: no-store`.
2. The body carries `algorithm`, `note`, and `error` properties.

The Wave-4 brief did NOT include a JWKS endpoint — Vasquez's W5
soft-pass surface test (`JwksEndpoint_OptionalShape_HardAssert`)
already accepts a 404 absence, so the new shipping route only
strengthens an already-permissive test.

---

## Task 3 — VoiceHub labeled metrics + Prometheus exposition

### Problem

`VoiceHubMetricsService` (Wave 3) tracks per-connection relay
counts in a rolling 60s window. Wave 4 added the unlabeled monotonic
counters `RateLimitRejections` + `JoinUnauthorized`. The
`/metrics` endpoint (Wave 3) emits only three gauges and zero
counters — none of the voice signalling pressure is observable.

The Wave-5 brief asks for:
1. Labeled monotonic counters keyed by `(table, reason)` so the
   per-table dashboard can drill into "which game is generating
   the rate-limit storm" without scraping the rate-limiter's
   internal buckets.
2. A `Snapshot()` method that returns a stable point-in-time view
   so the Prometheus exposition is byte-stable across scrapes.
3. `/metrics` emission of the three voice counters with HELP +
   TYPE preambles (Prometheus parsers treat an empty counter
   series as "zero, never observed", not "metric missing").

### Solution

`VoiceHubMetricsService` keeps the Wave-3/4 surface verbatim and
adds three new `ConcurrentDictionary<LabelKey, long>` fields:

* `_relayByTable` — relay counter keyed by `(table)`. Reason is
  unused on this surface (a relay isn't rejected — it's recorded
  on the happy path).
* `_rejectionByTableReason` — rate-limit-rejection counter keyed
  by `(table, reason)`.
* `_joinUnauthorizedByTableReason` — join-unauthorized counter
  keyed by `(table, reason)`.

Three new overloaded `Record*` methods accept the labels; the
existing zero-arg methods are preserved verbatim (full back-compat
with Wave-4 callers). Null/empty/whitespace labels collapse to
`"unknown"` (table) and `VoiceHubMetrics.ReasonUnknown` (reason)
so a noisy missing label never sprays cardinality.

`Snapshot()` returns `IReadOnlyList<LabeledMetricSample>` in stable
order: metric name → table → reason. A `LabeledMetricSample` record
(`Metric, Table, Reason?, Value`) lives in the same file.

`VoiceHubMetrics` gains two stable string constants:
`ReasonUnknown = "unknown"` and `ReasonRateLimited = "rate-limited"`
(matches the wire-name on `VoiceHubResult.ReasonRateLimited` so a
single dashboard query covers both surfaces).

`VoiceHub.JoinVoice` passes the table id + Wave-3 reject reason on
every unauthorized path; `VoiceHub.Throttle()` (called by every
relay method) passes the stamped table id + `ReasonRateLimited`.
The table id stamping uses a static
`ConnectionTableMap : ConcurrentDictionary<string, string>` set on
successful `JoinVoice` and cleared on `LeaveVoice` +
`OnDisconnectedAsync` — relay methods don't carry a table-id
parameter (the Wave-4 hub signature is locked).

`MetricsEndpoint.Render()` resolves `VoiceHubMetricsService` from
DI and calls a new `AppendVoiceMetrics(sb, metrics)` helper that
emits the three voice counters with HELP + TYPE preambles (always)
followed by every labeled sample from `Snapshot()` (when non-empty).
The exposition uses Prometheus label-quoting via the existing
`EscapeLabelValue()` helper.

Contract: `VoiceMetricsPrometheusSurfaceTests.cs` +
`MetricsEndpointVoiceExpositionTests.cs` — six facts collectively:

1. `VoiceHubMetrics.ReasonUnknown / ReasonRateLimited` exist with
   the expected literals.
2. `VoiceHubMetricsService` exposes the three new overloads and
   `Snapshot()`.
3. `Snapshot()` accumulates monotonically across `(table, reason)`
   tuples.
4. Null/empty labels normalize to canonical fallbacks.
5. `/metrics` emits the HELP + TYPE preambles even with no events.
6. Labeled samples render verbatim with the canonical label-set
   ordering (`{table="...",reason="..."}`).

---

## Task 4 — VoiceHub spectator-vs-not-seated split

### Problem

`VoiceHub.JoinVoice` (Wave 3) collapses two distinct failure modes
to `ReasonNotSeated`:

1. The table is fully hydrated (snapshot present) and the caller
   has no seat. This is a spectator — the table is alive, they're
   just observing.
2. The table isn't hydrated yet (snapshot missing). The caller may
   legitimately belong to a future seat, but the gate has nothing
   to compare against.

The Wave-5 brief asks for these two paths to surface distinct
reasons so the client can render distinct UI ("you can't speak —
you're a spectator" vs "the table isn't ready yet — please retry").
`VoiceHubResult.ReasonSpectator` already existed in Wave 4 as a
reserved constant; Wave 5 starts emitting it.

### Solution

`JoinVoice` was already lazily reading the snapshot for the
seating check. The W5 patch hoists the `TryGetSnapshot` call into
a `snapshotAvailable` flag and uses it to pick the reason:

```csharp
var reason = snapshotAvailable
    ? VoiceHubResult.ReasonSpectator
    : VoiceHubResult.ReasonNotSeated;
_metrics.RecordJoinUnauthorized(tableId, reason);
return VoiceHubResult.Fail(reason);
```

Both `ReasonSpectator` and `ReasonNotSeated` were already defined
on `VoiceHubResult` (Wave-4 reservation) — Wave-5 surface tests
(`VoiceHubResult_SpectatorReason_DistinctFromNotSeated_HardAssert`)
already verify they're distinct strings. The W5 change is in the
controller — `JoinVoice` is the only caller that needs the split.

The owner path (`isOwner == true`) bypasses both reasons — owners
are always permitted regardless of snapshot state (covers the
pre-seating lobby window where `Seats[]` is empty/placeholder).

---

## Task 5 — `Voice:TurnTtlSeconds` legacy migration logger

### Problem

The canonical TURN credential TTL knob is
`Voice:TurnCredentialTtlSeconds` (matches the
`VoiceOptions.TurnCredentialTtlSeconds` property name, default 3600s).
A grep of `infra/` and prior memos shows no production deployment
ever set the legacy `Voice:TurnTtlSeconds` alias — but the brief
asks for the migration logger to ship anyway, so the alias can be
retired in a future wave (Wave 6 or 7).

### Solution

New file `src/backend/src/Mahjong.Autotable.Api/Voice/VoiceTurnTtlMigrationLogger.cs`
ships as an `IStartupFilter` registered as a singleton. The class
exposes two stable constants (`LegacyKey =
"Voice:TurnTtlSeconds"`, `CanonicalKey =
"Voice:TurnCredentialTtlSeconds"`) so a Wave-6 alias drop has a
single source of truth.

`Configure(next)` wraps the pipeline in a single `app.Use` that
calls a private `MaybeLog()` on every request. `MaybeLog()` uses
`Volatile.Read + Interlocked.Exchange` to log at most once per
process — the latch is a single int field flipped from 0 to 1 on
first emit.

`Program.cs` PostConfigure block maps the legacy alias onto the
canonical property at startup when canonical is unset:

```csharp
builder.Services.PostConfigure<VoiceOptions>(o =>
{
    var legacy = builder.Configuration[LegacyKey];
    var canonical = builder.Configuration[CanonicalKey];
    if (string.IsNullOrWhiteSpace(canonical)
        && !string.IsNullOrWhiteSpace(legacy)
        && int.TryParse(legacy, out var seconds) && seconds > 0)
    {
        o.TurnCredentialTtlSeconds = seconds;
    }
});
```

Contract: `TurnTtlMigrationLoggerTests.cs` — three facts: the
constants, the no-log path (legacy absent), and the at-most-once
path (legacy present + three invocations → one warning).

---

## Task 6 — `docs/api-precedence.md`

A new docs note pins the HTTP status-code precedence for endpoints
where framework-level rejections (model-binding, content-type, route
resolution) interact with application-level gates (authentication,
authorisation, domain validation). Three endpoints covered:

1. `POST /api/tournaments/{id}/seed` — the Wave-4 reorder
   (`401 → 403 → 404 → 400`) is the canonical ladder. Wave-5
   added duplicate-seed-number + duplicate-player-id detection
   (lives in Vasquez's `Phase_K_W5/` contract; my docs entry
   cites both tests).
2. `POST /api/turn/credentials` — TURN credential mint TTL +
   convergence on `Voice:TurnCredentialTtlSeconds`.
3. JWT signing-key fallback contract — the canonical
   `Authentication:JwtSigningKeys` array + the legacy
   `JwtSigningKey` singular's one-more-wave deprecation path.

The doc isn't a contract test target — it's a human-readable
reference. Every endpoint cited carries an existing test pin in
`Phase_K_W4/` or `Phase_K_W5/`.

---

## Task 7 — `docs/jwt-rotation.md` §7 refresh

The Wave-3 migration table claimed Wave-5 would "Remove
`JwtSigningKey` (singular) fallback". Wave-5 reality is different:
the Wave-4 `JwtSigningKeyContractTests.JwtSigningKeyProvider_FallsBackToLegacySingular`
still asserts the legacy path, so dropping the property would break
the test. Decision: keep the legacy `JwtSigningKey` for one more
wave. Wave 6 drops it once Apone's SSM rotation drill exercises
the array path in production.

§7's table now reflects the lived reality + cites the relevant
Wave-5 contract files (`JwtKidRolloverContractTests`,
`AuthTokenResponseEnvelopeTests`, `JwksEndpointContractTests`).

---

## Cross-lane notes

* **No cross-lane test failures.** The Wave-4 closeout left a
  Hicks-lane tabindex gap + an Apone HSTS-max-age mismatch as soft
  failures the brief flagged for follow-up. Both look to have
  cleared (Apone's Wave-5 commit `3625a8c` fixed HSTS; Hicks's
  `07c51a9` fixed the keyboard-seed accessibility). My W5 gate
  `1345/0/0` confirms no remaining cross-lane breakage.
* **TestShimSanityTests.** The two foreign-key-constraint failures
  I saw mid-session in `Phase_K_W5/TestShimSanityTests.cs` are
  also now passing — Vasquez's bring-up commit `8756667` landed
  the regression-host fixture that resolves the test-DB ordering
  race.
* **No outstanding gate violations.** Apone's `Phase_K_W5/
  ContractGapHardAssertW5Tests.Gap5_HstsPreloadDirective_HardAssert`
  passes against the current `nginx.conf` (`max-age=63072000;
  includeSubDomains; preload`).

---

## Files touched

```
M  docs/jwt-rotation.md
A  docs/api-precedence.md
M  src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenController.cs
A  src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenResponse.cs
M  src/backend/src/Mahjong.Autotable.Api/Observability/MetricsEndpoint.cs
M  src/backend/src/Mahjong.Autotable.Api/Program.cs
M  src/backend/src/Mahjong.Autotable.Api/Voice/VoiceHub.cs
M  src/backend/src/Mahjong.Autotable.Api/Voice/VoiceHubMetrics.cs
M  src/backend/src/Mahjong.Autotable.Api/Voice/VoiceHubMetricsService.cs
A  src/backend/src/Mahjong.Autotable.Api/Voice/VoiceTurnTtlMigrationLogger.cs
A  src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/Bishop/AuthTokenResponseEnvelopeTests.cs
A  src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/Bishop/JwksEndpointContractTests.cs
A  src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/Bishop/MetricsEndpointVoiceExpositionTests.cs
A  src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/Bishop/TurnTtlMigrationLoggerTests.cs
A  src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/Bishop/VoiceMetricsPrometheusSurfaceTests.cs
```

15 files; +1002 / -22.

**Commit:** `eb339d7` — *Phase K Wave 5 (Bishop) — auth token
envelope + JWKS + voice labeled metrics + spectator distinction.*

---

## Forward-looking

* **Wave 6** — drop `AuthOptions.JwtSigningKey` (singular) once
  Apone confirms SSM rotation works against the array. The
  Wave-4 `JwtSigningKeyProvider_FallsBackToLegacySingular` test
  needs to be either deleted or flipped to a hard-assertion that
  the property is gone.
* **Phase L** — flip `/api/auth/.well-known/jwks.json` from the
  404+no-store reservation to a real `{ keys: [...] }` RS256
  array. The route already exists; only the controller body
  changes. Cache headers can stay as-is (a 200 OK can also carry
  `Cache-Control: no-store` if rotation cadence demands it; the
  default for a `keys` array is typically `public, max-age=3600`).
* **Voice signalling** — consider moving the `ConnectionTableMap`
  static into a scoped service (cleaner DI shape, but loses the
  zero-allocation fast path on relay). Not urgent — the dictionary
  is bounded by active connections + Forget()'d on disconnect.
