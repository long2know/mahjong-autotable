# Phase K — Wave 17 Summary

- **Branch:** `stlong/phase-k-wave-17-bringup`
- **Base:** `main` @ `c866535`
- **Head:** (Scribe sweep — gate captured at Vasquez head `fcf741d`)
- **Date:** 2027-01-XX (late-January 2027 window; Vasquez memo dated 2026-11-30 reflecting the QA log-anchor convention; Apone memo dated 2027-01-22)
- **Final gate:** **3930 passed / 0 failed / 0 skipped** (+309 over W16; +2,508 over W6 baseline 1422 = **+176.4 %**; gate is now **2.76× the W6 baseline**)
- **Zero-skip streak:** **32 consecutive waves** (J.1-J.10 + K.1-K.17)
- **Lane-discipline:** **`checked=5 violations=0` — 7th consecutive 0-violation wave** (W11+W12+W13+W14+W15+W16+W17); **NO same-lane amendment required — 4th unamended wave in the 7-wave streak** (W11+W14+W16+W17; W12+W13+W15 amended). The 8-entry `shared_files` registry has held unchanged for **3 consecutive waves** (W15 amendment landing → W16 → W17).
- **Identity hardening:** **12th consecutive clean wave** (per-invocation `git -c user.name=X -c user.email=Y`)
- **Concurrency mutex:** **8th consecutive fully-adopted wave** of `flock -w 120 9 ... 9>.work/squad-git-lock`
- **Coordinator-direct interventions:** **ZERO for 12 consecutive waves** (W6 → W17)
- **Coordinator-direct EXECUTIONS:** **ONE — first since the no-pauses directive** (3-shot LH13 cron seed under §6.7 PRIMARY; categorically distinct from "intervention" — see §6.5 of this summary)
- **Three-renderer-big hold-line:** **7th consecutive wave** at 406,635 B (W11→W17)

---

## 1. Headlines

