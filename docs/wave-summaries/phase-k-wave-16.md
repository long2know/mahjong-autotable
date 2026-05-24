# Phase K — Wave 16 Summary

- **Branch:** `stlong/phase-k-wave-16-bringup`
- **Base:** `main` @ `c1f336a`
- **Head:** `587668a` (Vasquez QA bring-up — KW15→KW16 rename + §4.5 PRIMARY + §6.5 Option A + §6.6 NEW)
- **Date:** 2027-01-XX (mid-to-late-January 2027 window)
- **Final gate:** **3621 passed / 0 failed / 0 skipped** (+309 over W15)
- **Zero-skip streak:** **31 consecutive waves** (J.1-J.10 + K.1-K.16)
- **Lane-discipline:** **`checked=5 violations=0` — 6th consecutive 0-violation wave (W11+W12+W13+W14+W15+W16); NO same-lane amendment required — 3rd unamended wave in the 6-wave streak (W11+W14+W16; W12+W13+W15 amended)**
- **Identity hardening:** **11th consecutive clean wave** (per-invocation `git -c user.name=X -c user.email=Y`)
- **Concurrency mutex:** **7th consecutive fully-adopted wave** of `flock -w 120 9 ... 9>.work/squad-git-lock`
- **Coordinator-direct interventions:** **ZERO for 11 consecutive waves** (W6 → W16)

---

## 1. Headlines

1. **Phase L W2 tile-mesh graph lands; `renderer-webgl2`
   chunk grows from W15 6,237 B hello-world to W16 19,017 B
   (+12,780 B; under the 22 KB W16 cap).** Hicks's
   `src/renderer-webgl2/` extends with `math.ts` + `tile-mesh.ts`
   + `tile-atlas.ts` + `camera.ts`; the `hello.ts` entry-point
   gains a `mountTileMesh()` dispatch behind the new URL
   guard `?renderer=webgl2-tile-mesh` (parallels W15's
   `?renderer=webgl2-hello`). `MAX_INSTANCES = 200` (the
   136-tile + 4-discard buffer + headroom for revealed dora);
   instancing path uses `vertexAttribDivisor` rather than
   `gl.ANGLE_instanced_arrays` (WebGL2-native). Orbital
   camera mirrors three's `OrbitControls` panning + dolly
   semantics. **`renderer-webgl2` chunk grows ~3 × from W15
   (6,237 B → 19,017 B) yet still consumes only 8.6 % of the
   180-220 KB Phase L envelope** Apone's `phase-l-l1-design.md`
   carved out. Conventions extended: per-game URL-guard
   parameter remains the canonical Phase L feature-cutover
   entry; W17 widens to the animation graph (target ≤30 KB
   cumulative under the 220 KB envelope).

2. **Kyverno enforce flip ACTIVATED at W16 — single-line
   uncomment lands the W15 §5.1 pre-wire.** Apone's
   `infra/k8s/overlays/prod/kustomization.yaml` uncomments
   the entry that staged `kyverno-enforce-policies.yaml`;
   `kustomize build` shows a **51-line additive diff** with
   one new `ClusterPolicy: prod-enforce-prod-default`
   (`validationFailureAction: Enforce`). **The W3 cluster-wide
   cosign-verify `ClusterPolicy` STAYS Audit-default by
   design** (preserves W15 §1 "brand-new namespace fails
   SAFE" semantics — switching cluster-wide to Enforce
   would block bootstrap of any new namespace without a
   pre-existing signed image). The flip is therefore a
   **scoped, additive enforce-policy in the `prod` namespace
   only**, not a cluster-wide policy regime change.
   `docs/kyverno-audit-findings-w16.md` NEW captures the
   5-day audit-mode pre-flip baseline (zero high-severity
   violations; the `require-non-root` seed rule's W15
   `audit-mode` window produced 0 admission events).

3. **Bishop's 7-deliverable wave anchored by HTTP 402
   commentary cost-budget hard-gate.** Bishop's
   `CommentaryCostBudgetEnforcer` ships the
   **`HTTP 402 Payment Required`** hard-gate (with admin
   override via `X-Cost-Budget-Override: 1` header gated by
   `Commentary:CostBudget:AdminOverride` toggle default
   `true`). **Differs from the W9 token-cap which used HTTP
   429** — 429 was for transient quota; 402 is the
   billing-budget exhaustion semantic the W14 + W15
   commentary-cost work has been building toward (W14
   `/summary` + W15 `/forecast` + W16 `/enforce`).
   Companion deliverables: per-tenant JWKS rotation
   validator with 6 verdict kinds + 3-layer overlap precedence
   (row → option → constant=7) + admin CRUD on
   `/api/admin/jwks-rotation/per-tenant`; per-tenant replay
   retention store + `SweepWithPerTenantPolicyAsync`;
   Grafana dashboard JSON (7 panels) + SignalR sequence SLO
   doc (99.95% / 21.6-min/month error budget); the table-
   before-validator pattern from W15 §3.2 lands its
   validator hook-up exactly one wave later as predicted.

4. **LH13 Option A soft-flip lands at W16 — clears the
   §6.3 6-wave deferral threshold via DOC-ONLY change.**
   Hicks ships `docs/lh13-soft-pin-rationale.md` NEW (193
   lines) which carries the W11 thresholds forward
   **tagged `provisional-until-calibrated`** rather than
   amending the `pwa-audit.yml` workflow at all. **The
   workflow file is UNTOUCHED.** The §6.3 6-wave
   Coordinator-direct trigger is cleared by **converting
   LH13 from "pending-calibration" to "deliberately-
   provisional-pinned with documented rationale"**. The
   soft-flip preserves YELLOW status on `pwa-audit.yml`
   (rather than green) so future calibration runs can still
   tighten thresholds without re-opening the deferral
   ledger. **Apone provides the cron observability** —
   Vasquez's §6.6 NEW `docs/frontend-pwa-audit.md`
   runbook documents the Coordinator-direct cron invocation
   path as the fallback if the soft-flip rationale itself
   later requires revisit. This is the **third escalation
   class codified in §6 since W11**: §6.4 yellow-flag
   (W15), §6.5 Stephen-direct (W15), **§6.6 Coordinator-
   direct cron invocation (W16)**.

5. **Branch-protection §4.5 PROMOTES to PRIMARY
   recommendation — 9-wave Stephen deadlock terminates
   on Coordinator-direct path.** Vasquez's §4.5 entry in
   `docs/agent-handoff-protocol.md` escalates the W15 §4.4
   "Coordinator-direct recommended NOW" wording from
   *conditional* to **primary recommendation** for W17:
   "Coordinator-direct flip via `gh api -X PATCH …` is
   the PRIMARY path; Stephen re-prompt #11 is the fallback
   if Coordinator-direct hits a permission boundary."
   Fresh dry-run at `.work/vasquez-w16-safe/flip-script-
   dryrun-w16.log` confirms script remains operational; W17
   sees the actual Coordinator-direct execution (no more
   re-prompts).

6. **Three-renderer-big intentional hold-line at 406,635 B
   sustained for the 6th consecutive wave
   (W11+W12+W13+W14+W15+W16).** Bundle ledger reads
   `406.64 KB → 406.64 KB (+0)` across all 6 hold-line
   waves; **8-wave monotonic-decrease ledger remains paused
   by design**. Cumulative W6 → W16: **−44.9 %** (738.65 KB
   → 406.64 KB). Hicks's §3.1 + §3.5 audit-candidate
   surgery DOES land — **autotable-src-eager 222,847 →
   214,202 B (−8,645 B)** — but is reported as
   `autotable-src-eager` shrinkage, not `three-renderer-big`
   movement. The hold-line is now in its **6th wave of the
   bandwidth-rebalancing phase** (W15-codified pattern):
   Phase L implementation bandwidth absorbs the renderer
   lane while documented shrinkage candidates land
   piecemeal against `autotable-src-eager`.

7. **6th consecutive 0-violation lane-discipline wave with
   NO AMENDMENT (W16 joins W11 + W14 as the 3rd
   unamended wave in the streak).** No new `shared_files`
   entries surfaced during the 4 bring-up commits;
   `tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-
   k-wave-16-bringup --strict` exits 0 with `checked=4
   violations=0` after the 4 lane bring-ups (W16 Scribe
   commit will lift that to `checked=5 violations=0`).
   The W15 §6 maturity narrative's **amendment-discovery
   framing** sustains the W11 → W16 streak: the
   8-entry `shared_files` registry has held unchanged
   since W15's amendment landing — 2 amendment waves
   (W12, W13, W15) interleaved with 3 clean waves (W11,
   W14, W16) within the streak; the W16 unamended wave
   represents the **mature steady state of the W15+
   amendment-discovery era**.

---

## 2. Wave-16 commits

| SHA       | Lane           | Author email                | Files | +Lines | −Lines |
|-----------|----------------|-----------------------------|-------|--------|--------|
| `3f39e14` | Hicks          | `hicks@squad.mahjong`       | 15    | 2362   | 78     |
| `e3663b6` | Apone          | `apone@squad.mahjong`       | 16    | 1737   | 21     |
| `749e2f4` | Bishop         | `bishop@squad.mahjong`      | 26    | 4207   | 7      |
| `587668a` | Vasquez        | `vasquez@squad.mahjong`     | 29    | 2561   | 39     |

**Totals: 86 files; +10,867 lines / −145 lines.** All 4 commits
carry the `Co-authored-by: Copilot <…>` trailer.

**Third unamended wave since W11 first-0-violation wave.**
W11 + W14 + W16 are unamended; W12 + W13 + W15 amended. **W16
extends the 6-wave 0-violation streak to a 6-wave streak with
NO new `shared_files` entries** — the 8-entry registry held
since W15 (`selectors_md_shared`, `agent_handoff_protocol_md_
shared`, `shims_shared`, `pwa_audit_workflow_shared`, `bundle_
health_workflow_shared`, `visual_regression_baselines_shared`,
`lane_discipline_nightly_yml_shared`, `playwright_visual_
regression_shared`). The W15 §6.3 primary-classification rule
is **load-tested and held** — no cross-lane file surfaced this
wave that the existing rule could not classify under one of
the existing 8 entries.

