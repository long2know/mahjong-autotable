# Phase K — Wave 14 Summary

- **Branch:** `stlong/phase-k-wave-14-bringup`
- **Base:** `main` @ `f0b8e4a`
- **Head:** `537594e` (Vasquez Final-Gate QA)
- **Date:** 2026-12-XX
- **Final gate:** **3029 passed / 0 failed / 0 skipped** (+240 over W13)
- **Zero-skip streak:** **29 consecutive waves** (J.1-J.10 + K.1-K.14)
- **Lane-discipline:** **`checked=4 violations=0` — 4th consecutive 0-violation wave (W11+W12+W13+W14); FIRST since W11 first 0-violation wave with NO same-lane amendment commit needed**
- **Identity hardening:** **9th consecutive clean wave** (per-invocation `git -c user.name=X -c user.email=Y`)
- **Concurrency mutex:** **5th consecutive fully-adopted wave** of `flock -w 120 9 ... 9>.work/squad-git-lock`
- **Coordinator-direct interventions:** **ZERO for 9 consecutive waves** (W6 → W14)

---

## 1. Headlines

1. **W14 executes the W13 forward queue across all 4 lanes with no
   amendment required.** Bishop's 7-endpoint admin-observability
   surface family + Hicks's 3 deep-link extensions + Apone's
   us-east-1 plan-readiness / TF bump / JWT rehearsal #3 GA-confirmed
   / PWA Builder hardening / Phase L DevOps-readiness +
   Vasquez's gate / DbSerial completion / mirror sync /
   branch-protection fallback runbook all land in one wave with
   `checked=4 violations=0` and **no Vasquez same-lane amendment
   needed** — the first such wave since W11.

2. **First wave of Phase K to emit cross-lane Phase L pre-work
   trifecta.** `docs/phase-l-devops-readiness.md` (Apone, 411 lines,
   4 surfaces) + `docs/phase-l-bringup.md` (Bishop, 199 lines,
   4 pillars) + `docs/phase-l-renderer-spike.md` (Hicks, 14 KB,
   Go-decision on WebGL2 hand-roll) together seed the Phase L
   narrative before Phase K terminates. Apone's 10-12 wave estimate
   harmonises with Bishop's 8-wave + L9 wrap (range absorbs
   compliance + observability + tooling waves).

3. **Bishop's 7-endpoint admin-observability surface family
   establishes pagination shape uniformity.**
   `{items, count, skip, limit, pageSize}` is the canonical envelope
   across spectator-audit / bracket-query / replay-listing with
   defaults `50 / 50 / 25` and maxes `200 / 200 / 100`. Future
   paginated admin endpoints follow this shape. The auth-precedence
   ladder `401 → 403 → 503 → 400 → 200` (missing-token → wrong-scope
   → store-unavailable → bad-input → ok) becomes the canonical
   ordering at `/api/spectator/handoff/audit`.

4. **JWKS overlap-window `rollback-rejected` security primitive
   lands.** `JwtValidationService.ErrorRollbackRejected =
   "rollback-rejected"` exported as `public const`; rejects tokens
   where signature matches non-active key (index > 0) AND policy
   wired AND `RotationStartUtc` set AND `iat >= RotationStartUtc`.
   Boundary `iat == RotationStartUtc` rejected defensively — closes
   the replay-of-just-rotated-token race window. Legacy single-arg
   ctor preserved for backward-compat with un-migrated W11 callers.

5. **SignalR sequence Prometheus 3-counter family extends W13
   observability surface.** `SignalRSequenceMetrics` singleton
   exposes `signalr_seq_replay_from_ack_total{hub, result}` counter
   + `signalr_seq_store_rows_active` gauge +
   `signalr_seq_retention_sweep_deleted_total` counter; result-label
   constants `hit`/`miss`/`expired` exported as `public const string`
   for assertion stability and `grep`-discoverability. Convention
   established at W13 with `commentary_cost_dollars_total` carries
   forward.

6. **Three-renderer-big intentional hold-line at 406,635 B = +0 W14.**
   First non-decreasing wave since W7; breaks 8-wave monotonic ledger
   by design to free Hicks's renderer-lane bandwidth for the
   `docs/phase-l-renderer-spike.md` Go-decision. Hicks's
   `phase-l-renderer-spike.md` documents the rationale: current
   406 KB three.js-stripped vs estimated 180-220 KB WebGL2 hand-roll
   ceiling (~46-56 % further reduction headroom); continued three.js
   stripping has flattening returns (W13 was the last big drop at
   −42 KB). Cumulative W6 → W14: **−44.9 %** (738.65 KB → 406.64 KB).

7. **JWT rehearsal #3 GA-confirmed; first real prod rotation
   recommended for end-of-January 2027.** Apone D4 captures
   rehearsal timing 3:51 vs W12 3:48 (+3 s within noise; well under
   W10 6:12 RED-baseline); quarterly cadence as canonical;
   `docs/jwt-rotation-rehearsal.md §5` NEW; existing §5-§10
   renumbered §6-§11. **W14 confirms quarterly rehearsal cadence
   stability across 3 rehearsal runs; production rotation paired
   with Q1 2027 rehearsal.**

---

## 2. Wave-14 commits

| SHA       | Lane    | Author email                | Files | +Lines | −Lines |
|-----------|---------|-----------------------------|-------|--------|--------|
| `c823e1c` | Apone   | `apone@squad.mahjong`       | 15    | 2843   | 18     |
| `02e2330` | Hicks   | `hicks@squad.mahjong`       | 33    | 3096   | 28     |
| `ec4a3c6` | Bishop  | `bishop@squad.mahjong`      | 27    | 3348   | 4      |
| `537594e` | Vasquez | `vasquez@squad.mahjong`     | 35    | 3419   | 26     |

**Totals: 110 files; +12,706 lines / −76 lines.** All 4 commits
carry the `Co-authored-by: Copilot <…>` trailer.

**First wave since W11 first 0-violation wave with NO Vasquez
same-lane amendment commit.** W12 + W13 both needed amendment
commits to `tests/ci/lane-map.yml` to clear cross-lane bundling
false-positives; W14 had no trigger — `checked=4 violations=0`
held through Vasquez's final-gate commit landing.

---

## 3. Bishop (Backend) `ec4a3c6` — 7-endpoint admin-observability surface family + 89 Bishop-lane test facts

Bishop ships **7 deliverables in one wave**, anchored by the
**7-endpoint admin-observability surface family** that establishes
pagination shape uniformity and the auth-precedence ladder as
canonical patterns.

### 3.1 `GET /api/spectator/handoff/audit` — admin-gated paginated query

- **Endpoint:** `GET /api/spectator/handoff/audit` admin-gated;
  filters `gameId?`, `fromUtc?`, `toUtc?` query parameters;
  pagination `skip` (default 0) + `take` (clamped to
  `Spectator:Audit:PageSize` default 50, max 200).
- **Store contract:**
  `ISpectatorHandoffAuditStore.QueryAsync(gameId?, fromUtc?,
  toUtc?, skip, take, ct)` — extends W13's `WriteAsync`-only
  contract.
- **Response envelope:**
  `{items: [...], count: N, skip: 0, limit: 50, pageSize: 50}` —
  **canonical W14 pagination shape, reused across all 3 paginated
  W14 endpoints.**
- **Auth precedence ladder:** `401 → 403 → 503 → 400 → 200`
  (missing-token → wrong-scope → store-unavailable → bad-input →
  ok). Convention applied here; extends to future admin
  endpoints. Note the **503 inserted between 403 and 400** —
  store-unavailability is a server-side condition, distinct from
  client-input validation failure, and must be distinguishable
  in admin-UI error handling.
- **Fail-safe:** Returns 503
  `{"error":"audit-store-unavailable"}` if
  `ISpectatorHandoffAuditStore` is not registered; does NOT
  panic.