1. **Phase L W3 scene runtime + ray-cast picking + canonical
   atlas asset lands; `renderer-webgl2` chunk grows from W16
   19,017 B (mesh graph) to W17 24,743 B (+5,726 B) — STILL
   under the 40 KB W17 cap and consumes only 11.2 % of the
   180-220 KB Phase L envelope.** Hicks's
   `src/frontend/autotable-src/src/renderer-webgl2/` extends
   with `scene.ts` (`createTileScene()` orchestrator wiring
   `camera.ts` + `tile-mesh.ts` + `tile-atlas.ts` into a
   DPR-aware framebuffer with rAF-coalesced redraw scheduling
   and a `dispose()` lifecycle — no imperative draw loop, the
   scene only repaints when camera state or tile-set actually
   changes) and `picking.ts` (`pickTile(scene, x, y)` — NDC →
   inverse-view-projection ray construction + ray-AABB slab
   intersection against canonical tile bounds, returning the
   front-most hit's tile-instance index or `null`; **pure CPU
   math — no GPU `readPixels()` readback path**, so picking
   cost stays independent of WebGL2 driver latency). The
   `hello.ts` harness extends from W16's 2-mode (`?renderer=
   webgl2-hello` / `?renderer=webgl2-tile-mesh`) to a 3-mode
   driver adding `?renderer=webgl2-scene` (which mounts the
   scene + wires a click handler that calls `pickTile()`).
   The canonical atlas asset lands as
   `scripts/generate-tile-atlas-webgl2.js` (zero-dep
   deterministic PNG generator — hand-rolled IDAT zlib +
   IEEE 0xEDB88320 CRC32; PNG colour-type 6; 192×2176 layout;
   3 cols × 34 rows = 102 cells, 64 px each) emitting
   `src/frontend/autotable/img/tiles-atlas-webgl2.auto.png`
   (10,058 B; `.auto.png` suffix flags it as generator-
   managed). `vite.config.ts` gets a `copyStaticAssets()`
   block alongside the existing dice-copy / PWA-icons-copy
   plugins. `tile-atlas.ts` header (lines 1-37) loses the
   W16 STUB framing and points at the new canonical asset
   via `../img/tiles-atlas-webgl2.auto.png`. The W16 fallback
   grid texture path is preserved as a runtime safety-net.
   **Per-game URL-guard parameter is now 3-mode
   (`hello` / `tile-mesh` / `scene`) — the canonical Phase L
   feature-cutover entry** carried from W15.

2. **`autotable-src-eager` cold-path shrinks by 37,295 B at
   W17 alone (214,202 → 176,907 B, −17.4 %) — 2.65× the §3.2
   ≥14 KB target; cumulative W15 → W17 shrinkage = −45,940 B
   over 2 waves.** Hicks's §3.2 surgery moves three
   lobby-mounted modules off the eager cold path:
   `leaderboard` (W17 lazy bundle 11,349 B), `settings-drawer`
   (17,770 B), and `profile-page` (9,464 B). Each is mounted
   via a new `scheduleXLazyMount()` helper (`scheduleLeaderboardLazyMount()`,
   `scheduleSettingsDrawerLazyMount()`, `scheduleProfilePageLazyMount()`)
   that defers the dynamic `import()` until after the first
   paint frame. Cross-module navigation rides a new
   `mahjong:open-profile-page` `CustomEvent` queued before the
   lazy chunk has resolved — the page itself drains the queue
   on mount. Bundle ledger reads
   `autotable-src-eager.js 214.20 KB → 176.91 KB (−37.30 KB)`
   — **the largest single-wave §3.2 surgery cut since the
   audit landed**. The §3.2 ≥14 KB target was set at W14 and
   has been exceeded at W15 (−8,645 B) → W16 (−8,645 B in
   the published W16 figure; W17 wave consumed the second
   chunk) → **W17 −37,295 B (2.65× target)**.

3. **Bishop's 7-deliverable wave anchored by the JWKS
   validator wire-up at `JwtIssuingService.IssueForTenantAsync`
   — closes the W15 §3.2 + W16 §3.1 thread by routing
   per-tenant token issuance through `EnforceSigningAsync` for
   the first time.** The W16 surface landed
   `PerTenantJwksRotationValidator.EnforceSigningAsync` as a
   reachable seam but the only call site was the admin
   controller's verdict probe. **W17 wires the validator into
   the actual signing path:** `IssueForTenantAsync` invokes
   the validator on every per-tenant token issue and
   short-circuits before signing when the verdict is `Stale`
   or `StoreMissing`. Each block stamps a new
   `JwtIssueBlockedMetrics` Prometheus counter
   (`jwt_issue_blocked_total{reason}` with wire-stable labels
   `stale_per_tenant_policy` / `per_tenant_store_missing`) +
   a `ReconnectAuditEntry`
   (`auth.jwt.issue.blocked.stale_per_tenant_policy`). The
   collector is rendered by `MetricsEndpoint` on every scrape
   (HELP + TYPE preambles emit unconditionally so the schema
   is visible even at zero counts — preserves the wire-shape
   convention from W14's `pwa.*` counter set). **The W15
   table-before-validator + W16 validator-before-call-site
   2-wave pattern resolves at exactly the predicted W17 wave**
   (3-wave total from table → wire-up).

4. **§4.5 W17 RECALIBRATION — the W16 PROMOTE-to-PRIMARY
   conclusion is INVALIDATED by HTTP 404 probe; §4.7 NEW
   defines the Coordinator-direct execution gate; §4.8 NEW
   defines the Stephen-decision tree with full `gh api -X PUT`
   payload exemplars for Options A / B / C.** Vasquez's
   Coordinator-direct probe
   (`gh api -X GET /repos/long2know/mahjong-autotable/branches/main/protection`)
   returned **HTTP 404 "Branch not protected"** — confirming
   that `main` has **zero branch protection of any kind**.
   The W14/W15/W16 dry-run framing (which assumed an existing
   protection set that needed *modification* via `gh api -X
   PATCH`) is therefore invalidated: from-zero install requires
   `gh api -X PUT` with the full policy payload, and that
   payload is **not a single decision** — it spans 8 separate
   policy choices (required PR review, status checks list,
   strict-up-to-date, signed commits, admins, branch-up-to-
   date-before-merging, restrictions, CODEOWNERS). **§4.5
   DOWNGRADES from "PROMOTE Coordinator-direct to PRIMARY" to
   "Coordinator-direct execution remains BLOCKED on Stephen
   policy choice — Coordinator can only execute after the
   §4.8 Option A/B/C selection lands."** §4.7 NEW defines the
   Coordinator-direct execution gate (4 pre-flight checks
   including the HTTP 404 probe re-confirmation, payload
   schema-validate, Stephen-option-selection acknowledgement,
   and the `flock`-guarded apply window). §4.8 NEW carries
   three exemplars — **Option A (minimal: PR-only review +
   no checks)**, **Option B (standard: PR review + required
   status checks + branch-up-to-date)**, **Option C (strict:
   B + signed commits + enforce-admins + CODEOWNERS-required
   review)** — with the full `gh api -X PUT
   /repos/long2know/mahjong-autotable/branches/main/protection`
   payload for each. **W17 escalates branch-protection from
   a 2-axis decision (apply yes/no × Coordinator vs Stephen)
   to a 4-axis decision (yes/no × actor × policy intensity ×
   irreversibility-acknowledgement)** — and the §4.5 + §4.7 +
   §4.8 triad is the canonical structure W18 inherits.

5. **§6.7 NEW — LH13 §6.6 Coordinator-direct cron seed
   PROMOTED from optional fallback to PRIMARY next-step;
   3-shot Coordinator-direct EXECUTION lands this wave (the
   first Coordinator-direct execution of a deferred
   Stephen-action since the no-pauses directive).** Hicks's
   W17 §8 update confirms LH13 cron is alive (one
   schedule-event run fired between W16 and W17) **but the
   conclusion is `failure`** and the convergence criterion
   (3 consecutive green runs) remains at 0 of 3. Vasquez's
   `docs/frontend-pwa-audit.md §6.7` PROMOTES the §6.6
   Coordinator-direct cron seed from "optional fallback" to
   "PRIMARY next-step" — the deferral ledger has run from W11
   inclusive to W17 inclusive = **7 waves**, well past the §6.3
   6-wave Coordinator-direct trigger threshold. Scribe-ledger
   note (W17): **3 manual `gh workflow run pwa-audit.yml
   --repo long2know/mahjong-autotable --ref main --field
   reason="W17 §6.7 PRIMARY Coordinator-direct seed (N/3)"`
   invocations were executed by the Coordinator to seed the
   convergence window**. This is **categorically distinct from
   "Coordinator-direct intervention" (zero-streak still 12)**:
   intervention counts corrective git work (force-push,
   identity-rewrite, lane-discipline patch), whereas this is
   a **first-execution of a designed §6.7 PRIMARY workflow run**.
   The §4.5 vs §6.7 reversibility-first asymmetry is the
   canonical W17 framing.

6. **The §4.5 vs §6.7 reversibility-first asymmetry — DOWNGRADE
   high-blast-radius irreversible action, PROMOTE
   trivial-reversible action — is the headline W17 convention
   addition.** Both deliverables landed in the same wave, both
   crossed identical deferral thresholds (§4.5 9-wave Stephen
   deadlock, §6.7 7-wave cron deferral), and **both could in
   principle have been resolved via Coordinator-direct
   execution**. The W17 reversibility table draws the asymmetry:
   | Surface | Reversibility | Affects all contributors? | W17 disposition |
   |---|---|---|---|
   | Branch-protection (§4.5) | **Hard** — DELETE ≠ null restore (8-axis policy state) | **Yes** — top-of-repo banner | **DOWNGRADE**; Stephen-decision via §4.8 Options A/B/C |
   | Cron seeding (§6.6/§6.7) | **Trivial** — append-only workflow-run history | **No** — workflow only | **PROMOTE** to PRIMARY; Coordinator-direct EXECUTED |
   **W17 codifies that the deferral ledger is necessary but
   not sufficient — the disposition (DOWNGRADE vs PROMOTE)
   also requires a reversibility + blast-radius classification**.
   W18 inherits this asymmetry as a 4-quadrant decision matrix
   for any future deferred action.

7. **7th consecutive 0-violation lane-discipline wave —
   `checked=5 violations=0` — with NO `shared_files`
   amendment.** This is the **4th unamended wave** in the
   7-wave streak (W11+W14+W16+W17 unamended; W12+W13+W15
   amended). The 8-entry `shared_files` registry has now held
   unchanged for **3 consecutive waves** (W15 amendment
   landing → W16 → W17). The W17 bring-up surfaced zero new
   cross-lane files that the existing rule could not classify
   — the registry has reached an **operational steady-state**
   in the W15-codified amendment-discovery era. The 8 entries
   (`selectors_md_shared`, `agent_handoff_protocol_md_shared`,
   `shims_shared`, `pwa_audit_workflow_shared`,
   `bundle_health_workflow_shared`,
   `visual_regression_baselines_shared`,
   `lane_discipline_nightly_yml_shared`,
   `playwright_visual_regression_shared`) carry forward intact.

8. **Three-renderer-big intentional hold-line at 406,635 B
   sustained for the 7th consecutive wave (W11→W17).** Bundle
   ledger reads `406.64 KB → 406.64 KB (+0)` across all 7
   hold-line waves; cumulative W6 → W17: **−44.9 %** (738.65
   KB → 406.64 KB). The hold-line is now in its **7th wave of
   the bandwidth-rebalancing phase** (W15-codified pattern):
   Phase L implementation bandwidth absorbs the renderer lane
   while documented shrinkage candidates land piecemeal
   against `autotable-src-eager` (W15 −22,847 B; W16 −8,645 B;
   W17 −37,295 B). The 8-wave monotonic-decrease ledger remains
   paused by design; the renderer-vs-eager rebalancing has
   produced **3 consecutive `autotable-src-eager` shrinkage
   waves** while preserving `three-renderer-big` stability.

---

## 2. Wave-17 commits

| SHA       | Lane           | Author email                | Files | +Lines | −Lines |
|-----------|----------------|-----------------------------|-------|--------|--------|
| `68469a6` | Hicks          | `hicks@squad.mahjong`       | ~18   | ~2200  | ~120   |
| `11ddf18` | Apone          | `apone@squad.mahjong`       | ~17   | ~1850  | ~25    |
| `0619dd2` | Bishop         | `bishop@squad.mahjong`      | ~28   | ~4500  | ~30    |
| `fcf741d` | Vasquez        | `vasquez@squad.mahjong`     | ~30   | ~3200  | ~45    |
| (Scribe)  | Scribe         | `scribe@squad.mahjong`      | 4     | (this commit) | 0 |

**Bring-up totals (4 commits): ~93 files; ~+11,750 lines /
~−220 lines.** All 4 bring-up commits carry the
`Co-authored-by: Copilot <…>` trailer; the Scribe sweep commit
extends the same trailer convention.

**4th unamended wave since W11 first-0-violation wave.**
W11+W14+W16+W17 unamended; W12+W13+W15 amended. **W17 extends
the 7-wave 0-violation streak as the 3rd consecutive wave
with NO new `shared_files` entries** — the 8-entry registry
has held since W15 (`selectors_md_shared`,
`agent_handoff_protocol_md_shared`, `shims_shared`,
`pwa_audit_workflow_shared`, `bundle_health_workflow_shared`,
`visual_regression_baselines_shared`,
`lane_discipline_nightly_yml_shared`,
`playwright_visual_regression_shared`). The W15 §6.3
primary-classification rule is **load-tested through 3 waves
of bring-up cycles and held** — no cross-lane file surfaced
in W16 or W17 that the existing rule could not classify
under one of the 8 entries.

---

## 3. Bishop (Backend) `0619dd2` — 7-deliverable wave anchored by JWKS validator wire-up at `JwtIssuingService.IssueForTenantAsync`; intermediate gate inside Bishop's commit window lifted total to 3807/0/0 (+186 over W16)

Bishop ships **7 deliverables in one wave**, anchored by the
W15 §3.2 + W16 §3.1 validator wire-up at the issuing call
site (3-wave total from table → wire-up; the pattern resolves
exactly as the W15 forward-note predicted) + the per-tenant
hard-delete seam that lifts the W16 sentinel-row workaround
+ admin CRUD on `/api/admin/replays/retention` + commentary
`X-Admin-Reason` unification + `DateTimeOffset` widening
round 2 + tournament-query Prometheus alerts + SignalR
per-tenant retention.

### 3.1 JWKS validator wired into `JwtIssuingService.IssueForTenantAsync` + Prometheus `jwt_issue_blocked_total{reason}` counter

- **Call-site:** `JwtIssuingService.IssueForTenantAsync`
  invokes `PerTenantJwksRotationValidator.EnforceSigningAsync`
  on every per-tenant token issue.
- **Short-circuit verdicts:** `Stale` and `StoreMissing` both
  short-circuit before signing. The W16 verdict shape (6 kinds:
  `ToggleDisabled` / `NoPolicy` / `PolicyFresh` /
  `WithinOverlapWindow` / `Stale` / `StoreMissing`) is
  preserved; only `Stale` and `StoreMissing` cause a block at
  the issuing call site.
- **Prometheus counter:** `JwtIssueBlockedMetrics` exposes
  `jwt_issue_blocked_total{reason}` with **wire-stable labels**
  `stale_per_tenant_policy` and `per_tenant_store_missing`
  (label-value strings are fixed at this wave to prevent
  downstream dashboard breakage).
- **MetricsEndpoint preamble convention:** HELP + TYPE
  preambles emit unconditionally on every scrape so the
  schema is visible even at zero counts. **Convention reused
  from W14's `pwa.*` counter set** — preserves zero-count
  schema discoverability for fresh dashboards.
- **Audit emission:** every block stamps a
  `ReconnectAuditEntry` with kind
  `auth.jwt.issue.blocked.stale_per_tenant_policy` (or
  `auth.jwt.issue.blocked.per_tenant_store_missing` for the
  `StoreMissing` case). Audit `Detail` captures the
  tenant-id + verdict-kind tuple.
- **Tests:** `JwtIssuingServiceTests.cs` extended with the
  4-block matrix (issue-allowed PolicyFresh / issue-allowed
  WithinOverlapWindow / issue-blocked Stale / issue-blocked
  StoreMissing) + Prometheus counter increment verification +
  audit emission verification.

### 3.2 `DeleteAsync` on `IPerTenantJwksRotationStore` — lifts the W16 sentinel-row workaround

- **W16 backdrop:** the admin controller's `DELETE` shipped
  as an `UpsertAsync` with `IsActive = false` + `RotationEndUtc
  = UtcNow` (sentinel-row pattern) because the store interface
  lacked a `DeleteAsync` seam.
- **W17 lifts the sentinel:** `IPerTenantJwksRotationStore.DeleteAsync`
  added; both impls (`InMemoryPerTenantJwksRotationStore` +
  `EfPerTenantJwksRotationStore`) implement real hard-delete
  semantics.
- **Audit-kind backward compat:** the W16 audit kind
  `auth.jwks.per-tenant.deleted` is **preserved verbatim** so
  existing W16 tests still pass. A NEW constant
  `auth.jwks.per-tenant.hard-deleted` is introduced for future
  call sites that want to distinguish the two semantics.
- **Admin controller:** the controller's `Delete` now calls
  the real hard-delete path. The 401 → 403 → 503 → 204 auth
  ladder is preserved.
- **Migration impact:** zero — `DeleteAsync` operates on the
  existing `PerTenantJwksRotationPolicies` table; no schema
  change required.
- **Tests:** new `PerTenantRotationDeleteAsyncTests.cs` under
  `Phase_K_W17/Bishop/` covers the hard-delete path +
  sentinel-row vs hard-delete audit-kind distinction +
  401/403/503 ladder preservation.

### 3.3 Admin CRUD surface for `ReplayRetentionPolicy` at `/api/admin/replays/retention`

- **W16 backdrop:** W16 landed the `ReplayRetentionPolicies`
  table + the per-tenant retention store seam, but no
  operator UX existed for adjusting policies.
- **W17 ships `ReplayRetentionAdminController`:** GET / POST /
  PUT / DELETE at `/api/admin/replays/retention`.
- **Canonical auth ladder:** 401 Unauthorized (no token) →
  403 Forbidden (token without admin scope) → 503 Service
  Unavailable (store not registered) → 200 OK / 201 Created /
  204 No Content (success).
- **Mandatory `X-Admin-Reason` header on every write:** empty
  or whitespace value fails the request with 400 Bad Request
  **rather than silently engaging** — the "fail-closed on
  empty header" pattern that Bishop later unifies in §3.4 for
  Commentary.
- **Audit emission:** every successful write stamps a
  `ReconnectAuditEntry` with kind
  `replays.retention.{created|updated|deleted}` and `Detail`
  capturing `"{tenantId}|{reason}"` verbatim (the pipe-delimited
  shape lets downstream consumers split into per-field projections
  without parsing the reason text).
- **Tests:** new `ReplayRetentionAdminControllerTests.cs`
  under `Phase_K_W17/Bishop/` covers full CRUD + 401/403/503
  ladder + empty-reason 400 + audit emission verification.

### 3.4 Commentary `X-Admin-Reason` header unification — `ResolveAdminOverride` returns triple `(engaged, reason, badEmptyReason)`

- **W16 backdrop:** `CommentaryController` accepted the legacy
  `X-Cost-Budget-Override: 1` header for the budget-cap bypass,
  but the dashboard had no operator-supplied reason field —
  the override engaged silently with no audit-trail explaining
  *why*.
- **W17 unification:** the same `X-Admin-Reason` header
  convention used by `ReplayRetentionAdminController` (§3.3),
  `PerTenantJwksRotationAdminController` (W16), and the
  new `SignalRRetentionAdminController` (§3.7) is now wired
  into `CommentaryController` as well.
- **`ResolveAdminOverride` triple return:** the new shape
  `(engaged, reason, badEmptyReason)` makes the three
  possible states explicit:
  - `(false, null, false)` — header absent; no override.
  - `(true, "<text>", false)` — header present with non-empty
    reason; override engaged.
  - `(false, null, true)` — header present with empty /
    whitespace value; **request fails 400** rather than
    silently engaging.
- **Audit row:** every engaged override stamps
  `commentary.admin.override` (kind constant exported off
  `ReconnectAuditEntry` for downstream consumers).
- **Backward-compat:** the W14 `X-Cost-Budget-Override: 1`
  trigger header is preserved unchanged — only the reason
  header is added.
- **Tests:** `CommentaryControllerTests.cs` extended with the
  3-state triple-return matrix + 400-on-empty-reason +
  audit emission verification.

### 3.5 `DateTimeOffset` widening round 2 — `phase-k-w17-r2`

- **Round 1 (W16) coverage:** `JwtStagedRotationPolicy`
  carried `DateTime`-only fields; W16 introduced
  `[NotMapped] *Offset` projections that returned
  `DateTimeOffset` views without touching the underlying
  `DateTime` columns (zero schema impact).
- **Round 2 (W17) coverage:** the extension-based
  `DateTimeOffsetWideningR2` projections cover
  `PlayerAuthIdentity`, `PlayerAuthSession`,
  `ReconnectAuditEntry`, and `SignalRSequenceEntry`.
- **`CacheAgeOffset` helper:** new helper that clamps
  negative `TimeSpan` deltas to zero so a cache miss never
  reports a negative age (the W16 cache-coherency probe
  surfaced 3 transient negative-age readings in the
  `signalr-sequence` admission probe under clock-skew).
- **Admin controller free upgrade:** new `[NotMapped]
  CreatedAtOffset` / `UpdatedAtOffset` projections on
  `ReplayRetentionPolicy`, `PerTenantJwksRotationPolicy`,
  and `SignalRRetentionPolicy` give the W17 admin controllers
  offset-aware JSON projections for free — the controller
  layer does not need to know the projection exists.
- **Wave tag:** `phase-k-w17-r2` is the canonical tag for
  round-2 widening tests and any cross-referencing audit
  doc work. (W18 forward-note: round 3 will cover the
  remaining `DateTime`-only columns on
  `TournamentRoundRecord` + `LeaderboardSnapshot`.)

### 3.6 Tournament-query-duration Prometheus alerts + runbook

- **Alert file:** new
  `src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`
  carrying two rails:
  - `TournamentQueryDurationP99HighPage` — severity `page`,
    threshold p99 > 500 ms over a 5-minute window,
    `runbook_url` pointing at the new
    `docs/tournament-query-duration-runbook.md`.
  - `TournamentQueryDurationP95HighTicket` — severity
    `ticket`, threshold p95 > 250 ms over a 15-minute window.
- **Runbook:** new `docs/tournament-query-duration-runbook.md`
  covers the alert firing path, the 3 most likely root causes
  (cold-cache after deploy / DB connection-pool exhaustion /
  unindexed-query plan regression), and the canonical
  diagnostic queries.
- **Convention:** matches the existing
  `commentary-cost-budget-runbook.md` shape from W14 — every
  alert with severity `page` MUST carry a corresponding
  `*-runbook.md` doc.
- **Tests:** new
  `TournamentQueryDurationAlertsTests.cs` parses the YAML
  and asserts the 2 alert names + 2 severity values + 2
  threshold values + 1 runbook URL.

### 3.7 SignalR per-tenant retention — `SignalRRetentionPolicy` + store + admin controller + `TenantId` on `SignalRSequenceEntry`

- **`SignalRRetentionPolicy` model:** new entity carrying
  `TenantId` + `RetentionWindowDays` + `IsActive` +
  audit-stamp columns. Migrated under
  `Phase_K_W17_AdminCrudAndPerTenantRetention` (see §3.8).
- **`ISignalRRetentionPolicyStore` seam:** GET (single +
  list) + Upsert + Delete shape, mirroring the W16 per-tenant
  JWKS rotation store.
- **`SweepWithPerTenantPolicyAsync` on the sweep service:**
  replaces the W14 single-policy `SweepAsync` for tenants
  that have a `SignalRRetentionPolicy` row; the W14
  single-policy default remains the fallback for tenants
  without a row.
- **`TenantId` column added to `SignalRSequenceEntry`:** new
  column carries the tenant scope for per-tenant retention.
  Backfill strategy: existing rows take `TenantId = null` and
  are retained under the single-policy default (no rows are
  silently swept).
- **`SignalRRetentionAdminController` at
  `/api/admin/signalr/retention`:** full GET / POST / PUT /
  DELETE CRUD + canonical 401/403/503/200/201/204 auth
  ladder + mandatory `X-Admin-Reason` header + per-write
  audit emission (`signalr.retention.{created|updated|deleted}`).
- **Tests:** new `SignalRRetentionAdminControllerTests.cs`
  under `Phase_K_W17/Bishop/` covers full CRUD + auth ladder
  + empty-reason 400 + audit emission +
  `SweepWithPerTenantPolicyAsync` verification.

### 3.8 Single cross-provider migration: `Phase_K_W17_AdminCrudAndPerTenantRetention`

- **Three provider snapshots updated** (Sqlite + Postgres +
  SqlServer) — single migration captures:
  - new `SignalRRetentionPolicy` table (W17 §3.7);
  - new `TenantId` column on `SignalRSequenceEntry` (W17 §3.7);
  - the W16 schema drift items: `OverlapWindowDays` on
    `PerTenantJwksRotationPolicy`, `ReplayRetentionPolicies`
    table seam, and `TenantId` on `Replays` (the W16
    migration captured 2 of the 3; the third drifted under
    the W16 sentinel-row pattern and is now committed).
- **Migration shape:** add-only — no column drops, no data
  loss. Re-runnable against any prior W16 close database
  without manual SQL.
- **Tests:** existing migration smoke tests pick up the new
  migration automatically; no Bishop-authored test changes
  required for the migration itself (the per-feature tests
  in §§3.1-3.7 exercise the schema additions through the
  service layer).

---

## 4. Hicks (Frontend) `68469a6` — 4 deliverables; Phase L W3 scene + picking + canonical atlas + §3.2 surgery (−37 KB on `autotable-src-eager`) + 7th-wave `three-renderer-big` hold-line + LH13 §8 HOLD soft-flip

Hicks ships **4 deliverables in one wave** with no defers
and no follow-up waves required. The headline is the
`autotable-src-eager` cold-path shrinkage — **−37,295 B at
W17 alone, the largest single-wave §3.2 surgery cut** since
the audit landed — driven by lazy-mounting three lobby
modules.

### 4.1 Phase L W3 — `scene.ts` runtime + `picking.ts` ray-cast + canonical atlas asset + 3-mode `hello.ts` harness

#### `scene.ts` — `createTileScene()` orchestrator

- **Factory:** `createTileScene()` wires `camera.ts` (W16) +
  `tile-mesh.ts` (W16) + `tile-atlas.ts` (W16 STUB, W17
  canonical) into a single orchestrator.
- **DPR-aware framebuffer:** the framebuffer dimensions track
  `window.devicePixelRatio` and resize on visibility change /
  orientation change. Resize is rAF-coalesced.
- **rAF-coalesced redraw:** `redraw()` is idempotent within a
  frame budget — multiple `redraw()` calls within one rAF
  window collapse to a single GL clear + instanced draw.
- **No imperative draw loop:** the scene only repaints when
  the camera state or the tile-set actually changes. **This
  is a deliberate departure from the three.js render-loop
  shape** — Phase L is targeting battery-friendly mobile
  rendering, so the renderer is event-driven rather than
  frame-driven.
- **`dispose()` lifecycle:** releases the GL context,
  unhooks the resize / visibility listeners, and zeroes the
  internal buffers. Idempotent (multiple `dispose()` calls
  are safe).

#### `picking.ts` — `pickTile(scene, x, y)`

- **Pure CPU math — no `gl.readPixels()` readback path.** The
  W15 forward-note framing was that picking might require a
  GPU readback round-trip; W17 demonstrates that ray-AABB
  slab intersection in JS is fast enough for the 102-tile
  scene budget.
- **NDC + inverse-view-projection:** input `(x, y)` are
  client-space pixels; converted to NDC `(-1, 1)` then
  multiplied through `inverse(view * projection)` to construct
  a world-space ray.
- **Ray-AABB slab intersection:** each tile instance has a
  canonical AABB (the W16 `tile-mesh.ts` half-extents);
  intersection returns `(tMin, tMax)` per tile.
- **Front-most hit:** the returned `tileIndex` is the tile
  whose intersection `tMin` is smallest among all positive
  hits; returns `null` if no intersection.
- **Cost characteristic:** picking cost is independent of
  WebGL2 driver `readPixels()` latency — the cost stays
  inside the V8 JIT-compiled inner loop.

#### Canonical atlas asset

- **`scripts/generate-tile-atlas-webgl2.js`:** zero-dep Node
  PNG generator. Hand-rolled IDAT zlib stream + IEEE
  0xEDB88320 CRC32. PNG colour-type 6 (truecolour + alpha).
  192×2176 layout = 3 cols × 34 rows = 102 cells, 64 px each.
  Cell labels match the `tile-mesh.ts` UV-index convention.
  **Deterministic output:** no timestamps in chunks, so
  regen is idempotent and produces the same bytes.
- **`src/frontend/autotable/img/tiles-atlas-webgl2.auto.png`:**
  10,058 bytes. The `.auto.png` suffix flags it as
  generator-managed (so contributors don't hand-edit the PNG).
- **`vite.config.ts` `copyStaticAssets()` block:** new entry
  alongside the existing dice-copy / PWA-icons-copy plugins.
  Lifts the PNG out of `src/frontend/autotable/img/` and into
  the published `dist/` tree where the runtime can fetch it.
- **`tile-atlas.ts` header rewrite (lines 1-37):** drops the
  W16 STUB framing and documents the W17 canonical asset
  path (`../img/tiles-atlas-webgl2.auto.png` via vite's
  static-copy alias). The fallback grid texture path is
  preserved as a runtime safety-net.

#### 3-mode `hello.ts` harness

- **W15 1-mode:** `?renderer=webgl2-hello` (canvas clear +
  GL_VERSION assertion).
- **W16 2-mode:** + `?renderer=webgl2-tile-mesh` (mesh-graph
  instanced draw).
- **W17 3-mode:** + `?renderer=webgl2-scene` (full scene
  mount + click handler that calls `pickTile()` and logs the
  hit).
- **Backward compat:** W15 + W16 modes are byte-for-byte
  preserved. Vasquez's W16 smoke harness in
  `tests/e2e/renderer-webgl2-smoke.spec.ts` is unchanged and
  still passes.

#### Bundle budget

- `renderer-webgl2` W15 → W16 → W17: **6,237 B → 19,017 B →
  24,743 B**.
- **W17 cap:** 40 KB. **Headroom:** 24.7 / 40 = 15.3 KB.
- **% of 220 KB Phase L envelope:** 24.7 / 220 = **11.2 %**.
- W18 forward-note: Hicks's design doc targets the animation
  graph (≤30 KB cumulative) under the same envelope.

### 4.2 §3.2 audit surgery — lazy-mount `leaderboard`, `settings-drawer`, `profile-page` from lobby

- **W17 lazy chunks (3 new entries in `dist-size.json`):**
  - `leaderboard.{hash}.js` — 11,349 B
  - `settings-drawer.{hash}.js` — 17,770 B
  - `profile-page.{hash}.js` — 9,464 B
- **`autotable-src-eager.js` ledger:** W15 222,847 → W16
  214,202 → **W17 176,907 B (−37,295 B at W17 alone;
  −45,940 B over 2 waves).**
- **§3.2 target:** ≥14 KB per wave. W17 result: **−37,295 B
  = 2.65× target** — the largest single-wave §3.2 cut since
  the audit landed.
- **Lazy-mount helpers:** new `scheduleLeaderboardLazyMount()`,
  `scheduleSettingsDrawerLazyMount()`,
  `scheduleProfilePageLazyMount()` helpers each defer the
  dynamic `import()` until after the first paint frame.
  Pattern matches the W14 `scheduleAchievementsLazyMount()`
  precedent.
- **`mahjong:open-profile-page` `CustomEvent`:** cross-module
  navigation (e.g., from the lobby header avatar) cannot
  block on the lazy chunk resolving. Solution: dispatch the
  event eagerly; the page itself drains the queue on mount.
  **Pattern reusable in W18** for any future lobby-mounted
  module that needs cross-module entry points.
- **Bundle ledger entry count:** 24 carry-over + 3 new lazy
  = **27 tracked chunks** in W17 `dist-size.json`.

### 4.3 `three-renderer-big` 7th-wave hold-line at 406,635 B

- **W11 → W17:** 7 consecutive waves at exactly 406,635 B
  (`406.64 KB`).
- **Cumulative W6 → W17:** **−44.9 %** (738.65 KB → 406.64
  KB). The W6 → W10 monotonic-decrease ledger is paused by
  design — the bandwidth-rebalancing phase shifts shrinkage
  to `autotable-src-eager` while the renderer chunk stabilises.
- **W17 audit-candidate status:** none. The §3.1 + §3.5
  shrinkage candidates documented in W15 / W16 are not yet
  ripe for landing — Phase L implementation bandwidth
  continues to absorb the renderer lane.

### 4.4 LH13 §8 HOLD soft-flip — first `schedule`-event run confirms cron is alive but conclusion=failure

- **W16 backdrop:** the `docs/lh13-soft-pin-rationale.md`
  Option A soft-flip carried the W11 thresholds forward
  tagged `provisional-until-calibrated` rather than amending
  the `pwa-audit.yml` workflow. The §6.3 6-wave Coordinator-
  direct trigger threshold was cleared by **converting LH13
  from "pending-calibration" to "deliberately-provisional-
  pinned with documented rationale"**.
- **W17 §8 update (new):** between W16 and W17, **one
  `schedule`-event run fired** on `pwa-audit.yml` — confirming
  the cron is **operationally alive**. However, the
  conclusion was `failure` (the audit thresholds tripped on
  the first run). Convergence criterion (3 consecutive green
  runs) is therefore still at **0 of 3**.
- **HOLD soft-flip:** §8 documents that the W16 Option A
  soft-flip remains in effect; no workflow file edit. The
  W17 PROMOTE is in Vasquez's §6.7 (see §6.4 below) rather
  than in this Hicks deliverable.
- **Forward-note:** W18 Hicks will revisit §8 with the post-
  Coordinator-direct seed run history (Vasquez §6.7 fired
  3 manual `gh workflow run` invocations this wave — see §6.5
  of this summary).

---

## 5. Apone (DevOps) `11ddf18` — D1 Kyverno enforce 7-day clean / D2 Mobile CI Android signing groundwork / D3 us-east-1 W17 plan (PARTIAL-GREEN/HOLD due to stale renderer-row AMBER measurement) / D4 HPA W17 7-day retro / D5 SLSA-3 +50 SHA pins across 9 workflows / D6 CHANGELOG 0.26.0 + mobile/package.json 0.25.0 → 0.26.0

### D1 — Kyverno enforce 7-day post-flip observability

- **Window:** the W16 cutover-day flip activated the prod-only
  `enforce-prod-default` ClusterPolicy after a 5-day grace
  window. The W16 hand-off tasked Apone W17 with a 14-day
  blast-radius watch; the W17 retro lands at the 7-day
  mid-window mark.
- **New doc:** `docs/kyverno-enforce-w17-observability.md`
  (~9.9 KB) — four-panel Hudson verdict table:
  - 0 denies on `enforce-prod-default` (prod-only
    enforce policy);
  - 0 denies on the W3 audit-mode `kyverno-cosign-verify.yaml`
    (cluster-wide cosign-verify policy STAYS Audit-default by
    design — preserves the W15 §1 "brand-new namespace fails
    SAFE" semantic);
  - 0 denies on the W4 `cosign-verify-prod` admission
    webhook;
  - **+3% headroom** on `pod-security-violations-prod`;
  - **+1 ms p99** on `admission-webhook-latency-prod` (4 ms
    W16 → 5 ms W17 — within noise).
- **Rollback decision:** **HOLD; no revert PR opened.** The
  14-day window stays open for W18+ continuous-observability
  watch but the cutover is operationally green.
- **`docs/kyverno-enforce-rollout.md` §11 appended:**
  cross-references the new observability doc + narrows the
  W17 watch slot to CLOSED-GREEN.

### D2 — Mobile CI Android signing groundwork

- **Why:** the W16 D5 hand-off was "Mobile CI — operator
  secret provisioning"; the `docs/mobile-ci-bootstrap.md §4`
  operator runbook listed the four `ANDROID_*` secret names
  but `.github/workflows/mobile-build.yml` had no consumption
  shape — Stephen could upload the secrets and they would do
  nothing.
- **What:** extended `mobile-build.yml`:
  - new job-level `env:` block exposing four `ANDROID_*`
    secrets (`KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`,
    `KEY_ALIAS`, `KEY_PASSWORD`);
  - new "Decode Android keystore (when secret present)" step
    that writes the base64 payload to
    `${RUNNER_TEMP}/mahjong-autotable.keystore` and emits a
    `keystore-present` boolean step output;
  - split the W2 `Gradle assembleRelease + bundleRelease`
    step into mutually-exclusive **SIGNED** (gated on
    `keystore-present == 'true'`) and **UNSIGNED** (gated on
    `!=`) branches.
- **Backward compat:** the UNSIGNED branch preserves the W16
  command shape exactly — workflow behaviour when secrets
  are absent is **byte-identical to W16**.
- **New doc:** `docs/mobile-android-signing.md` (~15 KB) —
  Stephen's operator runbook for uploading the four
  `ANDROID_*` secrets and verifying the SIGNED branch fires
  on the next release.
- **W18 forward-note:** once Stephen uploads the secrets,
  the first signed `.aab` artifact will land in the
  `mobile-build.yml` artifacts; Apone W18 will document the
  artifact verification path.

### D3 — us-east-1 W17 plan (PARTIAL-GREEN/HOLD)

- **Plan-only deliverable (no apply):** the W17 us-east-1
  apply is deferred under the **5-row gate** Apone defined
  at W16; the W17 deliverable is the plan capture +
  gate-row verification.
- **5-row gate result (W17 read):**
  - Row 1 (terraform plan diff = zero source-side drift):
    GREEN. No drift across W11 / W14 / W15 / W16 baselines.
  - **Row 2 (renderer payload < 200 KB ceiling): AMBER.**
    Apone's measurement at PRE-Hicks-rebase time captured
    `autotable-src-eager` at **209 KB** (Hicks's §3.2
    surgery had not yet rebased into Apone's working tree).
    Apone flagged this as Row 2 AMBER and held the apply.
  - **Scribe-ledger correction (W17 close):** the **actual
    post-Hicks-rebase W17 figure is 176,907 B = 177 KB**,
    well under the 200 KB ceiling. The AMBER reading was a
    stale measurement; the **Apone retrospective at W18 will
    green-light W18 us-east-1 apply** based on the corrected
    figure. Documented here so future Scribe sweeps can
    cross-reference the discrepancy.
  - Row 3 (Kyverno observability green): GREEN (D1 above).
  - Row 4 (HPA off-peak headroom): AMBER (D4 below — cron-
    override designed but not yet applied; lands W18).
  - Row 5 (SLSA-3 pin freshness): GREEN (D5 below — +50 pins).
- **Disposition:** PARTIAL-GREEN/HOLD; W18 us-east-1 apply
  expected after Row 2 re-measurement + Row 4 cron-override
  PR.

### D4 — HPA W17 7-day retrospective

- **Window:** the W16 HPA tuning patch landed `minReplicas =
  3` + `maxReplicas = 12` + `targetCPU = 65 %` across the
  three prod deployments. The W17 retro covers the first
  7 days of operation.
- **Off-peak over-provisioning trigger:** the W16 patch's
  `minReplicas = 3` tripped 7 out of 7 nights — between
  02:00 UTC and 06:00 UTC, the actual load supports
  `minReplicas = 1` with no SLO impact. The over-provision
  represents **~25 % infrastructure spend during off-peak
  hours**.
- **Cron-override design (W17 deliverable):**
  `docs/hpa-cron-override-w18.md` (~6 KB) — design doc for
  a `KubernetesCronJob`-based override that sets `minReplicas
  = 1` between 02:00 UTC and 06:00 UTC and restores
  `minReplicas = 3` outside that window. **Applies in W18
  as a single PR** (the design doc lands at W17; the
  workflow + CronJob YAML lands at W18).
- **Why W18 not W17:** the cron-override has a non-trivial
  test matrix (CronJob RBAC + HPA min-replicas mutation
  permission + smoke test that the off-peak window actually
  fires); Apone preferred to land the design doc at W17 and
  the apply at W18 to give the test matrix a full wave of
  preparation.

### D5 — SLSA-3 SHA pin expansion: W16's 6 pins → +50 pins across 9 workflows

- **Backdrop:** W16 D6 pinned the 6 first-party `docker-
  build.yml` actions to commit SHAs. The W17 expansion lifts
  the pinning convention to **9 security-critical workflows**.
- **Workflows covered (9):**
  - `.github/workflows/docker-build.yml` (W16 baseline +
    additional pins);
  - `.github/workflows/mobile-build.yml` (D2 above — new env
    block + new decode step both pinned);
  - `.github/workflows/pwa-audit.yml` (LH13 — pinning here
    avoids the §6.7 cron-seed surface tripping on an
    upstream action drift);
  - `.github/workflows/bundle-health.yml`;
  - `.github/workflows/lane-discipline-nightly.yml`;
  - `.github/workflows/cosign-verify.yml`;
  - `.github/workflows/kyverno-policy-check.yml`;
  - `.github/workflows/dotnet-ci.yml`;
  - `.github/workflows/frontend-ci.yml`.
- **Pin count:** +50 new SHA pins. Total project-wide pin
  count now **56** (6 W16 + 50 W17).
- **`slsa-github-generator@v2.0.0` stays tag-pinned (NOT
  SHA-pinned):** this is the **single intentional exception**
  — the SLSA generator's auditable-via-tag-only semantics
  require the tag rather than the SHA. Documented in
  `docs/slsa-3-pinning-rationale.md §3.2 (NEW W17)`.
- **Renovate convention:** Renovate rules updated to
  recognise the W17-pinned actions and propose SHA updates
  via PR rather than auto-bumping tags.

### D6 — CHANGELOG 0.26.0 + `mobile/package.json` 0.25.0 → 0.26.0

- **`CHANGELOG.md`:** new `[0.26.0]` entry documenting the
  W17 wave (Kyverno enforce 7-day window + mobile signing
  groundwork + us-east-1 plan + HPA retro + SLSA pin
  expansion). Format follows the W16 [0.25.0] entry shape.
- **`mobile/package.json`:** version bump 0.25.0 → 0.26.0
  matching the CHANGELOG.
- **`src/frontend/autotable-src/package.json`:** version
  bump consistent with the CHANGELOG (matches the W16
  convention of bumping mobile + autotable-src in lockstep).

---

## 6. Vasquez (QA) `fcf741d` — 19 forward-stage tests under `Phase_K_W17/Vasquez/` + 4 hard-asserts (SelfLane, SurfaceSmokeFacts, BranchProtectionW17Recalibration, PwaAuditWorkflowGateW17) + KW16 → KW17 regression rename + DbSerial 26-29 inventory + §4.5 RECALIBRATION + §4.7 NEW + §4.8 NEW + §6.7 NEW + Coordinator-direct LH13 cron seed EXECUTED (3-shot, see §6.5)

### 6.1 Gate trajectory and Vasquez contribution

| Metric | W16 close | W17 (post-Bishop, pre-Vasquez) | W17 (post-Vasquez = close) |
|---|---|---|---|
| Total | 3622 | 3807 | **3930** |
| Passed | 3622 | 3807 | **3930** |
| Failed | 0 | 0 | **0** |
| Skipped | 0 | 0 | **0** |

**Bishop contribution:** +186 (3621 → 3807; gate read across
Bishop's commit window — Hicks's bring-up landed before Bishop
in the wave-order but the Bishop wave-internal gate is the
reference for the +186 figure).

**Vasquez contribution:** **+123 tests** (broken down):
- 1 self-lane hard-assert
- 17 Bishop/Hicks/Apone forward-stage soft-pins (across the
  19 forward-stage files; 2 of the 19 are infra-cross
  coverage rather than pure soft-pins)
- 1 W17 surface-smoke hard-assert
- 1 PWA-audit §6.7 hard-assert
- 1 branch-protection §4.5 / §4.7 / §4.8 hard-assert
- 1 cross-wave DbSerial candidate soft-pin
- 1 W17 rename pin in the regression file
- 1 W16 historical pin in the regression file

### 6.2 DbSerial 26-29 candidate inventory — `docs/test-architecture.md §3.4a + §3.4b`

`docs/test-architecture.md` extended with two new
sub-sections:

- **§3.4a — W16 re-validation + 26th candidate identification:**
  records that Bishop's W16 commit added
  `Phase_K_W16/Bishop/PerTenantRotationAdminControllerTests.cs`
  (EF-touching admin CRUD facts) without applying
  `[Collection("DbSerial")]`. This is the **26th** open
  candidate — first Bishop-authored EF test added post-W15
  completion.
- **§3.4b — W17 re-validation + 27th-29th candidate
  identification:** records three more Bishop-authored W17
  candidates:
  - `Phase_K_W17/Bishop/PerTenantRotationDeleteAsyncTests.cs`,
  - `Phase_K_W17/Bishop/ReplayRetentionAdminControllerTests.cs`,
  - `Phase_K_W17/Bishop/SignalRRetentionAdminControllerTests.cs`.

**Inventory total (W17 close): 29.**
- 25 closed (all W12-migrated).
- 4 open (all Bishop-lane: 1 from W16, 3 from W17).

**Lane-rule observation:** all 4 open candidates are
blocked by the `wave_subdir_overrides` lane rule from
`tests/ci/lane-map.json` — **Vasquez-authored
`[Collection("DbSerial")]` attribute application would trip
the cross-lane bundling gate**. Bishop must author the
attribute application in a future Bishop-lane wave (W18 or
later).

### 6.3 19 forward-stage test files under `Phase_K_W17/Vasquez/`

The 19 files break down by surface:

- **8 Bishop-surface soft-pins:**
  `JwtIssuingServiceValidatorWireupTests.cs`,
  `JwtIssueBlockedMetricsTests.cs`,
  `PerTenantJwksDeleteAsyncSeamTests.cs`,
  `ReplayRetentionAdminCrudTests.cs`,
  `CommentaryAdminReasonTripleReturnTests.cs`,
  `DateTimeOffsetWideningR2Tests.cs`,
  `TournamentQueryDurationAlertsParseTests.cs`,
  `SignalRRetentionPerTenantSweepTests.cs`.
- **1 cross-wave DbSerial soft-pin:**
  `DbSerialOpenCandidateInventoryW17Tests.cs` — asserts the
  4-file open inventory by name + the 25-file closed inventory
  by name; fails if the open set grows or shrinks without an
  inventory amendment.
- **5 Hicks-surface soft-pins:**
  `RendererWebgl2SceneMountTests.cs`,
  `RendererWebgl2PickingRayAABBTests.cs`,
  `RendererWebgl2HelloHarness3ModeTests.cs`,
  `AutotableSrcEagerShrinkageW17Tests.cs` (asserts the
  176,907 B figure + the 3 new lazy chunks by name),
  `LazyMountHelpersDispatchTests.cs`.
- **1 Apone infra cross-surface (single file covering
  Kyverno + Android + EKS + HPA + SLSA):**
  `AponeInfraW17CrossSurfaceTests.cs` — asserts the
  presence + shape of the 6 D-deliverables (D1 doc + D2
  workflow env block + D3 plan capture + D4 design doc + D5
  +50 pins + D6 CHANGELOG bump).
- **4 Vasquez self-lane hard-asserts:**
  - `SelfLaneW17Tests.cs` — asserts Vasquez self-lane
    wave-name string is `Phase K Wave 17` (no underscore
    drift; no historical-only suffix).
  - `SurfaceSmokeFactsW17Tests.cs` — asserts the surface
    catalog adds the 3 W17 lazy chunks +
    `?renderer=webgl2-scene` mode + the new admin endpoints
    (`/api/admin/replays/retention`,
    `/api/admin/signalr/retention`).
  - `BranchProtectionW17RecalibrationTests.cs` — asserts
    the §4.5 RECALIBRATION wording is present in
    `docs/agent-handoff-protocol.md`; asserts §4.7 NEW
    presence; asserts §4.8 NEW presence with Options A / B /
    C named verbatim.
  - `PwaAuditWorkflowGateW17Tests.cs` — asserts §6.7 NEW
    presence in `docs/frontend-pwa-audit.md` with the
    PROMOTE wording verbatim.

### 6.4 §4.5 RECALIBRATION + §4.7 NEW + §4.8 NEW — branch-protection downgrade

**§4.5 W17 RECALIBRATION** (lines ~953-1014 of
`docs/agent-handoff-protocol.md`):

- **Coordinator-direct probe result:** `gh api -X GET
  /repos/long2know/mahjong-autotable/branches/main/protection`
  returned **HTTP 404 "Branch not protected"**.
- **Consequence:** `main` has **zero branch protection of
  any kind**. The W14/W15/W16 dry-run framing (which assumed
  an existing protection set that needed *modification* via
  `gh api -X PATCH`) is **invalidated**. From-zero install
  requires `gh api -X PUT` with a full policy payload.
- **Downgrade wording:** §4.5 changes from W16's "PROMOTE
  Coordinator-direct to PRIMARY recommendation" to W17's
  "Coordinator-direct execution remains BLOCKED on Stephen
  policy choice — Coordinator can only execute after the
  §4.8 Option A/B/C selection lands."

**§4.7 NEW** (lines ~1014-1101) — Coordinator-direct
execution gate with **4 pre-flight checks**:
1. **HTTP 404 probe re-confirmation:** Coordinator MUST
   re-probe immediately before any apply to verify no
   third party has applied a policy in the interim.
2. **Payload schema-validate:** Coordinator MUST validate
   the chosen Option payload against the `gh api -X PUT`
   schema before apply.
3. **Stephen-option-selection acknowledgement:** Coordinator
   MUST have an explicit Stephen choice of A / B / C
   captured in the decisions ledger before apply.
4. **`flock`-guarded apply window:** the apply MUST execute
   inside the same `flock -w 120 9>.work/squad-git-lock`
   critical section used for git commits.

**§4.8 NEW** (lines ~1101-1314) — three Stephen-decision
exemplars with the full `gh api -X PUT
/repos/long2know/mahjong-autotable/branches/main/protection`
payload for each:

- **Option A (minimal):** PR-only review (1 required
  reviewer); no required status checks; no signed-commits;
  no enforce-admins; no restrictions; no CODEOWNERS.
- **Option B (standard):** Option A + required status
  checks (the 4 gating workflows: `dotnet-ci`,
  `frontend-ci`, `lane-discipline-nightly`, `bundle-health`)
  + branch-up-to-date-before-merging.
- **Option C (strict):** Option B + signed commits required
  + enforce-admins + CODEOWNERS-required review.

**Reversibility framing:** §4.5 explicitly documents the
"DELETE ≠ null restore" asymmetry — the **8-axis policy
state cannot be reconstructed from a deleted protection set**,
so the apply is **operationally irreversible** without
re-applying the same payload. This is the canonical
hard-blast-radius example carried into §4.8's Stephen
decision tree.

### 6.5 Coordinator-direct LH13 cron seed EXECUTED (3-shot) — first Coordinator-direct EXECUTION of a deferred Stephen-action since the no-pauses directive

**Vasquez §6.7 NEW PROMOTES the §6.6 Coordinator-direct cron
seed from optional fallback to PRIMARY next-step.** The
deferral ledger has run from W11 inclusive to W17 inclusive
= **7 waves**, well past the §6.3 6-wave Coordinator-direct
trigger threshold.

**3 manual `gh workflow run pwa-audit.yml --repo
long2know/mahjong-autotable --ref main --field reason="W17
§6.7 PRIMARY Coordinator-direct seed (N/3)"` invocations
were executed by the Coordinator** to seed the convergence
window (the W11 §6 convergence criterion is 3 consecutive
green runs).

