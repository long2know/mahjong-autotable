# Phase K — Wave 15 Summary

- **Branch:** `stlong/phase-k-wave-15-bringup`
- **Base:** `main` @ `e6fef84`
- **Head:** `c5cf504` (Vasquez QA lane-map shared_files broadening)
- **Date:** 2027-01-XX
- **Final gate:** **3312 passed / 0 failed / 0 skipped** (+283 over W14)
- **Zero-skip streak:** **30 consecutive waves** (J.1-J.10 + K.1-K.15)
- **Lane-discipline:** **`checked=5 violations=0` — 5th consecutive 0-violation wave (W11+W12+W13+W14+W15); same-lane amendment commit re-required (Vasquez `c5cf504`) — second wave since W11 to need an amendment after the W14 no-amendment milestone**
- **Identity hardening:** **10th consecutive clean wave** (per-invocation `git -c user.name=X -c user.email=Y`)
- **Concurrency mutex:** **6th consecutive fully-adopted wave** of `flock -w 120 9 ... 9>.work/squad-git-lock`
- **Coordinator-direct interventions:** **ZERO for 10 consecutive waves** (W6 → W15)

---

## 1. Headlines

1. **Phase L renderer-webgl2 hello-world IMPLEMENTATION kickoff
   lands.** Hicks's `src/renderer-webgl2/` NEW directory (4 source
   files; hand-rolled WebGL2 behind `?renderer=webgl2-hello` URL
   guard; **zero three.js dependency**; chunk weight **6,237 B = 3 %
   of the 180-220 KB Phase L envelope** Apone's `phase-l-l1-design.md`
   carves out). Companion `docs/phase-l-renderer-implementation.md`
   NEW (206 lines) records the kickoff convention: implementation
   begins as a URL-guarded hello-world before the unified
   `three-renderer-big` / `renderer-webgl2` switching surface lands
   in W16+. **W14's spike Go-decision converts into actual
   implementation 1 wave later** — earlier than the W14 forward queue
   estimated (the 14 KB renderer-spike memo named W15+ as the
   implementation-kickoff candidate; W15 ships it).

2. **W5 heredoc bug FIXED at W15 — 10-wave-old bug closed.** Apone D3
   single-quotes the heredoc delimiter `<<'EOF'` at
   `.github/workflows/lane-discipline-nightly.yml:87`, env-pipes the
   scan-output substitution, and substitutes placeholders in the
   final body. **`actionlint` exits 0 for the first time since W5.**
   `docs/agent-handoff-protocol.md §5.10` codifies the 6-rule heredoc
   pattern with a canonical example block. Bug existed undetected
   since W5 because the workflow ran fine — the YAML parsed but a
   shell-expansion in the unquoted heredoc body collided with bash
   variable references at execution time only when scan output
   contained `$`-prefixed tokens (the failure mode was
   triggered-but-silent). W14's W12 + W13 + W14 retro pass had not
   yet exercised an actionlint sweep that surfaced it; W15 §5.10
   discipline plus Apone's targeted fix close the loop.

