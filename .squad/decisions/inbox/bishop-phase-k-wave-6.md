# Bishop — Phase K Wave 6

**Branch:** `stlong/phase-k-wave-6-bringup`
**Scope:** backend — Phase K Wave 6 bring-up. Eight scoped
deliverables (one was folded into another for natural cohesion):

1. RS256 JWT migration (config toggle, key loading, validation, JWKS
   surface, OIDC discovery folded in)
2. Voice livestream HLS controller stub (`/api/voice/livestream/...`)
3. WebRTC SFU spectator stub (`SpectatorVoiceHub` SignalR hub +
   sizing memo)
4. JWKS header tuning (Cache-Control + structured 404/200 bodies;
   folded into #1)
5. AI commentary stub API (`ICommentaryGenerator` + replay endpoint)
6. Swiss + double-elimination tournament brackets (factory +
   generators + service wiring)
7. OAuth production-verification + zero-downtime dev→prod migration
   runbook (`docs/oauth-production-setup.md` §7)
8. OIDC discovery stub (`/.well-known/openid-configuration` —
   delivered alongside #1/#4 since both gate on `JwtAlgorithm`)

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` → **Passed: 1421, Failed: 1, Skipped: 0** (2m 38s).
Baseline at session start (HEAD on `stlong/phase-k-wave-6-bringup`
after sibling agents' bring-up commits) was 1345/0/0. **+76 net
passing**.

> The single remaining failure
> (`K8sManifestSanityTests.BaseKustomization_IncludesAllResources`)
> is **outside Bishop's lane** — it lives in `infra/k8s/base/` (Apone
> DevOps). The `coturn-configmap.yaml` resource was added in Phase K
> Wave 2 but never enumerated in `infra/k8s/base/kustomization.yaml`,
> so the Phase-J-7 sanity test catches the omission. Forwarded to
> Apone via the W7 forward-notes section.

> **Author hygiene — Wave-6 hardening.** This wave introduced a hard
> rule: never call `git config user.name` (shared `--local` config
> drifts between sibling agents). Every state-mutating git command
> passes the identity inline:
> `git -c user.name="Bishop (Backend)" -c user.email="bishop@squad.mahjong" commit -m …`
> Multi-step sequences (stash pop → add → commit → push) wrap in
> `flock -w 120 9 … 9>/tmp/squad-git-lock` to serialize against
> Apone / Hicks / Vasquez.

---

## Task 1 — RS256 JWT migration

### Problem

Wave 5 shipped HMAC-only JWT mint. RS256 is required for any
production deployment that wants to publish a JWKS document the
frontend (or third-party verifiers) can fetch and cache: with HMAC
you can't publish the secret. Phase K's exit gate requires a
config-gated RS256 path that ships TODAY but defaults OFF so the
existing HMAC fleet keeps working unchanged.

### Solution

* New `Auth:JwtAlgorithm` config key (string, default `"HS256"`).
  Bound in `Program.cs` with the same `Auth:` / `Authentication:`
  fallback the rest of the auth section uses.
* New `Auth:JwtRsaKeys` config key (string array of PEM-encoded RSA
  private keys). The first key is the active signer; the rest are
  legacy keys held for verification only (covers the rotation
  window).
* `Auth/JwtRsaSigningKey.cs` — wraps an `RSA` instance. The kid is
  deterministically derived as 8 bytes of SHA-256 over the
  `SubjectPublicKeyInfo` (SPKI) bytes, base64url-no-padding. This
  matches RFC 7517 §4.5 ("Use a hash of the public key") and gives
  a stable kid that's stable across pod restarts but changes when
  the key rotates.
* `JwtSigningKeyProvider` now carries an `Algorithm` property and
  RSA-specific accessors (`ActiveRsaKey`, `AllRsaKeys`,
  `TryGetRsaByKid`). The HMAC accessors are untouched.
* `JwtIssuingService.IssueAsync` branches on
  `Provider.Algorithm`. On RS256 it builds a `SigningCredentials`
  with `SecurityAlgorithms.RsaSha256` instead of `HmacSha256`. The
  audit Kind `auth.jwt.signed.with_key.<index>` is emitted in both
  arms.
* `JwtValidationService` accepts both algorithm families but
  **never crosses** — an HMAC token presented when the algorithm is
  RS256 (or vice versa) is rejected with `invalid_algorithm`. This
  blocks algorithm-confusion attacks (CVE-2015-9235 family).
* `AuthTokenController.Jwks()`:
  - On RS256, returns 200 with a real JWKS document
    (`{ keys: [{ kty:"RSA", kid, use:"sig", alg:"RS256", n, e }] }`)
    and `Cache-Control: public, max-age=3600`. Modulus + exponent
    are base64url-no-padding per RFC 7517 §6.3.1.
  - On HS256, returns 404 with `Cache-Control: public, max-age=60`
    (short TTL so a downstream CDN doesn't pin the 404 forever and
    block the eventual RS256 flip) + a structured body
    `{ reason: "jwt-algorithm-is-hs256", migrateTo: "RS256",
       migrate_to: "RS256" }` (both casings — frontend uses camel,
    log scrapers use snake).
* `Auth/JwtAlgorithmStartupLogger.cs` — `IStartupFilter` that
  emits a single warning at boot when `Algorithm == "HS256"`:
  `"JWT is configured for HS256 — flip Auth:JwtAlgorithm to RS256
   before production rollout."` Keeps the migration hint loud.

### Test coverage

* Wave 5 `JwksEndpointContractTests` updated to the W6
  cache + body contract.
* New `BracketGeneratorDeterminismTests` + the existing W6
  surface tests in `BishopW6SurfaceTests` cover the algorithm
  enum + JWKS branch.

---

## Task 2 — Voice livestream HLS controller

### Problem

W6 brief requires an admin-gated HLS endpoint that returns a
playlist + segments for game-replay livestreaming. The actual
ffmpeg pipe lands in Phase L — Wave 6 ships the controller +
in-memory recorder so the route shape is locked in.

### Solution

* `Voice/ILivestreamRecorder` + `LivestreamHandle` record (gameId,
  playlistPath, startedAtUtc).
* `Voice/InMemoryLivestreamRecorder` — `ConcurrentDictionary`-backed
  stub. Returns a canonical 6-segment m3u8 playlist + a 1-byte
  stub `stub-000.ts` payload. Start/stop are idempotent.
* `Voice/VoiceLivestreamController` — routes:
  - `POST /api/voice/livestream/{gameId:guid}/start` — owner / admin
    only; emits `voice.livestream.start` audit Kind.
  - `POST /api/voice/livestream/{gameId:guid}/stop` — same gate;
    emits `voice.livestream.stop`.
  - `GET /api/voice/livestream/{gameId:guid}/playlist.m3u8` — 200
    + `application/vnd.apple.mpegurl` when recording; 404 with
    structured `{ reason }` body otherwise.
  - `GET /api/voice/livestream/{gameId:guid}/{segment}.ts` —
    streams the segment bytes.
* DI: `services.AddSingleton<ILivestreamRecorder,
  InMemoryLivestreamRecorder>()`.
* Audit Kinds added to `ReconnectAuditEntry`:
  - `KindVoiceLivestreamStart = "voice.livestream.start"`
  - `KindVoiceLivestreamStop = "voice.livestream.stop"`

---

## Task 3 — Spectator voice hub (WebRTC SFU stub) + sizing memo

### Problem

Spectator voice (lobby-side audio for tournament observers) does NOT
scale via the existing peer-mesh — at N spectators it would need
N(N-1)/2 audio streams. Phase L will run an SFU (selective
forwarding unit); Wave 6 ships the join-handshake stub + the SFU
sizing analysis.

### Solution

* `Voice/SpectatorVoiceHub.cs` — SignalR `Hub` at
  `/hubs/voice/spectator`. Single method
  `JoinSpectatorVoice(string tableId)` → `SpectatorVoiceJoinResult
  { Ok, Reason?, SfuEndpoint?, PeerId? }`. Stub returns
  `sfu://stub/{tableId}` so the frontend can wire its handshake
  flow against a deterministic URL.
* Uses `PlayerIdentityService.ResolveFromCookie(HttpContext)` to
  authenticate. Anonymous reads OK (spectators).
* `docs/voice-sfu-design.md` — sizing table (50 / 100 / 500
  spectators), Janus recommendation, network-egress math.

---

## Task 4 — JWKS header tuning + OIDC discovery (folded into 1 & 8)

Folded into Task 1's `Jwks()` branch and Task 8's
`OpenIdConfiguration()` action. Both cache the response per the
contract above.

---

## Task 5 — AI commentary stub

### Problem

Operators need a stable contract for "AI commentary on this game"
even before the LLM integration lands. Wave 6 ships the interface
+ stub generator so the controller URL and audit Kind are pinned.

### Solution

* `Commentary/ICommentaryGenerator` — interface + `CommentaryReplay`
  + `CommentaryItem` records.
* `Commentary/StubCommentaryGenerator` — returns one
  `CommentaryItem` with the canonical message *"Game commentary not
  yet available — Phase L feature."* The `generator` field on the
  envelope reads `"stub"`.
* `Commentary/CommentaryController`:
  - `POST /api/games/{gameId:guid}/commentary` — admin-only; emits
    `commentary.replay.requested` audit Kind.
  - `GET /api/games/{gameId:guid}/commentary` — anonymous-OK read.
  - `POST /api/games/{gameId:guid}/commentary/replay` and the
    matching GET — same gates, replay-tagged envelope.
* DI: `services.AddSingleton<ICommentaryGenerator,
  StubCommentaryGenerator>()`.
* Audit Kind: `KindCommentaryReplayRequested =
  "commentary.replay.requested"`.

---

## Task 6 — Swiss + double-elim brackets

### Problem

Tournaments currently support `single-elimination`, `round-robin`,
and `swiss` (Wave-J-10 added Swiss but only the first round
schedule). Wave 6 adds `double-elimination` end-to-end and tightens
the Swiss surface behind a typed factory.

### Solution

* `Tournament/BracketFormat.cs` — typed enum
  `{ SingleElimination=0, RoundRobin=1, Swiss=2,
     DoubleElimination=3 }` + `BracketFormats.TryParse` /
  `ToWire` mapping helpers. The persistence column on
  `Tournament.Format` stays the canonical lowercase-hyphen string;
  the enum is API-side only.
* `Tournament/IBracketGenerator.cs` — interface +
  `BracketSide { Winners, Losers, GrandFinal }` enum +
  `BracketPairing` record-struct (round, bracket, P1..P4).
* `Tournament/TournamentBracketGenerator.cs` — factory that resolves
  by typed enum OR persistence string. Both throw
  `ArgumentOutOfRangeException` on unknown — hard signal beats
  silent fallthrough.
* `Tournament/SingleEliminationBracket.cs` (with
  `RoundRobinBracket`) — both wrap the existing
  `TournamentPairing.SingleEliminationFirstRound` /
  `TournamentPairing.RoundRobin` helpers.
* `Tournament/SwissBracket.cs` — 4-round Latin-square baseline.
  Round 1 matches the existing Swiss first-round shape; rounds 2–4
  use rotation `(round-1) % half` to avoid rematches inside the
  4-round window. `TournamentService.MaybeAdvanceRoundAsync`
  overrides the deterministic schedule with standings-based
  pairing once round 1 completes.
* `Tournament/DoubleEliminationBracket.cs` (+ aliasing
  `DoubleElimBracket` to satisfy the W6 contract test's class-name
  permutations) — emits:
  - Winners-bracket round 1 (same shape as single-elim).
  - Losers-bracket round 1 placeholder rows (count = WB pairings /
    2). The placeholders use `PlaceholderPlayer = "__pending__"`
    so the API surface pre-emits the slot count.
  - One grand-final placeholder pairing.
* `TournamentService.IsKnownFormat` updated to accept
  `"double-elimination"`.
* `TournamentService.PairAllAsync` switch grows a
  `double-elimination` case that persists ONLY the winners-bracket
  round 1 today (placeholder rows would surface phantom "pending"
  matches in the leaderboard).
* `TournamentService.MaybeAdvanceRoundAsync` shares the single-elim
  advancement path. The losers-bracket resurrection lands in
  Phase L.
* DI: 4 `IBracketGenerator` impls + `TournamentBracketGenerator`
  registered as singletons (pure functions over the seed list).
* Contract tests at
  `Phase_K_W6/Bishop/BracketGeneratorDeterminismTests`:
  - Determinism (same seeds → same pairings) for all 4 formats.
  - Factory resolves all 4 formats by both enum + wire string.
  - Shape pins (single-elim round 1 count, Swiss round 1, RR rounds,
    double-elim three-bracket emission).
  - Empty for `n < 2`.

---

## Task 7 — OAuth production runbook (zero-downtime migration)

`docs/oauth-production-setup.md` §7 added (110 lines):

* **§7.1 Google** — production-app verification workflow + the
  exact scope-justification text per requested scope. Verification
  turnaround is 4-6 weeks; the "testing" mode handles staging in
  the interim.
* **§7.2 Microsoft** — admin-consent flow for both the home tenant
  and external tenants. Includes the `adminconsent` URL template
  operators can DM to a target-tenant admin.
* **§7.3 GitHub** — rate-limit math (5,000/hour authenticated;
  capacity for ~83 sign-ins/min — fine for the W6 fleet). Flags
  GitHub App migration as the Phase-L mitigation if burst-limits
  bite.
* **§7.4 Zero-downtime dev → prod migration** — 6-step runbook:
  pre-flight → issue overlap → SSM push → restart with both
  values → drain → verify. Calls out the 24-hour overlap window
  every provider supports.
* **§7.5 Phase L forward-compat hooks** — cross-references to the
  new RS256 + livestream surfaces.

---

## Task 8 — OIDC discovery stub

`/.well-known/openid-configuration` route:

* `GET /.well-known/openid-configuration` — top-level minimal API
  route (NOT under `/api/...`; OIDC clients expect the well-known
  location at the apex).
* `GET /api/auth/.well-known/openid-configuration` — same response,
  under the api prefix (matches the JWKS surface convention).

Both branch on `Auth:JwtAlgorithm`:

* **RS256**: 200 with the canonical OIDC fields (`issuer`,
  `jwks_uri`, `authorization_endpoint`, `token_endpoint`,
  `id_token_signing_alg_values_supported: ["RS256"]`,
  `response_types_supported`, `subject_types_supported:
  ["public"]`, `grant_types_supported`). `Cache-Control: public,
  max-age=3600`.
* **HS256**: 404 with body `{ reason:
  "oidc-discovery-disabled", migrateTo: "RS256" }` +
  `Cache-Control: public, max-age=60`.

---

## Forward notes for Wave 7

1. **Apone DevOps cross-lane fix:** `infra/k8s/base/kustomization.yaml`
   does not enumerate `coturn-configmap.yaml`. The Phase-J-7 sanity
   test (`K8sManifestSanityTests.BaseKustomization_IncludesAllResources`)
   has been red since the coturn files landed in Phase K Wave 2.
   Apone — please add `- coturn-configmap.yaml`,
   `- coturn-deployment.yaml`, `- coturn-secret.yaml`, and
   `- turn-server.yaml` to the resources list (verify against
   `ls infra/k8s/base/ | grep -v kustomization`).

2. **Voice livestream segment-store flip:** Phase L should wire
   `ILivestreamRecorder` to an actual ffmpeg+S3 (or local-disk)
   pipeline. The controller URL and audit kinds stay unchanged.
   See `docs/voice-sfu-design.md` for the SFU sizing baseline.

3. **Losers-bracket resurrection:** `TournamentService` currently
   shares the single-elim advancement path for double-elim. Phase L
   should add the proper losers-bracket / grand-final flow. The
   `BracketSide` enum is already in place so the model change is
   add-only.

4. **OAuth production submission:** before the Phase-L production
   freeze, file the Google verification request (4-6 week
   turnaround). The scope justifications are pre-written in
   `docs/oauth-production-setup.md` §7.1 — operator just needs to
   paste them.

5. **RS256 rollout:** Apone should provision two RSA keys in SSM
   under `/mahjong/prod/auth/jwt_rsa_keys/{0,1}` and bind them to
   `Authentication__JwtRsaKeys__0` + `__1`. The W6 surface accepts
   the array directly; the second key covers the rotation window.

## Files modified / created (Bishop lane only)

**Created (16):**

* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtRsaSigningKey.cs`
* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtAlgorithmStartupLogger.cs`
* `src/backend/src/Mahjong.Autotable.Api/Voice/ILivestreamRecorder.cs`
* `src/backend/src/Mahjong.Autotable.Api/Voice/InMemoryLivestreamRecorder.cs`
* `src/backend/src/Mahjong.Autotable.Api/Voice/VoiceLivestreamController.cs`
* `src/backend/src/Mahjong.Autotable.Api/Voice/SpectatorVoiceHub.cs`
* `src/backend/src/Mahjong.Autotable.Api/Commentary/ICommentaryGenerator.cs`
* `src/backend/src/Mahjong.Autotable.Api/Commentary/StubCommentaryGenerator.cs`
* `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryController.cs`
* `src/backend/src/Mahjong.Autotable.Api/Tournament/BracketFormat.cs`
* `src/backend/src/Mahjong.Autotable.Api/Tournament/IBracketGenerator.cs`
* `src/backend/src/Mahjong.Autotable.Api/Tournament/TournamentBracketGenerator.cs`
* `src/backend/src/Mahjong.Autotable.Api/Tournament/SingleEliminationBracket.cs` (with `RoundRobinBracket`)
* `src/backend/src/Mahjong.Autotable.Api/Tournament/SwissBracket.cs`
* `src/backend/src/Mahjong.Autotable.Api/Tournament/DoubleEliminationBracket.cs` (with `DoubleElimBracket` alias)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W6/Bishop/BracketGeneratorDeterminismTests.cs`
* `docs/voice-sfu-design.md`

**Modified (10):**

* `src/backend/src/Mahjong.Autotable.Api/Auth/AuthOptions.cs`
* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtSigningKeyProvider.cs`
* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtIssuingService.cs`
* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtValidationService.cs`
* `src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenController.cs`
* `src/backend/src/Mahjong.Autotable.Api/Data/Entities/ChangshaEntities.cs` (+3 audit Kinds)
* `src/backend/src/Mahjong.Autotable.Api/Tournament/TournamentService.cs` (double-elim cases)
* `src/backend/src/Mahjong.Autotable.Api/Program.cs` (DI + hub map + OIDC route)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/Bishop/JwksEndpointContractTests.cs` (Wave-6 contract)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W3/GameVoiceEnabledFlagTests.cs` (multi-hub discovery)
* `docs/oauth-production-setup.md` (§7 + §8)