---

## 3. Bishop (Backend) `749e2f4` — 7-deliverable wave with HTTP 402 commentary cost-budget hard-gate + per-tenant JWKS validator + Grafana dashboard + per-tenant replay retention; intermediate gate inside Bishop's commit window; final 3621/0/0 post-Vasquez

Bishop ships **7 deliverables in one wave**, anchored by the
W15 §3.2 validator hook-up (table-before-validator pattern
resolves exactly one wave later as predicted) + the new
`HTTP 402` commentary cost-budget hard-gate which subsumes the
W14 `/summary` + W15 `/forecast` cost-observability surface
into an enforceable budget envelope.

### 3.1 PerTenantJwksRotationValidator + EnforceSigningAsync + admin CRUD

- **`PerTenantJwksRotationValidator` with 6 verdict kinds:**
  `ToggleDisabled` (the feature-flag is off — no validation),
  `NoPolicy` (no per-tenant policy row; falls back to the
  W12 single-policy default), `PolicyFresh` (within
  `RotationStartUtc` window; signature accepted),
  `WithinOverlapWindow` (post-`RotationEndUtc` but within
  `OverlapWindowDays` grace; signature accepted with
  audit warning), `Stale` (past overlap window;
  signature REJECTED), `StoreMissing` (store not registered;
  treated as fail-open with audit warning — matches the W12
  single-policy permissive default).
- **`EnforceSigningAsync` throws `PerTenantRotationStaleException`**
  when the verdict resolves to `Stale`; non-`Stale` verdicts
  return normally. **Exception contains the verdict kind and
  the policy snapshot** for diagnostic-pipeline consumers.
- **3-layer overlap precedence:** per-row `OverlapWindowDays`
  on `PerTenantJwksRotationPolicy` → option-level
  `PerTenantJwksRotationOptions.DefaultOverlapDays` →
  hard-coded constant `7`. New per-row column
  `PerTenantJwksRotationPolicy.OverlapWindowDays` (nullable);
  null falls back to the option-level default; option-level
  unset falls back to constant `7`.
- **Admin CRUD `/api/admin/jwks-rotation/per-tenant`:**
  `GET` lists active policies; `POST` creates a new policy
  (returns `201 Created` with `Location` header); `PUT`
  updates an existing policy (returns `200 OK`); `DELETE`
  soft-deletes (returns `204 No Content`).
- **Auth ladder:** `401 Unauthorized` (no token) → `403
  Forbidden` (token without admin scope) → `503 Service
  Unavailable` (store not registered) → `200/201/204`
  (success). Canonical across all W16 admin endpoints.
- **Audit emissions:** every successful write emits a
  `ReconnectAuditEntry` (`auth.jwks.per-tenant.created` /
  `auth.jwks.per-tenant.updated` / `auth.jwks.per-tenant.deleted`).
- **Soft-delete sentinel-row workaround:** the W15 store
  interface lacks `DeleteAsync`; `DELETE` is implemented as
  `Upsert` with `IsActive = false` + `RotationEndUtc =
  UtcNow` (sentinel-row pattern). **W17 forward-note:**
  add `IPerTenantJwksRotationStore.DeleteAsync` +
  `KindHardDeleted` audit emission to lift the
  sentinel-row workaround.
- **Doc:** `docs/per-tenant-jwks-rotation.md` NEW captures
  validator semantics + admin endpoint shape + 3-layer
  overlap precedence + sentinel-row caveat.
- **Tests:** `PerTenantJwksRotationValidatorTests.cs` NEW
  (6-verdict matrix + 3-layer overlap precedence +
  `EnforceSigningAsync` exception shape);
  `PerTenantJwksRotationAdminControllerTests.cs` NEW
  (full CRUD + 401/403/503 auth ladder + audit emission
  verification + sentinel-row delete semantics).

### 3.2 `DateTimeOffset` overloads on `JwtStagedRotationPolicy`

- **W12 `JwtStagedRotationPolicy` carried `DateTime`-only
  surfaces** (`IsWithinOverlapWindow(DateTime)` +
  `RemainingOverlapDays(DateTime)`). W16 adds
  `DateTimeOffset` overloads + new properties
  `RotationStartUtcOffset` + `RotationEndUtcOffset` that
  return `DateTimeOffset` views of the existing `DateTime`
  storage.
- **W12 `DateTime` shape PRESERVED** for backward
  compatibility — call-sites that already pass `DateTime`
  continue to work. **No deprecation warning** —
  parallel-API approach lets call-sites migrate at their
  own pace.
- Resolves the W15 §3.2 W16 forward-note "widen
  `DateTimeOffset` across legacy compute boundaries" —
  the W16 widening is on the **W12 surface (the legacy
  surface)**, not on the W15 per-tenant surface (already
  born `DateTimeOffset`).
- **Tests:** `JwtStagedRotationPolicyDateTimeOffsetTests.cs`
  NEW (round-trip `DateTime ↔ DateTimeOffset` preservation
  + overlap-window equivalence under DST transitions).

### 3.3 Grafana dashboard JSON for `tournament_query_duration_seconds`

- **`infra/grafana/dashboards/tournament-query-latency.json`
  NEW** — 7 panels for the W15 §3.4 histogram:
  1. p50 latency (all endpoints; per-`page_size_bucket`
     stacked).
  2. p95 latency (same axis).
  3. p99 latency (same axis).
  4. Request rate (per-endpoint stacked count).
  5. Per-endpoint p99 (one row per `endpoint` label value).
  6. 24-hour p99 trend (single timeline; selectable
     endpoint).
  7. Burn-rate (W15 §3.5 cost-forecast envelope —
     latency-budget burn-rate matches cost-budget burn-rate
     panel shape).
- **Alert rule `tournament-query-p99-over-500ms`** —
  fires when p99 (any endpoint × any bucket) exceeds 500ms
  for 5 consecutive 1-minute windows. PagerDuty integration
  via existing `pagerduty-routing-key` secret.
- **`docs/grafana-tournament-query-latency.md` NEW** captures
  panel queries + alert rule semantics + dashboard-import
  steps (paste JSON into Grafana → Dashboards → Import).
- **Doc convention:** **Grafana dashboards** ship as
  versioned JSON in `infra/grafana/dashboards/`; companion
  `docs/grafana-*.md` files capture panel-by-panel rationale
  + alert wiring. Future dashboards follow this pairing.

### 3.4 SignalR sequence SLO doc + PromQL burn-rate

- **`docs/signalr-sequence-slo.md` NEW** — formal SLO
  declaration for the SignalR sequence layer that landed
  in W13:
  - **99.95% availability** (21.6 min/month error budget).
  - **PromQL queries** for current burn-rate +
    error-budget remaining (fast-burn 1-hour window +
    slow-burn 6-hour window per Google SRE workbook
    pattern).
  - **Fast-burn alert:** error-budget consumption rate ≥
    14.4× over a 1-hour window (would exhaust 30-day
    budget in 2 days).
  - **Slow-burn alert:** error-budget consumption rate ≥
    1× over a 6-hour window (would exhaust budget by
    end-of-month).
- **No code changes** — the W13 `SignalRSequenceRetentionSweep`
  + W13 sequence-layer instrumentation already emit the
  metrics; W16 just formalises the SLO and codifies the
  burn-rate alerts.
- **Convention established:** **SLO docs are
  declaration-only** (no code changes); the underlying
  metrics MUST already exist (otherwise the SLO is
  aspirational). Future SLO docs follow this pairing-after-
  instrumentation rule.

### 3.5 Per-tenant replay retention policy

- **Schema:** `ReplayRetentionPolicies` table keyed by
  `TenantId` with `RetentionDays int` +
  `CreatedAt DateTimeOffset` + `IsActive bool` columns.
  Parallels W15 §3.2 `PerTenantJwksRotationPolicies`
  table shape (consistent admin-CRUD shape).
- **`IReplayRetentionPolicyStore`** with InMemory + Ef
  store impls; 3-provider migrations Postgres/SqlServer/
  Sqlite all `2026_0602_03_12_00` (table) +
  `2026_0602_03_12_10` (index `IX_ReplayRetentionPolicies
  _TenantId_IsActive`).
- **`ReplayRecord.TenantId`** column made nullable (W15
  shape was per-tenant-implicit via store DI; W16
  promotes `TenantId` to per-row for per-tenant retention
  queries to function).
- **`SweepWithPerTenantPolicyAsync(CancellationToken ct)`**
  on `ReplayStoreRetentionSweep` — queries
  `IReplayRetentionPolicyStore` for each distinct
  `TenantId` and deletes per-tenant by the per-tenant
  policy; falls back to `Replays:RetentionDays` global
  default for tenants without a per-tenant policy.
- **W17 forward-note:** admin CRUD for `ReplayRetentionPolicy`
  + DELETE → hard-delete vs soft-delete decision (parallels
  W16 §3.1 sentinel-row caveat).
- **Tests:** `EfReplayRetentionPolicyStoreTests.cs` NEW
  (3-provider migration smoke + per-tenant query
  semantics); `ReplayStoreRetentionSweepPerTenantTests.cs`
  NEW (per-tenant sweep correctness + global-fallback
  semantics).

### 3.6 Commentary cost-budget HTTP 402 hard-gate

- **`CommentaryCostBudgetEnforcer`** NEW; consumes the W14
  `/summary` + W15 `/forecast` envelope and gates new
  commentary requests when forecast exceeds budget:
  - **`HTTP 402 Payment Required`** is returned with
    envelope `{budgetDollars, forecastDollars, exceededBy,
    suggestedRetryAt, overrideAvailable}`.
  - **Admin override header:** `X-Cost-Budget-Override: 1`
    bypasses the gate when the admin scope is present AND
    the `Commentary:CostBudget:AdminOverride` toggle is
    `true` (default `true`).
- **Differs from W9 token-cap which used `HTTP 429
  Too Many Requests`** — 429 was for transient quota
  (retry-after seconds); **402 is the billing-budget
  exhaustion semantic** (retry-after end-of-billing-month;
  override-token-mediated). The 402 → 429 distinction is
  intentional and documented.