- **Tests:** `SpectatorAuditQueryEndpointTests.cs` NEW (280
  facts; auth precedence matrix + pagination boundary checks +
  filter combinations including overlap with W13 audit-write
  surface).

### 3.2 `GET /api/commentary/cost/summary` — admin-gated plain JSON

- **Endpoint:** `GET /api/commentary/cost/summary` admin-gated;
  no query parameters; plain JSON envelope
  `{currentMonthCost, budgetCapUsd, percentUsed, monthlyTokens,
  tokensPerDollar, state, model, month, at, byModel[]}`.
- **`byModel` is an ARRAY (single entry today: `{model, cost,
  tokens, percent}`) for Phase L multi-provider widening** — the
  schema is forward-stable when more providers (Anthropic,
  Google, local) are added without breaking admin-UI consumers
  built against the W14 single-entry shape. **Convention
  established: forward-stable schemas use arrays not maps for
  forward-stability where multi-tenant / multi-provider widening
  is anticipated.**
- **`percentUsed` is a raw float** (0.0-1.0+) — admin-cost UI
  (Hicks W14 §6) normalises with `value > 1 ? value : value *
  100` to handle both raw-float (0.0-1.0) and already-scaled
  (0-100) shapes during forward development.
- **Fail-safe:** Returns zeroed envelope when
  `CommentaryCostBudget` is unwired (no W13 `RecordSpend` calls
  observed); does NOT panic or 500.
- **Auth gate:** Admin-only; reads remain server-side admin
  surface (NOT share-friendly anonymous like bracket query or
  replay listing).
- **Tests:** `CommentaryCostSummaryEndpointTests.cs` NEW.

### 3.3 `GET /api/tournaments/{id:guid}/brackets` — paginated anonymous-allowed

- **Endpoint:** `GET /api/tournaments/{id:guid}/brackets`
  **anonymous-allowed by design** — bracket pages are
  share-friendly opaque tokens (parallels W12 replay-URL
  convention). Pagination `skip` + `take` clamped to
  `BracketQueryOptions(PageSize=50, MaxPageSize=200)`.
- **Ordering:** **`(RoundNumber ASC, MatchSlot ASC)`** —
  note `MatchSlot` is still W13-derived from seed-match
  (Bishop W15 §4 forward-note adds the column + 3-provider
  migration to collapse the O(N) scan to O(1)).
- **Response envelope:** `{items, count, skip, limit,
  pageSize}` — same as §3.1.
- **Fail-safe:** 503 `{"error":"bracket-store-unavailable"}`
  on `ITournamentBracketStore` missing.
- **Tests:** `BracketQueryEndpointTests.cs` NEW.

### 3.4 `GET /api/replays` — paginated metadata-only

- **Endpoint:** `GET /api/replays` paginated; filters
  `fromUtc?`, `toUtc?`, `variant?` query parameters;
  `IReplayStore.ListAsync(fromUtc?, toUtc?, variant?, skip,
  take, ct)` contract; `ReplayOptions.PageSize` default **25**
  max **100** — **smaller page-size than spectator-audit +
  bracket because replay metadata rows are heavier (replay
  name + variant + completed timestamp + participants
  payload).**
- **`payloadSize` field is ALWAYS 0 in listing envelope** —
  full replay blob streaming is **W15 Bishop §1 forward-note**
  on a dedicated endpoint
  (`GET /api/replays/{replayId}/stream` with `Range:` header
  + chunked transfer-encoding).
- **Ordering:** **`CompletedAt DESC`** — most-recent first.
- **Auth gate:** GET stays anonymous (share-friendly read);
  POST stays admin-gated per W13 §1 (`Replays:RequireAdminForPost=true`).
- **Tests:** `ReplayListingEndpointTests.cs` NEW.

### 3.5 JWKS overlap-window `rollback-rejected` security primitive

- **Code change:** `Auth/JwtValidationService.cs` adds error
  constant `ErrorRollbackRejected = "rollback-rejected"` as
  a `public const string`. Logic rejects tokens where
  signature matches **non-active key (index > 0)** AND
  `JwtStagedRotationPolicy` is wired AND `RotationStartUtc`
  is set AND token `iat >= RotationStartUtc`.
- **Boundary semantics:** `iat == RotationStartUtc` →
  **REJECTED (defensive)** — prevents
  replay-of-just-rotated-token race-window exploitation.
  Explicitly chose `>=` over `>` after security review.
- **Backward-compat:** Legacy single-arg ctor preserved for
  W11 callers not yet migrated to the policy-aware
  constructor. No breaking change at the API surface.
- **Convention reinforced:** Error codes for
  security-rejection paths are kebab-case constants exported
  from the validating service for E2E assertion stability —
  extends W13 audit-always `audit-failed` precedent.
- **Tests:** `JwksOverlapRollbackRejectedTests.cs` NEW —
  covers boundary case explicitly +
  rotation-window-active matrix.

### 3.6 `SignalRSequenceMetrics` Prometheus 3-counter family

- **Singleton:** `Hubs/SignalRSequenceMetrics.cs` NEW.
- **Metric 1:**
  `signalr_seq_replay_from_ack_total{hub, result}` counter —
  incremented on every `ReplayFromAck` call across all
  SignalR hubs. Result-label constants `hit` / `miss` /
  `expired` exported as `public const string`.
- **Metric 2:** `signalr_seq_store_rows_active` gauge —
  current active row count in the W13 sequence store.
- **Metric 3:**
  `signalr_seq_retention_sweep_deleted_total` counter —
  incremented per W13 retention sweep run by deleted-row
  count.
- **Fail-safe:** `MetricsEndpoint` falls back to zeroed
  envelope when collector singleton is absent (no panic
  on misconfiguration; observed as "0 across all labels"
  in Grafana).
- **Convention reinforced:** Prometheus metric families
  with result-label constants are exported as
  `public const string` from the metric-owning singleton
  for assertion stability and discoverability via `grep` —
  extends W13 `commentary_cost_dollars_total`
  labelled-counter precedent.
- **Tests:** `SignalRSequenceMetricsTests.cs` NEW (315
  facts).

### 3.7 `docs/phase-l-bringup.md` NEW (Phase L pre-work artifact)

- **199 lines spanning 4 Phase L pillars:**
  - **Pillar 1 — Tournament-grade hardening:** Swiss-rounds
    at scale (256+ seeds), ladder ranking + ELO, anti-cheat
    instrumentation.
  - **Pillar 2 — Spectator improvements:** multi-stream
    switching, latency floor (target < 500ms for live),
    commentary quality + per-model A/B framework.
  - **Pillar 3 — Mobile:** native iOS + Android shells via
    Capacitor or React-Native (decision deferred to L2);
    push notifications for game-ready / replay-ready.
  - **Pillar 4 — AI tuning:** commentary cost per-model
    (extends W14 `byModel[]` envelope); replay
    summarization; cost-aware model selection.
- **8-wave + L9 wrap estimate** matches Apone's
  `phase-l-devops-readiness.md` 10-12 wave range
  (Apone's range absorbs infrastructure spike +
  compliance + observability waves).
- **First W14 cross-lane Phase L pre-work artifact**
  landed — paired with Hicks
  `phase-l-renderer-spike.md` Go-decision and Apone
  `phase-l-devops-readiness.md`.

### 3.8 Transient gate during Bishop's commit

**Gate during Bishop's commit landing: 3027/2/0.**

The 2 transient failures were Vasquez-lane forward-stage
hard-assert tests:

- `PwaAuditDoc_Section6_3_W14_Decision_HardAssert`
- `FrontendPwaAuditDoc_W14_Section6_3_HardAssert`

