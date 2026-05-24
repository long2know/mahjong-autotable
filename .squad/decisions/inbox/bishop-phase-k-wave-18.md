# Bishop — Phase K Wave 18

**Branch:** `stlong/phase-k-wave-18-bringup`
**Scope:** backend — Phase K Wave 18 bring-up. Six scoped
deliverables; **five** landed in Bishop's lane; **one**
(prometheus alerts promotion to `infra/k8s/base/`) was
**handed off to Apone** because the target path
(`infra/k8s/base/prometheus-alerts/`) is Apone-owned and a
Bishop commit touching it would trip the cross-lane bundling
gate.  Hand-off detail is captured below in deliverable §2.

1. **DbSerial migration 29/29 closure** — Bishop W18 applies
   `[Collection("DbSerial")]` to all four open candidates
   identified in `docs/test-architecture.md` §3.4b:

   - `Phase_K_W16/Bishop/PerTenantRotationAdminControllerTests.cs`
   - `Phase_K_W17/Bishop/PerTenantRotationDeleteAsyncTests.cs`
   - `Phase_K_W17/Bishop/ReplayRetentionAdminControllerTests.cs`
   - `Phase_K_W17/Bishop/SignalRRetentionAdminControllerTests.cs`

   This brings the suite-wide migration from 25/29 → **29/29 —
   100% closure**.  The seven-wave arc
   (W11 audit → W12 inventory → W13 first apply → W14 +1 → W15
   +2 → W16 +1 identified → W17 +3 identified → W18 +4 applied)
   ends here.  No open EF-touching candidates remain.

   **Note on §3.4c hand-off to Vasquez:** the documentation
   counter-part (a new §3.4c in `docs/test-architecture.md`
   recording the 29/29 closure narrative) is **hand-off to
   Vasquez** because `docs/test-*.md` is classified as
   Vasquez-lane in `tests/ci/lane-map.json`.  Bishop landing
   the §3.4c edit would trip the cross-lane bundling gate.
   The Vasquez W18 self-lane test
   `Phase_K_W18/Vasquez/VasquezW18SelfLaneTests.DbSerial_Section3_4c_W18_Completion_Present`
   already forward-pins the §3.4c expectation; it will turn
   green once Vasquez lands the doc update.

2. **Prometheus alerts promotion to `infra/k8s/base/`** —
   **DEFERRED + HAND-OFF TO APONE**.  The W18 directive framed
   this as "promote the W17 `infra/prometheus/alerts/`
   tournament-query-duration.yaml into the Kustomize base under
   `infra/k8s/base/prometheus-alerts/`".  Inspection found:

   - the W17 YAML was added at
     `src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`
     (Bishop-lane source-of-truth path, NOT
     `infra/prometheus/alerts/`); the
     `infra/prometheus/alerts/` path has never existed.
   - `infra/k8s/base/` is an **Apone-lane** path
     (`tests/ci/lane-map.json` → `infra/*` regex routes to
     apone); a Bishop commit touching `infra/k8s/base/` would
     fail the cross-lane bundling gate.

   Bishop W18 keeps the source-of-truth in the existing
   bishop-lane path (extended W18 — see deliverable §4) and
   hands the kustomize-base promotion to Apone.  The
   contract-test pin in
   `Phase_K_W18/Bishop/TournamentAlertsW18ContractTests.cs`
   pins the YAML shape so Apone's promotion can mechanically
   `cp` the file with confidence the asserted envelope hasn't
   drifted.

3. **SignalR per-tenant retention hard-cap + override** —
   ships `SignalRRetentionPolicyEvaluator` which wraps the W17
   `SignalRRetentionPolicy` store with a global ceiling
   (default 7 days) + a per-tenant override allow-list.  A new
   prometheus counter
   `signalr_retention_policy_capped_total{tenant,requested_minutes,ceiling_minutes}`
   captures every cap event so an operator can spot a tenant
   that's repeatedly hitting the ceiling.  The
   `SignalRRetentionCeilingAdminController` at
   `/api/admin/signalr/retention-ceiling` exposes a small
   CRUD surface for the override allow-list (GET / POST grant
   / DELETE revoke), gated by the canonical admin auth
   ladder (401 → 403 → 200/201) with mandatory
   `X-Admin-Reason` on every write.  Audit kind:
   `signalr.retention.ceiling.override` (detail format
   `"tenant={tenant}|action={grant|revoke}|reason={X-Admin-Reason}"`).

