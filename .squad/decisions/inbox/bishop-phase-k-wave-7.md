# Bishop — Phase K Wave 7

**Branch:** `stlong/phase-k-wave-7-bringup`
**Scope:** backend — Phase K Wave 7 bring-up. Seven scoped
deliverables:

1. RS256 JWT end-to-end hardening + rotation drill
   (issuer claim, OIDC discovery hard contract, alg-confusion
   guard pin, JWKS shape pin).
2. Losers-bracket algorithm — full upper/lower/grand-final +
   reset-game slot, deterministic placeholder naming.
3. ffmpeg HLS livestream pipeline (`FfmpegHlsRecorder` swap-in
   for the W6 in-memory stub; opt-in via
   `Voice:LivestreamRecorderImpl`).
4. Phase L commentary JSON contract (`CommentaryRecord` DTO +
   `/api/replay/{id}/commentary/replay` records endpoint).
5. OIDC discovery hard contract (`Auth:Issuer` config knob;
   issuer-aware discovery doc).
6. JWT rotation §8 RS256 key provisioning runbook
   (`docs/jwt-rotation.md` §8 + `docs/jwt-ssm-runbook.md`
   operator cheat-sheet).
7. Google OAuth verification playbook
   (`docs/google-oauth-verification.md`).

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` → **Passed: 1505, Failed: 1, Skipped: 0** (~1m 39s).
Baseline at session start was 1422/0/0. **+83 net passing**.

> The single failure
> (`Phase_K_W5.HicksW5FrontendContractTests.ThreeRenderer_ModulePresent_HardAssert`)
> is **outside Bishop's lane** — it lives in
> `src/frontend/autotable-src/src/three-renderer.ts` (Hicks
> Frontend). The W5 contract test pins that the file MUST carry a
> `import … from 'three'` statement; the current Hicks working-tree
> three-renderer.ts is comment-only. The Bishop-lane working copy
> never touched this file. Forwarded to Hicks via the W8
> forward-notes section.

> **Author hygiene — Wave-6 rule carried forward.** Every commit
> uses inline identity:
> `git -c user.name="Bishop (Backend)" -c user.email="bishop@squad.mahjong" commit -m …`
> Multi-step sequences wrap in
> `flock -w 120 9 … 9>/tmp/squad-git-lock` to serialize against
> Apone / Hicks / Vasquez sibling agents.

---

## Task 1 — RS256 JWT E2E hardening + rotation drill

### Problem

Wave 6 shipped the RS256 toggle + JWKS surface + OIDC discovery
endpoint but pinned only the **shape** of those surfaces. Wave 7's
acceptance criteria requires the **operational drill**: an SRE must
be able to (a) generate a new RSA keypair, (b) rotate it into the
active slot, (c) confirm pre-rotation tokens still validate, and
(d) confirm new tokens are minted under the new `kid`. None of
this was test-pinned.

### Solution

* **`AuthOptions.Issuer`** — new string property. Bound in
  `Program.cs` with the same `Auth:` / `Authentication:` fallback
  pattern as the rest of the auth section.
* **`JwtSigningKeyProvider.ConfiguredIssuer`** — accessor reads
  `options.Issuer` trimmed at construction. Empty means "fall back
  to the request origin" in OIDC discovery; the `iss` claim is only
  stamped when non-empty so HS256 baseline tokens stay shape-
  compatible with the Wave-4 verifier.
* **`JwtIssuingService.IssueAsync`** stamps the `iss` claim when
  `ConfiguredIssuer` is non-empty. The claim sits next to `sub` /
  `iat` / `exp` and is preserved through both HS256 and RS256
  branches.
* **OIDC discovery endpoints** — both the controller action and the
  minimal-API route now resolve the `issuer` field as
  `ConfiguredIssuer ?? ${scheme}://${host}`. The fallback means an
  operator can ship RS256 to staging WITHOUT setting
  `Auth:Issuer` (and get a self-describing discovery doc), AND
  override it cleanly for production behind a load balancer where
  the `Host` header doesn't reflect the public hostname.

