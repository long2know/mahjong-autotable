# Drake — JWT signing-key production hardening

**Date:** 2026-06-04
**Branch:** `fix/drake-jwt-prod-hardening` (squashed into `main` @ _TBD_)
**Charter context:** Ripley's Docker-deploy-proof wave
(`ripley-docker-deploy-proof.md`, commit `ab34d09`) flagged ONE
remaining production blocker. Drake's lane (Auth / Identity area)
owns it.

## Verdict — ✅ PROD-READY

The container restart-survival path now matches the operator
expectation set by `docs/jwt-rotation.md §2` (a contract documented
since Phase K Wave 3 but never implemented).

## Problem statement

`JwtSigningKeyProvider` (Phase K Wave 4 — Bishop) shipped with a
**dev-friendly fallback**: when neither `Authentication:JwtSigningKeys`
nor the legacy singular `Authentication:JwtSigningKey` is set, it
mints a per-process random HMAC key + logs a loud warning. That
keeps `dotnet run` / VS Code F5 / `xunit` frictionless for local
developers.

**The production restart-survival bug:** the per-process random key
is regenerated on every container start. JWTs minted by the FIRST
container fail signature validation under the SECOND container
because the new random key never matches the old signature.
Effective user impact: every authenticated session is silently
invalidated on every restart / rolling-deploy / OOM-kill / node
reschedule.

Stephen runs the canonical `mahjong-autotable:latest` image on his
own Linux server. The container ENV pins
`ASPNETCORE_ENVIRONMENT=Production` (Dockerfile line 104). So
"restart re-mints JWTs" is a daily operational hazard, not a
once-per-quarter incident.

Ripley caught the symptom in the docker-deploy-proof log
(`Container application log shows clean startup: …, JWT signing key
minted (dev fallback — documented), …`) and flagged it explicitly:
> "JWT signing key falls back to per-process random HMAC when
> `Authentication:JwtSigningKeys` unset — operators must set this
> for restart-survival (`docs/jwt-rotation.md`)"

## Approach chosen — Option B (fail-fast in Production)

Briefing offered two options:
* **A.** On Production, derive a stable key from the DB or
  persistent file. Log a warning.
* **B.** On Production, REFUSE to start without
  `Authentication:JwtSigningKeys`. Log a clear error with the
  required env-var format.

I picked **B** because:

1. **`docs/jwt-rotation.md §2` already specifies B:** "if
   `JwtSigningKeys` is empty OR if `JwtSigningKeys[0]` is shorter
   than 32 bytes (the HMAC-SHA256 minimum), Program.cs throws
   `InvalidOperationException` before the host starts listening —
   this is the same fail-fast shape as today's
   `Auth:JwtSigningKey` validator." This is a documented contract
   that was never wired; the right fix is to wire it, not to
   invent a third shape.

2. **Matches existing security posture** —
   `RotationCadenceValidator.Validate()` (Phase K W9, Bishop)
   already throws at startup when `JwksCacheTtl >
   RotationGracePeriod / 2`. A stable-but-implicit derivation
   (Option A) is weaker than what other prod-critical knobs in
   the same file already enforce.

3. **Stephen's deploy environment is recoverable.** He runs the
   container directly, sees the startup error, sets the env var,
   restarts. Compared to silent JWT invalidation on every restart
   — which manifests as "users keep getting logged out and don't
   know why" — a noisy startup error is dramatically more
   debuggable.

4. **Option A's derivation source is hostile.** The DB is the
   natural place to derive from, but:
   - SQLite is the default; the DB file lives on a `/data`
     volume that may NOT survive container recreation (depends on
     `docker run -v` mount strategy).
   - A derived-from-DB key changes if the operator wipes
     `/data/mahjong-autotable.db` (e.g. clean re-deploy from
     scratch), so it doesn't actually solve the restart-survival
     problem in the operationally common case.
   - A derived-from-DB-state-row key would need a separate
     pre-issue migration + a chicken-and-egg startup-ordering
     dance with EF Core. Option B is one DI flag.

## Implementation

### Source changes — production code

* **`Auth/JwtSigningKeyProvider.cs`** — added `requireOperatorKeys`
  constructor parameter (default `false` to keep all Phase-K-W4
  tests passing without modification). When `true` and no
  operator-provided HMAC keys present AND `Algorithm == "HS256"`,
  throws `InvalidOperationException(ProdRequiresOperatorHmacKeyMessage)`.
  Mirror guard for `RS256` + empty `JwtRsaKeys` →
  `ProdRequiresOperatorRsaKeyMessage`. Both messages are
  `public const` so tests + ops tooling can hard-assert against
  the canonical wording.