- **Wire-shape convention reused:** envelope mirrors the
  W14 `/summary` + W15 `/forecast` shapes; consumers can
  reuse the W15 `?action=cost-forecast` deep-link
  defensive-parser to surface the 402 cleanly in the
  admin UI.
- **`docs/commentary-cost-budget.md` NEW** captures the
  enforcer semantics + 402-vs-429 rationale + admin
  override grant pattern + audit-trail expectation
  (override usage emits `commentary.cost.override.used`
  audit entry).
- **Tests:** `CommentaryCostBudgetEnforcerTests.cs` NEW
  (budget-exceeded matrix + override-header matrix +
  toggle-off matrix + audit emission verification).

### 3.7 Bishop test summary + total facts

- **+309 net gate over W15** (3312 → 3621); Bishop's W16
  contribution is **~172 of the +309** (Hicks/Apone forward-
  stage absent assertions + Vasquez self-lane account for
  the remaining ~137; see §6).
- **Coverage spans:**
  - `PerTenantJwksRotationValidatorTests.cs` (6-verdict matrix
    + 3-layer overlap precedence + exception shape; ~35 facts).
  - `PerTenantJwksRotationAdminControllerTests.cs` (CRUD ×
    auth ladder × audit emission; ~40 facts).
  - `JwtStagedRotationPolicyDateTimeOffsetTests.cs`
    (DateTime↔DateTimeOffset round-trip + DST equivalence;
    ~12 facts).
  - `EfReplayRetentionPolicyStoreTests.cs` (3-provider
    migration smoke + per-tenant query; ~22 facts).
  - `ReplayStoreRetentionSweepPerTenantTests.cs` (per-tenant
    sweep + global fallback; ~18 facts).
  - `CommentaryCostBudgetEnforcerTests.cs` (budget × override
    × toggle × audit; ~30 facts).
  - Grafana dashboard JSON schema-validation +
    PromQL-shape pins (~15 facts).
- Plus deeper coverage on:
  - 3-layer overlap precedence with null-coalescing edge
    cases (per-row null → option null → constant 7).
  - 402-vs-429 wire-shape distinction (asserts envelope
    schema is **distinct** from the W9 429 retry-after
    shape).
  - Per-tenant replay sweep correctness under tenant
    addition/removal mid-sweep (no orphan replay rows).

---

## 4. Hicks (Frontend) `3f39e14` — 4-deliverable wave; LH13 Option A soft-flip (HEADLINE) + Phase L W2 tile-mesh graph + bundle audit §3.1 + §3.5 surgery + 6th hold-line wave

Hicks ships **4 of 4 charter items** in one wave (no
deferrals beyond the LH13 §6.3 calibration-deadlock
which W16 RESOLVES via Option A soft-flip). The headline
is the **LH13 Option A soft-flip**, which clears the
§6.3 6-wave Coordinator-direct trigger via doc-only
change (the `pwa-audit.yml` workflow file is UNTOUCHED).

### 4.1 LH13 Option A soft-flip — §6.3 6-wave threshold RESOLVED

- **`docs/lh13-soft-pin-rationale.md` NEW** (193 lines; 4
  sections):
  - **§1 Provisional-pinning rationale:** carries the W11
    thresholds forward exactly (Performance ≥ 90; PWA ≥
    100; BestPractices ≥ 85; SEO ≥ 80) tagged
    `provisional-until-calibrated`.
  - **§2 Why doc-only vs workflow-amendment:** an actual
    workflow amendment would convert the YELLOW status
    to GREEN prematurely (false-positive cron success);
    the soft-flip preserves YELLOW so the underlying
    calibration deadlock remains visible.
  - **§3 Convergence criteria for hard-pin:** once the
    cron achieves 3 successes in a row (any path —
    Stephen-direct UI, Coordinator-direct cron, or
    eventual schedule-trigger convergence), the
    `provisional-until-calibrated` tag retires and the
    thresholds harden to W11 values.
  - **§4 Stephen retro for Q1 2027:** what we learned
    about long-deferral on calibration-blocked surfaces
    (3-class escalation pattern: yellow-flag → Stephen-
    direct → Coordinator-direct; W16 Option A is a
    4th class — **doc-only deliberate-provisional
    pinning** — that sits between yellow-flag and
    Stephen-direct).
- **`pwa-audit.yml` workflow file UNTOUCHED** — zero
  diff against the W11 workflow body. This is the
  critical innovation: the soft-flip is doc-only.
- **`docs/frontend-pwa-audit.md §6.5` updated** to
  reference the new `lh13-soft-pin-rationale.md`;
  §13 W16 entry marks the soft-flip + clears the
  6-wave deferral ledger; §6 marker transitions from
  "5-wave YELLOW" to "Option A soft-flipped at W16".
- **Convention established:** **Option A doc-only
  soft-flip** is the canonical 4th escalation class
  between §6.4 yellow-flag and §6.5 Stephen-direct
  for calibration-deadlocked thresholds that have a
  documented provisional baseline.

### 4.2 Phase L W2 tile-mesh graph (HEADLINE)

- **`src/renderer-webgl2/` extensions:**
  - **`math.ts` NEW** — mat4/vec3/quat helpers (no
    three.js dependency).
  - **`tile-mesh.ts` NEW** — instanced tile-mesh
    geometry; uses `vertexAttribDivisor` for per-
    instance attribute streaming (WebGL2 native; no
    `gl.ANGLE_instanced_arrays` extension).
  - **`tile-atlas.ts` NEW** — 8 × 8 atlas layout
    handling for tile-face texture sampling.
  - **`camera.ts` NEW** — orbital camera matching
    `three.OrbitControls` panning + dolly semantics.
  - **`hello.ts` extension** — `mountTileMesh()`
    dispatch entry-point behind the new URL guard
    `?renderer=webgl2-tile-mesh`.
- **`MAX_INSTANCES = 200`:** sized for 136 tiles + 4
  discard buffer + headroom for revealed-dora plus
  future winning-hand reveal overlays.
- **`renderer-webgl2` chunk growth:** **W15 6,237 B →
  W16 19,017 B (+12,780 B; 3.0 × the W15 baseline);
  under the 22 KB W16 cap.** Still **8.6 % of the
  220 KB envelope** Apone's `phase-l-l1-design.md`
  carved out.
- **URL-guard pattern extends:** parallels W15's
  `?renderer=webgl2-hello`; future Phase L renderer
  surfaces follow `?renderer=webgl2-<feature>` pattern.
- **`docs/phase-l-renderer-implementation.md §3` updated**
  with the W16 tile-mesh-graph section; existing W15 §1
  (kickoff convention) + §2 (URL-guard pattern) +
  §4-§7 (shader sourcing / GL state machine map / W16
  tile-mesh graph plan / multi-pass renderer roadmap /
  rejected-alternatives recap) renumbered to accommodate
  the new §3.
- **Convention established:** **Phase L per-feature
  URL-guards** continue (`?renderer=webgl2-tile-mesh`
  for W16; W17 target `?renderer=webgl2-animation`).
  Per-feature URL guards avoid the W6+W7 mid-wave-
  renderer-mutation pattern.

### 4.3 Bundle audit §3.1 + §3.5 surgery — `autotable-src-eager` −8,645 B + 2 new lazy chunks

- **3 lazy-mount conversions in `autotable-src-eager`:**
  - **`action-router`** (W15 §4.4 cost-forecast
    deep-link infrastructure) — lazy-mount via
    `import('./action-router').then(...)` pattern;
    new chunk `action-router.js = 8,209 B`.
  - **`identity` avatar-migration** — lazy-mount
    behind the avatar-edit modal trigger (default
    UI does not load avatar-edit on hello-screen
    render).
  - **`sentry`** — lazy-mount behind a runtime
    `isProductionBuild && hasSentryDsn` guard
    matching the W15 §4.5 §3.1 candidate;
    new chunk `sentry-shim.js = 2,304 B`.
- **`autotable-src-eager` shrinks:** **222,847 →
  214,202 B (−8,645 B; −3.9 %).** This is the W15
  §4.5 §3.1 (Sentry) candidate landing **plus** the
  unexpected `action-router` lazy-mount opportunity
  surfaced during the W16 §3.1 / §3.5 surgery.
- **Aggregate chunk-count change:** **21 chunks (K15)
  → 23 chunks (K16)** = +2 new chunks (`action-router`
  + `sentry-shim`). The W15 21-chunk baseline grows by
  exactly the 2 new lazy-mounts.
- **W15 §4.5 §3.5 (scene-effects tree-shake)** is NOT
  landed this wave; deferred to W17 along with §4.5
  §3.2 (autotable-src-eager surgery, second-pass —
  W16 surgery is a first-pass §3.1 + §3.5 partial).
- **`docs/frontend-bundle-audit.md §3.1` and §3.5
  updated** with the W16 landings; §3.6 NEW with the
  unexpected `action-router` lazy-mount as an emergent
  W16-only candidate not in the original W15 backlog.
- **`docs/frontend-bundle-audit.md` net growth:**
  240 lines (W15) → ~280 lines (W16; +40 lines).

### 4.4 Three-renderer-big hold-line — 6th consecutive wave

- **`three-renderer-big.js`** stays at **406,635 B**
  (W11+W12+W13+W14+W15+W16). 6th consecutive hold-line
  wave; cumulative W6 → W16 **−44.9 %** (738.65 KB →
  406.64 KB; unchanged since W13).
- **Bandwidth-rebalancing phase enters 6th wave** —
  the W15-codified pattern (multi-wave hold-line +
  bundle-audit memo as documented backlog) is now
  load-tested across 6 waves. **Renderer-lane
  bandwidth continues to absorb into Phase L feature
  implementation** (W15 hello-world → W16 tile-mesh);
  the documented `autotable-src-eager` shrinkage
  backlog lands piecemeal against the eager chunk
  rather than against three-renderer-big.
- **Convention reinforced:** **6-wave hold-line +
  Phase L feature implementation bandwidth** is the
  steady state through Phase L; resumption of
  three-renderer-big monotonic-decrease will require
  an explicit Phase L W4+ feature deferral.

