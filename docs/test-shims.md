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

### Overload 2 — DB-aware (`HttpClient` + `IServiceProvider` + `Guid`)

```csharp
client.WithDirectSession(factory.Services, playerId);
```

Same cookie wiring as overload 1, plus inserts a matching
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
