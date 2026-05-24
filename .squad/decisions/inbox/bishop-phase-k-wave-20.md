# Bishop — Phase K Wave 20

**Branch:** `stlong/phase-k-wave-20-bringup`
**Scope:** backend — Phase K Wave 20 bring-up. Seven scoped
deliverables, all in Bishop's lane (`src/backend/src/`,
`Phase_K_W20/Bishop/`). No cross-lane bundling.

## Deliverables

1. **csproj 0.29.0 bump.**
   `Mahjong.Autotable.Api.csproj` now carries
   `<Version>0.29.0</Version>` with a W20 cadence comment.
   Covered by `BackendCsprojVersionTests` (5 contract tests in
   `Phase_K_W20/Bishop/`).

2. **Swiss live pairing service + admin endpoint.**
   `src/backend/src/Mahjong.Autotable.Api/Tournament/SwissPairingService.cs`
   (553 lines) hosts `SwissPairingService.PairNextRoundAsync`
   — loads tournament + registrations (excluding withdrawn
   `Seed < 0` per the W19 forfeit sentinel), builds the
   match-point map + opponent graph from completed matches,
   selects the tiebreaker (single-Buchholz default;
   median-Buchholz at ≥ 5 completed rounds), pre-computes
   Buchholz, delegates to the existing
   `ISwissPairingService` engine, and persists a
   `SwissPairingAuditEntry` row per board. Wire-stable error
   codes: `tournament-not-found`, `not-swiss-format`,
   `insufficient-players`, `round-already-paired`,
   `pairing-engine-empty`.

   The HTTP surface is
   `POST /api/admin/tournaments/{id}/swiss-pair-next-round`
   on `SwissPairingAdminController`, X-Admin-Reason header
   mandatory.

   Audit kind: `tournament:swiss-pairing-computed`
   (`ReconnectAuditEntry.KindTournamentSwissPairingComputed`).

   Tests: `SwissPairingServiceTests` (30) +
   `SwissPairingAdminControllerTests` (7) = 37 swiss tests.

3. **Per-tenant rotation bulk-delete + bulk-enable.**
   Completes the W19 bulk-update triad.

   - `PerTenantRotationBulkDeleteController` —
     `POST /api/admin/per-tenant-jwks-rotation-policies/bulk-delete`.
     Accepts `{ tenantIds: [string...] }`, deletes every
     match, writes one audit row per evicted policy
     (`KindAuthJwksPerTenantBulkDeleted`). Missing tenants
     are reported in `notFoundTenants[]` rather than treated
     as errors.

   - `PerTenantRotationBulkEnableController` —
     `POST /api/admin/per-tenant-jwks-rotation-policies/bulk-enable`.
     Accepts `{ items: [{ tenantId, renewalWindowDays? }] }`.
     **Semantics decision:** the
     `PerTenantJwksRotationPolicy` row has no `Enabled`
     column (W17 design); "bulk-enable" is therefore
     implemented as **renewing the rotation window** — sets
     `RotationStartUtc=now`, `RotationCompleteUtc=now +
     renewalWindowDays` (default 30, max 365). This avoids
     a 3-provider migration (SQL Server / Postgres /
     SQLite) for a single bool and lets W20 land cleanly.
     Audit kind: `KindAuthJwksPerTenantBulkEnabled`.

   Both surfaces mirror the W19 bulk-update posture —
   401/403/503/400/413 ladder, `X-Admin-Reason` header
   mandatory (≤ 512 chars), batch cap 100.

   Tests: `PerTenantRotationBulkDeleteControllerTests` (16) +
   `PerTenantRotationBulkEnableControllerTests` (17) = 33
   bulk tests.

4. **Replay auto-expiry CronJob seam.**
   `src/backend/src/Mahjong.Autotable.Api/Replays/ReplayStoreExpiryHandler.cs`
   (276 lines) registers a `BackgroundService` that ticks
   every `ReplayOptions.AutoExpiryTickIntervalMinutes`
   (default 60). On each tick it walks the per-tenant
   retention policy store and calls the new
   `IReplayStore.SweepWithPerTenantBreakdownAsync` extension
   so the metric can be sliced by tenant. When the policy
   store is null, the handler falls back to the global
   `SweepByCompletedAtAsync(retention, utcNow)` and buckets
   the row count under the `_unknown` tenant label —
   mirrors the W19 integrity-audit empty-tenant convention.

   New collector `ReplayExpiryMetrics`:
   `replay_expired_total{tenant}` counter, rendered by
   `MetricsEndpoint` with a zeroed `_unknown` envelope so
   the dashboard always has a series to query.

   Audit kind: `KindReplayAutoExpiry`.

   Tests: `ReplayStoreExpiryHandlerTests` (14) +
   `ReplayExpiryMetricsTests` (16) = 30 expiry tests.