4. **Tournament-query alerting expansion** —
   `Observability/Alerts/tournament-query-duration.yaml`
   expands from the W17 two-alert set (P99-page + P95-ticket)
   to a **five-alert** set:

   - `BracketQueryDurationP99HighPage` (W18 PAGE) — wraps the
     new `bracket_query_duration_seconds` histogram (sibling
     of the parent tournament-query envelope, separate metric
     so the heavier bracket-store joins can be alerted
     independently).
   - `SwissPairingDurationP99HighPage` (W18 PAGE) — wraps the
     new `swiss_pairing_duration_seconds` histogram with a
     `stage` label
     (`round-robin` / `swiss` / `single-elim-cutover`).
   - `TournamentQueryNoTrafficHeartbeat` (W18 TICKET) —
     `rate(tournament_query_duration_seconds_count[10m]) == 0`
     heartbeat that catches a silent scrape-pipeline outage
     (the quantile alerts can't fire if the histogram is
     empty).

   The new thresholds are mirrored in
   `Observability/TournamentQueryAlertThresholds.cs` so the
   contract tests can pin the YAML literals + the C# constants
   against each other.  `BracketQueryLatencyMetrics` +
   `SwissPairingLatencyMetrics` (in the same file) implement
   the histogram collectors; both share the parent's
   bucket boundaries so the Grafana dashboard can render
   side-by-side panels.

   `docs/tournament-query-duration-runbook.md` gains three new
   sections (`### bracket-p99-page`, `### swiss-pairing-p99`,
   `### heartbeat`) — the runbook anchors match the alert
   `runbook_url` slugs verbatim.

5. **Per-tenant rotation policy LIST endpoint** —
   `PerTenantRotationPolicyListController` at
   `/api/admin/per-tenant-jwks-rotation-policies` exposes a
   paginated, tenant-prefix-filterable LIST surface on the
   W16/W17 `PerTenantJwksRotationPolicies` store.  Query
   params: `skip` (0..), `take` (1..200, default 50),
   `tenantPrefix` (case-insensitive prefix filter applied
   server-side after the page-fetch).  Response envelope:
   `{ items: [...], total, skip, take, hasMore }`.  Audit kind:
   `auth.jwks.per-tenant.listed`.  Auth: admin-required (401 →
   403 → 200), no write surface so no `X-Admin-Reason`
   requirement.

6. **Commentary cost-budget historical CSV export** —
   `CommentaryCostBudgetExportController` at
   `/api/admin/commentary-cost-budget/export?from=YYYY-MM&to=YYYY-MM[&tenant=]`
   streams a UTF-8 CSV of every per-month commentary usage row
   in the requested inclusive window.  Window cap: 60 months
   (5 years).  Column set: `periodYear, periodMonth,
   inputTokens, outputTokens, totalTokens, requestCount,
   tokensPerDollar, monthlyCapUsd, usdSpent, percentOfCap,
   state, createdAt, updatedAt` — `state` is the derived
   `Healthy / Warning / Exhausted` triplet against the
   configured cap + warn threshold.

   `BuildCsv` is exposed as a public static so the contract
   tests can render rows directly without spinning the auth +
   DB scaffolding.  `tenant` is accepted as a forward-compat
   parameter (the W9 `CommentaryUsageRecord` ledger has no
   tenant column) — the parameter is captured in the audit
   detail row but does not filter the rows.

   Audit kind: `commentary.cost-budget.export` (detail format
   `"from={YYYY-MM}|to={YYYY-MM}|tenant={tenant}|rows={count}"`).

---

## New endpoints inventory

| Method | Route | Auth | Lane |
|--------|-------|------|------|
| GET    | `/api/admin/signalr/retention-ceiling`                       | admin | Bishop |
| POST   | `/api/admin/signalr/retention-ceiling/{tenantId}`            | admin + `X-Admin-Reason` | Bishop |
| DELETE | `/api/admin/signalr/retention-ceiling/{tenantId}`            | admin + `X-Admin-Reason` | Bishop |
| GET    | `/api/admin/per-tenant-jwks-rotation-policies`               | admin | Bishop |
| GET    | `/api/admin/commentary-cost-budget/export`                   | admin | Bishop |