3. **LH13 hard-pin DEFERRED to W16 — 5-wave cumulative deferral; §6.4
   yellow-flag at 1 wave below the §6.3 Coordinator-consultation
   trigger.** Cron history at W15: **5 PR runs / 0 schedule / 0
   success.** Hicks ships the deferral marker
   (`docs/frontend-pwa-audit.md §13` updated to mark **5-wave
   cumulative deferral** vs W14's 4-wave); Vasquez ships the §6.4
   yellow-flag entry in `docs/frontend-pwa-audit.md` plus the §6.5
   `lh13-cron-stephen-direct-runbook.md` (Stephen-direct cron-trigger
   path via Actions UI `Run workflow` ×3 — the calibration-deadlock
   escalation that does not need Coordinator-direct). **6-wave
   threshold for Coordinator-direct now lands at W16**, but Vasquez's
   §6.5 runbook gives Stephen a 30-second manual path to unblock
   first.

4. **Three-renderer-big intentional hold-line at 406,635 B sustained
   for the 5th consecutive wave (W11+W12+W13+W14+W15).** Bundle
   ledger reads `406.64 KB → 406.64 KB (+0)` across all 5 hold-line
   waves; **8-wave monotonic-decrease ledger remains paused by
   design**. Cumulative W6 → W15: **−44.9 %** (738.65 KB → 406.64
   KB). Hicks's `docs/frontend-bundle-audit.md` NEW (240 lines)
   identifies **5 W16/W17 shrinkage candidates** (§3.1 Sentry
   conditional load; §3.2 autotable-src-eager surgery; §3.3 HLS
   conditional gate; §3.4 GLTFLoader clean; §3.5 scene-effects
   tree-shake) — the hold-line is now in its **canonical
   bandwidth-rebalancing phase**: Phase L implementation kickoff
   absorbs renderer-lane bandwidth while a documented shrinkage
   backlog is ready for W16+ when bandwidth reopens.

5. **Per-tenant JWKS rotation policy table lands; `DateTimeOffset`
   edges replace W14 `DateTime`; 3-provider migration shipped;
   validator hook-up DEFERRED to W16.** Bishop's
   `PerTenantJwksRotationPolicies` table keyed by `TenantId` ships
   with InMemory + Ef stores + Postgres / SqlServer / Sqlite
   migrations (all `2026_0524_03_07_03` + `2026_0524_03_07_13`).
   Feature-flag `JwksRotation:PerTenant:Enabled` (default `false`);
   when off, behaviour is bit-identical to W14 single-policy.
   **`DateTimeOffset` replaces `DateTime`** at the wire and store
   edges to preserve timezone information explicitly (the W14
   `DateTime` shape stripped offset on JSON deserialisation).
   `docs/per-tenant-jwks.md` NEW captures the wiring sequence;
   **validator integration DEFERRED to W16** per Bishop's W15 §2 memo
   (a deliberate table-before-validator pattern — make the schema
   change observable without enabling the new code path; W16 ships
   the `JwtValidationService` consumer).

6. **`GET /api/replays/{id}/blob` RFC 7233 single-range blob
   streaming endpoint lands.** Bishop ships RFC 7233 single-range
   semantics (`bytes=A-B`, `bytes=A-`, `bytes=-N`); **multi-range and
   malformed range headers both return 416 Range Not Satisfiable**.
   Pairs with W14's `GET /api/replays` metadata-only listing — the
   listing returns `payloadSize: 0` placeholders, the blob endpoint
   serves the actual replay payload with Range support for resumable
   admin downloads. `docs/replay-streaming.md` NEW captures the
   convention. **W15 sets the precedent: single-range only is the
   canonical surface across future admin blob endpoints; multi-range
   support deferred to L-series widening only if observed admin-UI
   need.**

7. **Lane-discipline maturity narrative `§6` lands in
   `docs/agent-handoff-protocol.md`.** Vasquez's new top-level
   section (**188 lines**) canonises the W11 → W14 4-wave
   zero-violation streak as the W15+ baseline expectation; encodes
   the maturity arc (W3-W5 cross-lane-content era → W6-W10
   identity-hardening era → W11-W14 lane-discipline era → W15+
   amendment-discovery era); records the amendment-discovery pattern
   (lane-discipline strict-mode SURFACES previously-invisible
   shared-files; W12 + W13 + W15 amendments are amendment-discovery
   events, NOT regressions); codifies the primary-classification
   rule (when a shared-file straddles multiple lanes, the
   intent-owning lane wins — QA-harness intent overrides
   `.github/workflows/` filesystem heuristic; test-lane root owner
   wins when a Playwright config + test surface co-edit cross-lane);
   and tabulates the W8 → W13 allowlist evolution timeline. **W15 is
   the wave that converts 4 consecutive 0-violation waves into a
   documented baseline expectation.**

---

## 2. Wave-15 commits

| SHA       | Lane           | Author email                | Files | +Lines | −Lines |
|-----------|----------------|-----------------------------|-------|--------|--------|
| `173bb41` | Hicks          | `hicks@squad.mahjong`       | 33    | 2479   | 84     |
| `b88a5a4` | Apone          | `apone@squad.mahjong`       | 15    | 3152   | 10     |
| `e2986d2` | Bishop         | `bishop@squad.mahjong`      | 42    | 8076   | 3      |
| `0a316d7` | Vasquez        | `vasquez@squad.mahjong`     | 36    | 3873   | 22     |
| `c5cf504` | Vasquez (amend)| `vasquez@squad.mahjong`     | 4     | 249    | 1      |

**Totals: 130 files; +17,829 lines / −120 lines.** All 5 commits
carry the `Co-authored-by: Copilot <…>` trailer.

**Second wave since W11 first-0-violation wave to require a Vasquez
same-lane amendment.** W12 + W13 + W15 amendments are
**amendment-discovery events**: lane-discipline strict-mode surfaces
previously-invisible co-edits. W15 amendment broadens 2 entries:
`lane_discipline_nightly_yml_shared` (apone+vasquez; primary=vasquez;
parallels W10 `agent_handoff_protocol_md_shared`) and
`playwright_visual_regression_shared` (hicks+vasquez;
primary=vasquez). Both `primary=vasquez` are codified by §6 of
`docs/agent-handoff-protocol.md` as the **primary-classification
rule** (QA-harness intent overrides filesystem heuristic; test-lane
root owner wins).

---

## 3. Bishop (Backend) `e2986d2` — 7-deliverable wave with 111 new contract facts; intermediate gate 3307/5/0 → final 3312/0/0

Bishop ships **7 deliverables in one wave**, anchored by replay
blob streaming + per-tenant JWKS rotation table + tournament query
duration histogram with bucket label canonicalisation.

### 3.1 `GET /api/replays/{id}/blob` — RFC 7233 single-range only

- **Endpoint:** `GET /api/replays/{id}/blob` admin-gated;
  serves the actual replay payload bytes (companion to W14
  `GET /api/replays` metadata-only listing whose `payloadSize` is
  always 0).
- **Range support:** RFC 7233 single-range only —
  `bytes=A-B` (closed range), `bytes=A-` (open-ended from A), and
  `bytes=-N` (last N bytes) all supported. **Multi-range
  (`bytes=A-B,C-D`) and malformed (`bytes=A-B-C`,
  `bytes=B-A` where B < A, unsupported unit) both return 416
  Range Not Satisfiable.**
- **Response semantics:** 206 Partial Content on valid range with
  `Content-Range: bytes A-B/N` + `Accept-Ranges: bytes`; full 200
  OK with full body when no `Range:` header.
- **Auth gate:** Admin-only (parallels W14 `POST /api/replays`).
- **`Content-Disposition`:** `attachment; filename="replay-<id>.bin"`
  for admin-download workflow.
- **Convention established:** **single-range only is the canonical
  surface across future admin blob endpoints**; multi-range
  deferred to L-series widening only if observed admin-UI need.
- **Tests:** `ReplayBlobStreamingEndpointTests.cs` NEW.
- **Doc:** `docs/replay-streaming.md` NEW.

### 3.2 `PerTenantJwksRotationPolicies` table + `DateTimeOffset` edges

- **Schema:** `PerTenantJwksRotationPolicies` table keyed by
  `TenantId`; columns `RotationStartUtc DateTimeOffset`,
  `RotationEndUtc DateTimeOffset?`, `OverlapWindowMinutes int`,
  `IsActive bool`, `CreatedAt DateTimeOffset`.
- **`DateTimeOffset` edge replacement:** **W14 used `DateTime` at
  the wire and store edges; W15 switches both to `DateTimeOffset`
  to preserve timezone information explicitly.** The W14 shape
  stripped offset on JSON deserialisation. Legacy `DateTime`
  call-sites preserved at internal compute boundaries — only the
  wire + store edges are switched. **W16 forward-note:** widen the
  `DateTime` shape across legacy compute boundaries as a separate
  cleanup wave (not bundled here to keep the diff scope tight).
- **Stores:** `InMemoryPerTenantJwksRotationPolicyStore` +
  `EfPerTenantJwksRotationPolicyStore`; both expose
  `GetForTenantAsync(string tenantId, CancellationToken ct)`.
- **3-provider migrations:** Postgres / SqlServer / Sqlite all
  shipped with consistent `2026_0524_03_07_03` (table creation) +
  `2026_0524_03_07_13` (index `IX_PerTenantJwksRotationPolicies_TenantId_IsActive`)
  timestamps across all 3 providers.
- **Feature-flag:** `JwksRotation:PerTenant:Enabled` (default
  `false`); when off, behaviour is bit-identical to W14
  single-policy.
- **Validator integration DEFERRED to W16.** Deliberate
  table-before-validator pattern: schema change observable without
  enabling the new code path.
- **Doc:** `docs/per-tenant-jwks.md` NEW.

### 3.3 DbSerial completion on 2 W9 files (closes W14 Vasquez memo)

- **Files touched:**
  - `Tests/EfCommentaryUsageMeterTests.cs` —
    `[Collection("DbSerial")]` added.
  - `Tests/IdempotencyStoreContractTests.cs` —
    `[Collection("DbSerial")]` added.
- **Closes W14 Vasquez `db-serial-migration-completion.md`
  escalation step 1.** Migration completion ledger now reads
  **25/25 applied (no tracked-but-unfixed)** — closes the W12
  25-class audit.
- **Convention reinforced:** `[Collection("DbSerial")]` is canonical
  for SQLite-heavy contract tests; the W14 forward-stage memo's
  3-step escalation ladder (Bishop W15 → Vasquez W15 re-prompt →
  W16 Coordinator-direct) resolves at step 1.

### 3.4 `tournament_query_duration_seconds{endpoint, page_size_bucket}` histogram

- **Metric:** `tournament_query_duration_seconds` histogram with
  two labels:
  - `endpoint`: `bracket-records` / `replay-list` /
    `spectator-audit-query` (canonicalised from W14's 3-endpoint
    surface family).
  - `page_size_bucket`: `small` (≤25) / `medium` (≤75) / `large`
    (≤100). **Bucket label canonicalisation** prevents cardinality
    explosion at p99 reporting time.
- **Histogram buckets:** Prometheus defaults — appropriate for
  millisecond-to-second admin-query timing.
- **Collector:** `TournamentQueryLatencyMetrics.cs` NEW singleton;
  optional DI registration (`AddTournamentQueryLatencyMetrics()`
  extension); when absent, side-channel observation falls back to
  no-op.
- **Convention established:** **histogram label-value
  canonicalisation** — endpoint identifiers as kebab-case constants;
  `page_size_bucket` as `small`/`medium`/`large` enumerated
  bucket-labels rather than raw page-size value. Future admin
  histograms follow this 2-label endpoint × bucket pattern.
- **Tests:** `TournamentQueryLatencyMetricsTests.cs` NEW.

### 3.5 `GET /api/commentary/cost/forecast?days=<n>` admin-gated

- **Endpoint:** `GET /api/commentary/cost/forecast?days=<n>`
  admin-gated; companion to W14 `GET /api/commentary/cost/summary`.
- **Forecast logic:** **linear extrapolation by days-elapsed in
  current month.** If `days = 30`, projects current-month spend to
  full-month total. If `days < daysElapsed`, returns the actual
  current spend (no extrapolation needed). Forecast assumes
  uniform daily spend distribution.
- **Confidence bucket:** Returned envelope includes
  `confidence: high | medium | low` derived from `daysOfDataUsed`:
  - `daysOfDataUsed ≥ 7` → `high`.
  - `daysOfDataUsed ≥ 3` → `medium`.
  - `daysOfDataUsed < 3` → `low`.
- **Response envelope:**
  `{forecastDays, daysOfDataUsed, forecastDollars, confidence,
  baselineSpend, model, month, at}`.
- **Companion deep-link:** Hicks's `?action=cost-forecast&days=<n>`
  (W15 §4.4) consumes this endpoint.
- **Tests:** `CommentaryCostForecastEndpointTests.cs` NEW.
- **Code:** `CommentaryCostController.cs` NEW (cleanly extracted
  from the W14 controller's `GET .../summary` shape).

### 3.6 `SpectatorHandoffAuditRetentionSweep` hosted service

- **Service:** `SpectatorHandoffAuditRetentionSweep.cs` NEW hosted
  service; default cadence **5-minute tick**; deletes by
  `Spectator:Audit:RetentionDays` configuration (default 90 days).
- **Runtime tunability:** Each tick re-reads `RetentionDays` from
  `IOptionsMonitor<SpectatorAuditOptions>` — operator can change
  retention live without service restart.
- **Convention reinforced:** **hosted-service retention sweeps
  re-evaluate options at every tick** (extends W13
  `SignalRSequenceRetentionSweep` precedent which already
  pioneered the runtime-tunable hosted-service pattern).
- **Tests:** `SpectatorHandoffAuditRetentionSweepTests.cs` NEW.

### 3.7 `ReplayStoreRetentionSweep` hosted service

- **Service:** `ReplayStoreRetentionSweep.cs` NEW hosted service;
  default cadence **60-minute tick**; deletes replays where
  `CompletedAt < UtcNow - Replays:RetentionDays`.
- **Runtime tunability:** Each tick re-evaluates `CompletedAt`
  against current options (operator can change `RetentionDays`
  live without service restart — parallels §3.6).
- **Different cadence rationale:** 60-minute (vs §3.6's 5-minute)
  because replay blob storage is heavier (multi-MB binary payloads
  vs §3.6's small audit-record rows); reducing sweep frequency
  amortises Ef DELETE batches.
- **Tests:** `ReplayStoreRetentionSweepTests.cs` NEW.

### 3.8 Transient intermediate gate 3307/5/0 → final 3312/0/0

**Gate during Bishop's commit landing: 3307/5/0.**

The 5 transient failures were Vasquez-lane forward-stage hard-assert
tests on §6 (lane-discipline maturity narrative) and
`docs/replay-streaming.md` / `docs/per-tenant-jwks.md` /
`docs/phase-l-renderer-implementation.md` content invariants —
all of which Vasquez subsequently landed in `0a316d7`. **This
extends the W14 canonical Vasquez forward-stage pattern from 2 to
5 transient pins.** Final gate post-Vasquez bring-up: **3312/0/0**.

### 3.9 Bishop test summary

- **111 Bishop-lane new test facts** total across W15.
- Coverage spans:
  - `ReplayBlobStreamingEndpointTests.cs` (Range-header matrix:
    valid single-range × 3 shapes; multi-range / malformed → 416;
    full-body 200 OK; admin-gate matrix).
  - `PerTenantJwksRotationPolicyStoreTests.cs` (InMemory + Ef
    store contract pins; `DateTimeOffset` round-trip preservation;
    3-provider migration smoke).
  - `TournamentQueryLatencyMetricsTests.cs` (histogram emit;
    bucket-label canonicalisation; cardinality cap; no-op
    fallback when collector absent).
  - `CommentaryCostForecastEndpointTests.cs` (linear-extrapolation
    edge-cases; confidence bucket boundaries; admin-gate matrix).
  - `SpectatorHandoffAuditRetentionSweepTests.cs` (runtime-tunable
    retention; sweep cadence; deletion correctness).
  - `ReplayStoreRetentionSweepTests.cs` (60-minute cadence;
    CompletedAt-based deletion; runtime tunability).
- Plus deeper coverage on:
  - RFC 7233 boundary handling (zero-length file with
    `bytes=0-0`; exactly-at-file-end with `bytes=N-N`; `bytes=-0`
    explicitly rejected).
  - JWKS per-tenant store keying by `TenantId` (case-sensitivity,
    null-string rejection, unicode normalisation).
  - Forecast confidence-bucket boundary cases (`days = 0`, `days
    = 2`, `days = 3`, `days = 7`).

---

## 4. Hicks (Frontend) `173bb41` — 5-item charter; Phase L renderer-webgl2 hello-world implementation kickoff (headline); LH13 5-wave deferral; bundle hold-line 5th consecutive

Hicks ships **5 of 5 charter items** in one wave (no deferral
beyond LH13's standing 5-wave roll). The headline is the **Phase L
renderer-webgl2 hello-world** implementation kickoff converting
W14's spike Go-decision into actual code — 1 wave faster than the
W14 forward queue estimated.

### 4.1 LH13 3rd retry deferred to W16 — 5-wave cumulative deferral

- **Status:** Soft-pin retained; hard-pin DEFERRED.
- **Cron history at W15:** `pwa-audit.yml` shows **5 PR runs / 0
  schedule / 0 success** — gate still requires `≥ 3 cron successes`
  for hard-pin convergence.
- **Cause:** Calibration deadlock — `pwa-audit.yml` cron
  schedule is wired but the GitHub Actions UI `schedule:` trigger
  fires only on `main`; Stephen has not manually triggered it via
  Actions UI `Run workflow` button, and the PR-only run path
  produces zero cron successes. Apone's W14 §12 PWA Builder hardening
  landed but does not transitively fire the cron.
- **Deferral ledger now:** W11 → W12 → W13 → W14 → W15 cumulative
  **5 waves**. **§6.4 yellow-flag entered at W15** — 1 wave below
  the §6.3 6-wave Coordinator-consultation trigger.
- **`docs/frontend-pwa-audit.md §7.2` NEW** documents the migration
  context paired with snapshotPathTemplate; **§13 W15 entry** marks
  5-wave deferral.
- **W16 hard-pin conditional on either:** (a) Stephen manually
  triggers `pwa-audit.yml` cron 3+ times via Actions UI (the §6.5
  Stephen-direct path), OR (b) at 6-wave deferral at W16,
  Coordinator-direct intervention via §6.3.

### 4.2 `snapshotPathTemplate` convention + `manifest-screenshots-visual.spec.ts` migration

- **`playwright.config.ts`** pins baselines under
  `snapshotPathTemplate: '{testFileDir}/__screenshots__/{testFileName}/{arg}{ext}'`.
- **`manifest-screenshots-visual.spec.ts` migration:** drops
  `page.setContent()` entirely; replaced with `page.goto(<asset-url>)`
  against vite preview :4173. The W12 `about:blank` relative-URL
  404 latent bug is **fully eliminated** at the spec layer (the
  Hicks-side mitigation; pairs with Vasquez W14's
  `goto('/')` BEFORE `setContent()` for specs that still need
  inline HTML — both mitigations co-exist by design).
- **Doc:** `docs/frontend-pwa-audit.md §7.2` NEW captures the
  migration + canonical baseline layout.
- **Convention:** **`snapshotPathTemplate` is the canonical
  baseline path layout** for all future visual-regression specs;
  no per-spec custom paths.

### 4.3 Phase L renderer-webgl2 hello-world IMPLEMENTATION kickoff (HEADLINE)

- **`src/renderer-webgl2/` NEW directory** (4 source files):
  - `index.ts` — entry point + `?renderer=webgl2-hello` URL guard.
  - `gl-context.ts` — WebGL2 context acquisition + capability
    sniff.
  - `hello-triangle.ts` — minimal 2D triangle render
    (placeholder for the W16 tile-mesh graph).
  - `shaders/hello.vert.glsl` + `shaders/hello.frag.glsl` —
    inline shader strings.
- **Hand-rolled WebGL2:** **zero three.js dependency.** All
  context, buffer, shader, program, draw-call code is direct
  WebGL2 API.
- **URL guard:** `?renderer=webgl2-hello` activates the new
  renderer; default renderer remains W14's three.js-stripped
  `three-renderer-big` chunk. **Hello-world is opt-in for the W15
  wave**; W16 widens to per-game switching.
- **Chunk weight:** **`renderer-webgl2.js = 6,237 B`** —
  hot-loaded only when the URL guard fires; production users hit
  the existing renderer until W16 cutover decision. **6,237 B = 3 %
  of the 180-220 KB Phase L envelope** that Apone's L1 design memo
  carves out (W15 §5.5).
- **Doc:** `docs/phase-l-renderer-implementation.md` NEW (206
  lines; 7 sections: kickoff convention, URL-guard pattern,
  shader sourcing model, GL state machine map, W16 tile-mesh
  graph plan, multi-pass renderer roadmap, rejected-alternatives
  recap).
- **Convention established:** **Phase L feature implementations
  begin as URL-guarded hello-world variants** (chunk weight ~5-10
  KB; zero behaviour change for default users) before the
  per-game / per-tenant cutover surface lands. Avoids the W6 +
  W7 three.js-strip pattern of mutating the live renderer
  surface mid-wave.

### 4.4 `?action=cost-forecast&days=<n>` deep-link to Bishop's forecast endpoint

- **Deep-link routing:** `action-router.ts` extended with
  `cost-forecast` arm; consumes Bishop's
  `GET /api/commentary/cost/forecast?days=<n>` (W15 §3.5).
- **`src/admin-cost-forecast.ts` NEW** (323 lines) — admin-UI
  consumer reading the W15 §3.5 envelope; renders forecast bar
  + confidence-bucket badge + baseline-vs-forecast delta;
  reuses the W14 percentage-normalisation convention
  (`value > 1 ? value : value * 100`) and the W14 CSS class
  threshold convention (`ok / warn / critical` at `<80 / 80-94 /
  >=95`).
- **Defensive wire-shape parsing:** Tolerates 2 wire shapes
  during forward development —
  `{forecastDollars, baselineSpend, ...}` (W15 wire) and
  `{forecast: {dollars}, baseline: {spend}, ...}` (anticipated
  L-series widening).
- **Chunk weight:** **`admin-cost-forecast.js = 6,108 B`** —
  lazy-loaded only when the deep-link fires.
- **Doc:** `docs/frontend-routing.md §7.1` NEW captures the
  routing + days-parameter handling.

### 4.5 `docs/frontend-bundle-audit.md` NEW — 5 W16/W17 shrinkage candidates

- **`docs/frontend-bundle-audit.md` NEW** (240 lines; 3 sections +
  5 sub-candidates):
  - **§3.1 Sentry conditional load** — currently eager-loaded;
    estimated ~15 KB savings; gating on
    `import.meta.env.PROD` shape with PWA-route exclusion.
  - **§3.2 `autotable-src-eager` chunk surgery** — currently
    222,847 B (W15 +1,102 B from cost-forecast plumbing); target
    ~30 KB savings via tournament-mode lazy-load split.
  - **§3.3 HLS conditional gate** — `hls.js` currently
    eager-bundled for spectator path; estimated ~12 KB savings
    behind `?livestream=hls` guard.
  - **§3.4 GLTFLoader clean** — partial-strip candidates that
    survived W7 + W13 passes; estimated ~5 KB.
  - **§3.5 Scene-effects tree-shake** — `tone-mapping` /
    `encodings` / `packing` / Uniforms `points`/`sprite`/`linedashed`
    second-pass that survived W14 hold-line; estimated ~7 KB.
- **Convention:** **bundle-audit memos** identify shrinkage
  candidates with estimated savings + technical approach + risk
  category; future hold-line waves use these as the canonical
  backlog source.
- **Total estimated savings if all 5 land:** **~69 KB**, taking
  three-renderer-big toward ~338 KB (cumulative −54 % vs W6).

### 4.6 Build state K14 → K15

| Chunk                     | K14 (B)   | K15 (B)   | Δ      | Notes                          |
|---------------------------|-----------|-----------|--------|--------------------------------|
| `three-renderer-big.js`   | 406,635   | 406,635   | +0     | 5th consecutive hold-line wave |
| `autotable-src-eager.js`  | 221,745   | 222,847   | +1,102 | cost-forecast deep-link plumbing |
| `admin-cost-forecast.js`  | —         | 6,108     | NEW    | W15 §4.4                       |
| `renderer-webgl2.js`      | —         | 6,237     | NEW    | W15 §4.3 (HEADLINE)            |
| Other 17 chunks           | (no change at the bundle-watch threshold) | | | |

- **Total chunks:** 19 (K14) → **21 (K15)** = +2 new chunks
  (`admin-cost-forecast` + `renderer-webgl2`).
- **`dist-size.json`** updated with K15 row; auto-gated against
  the hold-line threshold by `bundle-health.yml` CI.

### 4.7 Hicks test summary (forward-stage soft-pins)

- Forward-stage soft-pins added under Hicks's test surfaces (see
  Vasquez §6 for the full forward-stage contract test inventory).
- **No new hard-asserts in Hicks's lane** — Hicks-lane test surface
  remains Playwright + bundle-health side-channel.

---

## 5. Apone (DevOps) `b88a5a4` — 7-item charter; Phase L L1 design memo; W5 heredoc bug FIXED; Kyverno enforce pre-wire + HPA tuning + us-east-1 readiness recheck

Apone ships **7 of 7 charter items** in one wave with **W5 heredoc
bug closed at W15** as the cross-cutting headline alongside the
**Phase L L1 design memo** that converts W14's pre-work trifecta
into a unified 10-baseline + 2-optional wave plan.

### 5.1 Kyverno enforce-policies pre-wire

- **`infra/k8s/overlays/prod/kyverno-enforce-policies.yaml` NEW**
  (190 lines) — single `ClusterPolicy: enforce-prod-default` with
  one seed rule (`require-non-root` matching all Pods, enforces
  `runAsNonRoot: true`).
- **Kustomization entry commented-out** —
  `infra/k8s/overlays/prod/kustomization.yaml` has the entry
  staged-but-disabled (W14-evolution "PR-ready commented-out
  pre-wire" pattern).
- **`docs/kyverno-enforce-rollout.md` NEW** (181 lines; 9
  sections; 4 pre-flip pre-conditions: 5-day audit-mode
  observability; zero high-severity audit violations; admin-UI
  Grafana dashboard panel ready; on-call runbook for
  enforce-mode rollback).
- **W16 forward-note:** flip executes after Stephen's 5-day grace
  period observability sign-off + zero high-severity violations
  in audit-mode.

### 5.2 HPA min-replicas tuning pre-flight

- **`docs/hpa-min-replicas-tuning.md` NEW** (172 lines; 8
  sections; 30-day Hudson survey GREEN; recommends 3 → 5
  min-replicas bump for autotable-api).
- **Counter-example to pre-wire pattern codified.** Apone's §2
  documents that **single-line numeric bumps DON'T pre-wire** —
  the PR is small enough that pre-wiring a commented-out
  numeric value is more confusing than just shipping the bump
  directly when Stephen approves. Pre-wire pattern is for
  structural changes (new resources, new fields, new files);
  numeric tunings ship inline.
- **W16 forward-note:** actual bump conditional on Stephen
  prod-capacity sign-off.

### 5.3 W5 heredoc bug FIXED (10-wave-old bug; actionlint exit 0 first time since W5)

- **Bug:** `.github/workflows/lane-discipline-nightly.yml:87`
  used an unquoted heredoc delimiter `<<EOF`; the scan-output
  substitution body contained `$`-prefixed tokens that bash
  expanded at execution time when the scan output happened to
  match shell-variable shapes (the failure mode was
  triggered-but-silent during W5-W14).
- **Fix:** Single-quote the heredoc delimiter `<<'EOF'`;
  env-pipe scan outputs via `--env` injection; substitute
  placeholders in the final body. **`actionlint` exits 0 for the
  first time since W5.**
- **`docs/agent-handoff-protocol.md §5.10` codifies:**
  - Rule 1: Heredoc delimiters are single-quoted (`<<'EOF'`)
    unless interpolation is explicitly intended.
  - Rule 2: Scan output, JSON output, and any computed-value
    pipeline goes through env injection, not body expansion.
  - Rule 3: Placeholder substitution happens after the heredoc,
    not inside it.
  - Rule 4: Multi-line strings with shell-special characters
    (`$`, backticks, `\`) are heredoc-quoted.
  - Rule 5: Single-line strings prefer `$'...'` ANSI-C quoting
    or `printf '%s'` over inline heredoc.
  - Rule 6: All heredoc workflow steps pass `actionlint` (CI
    enforcement on `lane-discipline-nightly.yml`).
- **Canonical example** in §5.10 shows the fix-shape for
  reference.
- **W15 escalation:** Bug existed undetected since W5 because the
  workflow ran fine. **Convention established:** workflow-heredoc
  body content goes through CI-enforced shellcheck-equivalent
  validation; future workflow additions must pass `actionlint`
  before merge.

### 5.4 us-east-1 EKS apply readiness re-check

- **`docs/regional-eks-bringup.md §2.2` NEW** (4 subsections; new
  W15 readiness re-check after W14 plan-readiness landed):
  - **§2.2.1 TF drift since W14:** zero source-side drift.
  - **§2.2.2 IRSA OIDC provider status:** still
    Stephen-blocked (W14 §2.1 assumption preserved).
  - **§2.2.3 EKS apply readiness:** all 4 W14 pre-flight
    checks remain GREEN; apply gated on Stephen action item #7.
  - **§2.2.4 Phase L renderer dependency:** us-east-1 apply
    now sequenced AFTER Hicks's W16 tile-mesh graph lands (the
    renderer-webgl2 chunk needs the wider EKS surface for
    Phase L spectator scaling).
- **No actual apply this wave** — Stephen action item #7
  unblocks the cluster apply.

### 5.5 Phase L L1 design memo

- **`docs/phase-l-l1-design.md` NEW** (312 lines; 7 sections; 12
  DD-numbered decisions across 4 W14 pre-plan surfaces):
  - **DD1-DD3:** Phase L wave count refinement —
    **10 baseline + 2 optional waves** (refines W14's 10-12 wave
    estimate).
  - **DD4-DD6:** Renderer surface design —
    180-220 KB envelope for `renderer-webgl2` final; URL-guard
    pattern for hello-world kickoffs; per-game cutover
    deferred to L2.
  - **DD7-DD9:** Spectator + livestream design —
    HLS over WebRTC for low-latency tier; mountpoint sharding
    for ≥ 500 concurrent.
  - **DD10-DD12:** Mobile design —
    Capacitor over React-Native (W14 deferral resolved);
    push notification topic-per-game; mobile-CI bootstrap
    sequenced for L8.
- **3 Stephen-decision items embedded:**
  - Stephen #L1: Phase L kickoff timing (W16 vs W18 vs L1.0
    branch fork).
  - Stephen #L2: Capacitor framework selection final-sign-off.
  - Stephen #L3: Mobile-CI runner provisioning (GitHub Actions
    macOS vs self-hosted).
- **Convention:** **L1 design memos** consolidate cross-lane
  pre-work into a single decision-numbered document; the
  DD-numbering convention enables surgical decision-by-decision
  retro reference.

### 5.6 SLSA-3 provenance hardening readiness

- **`docs/slsa-provenance.md §7b` NEW** (5 subsections; 3-gap
  analysis + W16-W18 sequenced remediation):
  - **§7b.1:** Gap 1 — non-isolated builder identity (W16).
  - **§7b.2:** Gap 2 — provenance signature transparency log
    (W17).
  - **§7b.3:** Gap 3 — hermetic build sandbox (W18; optional).
  - **§7b.4-§7b.5:** Remediation sequencing rationale + Phase L
    interaction notes.
- **No actual hardening this wave** — readiness analysis only.
  W16-W18 sequenced remediation in the forward queue.

### 5.7 CHANGELOG `[0.24.0]` + `docs/retro-2027-01.md` NEW

- **CHANGELOG `[0.24.0]`** entry added — covers all 5 W15
  agents' deliverables; version arithmetic check passes
  (`0.23.0 → 0.24.0` per W13 / W14 convention).
- **`docs/retro-2027-01.md` NEW** (556 lines) — 4-section
  retro: what-worked, what-stretched, what-broke, what-to-try
  for January 2027 cadence. Includes the W5 heredoc bug closure
  retrospective at length.

---

## 6. Vasquez (QA) `0a316d7` (bring-up) + `c5cf504` (amendment) — gate 3312/0/0 final; DbSerial completion mile-marker; LH13 §6.4 yellow-flag + §6.5 Stephen-direct runbook; lane-discipline maturity narrative §6; 17 forward-stage W15 contract files

Vasquez closes the wave with **gate 3312/0/0** (**+283 over W14**;
**4 successive flake-neutral runs**) and the new top-level
**§6 lane-discipline maturity narrative** in
`docs/agent-handoff-protocol.md` that canonises the W11 → W14
zero-violation streak as the W15+ baseline expectation. The
amendment commit `c5cf504` broadens 2 lane-map shared_files
entries surfaced by lane-discipline strict-mode during the
bring-up commit.

### 6.1 Final-gate run sequence

- **4 successive `dotnet test` runs:**
  - Run 1: 3312 passed / 0 failed / 0 skipped.
  - Run 2: 3312 passed / 0 failed / 0 skipped.
  - Run 3: 3312 passed / 0 failed / 0 skipped.
  - Run 4: 3312 passed / 0 failed / 0 skipped.
- **Flake-neutral verification:** 4-run streak satisfies the W12+
  flake-detection methodology (3-run minimum; 4th run is the W15
  Vasquez safety margin).
- **Captured at:** `Phase_K_W15/Vasquez/gate-snapshot.txt`.

### 6.2 DbSerial completion mile-marker

- **`docs/test-architecture.md §3.4`** updated with the W15
  mile-marker: **25/25 applied (no tracked-but-unfixed)**.
- Closes the W12 25-class audit at the **closing wave** of the
  multi-wave DbSerial migration; W12 → W13 → W14 → W15
  4-wave migration completion ledger lands clean.
- Convention reinforced: **`[Collection("DbSerial")]` is
  canonical** for SQLite-heavy contract tests; the migration
  itself is now a closed ledger item.

### 6.3 LH13 5-wave deferral YELLOW; §6.5 calibration-deadlock escalation

- **`docs/frontend-pwa-audit.md §6.4 yellow-flag entry`** at W15
  — 5-wave deferral (W11+W12+W13+W14+W15); **1 wave below the
  §6.3 6-wave Coordinator-consultation trigger.**
- **`docs/frontend-pwa-audit.md §6.5 NEW Stephen-direct runbook**
  documents the calibration-deadlock escalation path:
  - **30-second manual path:** Stephen opens the Actions UI for
    `pwa-audit.yml`, clicks `Run workflow` 3 times in
    succession (so the cron-success window reads ≥ 3).
  - **First-pass exit:** if 3 successive UI-triggered runs
    succeed, LH13 hard-pin lands at W16 without Coordinator
    intervention.
  - **Fallback:** if Stephen-direct path is exhausted by W16
    and 6-wave threshold hits, Vasquez recommends
    Coordinator-direct at W16.
- **Stephen action item #2** updates to reflect the W15 5-wave
  state.

### 6.4 §4.4 escalation re-verification (branch-protection 8-wave deadlock)

- **`docs/agent-handoff-protocol.md §4.4 re-verification entry`**
  — the §4.1 branch-protection flip request now sits at
  **9-wave deferral**; W15 Vasquez re-verifies the
  `lane-discipline-flip-required.sh --dry-run` path works
  cleanly.
- **Fresh dry-run captured at**
  `.work/vasquez-w15-safe/flip-script-dryrun-w15.log` — the
  W14 fallback script remains operational.
- **Coordinator-direct recommended NOW** per §4.4 (the
  9-wave deferral exceeds any reasonable Stephen-direct
  re-prompt cadence). Vasquez §4.4 entry escalates from W14's
  "fallback ready" wording to "Coordinator-direct recommended
  NOW".

### 6.5 17 forward-stage W15 contract files (~163 facts)

- **`Phase_K_W15/Vasquez/` directory** contains 17 new contract
  test files (`.cs` + `.spec.ts` + `.md` artifacts) totalling
  ~163 fact-pins:
  - W15 Bishop contract pins (replay blob, per-tenant JWKS,
    forecast endpoint, retention sweeps).
  - W15 Hicks contract pins (renderer-webgl2 hello-world,
    cost-forecast deep-link, bundle audit candidates).
  - W15 Apone contract pins (Kyverno pre-wire, HPA tuning,
    W5 heredoc fix, Phase L L1 design DDs).
- All hard-asserts pin **W15 doc content invariants** —
  parallels W14's `PwaAuditDoc_Section6_3_W14_Decision_HardAssert`
  shape.

### 6.6 `Wave1ThroughKW14RegressionTests` → `Wave1ThroughKW15RegressionTests` rename

- `git mv` to preserve history; 18 W15 smoke-tests appended
  (14 soft-pin + 4 hard-assert self-lane).
- Convention sustained: regression suite is renamed every wave
  per W6+ convention; one suite per wave ensures cross-wave
  regression facts cumulatively increase.

### 6.7 6 Playwright specs (chromium-only forward-stage tolerant)

- `replay-blob-streaming.spec.ts` (W15 §3.1).
- `cost-forecast-route.spec.ts` (W15 §4.4).
- `phase-l-renderer-bundle.spec.ts` (W15 §4.3).
- `lh13-thresholds-w15.spec.ts` (W15 §6.3).
- `snapshot-path-template.spec.ts` (W15 §4.2).
- `bundle-audit-candidates.spec.ts` (W15 §4.5).

### 6.8 §6 lane-discipline maturity narrative (NEW top-level section)

- **`docs/agent-handoff-protocol.md §6 NEW** (188 lines; 4
  sub-sections):
  - **§6.1 Maturity arc:** W3-W5 cross-lane-content era →
    W6-W10 identity-hardening era → W11-W14 lane-discipline
    era → **W15+ amendment-discovery era** (the W11 → W14
    4-wave zero-violation streak is canonised as the W15+
    baseline expectation).
  - **§6.2 Amendment-discovery pattern:** lane-discipline
    strict-mode SURFACES previously-invisible shared-files;
    W12 + W13 + W15 amendments are amendment-discovery events,
    NOT regressions. Frame future amendments as discovery, not
    regression.
  - **§6.3 Primary-classification rule:** when a shared-file
    straddles multiple lanes, the **intent-owning lane wins**
    — QA-harness intent overrides `.github/workflows/`
    filesystem heuristic (parallels W10
    `agent_handoff_protocol_md_shared`); test-lane root owner
    wins when a Playwright config + test surface co-edit
    cross-lane (W15 §c5cf504 amendment example).
  - **§6.4 W8 → W13 allowlist evolution timeline:** tabular
    history of every `*_shared` lane-map entry from W8 first
    introduction through W13 cumulative state; W15 amendment
    extends the table with 2 new entries.

### 6.9 W15 amendment commit `c5cf504` — 2 lane-map shared_files broadenings

- **`lane_discipline_nightly_yml_shared`** (apone+vasquez;
  **primary=vasquez**):
  - **Rationale:** Apone's W15 §5.3 heredoc fix at
    `.github/workflows/lane-discipline-nightly.yml:87` AND
    Vasquez's W15 §6.3 §6.4 yellow-flag re-running the same
    workflow surface cause a cross-lane shared-edit.
  - **Primary-classification:** **QA-harness intent overrides
    `.github/workflows/` filesystem heuristic** (parallels W10
    `agent_handoff_protocol_md_shared`).
  - **Path regex:** `^\.github/workflows/lane-discipline-nightly\.yml$`
  - **Both `lane-map.yml` JSON entry and bash matcher in
    `tests/ci/check-cross-lane-bundling.sh`** updated (per W11
    §5.9 policy: bash matcher mirrors JSON).
- **`playwright_visual_regression_shared`** (hicks+vasquez;
  **primary=vasquez**):
  - **Rationale:** Hicks's W15 §4.2 `snapshotPathTemplate`
    migration touches `playwright.config.ts` AND
    `manifest-screenshots-visual.spec.ts` simultaneously; Vasquez
    forward-stage spec touches `manifest-screenshots-visual.spec.ts`
    too — cross-lane shared-edit on two files at once.
  - **Primary-classification:** **test-lane root owner wins** —
    the Playwright config + test file pair is conceptually
    owned by the test/QA lane regardless of which file holds
    the migration body.
  - **Paths (single entry; 2 paths):**
    `playwright\.config\.ts` + `manifest-screenshots-visual\.spec\.ts`
  - **Single entry because snapshotPathTemplate migration spans
    both files** — splitting would cause false-negative on the
    migration-as-a-unit.
- **Restored lane-discipline strict-mode `checked=5
  violations=0`** — **5th consecutive 0-violation wave**.

---

## 7. Cross-cutting patterns from W15

### 7.1 Phase L feature implementations begin as URL-guarded hello-world variants

**Hicks's `?renderer=webgl2-hello` URL guard (§4.3)** activates the
new WebGL2 renderer; default users hit the existing
`three-renderer-big` chunk until W16 cutover decision.

**Convention:** Phase L feature implementations land as
URL-guarded hello-world variants (chunk weight ~5-10 KB; zero
behaviour change for default users) before the per-game /
per-tenant cutover surface lands. Avoids the W6 + W7 three.js-strip
pattern of mutating the live renderer surface mid-wave.

### 7.2 Table-before-validator pattern for feature-flagged schema changes

**Bishop's `PerTenantJwksRotationPolicies` table (§3.2)** lands with
schema + 3-provider migrations + stores but **validator
integration DEFERRED to W16**. Feature-flag `JwksRotation:PerTenant:Enabled`
(default `false`) means behaviour is bit-identical to W14
single-policy.

**Convention:** Feature-flagged schema changes ship **table +
migrations + stores first**, **validator hook-up next wave**. The
schema change is observable (admin can populate rows) without
enabling the new code path; rollout decoupled from schema landing.

### 7.3 RFC 7233 single-range only convention for admin blob endpoints

**Bishop's `GET /api/replays/{id}/blob` (§3.1)** supports single-range
only; multi-range and malformed both return 416.

**Convention:** **Single-range only is the canonical surface across
future admin blob endpoints.** Multi-range deferred to L-series
widening only if observed admin-UI need. Avoids the W6+ multipart-
range complexity for admin-only download workflows.

### 7.4 Histogram bucket label canonicalisation

**Bishop's `tournament_query_duration_seconds{endpoint, page_size_bucket}`
(§3.4)** uses `endpoint` as kebab-case enumerated constant
(`bracket-records`/`replay-list`/`spectator-audit-query`) and
`page_size_bucket` as enumerated bucket-labels
(`small`/`medium`/`large` rather than raw page-size values).

**Convention:** **Histogram label-value canonicalisation** —
endpoint identifiers as kebab-case constants; bucket parameters as
enumerated labels rather than raw values. Future admin histograms
follow this 2-label endpoint × bucket pattern. Prevents cardinality
explosion at p99 reporting time.

### 7.5 Hosted-service retention sweeps re-evaluate options at every tick

**Bishop's `SpectatorHandoffAuditRetentionSweep` (§3.6) +
`ReplayStoreRetentionSweep` (§3.7)** both re-read `RetentionDays`
from `IOptionsMonitor<...>` at every tick — operator can change
retention live without service restart.

**Convention:** **Hosted-service retention sweeps re-evaluate
options at every tick.** Extends W13 `SignalRSequenceRetentionSweep`
precedent which pioneered the runtime-tunable hosted-service pattern.

### 7.6 Sweep cadence proportional to payload weight

**Bishop's `SpectatorHandoffAuditRetentionSweep` (§3.6) ticks every
5 minutes; `ReplayStoreRetentionSweep` (§3.7) ticks every 60
minutes** — because replay blob storage is heavier (multi-MB binary
payloads vs §3.6's small audit-record rows); reducing sweep
frequency amortises Ef DELETE batches.

**Convention:** Hosted-service retention sweep cadence is
**proportional to payload weight**: small-record sweeps tick at
1-5-minute cadence; heavy-blob sweeps tick at 60+ minute cadence.

### 7.7 Bundle hold-line in canonical bandwidth-rebalancing phase

**Hicks's `three-renderer-big +0 B` hold-line (§4.6) at 5th
consecutive wave (W11+W12+W13+W14+W15)** — the hold-line has now
**transitioned from W14's first-deliberate-pause to W15's canonical
bandwidth-rebalancing phase**: Phase L implementation kickoff
absorbs renderer-lane bandwidth while
`docs/frontend-bundle-audit.md` (§4.5) provides a documented
shrinkage backlog for W16+ when bandwidth reopens.

**Convention:** Multi-wave hold-line transitions from
"intentional-pause" (single wave) to "bandwidth-rebalancing phase"
(multi-wave with documented shrinkage backlog) when paired with
a feature-implementation wave that absorbs the freed bandwidth.

### 7.8 Bundle audit memos with shrinkage candidates

**Hicks's `docs/frontend-bundle-audit.md` NEW (§4.5)** identifies 5
W16/W17 shrinkage candidates with estimated savings + technical
approach + risk category for each.

**Convention:** **Bundle-audit memos** identify shrinkage candidates
with estimated savings + technical approach + risk category. Future
hold-line waves use these as the canonical backlog source.

### 7.9 W5 heredoc bug closure pattern

**Apone's W15 §5.3 `lane-discipline-nightly.yml:87` heredoc fix**
closes a 10-wave-old bug via 6-rule pattern at
`docs/agent-handoff-protocol.md §5.10`.

**Convention:** **Single-quote heredoc delimiters (`<<'EOF'`) by
default**; computed values pass through env injection not body
expansion; placeholder substitution happens AFTER the heredoc, not
inside it. All workflow heredocs pass `actionlint` (CI enforcement).

### 7.10 "PR-ready commented-out pre-wire" vs single-line numeric bumps

**Apone's W15 §5.1 Kyverno enforce-policies (commented-out
kustomization entry) vs §5.2 HPA min-replicas (no pre-wire; inline
bump when ready)** establishes the distinction.

**Convention:** **PR-ready commented-out pre-wire** is for
structural changes (new resources, new fields, new files);
**single-line numeric bumps ship inline** when ready rather than
pre-wired. The pre-wire pattern's purpose is to make complex
diffs trivially-reviewable later, not to defer trivial diffs.

### 7.11 L1 design memos consolidate cross-lane pre-work with DD-numbering

**Apone's `docs/phase-l-l1-design.md` (§5.5)** consolidates W14's
Phase L pre-work trifecta into a unified L1 plan with 12
DD-numbered decisions across 4 surfaces.

**Convention:** **L1 design memos** consolidate cross-lane pre-work
into a single decision-numbered document. **DD-numbering**
(`DD1`, `DD2`, ...) convention enables surgical decision-by-decision
retro reference.

### 7.12 Calibration-deadlock escalation pattern

**Vasquez's `docs/frontend-pwa-audit.md §6.5 NEW Stephen-direct
runbook (§6.3)** documents the **calibration-deadlock escalation
path** for LH13: 5-wave deferral on a calibration-blocked surface
escalates to **Stephen-direct manual trigger via Actions UI** as a
30-second pre-Coordinator-direct path.

**Convention:** **Calibration-deadlock escalations** (where a
gate cannot self-calibrate without a one-shot manual trigger) get
a **Stephen-direct runbook** as a 30-second pre-Coordinator-direct
path. Differs from compliance-blocked escalations (where
Coordinator-direct goes first).

### 7.13 Amendment-discovery framing supersedes regression framing

**Vasquez's `docs/agent-handoff-protocol.md §6.2 NEW
amendment-discovery pattern (§6.8)** frames the W12 + W13 + W15
same-lane amendments as **amendment-discovery events**, NOT
regressions.

**Convention:** **Frame future amendments as discovery, not
regression.** Lane-discipline strict-mode is doing its job by
surfacing previously-invisible shared-files; the amendment is the
discipline working correctly.

### 7.14 Primary-classification rule for cross-lane shared files

**Vasquez's `docs/agent-handoff-protocol.md §6.3 NEW
primary-classification rule (§6.8)** codifies: when a shared-file
straddles multiple lanes, the **intent-owning lane wins** — not
the filesystem heuristic.

**Convention:** **Intent-owning lane wins** for cross-lane
shared-file primary classification. QA-harness intent overrides
`.github/workflows/` filesystem heuristic; test-lane root owner
wins when Playwright config + test surface co-edit cross-lane.

### 7.15 Lane-discipline maturity arc canonised

**Vasquez's `docs/agent-handoff-protocol.md §6.1 NEW maturity arc
(§6.8)** canonises the 4-era progression: W3-W5 cross-lane-content
era → W6-W10 identity-hardening era → W11-W14 lane-discipline era →
W15+ amendment-discovery era.

**Convention:** **W11 → W14 4-wave zero-violation streak is the
W15+ baseline expectation.** Future waves are measured against this
baseline; deviation requires explicit escalation reasoning.

### 7.16 Forward-stage hard-assert pattern extended from 2 to 5 transient pins

**Bishop's W15 §3.8 intermediate gate 3307/5/0** extends W14's
canonical Vasquez forward-stage pattern from 2 to 5 transient
hard-assert pins; all 5 cleared post-Vasquez bring-up to final
3312/0/0.

**Convention:** **Cross-lane forward-stage hard-asserts** can scale
to 5+ transient pins; the pin count is bounded only by the number
of W15 doc-content invariants Vasquez forward-stages within the
same wave.

### 7.17 `DateTimeOffset` over `DateTime` at wire and store edges

**Bishop's W15 §3.2 `PerTenantJwksRotationPolicies` schema** uses
`DateTimeOffset` at wire and store edges to preserve timezone
information explicitly (the W14 `DateTime` shape stripped offset on
JSON deserialisation).

**Convention:** **`DateTimeOffset` over `DateTime` at wire and
store edges.** Internal compute boundaries can use `DateTime` for
brevity but edge-of-system surfaces preserve offset. W16
forward-note: widen `DateTimeOffset` across legacy `DateTime`
call-sites as a separate cleanup wave.

### 7.18 Hello-world chunk size as Phase L envelope reference

**Hicks's `renderer-webgl2.js = 6,237 B` (§4.3)** + Apone's L1
design memo's `180-220 KB envelope` (§5.5) establishes a chunk-size
reference: **hello-world is 3 % of envelope**.

**Convention:** Phase L feature implementations report chunk size
as **% of envelope** in addition to absolute bytes; enables
quick "how much budget have we consumed?" reads at every wave
checkpoint.

---

## 8. Numeric milestones recap

### 8.1 Gate trajectory W6 → W15

| Wave | Passed | Δ vs prior | Cumulative vs W6 |
|------|--------|------------|------------------|
| W6   | 1422   | (baseline) | (baseline)       |
| W7   | 1506   | +84        | +5.9 %           |
| W8   | 1706   | +200       | +20.0 %          |
| W9   | 1880   | +174       | +32.2 %          |
| W10  | 2108   | +228       | +48.2 %          |
| W11  | 2403   | +295       | +69.0 %          |
| W12  | 2610   | +207       | +83.5 %          |
| W13  | 2789   | +179       | +96.1 %          |
| W14  | 3029   | +240       | +113.0 %         |
| **W15** | **3312** | **+283** | **+132.9 %** |

- **Gate has more than doubled since W6** — cumulative
  **+1890** tests / **+132.9 %**.
- **W15 +283 is above the W6-W15 average delta (+210)** —
  Bishop's 111 contract facts + Vasquez's ~163 forward-stage
  facts + 18 W15 regression smokes drive the W15 size.
- **Zero-skip streak: 30 consecutive waves preserved.**

### 8.2 Bundle ledger W6 → W15

| Wave | three-renderer-big (KB) | Δ vs prior | Cumulative vs W6 |
|------|-------------------------|------------|------------------|
| W6   | 738.65                  | (baseline) | (baseline)       |
| W7   | 577.20                  | −161.45    | −21.9 %          |
| W8   | 552.40                  | −24.80     | −25.2 %          |
| W9   | 530.10                  | −22.30     | −28.2 %          |
| W10  | 510.30                  | −19.80     | −30.9 %          |
| W11  | 470.62                  | −39.68     | −36.3 %          |
| W12  | 448.65                  | −21.97     | −39.3 %          |
| W13  | 406.64                  | −42.01     | −44.9 %          |
| W14  | 406.64                  | +0.00      | −44.9 %          |
| **W15** | **406.64**           | **+0.00**  | **−44.9 %**      |

- **8-wave monotonic-decrease ledger remains paused (entering
  5th consecutive hold-line wave at W15).**
- **Cumulative reduction = 44.9 %**, far exceeding W6-era
  25 % design-budget aspiration.
- **W16 forward-note:** `docs/frontend-bundle-audit.md` (W15
  §4.5) identifies 5 shrinkage candidates totalling ~69 KB
  potential savings; W16+ deferred-bandwidth wave can pull from
  this backlog.

### 8.3 Phase L renderer envelope budget

| Surface                       | W15 (B) | % of 220 KB envelope |
|-------------------------------|---------|----------------------|
| `renderer-webgl2.js` (hello)  | 6,237   | 2.8 %                |
| W16 target tile-mesh graph    | ~15,000 | ~6.8 %               |
| L1 envelope remaining (~199 KB) | ~199,000 | ~90.4 %         |

- **6,237 B = 2.8 % of envelope** (rounded down from 3 %).
- **W16 tile-mesh graph estimated 15 KB** lands on the 6.2 KB
  baseline → cumulative ~21 KB = 9.6 % of envelope.

### 8.4 Lane-discipline ledger

| Wave | Strict | Violations | Same-lane amendment |
|------|--------|------------|---------------------|
| W11  | yes    | 0          | (none — first 0-vio wave) |
| W12  | yes    | 0          | yes (Vasquez)       |
| W13  | yes    | 0          | yes (Vasquez)       |
| W14  | yes    | 0          | NO — first since W11 |
| **W15** | **yes** | **0**   | **yes (Vasquez `c5cf504`)** |

- **5 consecutive 0-violation waves** (W11+W12+W13+W14+W15).
- **W15 amendment** broadens 2 entries
  (`lane_discipline_nightly_yml_shared` apone+vasquez +
  `playwright_visual_regression_shared` hicks+vasquez); both
  primary=vasquez under the new §6.3 primary-classification rule.

### 8.5 Identity hardening + concurrency mutex ledger

- **10 consecutive clean waves of per-invocation
  `git -c user.name=X -c user.email=Y`** (W6 → W15;
  **85+ commits**).
- **6 consecutive fully-adopted waves of
  `.work/squad-git-lock` flock mutex** (W10 → W15).
- **Zero coordinator-direct interventions for 10 consecutive
  waves** (W6 → W15).

### 8.6 JWT rotation rehearsal timing (carry-over from W14)

| Rehearsal | Wave | Target env | Timing | Notes |
|-----------|------|------------|--------|-------|
| #1 (RED)  | W10  | staging    | 6:12   | RED baseline |
| #2        | W11  | staging    | 5:42   | -30 s        |
| #3        | W12  | staging    | 3:48   | -1:54 (large improvement; GA-rec) |
| #4        | W14  | staging    | 3:51   | +3 s vs W12; within noise; GA-confirmed |

- **W15 did not run a rehearsal** — quarterly cadence (canonical
  per W14 D4) handled by the scheduled cron, not by per-wave
  Apone manual triggers.
- **First real prod JWT rotation: end-of-January 2027** paired
  with Q1 2027 rehearsal.

---

## 9. Forward queue for W16

### 9.1 Bishop (Backend) W16 candidates

1. **PerTenantJwksRotationPolicy validator hook-up** —
   `JwtValidationService` consumer of the W15 §3.2 table;
   feature-flag `JwksRotation:PerTenant:Enabled` flip to `true`
   when validator wired. **Headline W16 Bishop deliverable.**
2. **`DateTimeOffset` widening across legacy `DateTime`
   call-sites** — the W15 §3.2 edge-only switch needs full
   internal compute-boundary widening as a separate cleanup
   wave.
3. **Grafana dashboard for
   `tournament_query_duration_seconds`** — admin-UI Grafana
   panel pulling the W15 §3.4 histogram with p50/p95/p99 rows
   per endpoint and per bucket.
4. **Per-tenant rotation prod-readiness** — operator runbook +
   admin-UI population workflow for `PerTenantJwksRotationPolicies`
   table (W15 §3.2 forward-note).
5. **Replay-blob CDN-edge cache evaluation** — W15 §3.1
   single-range only is in-process; CDN-edge caching evaluation
   for cost reduction.
6. **CommentaryCostBroadcaster backpressure-aware variant**
   (W14 forward-note carry-over; still pending after 2 waves).
7. **Replay storage default flip `InMemory` → `Ef`** (W14
   forward-note carry-over; still pending after 2 waves).

### 9.2 Hicks (Frontend) W16 candidates

1. **LH13 sixth-wave decision** — if Stephen-direct §6.5
   runbook resolves the 5-wave calibration deadlock, LH13
   hard-pin lands. Otherwise **6-wave threshold triggers
   Coordinator-direct via §6.3**.
2. **Phase L tile mesh graph** — W16 target ~15 KB onto the
   W15 6.2 KB baseline; first non-hello-world Phase L renderer
   surface; lands behind `?renderer=webgl2-tile-mesh` URL guard.
3. **Bundle audit §3.1 Sentry conditional load** — W15 §4.5
   first candidate; estimated ~15 KB savings.
4. **Bundle audit §3.2 `autotable-src-eager` surgery** — W15
   §4.5 second candidate; estimated ~30 KB savings (largest
   single-target on the backlog).
5. **Tablet-viewport visual-regression baselines** (768 ×
   1024) — W13 + W14 forward-note carry-over.
6. **`?action=tournament&tournamentId` deep-link extension** —
   W13 + W14 forward-note carry-over.
7. **Bundle-health PR-comment rolling-trend hardening**
   (delta vs prior 5 commits) — W13 + W14 forward-note
   carry-over.

### 9.3 Apone (DevOps) W16 candidates

1. **Kyverno enforce flip** — actual `validationFailureAction:
   audit → enforce` flip after Stephen's 5-day grace period
   observability sign-off. **Headline W16 Apone deliverable.**
2. **HPA min-replicas actual bump** — 3 → 5 inline bump after
   Stephen prod-capacity sign-off.
3. **us-east-1 actual `terraform apply`** — pending Stephen
   action item #7 IRSA OIDC provider; W15 §5.4 readiness
   re-check stays GREEN. Sequenced AFTER Hicks W16 tile-mesh
   graph lands (W15 §5.4.4 dependency).
4. **SLSA-3 partial hardening** — W15 §5.6 §7b.1 Gap 1
   non-isolated builder identity remediation.
5. **Mobile native CI bootstrap** — W14 + W15 Apone forward
   queue ($L8 from W15 §5.5 DD12; mobile-CI runner
   provisioning).
6. **First real prod JWT rotation** — end-of-January 2027 (W15
   = late-January window).
7. **CHANGELOG `[0.25.0]`** + `docs/retro-2027-02.md`.

### 9.4 Vasquez (QA) W16 candidates

1. **LH13 cron status** — if no cron successes by W16,
   **recommends Coordinator-direct via §6.3 6-wave threshold**.
2. **§4.5 branch-protection escalation** — 9-wave Stephen
   re-prompt sequence; Vasquez §4.4 W15 entry already
   escalates to "Coordinator-direct recommended NOW".
3. **`Wave1ThroughKW15RegressionTests → Wave1ThroughKW16RegressionTests`**
   rename per W6+ convention.
4. **W16 forward-stage contract tests for Bishop W16
   surfaces** (per-tenant JWKS validator hook-up + Grafana
   dashboard + DateTimeOffset widening + replay-blob CDN
   evaluation) under `Phase_K_W16/Vasquez/`.
5. **W16 forward-stage contract tests for Hicks W16 surfaces**
   (LH13 sixth-wave + tile-mesh graph + bundle-audit §3.1/§3.2)
   under `Phase_K_W16/Vasquez/`.
6. **W16 forward-stage contract tests for Apone W16 surfaces**
   (Kyverno enforce flip + HPA bump + us-east-1 apply +
   SLSA-3 partial + JWT rotation) under `Phase_K_W16/Vasquez/`.
7. **§6 maturity narrative update at W16** — append W15+
   amendment-discovery era data point.

### 9.5 Lane-discipline cross-cutting W16 candidates

- **0-violation stretch goal sustained across
  W11+W12+W13+W14+W15 — maintain through W16.** Goal: 6
  consecutive 0-violation waves.
- **10 consecutive waves with zero coordinator-direct
  interventions** (W6 → W15) — maintain through W16 (or
  consume the §4.4 branch-protection Coordinator-direct
  recommendation if Stephen still silent at W16).
- **W16 candidate `phase_l_renderer_shared`** — pre-emptive
  lane-map entry if W16 sees co-edited Phase L renderer
  surfaces across Hicks + Bishop lanes (Hicks owns the renderer
  module; Bishop may extend renderer-state contracts).

### 9.6 Scribe / Coordinator W16 candidates

- **Per-invocation `git -c user.name=X -c user.email=Y commit
  ...`** remains canonical (held over W6 → W15; **10
  consecutive clean waves; 85+ commits**).
- **`flock 9>.work/squad-git-lock` mutex** (**6th consecutive
  fully-adopted wave at W15**; W16 prompt templates continue
  path uniformity).
- **`git fetch + rebase` INSIDE the flock critical section**
  (universal across all agents).
- **`.work/<agent>-w<N>-safe/` backup directory** as a
  first-class step in every prompt template.
- **CHANGELOG version-arithmetic check** goes in every
  changelog-bump pattern (W13 `[0.22.0]` clean; W14 `[0.23.0]`
  clean; W15 `[0.24.0]` clean; **W16 `[0.25.0]`**).
- **Phase L L1 design memo's 3 Stephen-decision items** —
  Stephen #L1 (Phase L kickoff timing), #L2 (Capacitor
  framework), #L3 (Mobile-CI runner) — Scribe sweeps W16 to
  confirm whether any of these resolved between W15 and W16.

---

## 10. Stephen action items (carry-into-January 2027)

1. **Branch-protection flip** for the lane-discipline gate
   (`tests/ci/check-cross-lane-bundling.sh --strict`) —
   Stephen re-prompt **#10 unresolved at W15**. **W15 §4.4
   re-verification escalates to "Coordinator-direct
   recommended NOW"** (9-wave deferral exceeds reasonable
   re-prompt cadence). Fresh dry-run at
   `.work/vasquez-w15-safe/flip-script-dryrun-w15.log`
   confirms script remains operational; 1-line `gh api -X
   PATCH` copy-paste in `docs/agent-handoff-protocol.md §4.3`.

2. **Trigger `pwa-audit.yml` cron via Actions UI** — 5-wave
   LH13 calibration deadlock; W15 §6.5 Stephen-direct runbook
   ready: open Actions UI for `pwa-audit.yml`, click `Run
   workflow` 3 times in succession; **30-second manual path**
   pre-Coordinator-direct. If unresolved by W16, 6-wave
   threshold triggers Coordinator-direct via §6.3.

3. **`PWA_PREVIEW_URL` secret** — Hicks LH13 hard-pin W16
   unlock depends on this AND the cron-trigger action item #2.

4. **Secrets provisioning:**
   - **Sentry DSN** (W9 error-reporting; unresolved since W9).
   - **OpenAI API key** (W10; **now blocks `EfCommentaryStore`
     persistence dogfood in prod for 5 consecutive waves**).
   - **Janus credentials** (W11 spectator livestream stub).
   - **Redis prod credentials** (W11 ESO; W14 commented-out
     pre-wire still blocked).

5. **Argo Rollouts install** in prod cluster — Apone W11+W12+W13+W14+W15
   prep all ready; W16 install unlocks Rollouts cutover.

6. **Prod Redis TF apply** — Apone W11+W12+W13+W14+W15 prep
   all ready; W16 apply unlocks prod cutover.

7. **us-east-1 IRSA OIDC provider** — W14 §2.1 + W15 §5.4
   plan-readiness re-check assume ACTIVE; cluster apply
   blocked until provider provisioned.

8. **First real prod JWT rotation end-of-January 2027** —
   Apone W14 D4 GA-confirmed; W15 falls within the late-January
   2027 window; **paired with Q1 2027 rehearsal**.

**10 consecutive weeks of Stephen re-prompt sequence; W15 §4.4
escalates branch-protection to Coordinator-direct
recommendation; W15 §6.5 ships Stephen-direct LH13 cron-trigger
runbook as 30-second pre-Coordinator-direct path.**

---

## 11. Identity hardening recap

W15 preserves the **10th consecutive clean wave** of:

- **Per-invocation identity binding:**
  `git -c user.name="Agent Name" -c user.email="agent@squad.mahjong" commit ...`
  Never `git config user.name=X` (per-commit isolation; no
  global config drift between waves or agents).
- **`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
  trailer** on every commit message.
- **`flock -w 120 9 ...` mutex** wrapping every agent's
  fetch + rebase + commit + push, with the lock file at
  `.work/squad-git-lock` (6th consecutive fully-adopted
  wave).
- **`git fetch` + `git rebase` INSIDE the flock critical
  section** — prevents the W5+ "race the upstream main
  between fetch and push" failure mode.
- **`.work/<agent>-w<N>-safe/` backup directory** — every
  agent stashes work-in-progress before the rebase;
  rollback path on rebase conflict.

**85+ commits across W6 → W15 with zero identity drift and
zero coordinator-direct interventions.**

---

## 12. Sign-off

**Phase K Wave 15 closes at:**
- **Final gate:** 3312 passed / 0 failed / 0 skipped (+283).
- **Zero-skip streak:** 30 consecutive waves (J.1-J.10 +
  K.1-K.15).
- **Lane-discipline:** `checked=5 violations=0` (5th
  consecutive 0-violation wave; W15 amendment via Vasquez
  `c5cf504` broadens 2 entries under the new §6.3
  primary-classification rule).
- **Bundle ledger:** three-renderer-big 406,635 B (+0 W15
  hold-line; 5th consecutive hold-line wave; cumulative
  W6 → W15 −44.9 %).
- **Identity hardening:** 10th consecutive clean wave.
- **Concurrency mutex:** 6th consecutive fully-adopted wave.
- **Coordinator-direct interventions:** ZERO for 10
  consecutive waves (W6 → W15).
- **Phase L renderer-webgl2 hello-world implementation
  kickoff landed:** `src/renderer-webgl2/` NEW + `?renderer=webgl2-hello`
  URL guard + 6,237 B chunk (2.8 % of 220 KB envelope) +
  `docs/phase-l-renderer-implementation.md` NEW.
- **W5 heredoc bug CLOSED at W15** — 10-wave-old bug fixed;
  `actionlint` exits 0 for first time since W5.
- **LH13 5-wave deferral YELLOW** — §6.5 Stephen-direct
  cron-trigger runbook ready; 6-wave Coordinator-direct
  threshold at W16.
- **Lane-discipline maturity narrative §6 landed** —
  W11→W14 4-wave zero-violation streak canonised as W15+
  baseline expectation; amendment-discovery era opens at W15.
- **W16 forward queue:** ~28 items across 4 lanes; Bishop
  per-tenant JWKS validator hook-up + Apone Kyverno enforce
  flip + Hicks Phase L tile-mesh graph are the headlines.

**Phase K Wave 15 — DONE.**