5. **JWT key-rotation drill endpoint.**
   `src/backend/src/Mahjong.Autotable.Api/Auth/JwtRotationDrillController.cs`
   (214 lines). `POST /api/admin/jwt-keys/rotation-drill`.
   Non-prod gate on `IWebHostEnvironment.IsProduction()`
   (returns 403 in Production); additional env-var gate
   `MAHJONG_JWT_ROTATION_DRILL_ENABLED=false` returns 403
   even in non-prod. Walks every per-tenant policy via
   `IPerTenantJwksRotationStore.ListAsync()`, calls
   `PerTenantJwksRotationValidator.EvaluateAsync`,
   invalidates the JWKS cache, writes a
   `KindJwtKeyRotationDrill` audit row.

   The drill exercises the same code paths as a real
   rotation but never modifies signing keys — operators
   can repeat the drill safely.

   Tests: `JwtRotationDrillControllerTests` (12 — covers
   production block, env-var override, admin auth ladder,
   X-Admin-Reason contract, audit detail).

6. **Swiss pairing duration alerts (2 new).**
   Appended to
   `src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`:

   - `SwissPairingDurationHigh` — P95 ≥ 5s for 5m → ticket.
   - `SwissPairingDurationCritical` — P95 ≥ 15s for 2m → page.

   Both carry `team: bishop` + `wave: phase-k-w20` labels.

   Tests: `SwissPairingAlertsW20ContractTests` (14).

7. **SignalR retention dashboard JSON.**
   `src/backend/src/Mahjong.Autotable.Api/Observability/dashboards/signalr-retention-metrics.json`
   — uid `bishop-signalr-retention`, schemaVersion 38,
   tags `bishop / wave-20 / signalr / retention`, 6 panels
   covering `signalr_retention_applied`,
   `signalr_retention_cap_triggered`, the W18 capped
   counter, and the new `replay_expired_total`.

   **Lane purity decision:** placed under
   `src/backend/src/.../Observability/dashboards/` instead
   of `infra/grafana/` because `infra/` is Apone's lane.
   Apone can pick up the JSON from a published artifact in
   a future wave; that pickup is *outside* Bishop's W20
   scope.

   Tests: `SignalRRetentionDashboardTests` (15).

## Audit kinds added

Added to `ReconnectAuditEntry` (in `ChangshaEntities.cs`):

| Constant | Value |
| --- | --- |
| `KindTournamentSwissPairingComputed` | `tournament:swiss-pairing-computed` |
| `KindAuthJwksPerTenantBulkDeleted`   | `auth:jwks-per-tenant-bulk-deleted` |
| `KindAuthJwksPerTenantBulkEnabled`   | `auth:jwks-per-tenant-bulk-enabled` |
| `KindReplayAutoExpiry`               | `replay:auto-expiry` |
| `KindJwtKeyRotationDrill`            | `auth:jwt-key-rotation-drill` |

Covered by `W20AuditKindConstantTests` (8 contract tests).

## Cadence-pin test relaxations

W19 → W20 follows the same cadence-pin pattern used for
W17 → W18 and earlier — the previous wave's exact-match
version assertion is relaxed to a strict-≥ floor, and the
new wave pins a fresh exact match:

- `Phase_K_W19/Bishop/BackendCsprojVersionTests.CsprojFile_VersionIsExpectedW19Stamp`
  — relaxed from `Equal("0.28.0", ...)` to a strict-≥ 0.28.0
  check. Leaves the `ExpectedVersion` const in place for
  documentation.
- `Phase_K_W18/Bishop/TournamentAlertsW18ContractTests.Yaml_W18_AlertsCarry_TeamBishop`
  — relaxed from `Equal(5, teamBishopCount)` to `>= 5` so
  the W20 additions (2 more bishop alerts) do not break
  the contract.

## Test gate

```
Passed!  - Failed:     0, Passed:  4522, Skipped:     0, Total:  4522
```

- W19 baseline: 4376
- W20 net adds:
  - `BackendCsprojVersionTests` ............ 5
  - `W20AuditKindConstantTests` ............ 8
  - `SwissPairingAlertsW20ContractTests` ... 14
  - `SignalRRetentionDashboardTests` ....... 15
  - `ReplayExpiryMetricsTests` ............. 16
  - `ReplayStoreExpiryHandlerTests` ........ 14
  - `SwissPairingServiceTests` ............. 30
  - `PerTenantRotationBulkDeleteControllerTests` 16
  - `PerTenantRotationBulkEnableControllerTests` 17
  - `JwtRotationDrillControllerTests` ...... 12
  - `SwissPairingAdminControllerTests` ..... 7
  - Total W20 tests: 154 (target was 125)
- Net delta: +146 (some replacements / removals balance).

## Lane discipline

All files touched are under:

- `src/backend/src/Mahjong.Autotable.Api/`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W*/Bishop/`
- `.squad/decisions/inbox/bishop-phase-k-wave-20.md` (this memo, gitignored — force-added)

No cross-lane bundling. `infra/`, `.github/workflows/`,
`docs/`, `mobile/`, and `src/frontend/` are untouched.

## Co-operation with the wave-20 cohort

- Apone committed first (`bc775b9`) — DevOps lane only.
  Lane-clean per their commit message.
- This W20 commit lands after Apone's. If Hicks (frontend)
  or Vasquez (mobile) land before push, the rebase in the
  flock block keeps the history linear.

## Build posture

- API project: 0 warnings, 0 errors.
- Test project: 2 pre-existing xUnit2002 warnings in
  Vasquez's W19 lane — outside Bishop's scope, NOT
  introduced by W20.
