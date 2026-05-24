# Bishop — Phase K Wave 19

**Branch:** `stlong/phase-k-wave-19-bringup`
**Scope:** backend — Phase K Wave 19 bring-up. Seven scoped
deliverables, all in Bishop's lane (`src/backend/src/`,
`Phase_K_W19/Bishop/`). No cross-lane bundling.

## Deliverables

1. **csproj 0.28.0 bump.** `Mahjong.Autotable.Api.csproj`
   now carries `<Version>0.28.0</Version>` — covered by
   `BackendCsprojVersionTests` (5 contract tests).

2. **Per-tenant rotation bulk update endpoint** —
   `POST /api/admin/jwks/per-tenant/rotation/bulk-update`.
   Admin-gated via `AuthCookieService.ResolveAsync` +
   `Role=admin` check. Accepts a JSON array of
   `{TenantId, RotationIntervalSeconds, GracePeriodSeconds,
   Enabled}` items, validates each before persisting,
   returns a per-row result list (success/error). Audit
   trail goes through `ReconnectAuditEntry` with kind
   `per-tenant-rotation:bulk-update`.

   Validation rules (kept tight to mirror the single-tenant
   surface):
   - `TenantId` required, non-empty.
   - `RotationIntervalSeconds ≥ 60`.
   - `GracePeriodSeconds ≥ 0` and `< RotationIntervalSeconds`.

   Tests: `PerTenantRotationBulkUpdateControllerTests` (18).

3. **SignalR retention lifecycle metrics.** New collector
   `SignalRRetentionLifecycleMetrics` exposes:

   - `mahjong_signalr_retention_policy_applied_total` —
     counter incremented every time
     `SignalRRetentionPolicyEvaluator.Evaluate` runs and
     persists a non-no-op result.
   - `mahjong_signalr_retention_policy_cap_triggered_total`
     — counter incremented when the per-tenant cap caused
     the evaluator to short-circuit (rows trimmed to fit
     under the cap).

   `MetricsEndpoint` now renders both with zeroed
   envelopes so dashboards always have a series to query.
   Tests: `SignalRRetentionLifecycleMetricsTests` (11) +
   `SignalRRetentionEvaluatorLifecycleIntegrationTests` (6)
   = 17 lifecycle tests total.

4. **Replay store integrity audit.** New endpoint
   `GET /api/admin/replays/integrity-audit?from=&to=&tenant=`
   (admin-gated). Window is capped at **90 days** to keep
   runtime bounded; out-of-bounds requests return HTTP 400.

   For each replay row in the window:
   - decompresses the payload,
   - computes SHA-256 over the decompressed bytes,
   - aggregates per-tenant (incl. an `_unknown` tenant
     bucket for nullable `TenantId`),
   - emits a **global** SHA-256 over the ordered
     concatenation of per-tenant tenant-id ‖ hex-checksum
     pairs.

   Response body shape:
   ```
   {
     "from": "...",
     "to": "...",
     "scannedCount": N,
     "perTenant": [ { "tenantId": "...", "rowCount": ...,
                       "checksumHex": "..." } ],
     "globalChecksumHex": "..."
   }
   ```

   An audit row is written via `ReconnectAuditEntry` with
   kind `replay-store:integrity-audit` and `Detail` set to
   the global checksum (so call sites can correlate without
   re-running the scan). Tests:
   `ReplayStoreIntegrityAuditControllerTests` (15).

