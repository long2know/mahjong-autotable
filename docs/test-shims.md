# Test shims — inventory

**Owner:** Vasquez (QA).
**Wave:** Phase K Wave 5.

Test-only helpers gated by the `TESTING_SHIM` preprocessor symbol so
they NEVER leak into the production assembly. The symbol is defined
exclusively in the test project (`src/backend/tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj`):

```xml
<PropertyGroup>
  <DefineConstants>$(DefineConstants);TESTING_SHIM</DefineConstants>
</PropertyGroup>
```

Each shim file wraps its code in `#if TESTING_SHIM … #endif` so that
referencing it from the production assembly is a compile-time error.

---

## `TestHttpClientExtensions.WithDirectSession`

**Location:** `src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/TestHttpClientExtensions.cs`

**Purpose:** stamp an authenticated session on an `HttpClient` so a
contract test can hit a controller that requires an authenticated
cookie WITHOUT going through the full magic-link / OAuth flow. The
shim has three overloads of increasing fidelity:

### Overload 1 — cookie-only (`HttpClient` + `Guid`)

```csharp
client.WithDirectSession(playerId);
```

Sets the `mahjong_pid` (anonymous identity) and `mahjong_auth`
(session token) cookies on the `HttpClient`'s default request
headers. The session token is a deterministic hex string derived
from `playerId` — no DB row is inserted. Use when the controller
under test does NOT validate the session against the DB.

> **`mahjong_pid` is UNSIGNED in this overload.** It has no
> `IServiceProvider`, so it cannot mint the signed identity credential.
> The server rejects unsigned identity cookies and issues a fresh
> identity instead, so it will NOT resolve as `playerId`. Use overload 2
> or 3 (or `TestHttpClientExtensions.SignedPlayerIdCookie(services, playerId)`)
> whenever the endpoint under test must resolve THIS player id.

### Overload 2 — DB-aware (`HttpClient` + `IServiceProvider` + `Guid`)

```csharp
client.WithDirectSession(factory.Services, playerId);
```

Same cookie wiring as overload 1 — except `mahjong_pid` carries a
properly **signed** identity credential (minted through the host's own
`PlayerIdentityService`), so the server resolves it as `playerId` —
plus inserts a matching
`PlayerProfile` + `PlayerAuthIdentity` + `PlayerAuthSession` row
into the test DB (resolved via `IServiceProvider.GetRequiredService<AppDbContext>()`).
Use when the controller validates the cookie via the DB lookup path
(`AuthCookieService.ResolveAsync` and friends).

### Overload 3 — role-stamped (`HttpClient` + `IServiceProvider` + `Guid` + `role`)

```csharp
client.WithDirectSession(factory.Services, playerId, role: "admin");
```

Same as overload 2 but stamps the supplied role onto
`PlayerAuthSession.Role`. Use for the admin-gated controllers
(audit, replay-admin, tournament-admin) that check the role on
the session row.

### FK invariants (Wave-5 shim correctness)

`PlayerAuthIdentity.PlayerId` has a cascade FK to
`PlayerProfile.PlayerId`. Overloads 2 & 3 idempotently insert
a profile row first; calling the shim multiple times for the
same `playerId` is safe and does NOT duplicate identity rows.

### Sanity tests

See `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/TestShimSanityTests.cs`
for the three regression facts that lock the shim's behaviour:

- `WithDirectSession_CookieOnly_StampsBothCookies`
- `WithDirectSession_DbOverload_InsertsResolvableSession`
- `WithDirectSession_Idempotent_NoDuplicateIdentityRows`

---

## Production-leakage guarantee

The shim file references `AppDbContext`, `PlayerProfile`,
`PlayerAuthIdentity`, `PlayerAuthSession`, and DI plumbing — all
production-assembly types — but the production assembly itself
NEVER compiles the shim code because the test csproj is the only
csproj that defines `TESTING_SHIM`. The compiler treats the file
as empty for any other consumer.

To verify (after a build of the API project):

```bash
strings src/backend/src/Mahjong.Autotable.Api/bin/Debug/net10.0/Mahjong.Autotable.Api.dll \
  | grep -F WithDirectSession
# (no output)
```

---

## Adding new shims

When you add a new test-only extension method:

1. Place it under `src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/`.
2. Wrap the entire file in `#if TESTING_SHIM … #endif`.
3. Append a new section to this document (path, purpose, signatures,
   sanity tests).
4. Write at least one sanity test that proves the shim's
   contract — typically under `Phase_K_W<N>/<Name>SanityTests.cs`.
5. Update `docs/test-harness-handoff.md` if the shim changes the
   harness boot order.

---

## §2 `CommentaryGeneratorTestShim` (Phase K Wave 6)

**Location:** `src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/CommentaryGeneratorTestShim.cs`

**Purpose:** test-only deterministic commentary generator stub
parallel to the W5 `TestHttpClientExtensions.WithDirectSession`
auth shim. Returns predictable per-game content keyed by a
SHA-256 hash of the `gameId` so the W6 commentary-panel Playwright
spec + the backend contract gap tests can assert against stable
output WITHOUT a real LLM in the loop.

### Surface

```csharp
public static IReadOnlyList<CommentaryItem> Generate(string gameId);
public static string HashSeed(string gameId);
public static bool ProductionInterfaceShipped();
public sealed record CommentaryItem(int Sequence, string Speaker, string Text);
```

### Determinism contract

- Same `gameId` → identical items across runs (sequence, speaker,
  text all stable).
- Different `gameId`s → distinct text (no truncation collision in
  the hex slice).
- 4 items per call, rotating across 3 speakers
  (`ShimAnalyst`, `ShimColourCommentary`, `ShimSidelineReporter`).
- Empty / null / whitespace `gameId` → `ArgumentException`.

### Why a SEPARATE shim instead of using Bishop's W6 default impl?

Bishop's `ICommentaryGenerator` ships with a no-op default impl that
always returns `{ items: [] }`. For tests that need non-trivial
content (panel-renders-content state assertion), an empty array
makes the panel state machine collapse to its empty arm — the
content arm is never exercised. The shim returns 4 deterministic
items so the content arm runs.

### Forward-stage probe

```csharp
CommentaryGeneratorTestShim.ProductionInterfaceShipped();
// true once Bishop's W6 ICommentaryGenerator lands in the API
// assembly; false during the bring-up window. Sanity-test friendly.
```

### Sanity tests

See `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W6/CommentaryGeneratorTestShimSanityTests.cs`
for the contracts the shim's behaviour is pinned against:

- `Generate_SameGameId_ReturnsSameItems`
- `Generate_DifferentGameIds_DistinctOutput`
- `Generate_SpeakerRotation_CoversAllRosterNames`
- `Generate_EmptyOrNullGameId_Throws`
- `Generate_FourItems_SequenceMonotonic`
- `HashSeed_IsLowercase64HexChars`
- `ProductionInterfaceShipped_ReturnsBool`

### Production-leakage guarantee

The file is wrapped in `#if TESTING_SHIM … #endif`, defined ONLY in
the test csproj. The production `Mahjong.Autotable.Api.dll` never
compiles this code. To verify after a build:

```bash
strings src/backend/src/Mahjong.Autotable.Api/bin/Debug/net10.0/Mahjong.Autotable.Api.dll \
  | grep -E "CommentaryGeneratorTestShim|ShimAnalyst|ShimColourCommentary"
# (no output)
```
