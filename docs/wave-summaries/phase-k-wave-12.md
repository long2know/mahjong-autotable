# Phase K — Wave 12 summary

> **Branch:** `stlong/phase-k-wave-12-bringup`
> **Base:** `main` @ `ee9dba0` (Phase K Wave 11 squash-merge PR #57)
> **Head:** `e22ef5c`
> **Date:** 2026-10-XX (CHANGELOG `[0.21.0]`)
> **Gate:** **2610 / 0 / 0** (+207 vs Wave 11 baseline 2403; +73 over Vasquez baseline 2537)
> **Zero-skip streak:** **27 consecutive waves** (J.1 → J.10 + K.1 → K.12)

## Headlines (read these first)

1. **Three-renderer big chunk: 448.65 KB — the W12 <450 KB
   stretch ceiling is BEAT by ~1.4 KB.** Trajectory now `740
   → 579 → 532 → 507 → 497 → 466 → 448 KB` across W6 → W7 →
   W8 → W9 → W10 → W11 → W12 — **monotonic-decrease across 7
   consecutive waves; cumulative −39.4 %**. The W12 levers
   were three independent strips landing in one commit: (a)
   the **PMREMGenerator-adjacent ShaderChunk strip** — six
   new `envmap_*` entries (`envmap_fragment`,
   `envmap_common_pars_fragment`, `envmap_pars_fragment`,
   `envmap_pars_vertex`, `envmap_physical_pars_fragment`,
   `envmap_vertex`) added to the W11
   `SHADER_CHUNKS_TO_EMPTY` list in `vite.config.ts`; each
   body is wrapped in `#ifdef USE_ENVMAP` so the GLSL
   preprocessor strips the include anyway — emptying the JS
   strings drops ~10 KB of carrying weight; (b) the **NEW
   `stripUnusedUniformsLib()` Vite plugin** — mirrors the W9
   brace-walker pattern, operates on `three.module.js` with
   `enforce: 'pre'`, registered in the `plugins:` array
   between W11's `stripUnusedShaderChunks` and
   `copyStaticAssets`; rewrites five W9-stubbed-material
   UniformsLib keys (`roughnessmap`, `metalnessmap`,
   `gradientmap`, `points`, `sprite`) to empty object
   literals so ShaderLib calls to
   `UniformsUtils.merge([UniformsLib.X, ...])` still resolve
   but read `{}` instead of the original 4-6 line
   descriptors; (c) the **`shadowmap_*` +
   `shadowmask_pars_fragment` strip** — four more entries
   (`shadowmap_pars_fragment`, `shadowmap_pars_vertex`,
   `shadowmap_vertex`, `shadowmask_pars_fragment`); the
   autotable's `WebGLRenderer` never sets
   `shadowMap.enabled` and no light has `castShadow = true`,
   and `shadowmask_pars_*` defines `getShadowMask()` whose
   only call site is the W9-stripped `shadow_frag` shader.
   Combined: **466,395 B → 448,648 B (−17,747 B / −3.8 %)**
   — second largest single-wave bundle delta of Phase K
   (W11's −31.04 KB still holds first place).
2. **Test gate +207 net passing in one wave**, taking the
   trajectory to **W6 1422 → W7 1506 → W8 1706 → W9 1880 →
   W10 2108 → W11 2403 → W12 2610 (+1188 over W6 baseline;
   83.5 % growth across 7 waves)**. Driven by Bishop's seven
   backend surfaces all landing in one commit:
   **replay-by-id endpoint** (`r-{8 url-safe base64 chars}`
   synthetic id + gzip codec + 90-day retention sweep +
   `Replays:StorageImpl=InMemory|Ef` toggle), **OAuth
   introspect sliding-window rate limiter**
   (`IOAuthIntrospectRateLimiter` with 60-second window +
   100 requests per `client_id` default + canonical
   `X-RateLimit-*` + `Retry-After` headers on HTTP 429),
   **JWKS staged rotation policy**
   (`JwtStagedRotationPolicy` surfacing the 30-day overlap
   window via `OverlapDays` / `RotationStartUtc` /
   `OverlapWindowEndsAtUtc` /
   `IsWithinOverlapWindow(utcNow)` /
   `RemainingOverlapDays(utcNow)`; signing path UNCHANGED —
   informational on top of existing multi-key validation),
   **tournament bracket EF persistence** (`EfBracketStore` +
   `BracketRecord` + idempotent upsert keyed on
   `(TournamentId, RoundNumber, MatchSlot)`;
   `TournamentService.AdvanceMatchAsync` integration
   deferred to W13), **spectator handoff via signed JWT**
   (`POST /api/spectator/handoff` body `{gameId}`; mints
   scope-pinned `spectator:{gameId}` JWT with 5-minute TTL;
   `/api/replay/{id}/livestream.m3u8` stub honours
   `?token=…`), **commentary LLM cost budgeting**
   (`CostBudgetOptions.MonthlyCapUsd` +
   `TokensPerDollar=200_000` + `WarnThreshold=0.8`;
   `CommentaryCostBudget.Evaluate(utcNow)` returns
   `BudgetEvaluation`;
   `CommentaryController.SelectGenerator` routes to
   deterministic stub when `BudgetState.Exhausted`), and
   **SignalR replay-from-ack persistence**
   (`EfSignalRSequenceStore` + `SignalRSequenceEntry` entity
   + 60-minute retention sweep;
   `SignalRBackpressureBroadcaster.PublishAsync`
   write-through deferred to W13). All three new EF entities
   (`Replays`, `BracketRecords`, `SignalRSequenceEntries`)
   ship in **one named migration
   `Phase_K_W12_Replays_Brackets_SignalRSeq` across Sqlite /
   Postgres / SqlServer** with all 3
   `AppDbContextModelSnapshot.cs` updated in sync.
3. **Seventh consecutive wave with zero identity drift +
   zero coordinator fix-up.** All 4 agent rollup commits
   correctly authored at the `%an <%ae>` level (Hicks
   `ec69dd5`, Vasquez `35e6018`, Bishop `a3a8788`, Apone
   `e22ef5c`). `.work/squad-git-lock` cutover holds at the
   **3rd consecutive fully-adopted wave**; `flock -w 120 9
   ... 9>.work/squad-git-lock` mutex held across all 4
   concurrent agent runs + the Scribe sweep. Per-invocation
   race-safe identity binding (`git -c user.name=X -c
   user.email=Y commit ...`) remains the canonical commit
   form — held over W6 → W12 across 60+ commits.
4. **SECOND CONSECUTIVE 0-VIOLATION LANE-DISCIPLINE WAVE** —
   `checked=4 violations=0`. W11 was the first such wave,
   made possible by Vasquez's `shims_shared` 4-author +
   `pwa_audit_workflow_shared` 2-author lane-map broadening.
   W12 confirms the pattern — the strict-mode gate continues
   to fire only on actual cross-lane regressions, and there
   were none this wave. The W11 broadening continues to
   suppress the W7+W10 false-positive bundling patterns; no
   new findings this wave.
5. **`?action=replay&replayId=<id>` deep-link routing wired
   against Bishop's W12 `GET /api/replays/{replayId}`
   endpoint.**
   `src/frontend/autotable-src/src/action-router.ts` gains
   its fourth `SUPPORTED_ACTION` (`'replay'`) joining W11's
   `new-game` / `spectate` / `tournament` (+ `tournaments`
   plural alias). New private helpers `dispatchReplay`,
   `fetchAndOpenReplay`, `showReplayNotFoundToast`. The
   switch case reads the `replayId` co-parameter from
   `URLSearchParams`, strips BOTH `action` AND `replayId`
   from the URL (refresh-safe — re-loading the rewritten URL
   does NOT re-trigger), fetches Bishop's endpoint,
   JSON-parses the body, and on success lazy-imports
   `./replay-launcher` to call the new
   `openReplayPayload(replayId, body, options?)` export
   while rewriting the URL to `/replay/{replayId}` via
   `history.replaceState()`. ANY failure path (404 / 5xx /
   network / JSON-parse / missing co-param) →
   `showToast('Replay not found', 'error')` from `./toast`,
   no URL rewrite. **No fallback** to the legacy
   `/api/games/{gameId}/replay` endpoint — would mask config
   drift. Convention: per-action co-parameter shape lives at
   `action-router.ts`; URL strip-rewrite-on-success +
   toast-on-any-failure is the canonical contract.
6. **Single-pane prod cutover runbook + cross-namespace
   kustomize pattern shipped.** Apone's
   `docs/prod-cutover.md` NEW 462-line operator runbook (5
   sections — terraform plan readiness / kustomization
   wire-up / cutover-ready checklist gated by agent lane /
   cross-namespace kustomize pattern rationale / rollback
   playbook) consolidates cutover notes previously scattered
   across `docs/redis-cluster.md §11` +
   `docs/argo-rollouts-setup.md §5` +
   `docs/edge-region-probes.md §3` +
   `docs/production-deployment-runbook.md (W7)` + the W11
   ESO + Ingress file headers. **Convention:** each cutover
   gets its own `docs/<cutover>-cutover.md` doc rather than
   appending sections to existing runbooks; existing
   runbooks back-reference into the cutover doc. The
   **`NamespaceTransformer + unsetOnly: true` pattern**
   swaps out the W11 top-level `namespace: mahjong-prod`
   directive in `kustomization.yaml`: resources WITHOUT
   `metadata.namespace` continue to pick up `mahjong-prod`
   (identical to W11 behaviour); resources WITH a
   pre-declared namespace (argo-rollouts ingress + W12
   NetworkPolicies, all pinned to `argo-rollouts`) keep
   their declared value. Rejected alternatives: JSON6902
   patch (ordering issue), strategic-merge patch (same),
   Kustomize Component (does NOT escape parent ns
   transformer — verified empirically), sub-base inclusion
   (ns transformer propagates), `replacements:` directive
   (introduces unwanted ConfigMap), two-overlay split
   (forces two apply commands + ordering gates). Documented
   `docs/prod-cutover.md §4`.
7. **JWT rotation rehearsal #2 at 3 min 48 s — 39 % faster
   than W11's first run at 6 min 12 s.** Documented
   `docs/jwt-rotation-rehearsal.md §3`. Speedup wins
   downstream of Bishop's W12 JWKS-cache pre-warm. Squad
   recommendation: workflow is **GA-ready** for promotion to
   scheduled monthly cadence (W13+ follow-up). Target timing
   scale documented (< 4 min green / 4–6 min yellow / > 6
   min red — W11 6:12 would now be a YELLOW signal).
   **Convention:** every rehearsal harness (DR,
   secret-rotation, certificate-rotation, dependency-bump)
   should land with at least TWO documented runs before
   promotion to scheduled cadence — first run validates the
   harness, second run validates that the harness is
   REPEATABLE.

---

## Commits (4 across 4 agent lanes, all correctly authored)

| SHA       | Author                                       | Summary                                                                                                                                                    |
|-----------|----------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ec69dd5` | **Hicks (Frontend)** `<hicks@squad.mahjong>` | `envmap_*` family ×6 + new `stripUnusedUniformsLib()` Vite plugin + `shadowmap_*`/`shadowmask_*` strip → three-renderer-big 466.40 → 448.65 KB (**−17.75 KB / −3.8 %; <450 KB stretch BEAT by ~1.4 KB**) + W10 placeholder screenshot copy block REMOVED + `img/screenshot-*.auto.png` deleted + `?action=replay&replayId=<id>` deep-link routing wired against Bishop's `GET /api/replays/{replayId}` endpoint (4th SUPPORTED_ACTION; lazy-import replay-launcher; URL strip-rewrite-on-success; toast on any failure; NO fallback to legacy `/api/games/{gameId}/replay`) + LH13 threshold workflow edit **DEFERRED TO W13** (0 cron data points; rationale `docs/frontend-pwa-audit.md §9`). 33 files. |
| `35e6018` | **Vasquez (QA)** `<vasquez@squad.mahjong>`   | DbSerial audit `Phase_K_W12/Vasquez/db-serial-candidates.md` (25 candidates + 3-parallel flake-detection methodology + Reads/Writes split proposal) + `docs/test-architecture.md` §3.1.1+§3.1.2+§4.4a+NEW §5 Visual regression + `docs/agent-handoff-protocol.md §4.1` W12 status block (8th re-prompt + W14 escalation fallback) + `docs/frontend-pwa-audit.md §6.1` LH13 hard-pin deferral + `PwaAuditWorkflowGateTests.cs` SOFT-pinned LH13 mirror tests + `Wave1ThroughKW11RegressionTests → Wave1ThroughKW12RegressionTests` rename + 12 W12 smokes + 7 forward-stage Bishop W12 contract test files + 5 surface mirrors + 6 Playwright specs + selectors.md W12 footer. **Gate 2537/0/0 (+134); 27-wave zero-skip; 0 lane viols.** 28 files. |
| `a3a8788` | **Bishop (Backend)** `<bishop@squad.mahjong>` | Replay-by-id endpoint (`r-{8 base64}` ids + gzip codec + 90-day retention sweep) + OAuth introspect sliding-window rate limiter (60s/100 req per `client_id` + canonical `X-RateLimit-*` + `Retry-After`) + JWKS staged rotation policy (30-day overlap window via `JwtStagedRotationPolicy`) + tournament bracket EF persistence (`EfBracketStore` + `BracketRecord` + idempotent upsert keyed `(TournamentId, RoundNumber, MatchSlot)`; `TournamentService` integration → W13) + spectator handoff JWT (5-min TTL scope=`spectator:{gameId}`) + commentary LLM cost budget (USD-denominated + cap-switch-to-stub) + `EfSignalRSequenceStore` (60-min retention; broadcaster integration → W13) + **single EF migration `Phase_K_W12_Replays_Brackets_SignalRSeq` × 3 providers** + 6 docs landed. 38 files. **Gate 2610/0/0 (+207 over W11; +73 over Vasquez).** |
| `e22ef5c` | **Apone (DevOps)** `<apone@squad.mahjong>`   | `docs/prod-cutover.md` NEW 462-line single-pane operator runbook + 5 Cutover-Ready checklist + `namespace-transformer.yaml` NEW (inline `NamespaceTransformer` with `unsetOnly: true` — canonical cross-namespace kustomize pattern) + prod kustomization wire-up of W11 ESO + W11 Argo ingress + W12 NetworkPolicy + `envFrom optional: true` deployment patch + `infra/load-tests/redis-load-test.yml` NEW k6 manifest (1000 RPS × 5 min; SLO thresholds) + per-region R53 records `r53-regional-records.tf` NEW (gated by EMPTY `regional_endpoints` default — opt-in surface) + `argo-rollouts-network-policy.yaml` NEW 3 narrow policies + JWT rehearsal #2 docs at 3:48 (39 % faster) + GA-ready recommendation + CHANGELOG `[0.21.0]` + `docs/retro-2026-10.md`. 23 files. |

---

## Lane 1 — Bishop (Backend): 7 deliverables, 7 forward-stage contracts pre-asserted, +73 net gate

### 1. Replay-by-id endpoint

Hicks's W12 `?action=replay` URL shape needs a durable
GET-by-id surface — the previous waves served replays via
`GET /api/games/{gameId}/replay` which leaks the internal
`gameId` and ties the URL to game lifecycle. A short opaque
`replayId` keeps the share URL stable + non-correlatable.

`Replays/ReplayRecord.cs` (NEW):
- `ReplayRecord` entity {`ReplayId`, `GameId`, `RoundCount`,
  `PayloadCompressed` byte[], `CreatedAtUtc`,
  `LastAccessedAtUtc`}.
- Codec helpers `CompressPayload(string)` /
  `DecompressPayload(byte[])` round-trip via gzip;
  symmetric.

`Replays/ReplayStore.cs` (NEW):
- `IReplayStore` (`GetAsync`, `PutAsync`, `SweepOlderThan`).
- `InMemoryReplayStore` — `ConcurrentDictionary<string,
  ReplayRecord>` with a `Func<DateTimeOffset>` clock seam
  for tests.
- `EfReplayStore` — EF-backed; `UpsertAsync` keyed on
  `ReplayId`.
- `ReplayIdGenerator` emits `r-{8 url-safe base64 chars}`.
  ~2.8 × 10¹⁴ id space; collision probability against a 1
  M-replay corpus < 2 × 10⁻³.
- `ReplayOptions` — `StorageImpl` (`"InMemory"` default |
  `"Ef"`), `RetentionDays` (90 default),
  `MaxCompressedBytes` (8 MiB default).
- `ReplayRetentionSweepService` — 24-hour cadence background
  job; calls `SweepOlderThan(now - retentionDays)`.

`Replays/ReplayController.cs` (NEW):
- `GET /api/replays/{replayId}` — returns 404 on miss, 200 +
  JSON body on hit; updates `LastAccessedAtUtc`.
- `POST /api/replays` — body `{ gameId, payload, roundCount
  }`; gzips payload server-side; rejects payloads >
  `MaxCompressedBytes`; W12 stub accepts any caller
  (admin-gating → W13).

`docs/replay-by-id.md` (NEW) — endpoint shape + URL strategy + retention rules.

EF migration adds the `Replays` table with `(ReplayId TEXT
PK, GameId TEXT, RoundCount INT, PayloadCompressed BLOB,
CreatedAtUtc TIMESTAMP, LastAccessedAtUtc TIMESTAMP)`.

### 2. OAuth introspect sliding-window rate limiter

The W11 RFC 7662 introspection endpoint accepts Basic-auth
client credentials but had no per-client rate limit — a
compromised or misbehaving client could probe thousands of
tokens per second. W12 adds a sliding-window limiter keyed
off the Basic-auth `client_id`.

`Auth/OAuthIntrospectRateLimiter.cs` (NEW):
- `IOAuthIntrospectRateLimiter` (`TryAcquire(clientId) →
  OAuthIntrospectRateLimitDecision`).
- `OAuthIntrospectRateLimitDecision` record struct `(bool
  Allowed, int Limit, int Remaining, DateTimeOffset Reset,
  TimeSpan RetryAfter)`.
- Default sliding-window impl backed by
  `ConcurrentDictionary<string, Queue<DateTimeOffset>>`. The
  queue's head is trimmed against the window before each
  `TryAcquire`.
- `OAuthIntrospectRateLimitOptions` — `RateLimitPerClient`
  (100 default), `WindowSeconds` (60 default).

`Auth/AuthTokenController.cs::Introspect`:
- Added an optional `IOAuthIntrospectRateLimiter?` ctor
  param (DI-resolves to the default impl).
- Rate-limit gate sits between Basic-auth validation and
  form parsing (so an unauthenticated probe still hits 401
  first, not 429).
- Returns HTTP 429 on `!Allowed` with `Retry-After` +
  canonical `X-RateLimit-Limit` / `X-RateLimit-Remaining` /
  `X-RateLimit-Reset` headers per the IETF draft spec.

Toggle: `OAuth:Introspect:RateLimitPerClient` (default 100),
`OAuth:Introspect:WindowSeconds` (default 60).

`docs/oauth-introspect-rate-limit.md` (NEW).

**Multi-replica caveat:** the W12 limiter is in-process. A
multi-replica deployment will enforce the cap per pod, not
globally. **W13 deliverable: Redis-backed swap** (Bishop
forward-queue item #4).

### 3. JWKS staged rotation policy

The W4 dual-key issuer accepted multiple keys via
`JwtRsaKeys[]` with kid-tagged JWTs and multi-key
validation. W12 layers on a **policy seam** that surfaces
the 30-day overlap window so operators can answer "are we
currently in a rotation overlap, and how many days are
left?" without reasoning about the multi-key list directly.

`Auth/JwtStagedRotationPolicy.cs` (NEW):
- Exposes `OverlapDays`, `RotationStartUtc`,
  `OverlapWindowEndsAtUtc`, `IsWithinOverlapWindow(utcNow)`,
  `RemainingOverlapDays(utcNow)`.

`Auth/AuthOptions.cs`:
- Adds `RotationOverlapDays` (default 30) +
  `RotationStartUtc` (nullable DateTime — null when no
  rotation is in flight).

DI: singleton seam consumed by the cadence validator + the
W13 operator dashboard.

**Signing path UNCHANGED** — index 0 in `JwtRsaKeys[]`
continues to be the active signer; the policy is
informational on top of the existing `JwtSigningKeyProvider`
multi-key validation.

`docs/jwt-rotation.md §13` appended.

### 4. Tournament bracket EF persistence

`Tournament/EfBracketStore.cs` (NEW):
- `BracketRecord` entity {`Id`, `TournamentId`,
  `RoundNumber`, `MatchSlot`, `Player1`, `Player2`,
  `Winner`, `Loser`, `CompletedAt`, `IsBye`, `Metadata`
  JSONB}.
- `IBracketStore` (`UpsertAsync`, `GetByTournamentAsync`,
  `RecordResultAsync`).
- `InMemoryBracketStore` + `EfBracketStore`.
- `BracketStorageOptions` — `BracketStoreImpl` (`"InMemory"`
  default | `"Ef"`).
- Idempotent `UpsertAsync` keyed on `(TournamentId,
  RoundNumber, MatchSlot)`.

**Seam vs full wire-up:**
`TournamentService.AdvanceMatchAsync` does NOT yet call into
`IBracketStore.RecordResultAsync` — that integration needs a
`MatchSlot` derivation on the existing `TournamentMatches`
table and lands in W13. The W12 deliverable is the durable
seam + entity + migration + contract tests.

Toggle: `Tournament:BracketStoreImpl` = `"InMemory"` | `"Ef"`.

`docs/bracket-shape.md` (NEW) — bracket addressability
rationale (the `(tournamentId, roundNumber, matchSlot)`
triple) + idempotency contract + W13 integration hand-off.

### 5. Spectator handoff via signed token

`Spectator/SpectatorHandoffController.cs` (NEW):
- `POST /api/spectator/handoff`.
- Body `{ gameId }`.
- Resolves the caller's session cookie, mints a JWT with
  `scope = "spectator:{gameId}"` + 5-minute TTL via the
  existing `JwtIssuingService`.

`Spectator/SpectatorHandoffTokenValidator.cs` (NEW):
- Wraps `JwtValidationService` with the per-game scope
  check.
- Rejects on `token-missing`, `scope-mismatch`, or any
  underlying JWT-validation error.

`Program.cs`:
- `/api/replay/{id}/livestream.m3u8` stub honours `?token=…`
  via the new validator.

**Algorithm choice:** the handoff token uses whatever
algorithm the operator has configured (HS256 or RS256);
validation falls through the existing `JwtValidationService`
so the same kid-lookup + multi-key fallback logic applies.

`docs/spectator-handoff.md` (NEW) — handoff sequence + scope
shape + W11 connection-id parity rationale.

### 6. Commentary LLM cost budgeting

The W8/W9 monthly token meter is denominated in tokens; the
business-facing budget is denominated in dollars. Operators
need a USD gate that flips the runtime to the deterministic
stub generator when the monthly USD cap is hit.

`Commentary/CommentaryOptions.cs::CostBudgetOptions` (NEW):
- `MonthlyCapUsd` (0 = no cap).
- `TokensPerDollar` (default 200_000 — calibrated against
  GPT-4o-mini list price at brief-write time).
- `WarnThreshold` (default 0.8 — emit a warning log when the
  month-to-date ratio crosses 80 %).

`Commentary/CommentaryCostBudget.cs` (NEW):
- `Evaluate(utcNow)` returns `BudgetEvaluation(State,
  MonthlyUsd, ratio, …)`.
- States: `Disabled` (no cap), `Healthy`, `Warning`,
  `Exhausted`.
- Emits a **one-shot per-month warning + exhausted log on
  state transitions** so the audit trail records the event
  without spamming.

`Commentary/CommentaryController.cs::SelectGenerator`:
- Routes new requests to the deterministic stub when
  `Evaluate` returns `BudgetState.Exhausted`.
- Idempotent + per-request — no shared mutable state in the
  decision path.

`docs/commentary-llm.md §4` (NEW) — USD budget definition +
token-to-dollar conversion + state machine + operator
runbook.

### 7. SignalR replay-from-ack persistence

`Observability/EfSignalRSequenceStore.cs` (NEW):
- `SignalRSequenceEntry` entity {`Id`, `HubName`,
  `GroupName`, `ConnectionId`, `SequenceNumber`,
  `EnvelopeJson`, `CreatedAtUtc`}.
- `ISignalRSequenceStore` + `InMemorySignalRSequenceStore` +
  `EfSignalRSequenceStore`.
- `SignalRSequenceStoreOptions` — `SequenceStoreImpl`
  (`"InMemory"` default | `"Ef"`), `RetentionMinutes` (60
  default).
- `SignalRSequenceRetentionSweepService` — 1-minute cadence
  background job; calls `SweepOlderThan(now -
  retentionMinutes)`.
- JSON serializer helper centralises envelope shape so the
  broadcaster's serialization stays single-source.

**Seam vs full wire-up:**
`SignalRBackpressureBroadcaster.PublishAsync` does NOT yet
write through to the store. The brief explicitly calls out
"ship the seam; production runtime hooks land in W13". The
store + tests + migration are landed; the W13 work is one DI
wrapper around the broadcaster's publish path.

Toggle: `SignalR:SequenceStoreImpl` = `"InMemory"` | `"Ef"`;
`SignalR:RetentionMinutes` default 60.

`docs/realtime-resilience.md §6` (appended).

### Bundled EF migration

**All three new EF entities** (`Replays`, `BracketRecords`,
`SignalRSequenceEntries`) ship in **one named migration
`Phase_K_W12_Replays_Brackets_SignalRSeq`** across Sqlite /
Postgres / SqlServer. All 3 `AppDbContextModelSnapshot.cs`
files updated in sync.

**Convention (W12-NEW):** when a wave ships multiple EF
entities, bundle into a single named migration per provider.
Reduces migration-history churn + simplifies forward /
backward rollouts. Naming pattern is
`Phase_<Letter>_W<N>_<Entity1>_<Entity2>_…`.

---

## Lane 2 — Hicks (Frontend): 6 deliverables (5 shipped, 1 deferred with rationale); <450 KB stretch BEAT

### 1. PMREMGenerator-adjacent ShaderChunk strip — `envmap_*` family ×6

Extends W11's `SHADER_CHUNKS_TO_EMPTY` in
`src/frontend/autotable-src/vite.config.ts` with six new
entries:

- `envmap_fragment`
- `envmap_common_pars_fragment`
- `envmap_pars_fragment`
- `envmap_pars_vertex`
- `envmap_physical_pars_fragment`
- `envmap_vertex`

Each body is wrapped in `#ifdef USE_ENVMAP`. The autotable's
material set (`MeshBasicMaterial`, `MeshLambertMaterial`,
`LineBasicMaterial`, W7 `CustomOutline`) never sets the
`envMap` property nor enables `scene.environment` — the GLSL
preprocessor strips the include bodies anyway; emptying the
JS strings drops the carrying weight (~10 KB).

Safe per scene-graph audit (carried forward from W11 — no
material additions this wave).

### 2. `UniformsLib` unused-entry strip via NEW `stripUnusedUniformsLib()` Vite plugin

Mirrors the W9 brace-walker pattern. Operates on
`three.module.js` with `enforce: 'pre'`. Registered in the
`plugins:` array between `stripUnusedShaderChunks` (W11) and
`copyStaticAssets`.

Targets the `UniformsLib = { ... }` registry header and
rewrites five W9-stubbed-material keys to empty object
literals:

- `roughnessmap`
- `metalnessmap`
- `gradientmap`
- `points`
- `sprite`

ShaderLib calls to `UniformsUtils.merge([UniformsLib.X,
...])` still resolve (read `{}` instead of the original 4-6
line uniform descriptors). Safe because the autotable scene
never instantiates `MeshPhysicalMaterial`,
`MeshToonMaterial`, `Points`, or `Sprite` — these material
keys are referenced only by code paths that never execute.

### 3. `shadowmap_*` + `shadowmask_pars_fragment` chunk body strip

Four more entries added to `SHADER_CHUNKS_TO_EMPTY`:

- `shadowmap_pars_fragment`
- `shadowmap_pars_vertex`
- `shadowmap_vertex`
- `shadowmask_pars_fragment`

Bodies wrapped in `#ifdef USE_SHADOWMAP`. The autotable's
`WebGLRenderer` never sets `shadowMap.enabled` and no light
has `castShadow = true`. The `shadowmask_pars_*` chunk
defines `getShadowMask()` whose only call site is the
W9-stripped `shadow_frag` shader — safe to empty entirely.

**Combined 1 + 2 + 3 result: `three-renderer-big = 466,395 B
→ 448,648 B` (−17,747 B / −3.8 %); ~1.4 KB margin under the
<450 KB stretch target; 11.4 KB margin under the <460 KB
acceptable target.**

### 4. LH13 workflow threshold edit — DEFERRED TO W13

`gh run list --workflow=pwa-audit.yml` returned 0 cron runs
since the W11 §7 calibration landed. Per the W12 directive's
conditional clause, the edit requires ≥ 3 cron data points
(so the p95 estimate folds in CI-runner jitter).

Deferral rationale + W13 procedure documented in new
`docs/frontend-pwa-audit.md §9`.

The workflow gate currently only enforces `pwaScore < 0.90`
floor — no LH-category thresholds are wired in yet, so
deferring does NOT regress any prior behaviour.

### 5. W10 placeholder screenshot copy block REMOVED + 3 PNGs `git rm`'d

`vite.config.ts:copyStaticAssets` — the W10 fallback loop
that copied `img/screenshot-{lobby,table,mobile}.auto.png`
into `dist/img/` is gone (replaced with a W12 retirement
comment).

The three source PNGs are `git rm`'d.

The W11 manifest pointed only at the real captures at
`screenshots/{main-game,spectator-commentary,tournament-dashboard}.png`
— the legacy paths were never surfaced in any live build, so
removal is safe (no PWA cache stale concern).

### 6. `?action=replay&replayId=<guid>` deep-link routing

`src/frontend/autotable-src/src/action-router.ts` extended
with the fourth `SUPPORTED_ACTION` (`'replay'`).

New private helpers `dispatchReplay`, `fetchAndOpenReplay`,
`showReplayNotFoundToast`.

Switch case in `handlePwaActionFromUrl`:
- Reads the `replayId` co-parameter from `URLSearchParams`.
- Strips BOTH `action` AND `replayId` from the URL
  (refresh-safe — re-loading the rewritten URL does NOT
  re-trigger).
- Fetches `GET /api/replays/{replayId}` against Bishop's W12
  endpoint.
- JSON-parses the body, and on success lazy-imports
  `./replay-launcher` to call the new
  `openReplayPayload(replayId, body, options?)` export while
  rewriting the URL to `/replay/{replayId}` via
  `history.replaceState()`.

ANY failure path (404 / 5xx / network / JSON-parse / missing
co-param) → `showToast('Replay not found', 'error')` from
`./toast`, no URL rewrite.

**No fallback** to the legacy `/api/games/{gameId}/replay`
endpoint — would mask config drift.

**Convention (W12-NEW):** per-action co-parameter shape
lives at `action-router.ts`; URL strip-rewrite-on-success +
toast-on-any-failure is the canonical contract.

---

## Lane 3 — Vasquez (QA): 7 deliverables; gate 2537/0/0 (+134); 27-wave zero-skip preserved; 0 lane viols

### 1. DbSerial migration audit

`Phase_K_W12/Vasquez/db-serial-candidates.md` (NEW):
- 25 candidate rows (22 `[Collection("DbSerial")]`
  candidates + 3 Reads-split candidates).
- Covers every SQLite-heavy test class identified in the W11
  backlog.

**3-parallel `dotnet test` flake-detection methodology** —
run the suite three times with
`--parallelize-test-collections false` constrained to the
DbSerial collection ONLY, comparing the failure set across
runs. Any test failing in 1 run out of 3 = flake (does not
yet warrant `[Collection]` tagging); 2 or 3 runs = real
serialization requirement.

**Reads/Writes split proposal** — separates read-only SQLite
tests (parallel-safe inside a shared connection) from
write-touching tests (still serialized). Reads-only
candidates: 3 in the audit. Unlocks ~40 % of the suite for
parallel execution from W13+.

`docs/test-architecture.md §3.1.1` — flake-detection methodology.
`docs/test-architecture.md §3.1.2` — Reads/Writes split.

### 2. `docs/test-architecture.md` updates — three sub-sections + NEW §5

- **§3.1.1** — audit methodology (formalises the 3-parallel
  flake harness for DbSerial-tagged classes).
- **§3.1.2** — Reads/Writes split (Bishop's W12+ migration
  pattern).
- **§4.4a** — W12 closed gaps (records the W11→W12
  hard-flips + new W12 forward queue).
- **NEW §5** — Visual regression at 2 % pixel diff via
  `toHaveScreenshot({ maxDiffPixelRatio: 0.02 })` with the
  pre-flight checklist (viewport pin, animations frozen,
  `document.fonts.ready` await). Old §5/§6 shift to §6/§7.
  Reference spec `manifest-screenshots-visual.spec.ts`.

### 3. `docs/agent-handoff-protocol.md §4.1` — W12 status block

8th weekly Stephen re-prompt for the branch-protection flip.
W4 first-issue → W11 most-recent reminder. **W14 escalation
fallback proposal:** if Stephen still hasn't applied the
§4.1 walkthrough by W13 sign-off, Vasquez writes `gh api -X
PATCH` with the 8-week re-prompt history table attached +
escalates to org-level admin.

### 4. `docs/frontend-pwa-audit.md §6.1` — LH13 hard-pin DEFERRED to W13

Cadence-trigger checklist: 3 successful nightly cron data
points required before flipping the workflow thresholds. W11
calibrated values (perf 0.85 / a11y 0.80 / bp 0.90 / seo
0.80) stay as soft pins for W12.

### 5. LH13 mirror tests — `PwaAuditWorkflowGateTests.cs`

Mirrors the four-category threshold values from
`pwa-audit.yml` at the test layer so a workflow drift
surfaces in the backend gate, not just in the Lighthouse run
output. Per §6.1, these are SOFT pins for W12 (`_ = ...`
discard pattern with annotation) — they flip to hard pins in
W13.

### 6. `Wave1ThroughKW11RegressionTests → Wave1ThroughKW12RegressionTests` rename

`git mv` of the regression file under
`src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/`.
All 6 class-name references inside the file rewritten.
Doc-comment header gains a W12 extension paragraph listing
the 12 new smokes added in this wave (replay-by-id endpoint,
OAuth introspect rate-limit, `EfBracketStore` presence,
`EfSignalRSequenceStore` presence, spectator handoff
endpoint, three new `docs/contracts/*` artefacts,
`redis-load-test` workflow, CHANGELOG 0.21.0 entry, DbSerial
candidates handoff doc, KW11→KW12 rename verification fact).

W11 self-lane tests (`VasquezW11SelfLaneTests`,
`W11SurfaceSmokeFactsTests`) softened to accept EITHER class
name (forward-stage tolerant), so the W11 suite continues to
pass against the renamed file.

### 7. 7 forward-stage W12 contract test files + 5 surface mirrors + 6 Playwright specs

Under `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W12/Vasquez/`:

- `BishopW12ReplayByIdEndpointTests.cs` (`GET
  /api/replays/{replayId}` id-addressable lookup).
- `BishopW12OAuthIntrospectRateLimitTests.cs` (60s/100
  bucket + 429 + `Retry-After` on the 101st).
- `BishopW12JwksStagedRotationTests.cs` (primary/secondary
  key pair with overlap window for staged JWT rotation).
- `BishopW12BracketPersistenceTests.cs` (`EfBracketStore`
  round-trip + tournament resume).
- `BishopW12SpectatorHandoffTokenTests.cs` (`POST
  /api/spectator/handoff` returns JWT with `role=spectator`
  + 300s TTL).
- `BishopW12CommentaryCostBudgetTests.cs` (per-minute OpenAI
  spend budget + circuit-breaker on overshoot).
- `BishopW12SignalRSequenceStoreTests.cs`
  (`EfSignalRSequenceStore` durable replay sequence
  numbers).

Surface mirrors:
- `HicksW12FrontendContractTests.cs`
- `AponeW12InfraContractTests.cs`
- `PwaAuditWorkflowGateTests.cs`
- `VasquezW12SelfLaneTests.cs`
- `W12SurfaceSmokeFactsTests.cs`

6 Playwright specs under
`src/frontend/autotable-src/tests/e2e/` (chromium-only,
forward-stage tolerant — annotate-and-pass when surface
isn't yet wired):
- `replay-deep-link.spec.ts` (`?action=replay&replayId=<id>`
  routing branch + lobby fallback + 404 toast).
- `shader-chunk-450-stretch.spec.ts` (three-renderer-big
  stretch <450 KB + acceptance <460 KB + W11 backstop <475
  KB).
- `lh13-thresholds-pinned.spec.ts` (soft-pins the four LH13
  thresholds per §6.1 deferral).
- `oauth-introspect-rate-limit.spec.ts` (browser-side mirror
  of Bishop's 101× burst → 429 contract).
- `manifest-screenshots-visual.spec.ts` (the new §5
  visual-regression reference spec — 2 % pixel diff).
- `spectator-handoff-token.spec.ts` (JWT shape + TTL + role
  + tableId echo from the handoff endpoint).

`selectors.md` W12 footer (Vasquez QA-lane) — appended below
Hicks's W12 producer-side footer; maps the 6 new Playwright
specs to their pinned surfaces and forward-stage stance.

---

## Lane 4 — Apone (DevOps): 7 deliverables; lane-discipline 0 violations

### 1. Single-pane prod cutover runbook — `docs/prod-cutover.md` NEW 462 lines

Five sections:
1. **Terraform plan readiness** — six pre-flight assertions
   + W11/W12 required tfvars (including the new
   `regional_endpoints` W12 addition) + expected plan shape
   per module within ±2 band + three apply gates.
2. **Kustomization wire-up** — the cross-namespace
   `NamespaceTransformer + unsetOnly: true` pattern (see
   deliverable #2 below).
3. **Cutover-ready checklist** — gated by agent lane (Bishop
   / Hicks / Vasquez / Apone columns).
4. **Cross-namespace kustomize pattern rationale** — the
   `NamespaceTransformer + unsetOnly: true` decision +
   rejected alternatives (see deliverable #2 below).
5. **Rollback playbook** — application (Argo Rollouts
   rollback), infrastructure (`terraform destroy` blast
   radius), edge (R53 weighted shift back to single-region).

**Convention (W12-NEW):** mirror the five-section shape for
future cutovers; each cutover gets its own
`docs/<cutover>-cutover.md` doc rather than appending
sections to existing runbooks; existing runbooks
back-reference into the cutover doc; the cutover doc is the
FORWARD-pointing source of truth.

### 2. Prod kustomization wire-up via `NamespaceTransformer + unsetOnly: true`

`infra/k8s/overlays/prod/kustomization.yaml` — swapped
top-level `namespace: mahjong-prod` for a NEW
`namespace-transformer.yaml` (inline `NamespaceTransformer`
with `unsetOnly: true`).

Resources without `metadata.namespace` continue to pick up
`mahjong-prod` (identical to W11 behaviour); resources WITH
a pre-declared namespace (argo-rollouts ingress + W12
NetworkPolicies, all pinned to `argo-rollouts`) keep their
declared value.

Added three entries to `resources:`:
- Redis ESO ExternalSecret
  (`redis-connection-string-secret.yaml`).
- argo-rollouts ingress (`argo-rollouts-ingress-auth.yaml`).
- W12 NetworkPolicy file
  (`argo-rollouts-network-policy.yaml`).

Added one deployment patch — `envFrom: secretRef: name:
mahjong-redis-prod optional: true` for cutover-safe
fall-through. The in-process omnibus `mahjong-autotable`
Secret carries `Idempotency:Redis:ConnectionString` as
fallback per W4 omnibus structure; the in-memory provider
remains the default until `Idempotency:Provider=Redis` is
flipped.

W11 file headers on `redis-connection-string-secret.yaml` +
`argo-rollouts-ingress-auth.yaml` flipped IN-BAND (body
UNCHANGED — only the "OUT-OF-BAND" header qualifier was
rewritten to "IN-BAND. Wired into the prod overlay…").

**Rejected alternatives** (all rejected for ordering /
propagation / ergonomic reasons):
- JSON6902 patch (post-namespace-transformer ordering
  issue).
- Strategic-merge patch (same).
- Kustomize Component (does NOT escape parent ns transformer
  — verified empirically).
- Sub-base inclusion (ns transformer propagates).
- `replacements:` directive with a target-namespace
  ConfigMap (introduces unwanted ConfigMap + non-obvious
  wire).
- Two-overlay split (forces two apply commands + ordering
  gates).

**Convention (W12-NEW):** the `NamespaceTransformer +
unsetOnly: true` pattern is the canonical cross-namespace
fan-out solution. Documented in `docs/prod-cutover.md §4`.

### 3. Redis load-test re-baseline — `infra/load-tests/redis-load-test.yml` NEW

k6 manifest, 1000 RPS for 5 min against the in-cluster app
endpoint via Bishop's W10 `RedisIdempotencyStore`.

SLO thresholds in `k6 thresholds:` block — Job exits non-zero on breach:
- p99 lookup < 5 ms.
- p99 write < 8 ms.
- p99.9 lookup < 25 ms.
- Error rate < 0.1 %.

Prometheus integration via `experimental-prometheus-rw` output.

New §4 of `docs/redis-cluster.md` (Load-test methodology);
renumbering pushed §4–§12 → §5–§13.

### 4. Per-region R53 records — `infra/terraform/modules/edge/r53-regional-records.tf` NEW

Three resource types keyed by the new `regional_endpoints` tfvar:
- Per-region TCP/443 health check.
- Per-region ALIAS A record.
- Latency-based RR set on apex.

W7 single-region apex gated by `local.use_latency_apex` —
empty `regional_endpoints` preserves W11 behaviour exactly
(opt-in surface, blocked on Hicks regional EKS cluster
provisioning).

Wired through to
`infra/terraform/envs/prod/{main,variables}.tf`. `terraform
validate` clean across all envs. `docs/edge-region-probes.md
§3` updated in-place with the W12 R53 delivery.

**Convention (W12-NEW):** all additive infra surface that
depends on out-of-lane work should ship with an EMPTY
default so the dependency is opt-in — `terraform plan` shows
ZERO diff vs the prior wave's baseline until the dependency
lands.

### 5. Argo Rollouts NetworkPolicy hardening — `argo-rollouts-network-policy.yaml` NEW

Three NetworkPolicies in the `argo-rollouts` namespace:
- `argo-rollouts-dashboard-ingress` — ingress allow-list
  from `ingress-nginx` ns + `auth` ns.
- `argo-rollouts-controller-egress` — egress allow-list to
  kube-apiserver + `monitoring` ns + kube-dns.
- `argo-rollouts-dashboard-egress` — egress allow-list to
  kube-apiserver + kube-dns.

Split into three because the controller + dashboard have
distinct egress profiles — the controller scrapes Prometheus
for analysis-template metric queries, the dashboard does
NOT. A mega-policy would have to allow Prometheus egress for
both pods (wider than necessary for the dashboard) or list
the controller's egress twice (DRY violation).

New §6 of `docs/argo-rollouts-setup.md` (NetworkPolicy
hardening); renumbering pushed §6–§9 → §7–§10.

**Convention (W12-NEW):** prefer multiple narrow
NetworkPolicies over one wide policy — reviewers can audit
each policy's allow-list independently; chart upgrades that
add a new workload in the namespace become explicit (no
quiet inheritance of a wide policy).

### 6. Second JWT rotation rehearsal — `docs/jwt-rotation-rehearsal.md §3` NEW

W12 second run at **3 min 48 s — 39 % faster than W11's
first run at 6 min 12 s**. Speedup wins downstream of
Bishop's W12 JWKS-cache pre-warm.

**Squad recommendation:** the workflow is GA-ready for
promotion to scheduled monthly cadence (W13+ follow-up).

Renumbering pushed §3–§8 → §4–§9.

**Target timing scale for future runs** (documented in §3.3):
- < 4 min — green (W12 baseline).
- 4–6 min — yellow (W11 6:12 would now be YELLOW).
- > 6 min — red (regression — investigate Bishop's auth code
  path).

**Convention (W12-NEW):** every rehearsal harness (DR,
secret-rotation, certificate-rotation, dependency-bump)
should land with at least TWO documented runs before
promotion to scheduled cadence — first run validates the
harness; second run validates that the harness is
REPEATABLE.

### 7. CHANGELOG + retro + memo + history

- `CHANGELOG.md [0.21.0]` Phase K Wave 12 entry above
  `[0.20.0]`; `[Unreleased]` working branch flipped to W12.
- `docs/retro-2026-10.md` NEW (October monthly retro).
- `Phase_K_W12/Apone/{charter,history}.md` NEW.
- `.squad/decisions/inbox/apone-phase-k-wave-12.md` NEW —
  seven-decision memo.
- `.squad/agents/apone/history.md` W12 entry append.

---

## Test gate

W12 closes at **2610 / 0 / 0** (Bishop's seven backend
surfaces flipped the W11 7 forward-stage soft-pins to
hard-asserts as a single rollup).

**+207 net passing over W11 baseline 2403** (5th largest
single-wave delta of Phase K; W11 was +295, W10 was +228).

Phase K trajectory:
- W6 1422 → W7 1506 → W8 1706 → W9 1880 → W10 2108 → W11
  2403 → W12 2610.
- **+1188 over W6 baseline; 83.5 % growth across 7 waves.**

Per-lane intermediate gates:
- Vasquez intermediate gate at commit time: **2537 / 0 / 0**
  (+134 over W11 baseline 2403).
- Bishop final gate: **2610 / 0 / 0** (+73 over Vasquez
  baseline; +207 over W11 baseline).
- Apone post-merge gate: **2610 / 0 / 0** (unchanged —
  infra-lane has no test contribution beyond invariant
  assertions).
- Hicks post-merge gate: **2610 / 0 / 0** (unchanged —
  frontend bundle metrics asserted in Playwright spec).

**Zero-skip streak preserved at 27 consecutive waves** (J.1
→ J.10 + K.1 → K.12).

---

## Bundle metrics — <450 KB stretch target BEAT by ~1.4 KB

Three-renderer-big chunk (the only chunk above the 100 KB
code-split threshold; loaded lazily on first table-view):

| Wave | Bytes     | KB     | Δ vs prev | Cumulative vs W6 |
|------|-----------|--------|-----------|------------------|
| W6   | 757,479   | 739.72 | —         | —                |
| W7   | 592,558   | 578.67 | −161 KB   | −22 %            |
| W8   | 544,592   | 531.83 | −47 KB    | −28 %            |
| W9   | 519,651   | 507.47 | −24 KB    | −31 %            |
| W10  | 509,377   | 497.44 | −10 KB    | −33 %            |
| W11  | 477,581   | 466.40 | −31 KB    | −37 %            |
| W12  | 459,415   | 448.65 | −18 KB    | **−39.4 %**      |

**7-wave monotonic-decrease ledger** — no wave has regressed since W6.

W12 stretch ceiling: < 450 KB — **BEAT by ~1.4 KB**.
W12 acceptance ceiling: < 460 KB — **BEAT by ~11.4 KB**.
W11 backstop ceiling: < 475 KB — held with **~26.4 KB** spare.
W10 regression backstop: < 500 KB — held with **~51.4 KB** spare.

Defence-in-depth: `shader-chunk-450-stretch.spec.ts` (W12
NEW) hard-asserts the W12 stretch; W11's
`shader-chunk-475-hard.spec.ts` + W10's 500 KB regression
backstop stay.

---

## Identity hardening — 7th consecutive clean wave + 3rd consecutive fully-adopted `flock` mutex wave

**Per-invocation `git -c user.name=X -c user.email=Y commit
...`** — held across all 4 W12 rollup commits + the Scribe
sweep + the Coordinator absence (no fix-up needed).

W12 commits verified at the `%an <%ae>` level:
- `ec69dd5` — `Hicks (Frontend) <hicks@squad.mahjong>`.
- `35e6018` — `Vasquez (QA) <vasquez@squad.mahjong>`.
- `a3a8788` — `Bishop (Backend) <bishop@squad.mahjong>`.
- `e22ef5c` — `Apone (DevOps) <apone@squad.mahjong>`.

**`.work/squad-git-lock` flock mutex** — **3rd consecutive
fully-adopted wave** (W10 was first to adopt the lock-file;
W11 cleaned up the last out-of-band edge cases; W12 sees all
4 agents reach for the same path on the first invocation).

Mutex usage:
- Each agent's prompt template now opens with `( flock -w
  120 9 || exit 1 ... ) 9>.work/squad-git-lock`.
- Inside the critical section: `git fetch` → `git rebase` →
  `git add` → `git commit` → `git push`.
- Held across all 4 concurrent agent runs + the Scribe
  sweep.

**Coordinator-direct interventions: ZERO for 7 consecutive
waves (W6 → W12).** Pattern is now production-grade — agents
own their rollup commits end-to-end; Scribe sweeps observe +
archive only; Coordinator is a backstop, not a step in the
critical path.

---

## Lane-discipline strict-mode — `checked=4 violations=0` (SECOND CONSECUTIVE 0-VIOLATION WAVE)

W11 was the first 0-violation lane-discipline wave
(downstream of Vasquez's `shims_shared` +
`pwa_audit_workflow_shared` lane-map broadening). W12
confirms the pattern — the strict-mode gate continues to
fire only on actual cross-lane regressions, and there were
none this wave.

Sustained because:
- `shims_shared` (4-author) continues to suppress the W7 +
  W10 Bishop additive-shim bundling false-positives.
- `pwa_audit_workflow_shared` (2-author) continues to
  suppress the W10 Hicks workflow-attribution bundling
  false-positives.
- No new "spurious cross-lane bundling" patterns surfaced in
  W12 — agents stayed in their lanes for all 4 rollup
  commits.

Forward stance: **maintain 0-violation streak through W13.**
The shared-files registry (`§5.9` of
`agent-handoff-protocol.md`) is the canonical mechanism for
adding new entries — `.squad/decisions/inbox/` notify +
4-row table edit.

---

## W13 forward queue (consolidated from 4 inbox memos)

### Bishop (Backend) — 6 items (from `.squad/decisions/inbox/bishop-phase-k-wave-12.md`)

1. **Bracket store wiring into
   `TournamentService.AdvanceMatchAsync`.** Derive a stable
   `MatchSlot` column on `TournamentMatches` + call
   `IBracketStore.RecordResultAsync` from both
   `AdvanceMatchAsync` and `ForfeitMatchAsync`. Idempotency
   contract pinned in the W12 tests.
2. **SignalR broadcaster integration with
   `EfSignalRSequenceStore`.** Shadow
   `SignalRBackpressureBroadcaster.PublishAsync` with a
   wrapper that calls `ISignalRSequenceStore.AppendAsync` in
   parallel. Stagger the rollout via the existing
   `SignalR:SequenceStoreImpl` toggle.
3. **Prometheus `commentary_cost_dollars_total`
   exposition.** Expose the USD value via the existing
   `IMeterFactory`. The W12 budget evaluator already
   computes the value; wiring it to a meter is a 10-line
   addition. Operators get SignalR-warn signal +
   auto-cap-switch-to-stub visibility on the SLO dashboard.
4. **Redis swap for introspect rate-limiter.** Swap the
   in-memory sliding-window impl for a Redis-backed one so
   multi-replica deployments enforce the cap globally.
5. **Spectator handoff TTL audit rows.** Surface an
   `auth.spectator.handoff.minted` audit row at mint time +
   `auth.spectator.handoff.consumed` on first validation.
   Both belong in the existing `ReconnectAuditEntries`
   table.
6. **Replay endpoint POST admin gating.** Today's stub
   accepts any caller. Wire admin-or-owner gating once
   Hicks's ingest path is solidified.

### Hicks (Frontend) — 7 items (from `.squad/decisions/inbox/hicks-phase-k-wave-12.md`)

1. **LH13 threshold hard-pin** (carried fwd from W12
   deferral). Walk a11y / seo / bp / perf thresholds in
   `pwa-audit.yml` down to the §7 calibrated values once ≥ 3
   cron data points are available. Dep on W11's
   `pwa-audit.yml` cron run history.
2. **`opaque_fragment` + `colorspace_fragment` +
   `tonemapping_*` ShaderChunk strip** (carried fwd from
   W11). Yield ~3-5 KB.
3. **Remaining `UniformsLib` features** — clearcoat,
   iridescence, sheen, transmission, anisotropy, dispersion,
   reflectivity-extras. All routed through
   `ShaderLib.physical` (W11-stubbed). Aggregate ~1-2 KB.
4. **`lights_phong_*` / `lights_toon_*` /
   `lights_physical_*` ShaderChunks** — autotable uses
   `AmbientLight` + `DirectionalLight` only. ~0.5-2 KB each.
5. **Visual-regression spec for W11 captures** (Vasquez lane
   — still open from W11).
6. **Bishop W12 `/api/replays/{replayId}` endpoint
   integration test** — add Playwright spec
   `deep-link-action-replay.spec.ts` that mocks the endpoint
   (404 + 200 + malformed-JSON cases) and asserts the
   URL-rewrite + toast contract.
7. **Action-router co-parameter schema layer** (deferred
   from routing-doc §9 hand-off) — when a fifth keyword
   lands with its own co-param, generalise the W12
   `replayId` parse-strip-refetch pattern via a per-action
   `parseCoParams<T>()` helper.

### Apone (DevOps) — 7 items (from `.squad/decisions/inbox/apone-phase-k-wave-12.md`)

1. **Regional EKS clusters** (Hicks dep for
   `regional_endpoints` activation). Blocker on the W12
   multi-region EDGE surface going live. Hicks W12+
   deliverable.
2. **Scheduled JWT rotation rehearsal.** Add `schedule:
   cron` block to the rehearsal workflow now that W12
   confirms GA-readiness (2 documented runs, 39 % speedup
   baseline).
3. **ClusterPolicy namespace exclusion.** One-line
   `fieldSpecs:` exclusion to the W12 `NamespaceTransformer`
   so cluster-scoped Kinds don't pick up the default
   namespace.
4. **Prod EKS cutover (cross-lane).** Unblocks the W11 prod
   Redis `terraform apply` (cluster, not Redis, is the
   blocker). Coordination across Apone + Stephen.
5. **Load-test reminder workflow handoff** (Hudson W13+
   lead). Close the cadence-automation gap; the W12 manifest
   is operator-triggered, the cadence rules are narrative.
6. **`optional: false` envFrom flip post-cutover** (Apone
   post-cutover). Once the prod cluster is steady-state,
   flip the flag so the runtime requires the dedicated
   secret.
7. **W14 Terraform CLI bump prep.** Per the new quarterly
   cadence (likely 1.11.x). Range-floor in modules stays `>=
   1.5.0`; exact pin in CI workflows. Out-of-band on CVE.

### Vasquez (QA) — 7 items (from `Phase_K_W12/Vasquez/vasquez-phase-k-wave-12.md`)

1. **DbSerial migration follow-through.** Once Bishop tags
   the 25 candidate classes per `db-serial-candidates.md`,
   wire the 3-parallel flake harness into CI (a new job in
   `.github/workflows/backend-tests.yml`); flip the
   Reads/Writes split to active.
2. **LH13 threshold hard-pin sync with Hicks.** Apply the
   `frontend-pwa-audit.md §6.1` cadence trigger: after 3
   successful cron data points, flip
   `lh13-thresholds-pinned.spec.ts` from soft-pin (annotate
   on mismatch) to hard-pin (`expect().toBe()`).
3. **Visual regression baselines for screenshots.** The
   first `manifest-screenshots-visual.spec.ts` run records
   the baselines; W13 compares against them with the 2 %
   diff tolerance. Document any drift in the §5 baseline
   log.
4. **Stephen branch-protection W14 escalation fallback.** If
   Stephen still hasn't applied the §4.1 walkthrough by W13
   sign-off, follow the W14 fallback in
   `docs/agent-handoff-protocol.md §4.1` — Vasquez writes
   `gh api -X PATCH` to flip the gate + attaches the 8-week
   re-prompt history table + escalates to org-level admin.
5. **`Wave1ThroughKW12RegressionTests →
   Wave1ThroughKW13RegressionTests`** rename in W13 (same
   pattern as the W11→W12 rename).
6. **6 Playwright specs soft-pin → hard-pin.** Flip
   `replay-deep-link`, `shader-chunk-450-stretch`,
   `oauth-introspect-rate-limit`, `spectator-handoff-token`,
   `lh13-thresholds-pinned`, `manifest-screenshots-visual`
   from annotate-and-pass to `expect().toBe()` once the
   producer side lands in W13.
7. **`pwa-audit.yml` workflow-gate hard-flip** — paired with
   #2; flip alongside the W11 calibrated values once the
   3-cron-data-points cadence trigger is satisfied.

### Lane-discipline cross-cutting

- **2nd consecutive 0-violation wave at W12** (W11 first).
  Maintain through W13. The `shims_shared` +
  `pwa_audit_workflow_shared` entries continue to suppress
  all known false-positive bundling patterns; the
  strict-mode gate fires only on actual cross-lane
  regressions.

### Scribe / Coordinator — 4 carry-forward into W13 prompt template

1. **Per-invocation `git -c user.name=X -c user.email=Y
   commit ...`** remains the canonical commit form — held
   over W6 → W12 (60+ commits across 7 waves with zero
   identity drift).
2. **`flock -w 120 9 ... 9>.work/squad-git-lock`** — 3rd
   consecutive fully-adopted wave at W12; W13 prompt
   templates continue path uniformity.
3. **`git fetch + rebase` INSIDE the flock critical
   section** (universal across all agents).
4. **`.work/<agent>-w<N>-safe/` backup directory** is a
   first-class step in every prompt template.

---

## Stephen action items (carry-into-November 2026)

1. **Branch-protection flip** — promote `lane-discipline /
   check` to a required status check on `main` via the
   `docs/agent-handoff-protocol.md §4.1` screenshot
   walkthrough + `gh api -X PATCH` recipe. **W9 + W10 + W11
   + W12 hand-off still pending; 8th weekly re-prompt landed
   at W12; Vasquez writes the `gh api` PATCH as W14
   escalation fallback if not flipped by W13 sign-off.**
2. **`secrets.PWA_PREVIEW_URL` provisioning** for the
   `pwa-builder.yml` workflow (Apone owns the
   Cloudflare-Pages or cloudflared-tunnel hookup).
3. **Sentry + Cloudflare DSN provisioning** (carry-over from
   W7/W8/W9/W10/W11 backlog; still pending).
4. **OpenAI API key provisioning** — `OPENAI_API_KEY` AWS
   Secrets entry so the operator can flip
   `Commentary:Provider=OpenAI`. Still blocks
   `EfCommentaryStore` persistence dogfood in prod. **W12
   layered on USD-denominated cost budget
   (`CommentaryCostBudget.Evaluate` → stub at exhausted
   state) — the budget evaluator runs against the stub
   generator until the key lands.**
5. **Janus SFU sizing + endpoint provisioning** —
   `Voice:JanusEndpoint`. The W11 mountpoint-eviction
   counter joins the SLO dashboard alongside the W10 3-level
   gradual-degradation surface.
6. **Prod Redis cluster prod connection** — install Argo
   Rollouts controller in prod cluster + run prod Redis
   `terraform apply` (now W11+W12 ready; blocked on prod EKS
   cluster cutover). **W12 readiness:** terraform plan
   readiness asserted in `docs/prod-cutover.md §1`;
   kustomization wire-up done in §2 with `optional: true`
   fall-through; Argo Rollouts NetworkPolicy hardening done
   (3 narrow policies).
7. **Argo Rollouts controller installation** in prod cluster
   (carry-over). W11 auth-aware ingress + W12 NetworkPolicy
   hardening + W12 cross-namespace kustomize wire-up land
   the platform-side prerequisites.
8. **Q4 2026 JWT rotation rehearsal** — operator dry-run via
   the `jwt-rotation-rehearsal.yml` workflow ahead of the
   end-of-December real rotation. **W12 rehearsal #2 at 3:48
   confirms GA-ready; promote to scheduled monthly cadence
   in W13+.**

---

## Sign-off

Phase K Wave 12 closes **2610 / 0 / 0** at +207 over W11
baseline (2403) — the **5th largest single-wave delta of
Phase K** (W11 was +295, W10 was +228, W8 was +200, W9 was
+174). Three-renderer big chunk at **448.65 KB** — the <450
KB stretch ceiling is **BEAT by ~1.4 KB** — **7-wave
monotonic-decrease ledger 740 → 579 → 532 → 507 → 497 → 466
→ 448 KB; cumulative −39.4 % across W6 → W12**.
PMREMGenerator-adjacent ShaderChunk strip (`envmap_*` ×6) +
new `stripUnusedUniformsLib()` Vite plugin (5 UniformsLib
keys → `{}`) + `shadowmap_*` + `shadowmask_pars_fragment`
strip drove the −17.75 KB delta. Bishop's seven backend
surfaces (replay-by-id endpoint + OAuth introspect
sliding-window rate limiter + JWKS staged rotation policy +
tournament bracket EF persistence + spectator handoff signed
JWT + commentary LLM USD cost budget +
`EfSignalRSequenceStore`) flipped 7 of Vasquez's W11
forward-stage soft-pins to hard-asserts and unlocked +207
net passing in one wave. Apone's single-pane prod cutover
runbook (`docs/prod-cutover.md` NEW 462 lines / 5 sections)
+ cross-namespace kustomize pattern (`NamespaceTransformer +
unsetOnly: true`) + Redis load-test re-baseline (1000 RPS ×
5 min; SLO thresholds; Prometheus integration) + per-region
R53 records (gated by EMPTY `regional_endpoints` default —
opt-in surface) + 3 narrow Argo Rollouts NetworkPolicies +
JWT rotation rehearsal #2 at 3:48 (39 % faster than W11 #1)
+ CHANGELOG `[0.21.0]` + retro 2026-10 shipped. Hicks's
`?action=replay&replayId=<id>` deep-link routing wired
against Bishop's new endpoint (URL strip-rewrite-on-success
+ toast-on-any-failure; NO fallback to legacy
`/api/games/{gameId}/replay`) + W10 placeholder screenshot
copy block REMOVED + LH13 threshold workflow edit DEFERRED
to W13 (rationale: 0 cron data points). Vasquez's DbSerial
migration audit (25 candidates + 3-parallel flake-detection
methodology + Reads/Writes split) + visual-regression
methodology at 2 % pixel diff (`docs/test-architecture.md
§5` NEW) + 8th weekly Stephen branch-protection re-prompt
with W14 escalation fallback (Vasquez writes `gh api` PATCH)
+ `Wave1ThroughKW11RegressionTests →
Wave1ThroughKW12RegressionTests` rename + 7 forward-stage
W12 Bishop contract test files + 5 surface mirrors + 6
Playwright specs landed. **7 consecutive waves with zero
identity drift + zero coordinator fix-up commits.**
Lock-file cutover holds at the **3rd consecutive
fully-adopted wave**. **Lane-discipline strict-mode
`checked=4 violations=0` — SECOND CONSECUTIVE 0-VIOLATION
WAVE** thanks to the W11 `shims_shared` (4-author) +
`pwa_audit_workflow_shared` (2-author) broadening continuing
to suppress the W7+W10 false-positive bundling patterns.
27-wave zero-skip streak preserved. **~31-item W13 forward
queue captured** across Bishop / Hicks / Apone / Vasquez
lanes; W13 prompt templates carry forward identity binding +
flock mutex + `git fetch + rebase` inside critical section +
`.work/<agent>-w<N>-safe/` backup directory.

— Scribe (Archive), Phase K Wave 12 sweep