**Categorical distinction from "Coordinator-direct
intervention" (zero-streak still 12):**

| Concept | Counted as intervention? | What it counts |
|---|---|---|
| Coordinator-direct **intervention** | YES (zero-streak metric) | Corrective git work — force-push, identity-rewrite, lane-discipline patch, retroactive amend |
| Coordinator-direct **execution** (W17 §6.7) | NO | First-execution of a designed §6.7 PRIMARY workflow run |

**The 12-wave zero-intervention streak is preserved.** The
3-shot cron seed is a NEW class — recorded in the Scribe
ledger only (not in the agent-inbox memos) so the
zero-intervention metric remains comparable across W6-W17
without redefinition.

**Why this is documented in §6.5 of the Scribe summary
rather than in an agent-inbox memo:** the cron seed is an
operational action by the Coordinator, not a deliverable by
any single Squad agent. Scribe ledger is the canonical
single-author entry for cross-agent operational actions
(precedent: W12 lane-discipline rule precedence note;
W13 mutex-fully-adopted convention).

### 6.6 KW16 → KW17 regression rename

- **`tests/Mahjong.Autotable.Api.Tests/Regression/Wave1ThroughKW17RegressionTests.cs`:**
  3,048 lines (renamed from
  `Wave1ThroughKW16RegressionTests.cs`).
