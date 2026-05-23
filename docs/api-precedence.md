# API status-code precedence

> Phase K Wave 5 — Bishop (Backend).

This note pins the canonical HTTP status-code ordering for endpoints
where the controller has to choose between framework-level rejections
(model-binding, content-type, route resolution) and application-level
gates (authentication, authorisation, domain validation).

The intent is to surface "where does this 4xx come from?" with a
single reference. Every endpoint listed below has a regression-pin
contract test in `src/backend/tests/Mahjong.Autotable.Api.Tests/`;
any deviation from this ordering is a contract change that requires
a memo + a wave bump.

## 1. POST /api/tournaments/{id}/seed

Locked in Phase K Wave 5. Test:
[`TournamentSeedHttpPrecedenceTests`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W4/TournamentSeedHttpPrecedenceTests.cs)
(W4 — soft-pass variant) +
[`ContractGapHardAssertTests.Gap7_TournamentSeed_HttpPrecedence_HardAssert`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W4/ContractGapHardAssertTests.cs)
(W4 — hard variant) + new Wave-5 duplicate-seed coverage in
[`TournamentSeedDuplicateContractTests`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/TournamentSeedDuplicateContractTests.cs).

| Order | Status | Trigger | Source |
|-------|--------|---------|--------|
| 1     | **400** | Body fails the JSON-schema bind (malformed JSON, type mismatch, wrong `Content-Type`). | ASP.NET model binding — fires before any controller code runs. |
| 2     | **401** | No `mahjong_pid` session cookie / unresolved session. | `AuthCookieService.ResolveAsync` (controller code). |
| 3     | **403** | Session resolved but role is not `admin`. | `session.Role` comparison. |
| 4     | **404** | Tournament id does not resolve to a row. | `TournamentService.GetAsync`. |
| 5     | **400** | Empty `seeds` array. | Controller semantic check. |
| 6     | **400** | Duplicate `seedNumber` or duplicate `playerId` in the payload. | Controller semantic check (Wave 5). |
| 7     | **409** | Tournament is already in flight (state conflict from `TournamentService.SeedAsync`). | Service layer. |
| 8     | **200** | `{ tournamentId, updated }`. | Success. |

### Why does step 1 win over step 2?

ASP.NET Core validates the inbound body against the parameter type
BEFORE the controller action runs. The `[ApiController]` attribute
short-circuits a 400 response automatically — auth gates are never
consulted on a schema failure.

**This is the expected behaviour and is not a bug.** Wave-5 test
`Precedence_Anonymous_EmptyBody_Returns_401_NotBody400` accepts
EITHER `400` (model-binding) or `401` (auth gate) for an anonymous
empty body to document the framework-level boundary; clients should
treat both as "your request was rejected before any business logic
ran" and retry only after fixing the schema.

If you NEED the auth gate to fire ahead of body validation for a
specific endpoint (e.g., to keep credential-probing 401s symmetric),
you must read the body as a raw stream and bypass model binding —
that pattern is reserved for the magic-link surface (see
`AuthController.RequestMagicLink`) and is not used for tournament
admin endpoints.

## 2. POST /api/auth/token

| Order | Status | Trigger |
|-------|--------|---------|
| 1     | **400** | Body schema fails. |
| 2     | **401** | No session. |
| 3     | **403** | Session present but role is not `admin`. |
| 4     | **400** | Body missing `subject` (semantic). |
| 5     | **200** | Pinned envelope per `AuthTokenResponse` (Wave 5). |

The same model-binding precedence applies — schema failures emit 400
before the controller runs.

## 3. POST /api/auth/validate

Anonymous endpoint — 401 is never returned. Precedence:

| Order | Status | Trigger |
|-------|--------|---------|
| 1     | **400** | Body schema fails. |
| 2     | **200** | `{ valid: false, error }` for malformed body or invalid token. |
| 3     | **200** | `{ valid: true, subject, claims?, kid }` for valid token. |
| 4     | **429** | Rate-limited at 100/min per-IP (Wave 4 `AuthValidatePolicy`). |

## 4. GET /api/auth/.well-known/jwks.json

Phase K Wave 5 — endpoint is wired but deliberately returns 404 while
the signing scheme is symmetric HMAC (oct keys MUST NOT be published).
Cache-Control: no-store ensures intermediaries don't pin the 404.
Migration to RS256 in Phase L will flip this to a real key set.

## 5. Voice TURN-credential TTL config convergence

Not a status-code precedence, but a related "two valid keys, which
wins?" surface:

| Config key | Wave landed | Wave 5 status |
|------------|-------------|---------------|
| `Voice:TurnCredentialTtlSeconds` | Wave 3 | **canonical** |
| `Voice:TurnTtlSeconds` | Wave 3 (legacy alternate) | accepted with deprecation warning; mapped to canonical at startup; **removal in Wave 6** |

The startup mapping logs a warning identifying the legacy key value
so operators can migrate cluster configs before the Wave-6 removal.
The canonical value wins when both are set.

## 6. JWT signing-key configuration

Phase K Wave 5 — Bishop. Singular `Auth:JwtSigningKey` /
`Authentication:JwtSigningKey` is REMOVED. Setting either causes a
startup `InvalidOperationException` with a migration message
pointing at `docs/jwt-rotation.md` §7. The array
`Auth:JwtSigningKeys` is the only supported shape.

## Cross-references

* [`docs/jwt-rotation.md`](jwt-rotation.md) — rotation runbook and Wave-5 migration note.
* [`src/backend/src/Mahjong.Autotable.Api/Tournament/TournamentController.cs`](../src/backend/src/Mahjong.Autotable.Api/Tournament/TournamentController.cs) — `Seed` endpoint with precedence comments.
* [`src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenController.cs`](../src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenController.cs) — `Issue`, `Validate`, `Jwks` endpoints.
* [`src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/) — Wave-5 contract tests.