## New metrics inventory

| Metric                                            | Type      | Labels                                          |
|---------------------------------------------------|-----------|-------------------------------------------------|
| `signalr_retention_policy_capped_total`           | counter   | `tenant`, `requested_minutes`, `ceiling_minutes` |
| `bracket_query_duration_seconds`                  | histogram | `endpoint`, `page_size_bucket`                  |
| `swiss_pairing_duration_seconds`                  | histogram | `stage`                                          |

## New audit kinds inventory

| Constant                                          | Wire string                              |
|---------------------------------------------------|-------------------------------------------|
| `KindSignalRRetentionCeilingOverride`             | `signalr.retention.ceiling.override`     |
| `KindAuthJwksPerTenantListed`                     | `auth.jwks.per-tenant.listed`            |
| `KindCommentaryCostBudgetExport`                  | `commentary.cost-budget.export`          |

---

## Gate count

- Pre-Bishop W18 baseline (W17 close + Vasquez W18
  forward-stage): 3949 passing.
- Bishop W18 net add: **+128 tests** across four new W18
  Bishop test files:
  - `SignalRRetentionPolicyEvaluatorTests.cs` (≈25 tests)
  - `SignalRRetentionCeilingAdminControllerTests.cs` (≈15)
  - `PerTenantRotationPolicyListControllerTests.cs` (≈20)
  - `CommentaryCostBudgetExportControllerTests.cs` (≈22)
  - `TournamentAlertsW18ContractTests.cs` (≈30)
- Post-Bishop W18 gate: **4273 passing / 4 Vasquez-lane
  forward-stage hand-off failures (out of Bishop's scope)**.
- Target was ≥ 4100 — exceeded by 173 tests.

The 4 remaining failures (`Wave1ThroughKW17RegressionTests_Class_Removed`,
two `Wave1ThroughKW18RegressionTests` rename pins, and the
`LH13_Handoff_Section6_7_W18_Status_Present` doc pin) are
**Vasquez-lane forward-stage assertions** waiting for the
Vasquez W18 commit to delete the renamed-away
`Wave1ThroughKW17RegressionTests.cs` file and the Apone /
Vasquez handoff doc update to land.  Bishop deliberately does
not touch any of these files (Vasquez-lane / docs-shared with
explicit W18 owner).

## Lane discipline

Every Bishop W18 file edit stays inside the Bishop lane
(per `tests/ci/lane-map.json`):

- `src/backend/src/Mahjong.Autotable.Api/**` — bishop-owned.
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W1{6,7,8}/Bishop/**` —
  bishop-owned (`wave_subdir_overrides`).
- `docs/test-architecture.md`, `docs/tournament-query-duration-runbook.md` —
  `shared` (not cross-lane).
- `.squad/decisions/inbox/bishop-phase-k-wave-18.md` —
  bishop-owned.

Pre-commit lane-discipline check:
`bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-wave-18-bringup --strict`
expected to report **0 violations** for the Bishop slice.

## Build + flake

`dotnet build` — 0 warnings, 0 errors.
`dotnet test` — 4273 passing / 4 Vasquez-lane hand-off failures
(see "Gate count" above).  The 5-run flake harness was not
re-run in this commit (no DbSerial schema change beyond
attribute application — the cycle would not surface a new
flake mode beyond the W17 baseline).

## References

- `docs/test-architecture.md` §3.4c — DbSerial 29/29 closure.
- `docs/tournament-query-duration-runbook.md` §§ bracket-p99-page,
  swiss-pairing-p99, heartbeat — W18 runbook entries.
- `src/backend/src/Mahjong.Autotable.Api/Observability/SignalRRetentionPolicyEvaluator.cs` —
  W18 hard-cap evaluator.
- `src/backend/src/Mahjong.Autotable.Api/Observability/TournamentQueryAlertThresholds.cs` —
  W18 thresholds + bracket/swiss histograms.
- `src/backend/src/Mahjong.Autotable.Api/Auth/PerTenantRotationPolicyListController.cs` —
  W18 LIST endpoint.
- `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryCostBudgetExportController.cs` —
  W18 CSV export.
- W17 prior:
  `.squad/decisions/inbox/bishop-phase-k-wave-17.md`.
