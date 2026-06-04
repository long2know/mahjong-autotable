# Drake — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend +
autotable-derived TS frontend (Parcel-bundled). Single-page mahjong
table with WS + SignalR transport, in-memory game runtime, EF Core
SQLite (dev) / Postgres (prod) / SqlServer (prod) persistence.

**User:** Stephen Long. Standing directives: (1) "No pauses — keep
iterating until 100% done done." (2) All agents use
`claude-opus-4.7-xhigh`.

**Joined:** 2026-05-27, as a backend hotfix engineer. Stephen brought
me in to handle a stray runtime exception while Bishop, Frost, and
Hicks were deep in the Changsha dealing-ceremony rework.

**Charter status:** None yet. Stephen has not committed to keeping me
on the roster — first task is a one-off, and a charter only gets
written if I stick around past it.

**Team context I should know (snapshot at join):**

- Bishop owns backend `Changsha/Runtime/**` + `Autotable/**`.
  Currently on `fix/walls-facedown-and-pickup-state-machine`.
- Frost owns `Changsha/Dealing/**` (new module), `Changsha/Bot/**`,
  `Changsha/Scoring/**`. Currently on `feat/changsha-dealing-ceremony`.
- Hicks owns the frontend.
- Vasquez owns the test infrastructure (`TestInfrastructure/**` —
  e.g. `PostgresTestDatabaseLifetime.cs`) and runs playtests.
- Apone owns DevOps / CI / Docker / observability.
- Scribe handles decisions.md merges and orchestration logs.
- Ripley / Ralph / Ferro / Hudson — other specialists already on the
  roster, dormant at the time I joined.

**Lane rules learned the first day:**

- Don't touch other agents' active branches even adjacently.
- The squad's flock pipeline lives at `.work/squad-git-lock`. Always
  branch from `origin/main`, never from another agent's branch.
- Memos go in `.squad/decisions/inbox/<agent>-<short-handle>.md` and
  are gitignored — force-add with `git add -f`.
- Agent history files live under `.squad/agents/<agent>/history.md`
  and ARE tracked (no `-f` needed in principle, but the brief
  asked for `-f` belt-and-braces so I followed instructions).

## First task — PlayerStats.LastGameAt nullable hotfix (2026-05-27)

**Commit authored:** _TBD — recorded after squash-merge_

**Symptom:** Runtime `SqliteException 19 — NOT NULL constraint failed:
PlayerStats.LastGameAt` on `POST /api/identity` against a dev SQLite
file that pre-dated Phase J Wave 5.

**Root cause:** `Data/DatabaseBootstrapper.cs:301` declared the
SQLite-only defensive bootstrap CREATE TABLE for `PlayerStats` with
`LastGameAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'`. The EF
model (`Players/PlayerStats.cs:18` → `public DateTime? LastGameAt`)
and every EF migration + model snapshot across SQLite / Postgres /
SqlServer say nullable. The hand-rolled bootstrap was the only thing
shadowing the model — and it only ran on dev SQLite files that pre-
date the migration set.

**Fix:** Surgical single-file change to
`Data/DatabaseBootstrapper.cs`:
1. Corrected the CREATE: `"LastGameAt" TEXT NULL`.
2. Added a defensive remediation pass: PRAGMA-introspect the
   `notnull` flag on `LastGameAt`; if `1`, rebuild the table with
   the SQLite-recommended pattern and remap the sentinel default
   back to `NULL`.

No EF migration changes (they were already correct). No model snapshot
changes (already correct). No entity-config changes (no fluent
`.IsRequired()` was set).

**Verified:**
- `dotnet build` 0 errors.
- `dotnet test … --filter PlayerStats|PlayerProfile|DatabaseBootstrap`
  — 11/11 pass.
