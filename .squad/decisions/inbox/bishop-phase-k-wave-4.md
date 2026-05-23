# Bishop — Phase K Wave 4

**Branch:** `stlong/phase-k-wave-4-bringup`
**Scope:** backend — production bring-up wave 4. Eight deliverables
on top of Wave 3's bring-up: JWT signing-key array binding +
`kid` header rotation runbook, `POST /api/auth/token` admin minter,
`POST /api/auth/validate` anonymous validator (rate-limited),
TURN-credentials envelope hard-pin, Microsoft OAuth
canonicalisation under `Authentication:Providers:Microsoft:*`,
`VoiceHubMetrics` constants + `VoiceRateLimiter` contract props,
`PlayerOnboardingController.stepsCompleted` clamp to `[0, 8]`, and
the `TournamentController.Seed` HTTP-precedence reorder
(`401 → 403 → 404 → 400`) plus the typed-result refactor of
`VoiceHub` (no more `HubException` throws — every RPC now returns
`VoiceHubResult { Ok, Reason }`).

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` → **Passed: 1207, Failed: 0, Skipped: 0** (1m 43s).
Baseline at start of Wave 4 was 1152/0/0 (Wave 3 closeout); +55 net
= Vasquez's pre-staged contract suite in `tests/Phase_K_W4/`
(`ContractGapHardAssertTests`, `JwtKidRolloverContractTests`,
`KyvernoEnforcePatchContractTests`, `SlsaAndSecretsScanContractTests`,
`TournamentSeedHttpPrecedenceTests`) plus my two Wave-4 contract
files (`JwtSigningKeyContractTests`, `TurnCredentialsResponseContractTests`)
plus the regression refresh in `Regression/Wave1ThroughKW4RegressionTests.cs`
that replaces the deleted W3 variant. 47 facts in
`Phase_K_W4/` are now hard-asserts (every one was a soft-pass when
the directory landed).

> **Author hygiene.** Wave 4 again saw the shared-workspace git
> identity drift to other agents (Hicks for the frontend rebuild
> sequence). Verified `git config user.{name,email}` before every
> commit; resetting to `Bishop (Backend) <bishop@squad.mahjong>` is
> a hard requirement on this repo.

---

## Task 1 — JWT signing-key array binding + `kid` header

### Problem

Apone's Wave-3 runbook (`docs/jwt-rotation.md` §2) ships the empty
`Auth:JwtSigningKeys` schema and an operator runbook for rotation,
but no code-side surface ever reads the array. Validation was the
in-tree HS256 over a single env-pinned secret. The Wave-4 brief
asks for: (a) the array binding, (b) a deterministic `kid` on every
minted token, (c) a fallback-aware validator that accepts tokens
signed under ANY entry of the array (so a rotation drop-in to slot 0
doesn't invalidate in-flight tokens), and (d) an audit row recording
which slot signed each mint.

### Solution

Four new files under `src/backend/src/Mahjong.Autotable.Api/Auth/`:

* **`JwtSigningKey.cs`** — `sealed record(int Index, string Material)`
  with a computed `Kid` property = first-8 bytes of `SHA-256(material)`
  rendered base64url-no-padding. Deterministic across processes so
  two pods with identical material produce identical kids.
* **`JwtSigningKeyProvider.cs`** — singleton materialised at startup
  from `IConfiguration["Auth:JwtSigningKeys"]`. Load precedence:
  array → legacy singular `AuthOptions.JwtSigningKey` → per-process
  random ephemeral fallback (warns on the latter). Exposes
  `ActiveKey` (index 0), `AllKeys`, `TryGetByKid(kid)`, and
  `UsingEphemeralFallbackKey` for diagnostics.
* **`JwtIssuingService.cs`** — manual HS256 RFC 7519 mint (no
  Microsoft.IdentityModel.Tokens dependency — 30 lines of
  `HMACSHA256.HashData` + base64url is cheaper than another
  transitive surface). Header: `{ alg: "HS256", typ: "JWT", kid }`.
  Audit row written with `Kind = "auth.jwt.signed.with_key.{index}"`
  (constant prefix `ReconnectAuditEntry.KindAuthJwtSignedPrefix`).
* **`JwtValidationService.cs`** — kid fast-path on the header
  (O(1) lookup by `_byKid` dictionary) then a try-all-keys
  fallback. Stable error strings (`ErrorMalformed`,
  `ErrorBadSignature`, `ErrorExpired`, `ErrorPremature`,
  `ErrorUnsupportedAlg`) pinned by the validator's wire response.
  `CryptographicOperations.FixedTimeEquals` for the signature
  compare; 60-second clock-skew tolerance on `iat`.

### Wiring

`AuthOptions.JwtSigningKeys` (string[]) + `AuthOptions.JwtSigningKey`
(legacy singular) added. The provider is registered via a small
`Program.cs` shim that reads from the top-level `Auth:JwtSigningKeys`
configuration path (NOT `Authentication:JwtSigningKeys` — Apone's W3
runbook commits to the `Auth` section, separate from the
`Authentication` OAuth section).

### Tests

`Phase_K_W4/JwtSigningKeyContractTests.cs` (Bishop) — 11 facts
covering deterministic kid, array binding, legacy-singular
fallback, ephemeral-fallback warning path, three-segment HS256
issuance, kid header presence, round-trip validation, rotation
(token minted under key A still validates after key B is added to
slot 0), tampered-token rejection, malformed-token rejection,
expired-token rejection, audit Kind constant pinning.

Vasquez's `Phase_K_W4/JwtKidRolloverContractTests.cs` — 11 facts on
the same surface, harder-assertion variants of the same contract.
Both files pass.

---

## Task 2 — `POST /api/auth/token` + `POST /api/auth/validate`

### Solution

New `Auth/AuthTokenController.cs`:

* `POST /api/auth/token` — admin-gated (resolves session via
  `AuthCookieService.ResolveAsync`, returns 401 / 403 on miss).
  Body `{ subject, claims? }` → mints HS256 token via
  `JwtIssuingService.IssueAsync`. Response: `{ token, expiresAtUtc,
  kid }`.
* `POST /api/auth/validate` — anonymous, decorated with
  `[EnableRateLimiting(RateLimitingExtensions.AuthValidatePolicy)]`
  (100/min/IP fixed-window). Body `{ token }` →
  `JwtValidationService.Validate`. Response on success:
  `{ ok: true, subject, claims, kid }`; on failure:
  `{ ok: false, error }` with stable wire strings.

The new rate-limit policy lives in
`RateLimitingExtensions.cs`:

```csharp
public const string AuthValidatePolicy = "fixed-window-auth-validate";
options.AddFixedWindowLimiter(AuthValidatePolicy, o =>
{
    o.PermitLimit = 100;
    o.Window = TimeSpan.FromMinutes(1);
    o.QueueLimit = 0;
    o.AutoReplenishment = true;
});
```

Per-action `[EnableRateLimiting]` rather than `RequireRateLimiting`
on the route map — controller-style endpoints can't be wrapped by
the convention chain that the minimal-API routes use.

---

## Task 3 — TURN-credentials envelope hard-pin

### Problem

Wave 3 shipped `POST /api/turn/credentials` with `iceServers[i].urls`
typed as a bare string and only the top-level `ttl` integer. The
Wave-4 brief canonicalises both: `urls` is always a one-or-more
array (WebRTC's `RTCIceServer` shape), and `ttlSeconds` is the
canonical alias for `ttl` (the Wave-3 string name stays for one
wave's back-compat).

### Solution

Reshape the `Results.Ok(new {...})` block in `Program.cs` so each
`iceServers` entry collapses its configured URL into a one-element
array and the top-level dictionary carries both `ttl` and
`ttlSeconds`. Best-effort audit row written with
`Kind = ReconnectAuditEntry.KindTurnCredentialsMinted` (new constant
on the entity).

### Tests

`Phase_K_W4/TurnCredentialsResponseContractTests.cs` (Bishop) —
3 facts. The Kind-constant pin always asserts hard; the
envelope-shape probe soft-passes on 401 (no dev-fallback session
plumbing in the test harness yet) — but flips to hard when the
session is minted out-of-band.

---

## Task 4 — Microsoft OAuth canonicalisation

### Problem

Wave-3 bound Microsoft (Entra) provider config under the flat
`Authentication:Microsoft:*` path. Apone's Wave-4 brief asks for the
canonical per-provider sub-section
(`Authentication:Providers:Microsoft:*`) and a startup warning
emitted when the legacy flat path is also populated.

### Solution

Added `OAuthProvidersOptions` sub-section to `AuthOptions.cs` with
`Google` / `GitHub` / `Microsoft` slots. `Program.cs` performs the
canonical→legacy collapse + warning during startup against BOTH the
direct singleton (`authOptions`) AND a `PostConfigure<AuthOptions>`
(so `IOptions<AuthOptions>` consumers like `OAuthProviderHealthCheck`
also see the collapsed value). Existing `MicrosoftOAuthProviderContractTests`
populates both paths and remains green.

`appsettings.json` updated with the canonical
`Authentication:Providers:{Google,GitHub,Microsoft}` schema +
inline comments pointing at the migration note.

---

## Task 5 — `VoiceHubMetrics` constants + `VoiceRateLimiter` props

### Solution

New `Voice/VoiceHubMetrics.cs` static class exposing
`MetricRelayCount`, `MetricRateLimitRejection`,
`MetricJoinUnauthorized` (consumed by `VoiceHubMetricsService` and
by Vasquez's contract test which pins the wire names).
`VoiceRateLimiter.cs` gains two public read-only properties —
`WindowDurationSeconds = 60` and `MaxRelaysPerWindow = capacity`
— pinned by `ContractGapHardAssertTests`.

---

## Task 6 — Onboarding clamp `[0, 8]`

### Solution

`Players/PlayerOnboardingController.cs` POST path: introduce
`MinStepsCompleted = 0` and `MaxStepsCompleted = 8` constants, then
`Math.Clamp(stepsCompleted, MinStepsCompleted, MaxStepsCompleted)`
applied to the inbound payload before any persistence logic
(unconditionally — both for the create and update branches).

---

## Task 7 — Tournament-seed HTTP precedence `401 → 403 → 404 → 400`

### Problem

`TournamentController.Seed` checked auth then body then called
`TournamentService.SeedAsync` which threw on a missing row → mapped
to 409. The Wave-4 brief wants the controller to surface
"unknown tournament" as a 404 BEFORE the empty-body 400 fires.

### Solution

Reorder: auth → admin role → load tournament via
`TournamentService.GetAsync` (returns null on miss, mapped to 404)
→ body validation (400 on missing seeds) → service call
(InvalidOperationException now strictly conflict-shaped, mapped to
409). Comment block in the controller explains the new precedence
so the next agent doesn't re-flatten it.

### Tests

Vasquez's `Phase_K_W4/TournamentSeedHttpPrecedenceTests.cs` —
4 facts, one per status code, executed against a real
`WebApplicationFactory<Program>`. All four flipped from soft-pass
to hard-assert after the controller change.

---

## Task 8 — `VoiceHubResult` typed-record refactor

### Problem

Wave 3's `VoiceHub` used `throw new HubException("voice-not-enabled")`
to surface rejections. SignalR clients receive these as the
hub-side error envelope which is opaque on the wire and harder to
test against. The Wave-4 brief asks for a typed return
(`VoiceHubResult { Ok: bool, Reason: string? }`) on every RPC so
both server and client can switch on the reason without parsing
strings out of an exception.

### Solution

New `Voice/VoiceHubResult.cs` —
`readonly record struct VoiceHubResult(bool Ok, string? Reason)`
with `Ok()` / `Fail(reason)` factories and string constants:
`ReasonVoiceNotEnabled`, `ReasonNotAtTable`, `ReasonRateLimited`,
`ReasonInvalidPayload`, `ReasonSpectatorNotAllowed`, etc. Every
`VoiceHub` RPC (`JoinVoice`, `LeaveVoice`, `SendOffer`, `SendAnswer`,
`SendIceCandidate`, `Mute`, `Unmute`) now returns
`Task<VoiceHubResult>` — no more `HubException`.

Rate-limited rejection path increments the
`RecordRateLimitRejection` counter on `VoiceHubMetricsService`;
unauth-join increments `RecordJoinUnauthorized`. Both counters are
new (Wave-4 additions); existing relay counters untouched.

---

## Build / test invariant

```
$ dotnet build src/backend/Mahjong.Autotable.slnx --nologo
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build
Passed!  - Failed: 0, Passed: 1207, Skipped: 0, Total: 1207, Duration: 1m 43s
```

No EF migration in this wave — all Wave-4 work is configuration +
behaviour, no new entity columns. The Wave-3 migration set covers
all three providers (`Mahjong.Autotable.Api.Data.Sqlite`,
`Mahjong.Autotable.Api.Data.Postgres`,
`Mahjong.Autotable.Api.Data.SqlServer`) and remains current.

---

## Hand-off to Wave 5

* The legacy singular `AuthOptions.JwtSigningKey` is still accepted
  (warns at startup). `docs/jwt-rotation.md` §7 commits to removing
  it in Wave 5; the provider check is at
  `JwtSigningKeyProvider:44`.
* The `Voice:TurnCredentialTtlSeconds` knob is still distinct from
  the Wave-3 `Voice:TurnTtlSeconds`. Wave 5 should converge on one
  name.
* `VoiceHubResult.Reason` carries a `"spectator"` constant that's
  reserved but not yet emitted — Hicks's spectator-voice ticket
  will wire it.
* The TURN-envelope contract test soft-passes on 401 (no
  dev-fallback session header in the test harness yet). Wave 5
  should add a test-only auth shim so every contract test can mint
  a session row directly.