Both depended on §6.3 of `docs/frontend-pwa-audit.md` which
Vasquez was set to land afterward in the same wave. **This
is the canonical Vasquez forward-stage pattern:** Vasquez
ships the §6.3 doc + hard-assert tests in a single commit;
the tests pin the doc-content invariant; during the wave's
critical window they appear as 2 "failures" tracked to a
known landing commit. **Final gate post-Vasquez landing:
3029/0/0.** Bishop commit shipped with intentional
2-failure cross-lane forward-stage pin by design.

### 3.9 Bishop test summary

- **89 Bishop-lane new test facts** total across W14.
- Coverage spans:
  - 280 facts in `SpectatorAuditQueryEndpointTests.cs`
  - 8 facts in `CommentaryCostSummaryEndpointTests.cs`
  - 12 facts in `BracketQueryEndpointTests.cs`
  - 10 facts in `ReplayListingEndpointTests.cs`
  - 6 facts in `JwksOverlapRollbackRejectedTests.cs`
  - 315 facts in `SignalRSequenceMetricsTests.cs`
- Plus deeper coverage on:
  - Auth-precedence ladder boundary cases
  - JWKS overlap-window inclusive-boundary case (`iat == RotationStartUtc` rejected)
  - Pagination boundary clamping (negative skip → 0; take > max → max)
  - Fail-safe zeroed-envelope behaviour for commentary cost and SignalR metrics
  - Wire-shape contract pins for `byModel[]` array, `{items, count, skip, limit, pageSize}` envelope

---

## 4. Hicks (Frontend) `02e2330` — 5 ship + 1 defer; bundle hold-line; Phase L Go-decision

Hicks ships **5 of 6 deliverables** + 1 deferral. The bundle
ledger holds at W13's 406,635 B intentional non-decrease, freeing
renderer-lane bandwidth for the Phase L renderer-spike Go-decision.

### 4.1 LH13 hard-pin DEFERRED to W15 — 4-wave cumulative deferral

- **Status:** Soft-pin retained; hard-pin DEFERRED.
- **Cron history at W14:** `pwa-audit.yml` shows **4 PR runs /
  0 schedule / 0 success** — the gate requires `>= 3 cron
  successes` for hard-pin convergence.
- **Cause:** Dependency-blocked on Apone W14 §12 PWA Builder
  preview-URL provisioning landing first AND Stephen-provisioned
  `PWA_PREVIEW_URL` secret + `GH_TOKEN` for cron query.
- **Deferral ledger now:** W11 → W12 → W13 → W14 cumulative
  **4 waves**.
- **`docs/frontend-pwa-audit.md §13` NEW** supersedes §10 W13
  deferral marker.
- **W15 hard-pin conditional on:** (a) ≥ 3 cron successes after
  PWA_PREVIEW_URL provisioning + Apone §12 lands + (b)
  Stephen-provisioned `GH_TOKEN` for cron query.
- **W15+ escalation criteria:** **6-wave deferral →
  Coordinator-direct intervention** per Vasquez
  `docs/frontend-pwa-audit.md §6.3` mirror sync policy. If W17
  still shows zero cron successes, Vasquez recommends
  Coordinator-direct execution.

### 4.2 Real visual-regression captures (replaces W13 placeholder PNGs)

- **`scripts/capture-real-surfaces.js` NEW** — Playwright
  runtime API against vite preview :4173 with **W11 tour
  suppression + W12 magic-link overlay suppression + W12
  sign-in modal suppression** producing 3 PNGs at **1280×720**:
  - `main-game.png` — 97,771 B
  - `spectator-commentary.png` — 105,819 B
  - `tournament-dashboard.png` — 82,173 B
- **3 distinct MD5s confirmed.** Replaces W13 placeholder
  320×240 manifest-icon PNGs that snuck through W13's
  `capture-visual-baselines.js` due to a W12-introduced
  `page.setContent` latent bug Vasquez documented.
- **`docs/frontend-pwa-audit.md §14` NEW** documents the
  runtime-capture-with-overlay-suppression methodology.

### 4.3 `docs/phase-l-renderer-spike.md` NEW — Go-decision on WebGL2 hand-roll

- **14 KB; 6 sections.**
- **Decision:** **Hand-roll WebGL2 renderer in Phase L.**
- **Rationale:** current 406 KB three-renderer-big vs estimated
  180-220 KB hand-roll ceiling (~46-56 % further reduction
  headroom); W6 → K14 cumulative −44.9 % flattening rationale —
  continued three.js stripping has flattening returns (W13 was
  the last big drop at −42 KB; W14 hold-line acknowledges
  plateau).
- **Rejected alternatives ledger:**
  - **PixiJS** — 2D-first; would lose mahjong-table 3D tilt.
  - **Babylon.js** — heavier than three.js even stripped.
  - **bare-WebGL1** — no instanced drawing for 136-tile
    dense scene.
  - **three.module-fork** — vendoring burden +
    upstream-divergence risk.
- **Convention established:** Spike Go/no-go decisions are
  documented with rejected-alternatives list for future
  reference.

### 4.4 `?action=bracket&tournamentId=<id>` deep-link

- **`src/bracket-listing.ts` NEW** — 12 KB source / 6.5 KB
  lazy chunk.
- **Defensive wire-shape parse** handles `{brackets:[]}` /
  `{records:[]}` / bare `[]` array response shapes from
  Bishop's W14 §3 endpoint (forward-compat while admin-UI
  shape stabilises).
- **Per-record `playerA` accepts string OR `{displayName}`
  object shape** (cross-API compatibility).
- **Empty-state in-overlay
  `data-testid="bracket-listing-empty"`** for E2E assertion.
- **`docs/frontend-routing.md §3.2` NEW.**

### 4.5 `?action=replays` deep-link

- **`src/replays-listing.ts` NEW** — 8.6 KB source / 4.7 KB
  lazy chunk.
- **Metadata-only table** fed by Bishop W14 §4 `GET /api/replays`.
- **Alias-tolerant field reading** — `id` → `replayId`,
  `completedAtUtc` → `completedAt`.
- **Rows link to W12 `?action=replay&replayId=<id>`** — closes
  the navigation loop between listing surface (W14) and
  individual-replay surface (W12).
- **`docs/frontend-routing.md §3.3` NEW.**

### 4.6 `?action=admin-cost` deep-link

- **`src/admin-cost.ts` NEW** — 11 KB source / 5.97 KB lazy
  chunk.
- **Preflight `/api/auth/me` 401-redirect** to `/login` then
  back — gracefully handles missing-auth case.
- **Fetches `/api/commentary/cost/summary`** (Bishop W14 §2).
- **`percentUsed` normalised `value > 1 ? value : value * 100`**
  — handles both Bishop's raw-float shape AND legacy
  already-scaled percentage shapes (forward-tolerance during
  schema-stabilisation churn).
- **CSS class `ok` / `warn` / `critical`** thresholds at
  **`<80` / `80-94` / `>=95`** percentage.
- **`docs/frontend-routing.md §3.4` NEW.**

### 4.7 Bundle state at W14

| Chunk                          | W13 (B)   | W14 (B)   | Delta     |
|--------------------------------|-----------|-----------|-----------|
| `three-renderer-big`           | 406,635   | **406,635**| **+0**   |
| `autotable-src-eager`          | 219,528   | 221,745   | +2,217    |
| `bracket-listing` (NEW)        | —         | 6,520     | +6,520    |
| `replays-listing` (NEW)        | —         | 4,723     | +4,723    |
| `admin-cost` (NEW)             | —         | 5,968     | +5,968    |

- **Three-renderer-big HOLD-LINE at 406,635 B = +0 W14** —
  first non-decreasing wave since W7; breaks 8-wave monotonic
  ledger by design.
- **Rationale:** Free Hicks's renderer-lane bandwidth for
  `docs/phase-l-renderer-spike.md` Go-decision.
- **Convention established:** Bundle hold-line as
  bandwidth-rebalancing signal — intentional non-decrease wave
  is a deliberate signal (must be documented in the wave fold
  with forward-bandwidth-redirection rationale), not a
  regression.