* **`Program.cs`** — eager construction of the provider (no
  factory lambda) so the `InvalidOperationException` fires at
  `WebApplication.Build()` time, not on the first JWT resolve
  (which would allow the listener to bind under a half-broken
  auth surface). Wired `builder.Environment.IsProduction()` →
  `requireOperatorKeys`.

* **`Program.cs` (incidental bug fix tightly coupled to my
  change)** — fixed a pre-existing precedence bug uncovered by
  the restart-survival shell-script proof on its FIRST run.
  `appsettings.json` ships `Auth:JwtSigningKeys: []` which
  `IConfiguration.GetSection("Auth:JwtSigningKeys").Get<string[]>()`
  materialises as a **non-null empty array**. The original
  `?? authOptions.JwtSigningKeys ?? Array.Empty<string>()` chain
  short-circuited on the non-null empty array and NEVER read
  `authOptions.JwtSigningKeys` (which is bound from the
  `Authentication:JwtSigningKeys` env-var path documented in
  `docs/jwt-rotation.md`). Effect: an operator setting
  `Authentication__JwtSigningKeys__0=<key>` was being SILENTLY
  IGNORED in production. The container failed to start with the
  Phase-L guard tripped, even when the env var was correctly set
  — making the fail-fast UNRECOVERABLE without setting the
  `Auth__JwtSigningKeys__0` (legacy section) path instead.
  Replaced both `JwtSigningKeys` and `JwtRsaKeys` precedence
  chains with a `FirstNonEmptyArray()` helper that prefers
  "non-null AND non-empty AND not-all-blank-entries". Same fix
  applied to `JwtRsaKeys`.

### Tests — `Auth/JwtProdHardeningTests.cs` (new, 10 facts)

| Trait | Assertion |
|-------|-----------|
| DEV + no keys (new ctor) | starts with ephemeral fallback, `UsingEphemeralFallbackKey == true` |
| DEV + no keys (back-compat ctor) | identical shape — back-compat overload preserved |
| PROD + no keys + HS256 | throws `InvalidOperationException` with `ProdRequiresOperatorHmacKeyMessage` verbatim |
| PROD + empty-string entries only | treated as no-keys, throws |
| PROD + `JwtSigningKeys[0]` | starts cleanly, kid stable |
| PROD + legacy singular `JwtSigningKey` | starts cleanly (back-compat preserved) |
| PROD + keys + functional pair | issuer mints, validator validates, kid round-trips |
| PROD + restart simulation | token minted by provider A validates under freshly-constructed provider B with SAME key → kids match |
| DEV + restart simulation | token minted by provider A does NOT validate under freshly-constructed provider B (different random keys) — regression guard documenting the original bug shape |
| PROD + RS256 + no RSA keys | throws `InvalidOperationException` with `ProdRequiresOperatorRsaKeyMessage` verbatim |

### Test-file fan-out (necessary, tightly coupled)

The Production fail-fast change required updating 9 existing test
files that build a Production-env `WebApplicationFactory` without
supplying JWT keys (would otherwise be broken by my change).
Surgical one-line `UseSetting("Auth:JwtSigningKeys:0", "test-prod-stable-jwt-key-…")`
addition per file, each with a `Phase L — Drake. Prod hardening:
see docs/jwt-rotation.md §7` comment. Files:
* `Auth/DevLoginTests.cs` (prod factory only)
* `Regression/RegressionHostFixture.cs`
* `Security/CdnCacheHeadersTests.cs`
* `Security/CspHeaderTests.cs` (2 factories)
* `Security/CspStrictStylesProductionConfigTests.cs` (guarded by env so dev path still tested)
* `Security/CspStyleSrcNoUnsafeInlineTests.cs`
* `Security/SecurityHeadersTests.cs`
* `RateLimiting/RateLimitingTests.cs`
* `Phase_K_W5/TestShimSanityTests.cs`

These tests pre-date my change but their factories had been
relying on the dev-fallback shape implicitly. Without these
edits, my change would have broken 20 cross-cutting prod-env
tests with the same `InvalidOperationException`. The edits stay
surgical (one line each) and the test intent is unchanged.

### Validation

* **Targeted suite:**
  `dotnet test ... --filter "FullyQualifiedName~Jwt|FullyQualifiedName~Auth|FullyQualifiedName~Signing"`
  → **507/507 PASS** (47 s).