5. **JWT duration histograms + Grafana dashboard.** New
   `JwtDurationMetrics` collector tracking issue + validate
   latency in two histograms with a shared bucket ladder:

   ```
   0.0001, 0.0005, 0.001, 0.005, 0.01, 0.025, 0.05,
   0.1,    0.25,   0.5,   1.0,   2.5,  5.0
   ```

   Rationale: the bottom of the ladder (100 µs → 1 ms)
   covers the fast in-process path for both
   `JwtIssuingService` and `JwtValidationService`; the top
   (1 s → 5 s) covers cold paths where the JWKS store or
   per-tenant rotation validator does I/O. Anything beyond
   5 s only increments the histogram `_count` and surfaces
   in the rendered `+Inf` bucket, which is sufficient to
   alert on saturation.

   Wiring:
   - `JwtIssuingService` has a new optional ctor param
     `JwtDurationMetrics? durationMetrics`. `IssueAsync`
     stamps a stopwatch + records on the issue histogram
     with `tenant` labelled (falls back to `_unknown` if
     the request body omits a tenant).
   - `JwtValidationService` was refactored: the existing
     `Validate` method became a wrapper around a private
     `ValidateCore` so the wrapper can `try/finally` and
     stamp the validate histogram **unconditionally** —
     even malformed/empty tokens fold into the `_unknown`
     tenant bucket. Tenant is lifted either from the
     `claims.tenant` JSON field or from a top-level
     `tenant` claim.

   Three ctors are now in place for `JwtValidationService`
   to keep W14/W4 callers compiling:
   - 1-arg (W4): keys only.
   - 2-arg (W14): keys + blocked-metrics.
   - 3-arg (W19): keys + blocked-metrics + duration-metrics.

   Grafana dashboard lives at
   `src/backend/src/Mahjong.Autotable.Api/Observability/dashboards/jwt-validator-metrics.json`
   (Bishop lane — explicitly NOT `infra/grafana/dashboards/`,
   which is Apone). 8 panels: p50/p95/p99 issue and
   validator latency, plus request rate per tenant. UID
   `bishop-jwt-validator-metrics`, tagged `bishop`,
   `wave-19`, `jwt`, `auth`.

   Tests: `JwtDurationMetricsTests` (16) +
   `JwtDurationMetricsBucketLadderTests` (10) +
   `JwtValidatorMetricsDashboardTests` (10) +
   `JwtServiceDurationIntegrationTests` (10) = 46.

6. **Swiss pairing audit endpoint + entity + 3-provider
   migrations.** New entity `SwissPairingAuditEntry`
   (PK `Id`/Guid, `TournamentId`/Guid,
   `Round`/int, `Board`/int, `White`/string,
   `Black`/string, `Tiebreaker`/string?,
   `CreatedAtUtc`/DateTime) with a unique
   `(TournamentId, Round, Board)` index and a single-column
   `CreatedAtUtc` index for windowed scans.

   Migrations (timestamped `20260524105946` for Sqlite,
   `20260524105955` for Postgres, `20260524105958` for
   SqlServer) live under
   `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`
   with their `.Designer.cs` companions and shared
   `{Provider}AppDbContextModelSnapshot.cs` updates.

   Endpoint `GET /api/tournaments/{id}/swiss/audit` returns
   the audit rows for a tournament ordered by `Round`,
   `Board`. The `Black` column is compared against
   `FideC04SwissPairingService.ByeOpponent` (`"__bye__"`)
   to flag `isBye` in the response payload. Admin-gated.
   Audit row written via `ReconnectAuditEntry` with kind
   `swiss-pairing-audit:read`.

   Tests: `SwissPairingAuditEntityTests` (10) +
   `TournamentSwissPairingAuditControllerTests` (11) +
   `SwissPairingAuditMigrationFilesTests` (15 with Theory)
   = 36.

7. **New audit kinds in `ChangshaEntities`.** Four
   constants added to keep audit-kind grep-discoverable:
   - `per-tenant-rotation:bulk-update`
   - `replay-store:integrity-audit`
   - `swiss-pairing-audit:read`
   - `jwt:duration-metrics` (reserved for future
     out-of-band stamping; currently unused at runtime but
     guarded by tests so we don't drift).

   Tests: `W19AuditKindConstantTests` (6).

## W18 → W19 cadence note

W18 finalized the `DbSerial` migration to 29/29 candidates.
W19 picks up at 29/29 and **does not** introduce new DB-
touching tests outside that collection — every new
`Phase_K_W19/Bishop/*.cs` file that touches sqlite carries
`[Collection("DbSerial")]`. Each test class spins its own
`_scratchDir` plus a unique
`bishop-w19-{topic}-{guid}.sqlite` to keep parallel runs
safe.

## Gate / test counts

- Bishop W19 lane filter: `Wave=Phase-K-19&Lane=Bishop` →
  163 tests, all passing.
- Full suite: 4376 total, 4367 passing (well above the
  4300 gate). The 9 failures are all in Apone/Vasquez
  lanes (W19 Kyverno additional-rules contract +
  Vasquez self-lane memo presence) and unblock once those
  agents ship.

## Lane discipline

All paths Bishop touched:
- `src/backend/src/Mahjong.Autotable.Api/**`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W19/Bishop/**`
- `.squad/decisions/inbox/bishop-phase-k-wave-19.md` (this memo)

No infra, no frontend, no other lanes' tests.