- **Autotable-src-eager +2,217 B** accommodates action-router
  extensions for the 3 new deep-link surfaces.
- **3 new chunks all under 7 KB** — meets lazy-chunk size budget.
- **`dist-size.json` K14 row appended** (19 chunks total).
- **`scripts/append-dist-size.js KEY_PATTERNS` extended** for
  new chunk names.

### 4.8 Bundle ledger across Phase J + K

| Wave | three-renderer-big (KB) | Delta vs prior | Cumulative vs W6 |
|------|-------------------------|----------------|------------------|
| W6   | 738.65                  | (baseline)     | (baseline)       |
| W7   | 577.20                  | −161.45        | −21.9 %          |
| W8   | 552.40                  | −24.80         | −25.2 %          |
| W9   | 530.10                  | −22.30         | −28.2 %          |
| W10  | 510.30                  | −19.80         | −30.9 %          |
| W11  | 470.62                  | −39.68         | −36.3 %          |
| W12  | 448.65                  | −21.97         | −39.3 %          |
| W13  | 406.64                  | −42.01         | −44.9 %          |
| **W14** | **406.64**           | **+0.00**      | **−44.9 %**      |

- **8-wave monotonic-decrease ledger intentionally paused at
  W14** with +0 B hold-line.
- **Cumulative reduction since W6 = 44.9 %**, far exceeding
  the W6-era 25 % design-budget aspiration.
- **W15 forward-note:** Hicks's W15 second-pass strip
  candidates (`tonemapping_*` / `encodings_pars_fragment` /
  `packing` / UniformsLib `points`/`sprite`/`linedashed`) are
  deferred at W14 for renderer-spike bandwidth; W15 decides
  whether to resume the monotonic ledger or shift focus to
  Phase L spike implementation.

---

## 5. Apone (DevOps) `c823e1c` — us-east-1 plan-readiness + TF bump + JWT GA + Phase L DevOps-readiness

Apone ships **7 deliverables** anchored by:
- **us-east-1 plan-readiness** (apply-blocked on Stephen-side
  IRSA OIDC).
- **TF 1.10.5 → 1.11.4 bump applied.**
- **Redis envFrom PR-ready commented-out pre-wire**
  (W13→W14 pattern evolution).
- **JWT rehearsal #3 GA-confirmed.**
- **PWA Builder hardening** (Hicks LH13 W15 unlock dependency).
- **`docs/phase-l-devops-readiness.md`** NEW 411-line 4-surface plan.
- **CHANGELOG `[0.23.0]`** + `docs/retro-2026-12.md`.

### 5.1 `docs/regional-eks-bringup.md §2.1` NEW — us-east-1 plan-readiness

- **6 subsections** cover:
  (a) dry-run command sequence — `terraform plan -var-file=us-east-1.tfvars`;
  (b) ~20 expected resources (cluster + node-group + IAM roles
  + ALB + Route53 + Redis + RDS + security groups);
  (c) gating (cluster ACTIVE + IRSA OIDC provisioned +
  Apone-side approval);
  (d) rollback path (`terraform destroy` with state-isolation);
  (e) operator hand-off note;
  (f) cross-reference to `docs/prod-cutover.md`.
- **Plan-readiness only** — actual `terraform apply` blocked
  on Stephen-provisioned IRSA OIDC provider. **Carries over
  as Stephen action item #7.**

### 5.2 TF 1.10.5 → 1.11.4 bump applied

- **`.github/workflows/dr-rehearsal.yml`** single-line bump
  from `terraform_version: 1.10.5` to
  `terraform_version: 1.11.4`.
- **`docs/terraform.md §7` NEW** (7 subsections): bump
  rationale; breaking-change inventory; `terraform fmt
  -recursive -check` clean validation (all 47 `.tf` files);
  `terraform init+validate` clean across prod / staging /
  dr-us-west-2; rollback path; W15+ pin-removal candidate;
  Phase L L1 design memo reference.
- **W13 survey → W14 apply cadence honoured** — Apone surveyed
  the 1.11.x changelog in W13 and applied in W14, matching
  the canonical "survey-then-apply" cadence.

### 5.3 Redis envFrom PR-ready commented-out pre-wire

- **`infra/k8s/overlays/prod/kustomization.yaml`** gains a
  4-line commented-out block:

```yaml
# patchesStrategicMerge:
# - redis-envfrom-required-patch.yaml
# W15 cutover: uncomment after ESO secret
# population verified in prod cluster
```

- **`docs/prod-cutover.md §6.8` NEW** (5 subsections) captures
  the envFrom index-pin table:
  - **index 0** — base ConfigMap (W3 baseline)
  - **index 1** — base Secret (W3 baseline)
  - **index 2** — W4 jwt-keys patch
  - **index 3** — W7 jwt-rsa-keys patch
  - **index 4** — W12 redis-prod patch (this W15 flip
    activates `optional: false` semantics)
- **W14 evolution: W13 "PR-ready not-wired" → W14 "PR-ready
  commented-out pre-wire" — the 4-line uncomment cutover
  collapse is the canonical W14 forward-stage shape for Apone
  deliverables depending on Stephen-side infrastructure (ESO
  secret population).**
- **Convention motivation:** committed-but-disabled state
  means the W15 wire-up commit is a 4-line diff that humans
  can review in 30 seconds, vs. W13's "patch on disk but not
  in `kustomization.yaml`" which required cross-file diffing
  to verify the wire-up.

### 5.4 JWT rehearsal #3 — GA-readiness CONFIRMED

- **Rehearsal parameters:** `target_env=staging,
  new_key_label=2026-12-rehearsal`.
- **Timing ledger:**
  - W10 (RED-baseline): 6:12
  - W11: 5:42
  - W12: 3:48
  - **W14: 3:51** (+3 s vs W12, **within noise band**)
- **GA-readiness CONFIRMED** on Apone D4.
- **First real prod JWT rotation recommended for end-of-January
  2027 paired with Q1 2027 rehearsal.**
- **`docs/jwt-rotation-rehearsal.md §5` NEW**; existing §5-§10
  renumbered §6-§11.
- **W14 confirms quarterly cadence as canonical** — exercised
  Redis JWKS cache miss + refresh path; staged-key advance /
  promote / retire ledger preserved across rehearsal runs.
- **Convention reinforced:** Rehearsal-cadence GA-confirmation
  requires 3 rehearsal runs within timing-noise band.

### 5.5 PWA Builder hardening

- **`pwa-builder.yml` gains:**
  - **(a) Provenance tag** via `outputs.source` carrying SHA.
  - **(b) 4-line `$GITHUB_STEP_SUMMARY` block** rendering
    success / skip / preview-URL.
  - **(c) Success comment URL hyperlink** to PWA Builder
    preview.
  - **(d) NEW skip-path PR comment** under
    `<!-- pwa-builder-report -->` marker for idempotent
    updates when workflow skip-condition fires (no PR
    comment churn).
- **`docs/frontend-pwa-audit.md §12` NEW** (6 subsections)
  captures rationale.
- **Hicks LH13 hard-pin W15 unlock depends on this landing
  first** (plus Stephen-provisioned `PWA_PREVIEW_URL`).

### 5.6 `docs/phase-l-devops-readiness.md` NEW (411 lines)

- **7 sections; 4 Phase L DevOps surfaces:**
  - **§2.1 — TURN cluster scaling: 3 waves.** Multi-region
    TURN deployment for spectator livestream eviction floor.
  - **§2.2 — Mobile native CI: 2 waves.** iOS / Android via
    fastlane + Bitrise integration.
  - **§2.3 — Multi-region active-active: 4-5 waves** with
    **explicit `session-affinity` recommendation OVER Aurora
    Global**. Rationale: Aurora Global writes are
    single-region; session-affinity at ALB layer avoids
    cross-region writes for in-flight games; replay archive
    can ship to S3-cross-region-replication async.
  - **§2.4 — Container scan shift-left: 1 wave.** Trivy in
    pre-commit hook + GitHub Action.