### Contract tests

* **`Phase_K_W7/Bishop/JwtRotationE2ETests.cs`** (3 facts, all
  hard-assert):
  - `FullRotation_KeyA_To_KeyB_LegacyTokensStillValidate` — issue
    token under key A, rebuild provider with `[B, A]`, validate
    legacy token (MUST succeed with kid=A), mint new token (MUST
    have kid=B), confirm JWKS publishes BOTH keys.
  - `FullRotation_AlgorithmConfusionAttack_Rejected` — forges a
    JWT with `alg=HS256` but the RSA public key bytes as the HMAC
    secret (classical CVE-2015-9235). Validator MUST reject.
  - `Jwks_NAndE_Are_Base64UrlNoPadding_Per_Rfc7517` — pins the wire
    shape so downstream verifiers (Auth0 / Cognito / jose-jwt /
    pyJWT) parse the JWKS without manual padding fixups. Asserts
    the decoded modulus bytes match the actual key parameters.
* **`Phase_K_W7/Bishop/OidcDiscoveryHardContractTests.cs`**
  (Vasquez pre-stage, now hard-asserting): HS256 → 404 with
  structured reason; RS256 → 200 with `issuer` / `jwks_uri` /
  `token_endpoint` / `grant_types_supported`.
* **`Phase_K_W7/Bishop/RS256HappyPathTests.cs`** (Vasquez
  pre-stage): exercises the HTTP layer + JWKS shape. Soft-passes
  the unauthenticated POST path; hard-asserts JWKS keys when
  RSA keys are configured.

---

## Task 2 — Losers-bracket algorithm

### Problem

W6 shipped `DoubleEliminationBracket.Generate` emitting only the
winners-bracket round 1 + a 2-slot losers-bracket stub + a single
grand-final slot. W7 needs the full bracket so the tournament UI
can render the schedule end-to-end before any games start, and so
the service layer can resolve placeholders as games complete.

### Solution

`DoubleEliminationBracket.Generate` now emits:

* **Winners bracket** — rounds 1 through `k = ceil(log2(N))`.
  Round 1 carries the actual seeds (with bye placeholders when N
  is not a power of two); rounds 2..k carry winner placeholders
  (`__pending_wb_r{r}_m{m}_p{slot}__`).
* **Losers bracket** — `2*(k-1)` rounds in a strict drop-tier
  pattern. Tier j (1..k-1) contributes `wbMatchesPerRound[j+1]`
  matches in BOTH round `2j-1` (the consolidation game between
  prior LB survivors) and round `2j` (the WB-feed game). For 8
  seeds → `[2, 2, 1, 1]` = 6 LB matches.
* **Grand final** — round 1 (WB-champion vs LB-champion) + round 2
  (the "reset" game). Round 2's both slots carry the dedicated
  `GrandFinalResetPlaceholder` constant so the service layer can
  distinguish reset slots from ordinary placeholders without
  re-running the round counter.
* **`BracketDepth(int playerCount)`** — exposed helper returning
  `ceil(log2(N))` for N ≥ 2. Used by the test layer to derive the
  expected round count.

For 8 seeds → **15 pairings** (7 WB + 6 LB + 2 GF).
For 16 seeds → **31 pairings** (15 WB + 14 LB + 2 GF).

### Contract tests

* **`Phase_K_W7/Bishop/LosersBracketDeterminismTests.cs`** (Vasquez
  pre-stage, now hard-asserting): LB non-empty for 8 seeds,
  deterministic across calls, monotone in seed count.