- **15 in-file references rewritten** — class name + 14
  `[Fact]`/`[Theory]` attribute strings that embedded the
  wave label.
- **W16 pin rewritten to `_Historical`:** the W16
  hard-assert pin (`Phase_K_Wave_16` rename pin) is
  preserved as `Phase_K_Wave_16_Historical` so prior-wave
  regression coverage carries forward.
- **W17 rename pin added:** new `Phase_K_Wave_17` rename
  pin captures the W16 → W17 rename canonically (W18 will
  rewrite this to `_Historical` and add `Phase_K_Wave_18`).
- **Forward-compat broadenings in W11-W16 self-lane wave-name
  assertions:** the W11 → W16 self-lane wave-name regexes
  broaden from `^Phase K Wave \d+$` to `^Phase K Wave
  (\d+|17[A-Z]?)$` to admit W17 sub-wave letter suffixes
  (none used in W17, but the broadening is forward-defensive).

### 6.7 §6.7 NEW in `docs/frontend-pwa-audit.md` — PROMOTE Coordinator-direct cron seed to PRIMARY

`docs/frontend-pwa-audit.md §6.7` (lines ~867-996) PROMOTES
the §6.6 Coordinator-direct cron seed from "optional
fallback" to "PRIMARY next-step":

- **Trigger:** §6.3 6-wave Coordinator-direct deferral
  threshold is crossed at W17 (W11 → W17 inclusive = 7 waves).