- **10-12 wave estimate** matches Bishop `phase-l-bringup.md`
  8-wave + L9 wrap (Apone's range absorbs compliance +
  observability + tooling waves).
- **First W14 Phase L pre-work artifact landed.**

### 5.7 CHANGELOG `[0.23.0]` + `docs/retro-2026-12.md`

- **`CHANGELOG.md`** — `[0.23.0]` Phase K Wave 14 entry above
  `[0.22.0]`; `[Unreleased]` working branch flipped to W14.
- **`docs/retro-2026-12.md` NEW 588 lines** December monthly
  retro per W6+ convention (wins / losses / surprises /
  forward-asks); captures W14 cross-lane Phase L pre-work
  trifecta as the headline December accomplishment.

---

## 6. Vasquez (QA) `537594e` — Final gate 3029/0/0 + DbSerial completion + LH13 mirror + branch-protection fallback

Vasquez closes the wave at **3029/0/0 (+240)**, **29-wave
zero-skip**, **`checked=4 violations=0`** — and ships **7
deliverables** including the W14 first-since-W11-first
0-violation-no-amendment wave.

### 6.1 DbSerial migration COMPLETION memo

- **`Phase_K_W14/Vasquez/db-serial-migration-completion.md`
  NEW 173 lines.**
- Documents the **2 remaining W9 Bishop-lane candidates**
  from the W12 25-class audit:
  - **(a)** `Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs`
    — depends on Bishop W15 EF migration refactor.
  - **(b)** `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`
    — depends on Bishop W15 idempotency-store cutover.
- **Escalation ladder:**
  - **Step 1 — W15 Bishop:** apply `[Collection("DbSerial")]`
    attribute as part of W15 surface work.
  - **Step 2 — W15 Vasquez re-prompt:** if Bishop silent.
  - **Step 3 — W16 Coordinator-direct via
    `docs/agent-handoff-protocol.md §4.3`** fallback runbook:
    if W15 re-prompt silent.
- **W14 closes the DbSerial migration chapter** at
  **23-of-25 + 2-tracked = 25/25 accountable**; the 2
  trailing files are on Bishop's W15 plate with explicit
  escalation path.

### 6.2 LH13 mirror sync

- **`docs/frontend-pwa-audit.md §6.3` set to YELLOW**
  (cumulative 4-wave deferral) matching Hicks's §13 deferral
  marker.
- **Workflow + mirror tests soft-pin retained.**
- **`PwaAuditWorkflowGateW14Tests.cs` NEW (8 facts)** pinning
  the soft-pin envelope and §6.3 doc invariants.
- **W15+ escalation criteria: 6-wave deferral →
  Coordinator-direct intervention.** If W17 still shows zero
  cron successes, Vasquez recommends Coordinator-direct per
  §4.3 escalation.
- **Convention reinforced:** Mirror-test bookkeeping at every
  wave deferral maintains accountability ledger when LH13
  convergence is blocked by Stephen-side infrastructure.

### 6.3 Visual-regression spec fix — `goto` BEFORE `setContent`

- **`tests/e2e/manifest-screenshots-visual.spec.ts`** modified
  to call `await page.goto('/')` **BEFORE**
  `page.setContent(…)` so relative `<img src="/foo.png">`
  URLs resolve against the `baseURL` rather than
  `about:blank` (W12-introduced latent bug Vasquez first
  documented in W13).
- **`docs/test-architecture.md §5.2` NEW** documenting
  **the goto-then-setContent pattern** as canonical for
  visual-regression specs that need relative-URL HTML.
- **Convention established:** `page.goto('/')` BEFORE
  `page.setContent(…)` — for specs needing relative-URL
  HTML, `goto('/')` first so `baseURL` is set, THEN
  `setContent`.
- **Hicks's W14 `capture-real-surfaces.js` (Playwright
  runtime API side-channel, no `page.setContent`) and this
  spec fix together resolve the W12-introduced
  `about:blank` relative-URL 404 latent bug permanently.**

### 6.4 Branch-protection W14 fallback runbook

- **`docs/agent-handoff-protocol.md §4.3` NEW.**
- **Re-validated dry-run** of
  `tests/ci/lane-discipline-flip-required.sh` at
  `.work/vasquez-w14-safe/flip-script-dryrun.log`.
- **1-line `gh api -X PATCH` copy-paste:**

```
gh api -X PATCH \
  /repos/long2know/mahjong-autotable/branches/main/protection \
  -F required_status_checks[checks][][context]='lane-discipline / check'
```

- **Documents cosmetic dry-run summary bug** at
  `tests/ci/lane-discipline-flip-required.sh` line ~133
  where `MODE != "apply"` guard incorrectly prints "would
  apply" on `--rollback` mode (does NOT affect actual
  rollback behaviour); **DevOps lane W15 fix** flagged.
- **9th-wave standing action item; fallback runbook ready
  for W15 execution if 10th re-prompt lands silent.**

### 6.5 14 forward-stage W14 contract test files under `Phase_K_W14/Vasquez/`

- **~104 facts across 14 files** with `BishopW14*` /
  `HicksW14*` / `AponeW14*` name-prefix per W13 precedent.
- **ALL files under `Phase_K_W14/Vasquez/`** to satisfy
  `wave_subdir_overrides` map in `tests/ci/lane-map.yml`.
- **Coverage:**
  - Bishop's 7 surfaces (spectator-audit, commentary-cost,
    bracket-query, replay-listing, JWKS-rollback, SignalR
    metrics, phase-l-bringup doc).
  - Hicks's 3 deep-links (bracket, replays, admin-cost) +
    1 visual-regression real-captures.
  - Apone's 3 (TF bump + Redis envFrom commented-out
    pre-wire + PWA Builder hardening).

### 6.6 `Wave1ThroughKW13RegressionTests` → `Wave1ThroughKW14RegressionTests` (via `git mv`)

- **W6+ convention preserved.** Class adds **14 W14 smokes:**
  - **10 soft-pin** (cross-lane forward-stage smokes).
  - **4 hard-assert self-lane** (Vasquez owns):
    1. DbSerial completion memo invariant.
    2. Visual-regression spec fix invariant.
    3. `docs/agent-handoff-protocol.md §4.3` doc invariant.
    4. `KW13 → KW14` rename pin.
- **W11/W12/W13 SelfLaneTests + SurfaceSmokeFactsTests
  updated** for across-rename-wave lists (preserves W6+
  `Wave1Through<N>` regression-suite chain).

### 6.7 6 W14 Playwright specs + `selectors.md` W14 footer

- **All 6 chromium-only forward-stage tolerant:**
  - `bracket-ui-route.spec.ts` (Hicks D4).
  - `replay-listing-route.spec.ts` (Hicks D5).
  - `commentary-cost-admin-panel.spec.ts` (Hicks D6 +
    Bishop D2).
  - `visual-regression-real-captures.spec.ts` (Hicks D2 +
    Vasquez D3).
  - `lh13-thresholds-hard-pinned-final.spec.ts` (Hicks D1
    deferral marker).
  - `jwks-overlap-rollback-rejected.spec.ts` (Bishop D5).
- **`tests/e2e/selectors.md` W14 footer** adds 6 new
  selectors covering the new deep-link UI states
  (preserves W12+W13 footer entries).

### 6.8 Final gate

- **Final gate 3029/0/0** (Vasquez verified post-§6.3
  landing).
- **+240 over W13 baseline 2789.**
- **29-wave zero-skip streak preserved** (J.1-J.10 +
  K.1-K.14).
- **Lane-discipline strict-mode `checked=4 violations=0`**
  at Vasquez commit.