- Full suite — 5219 pass; 2 flaky `Autotable/MultiGameRoutingTests`
  (Bishop's lane) re-passed 8/8 in isolation, flake attributable to
  a concurrent test runner from another agent racing on the shared
  test DB.
- Fresh-DB runtime smoke: `/health` + `POST /api/identity` 200 OK.
- Broken-DB remediation: hand-seeded the pre-fix schema, booted,
  confirmed table rebuilt + data preserved + sentinel default mapped
  back to NULL.

**Memo:** `.squad/decisions/inbox/drake-playerstats-lastgameat-fix.md`

**Lane discipline observed:** Did not touch any of
`Changsha/Runtime/**`, `Changsha/Dealing/**`, `Changsha/Bot/**`,
`Changsha/Scoring/**`, `Autotable/**`, frontend, workflows, or
`TestInfrastructure/**`. The only file changed in product code was
`src/backend/src/Mahjong.Autotable.Api/Data/DatabaseBootstrapper.cs`.

**Pattern locked in:** The defensive `EnsureSqlite…TablesAsync`
bootstrappers in `DatabaseBootstrapper.cs` are effectively a
hand-rolled migration chain for the SQLite provider. Any future
schema change covered by one of those helpers MUST update both the
canonical EF migration AND the hand-rolled SQL in lockstep, or this
exact class of bug recurs.

## Second task — PlayerProfiles.PlayerId UNIQUE race hotfix (2026-05-29)

**Commit authored:** `2df2e75` — `fix(persistence): PlayerProfiles.PlayerId UNIQUE race-safe upsert (squash)`

**Symptom:** Stephen hit
`CLR/Microsoft.EntityFrameworkCore.DbUpdateException` with innermost
`Microsoft.Data.Sqlite.SqliteException : SQLite Error 19: 'UNIQUE
constraint failed: PlayerProfiles.PlayerId'` during live play. Stack
started at `ReaderModificationCommandBatch.ExecuteAsync`.

**Root cause:** Classic SELECT-then-INSERT race in
`Players/PlayerProfileService.GetOrCreateAsync` (and the two sibling
`Update*Async` methods that share the same shape). Two concurrent
requests for the same persistent player id (POST `/api/identity` racing
the `ChangshaHub.OnConnectedAsync` "ensure profile on first connect"
call, or two browser tabs onboarding together) both saw
`FirstOrDefault → null` and both called `db.PlayerProfiles.Add`. The
losing `SaveChangesAsync` violated the unique PK.

**Fix:** Surgical single-file refactor of
`Players/PlayerProfileService.cs`:
1. New `UpsertProfileAsync(playerId, onCreate, onExisting, ct)` private
   helper — 2-attempt loop, fresh `IServiceScope` per attempt, catches
   `DbUpdateException` only when `IsUniqueViolation(ex)` is true and
   re-fetches the row the winning caller just committed.
2. New `IsUniqueViolation(DbUpdateException)` cross-provider predicate
   (SQLite errno 19, Postgres SqlState 23505, SqlServer Number 2627/2601)
   so the fix lands once and works on every provider this codebase ships
   against.
3. `GetOrCreateAsync`, `UpdateDisplayNameAsync`, `UpdateAvatarColorAsync`
   rewritten as thin shells over `UpsertProfileAsync`. Happy-path
   behaviour identical; only race semantics changed.
4. `GetStatsAsync` and `RecordGameCompletedAsync` left alone —
   different table (`PlayerStats`), not what Stephen hit.

No schema / migration / bootstrap changes — the schema was correct, the
bug was at the service layer.

**Regression test added:**
`PlayerProfileServiceTests.GetOrCreate_IsRaceSafe_WhenCalledConcurrently_WithSameId`
— 8 parallel `GetOrCreateAsync(samePlayerId)` via `Task.Run` +
`Task.WhenAll`. Verified to fail **with the exact exception Stephen
reported** when the production fix is stashed; passes deterministically
with the fix in place.

**Verified:**
- `dotnet build` — 0 errors, 0 warnings.
- `dotnet test … --filter PlayerProfile|PlayerIdentity|PersistentPlayerId`
  — 15/15 pass (4 pre-existing + 1 new race regression + 10 across the
  three sibling test classes).
- Fresh-DB runtime probe: backend booted on port 8088, `/health` 200,
  `POST /api/identity` (no cookie + same-cookie idempotent) 200.
- Race probe: **20 parallel `POST /api/identity` with the SAME
  fresh-but-never-created cookie value** — 53/53 POSTs across all
  probes returned 200; exactly **1 row** in PlayerProfiles + 1 in
  PlayerStats for the racy PID; EF logged 5/20 race losses, my retry
  loop recovered all 5; 0 unhandled exceptions.

**Memo:** `.squad/decisions/inbox/drake-playerprofiles-unique-fix.md`

**Lane discipline observed:** Touched only
`src/backend/src/Mahjong.Autotable.Api/Players/PlayerProfileService.cs`
(production) and `src/backend/tests/Mahjong.Autotable.Api.Tests/Players/PlayerProfileServiceTests.cs`
(test). Did NOT touch Changsha runtime, Bot / Scoring (Frost lane),
Autotable WS (Bishop lane), frontend (Hicks lane), test infrastructure
(Vasquez lane), or DevOps / CI (Apone lane).

**Cross-reference to first fix (`c369c54`):** Same exception class
(`SqliteException 19`), totally different root cause. Last time it was
a hand-rolled `CREATE TABLE` in `DatabaseBootstrapper` declaring
`LastGameAt NOT NULL` against an EF model that said nullable. This
time it was a service-layer race on a unique PK. **Pattern locked in:**
"`SqliteException 19`" can come from either a NOT-NULL violation OR a
UNIQUE/PK violation — always read the constraint name in the error
message before deciding which bug class it is. And always check BOTH
the EF migration chain AND the runtime write path when this error
class surfaces.

**Forward-looking note:** Every other "natural-key-PK upsert" in this
codebase (`MatchHistory(PlayerId, GameId)`, `PlayerSeasonStats(PlayerId,
Season)`, `TournamentParticipants(TournamentId, PlayerId)`, etc.) has
the same race shape. Not fixing them in this hotfix (surgical scope),
but flagging for whichever agent owns the next race report.

## Team updates

📌 **2026-06-01** — Broken-deal response: PlayerProfiles.PlayerId UNIQUE race hotfix — commit `2df2e75`.


## Third task — Persistence layer thorough audit (2026-06-03)

**Triggered by:** Stephen's directive "are you done? Have the team fan
out and thoroughly test the game and its functionality."

**Scope:** Verify the WHOLE persistence layer Drake owns is robust
against six scenarios: reconnect, multi-tab, 100-parallel concurrent
race, Hu-win persistence, reset-DB cold start, schema drift between
EF migrations vs hand-rolled SQLite bootstrap. Plus cross-provider
parity (SqlServer / Postgres / SQLite) for the `IsUniqueViolation`
predicate that backs the `2df2e75` upsert hotfix.

**Verdict:** PASS — both prior fixes (`c369c54` LastGameAt-nullable
and `2df2e75` PlayerProfiles unique-PK race) hold under every probe
Stephen named.

**Bugs found:** None blocking. One JSON-contract subtlety surfaced
(CreatedAt `Z` suffix dropped on race-loss refetch path — underlying
ticks identical, contract is "same playerId" not "same string", no
current consumer affected). Documented in the memo for the next
contract-stability pass; intentionally not fixed in this surgical
audit pass.

**Tests added (3 files, all in Players/):**
1. `IsUniqueViolationCrossProviderTests.cs` — 10 unit tests
   synthesizing a SQLite / Postgres / SqlServer provider exception
   via reflection, wrapped in `DbUpdateException`, asserting the
   predicate matches the canonical unique-violation codes for each
   provider and does NOT false-fire on FK / NOT-NULL / generic
   exceptions. Verifies the "fix lands once, works everywhere"
   claim of `2df2e75` without needing a live Postgres / SqlServer.
2. `PlayerTablesSchemaBootstrapTests.cs` — 4 integration tests on
   per-test fresh SQLite files. Walks every EF property on
   `PlayerProfile` + `PlayerStats` and asserts column existence,
   NOT-NULL bit matches `IsNullable`, PK matches `HasKey`, and the
   `PlayerStats → PlayerProfiles` FK has ON DELETE CASCADE. Hard-pin
   for `LastGameAt` nullability (c369c54 regression guard).
3. `PlayerProfileServiceTests.cs` +2: 50-parallel race tests (same id
   + distinct ids) — pin the upsert retry loop at xUnit fidelity to
   match the 100-parallel HTTP probe.

**Visibility change (only product-code edit):**
`PlayerProfileService.IsUniqueViolation` bumped from `private static`
→ `internal static` so the cross-provider parity tests can call it
directly. InternalsVisibleTo already wired in API csproj line 51.

**Validation:**
- `dotnet test ... --filter "Player|PersistentPlayer|Identity|Bootstrap|Schema"`
  → **207/207 PASS** (42s).
- Targeted breakdown: 10/10 IsUniqueViolation, 4/4 SchemaBootstrap,
  8/8 PlayerProfileService (was 6 + 2 new), 3/3 PlayerStatsAggregation,
  4/4 PersistentPlayerId.

**Live probes:**
- A1: 100 parallel POST /api/identity SAME cookie → 100/100 OK, 1 row.
- A2: 100 parallel POST /api/identity DISTINCT cookies → 100/100 OK.
- A3: 100 parallel POST /api/identity SAME never-seen cookie →
  100/100 OK, exactly 1 PlayerProfile + 1 PlayerStats row.
  Backend log: 2 race retries logged at Debug, recovered transparently.
- B: 3 sequential POSTs same cookie ("reconnect") → single PID,
  parsed CreatedAt identical, LastSeenAt strictly monotonic.
- C: 3 concurrent POSTs same NEW cookie ("multi-tab") → 3/3 OK,
  single PID, single parsed CreatedAt.
- C2: 3 concurrent SignalR `/hubs/changsha/negotiate` → 3/3 × 200.

**Memo:** `.squad/decisions/inbox/drake-persistence-thorough-audit.md`

**Lane discipline observed:** Touched only
`Players/PlayerProfileService.cs` (1-line visibility flip) and three
test files under `tests/.../Players/`. Did NOT touch frontend,
Changsha runtime, Bot / Scoring (Frost), Autotable WS (Bishop), test
infrastructure (Vasquez), DevOps / CI (Apone), AppDbContext.cs, or
DatabaseBootstrapper.cs — both prior fixes hold and no schema change
is needed.

**Forward-looking note:** The JSON DateTime contract drift (S2 in the
memo) and the sibling natural-key-PK tables (`MatchHistory`,
`PlayerSeasonStats`, `TournamentParticipants`,
`PlayerOnboardingStatuses`) that still have the SELECT-then-INSERT
race shape can re-use the now-`internal` `IsUniqueViolation` helper
verbatim. If a second consumer lands, lift to a
`Persistence/UniqueViolationDetector.cs` static helper.

📌 Persistence thorough audit (2026-06-03): 100-parallel race-safe, schema drift detection, cross-provider parity — committed `67be128`.

## JWT signing-key production hardening (2026-06-04)

**Commit authored:** _TBD — recorded after squash-merge_

**Briefing:** Ripley's Docker-deploy-proof wave
(`ripley-docker-deploy-proof.md`, `ab34d09`) flagged ONE remaining
production blocker: JWT signing key falls back to per-process random
HMAC when `Authentication:JwtSigningKeys` unset. Container restart
silently invalidates every prior JWT. Stephen runs the canonical
image on his own Linux server — daily-hazard, not quarterly-risk.

**Approach chosen: Option B — fail-fast in Production.** Rationale:
docs/jwt-rotation.md §2 already specified this shape ("Program.cs
throws InvalidOperationException before the host starts listening")
but it was never implemented. Matches the existing
`RotationCadenceValidator` security posture (W9 Bishop).

**Implementation:**

1. `JwtSigningKeyProvider` — added `requireOperatorKeys` ctor flag.
   When `true` AND `_algorithm == "HS256"` AND no operator-provided
   HMAC keys, throws `InvalidOperationException` with the canonical
   `ProdRequiresOperatorHmacKeyMessage` literal (exposed as a public
   constant so tests + ops tooling can hard-assert against the
   wording). Mirror guard for `RS256` + empty `JwtRsaKeys`. Back-
   compat constructor overload preserved so 0 existing W4 tests need
   to change to declare the dev-fallback shape.

2. `Program.cs` — eager construction (no factory lambda) so the
   `InvalidOperationException` fires at boot, not on first JWT
   resolve. Wired `builder.Environment.IsProduction()` →
   `requireOperatorKeys`.

3. `Program.cs` — fixed a pre-existing precedence bug uncovered by
   the restart-survival proof: `appsettings.json` ships
   `Auth:JwtSigningKeys: []` which `.Get<string[]>()` materialises
   as a NON-NULL empty array, short-circuiting the `??` chain and
   ignoring `Authentication__JwtSigningKeys__N` env vars entirely.
   Replaced with a `FirstNonEmptyArray()` helper that prefers
   non-null AND non-empty AND not-all-blank-entries. Same fix
   applied to `JwtRsaKeys`. Without this, Stephen's
   `Authentication__JwtSigningKeys__0=<key>` would have been a
   silent no-op and the fail-fast would have been unrecoverable —
   the restart-survival shell-script caught this on first run.

**Tests:**
`tests/Mahjong.Autotable.Api.Tests/Auth/JwtProdHardeningTests.cs`
(new, 10 facts):
- `Dev_NoOperatorKeys_StartsWithEphemeralFallback`
- `Dev_NoOperatorKeys_BackCompatCtor_StartsWithEphemeralFallback`
- `Prod_NoOperatorKeys_HS256_Throws_WithOperatorActionableMessage`
- `Prod_EmptyStringEntries_StillTreatedAsNoOperatorKeys_Throws`
- `Prod_WithJwtSigningKeysArray_StartsCleanly`
- `Prod_WithLegacySingularJwtSigningKey_StartsCleanly`
- `Prod_WithJwtSigningKeysArray_SignsAndValidatesJwts`
- `Prod_TokenIssuedThenRebound_SurvivesRestartWithSameKey`
- `Dev_TokenIssuedThenRebound_DoesNotSurviveRestart_ProvesProblem`
  (regression-guard documenting the original bug shape)
- `Prod_Rs256_NoRsaKeys_Throws_WithOperatorActionableMessage`

**Validation:**
- `dotnet test ... --filter "Jwt|Auth|Signing"` → **507/507 PASS** (47s).
- Full suite (`dotnet test ...`): 5332/5343 pass (2 skipped, 11
  pre-existing `*_Memo_Present` failures — these check for
  agents' inbox memo files that don't exist on `origin/main`;
  unrelated to my change, verified by stashing).

**Test-file coupling (necessary fan-out):** the prod fail-fast
change required updating existing tests that build a Production-env
`WebApplicationFactory` without supplying keys (would otherwise
be broken by my change). Surgical one-line `UseSetting` addition
per file, with a comment pointing at `docs/jwt-rotation.md §7`:
- `Auth/DevLoginTests.cs`
- `Regression/RegressionHostFixture.cs`
- `Security/CdnCacheHeadersTests.cs`
- `Security/CspHeaderTests.cs` (2 factories)
- `Security/CspStrictStylesProductionConfigTests.cs` (guarded by env)
- `Security/CspStyleSrcNoUnsafeInlineTests.cs`
- `Security/SecurityHeadersTests.cs`
- `RateLimiting/RateLimitingTests.cs`
- `Phase_K_W5/TestShimSanityTests.cs`

I used `Auth:JwtSigningKeys:0` (legacy section) in the tests
because Program.cs reads it first; both shapes bind the same
provider in production (operators set `Authentication__JwtSigningKeys__N`
per the docs and ESO conventions — both work after the precedence fix).

**Live restart-survival proof:**
`playtest-artifacts/jwt-restart-survival.sh` (bash + openssl
HMAC-SHA256 minter; no UI flow). Builds image, runs container A
with stable key, mints JWT, validates → 200 valid:true,
`docker rm -f` + re-runs container B with SAME key, re-validates
SAME token → 200 valid:true. Both kids match
(`dTMKdVtuJFE` deterministic from the SHA-256 truncation in
`JwtSigningKey.ComputeKid`). Also exercised the negative path
(prod env, no key) → container exits with the canonical
`InvalidOperationException` message on the first stdout line.

**Docs:**
- `docs/jwt-rotation.md` §7.1 new — "Phase L — Production
  fail-fast on missing operator keys (Drake)". Documents the
  contract, the operator-actionable error message verbatim, the
  required env-var format, and points at the restart-survival
  shell script.
- `README.md` Docker section — added the `JWT_KEY="$(openssl rand
  -base64 48)"` minting step and the
  `Authentication__JwtSigningKeys__0` env-var to the verified
  `docker run` example, plus a callout blockquote linking to
  `docs/jwt-rotation.md §7.1`.

**Memo:** `.squad/decisions/inbox/drake-jwt-hardening.md`

**Lane discipline observed:** Touched only auth-area source
(`Auth/JwtSigningKeyProvider.cs` + the JWT-config block of
`Program.cs`), JWT-area tests (new `Auth/JwtProdHardeningTests.cs`
plus one-line fan-out to the prod-env factory tests listed
above), `docs/jwt-rotation.md`, `README.md` Docker section, and
the playtest-artifacts shell script. Did NOT touch frontend,
Changsha runtime, Bishop's WS dispatch, Frost's bot / scoring,
Persistence layer, Apone's Dockerfile, or any other agent's
production source.

📌 JWT signing-key prod hardening (2026-06-04): fail-fast in Production + restart-survival proven end-to-end — committed `385e7fc`. Merged to squad decision: Production-Ready Wave (2026-06-04).