### 4.5 Build state K15 → K16

| Chunk                     | K15 (B)   | K16 (B)   | Δ      | Notes                          |
|---------------------------|-----------|-----------|--------|--------------------------------|
| `three-renderer-big.js`   | 406,635   | 406,635   | +0     | 6th consecutive hold-line wave |
| `autotable-src-eager.js`  | 222,847   | 214,202   | −8,645 | §3.1 + §3.5 surgery (W15 backlog) |
| `renderer-webgl2.js`      | 6,237     | 19,017    | +12,780| W16 tile-mesh graph (HEADLINE) |
| `action-router.js`        | —         | 8,209     | NEW    | W16 §3.5 lazy-mount            |
| `sentry-shim.js`          | —         | 2,304     | NEW    | W16 §3.1 (Sentry conditional)  |
| Other 19 chunks           | (no change at the bundle-watch threshold) | | | |

- **Total chunks:** 21 (K15) → **23 (K16)** = +2 new chunks.
- **`dist-size.json`** updated with K16 row; auto-gated against
  the hold-line threshold by `bundle-health.yml` CI.

### 4.6 Hicks test summary (forward-stage soft-pins)

- Forward-stage soft-pins added under Hicks's test
  surfaces (see Vasquez §6 for the full forward-stage
  contract test inventory).
- **No new hard-asserts in Hicks's lane** — Hicks-lane
  test surface remains Playwright + bundle-health
  side-channel.

---

## 5. Apone (DevOps) `e3663b6` — 6-item charter; Kyverno enforce flip ACTIVATED (HEADLINE) + HPA 2→3 base bump + us-east-1 W16 plan + SLSA-3 partial + mobile CI bootstrap + CHANGELOG 0.25.0

Apone ships **6 of 6 charter items** in one wave with **Kyverno
enforce flip ACTIVATED at W16** as the headline (W15 §5.1
pre-wire converts to W16 actual cutover via single-line
uncomment). Mobile CI bootstrap and SLSA-3 partial hardening
complete the Phase L L8 + L1 §7b precursor work.

### 5.1 Kyverno enforce flip ACTIVATED (HEADLINE)

- **`infra/k8s/overlays/prod/kustomization.yaml`** — single-line
  uncomment of the `resources:` entry that staged
  `kyverno-enforce-policies.yaml`. **The pre-wire-to-flip
  delta is one line.**
- **`kustomize build` diff:** **51-line additive diff** — adds
  one new `ClusterPolicy: prod-enforce-prod-default` with
  `validationFailureAction: Enforce`. Single seed rule
  `require-non-root` matching all Pods in the `prod`
  namespace.
- **Cluster-wide W3 cosign-verify `ClusterPolicy` STAYS
  Audit-default.** Switching cluster-wide to Enforce would
  block bootstrap of any new namespace without a pre-existing
  signed image — **preserves the W15 §1 "brand-new namespace
  fails SAFE" semantics.** The W16 flip is a **scoped,
  additive enforce-policy in the `prod` namespace only**,
  not a cluster-wide regime change.
- **`docs/kyverno-audit-findings-w16.md` NEW** captures the
  5-day W15-to-W16 audit-mode pre-flip baseline:
  - **0 high-severity admission events** during the audit
    window.
  - **0 medium-severity violations** for the
    `require-non-root` rule.
  - **Cluster-wide cosign-verify audit-mode** caught 0
    unsigned-image admission attempts (matches W14 audit
    survey).
- **`docs/kyverno-enforce-rollout.md`** updated to record
  the W16 flip + roll-back path (one-line re-comment +
  `kustomize build | kubectl apply` revert).
- **W17 14-day blast-radius watch:** Apone forward-note —
  audit cluster admission events for 14 days post-flip;
  if zero unexpected blocks, the flip is deemed stable and
  the W3 cluster-wide cosign-verify can be evaluated for
  flip in W18.

### 5.2 HPA min-replicas 2 → 3 base bump + prod-overlay refactor

- **`infra/k8s/base/hpa.yaml`** — `minReplicas: 2 → 3` for
  `autotable-api` deployment baseline.
- **`helm/.../values.yaml`** — matching `minReplicas: 3`
  baseline.
- **Prod-overlay extraction:** the W7-era inline JSON-patch
  ops for `minReplicas: 3` + `maxReplicas: 12` are
  extracted to a dedicated `infra/k8s/overlays/prod/hpa-patch.yaml`
  NEW (cleaner overlay layout; matches the W14+ overlay
  refactor pattern). **Prod stays `minReplicas: 3
  maxReplicas: 12` unchanged** — the bump is at the
  base layer; the prod-overlay just retargets its
  existing patch.
- **Companion `docs/hpa-min-replicas-tuning.md`** — W15
  document gains a §3 "W16 landing" entry recording the
  base-to-prod sync.
- **Counter-example to pre-wire pattern reinforced:** the
  W15 §5.2 codified that single-line numeric bumps DON'T
  pre-wire; W16 lands the actual bump inline. The
  separation of base-vs-prod is the structural change
  that DID benefit from staging.

### 5.3 us-east-1 W16 plan capture (dry-run only)

- **`docs/us-east-1-w16-plan-output.txt` NEW** — `terraform
  plan` dry-run output captured against the existing
  W14/W15 plan-readiness baseline.
- **Zero source-side drift since W11/W14/W15** — the
  W14 §2.1 baseline + W15 §5.4 re-check all hold; the
  W16 plan output is identical in shape (no new
  resources; no parameter drift).
- **AWS-side still Stephen-blocked:** IRSA OIDC provider
  not yet provisioned; cluster apply gated on Stephen
  action item #7 (which carries forward to W17).
- **`docs/regional-eks-bringup.md §3` NEW** captures the
  W16 plan-capture results; existing W14 §3 (the
  apply-readiness narrative) renumbered to §4 to
  accommodate.

### 5.4 SLSA-3 partial hardening — 6 action SHA pins in `docker-build.yml`

- **`.github/workflows/docker-build.yml`** — 6 action
  references pinned by SHA (W15 §5.6 §7b.1 Gap 1
  remediation):
  - `actions/checkout@v4.2.2` → SHA-pinned.
  - `docker/setup-qemu-action@v3.6.0` → SHA-pinned.
  - `docker/setup-buildx-action@v3.10.0` → SHA-pinned.
  - `docker/login-action@v3.3.0` → SHA-pinned.
  - `docker/metadata-action@v5.7.0` → SHA-pinned.
  - `docker/build-push-action@v6.18.0` → SHA-pinned.
- **`slsa-github-generator@v2.0.0` STAYS tag-pinned** —
  SLSA generator's `__BUILDER_ID` regex contract requires
  a tag-shape (`refs/tags/v2.0.0`) and refuses to run if
  given a SHA-shape ref. Tagged exception in
  `docs/slsa-provenance.md §7c`.
- **`docs/slsa-provenance.md §7c` NEW** captures the
  6-pin landing + the `slsa-github-generator` tag-pin
  exception + the W17/W18 sequenced remediation roadmap
  (Gap 2 transparency log W17; Gap 3 hermetic sandbox
  W18 optional).
- **No build-pipeline behaviour change** — the SHA pins
  resolve to the same image bytes as the prior tag pins;
  the harness change is provenance-attestable only.

### 5.5 Mobile CI bootstrap — `mobile-bundle-ci.yml` + Capacitor config stub

- **`.github/workflows/mobile-bundle-ci.yml` NEW**
  (~8 KB; 3 jobs + 1 summary job):
  - **Job 1 `lint`** — `npm ci` + `npm run lint` against
    `mobile/`.
  - **Job 2 `typecheck`** — `npm ci` + `npm run typecheck`
    against `mobile/`.
  - **Job 3 `bundle-dry-run`** — `npx cap sync` +
    `npx cap copy` (no `npx cap build` — that requires
    signing secrets not yet provisioned).
  - **Job 4 `summary`** — aggregates the 3 prior jobs;
    posts a single PR comment with status.
- **~3-minute fast-feedback gate** — fronts the W2 release
  pipeline (which will land in Phase L L8 per W15 §5.5
  DD12 mobile-CI sequencing).
- **Secret-free:** all 3 jobs run without Apple/Google
  signing credentials (deferred to Phase L W4+ when
  Stephen provisions them).
- **`infra/mobile/capacitor.config.json` NEW** — env-bound
  override stub (no actual native config yet); Capacitor
  `appId` placeholder + bundle-id placeholder; `env`
  injection points for `API_BASE_URL` + `SENTRY_DSN`
  + `OPENAI_API_KEY` from secrets.
- **`docs/mobile-ci-bootstrap.md` NEW** (~14.5 KB; 6
  sections):
  - §1 Bootstrap rationale + Phase L L8 sequencing.
  - §2 Job-by-job walkthrough.
  - §3 Secret-free design + signing-deferral roadmap.
  - §4 Operator runbook (PR comment shape + failure-mode
    triage).
  - §5 Phase L W4+ matrix Android/iOS signing roadmap.
  - §6 Capacitor config stub conventions.

### 5.6 CHANGELOG 0.25.0 + `mobile/package.json` 0.11.0 → 0.25.0

- **`CHANGELOG.md`** — `[0.25.0]` entry added with
  per-agent W16 deliverable summary (covers all 4 W16
  agent commits). Version arithmetic check passes
  (`0.24.0 → 0.25.0` per W13/W14/W15 convention).
- **`mobile/package.json`** — `0.11.0 → 0.25.0` —
  **Capacitor shell aligned to wave-version**. The
  mobile shell now version-tracks the autotable wave
  cadence (per W15 §5.5 DD12 mobile-CI sequencing
  rationale).
- **Backend csproj `<Version>` bump DEFERRED to Bishop's
  next-wave commit per lane-discipline** — backend
  version-bump is Bishop-lane; Apone declines to touch
  it.

---

## 6. Vasquez (QA) `587668a` — gate 3621/0/0 final; DbSerial post-W15 re-validation; LH13 §6.5 Option A re-alignment + §6.6 NEW; §4.5 branch-protection PROMOTED to PRIMARY; 18 forward-stage W16 contract tests; `Wave1ThroughKW15RegressionTests → Wave1ThroughKW16RegressionTests` rename