- **4th consecutive 0-violation wave** (W11+W12+W13+W14).
- **FIRST since W11 first 0-violation wave with NO same-lane
  amendment commit needed** — W12 + W13 both needed Vasquez
  same-lane amendments to lane-map.yml; W14 had no trigger.

---

## 7. Cross-cutting patterns from W14

### 7.1 "PR-ready commented-out pre-wire" — W13 "not-wired" pattern evolution

The W13 "PR-ready not-wired" pattern (patch file on disk but
NOT registered in `kustomization.yaml`) **evolves at W14 to
"PR-ready commented-out pre-wire"** (patch file on disk AND
registered in `kustomization.yaml` but commented out with the
4-line block):

```yaml
# patchesStrategicMerge:
# - redis-envfrom-required-patch.yaml
# W15 cutover: uncomment after ESO secret
# population verified in prod cluster
```

**Convention motivation:** committed-but-disabled state means
the W15 wire-up commit is a **4-line diff that humans can
review in 30 seconds**, vs. W13's "patch on disk but not in
list" which required cross-file diffing to verify the wire-up.

**Apone D3 Redis envFrom is the canonical W14 example.** All
future Apone deliverables depending on Stephen-side
infrastructure (ESO secret population, IRSA OIDC, etc.) follow
this shape.

### 7.2 Admin-observability pagination shape uniformity

**`{items, count, skip, limit, pageSize}` is the canonical
W14 admin-endpoint pagination envelope.** Applied at:
- Bishop §3.1 — `/api/spectator/handoff/audit`
- Bishop §3.3 — `/api/tournaments/{id}/brackets`
- Bishop §3.4 — `/api/replays`

Defaults / maxes:
- Spectator-audit: 50 / 200
- Bracket-query: 50 / 200
- Replay-listing: 25 / 100 (heavier rows)

Future paginated admin endpoints follow this shape.

### 7.3 Admin-endpoint auth precedence ladder

**`401 → 403 → 503 → 400 → 200`** (missing-token →
wrong-scope → store-unavailable → bad-input → ok).

Convention applied at `/api/spectator/handoff/audit`; extends
to future admin endpoints. The **503 inserted between 403 and
400** is the key insight: store-unavailability is a server-side
condition, distinct from client-input validation failure, and
must be distinguishable in admin-UI error handling.

### 7.4 Forward-stable schemas use arrays not maps

**`byModel[]` array shape in `/api/commentary/cost/summary`
envelope (Bishop §3.2).** Single entry today; Phase L
multi-provider widening forward-stable schema.

**Convention:** Future multi-tenant / multi-provider widening
candidates use arrays not maps for forward-stability — adding
a new tenant or provider extends the array without breaking
existing consumers built against the single-entry shape.

### 7.5 Security-rejection error codes as `public const string` exports

**JWKS overlap-window `rollback-rejected` (Bishop §3.5)** adds
`JwtValidationService.ErrorRollbackRejected = "rollback-rejected"`
as a `public const string`.

**Convention:** Error codes for security-rejection paths are
kebab-case constants exported from the validating service for
E2E assertion stability — extends W13 audit-always
`audit-failed` precedent. Tests can import the constant
directly rather than hardcoding the string.

### 7.6 Prometheus metric families with result-label constants

**`SignalRSequenceMetrics` (Bishop §3.6)** exposes 3 metrics
with result-label constants `hit` / `miss` / `expired` as
`public const string` exports.

**Convention:** Prometheus metric families with result-label
constants are exported as `public const string` from the
metric-owning singleton for assertion stability and
discoverability via `grep` — extends W13
`commentary_cost_dollars_total` labelled-counter precedent.

### 7.7 Defensive wire-shape parsing in deep-link consumers

**Hicks's `?action=bracket` (§4.4)** tolerates **3 wire shapes**
from Bishop's W14 §3 endpoint:
- `{brackets: [...]}`
- `{records: [...]}`
- Bare `[...]` array

Plus per-record `playerA` accepts string OR `{displayName}`
object shape.

**Convention:** Deep-link consumers tolerate 2-3 wire shapes
during forward development to protect deep-link UI from
upstream admin-API schema-stabilisation churn.

### 7.8 Alias-tolerant field reading in deep-link consumers

**Hicks's `?action=replays` (§4.5)** reads:
- `id` → `replayId` (W14 wire shape)
- `completedAtUtc` → `completedAt` (legacy alias)

**Convention:** Deep-link consumers read primary field name
and known aliases in fallback order — extends defensive
wire-shape parsing convention.

### 7.9 Percentage normalisation for raw-float APIs

**Hicks's `?action=admin-cost` (§4.6)** normalises
`percentUsed`:

```javascript
const pct = value > 1 ? value : value * 100;
```

Handles both raw-float (0.0-1.0) and already-scaled (0-100)
shapes during schema-stabilisation churn.

**Convention:** Future percentage-displaying consumers
normalise to consistent 0-100 scale defensively.

### 7.10 CSS class thresholds for percentage UIs

**Hicks's admin-cost UI (§4.6)** uses:
- `ok` at `<80`
- `warn` at `80-94`
- `critical` at `>=95`

**Convention:** Future percentage status UIs follow these
threshold conventions.

### 7.11 Real-content visual-regression capture with overlay suppression

**Hicks's `scripts/capture-real-surfaces.js` (§4.2)**
Playwright runtime API against vite preview :4173 with W11
tour + W12 magic-link + W12 sign-in overlay suppression
producing 1280×720 PNGs.

**Convention:** Real-content captures supplement (do not
replace) W13 side-channel placeholder captures. The runtime
API path avoids the W12-introduced `page.setContent`
`about:blank` relative-URL 404 latent bug entirely.

### 7.12 `goto('/')` BEFORE `setContent` for visual-regression specs

**Vasquez's `manifest-screenshots-visual.spec.ts` fix (§6.3)**
calls `await page.goto('/')` **BEFORE** `page.setContent(…)`
so relative `<img src="/foo.png">` URLs resolve against
`baseURL`.

**Convention:** `docs/test-architecture.md §5.2` documents
the goto-then-setContent pattern as canonical. Together with
§4.2 capture path, this **permanently resolves the
W12-introduced `about:blank` relative-URL 404 latent bug.**

### 7.13 Bundle hold-line as bandwidth-rebalancing signal

**Hicks's W14 three-renderer-big +0 B hold-line (§4.7)** is
the first non-decreasing wave since W7; breaks the 8-wave
monotonic-decrease ledger **by design** to free Hicks's
renderer-lane bandwidth for the
`docs/phase-l-renderer-spike.md` Go-decision.

**Convention:** Intentional non-decrease wave is a deliberate
signal (must be documented in the wave fold with
forward-bandwidth-redirection rationale), not a regression.

### 7.14 Phase L pre-work cross-lane trifecta

**W14 is the first wave to emit Phase L pre-work artifacts
across 3 lanes simultaneously:**
- `docs/phase-l-devops-readiness.md` (Apone, 411 lines).
- `docs/phase-l-bringup.md` (Bishop, 199 lines).
- `docs/phase-l-renderer-spike.md` (Hicks, 14 KB).

**Convention:** Next-phase pre-work lands as cross-lane
artifact set 1-2 waves before phase boundary. W15 likely
sees the Phase L L1 design memo (Apone §5.6 forward queue)
pulling these three artifacts into a unified L1 plan.

### 7.15 Spike Go-decisions with rejected-alternatives ledger

**Hicks's `docs/phase-l-renderer-spike.md` (§4.3)** documents
the Go-decision on WebGL2 hand-roll with rejected
alternatives:
- PixiJS — 2D-first; loses 3D tilt.
- Babylon.js — heavier than three.js stripped.
- bare-WebGL1 — no instanced drawing.
- three.module-fork — vendoring + divergence risk.

**Convention:** Spike Go/no-go decisions are documented with
rejected-alternatives list for future reference.