* **`Phase_K_W7/Bishop/LosersBracketGrandFinalResetTests.cs`** (new
  Bishop pin):
  - GF emits BOTH final + reset for 8 seeds (exactly 2 GF
    pairings).
  - GF reset carries the `GrandFinalResetPlaceholder` literal
    (not the generic `__pending__`).
  - GF final carries `__pending_wb_champion__` / `__pending_lb_champion__`.
  - Reset is emitted for every power-of-two seed count (4, 8, 16).
  - Deterministic across calls for 16 seeds.
  - `BracketDepth` matches `ceil(log2(N))` for [2, 4, 5, 8, 16, 32].
* **`Phase_K_W6/Bishop/BracketGeneratorDeterminismTests.cs`**
  (existing) — `Double_elimination_emits_winners_losers_and_grand_final_slots`
  updated to expect the W7 expanded shape (15 pairings + new
  placeholder names). The original W6 7-pairing pin would have
  caught the algorithm change; updating it in Bishop's own subdir
  is on-pattern per the W6 lane-discipline rules.

---

## Task 3 — ffmpeg HLS livestream pipeline

### Problem

W6 shipped `InMemoryLivestreamRecorder` — a stub that accepted
PCM frames and emitted in-memory segments without actually
producing HLS. W7 swaps in a real ffmpeg-backed implementation
that produces playable `.m3u8` + `.ts` segments while keeping the
stub as the test default (so CI doesn't depend on ffmpeg).

### Solution

* **`Voice/IFfmpegHealthProbe.cs`** — interface +
  `FfmpegBinaryHealthProbe` implementation. Runs `ffmpeg -version`
  with a 2-second timeout and caches the result for the process
  lifetime. Returns false on any failure mode (binary missing /
  permission denied / non-zero exit).
* **`Voice/FfmpegHlsRecorder.cs`** (~340 lines) — concrete
  `ILivestreamRecorder` implementation. Spawns a per-game ffmpeg
  subprocess reading PCM s16le 48kHz stereo from stdin, muxing
  AAC 128k into HLS segments with sliding-window + delete_segments
  + omit_endlist. Segment pattern `seg-%05d.ts`, playlist
  `playlist.m3u8` per game subdirectory. Graceful stop sends
  `q\n` to stdin with a 3-second grace then SIGKILL. The
  per-game working directory is rooted at
  `Voice:LivestreamWorkingDirectory` (default `voice-livestream`)
  and the `GetSegment` lookup is directory-traversal-guarded via
  `Path.GetFullPath` comparison.
* **`Voice/VoiceOptions`** — four new properties:
  - `LivestreamRecorderImpl` (`"InMemoryStub"` | `"FfmpegHls"` —
    default `"InMemoryStub"`).
  - `LivestreamSegmentSeconds` (default 6, clamped [2..30]).
  - `LivestreamPlaylistSegmentCount` (default 5, clamped [2..30]).
  - `LivestreamWorkingDirectory` (default `"voice-livestream"`).
* **`Program.cs` DI** — when
  `Voice:LivestreamRecorderImpl == "FfmpegHls"`, a boot-time
  `FfmpegBinaryHealthProbe` runs and throws
  `InvalidOperationException` if ffmpeg is missing. Unknown values
  fall back to the stub with a warning. The default (`InMemoryStub`
  or unset) preserves Wave-6 behaviour.

### Contract tests

* **`Phase_K_W7/Bishop/FfmpegHlsRecorderHealthcheckTests.cs`**
  (Vasquez pre-stage, now hard-asserting):
  - `FfmpegHlsRecorder` exists, is a concrete class, exposes
    `StartAsync` / `RecordAsync` OR implements `IHostedService`.
  - Healthcheck tag axis present-or-forward-staged.

### Operational notes

`docs/voice-livestream.md` (if it exists) and the W7 forward-notes
should call out:

* ffmpeg subprocess management — Bishop's implementation uses
  `Process.Start` with stdin/stdout/stderr redirected. The graceful
  stop path is `q\n` to stdin (ffmpeg's interactive quit) then a
  3-second wait then `Kill()`.
* CI does NOT install ffmpeg by default. The default
  `InMemoryStub` impl is selected so all CI builds + the Wave-7
  test gate pass without ffmpeg.
* Apone owns the production Helm chart edit to set
  `Voice__LivestreamRecorderImpl=FfmpegHls` + bake ffmpeg into
  the container image. Forwarded.

---

## Task 4 — Phase L commentary JSON contract

### Problem

W6 shipped `ICommentaryGenerator` + `StubCommentaryGenerator` + a
single `/api/replay/{id}/commentary` envelope endpoint. Phase L
needs a canonical record shape (per-turn play-by-play / color /
analyst beats) so the spectator UI can render commentary as a
scrollable feed AND so a future Bedrock-backed generator can emit
records into the same JSON contract.

### Solution

* **`CommentaryRecord`** record in
  `Commentary/ICommentaryGenerator.cs`:
  ```csharp
  public sealed record CommentaryRecord(
      int TurnNumber,
      string Phase,
      string Speaker,
      string Text,
      string? Emotion = null,
      string? TileRef = null);
  ```
* **`CommentaryPhases`** static class — string vocabulary
  (`Draw` / `Discard` / `Claim` / `Win`) so the record producer +
  consumer agree without a hard-coded enum on either side.
* **`CommentarySpeakers`** static class — string vocabulary
  (`PlayByPlay` / `Color` / `Analyst`).
* **`ICommentaryGenerator.GetRecordsAsync(Guid gameId)`** — new
  interface method returning `Task<IReadOnlyList<CommentaryRecord>>`.
* **`StubCommentaryGenerator.GetRecordsAsync`** — returns a single
  placeholder record per game so the surface is non-empty during
  development.
* **`CommentaryController`** — split routes:
  - `GET /api/replay/{id}/commentary` → unchanged W6 envelope.
  - `GET /api/replay/{id}/commentary/replay` → new W7 records
    list endpoint.
  - `POST` endpoints unchanged.
* **`tests/Shims/CommentaryGeneratorTestShim.cs`** — additive
  `GenerateRecords(string gameId)` method producing 4 deterministic
  records following the Phase / Speaker vocabularies. Vasquez owns
  the rest of the shim; this method was added per the W7 brief's
  explicit delegation note.

### Contract tests

* **`Phase_K_W7/Bishop/CommentaryRecordContractTests.cs`** (Vasquez
  pre-stage, now hard-asserting): type exists + is not enum, carries
  `Speaker` + `Text` + an ordering axis (`Sequence` / `TurnNumber`
  / `Index` / `Order`), JSON round-trips through System.Text.Json.

---

## Task 5 — OIDC discovery hard contract

Folded into Task 1 (Issuer support + discovery endpoint). The
Vasquez pre-stage test
`OidcDiscoveryHardContractTests.cs` now hard-asserts both arms
end-to-end.

---

## Task 6 — JWT rotation §8 RS256 key provisioning

### Problem

`docs/jwt-rotation.md` documented the HS256 fallback-list pattern
through §7 but stopped there. Operators wanting to ship RS256 had
no runbook for keypair generation, SSM Parameter Store topology,
or the rotation procedure on the RSA path.

### Solution

* **`docs/jwt-rotation.md` §8** (new) — "RS256 key provisioning
  (Phase K Wave 7)". Six subsections covering keypair generation
  (OpenSSL PKCS#1 → PKCS#8), SSM Parameter Store topology (active
  / previous / archive slots), ESO ExternalSecret mount (additive
  to the existing `mahjong-jwt-keys` Secret), algorithm flip
  procedure, rotation procedure (4-step SSM shuffle + ESO
  force-sync + pod restart), AWS KMS asymmetric-keypair alternative
  (documented for Wave 8/9), and lost-key recovery.
* **`docs/jwt-rotation.md` §9** (renumber) — Cross-references
  preserved.
* **`docs/jwt-ssm-runbook.md`** (new) — operator-facing cheat-sheet
  cross-referenced from §8 and the Vasquez W7 filesystem contract
  test. Carries the bash commands in copy-pasteable form for first-
  time provisioning, rotation, and emergency rotation, plus the IAM
  permissions block.

### Contract tests

* **`Phase_K_W7/Bishop/JwtOperationalDocsContractTests.cs`**
  (Vasquez pre-stage): looks for `docs/jwt-ssm-runbook.md` (now
  present) + asserts the doc mentions "SSM" / "Parameter Store" +
  "rotat" — hard-asserts after this commit.

---

## Task 7 — Google OAuth verification playbook

### Problem

The W6 OAuth client is in Google's **Testing** state (≤ 100
external users, "unverified app" warning screen). Moving to
**In production** requires submitting a verification request
through Google Cloud Console; the most common failure mode is a
malformed submission (missing scope justification / incomplete
privacy policy / demo video doesn't show the consent screen). The
W7 brief calls for a playbook so the SRE / product team can run
the submission without reverse-engineering Google's checklist.

### Solution

* **`docs/google-oauth-verification.md`** (new, 9 sections):
  - Prerequisites table (8 line items: privacy URL, terms URL,
    homepage, logo, authorized domain, redirect URIs, demo video,
    scope justification).
  - Scope inventory (`openid` / `userinfo.email` /
    `userinfo.profile` — all non-sensitive) + explicit listing of
    NOT-requested scopes to pre-empt scope-reduction queries.
  - Authorized-domain verification via Search Console (DNS TXT).
  - Copy-paste scope justification body (~250 words).
  - 90-second demo video script with per-beat voiceover.
  - Submission checklist.
  - Common rejection reasons + fixes table.
  - Post-approval operations (rotation impact + scope-expansion
    cost projection).
  - Cross-references.

### Contract tests

* **`Phase_K_W7/Bishop/JwtOperationalDocsContractTests.cs`**
  (Vasquez pre-stage): looks for
  `docs/google-oauth-verification.md` + asserts it mentions
  "verif" + "google" — hard-asserts after this commit.

---

## Forward notes — Phase K Wave 8 candidates

* **Hicks Frontend**: `Phase_K_W5.HicksW5FrontendContractTests.ThreeRenderer_ModulePresent_HardAssert`
  fails on the working tree because
  `src/frontend/autotable-src/src/three-renderer.ts` lost its
  `import … from 'three'` statement somewhere between the W6 close
  and the W7 working state. Hicks should restore the canonical
  static import.
* **Apone DevOps**:
  - Helm-chart edit to set
    `Voice__LivestreamRecorderImpl=FfmpegHls` + bake ffmpeg into
    the container image (currently the runtime opt-in is wired but
    the production image doesn't carry ffmpeg).
  - ESO ExternalSecret edit at
    `infra/k8s/overlays/prod/jwt-keys-secret.yaml` to add the three
    new RSA SSM mounts (`Auth__JwtRsaKeys__{0,1,2}`). Schema
    documented in `docs/jwt-rotation.md` §8.3.
* **Bishop (next wave)**:
  - **AWS KMS asymmetric signing** — replace the in-process RSA
    signer with `kms:Sign` so the private key lives in an HSM.
    Documented in `docs/jwt-rotation.md` §8.5.
  - **Issuer-rooted JWKS URI in discovery doc** — currently
    `jwks_uri` is composed from the request origin. When
    `ConfiguredIssuer` is set, the URI should be rooted at the
    issuer host so a load balancer that rewrites paths doesn't
    break the discovery contract.
  - **CommentaryRecord persistence** — the new DTO is in-memory
    only via `StubCommentaryGenerator`. Phase L needs a real
    persistence path (DbSet + EF Migration) so replay records
    survive a pod restart.

---

## Bishop history

History.md appended with the W7 entry following the W6 pattern.

**Memo:** `.squad/decisions/inbox/bishop-phase-k-wave-7.md` —
per-deliverable design, contract-test coverage, forward-looking
notes including the Hicks Frontend three-renderer.ts cross-lane
fix.