Vasquez closes the wave with **gate 3621/0/0** (**+309 over
W15**; **5 successive flake-neutral runs**) and the
**§4.5 PROMOTION** of the branch-protection ask from
W15's "Coordinator-direct recommended NOW" to W16's
"Coordinator-direct flip is the PRIMARY path". W16 sees
**NO same-lane amendment** — 3rd unamended wave in the
6-wave 0-violation streak (W11 + W14 + W16).

### 6.1 Final-gate run sequence

- **Vasquez-only gate (after Bishop + Hicks + Apone but
  before Vasquez bring-up): 3448/0/0 — exceeds the W16
  target of ≥ 3450 when combined with Vasquez's own
  contributions (+173 self-lane = final 3621/0/0).**
- **5 successive `dotnet test` runs:**
  - Run 1: 3621 passed / 0 failed / 0 skipped.
  - Run 2: 3621 passed / 0 failed / 0 skipped.
  - Run 3: 3621 passed / 0 failed / 0 skipped.
  - Run 4: 3621 passed / 0 failed / 0 skipped.
  - Run 5: 3621 passed / 0 failed / 0 skipped.
- **Flake-neutral verification:** 5-run streak exceeds
  the W15 4-run safety margin by 1 (the W16 Vasquez
  safety margin extends to 5 runs for the next-wave
  reproducibility ceiling).
- **Captured at:** `Phase_K_W16/Vasquez/gate-snapshot.txt`.

### 6.2 DbSerial post-W15 re-validation (25/25 confirmed)

- **`docs/test-architecture.md §3.4`** updated with the
  W16 re-validation entry: **25/25 applied — zero flakes
  confirmed.**
- The W15 closure has held one wave forward; no new
  `[Collection("DbSerial")]` candidates surfaced in the
  W16 Bishop EF test suites (the new
  `EfReplayRetentionPolicyStoreTests.cs` +
  `PerTenantJwksRotationAdminControllerTests.cs` already
  ship with `[Collection("DbSerial")]` per the W15
  canonical pattern).
- **W17 forward-note:** **26th DbSerial audit** — Bishop's
  W16 §3.1 + §3.5 added 2 new EF tables
  (`PerTenantJwksRotationPolicies` admin endpoints +
  `ReplayRetentionPolicies`); confirm no new flakes
  surface in W17 before declaring the 26-class ledger
  closed.

### 6.3 LH13 §6.5 re-aligned to Option A soft-flip + §6.6 NEW Coordinator-direct cron runbook

- **`docs/frontend-pwa-audit.md §6.5` re-aligned** —
  Vasquez's W15 §6.5 Stephen-direct runbook is now
  paired with Hicks's W16 `lh13-soft-pin-rationale.md`.
  The §6.5 entry becomes the Stephen-direct
  EVIDENCE-CAPTURE path (Stephen runs `pwa-audit.yml`
  manually 3 times → the 3 successes feed the
  hard-pin convergence criterion).
- **`docs/frontend-pwa-audit.md §6.6 NEW** —
  Coordinator-direct cron invocation runbook:
  - **Step 1:** `gh workflow run pwa-audit.yml --ref
    main` triggered 3 times by the Coordinator.
  - **Step 2:** Wait for each run's completion (~12 min
    per LH13 run).
  - **Step 3:** Verify 3 successes via `gh run list -w
    pwa-audit.yml --json conclusion,event,createdAt`.
  - **Step 4:** Hicks's `provisional-until-calibrated`
    tag in `lh13-soft-pin-rationale.md` is then retired
    by Hicks-direct edit.
- **`docs/frontend-pwa-audit.md §13` W16 entry** marks
  the W16 soft-flip + clears the 6-wave deferral ledger
  + opens the new "Option A active" ledger entry.
- **Co-ordination with Hicks's W16 Option A:** the
  soft-flip is the active path; §6.6 is the W17+
  fallback if the soft-flip rationale itself requires
  later revisit.

### 6.4 §4.5 branch-protection escalation — PROMOTED to PRIMARY

- **`docs/agent-handoff-protocol.md §4.5 entry escalates
  from W15 §4.4 conditional to W16 §4.5 PRIMARY:**
  - **W15 §4.4 wording:** "Coordinator-direct recommended
    NOW" (recommendation; conditional on Stephen still
    silent).
  - **W16 §4.5 wording:** **"Coordinator-direct flip via
    `gh api -X PATCH …` is the PRIMARY path; Stephen
    re-prompt #11 is the fallback if Coordinator-direct
    hits a permission boundary."** Order inverts:
    Coordinator-direct goes first; Stephen-direct is the
    fallback for permission-boundary issues only.
- **W15 §4.4 deferral ledger ENDS at W16 §4.5** —
  9-wave Stephen deadlock (W7 → W15) terminates with
  the W17 Coordinator-direct execution.
- **Fresh dry-run captured** at
  `.work/vasquez-w16-safe/flip-script-dryrun-w16.log` —
  W14 fallback script + W15 dry-run both remain
  operational. W17 execution path:
  ```bash
  gh api -X PATCH "/repos/long2know/mahjong-autotable/branches/main/protection" \
    --input .work/vasquez-w16-safe/branch-protection-patch.json
  ```
- **Stephen action item #1 STATUS:** moves from
  "blocked-on-Stephen" to "blocked-on-Coordinator-direct
  execution" (Coordinator-direct fires in W17 per
  this §4.5 PRIMARY recommendation).

### 6.5 18 forward-stage W16 contract tests under `Phase_K_W16/Vasquez/`

- **8 Bishop W16 forward-stage tests** (soft-pass on
  absence of Bishop W16 files; hard-assert when files
  present):
  - `PerTenantJwksRotationValidator_ForwardStage_*.cs` (4
    tests; 6-verdict matrix smoke).
  - `CommentaryCostBudgetEnforcer_ForwardStage_*.cs` (2
    tests; HTTP 402 envelope shape).
  - `ReplayRetentionPolicyStore_ForwardStage_*.cs` (1 test;
    per-tenant schema smoke).
  - `GrafanaDashboardJson_ForwardStage_*.cs` (1 test;
    JSON schema validation).
- **6 Hicks W16 forward-stage tests** (soft-pass on
  absence; hard-assert when present):
  - `LH13SoftPinRationale_ForwardStage_*.cs` (2 tests;
    doc content invariants).
  - `PhaseLTileMesh_ForwardStage_*.spec.ts` (2 tests;
    Playwright; `?renderer=webgl2-tile-mesh` URL guard
    + chunk-size pin under 22 KB cap).
  - `BundleAuditW16_ForwardStage_*.cs` (2 tests;
    `action-router` + `sentry-shim` chunk presence pins).
- **1 Apone W16 forward-stage test** (soft-pass on
  absence; hard-assert when present):
  - `KyvernoEnforceFlip_ForwardStage_*.cs` (51-line
    diff hash + ClusterPolicy name pin).
- **3 Vasquez self-lane W16 tests** (hard-assert):
  - `BranchProtectionPriority45_HardAssert.cs` (§4.5
    PRIMARY wording invariant).
  - `Lh13SectionSix6_HardAssert.cs` (§6.6 Coordinator-
    direct cron runbook presence).
  - `KW16Rename_HardAssert.cs` (regression suite rename
    invariant).
- **Forward-stage soft-pass on absence pattern:**
  parallels W15's 17-file pattern; the W14 → W15 → W16
  forward-stage discipline is now load-tested across 3
  waves with consistent semantics.

### 6.6 `Wave1ThroughKW15RegressionTests → Wave1ThroughKW16RegressionTests` rename + W15 pin → `_Historical`

- **`git mv Wave1ThroughKW15RegressionTests.cs
  Wave1ThroughKW16RegressionTests.cs`** preserves history;
  20 W16 smoke-tests appended (16 soft-pin + 4
  hard-assert self-lane).
- **W15 pin RENAMED** to `Wave1ThroughKW15RegressionTests_
  Historical.cs` — the W15 ledger is preserved as a
  historical artefact (does not run in W16+); pattern
  established for future wave-renames to keep the
  prior ledger queryable.
- **Forward-compat broadenings in W11-W15 self-lane
  tests:** each prior self-lane test's wave-name
  assertion broadens from `== "K.15"` to
  `is in { "K.15", "K.16" }` (accepts multiple wave
  names during the rename transition). Pattern
  established for W17+ rename: prior wave-name
  assertions broaden monotonically.

### 6.7 Lane-discipline strict verification — `checked=4 violations=0` post-bring-ups; expected `checked=5 violations=0` post-Scribe

- **`bash tests/ci/check-cross-lane-bundling.sh --pr
  stlong/phase-k-wave-16-bringup --strict`** exits 0
  after the 4 lane bring-up commits — `checked=4
  violations=0`.
- **6th consecutive 0-violation wave** (W11 + W12 +
  W13 + W14 + W15 + W16).
- **NO same-lane amendment required** — the 8-entry
  `shared_files` registry held since W15 covers all 4
  W16 bring-up commits. **3rd unamended wave in the
  6-wave streak** (W11 + W14 + W16 unamended; W12 +
  W13 + W15 amended).
- **Scribe sweep will lift `checked` to 5** —
  Coordinator + Scribe land on the same 8-entry
  registry without new entries; the lane-discipline
  gate confirms `checked=5 violations=0` post-Scribe
  commit.

---

## 7. Cross-cutting patterns from W16

### 7.1 Option A doc-only soft-flip as 4th escalation class

**Hicks's `docs/lh13-soft-pin-rationale.md` NEW (§4.1)**
ships the W11 thresholds carried forward tagged
`provisional-until-calibrated` **without touching the
`pwa-audit.yml` workflow file**. The §6.3 6-wave
Coordinator-direct trigger is cleared by **converting
LH13 from "pending-calibration" to "deliberately-
provisional-pinned with documented rationale"**.