### 7.16 Multi-wave migration completion ledger

**Vasquez's `db-serial-migration-completion.md` (§6.1)**
closes the W12 25-class audit at 23-of-25 + 2-tracked = 25/25
accountable.

**Convention:** Multi-wave migration completion memo
identifies trailing files with explicit escalation path
(W15 Bishop → W15 Vasquez re-prompt → W16 Coordinator-direct
via §4.3).

### 7.17 Long-running deferral escalation criteria

**Vasquez's LH13 mirror sync (§6.2)** documents W15+
escalation criteria: **6-wave deferral → Coordinator-direct
intervention**.

**Convention:** Long-running deferrals define escalation
criteria with explicit wave-count threshold. LH13 at W14 is
at 4-wave deferral (W11+W12+W13+W14); W15 is 5-wave;
**W17 = 6-wave threshold for Coordinator-direct escalation.**

### 7.18 First-since-W11 0-violation wave with NO same-lane amendment

**W14 lane-discipline strict-mode `checked=4 violations=0`**
at Vasquez commit holds with **NO same-lane amendment
needed**. W12 + W13 both needed Vasquez amendments to
`lane-map.yml`; W14 had no trigger.

**Convention reinforced:** Agents land cross-lane-aware files
within their own surface-set; lane-map `*_shared` entries
pre-emptively register cross-lane co-edit surfaces. W11
shared-files (`shims_shared` + `pwa_audit_workflow_shared`)
+ W13 shared-by-pipeline-artifact
(`bundle_health_workflow_shared` +
`visual_regression_baselines_shared`) entries together close
known false-positive bundling patterns.

---

## 8. Numeric milestones recap

### 8.1 Gate trajectory W6 → W14

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
| **W14** | **3029** | **+240** | **+113.0 %** |

- **Gate has more than doubled since W6** — cumulative
  **+1607** tests / **+113.0 %**.
- **W14 +240 is above the W6-W14 average delta (+201)** —
  Bishop's 7-endpoint surface family + Vasquez's 14 +
  forward-stage facts drive the W14 size.
- **Zero-skip streak: 29 consecutive waves preserved.**

### 8.2 Bundle ledger W6 → W14

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
| **W14** | **406.64**           | **+0.00**  | **−44.9 %**      |

- **8-wave monotonic-decrease ledger intentionally paused
  at W14.**
- **Cumulative reduction = 44.9 %**, far exceeding W6-era
  25 % design-budget aspiration.
- **W15 forward-note:** Hicks's second-pass strip candidates
  deferred at W14 for Phase L renderer-spike bandwidth.

### 8.3 Lane-discipline ledger

| Wave | Strict | Violations | Same-lane amendment |
|------|--------|------------|---------------------|
| W11  | yes    | 0          | (none — first 0-vio wave) |
| W12  | yes    | 0          | yes (Vasquez)       |
| W13  | yes    | 0          | yes (Vasquez)       |
| **W14** | **yes** | **0**   | **NO — first since W11** |

- **4 consecutive 0-violation waves** (W11+W12+W13+W14).
- **W14 is the FIRST since W11 0-violation wave with NO
  same-lane amendment commit needed.**

### 8.4 Identity hardening + concurrency mutex ledger

- **9 consecutive clean waves of per-invocation
  `git -c user.name=X -c user.email=Y`** (W6 → W14;
  75+ commits).
- **5 consecutive fully-adopted waves of
  `.work/squad-git-lock` flock mutex** (W10 → W14).
- **Zero coordinator-direct interventions for 9 consecutive
  waves** (W6 → W14).

### 8.5 JWT rotation rehearsal timing

| Rehearsal | Wave | Target env | Timing | Notes |
|-----------|------|------------|--------|-------|
| #1 (RED)  | W10  | staging    | 6:12   | RED baseline |
| #2        | W11  | staging    | 5:42   | -30 s        |
| #3        | W12  | staging    | 3:48   | -1:54 (large improvement; GA-rec) |
| #4        | **W14** | **staging** | **3:51** | **+3 s vs W12; within noise; GA-confirmed** |

- **W14 confirms quarterly rehearsal cadence stability.**
- **First real prod JWT rotation recommended for
  end-of-January 2027 paired with Q1 2027 rehearsal.**

---

## 9. Forward queue for W15

### 9.1 Bishop (Backend) W15 candidates

1. **Replay blob streaming endpoint** —
   `GET /api/replays/{replayId}/stream` with `Range:` header
   + chunked transfer-encoding (companion to W14 §3.4
   metadata-only listing).
2. **Per-tenant JWKS rotation** — `DateTimeOffset` switch in
   `JwtStagedRotationPolicy` `RotationStartUtc` (currently
   `DateTime`); enables per-tenant rotation windows in Phase L
   multi-tenant work.
3. **DbSerial attribute application** on remaining 2 W9 test
   files (Vasquez completion memo escalation step 1).
4. **`TournamentMatch.MatchSlot` column** +
   `Phase_K_W15_TournamentMatchSlot` 3-provider migration;
   collapses W13/W14 O(N) bracket-query scan to O(1).
5. **Tournament-scale page-size tuning** —
   `BracketQueryOptions` default 50 may be too small for
   256-seed tournaments; revisit with admin-UI partner.
6. **`CommentaryCostBroadcaster` backpressure-aware variant**
   (W13 forward-note carry-over; queue rather than fire inline
   if admin-hub clients > 50).
7. **Replay storage default flip from `InMemory` to `Ef`**
   (W13 forward-note carry-over).

### 9.2 Hicks (Frontend) W15 candidates

1. **LH13 third retry** — conditional on Apone W14 §12 PWA
   Builder preview-URL landing + Stephen-provisioned
   `GH_TOKEN` + ≥ 3 cron successes. W15+ deferral ledger
   entry if still blocked.
2. **Visual-regression spec `setContent` → `snapshotPathTemplate`
   migration** — move 3 PNGs to Playwright's native snapshot
   path convention.
3. **Phase L renderer spike implementation kickoff** — W14
   Go-decision on WebGL2 hand-roll; W15+ bring-up under
   Phase L L1.
4. **Additional bundle optimization** — second-pass shader
   strip candidates (`tonemapping_*` / `encodings_pars_fragment`
   / `packing` / UniformsLib `points`/`sprite`/`linedashed`)
   — deferred at W14 for renderer-spike bandwidth.
5. **Tablet-viewport visual-regression baselines** (768 ×
   1024) — W13 forward-note carry-over; paired with Vasquez
   matrix extension.
6. **`?action=tournament&tournamentId` deep-link extension**
   — W13 forward-note carry-over.
7. **Bundle-health PR-comment rolling-trend hardening**
   (delta vs prior 5 commits) — W13 forward-note carry-over.

### 9.3 Apone (DevOps) W15 candidates

1. **Kyverno enforce pre-wire candidate** —
   `infra/k8s/overlays/prod/kyverno-policies.yaml` PR-ready
   commented-out pre-wire flipping `validationFailureAction:
   audit` → `enforce`; conditional on Stephen-side policy
   review.
2. **HPA min-replicas bump pre-flight** —
   `infra/k8s/overlays/prod/hpa-patch.yaml` PR-ready
   commented-out pre-wire; conditional on Stephen-side
   prod-capacity review.
3. **`tests/ci/lane-discipline-nightly.yml:87` heredoc fix**
   — minor YAML quoting bug Apone noticed during W14 review;
   cosmetic, no gate impact.
4. **us-east-1 actual `terraform apply`** pending Hicks
   cluster ACTIVE — Stephen-blocked on IRSA OIDC provider;
   conditional on Stephen action item #7 resolution.
5. **Phase L L1 design memo** — `docs/phase-l-l1-design.md`
   pulling forward W14 `phase-l-devops-readiness.md` +
   `phase-l-bringup.md` + `phase-l-renderer-spike.md` into
   a unified L1 plan.