- **Action:** Coordinator runs `gh workflow run pwa-audit.yml
  --repo long2know/mahjong-autotable --ref main --field
  reason="W17 §6.7 PRIMARY Coordinator-direct seed (N/3)"`
  three times to seed the convergence window.
- **Convergence criterion (unchanged from W11):** 3
  consecutive green runs flips LH13 from
  `provisional-until-calibrated` to `calibrated-green`.
- **Reversibility framing:** §6.7 documents that the cron
  seed is **trivially reversible** — the workflow-run
  history is append-only; a failed run simply joins the
  history without mutating any policy. Contrast with §4.5's
  hard-reversibility branch-protection apply.
- **PRIMARY-vs-fallback wording:** the W16 §6.6 wording
  ("optional fallback if the soft-flip rationale itself
  later requires revisit") is replaced with W17 §6.7's
  ("PRIMARY next-step at the §6.3 deferral threshold
  crossing"). The fallback escalation (§6.4 yellow-flag,
  §6.5 Stephen-direct) remains documented but is now
  superseded by the §6.7 Coordinator-direct PRIMARY for
  the cron-seed surface specifically.

---

## 7. Cross-cutting patterns codified at W17 (18 entries)

1. **Reversibility-first asymmetry table** — DOWNGRADE
   high-blast-radius irreversible actions; PROMOTE
   trivial-reversible actions. The §4.5 vs §6.7 contrast is
   the canonical exemplar; W18 inherits the 4-quadrant
   decision matrix (reversibility × blast-radius).
2. **Coordinator-direct intervention vs execution
   distinction** — the 12-wave zero-intervention streak
   metric remains comparable because §6.5 EXECUTION is a
   new class, not a reclassification of intervention.
3. **HTTP-status probe before policy apply** — the §4.7
   pre-flight check #1 requires a re-probe immediately
   before apply to verify no third party has changed state
   in the interim. Pattern applicable to any future
   gh-api-driven policy change.
4. **`flock`-guarded apply window for non-git operations**
   — §4.7 pre-flight check #4 extends the `flock -w 120
   9>.work/squad-git-lock` critical section convention from
   git commits to gh-api applies. Single mutex prevents
   any two operational actions from racing.
5. **Stephen-decision-tree exemplars with full payloads** —
   §4.8 Options A / B / C ship complete `gh api -X PUT`
   payload exemplars rather than narrative-only options.
   Pattern reusable for any multi-axis policy decision.
6. **Fail-closed on empty header** — Bishop's §3.3 + §3.4
   + §3.7 unify on `X-Admin-Reason` empty / whitespace →
   400 Bad Request. **Convention now applies to ALL admin
   write endpoints** (PerTenantJwksRotation +
   ReplayRetention + Commentary + SignalRRetention).
7. **HELP + TYPE preamble on every scrape (zero-count
   schema visibility)** — Bishop §3.1's
   `JwtIssueBlockedMetrics` preamble emits unconditionally.
   Pattern carried from W14 `pwa.*` counter set.
8. **Audit-kind backward compat via constant aliasing** —
   Bishop §3.2 preserves W16's `auth.jwks.per-tenant.deleted`
   verbatim and introduces `auth.jwks.per-tenant.hard-deleted`
   as a NEW constant. Pattern for any future
   semantic-distinction kind split.
9. **Deterministic generator-managed asset suffix
   (`.auto.png`)** — Hicks §4.1's
   `tiles-atlas-webgl2.auto.png` uses the suffix to flag
   generator-managed status. Convention applicable to any
   future generator-produced binary asset.
10. **`CustomEvent`-queued cross-module navigation for lazy
    chunks** — Hicks §4.2's `mahjong:open-profile-page`
    pattern. Cross-module navigation cannot block on lazy
    chunk resolution; dispatch eagerly, drain on mount.
11. **`scheduleXLazyMount()` helper naming convention** —
    Hicks §4.2 generalises the W14
    `scheduleAchievementsLazyMount()` precedent. Helper
    name is `schedule<ChunkName>LazyMount()`; helper
    encapsulates the rAF-deferred dynamic import.
12. **3-mode `?renderer=` URL-guard parameter** — Hicks §4.1
    extends the W15 1-mode + W16 2-mode pattern. Per-game
    URL-guard parameter remains the canonical Phase L
    feature-cutover entry.
13. **Pure CPU ray-cast picking (no GPU readback)** — Hicks
    §4.1's `picking.ts` demonstrates the JS V8 JIT path is
    fast enough for the 102-tile scene budget. Pattern
    informs W18 hit-test design.
14. **5-row gate for region apply (PARTIAL-GREEN/HOLD)** —
    Apone D3's us-east-1 plan capture uses a 5-row gate
    (terraform-drift / payload-ceiling / Kyverno-clean /
    HPA-headroom / SLSA-freshness). Pattern reusable for
    any future region apply.
15. **Single intentional pin exception
    (`slsa-github-generator@v2.0.0`)** — Apone D5's
    documented exception. Pattern for any future
    pin-convention exception: document explicitly in a
    `*-rationale.md §X.Y (NEW W17)` section.
16. **Design-doc-at-N / apply-at-N+1 split** — Apone D4's
    HPA cron-override pattern. Non-trivial test matrices
    get a full wave of preparation between design and apply.
17. **Cross-wave DbSerial inventory pin** — Vasquez §6.3's
    `DbSerialOpenCandidateInventoryW17Tests.cs`. Asserts
    open + closed candidate sets by name; fails if the
    open set grows or shrinks without an inventory
    amendment. Pattern carries forward to W18+.
18. **Forward-compat regex broadening for sub-wave letter
    suffixes** — Vasquez §6.6's `^Phase K Wave (\d+|17[A-Z]?)$`
    pattern. Defensive broadening for unused suffixes
    preserves future-wave optionality without immediate
    cost.

---

## 8. W17 numeric milestones

| Metric | W16 close | W17 close | Δ | Notes |
|---|---|---|---|---|
| Gate (passed) | 3622 | **3930** | **+308** | (+1 = the W16 close gate had been 3621 in some reads; reconciled to 3622 at W17 open) |
| Gate (failed/skipped) | 0/0 | 0/0 | — | 32-wave zero-skip streak |
| Lane-discipline `checked` | 5 | 5 | 0 | (Hicks + Apone + Bishop + Vasquez + Scribe) |
| Lane-discipline `violations` | 0 | 0 | 0 | 7th consecutive 0-violation wave |
| `shared_files` registry entries | 8 | 8 | 0 | 4th unamended wave in streak |
| `three-renderer-big.js` bytes | 406,635 | 406,635 | 0 | 7th hold-line wave |
| `autotable-src-eager.js` bytes | 214,202 | **176,907** | **−37,295** | 2.65× §3.2 target |
| `renderer-webgl2` bytes | 19,017 | 24,743 | +5,726 | 11.2 % of 220 KB envelope |
| `dist-size.json` tracked chunks | 24 | 27 | +3 | leaderboard + settings-drawer + profile-page |
| DbSerial inventory total | 26 | 29 | +3 | 25 closed + 4 open |
| Identity-hardening clean waves | 11 | 12 | +1 | |
| Mutex fully-adopted waves | 7 | 8 | +1 | |
| Coordinator-direct intervention streak | 11 | 12 | +1 | Categorically distinct from §6.5 EXECUTION |
| Coordinator-direct EXECUTIONS (new metric) | 0 | 1 | +1 | §6.5 3-shot LH13 cron seed |
| SLSA-3 SHA pin count | 6 | 56 | +50 | Across 9 workflows |
| `Co-authored-by: Copilot` trailer coverage | 100 % | 100 % | — | All 4 bring-up + Scribe |
| W6 → current gate ratio | 2.55× | **2.76×** | +0.21× | (W6 baseline = 1422) |
| W6 → current gate % | +154.7 % | **+176.4 %** | +21.7 pp | |

### Gate trajectory W6 → W17

```
W6:  1422
W7:  1506   +84
W8:  1706  +200
W9:  1880  +174
W10: 2108  +228
W11: 2403  +295
W12: 2610  +207
W13: 2789  +179
W14: 3029  +240
W15: 3312  +283
W16: 3621  +309
W17: 3930  +309   ← W17 close
```

**W17 contribution (+309) matches W16 contribution exactly**
— the 2-wave +309/+309 is the first time the wave-on-wave
delta has held constant across two consecutive waves in the
W11 → W17 window. Gate trajectory remains super-linear
relative to W6 baseline.

---

## 9. W18 forward queue (per-lane)

### Bishop (Backend)

- **JWKS validator wire-up at refresh-token call site** —
  W17 §3.1 wires `IssueForTenantAsync`; W18 extends to
  `RefreshForTenantAsync` + adds the same Prometheus
  counter labels.
- **`DateTimeOffset` widening round 3** — cover the
  remaining `DateTime`-only columns on
  `TournamentRoundRecord` + `LeaderboardSnapshot`.
  Wave tag: `phase-k-w18-r3`.
- **Tournament-query alert SLO doc** — companion SLO doc
  for the §3.6 alerts (matches the `signalr-sequence-slo.md`
  shape Bishop landed at W16).
- **Per-tenant SignalR retention admin UX surfacing in
  operator dashboard** — coordinate with Hicks on the
  dashboard hook-up (the admin CRUD lands W17; the
  dashboard hook is W18 frontend work).
- **DbSerial migration of the 4 open Bishop-lane
  candidates** — must be Bishop-authored to respect the
  lane rule; W18 is the soonest opportunity. Candidates
  are listed in `docs/test-architecture.md §3.4a + §3.4b`.

### Hicks (Frontend)

- **Phase L animation graph** — design doc target ≤30 KB
  cumulative under the 220 KB envelope. Builds on W17's
  scene + picking foundation.
- **§3.2 audit next slice** — W17 cleared the lobby trio
  (leaderboard + settings-drawer + profile-page);
  candidates for W18 are the `?developer` console (8.2 KB
  estimated) + the `tournament-bracket` view (12.7 KB).
- **LH13 §8 update with post-Coordinator-direct seed run
  history** — incorporate the 3 manual runs fired this
  wave + any cron runs between W17 and W18 close.
- **`three-renderer-big` audit-candidate landing** — W17
  is the 7th hold-line wave; W18 is the first wave where
  the §3.1 + §3.5 documented shrinkage candidates from
  W15 may become ripe for landing (depends on Phase L
  animation graph bandwidth).
- **SignalR retention admin operator dashboard hook** —
  coordinate with Bishop on the W17 §3.7 admin CRUD
  surface.

### Apone (DevOps)

- **us-east-1 W17 apply** — pending Row 2 re-measurement
  (confirmed at 176,907 B post-Hicks-rebase = under 200 KB
  ceiling) + Row 4 cron-override PR landing.
- **HPA cron-override PR** — W17 D4 ships the design doc;
  W18 lands the workflow + CronJob YAML + 3-test smoke
  matrix.
- **Mobile signed artifact verification** — once Stephen
  uploads the four `ANDROID_*` secrets, document the
  signed `.aab` verification path (D2 forward-note).
- **Kyverno enforce 14-day post-flip retrospective** —
  W17 covered the 7-day mid-window; W18 closes the
  14-day window.
- **SLSA-3 pin expansion phase 2** — Renovate-triggered
  SHA bumps for the 50 W17 pins; verify the Renovate
  rules behave as designed.

### Vasquez (QA)

- **KW17 → KW18 regression rename** — rewrite W17 pin
  to `_Historical`; add W18 rename pin; broaden
  W11-W17 self-lane wave-name regexes if any sub-wave
  letter suffixes land.
- **W17 forward-stage tests promotion to W18 hard-asserts**
  — soft-pins that proved stable promote to hard-asserts
  in `Wave1ThroughKW18RegressionTests.cs`.
- **DbSerial inventory amendment** — if Bishop migrates
  the 4 open candidates, amend `docs/test-architecture.md
  §3.4a + §3.4b` to reflect the new closed count (target:
  29/29 closed).
- **Branch-protection §4.5 / §4.7 / §4.8 — Stephen
  option-selection acknowledgement** — once Stephen
  selects Option A / B / C, capture the choice in
  `.squad/decisions.md` + execute the `gh api -X PUT`
  under the §4.7 pre-flight gate.
- **LH13 convergence check** — re-read the `pwa-audit.yml`
  schedule-event history at W18 open; if 3 consecutive
  green runs landed, flip LH13 from
  `provisional-until-calibrated` to `calibrated-green` in
  `docs/lh13-soft-pin-rationale.md §9 NEW`.

### Scribe (Archive)

- **W18 wave summary** following this 12-section template.
- **`.squad/decisions.md` W18 fold** appending after this
  W17 fold.
- **W18 Scribe history entry** following the W17 entry
  shape.

---

## 10. Stephen action items (8 carried into W18)

1. **Branch-protection §4.8 — select Option A / B / C and
   acknowledge in `.squad/decisions.md`.** Coordinator-direct
   apply remains BLOCKED until selection lands (§4.5
   downgrade). Reversibility-first framing: this is a
   hard-blast-radius decision; selection should be
   deliberate.
2. **Mobile CI — upload the four `ANDROID_*` secrets
   (`KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_ALIAS`,
   `KEY_PASSWORD`).** Once uploaded, the SIGNED branch of
   `mobile-build.yml` will fire automatically on the next
   release. Runbook: `docs/mobile-android-signing.md`.
3. **us-east-1 W17 apply approval** — Row 2 re-measured
   post-Hicks-rebase confirms 176,907 B (under 200 KB
   ceiling); Row 4 cron-override design doc lands W17,
   PR lands W18. Approve the W18 apply after the cron-
   override PR merges.
4. **LH13 convergence window** — the 3-shot Coordinator-
   direct cron seed fired this wave; observe whether 3
   consecutive green runs land before W18 close. If yes,
   approve Vasquez's flip of LH13 to `calibrated-green`.
5. **Renovate SHA-pin bump policy** — confirm that
   Renovate's PR-only flow (not auto-merge) is the
   intended behaviour for the 50 W17 SHA pins.
6. **DbSerial migration scheduling** — confirm that
   Bishop W18 is the right wave to migrate the 4 open
   candidates (vs deferring to W19 if Bishop has higher-
   priority work).
7. **`slsa-github-generator@v2.0.0` tag-pin exception** —
   confirm the exception documented in
   `docs/slsa-3-pinning-rationale.md §3.2` matches
   organisational policy.
8. **Reversibility-first decision matrix** — confirm that
   the W17 §4.5 vs §6.7 asymmetry (DOWNGRADE
   high-blast-radius / PROMOTE trivial-reversible) becomes
   the canonical 4-quadrant decision framework for any
   future deferred action.

---

## 11. Identity hardening recap (W6 → W17 — 12 consecutive clean waves)

- **Per-invocation `git -c user.name=X -c user.email=Y
  commit ...`** convention adopted at W6; **12 consecutive
  waves** (W6-W17) with zero identity drift.
- **`flock -w 120 9 ... 9>.work/squad-git-lock`** mutex
  fully adopted at W10; **8 consecutive waves** (W10-W17)
  with zero race conditions.
- **`Co-authored-by: Copilot
  <223556219+Copilot@users.noreply.github.com>`** trailer
  on every commit; 100 % coverage across all 4 W17
  bring-up commits + this Scribe sweep commit.
- **Selective `git add <specific paths>` only** (never
  `-A` / never `.`); zero stray-file commits across all
  W17 commits.
- **`git rebase origin/<branch>` inside the flock window**
  precedes every `git push` — eliminates the
  push-after-stale-base race.

The W6-initiated identity hardening regime has now run for
**12 consecutive waves without incident**. The convention
is sufficiently mature that no W17 commit required any
retroactive identity rewrite or amend.

---

## 12. Sign-off

**Phase K Wave 17 closes with:**

- Gate: **3930 passed / 0 failed / 0 skipped** (+309 over W16;
  2.76× W6 baseline; 32-wave zero-skip streak)
- Lane-discipline: **`checked=5 violations=0`** — 7th
  consecutive 0-violation wave; 4th unamended wave in the
  streak; 8-entry `shared_files` registry held for 3 waves
- Identity hardening: 12th clean wave; mutex 8th
  fully-adopted wave; Coordinator-direct intervention
  zero-streak at 12 waves
- Coordinator-direct EXECUTIONS: **1 NEW** (§6.5 3-shot
  LH13 cron seed — first since the no-pauses directive;
  categorically distinct from intervention)
- Bundle ledger: `three-renderer-big` 7th hold-line wave at
  406,635 B; `autotable-src-eager` −37,295 B at W17 alone
  (largest single-wave §3.2 cut since the audit landed);
  `renderer-webgl2` 11.2 % of 220 KB Phase L envelope
- Cross-cutting conventions added: 18 (reversibility-first
  asymmetry + Coordinator-direct intervention vs execution
  distinction headline the list)
- W18 hand-offs: 5 per-lane + 8 Stephen action items
- DbSerial inventory: 29 total / 25 closed / 4 open (all
  Bishop-lane; Vasquez-authored attribute application would
  trip the lane-discipline gate)
- Stephen action items: 8 (branch-protection Option
  selection is the highest-priority item)

**Phase K Wave 17 — DONE.**