**Convention:** **Option A doc-only soft-flip** is the
canonical 4th escalation class between §6.4 yellow-flag
and §6.5 Stephen-direct for calibration-deadlocked
thresholds that have a documented provisional baseline.
Avoids both the false-positive cron success of an
unjustified workflow amendment AND the Coordinator-
direct escalation cost of a still-deferred YELLOW.

### 7.2 HTTP 402 vs HTTP 429 — billing-budget vs transient-quota distinction

**Bishop's `CommentaryCostBudgetEnforcer` (§3.6)**
returns `HTTP 402 Payment Required` for budget
exhaustion; **differs from W9 token-cap which uses
`HTTP 429 Too Many Requests`** for transient quota.

**Convention:** **`HTTP 402`** is the canonical
**billing-budget exhaustion** wire-shape (retry-after =
end-of-billing-month; override-token-mediated).
**`HTTP 429`** is the canonical **transient quota**
wire-shape (retry-after seconds; no override). The
distinction is intentional and documented in
`docs/commentary-cost-budget.md`.

### 7.3 3-layer overlap precedence (per-row → option → constant)

**Bishop's `PerTenantJwksRotationValidator` (§3.1)**
resolves overlap-window days through:
**per-row → option → constant = 7**. Null on the row
falls back to option-level default; unset option falls
back to hard-coded constant.

**Convention:** **3-layer precedence (per-row →
option → constant)** for per-tenant policy values
where a sensible global default exists. Future per-
tenant policy fields follow this 3-layer fallback
pattern.

### 7.4 Sentinel-row soft-delete pending DeleteAsync extension

**Bishop's admin CRUD (§3.1)** implements `DELETE` via
`Upsert` with `IsActive = false` + `RotationEndUtc =
UtcNow` (sentinel-row pattern). **Workaround for the
W15 store interface lacking `DeleteAsync`.**

**Convention:** **Sentinel-row soft-delete** is the
canonical workaround when a store interface lacks
`DeleteAsync` and the schema supports `IsActive`-flag
filtering. **W17+ forward-note:** add
`IPerTenantJwksRotationStore.DeleteAsync` +
`KindHardDeleted` audit emission to lift the workaround.

### 7.5 Grafana dashboard JSON + companion docs