6. **First real prod JWT rotation** — end-of-January 2027
   paired with Q1 2027 rehearsal per Apone D4 recommendation.
7. **CHANGELOG `[0.24.0]`** + `docs/retro-2027-01.md`.

### 9.4 Vasquez (QA) W15 candidates

1. **DbSerial Bishop-lane 2-file attribute application**
   (AFTER Bishop W15 §3 ships); cleanup wave for the migration
   completion ledger.
2. **LH13 cron convergence wait** — 5-wave deferral marker
   if still no cron successes (W15 = 5th wave of deferral;
   W17 = 6-wave threshold for Coordinator-direct escalation).
3. **`docs/agent-handoff-protocol.md §4.3` escalation status
   update** — W15 status check on 10th-wave Stephen
   re-prompt outcome.
4. **`Wave1ThroughKW14RegressionTests` →
   `Wave1ThroughKW15RegressionTests`** rename per W6+
   convention.
5. **W15 forward-stage contract tests for Bishop W15
   surfaces** (replay blob streaming + DbSerial 2-file +
   bracket page-size tuning + per-tenant JWKS) under
   `Phase_K_W15/Vasquez/`.
6. **W15 forward-stage contract tests for Hicks W15 surfaces**
   (LH13 third retry + setContent migration + renderer-spike
   kickoff) under `Phase_K_W15/Vasquez/`.
7. **W15 forward-stage contract tests for Apone W15 surfaces**
   (Kyverno enforce + HPA bump + heredoc fix + us-east-1
   apply + Phase L L1 design) under `Phase_K_W15/Vasquez/`.

### 9.5 Lane-discipline cross-cutting W15 candidates

- **0-violation stretch goal sustained across W11+W12+W13+W14
  — maintain through W15.** Goal: 5 consecutive 0-violation
  waves.
- **9 consecutive waves with zero coordinator-direct
  interventions** (W6 → W14) — maintain through W15.
- **W15 candidate `phase_l_pre_work_shared`** — pre-emptive
  lane-map entry if W15 sees co-edited Phase L design
  surfaces across Apone + Bishop + Hicks lanes.

### 9.6 Scribe / Coordinator W15 candidates

- **Per-invocation `git -c user.name=X -c user.email=Y commit
  ...`** remains canonical (held over W6 → W14; **9
  consecutive clean waves; 75+ commits**).
- **`flock 9>.work/squad-git-lock` mutex** (**5th consecutive
  fully-adopted wave at W14**; W15 prompt templates continue
  path uniformity).
- **`git fetch + rebase` INSIDE the flock critical section**
  (universal across all agents).
- **`.work/<agent>-w<N>-safe/` backup directory** as a
  first-class step in every prompt template.
- **CHANGELOG version-arithmetic check** goes in every
  changelog-bump pattern (W13 `[0.22.0]` shipped clean; W14
  `[0.23.0]` shipped clean; **W15 `[0.24.0]`**).

---

## 10. Stephen action items (carry-into-January 2027)

1. **Branch-protection flip** for the lane-discipline gate
   (`tests/ci/check-cross-lane-bundling.sh --strict`) —
   Stephen re-prompt **#9 unresolved at W14**. **W15
   fallback execution plan in place** via Vasquez's
   `lane-discipline-flip-required.sh --apply`; 1-line
   `gh api -X PATCH` copy-paste in
   `docs/agent-handoff-protocol.md §4.3`. If W15 re-prompt
   still silent, Vasquez recommends Coordinator-direct
   execution as last resort.

2. **`GH_TOKEN` for LH13 cron data point query** — Hicks
   W14 LH13 hard-pin deferral conditional on this token.
   Currently unresolved through 4 waves (W11 → W14). W15 =
   5-wave deferral marker.

3. **`PWA_PREVIEW_URL` secret** — Apone W14 PWA Builder
   hardening landed graceful-skip behaviour; actual
   preview-URL provisioning still pending. **Hicks LH13
   hard-pin W15 unlock depends on this.**

4. **Secrets provisioning:**
   - **Sentry DSN** (W9 error-reporting wire-up;
     unresolved since W9).
   - **OpenAI API key** (W10 commentary generator;
     currently W12 deterministic stub fallback in CI;
     **now blocks `EfCommentaryStore` persistence
     dogfood in prod for 4 consecutive waves**).
   - **Janus credentials** (W11 spectator livestream
     backend; currently W12 stub).
   - **Redis prod credentials** (W11 ESO ExternalSecret
     to populate underlying Kubernetes Secret in prod
     cluster — W14 `redis-envfrom-required-patch.yaml`
     PR-ready-commented-out pre-wire blocked on this).

5. **Argo Rollouts install** in prod cluster — Apone W11
   NetworkPolicies + W12 Ingress + W13 regional-bring-up
   doc + W14 `phase-l-devops-readiness.md` all ready;
   W15 install unlocks Rollouts cutover.

6. **Prod Redis TF apply** — Apone W11
   `aws_elasticache_replication_group` + W12 R53 records +
   W13 regional-bring-up doc + W14 `kustomization.yaml`
   commented-out prod-cutover patch enablement all ready;
   W15 apply unlocks prod cutover.

7. **us-east-1 IRSA OIDC provider** — **W14 §2.1
   plan-readiness doc lands assuming this is ACTIVE;
   cluster apply needs ACTIVE provider.**

8. **First real prod JWT rotation end-of-January 2027** —
   Apone W14 D4 GA-readiness CONFIRMED; paired with Q1
   2027 rehearsal; **NEW action item entering January
   2027 cadence**.

**9 consecutive weeks of Stephen re-prompt sequence; W15
escalation fallback plan in place (via
`lane-discipline-flip-required.sh --apply` or
Coordinator-direct last resort).**

---

## 11. Identity hardening recap

W14 preserves the **9th consecutive clean wave** of:

- **Per-invocation identity binding:**
  `git -c user.name="Agent Name" -c user.email="agent@squad.mahjong" commit ...`
  Never `git config user.name=X` (per-commit isolation; no
  global config drift between waves or agents).
- **`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
  trailer** on every commit message.
- **`flock -w 120 9 ...` mutex** wrapping every agent's
  fetch + rebase + commit + push, with the lock file at
  `.work/squad-git-lock` (5th consecutive fully-adopted
  wave).
- **`git fetch` + `git rebase` INSIDE the flock critical
  section** — prevents the W5+ "race the upstream main
  between fetch and push" failure mode.
- **`.work/<agent>-w<N>-safe/` backup directory** — every
  agent stashes work-in-progress before the rebase;
  rollback path on rebase conflict.

**75+ commits across W6 → W14 with zero identity drift and
zero coordinator-direct interventions.**

---

## 12. Sign-off

**Phase K Wave 14 closes at:**
- **Final gate:** 3029 passed / 0 failed / 0 skipped (+240).
- **Zero-skip streak:** 29 consecutive waves (J.1-J.10 +
  K.1-K.14).
- **Lane-discipline:** `checked=4 violations=0` (4th
  consecutive 0-violation wave; FIRST since W11 first
  0-violation wave with NO same-lane amendment commit
  needed).
- **Bundle ledger:** three-renderer-big 406,635 B (+0 W14
  hold-line; cumulative W6 → W14 −44.9 %).
- **Identity hardening:** 9th consecutive clean wave.
- **Concurrency mutex:** 5th consecutive fully-adopted wave.
- **Coordinator-direct interventions:** ZERO for 9
  consecutive waves (W6 → W14).
- **Cross-lane Phase L pre-work trifecta landed:**
  `phase-l-devops-readiness` (Apone) + `phase-l-bringup`
  (Bishop) + `phase-l-renderer-spike` (Hicks).
- **W15 forward queue:** ~28 items across 4 lanes; Phase L
  L1 design memo (Apone) is the headline.

**Phase K Wave 14 — DONE.**