* **Full suite:** 5332/5343 pass; 2 pre-existing skips; 11
  pre-existing `*_Memo_Present` failures (these check for agents'
  inbox memo files that don't exist on `origin/main` — verified
  pre-existing by stashing my change). My change introduced
  zero new failures.

### Live restart-survival proof

[`playtest-artifacts/jwt-restart-survival.sh`](../../playtest-artifacts/jwt-restart-survival.sh)
— pure-bash + openssl HMAC-SHA256 minter (header-level check, no
UI flow needed for this contract). The script:

1. Builds the image if absent.
2. Starts container A with
   `Authentication__JwtSigningKeys__0=<stable key>`.
3. Mints a JWT in bash using HMAC-SHA256 over the same key.
4. POSTs to `/api/auth/validate` against container A → asserts
   `valid:true` (HTTP 200).
5. `docker rm -f` + re-runs the image as container B with the
   SAME key.
6. POSTs the SAME token to `/api/auth/validate` against container
   B → asserts `valid:true` (HTTP 200).
7. Both kids match (`dTMKdVtuJFE`, deterministic per
   `JwtSigningKey.ComputeKid` SHA-256 truncation).

Exit code 0 on full pass; non-zero on any step failure.
CI-runnable as-is.

Negative-path proof: started the same image with `ASPNETCORE_ENVIRONMENT=Production`
AND NO `Authentication__JwtSigningKeys__0` env var → container
exits immediately with the canonical
`InvalidOperationException: Authentication:JwtSigningKeys is
required in Production but is empty. Set Authentication__JwtSigningKeys__0=…
See docs/jwt-rotation.md §1 + §7.` on the first stdout line.
Pre-Phase-L the same invocation would have started successfully
and minted JWTs that 401 on restart.

### Docs

* **`docs/jwt-rotation.md` §7.1 — new section.** "Phase L —
  Production fail-fast on missing operator keys (Drake)". Covers
  the contract, the operator-actionable error message verbatim,
  the required env-var format
  (`Authentication__JwtSigningKeys__0=$(openssl rand -base64 48)`),
  and points at the restart-survival shell script.
* **`README.md` Docker single-image deploy section** — added the
  `JWT_KEY="$(openssl rand -base64 48)"` minting step + the
  `Authentication__JwtSigningKeys__0` env-var to the verified
  `docker run` example, with a callout blockquote linking to
  `docs/jwt-rotation.md §7.1`. Two-line addition; rest of the
  section is unchanged.

## Lane discipline observed

Touched only:
* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtSigningKeyProvider.cs`
* `src/backend/src/Mahjong.Autotable.Api/Program.cs` (JWT-config
  block only; eager construction + precedence helper)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Auth/JwtProdHardeningTests.cs` (new)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Auth/DevLoginTests.cs` (1 line)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/RegressionHostFixture.cs` (1 line)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/CdnCacheHeadersTests.cs` (1 line)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/CspHeaderTests.cs` (2 lines)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/CspStrictStylesProductionConfigTests.cs` (1 line, env-guarded)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/CspStyleSrcNoUnsafeInlineTests.cs` (1 line)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Security/SecurityHeadersTests.cs` (1 line)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/RateLimiting/RateLimitingTests.cs` (1 line)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/TestShimSanityTests.cs` (1 line)
* `docs/jwt-rotation.md` (new §7.1)
* `README.md` (Docker section — 2-line surgical addition)
* `playtest-artifacts/jwt-restart-survival.sh` (new)
* `.squad/agents/drake/history.md` (this run's entry)
* `.squad/decisions/inbox/drake-jwt-hardening.md` (this memo)

Did NOT touch the Dockerfile (Apone's lane — works as-is, no
change needed), frontend (Hicks), bot / scoring (Frost),
Changsha runtime / WS dispatch (Bishop), persistence (Bishop /
prior Drake territory but unrelated to this fix), or any other
agent's production source.

## Forward-looking note

The 11 pre-existing `*_Memo_Present` test failures (e.g.
`Vasquez_W22_InboxMemo_Present`) check for memo files in
`.squad/decisions/inbox/` that don't exist on `origin/main`
(the inbox directory is gitignored + housekept). They are not
caused by my change and not in my lane. If the squad decides
those should hard-assert on green CI, the right fix is either
(a) commit a placeholder memo per wave, or (b) tag those tests
`Trait("Category", "Forward")` with a forward-soft-pass shape
matching the Phase-K-W5 `forward-compat` pattern (Vasquez's
lane).

---

📌 JWT signing-key production hardening — fail-fast in
Production + restart-survival proven end-to-end via Docker
image rebirth. Stephen's single-image deploy story is now
operationally complete.