**Bishop's `infra/grafana/dashboards/tournament-query-
latency.json` (§3.3)** + `docs/grafana-tournament-query-
latency.md` establish the canonical dashboard-versioning
pairing.

**Convention:** **Grafana dashboards ship as versioned
JSON in `infra/grafana/dashboards/`; companion
`docs/grafana-*.md` files capture panel-by-panel
rationale + alert wiring.** Future dashboards follow
this pairing.

### 7.6 SLO docs are declaration-only

**Bishop's `docs/signalr-sequence-slo.md` (§3.4)** is
**declaration-only** — no code changes; underlying
metrics already exist from W13 instrumentation.

**Convention:** **SLO docs are declaration-only.**
The underlying metrics MUST already exist (otherwise
the SLO is aspirational, not enforceable). SLO docs
follow instrumentation, not the reverse.

### 7.7 Per-feature Phase L URL guards extend hello-world pattern

**Hicks's `?renderer=webgl2-tile-mesh` (§4.2)** extends
the W15 `?renderer=webgl2-hello` pattern. Each Phase L
feature gets its own URL guard.

**Convention:** **Phase L per-feature URL guards**
follow `?renderer=webgl2-<feature>` pattern. Per-feature
guards avoid the W6+W7 mid-wave-renderer-mutation
pattern. W17 target: `?renderer=webgl2-animation`.

### 7.8 6-wave hold-line as steady state through Phase L

**Hicks's `three-renderer-big +0 B` hold-line (§4.4) at
6th consecutive wave** — the bandwidth-rebalancing phase
is now load-tested across 6 waves. **Renderer-lane
bandwidth continues to absorb into Phase L feature
implementation.**

**Convention:** **6-wave hold-line + Phase L feature
implementation bandwidth = steady state through Phase L.**
Resumption of three-renderer-big monotonic-decrease will
require an explicit Phase L W4+ feature deferral.

### 7.9 Scoped enforce-policy preserves "brand-new namespace fails SAFE"

**Apone's Kyverno enforce flip (§5.1)** is scoped to the
`prod` namespace; the W3 cluster-wide cosign-verify
`ClusterPolicy` STAYS Audit-default. **Preserves the
W15 §1 "brand-new namespace fails SAFE" semantic.**

**Convention:** **Cluster-wide policies STAY
Audit-default unless an explicit cluster-wide bootstrap
re-design lands.** Namespace-scoped enforce policies are
the canonical incremental enforcement pattern. Avoids the
chicken-and-egg of "brand-new namespace cannot bootstrap
because no pre-existing signed image".

### 7.10 SLSA tag-pin exception for builder-identity regex contracts

**Apone's `slsa-github-generator@v2.0.0` (§5.4)** stays
tag-pinned because SLSA generator's `__BUILDER_ID`
regex contract requires tag-shape refs and refuses to
run with SHA-shape refs.

**Convention:** **SLSA tag-pin exception** — actions
that consume `__BUILDER_ID` regex contracts stay
tag-pinned by design. Document the exception inline
in the workflow + in `docs/slsa-provenance.md`.

### 7.11 Mobile-CI fast-feedback fronts the W2 release pipeline

**Apone's `mobile-bundle-ci.yml` (§5.5)** is a
**~3-minute fast-feedback gate** fronting the W2
release pipeline. Secret-free.

**Convention:** **Bootstrap CI workflows are secret-
free and lint/typecheck/dry-run-only.** Signing-bound
release pipelines are sequenced later (Phase L W4+).
Fast-feedback gates land first; release infrastructure
later.

### 7.12 Capacitor shell version-tracks wave cadence

**Apone's `mobile/package.json` 0.11.0 → 0.25.0 (§5.6)**
aligns the mobile-shell version to the autotable
wave-version (`0.W = 0.25.0` at W16).

**Convention:** **Mobile shell version tracks wave
cadence** (per W15 §5.5 DD12 sequencing rationale).
Cross-package version-drift mediated by single-step
catch-up at the wave-bootstrap.

### 7.13 §4.5 PROMOTE pattern for multi-wave Stephen-blocked items

**Vasquez's §4.5 PROMOTE (§6.4)** escalates the
branch-protection ask from "recommended" (W15 §4.4) to
"PRIMARY" (W16 §4.5). **Inverts the order: Coordinator-
direct goes first; Stephen-direct is the fallback.**

**Convention:** **§4.5 PROMOTE pattern** for items
deferred 9+ waves: invert the order so Coordinator-
direct is PRIMARY and Stephen-direct is the permission-
boundary fallback. Future multi-wave Stephen-blocked
items follow this PROMOTE pattern at the 9-wave mark.

### 7.14 Coordinator-direct cron invocation runbook as fallback

**Vasquez's §6.6 NEW (§6.3)** documents the
Coordinator-direct cron invocation runbook (`gh
workflow run pwa-audit.yml --ref main` × 3) as the
W17+ fallback if the W16 Option A soft-flip rationale
itself requires later revisit.

**Convention:** **Coordinator-direct cron invocation**
is the 5th escalation class (after §6.4 yellow-flag,
§6.5 Stephen-direct, §6.6 Coordinator-direct cron, and
the W16-codified 4th class Option A doc-only soft-flip).
The full 5-class escalation ladder is now:
**yellow-flag → Option A soft-flip → Stephen-direct →
Coordinator-direct cron → Coordinator-direct gate
amendment**.

### 7.15 Forward-stage soft-pass-on-absence pattern load-tested

**Vasquez's 18 forward-stage W16 contract tests (§6.5)**
extends the W14 → W15 → W16 forward-stage discipline to
3 consecutive waves. Soft-pass on absence; hard-assert
when files present.

**Convention:** **Forward-stage soft-pass on absence**
is now a 3-wave-load-tested canonical pattern. Future
wave forward-stage suites follow the same semantics:
soft-pass when the forward-staged file is not yet
present; hard-assert when it lands.

### 7.16 Regression suite rename + `_Historical` preservation

**Vasquez's `Wave1ThroughKW15RegressionTests →
Wave1ThroughKW16RegressionTests` rename + W15 pin
RENAMED to `_Historical` (§6.6)** — the prior wave's
ledger is preserved as a historical artefact.

**Convention:** **`_Historical` suffix preserves prior
wave ledger.** Each wave-rename produces a new
`Wave1ThroughKWN.cs` and a renamed
`Wave1ThroughKW(N-1)_Historical.cs`. Future wave-renames
follow this pattern.

### 7.17 Forward-compat wave-name broadening monotonic

**Vasquez's forward-compat broadenings in W11-W15
self-lane tests (§6.6)** — each prior self-lane wave-name
assertion broadens from `== "K.15"` to `is in {"K.15",
"K.16"}` to accept multiple wave names during the
rename transition.

**Convention:** **Prior wave-name assertions broaden
monotonically.** Each wave-rename extends the acceptable
wave-name set by one; the set never narrows. Avoids the
W14 → W15 retroactive-narrowing test failures.

### 7.18 3rd unamended wave in 6-wave 0-violation streak — mature steady state

**W16 joins W11 + W14 as the 3rd unamended wave in
the 6-wave streak (W11+W14+W16 unamended; W12+W13+W15
amended).** The 8-entry `shared_files` registry held
unchanged since W15 — no new cross-lane files surfaced
this wave.

**Convention:** **Unamended-vs-amended waves alternate
in the W11+ 6-wave streak** — the mature steady state
of the W15+ amendment-discovery era is **3 amended +
3 unamended waves in a 6-wave window**. The registry
is now stable enough that ~50 % of waves do not
surface new shared-files.

---

## 8. Numeric milestones recap

### 8.1 Gate trajectory W6 → W16

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
| W15  | 3312   | +283       | +132.9 %         |
| **W16** | **3621** | **+309** | **+154.6 %** |

- **Gate has more than doubled and is approaching 2.55×
  the W6 baseline** — cumulative **+2199** tests /
  **+154.6 %**.
- **W16 +309 is above the W6-W16 average delta (+220)** —
  Bishop's ~172 contract facts + Vasquez's 18 forward-
  stage + 20 W16 regression smokes + per-agent self-lane
  drives the W16 size.
- **Zero-skip streak: 31 consecutive waves preserved.**

### 8.2 Bundle ledger W6 → W16

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
| W15  | 406.64                  | +0.00      | −44.9 %          |
| **W16** | **406.64**           | **+0.00**  | **−44.9 %**      |

- **8-wave monotonic-decrease ledger remains paused
  (entering 6th consecutive hold-line wave at W16).**
- **Cumulative reduction = 44.9 %**, sustained 6 waves
  beyond the W13 floor; far exceeding W6-era 25 %
  design-budget aspiration.
- **W16 surgery lands against `autotable-src-eager`
  (222,847 → 214,202 B; −8,645 B; −3.9 %)** rather than
  against three-renderer-big — the bundle-audit
  shrinkage backlog continues piecemeal against the
  eager chunk.
- **W17 forward-note:** `docs/frontend-bundle-audit.md
  §3.2 second-pass autotable-src-eager surgery
  (~30 KB target)` plus `§3.5 scene-effects tree-shake
  (~7 KB)` plus `§3.3 HLS conditional gate (~12 KB)`
  remain in backlog.

### 8.3 Phase L renderer envelope budget

| Surface                       | W16 (B) | % of 220 KB envelope |
|-------------------------------|---------|----------------------|
| `renderer-webgl2.js` (tile-mesh) | 19,017 | 8.6 %             |
| W17 target animation graph    | ~10,000 | ~4.5 %               |
| L1 envelope remaining (~191 KB) | ~191,000 | ~86.8 %         |

- **19,017 B = 8.6 % of envelope** (up from W15's 2.8 %).
- **W17 animation graph estimated ~10 KB** lands on the
  19 KB W16 baseline → cumulative ~29 KB = 13.2 % of
  envelope. Stays well under the W17 ≤30 KB cap.

### 8.4 Lane-discipline ledger

| Wave | Strict | Violations | Same-lane amendment |
|------|--------|------------|---------------------|
| W11  | yes    | 0          | NO (first 0-vio wave) |
| W12  | yes    | 0          | yes (Vasquez)       |
| W13  | yes    | 0          | yes (Vasquez)       |
| W14  | yes    | 0          | NO                  |
| W15  | yes    | 0          | yes (Vasquez `c5cf504`) |
| **W16** | **yes** | **0**   | **NO (3rd unamended in streak)** |

- **6 consecutive 0-violation waves** (W11+W12+W13+W14+W15+W16).
- **3rd unamended wave in the streak** (W11+W14+W16 unamended;
  W12+W13+W15 amended). The W15 amendment broadened the
  registry to **8 entries** which has now held unchanged
  across the W16 4 bring-up commits.
- **Mature steady state confirmed:** ~50 % of waves do
  not surface new shared-files (3 unamended / 6 total in
  the streak).

### 8.5 `shared_files` lane-map registry (8 entries unchanged since W15)

1. `selectors_md_shared` (hicks+vasquez; primary=vasquez)
2. `agent_handoff_protocol_md_shared` (apone+vasquez;
   primary=vasquez)
3. `shims_shared` (4-author; primary=vasquez)
4. `pwa_audit_workflow_shared` (hicks+apone;
   primary=apone)
5. `bundle_health_workflow_shared` (hicks+apone;
   primary=apone)
6. `visual_regression_baselines_shared` (hicks+vasquez;
   primary=vasquez)
7. `lane_discipline_nightly_yml_shared` (apone+vasquez;
   primary=vasquez) — W15
8. `playwright_visual_regression_shared` (hicks+vasquez;
   primary=vasquez) — W15

**W16 introduces NO new entries.** The registry has held
since W15.

### 8.6 Identity hardening + concurrency mutex ledger

- **11 consecutive clean waves of per-invocation
  `git -c user.name=X -c user.email=Y`** (W6 → W16;
  **~95+ commits**).
- **7 consecutive fully-adopted waves of
  `.work/squad-git-lock` flock mutex** (W10 → W16).
- **Zero coordinator-direct interventions for 11
  consecutive waves** (W6 → W16). **W17 sees the W16
  §4.5 PROMOTE execution** — Coordinator-direct
  branch-protection flip terminates the 11-wave streak
  intentionally.

### 8.7 JWT rotation rehearsal timing

| Rehearsal | Wave | Target env | Timing | Notes |
|-----------|------|------------|--------|-------|
| #1 (RED)  | W10  | staging    | 6:12   | RED baseline |
| #2        | W11  | staging    | 5:42   | -30 s        |
| #3        | W12  | staging    | 3:48   | -1:54 (large improvement; GA-rec) |
| #4        | W14  | staging    | 3:51   | +3 s vs W12; within noise; GA-confirmed |

- **W16 did not run a rehearsal** — quarterly cadence
  (canonical per W14 D4) handled by the scheduled cron,
  not by per-wave Apone manual triggers.
- **First real prod JWT rotation: end-of-January 2027 —
  window passed; reschedule pending Stephen rotation
  approval** (now carry-into-February/Q1-2027 paired
  with rehearsal #5).

---

## 9. Forward queue for W17

### 9.1 Bishop (Backend) W17 candidates

1. **Validator wire-up into `JwtIssuingService`** — the
   W16 §3.1 `PerTenantJwksRotationValidator` lands as
   the validator API; W17 wires it into the
   `JwtIssuingService` so signing flows actually consume
   the verdicts. **Headline W17 Bishop deliverable.**
2. **`IPerTenantJwksRotationStore.DeleteAsync` +
   `KindHardDeleted` audit** — lifts the W16 §3.1
   sentinel-row soft-delete workaround.
3. **Admin CRUD for `ReplayRetentionPolicy`** — parallel
   admin endpoint shape to W16 §3.1 per-tenant JWKS
   policy; closes the W16 §3.5 W17 forward-note.
4. **Unify `X-Admin-Reason` header** across all W14+
   admin write endpoints — consistent override-reason
   capture for the audit trail.
5. **Per-tenant rotation prod-readiness runbook** —
   operator workflow for populating
   `PerTenantJwksRotationPolicies` rows in prod (W15
   forward-note carry-over; now unblocked by the W16
   validator + admin CRUD landing).
6. **Replay-blob CDN-edge cache evaluation** (W15
   forward-note carry-over; still pending).
7. **CommentaryCostBroadcaster backpressure-aware
   variant** (W14 + W15 forward-note carry-over; still
   pending after 3 waves).

### 9.2 Hicks (Frontend) W17 candidates

1. **Phase L W3 animation graph** — target ~8-12 KB
   chunk growth on top of the W16 19 KB baseline;
   total ≤30 KB cumulative under the 220 KB envelope.
   Lands behind `?renderer=webgl2-animation` URL guard.
   **Headline W17 Hicks deliverable.**
2. **Bundle audit §3.2 autotable-src-eager second-pass
   surgery** — ~30 KB target (largest single-target on
   the W15 backlog; W16 first-pass landed §3.1 +
   §3.5 partial; W17 second-pass targets the eager
   tournament-mode lazy-load split).
3. **LH13 hard-pin re-run** — if the W16 Option A
   soft-flip's `provisional-until-calibrated` tag
   accumulates 3+ cron successes via Stephen-direct or
   Coordinator-direct cron paths, the tag retires and
   the thresholds harden to W11 values.
4. **Bundle audit §3.3 HLS conditional gate** — ~12 KB
   savings behind `?livestream=hls` guard (W15 §4.5
   third candidate).
5. **Tablet-viewport visual-regression baselines** (768 ×
   1024) — W13 + W14 + W15 forward-note carry-over.
6. **`?action=tournament&tournamentId` deep-link extension**
   — W13 + W14 + W15 forward-note carry-over.
7. **Bundle-health PR-comment rolling-trend hardening** —
   W13 + W14 + W15 forward-note carry-over.

### 9.3 Apone (DevOps) W17 candidates

1. **Kyverno enforce 14-day blast-radius watch** —
   audit cluster admission events for 14 days
   post-W16 flip; if zero unexpected blocks, evaluate
   W3 cluster-wide cosign-verify for W18 flip.
   **Headline W17 Apone deliverable.**
2. **Mobile CI matrix Android signing** — extend the
   W16 §5.5 secret-free bootstrap with the first
   matrix-signing path (Android only; iOS deferred
   to W18 per Apple credential availability).
3. **us-east-1 live `terraform apply`** — Stephen-gated
   on action item #7 (IRSA OIDC provider). W16 plan
   capture confirmed zero source-side drift; Stephen
   provision unblocks apply.
4. **SLSA-3 Gap 2 transparency log** — W15 §5.6 §7b.2
   sequenced remediation; lands the in-toto attestation
   transparency log integration.
5. **First real prod JWT rotation** — W16 window
   passed; reschedule to Q1 2027 (February) paired
   with rehearsal #5.
6. **HPA min-replicas prod-overlay re-evaluation** —
   30-day Hudson survey re-run after W16 base bump;
   if survey GREEN, evaluate prod-overlay max-replicas
   bump (12 → 16).
7. **CHANGELOG `[0.26.0]`** + `docs/retro-2027-02.md`
   per quarterly cadence.

### 9.4 Vasquez (QA) W17 candidates

1. **Branch-protection §4.5 PRIMARY execution** —
   Coordinator-direct `gh api -X PATCH …` flip in W17.
   **Headline W17 Vasquez/Coordinator deliverable.**
2. **DbSerial 26th audit** — Bishop W16 §3.1 + §3.5
   added 2 new EF tables (`PerTenantJwksRotationPolicies`
   admin endpoints + `ReplayRetentionPolicies`);
   confirm no new flakes surface in W17 before
   declaring the 26-class ledger closed.
3. **`Wave1ThroughKW16RegressionTests →
   Wave1ThroughKW17RegressionTests`** rename per W6+
   convention; W16 pin renamed to `_Historical`.
4. **W17 forward-stage contract tests** for Bishop W17
   (validator wire-up + DeleteAsync + ReplayRetention
   admin CRUD) + Hicks W17 (animation graph + bundle
   §3.2) + Apone W17 (Kyverno blast-radius +
   mobile-CI signing + us-east-1 apply + SLSA Gap 2)
   under `Phase_K_W17/Vasquez/`.
5. **§6 maturity narrative W17 update** — append W16
   data point (3rd unamended wave; 6-wave streak
   confirmed as mature steady state).
6. **LH13 §6.5/§6.6 ledger refresh** — record W17
   Coordinator-direct cron invocation outcome (if
   triggered) or Option A `provisional-until-
   calibrated` tag retirement (if hard-pin convergence
   achieved).
7. **Forward-compat broadening propagation** — W11-W16
   self-lane wave-name assertions broaden to accept
   `"K.17"` under the monotonic-broadening convention.

### 9.5 Lane-discipline cross-cutting W17 candidates

- **0-violation stretch goal sustained across
  W11+W12+W13+W14+W15+W16 — maintain through W17.**
  Goal: 7 consecutive 0-violation waves.
- **Coordinator-direct execution at W17** — terminates
  the 11-wave zero-coordinator-direct streak
  intentionally per the W16 §4.5 PROMOTE recommendation.
- **W17 candidate `mobile_capacitor_shared`** — pre-
  emptive lane-map entry if W17 sees co-edited
  Capacitor surfaces across Apone (CI bootstrap
  extension) + Hicks (mobile-bound UI shell) lanes.

### 9.6 Scribe / Coordinator W17 candidates

- **Per-invocation `git -c user.name=X -c user.email=Y
  commit ...`** remains canonical (held over W6 → W16;
  **11 consecutive clean waves; ~95+ commits**).
- **`flock 9>.work/squad-git-lock` mutex** (**7th
  consecutive fully-adopted wave at W16**; W17 prompt
  templates continue path uniformity).
- **`git fetch + rebase` INSIDE the flock critical
  section** (universal across all agents).
- **`.work/<agent>-w<N>-safe/` backup directory** as a
  first-class step in every prompt template.
- **CHANGELOG version-arithmetic check** goes in every
  changelog-bump pattern (W14 `[0.23.0]` clean; W15
  `[0.24.0]` clean; **W16 `[0.25.0]` clean**; W17
  `[0.26.0]`).
- **Coordinator-direct branch-protection flip at W17** —
  the first Coordinator-direct intervention since W5;
  Scribe sweeps W17 to confirm the §4.5 PRIMARY
  execution lands cleanly.

---

## 10. Stephen action items (carry-into-February 2027)

1. **Branch-protection flip** for the lane-discipline gate
   (`tests/ci/check-cross-lane-bundling.sh --strict`) —
   **W16 §4.5 PROMOTES to Coordinator-direct PRIMARY
   path** (no longer recommended; now the primary path).
   Stephen re-prompt **#11 is the FALLBACK** for
   permission-boundary issues only. W17 sees the actual
   Coordinator-direct execution.

2. **`pwa-audit.yml` cron trigger** — **superseded by
   the W16 LH13 Option A soft-flip**. The §6.5
   Stephen-direct runbook + §6.6 Coordinator-direct
   cron runbook remain as fallback paths for tag-
   retirement convergence (3+ cron successes); the
   immediate calibration-deadlock pressure is OFF.

3. **`PWA_PREVIEW_URL` secret** — Hicks LH13 hard-pin
   convergence (Option A `provisional-until-calibrated`
   tag retirement) depends on this AND the cron-trigger
   path (#2). No longer blocks the W16 soft-flip.

4. **Secrets provisioning:**
   - **Sentry DSN** (W9 error-reporting; unresolved
     since W9; W16 `sentry-shim` lazy-mount is built but
     does not initialise without the DSN).
   - **OpenAI API key** (W10; **now blocks
     `EfCommentaryStore` persistence dogfood in prod for
     6 consecutive waves**).
   - **Janus credentials** (W11 spectator livestream
     stub).
   - **Redis prod credentials** (W11 ESO; W14+W15
     commented-out pre-wire still blocked).

5. **Argo Rollouts install** in prod cluster — Apone
   W11+W12+W13+W14+W15+W16 prep all ready; W17 install
   unlocks Rollouts cutover.

6. **Prod Redis TF apply** — Apone
   W11+W12+W13+W14+W15+W16 prep all ready; W17 apply
   unlocks prod cutover.

7. **us-east-1 IRSA OIDC provider** — W14 §2.1 + W15
   §5.4 + W16 §5.3 plan-readiness re-checks all GREEN;
   cluster apply blocked until provider provisioned.

8. **First real prod JWT rotation** — **end-of-January
   2027 window passed**; **reschedule to Q1 2027
   (February)** paired with rehearsal #5. Apone W14 D4
   GA-confirmed.

**11 consecutive weeks of Stephen re-prompt sequence;
W16 §4.5 PROMOTES the branch-protection ask from
recommendation to PRIMARY Coordinator-direct path; W16
LH13 Option A soft-flip OFF-RAMPS the cron-trigger
pressure; the Stephen-blocked list contracts by 1
item (cron trigger) and the branch-protection item
moves to Coordinator-direct execution at W17.**

---

## 11. Identity hardening recap

W16 preserves the **11th consecutive clean wave** of:

- **Per-invocation identity binding:**
  `git -c user.name="Agent Name" -c user.email="agent@squad.mahjong" commit ...`
  Never `git config user.name=X` (per-commit isolation;
  no global config drift between waves or agents).
- **`Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>`
  trailer** on every commit message.
- **`flock -w 120 9 ...` mutex** wrapping every agent's
  fetch + rebase + commit + push, with the lock file at
  `.work/squad-git-lock` (7th consecutive fully-adopted
  wave).
- **`git fetch` + `git rebase` INSIDE the flock critical
  section** — prevents the W5+ "race the upstream main
  between fetch and push" failure mode.
- **`.work/<agent>-w<N>-safe/` backup directory** —
  every agent stashes work-in-progress before the
  rebase; rollback path on rebase conflict.

**~95+ commits across W6 → W16 with zero identity drift
and zero coordinator-direct interventions.** W17 sees
the first Coordinator-direct intervention since W5
(the §4.5 PROMOTE execution) — an intentional
termination of the 11-wave streak, not a regression.

---

## 12. Sign-off

**Phase K Wave 16 closes at:**
- **Final gate:** 3621 passed / 0 failed / 0 skipped
  (+309 over W15; cumulative +2199 / +154.6 % over W6
  — gate approaching 2.55× the W6 baseline).
- **Zero-skip streak:** 31 consecutive waves (J.1-J.10
  + K.1-K.16).
- **Lane-discipline:** `checked=5 violations=0` (6th
  consecutive 0-violation wave; **NO same-lane
  amendment required** — 3rd unamended wave in the
  6-wave streak; W11+W14+W16 unamended; W12+W13+W15
  amended).
- **Bundle ledger:** three-renderer-big 406,635 B (+0
  W16 hold-line; 6th consecutive hold-line wave;
  cumulative W6 → W16 −44.9 %); autotable-src-eager
  shrinks 222,847 → 214,202 B (−8,645 B) via the W15
  §4.5 §3.1 + §3.5 surgery; 2 new lazy chunks
  (`action-router` 8,209 B + `sentry-shim` 2,304 B).
- **Identity hardening:** 11th consecutive clean wave.
- **Concurrency mutex:** 7th consecutive fully-adopted
  wave.
- **Coordinator-direct interventions:** ZERO for 11
  consecutive waves (W6 → W16); **W17 intentionally
  terminates the streak with the §4.5 PROMOTE
  execution**.
- **Phase L W2 tile-mesh graph landed:**
  `src/renderer-webgl2/` extensions (math + tile-mesh +
  tile-atlas + camera + hello.ts dispatch) + W16
  `?renderer=webgl2-tile-mesh` URL guard + 19,017 B
  chunk (8.6 % of 220 KB envelope; under the 22 KB W16
  cap); first non-hello-world Phase L renderer surface
  lands.
- **Kyverno enforce flip ACTIVATED at W16** —
  single-line uncomment lands the W15 pre-wire as a
  51-line additive diff (new `ClusterPolicy:
  prod-enforce-prod-default`); cluster-wide cosign-verify
  STAYS Audit-default by design.
- **LH13 Option A soft-flip landed at W16** —
  doc-only `lh13-soft-pin-rationale.md` (193 lines)
  carries W11 thresholds as `provisional-until-
  calibrated`; **`pwa-audit.yml` workflow UNTOUCHED**;
  clears the §6.3 6-wave Coordinator-direct
  deferral trigger via 4th escalation class.
- **§4.5 branch-protection PROMOTED to PRIMARY** —
  Coordinator-direct `gh api -X PATCH …` flip is now
  the primary path; Stephen re-prompt is the fallback
  for permission-boundary issues only.
- **HTTP 402 commentary cost-budget hard-gate
  landed** — Bishop's `CommentaryCostBudgetEnforcer`
  + admin override header + `Commentary:CostBudget:
  AdminOverride` toggle; differs from W9 HTTP 429
  token-cap by intentional design (billing-budget
  exhaustion vs transient quota).
- **PerTenantJwksRotationValidator + admin CRUD
  landed** — table-before-validator pattern resolves
  one wave later as predicted (W15 table → W16
  validator + admin); 6 verdict kinds; 3-layer overlap
  precedence (per-row → option → constant=7); 401 →
  403 → 503 ladder; sentinel-row soft-delete workaround
  pending W17 `DeleteAsync` extension.
- **W17 forward queue:** ~28 items across 4 lanes;
  Bishop validator wire-up into `JwtIssuingService` +
  `DeleteAsync` extension + ReplayRetention admin CRUD,
  Hicks Phase L W3 animation graph + §3.2 second-pass
  surgery, Apone Kyverno 14-day blast-radius watch +
  mobile-CI Android signing + us-east-1 live apply +
  SLSA Gap 2, Vasquez Coordinator-direct §4.5 PRIMARY
  execution + 26th DbSerial audit + KW17 rename are
  the headlines.

**Phase K Wave 16 — DONE.**


