# Phase K Wave 15 — Bishop — test summary

> Bishop-lane W15 backend test inventory. Companion to
> [`charter.md`](charter.md) and [`history.md`](history.md) in
> the same directory.
> The existing [`README.md`](README.md) in this directory is a
> Vasquez-authored forward-stage marker — it points at the
> Vasquez forward-staged contract tests under
> `tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Vasquez/`. This
> file is the Bishop-authored counterpart.

## Test surface added in W15

Bishop-lane tests added in W15 under
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Bishop/`:

| Test file | Count |
| --- | --- |
| `ReplayBlobStreamingEndpointTests.cs` | 19 |
| `PerTenantJwksRotationStoreTests.cs` | 15 |
| `CommentaryCostForecastEndpointTests.cs` | 13 |
| `SpectatorHandoffAuditRetentionSweepTests.cs` | 11 |
| `ReplayStoreRetentionSweepTests.cs` | 13 |
| `TournamentQueryLatencyMetricsTests.cs` | 19 |
| `BishopW15SelfLaneTests.cs` | 16 |
| `DbSerialCompletionTests.cs` | 5 |
| **Total Bishop W15 tests** | **111** |

All 111 tests pass via:

```
dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build \
    --filter "FullyQualifiedName~Phase_K_W15.Bishop"
```

Full backend gate: **3307 passed / 5 failed / 0 skipped** —
the 5 failures are forward-staged Vasquez-lane tests
(`Phase_K_W15/Vasquez/*`) that probe markdown content scheduled
to land in the Vasquez W15 commit. Per cross-lane discipline,
Bishop cannot modify Vasquez-lane files.

## Files modified / added (Bishop lane)

### New backend files

* `src/backend/src/Mahjong.Autotable.Api/Auth/PerTenantJwksRotationPolicy.cs`
  — entity, `IPerTenantJwksRotationStore` seam, InMemory + Ef
  implementations, `PerTenantJwksRotationOptions`.
* `src/backend/src/Mahjong.Autotable.Api/Observability/TournamentQueryLatencyMetrics.cs`
  — self-contained Prometheus histogram collector.
* `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/Sqlite/20260524030703_Phase_K_W15_PerTenantJwksRotation.cs`
  (+ Designer).
* `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/Postgres/20260524030713_Phase_K_W15_PerTenantJwksRotation.cs`
  (+ Designer).
* `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/SqlServer/20260524030713_Phase_K_W15_PerTenantJwksRotation.cs`
  (+ Designer).

### Modified backend files

* `Replays/ReplayController.cs` — new `GetBlob` endpoint +
  `TryParseSingleByteRange` helper; latency-metric observation
  in `List`.
* `Replays/ReplayStore.cs` — `SweepByCompletedAtAsync` on
  the interface + both implementations + new
  `ReplayStoreRetentionSweep` background service +
  `StoreSweepIntervalMinutes` option.
* `Spectator/SpectatorHandoffAudit.cs` —
  `SweepIntervalMinutes` option +
  `SpectatorHandoffAuditRetentionSweep` background service.
* `Spectator/SpectatorHandoffController.cs` — latency-metric
  observation in `QueryAudit`.
* `Commentary/CommentaryCostController.cs` — new `Forecast`
  endpoint.
* `Tournament/TournamentController.cs` — latency-metric
  observation in `BracketRecords`.
* `Data/AppDbContext.cs` — new `PerTenantJwksRotationPolicies`
  `DbSet` + entity configuration.
* `Observability/MetricsEndpoint.cs` — render the new
  histogram alongside the W14 SignalR + commentary cost
  metrics.
* `Program.cs` — wire the W15 hosted services + per-tenant
  JWKS store (toggle-gated) + latency-metric singleton.
* `Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs` —
  `[Collection("DbSerial")]`.
* `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs` —
  `[Collection("DbSerial")]`.

### New documentation

* `docs/replay-streaming.md` (new).
* `docs/per-tenant-jwks.md` (new).
* `docs/bracket-shape.md §6` (append).
* `docs/commentary-llm.md §7` (append).
* `docs/spectator-handoff.md §5` (append).
* `docs/replay-by-id.md §4` (append).

### New Bishop W15 artefacts

* `Phase_K_W15/Bishop/charter.md` (Bishop, this wave).
* `Phase_K_W15/Bishop/history.md` (Bishop, this wave).
* `Phase_K_W15/Bishop/bishop-w15-test-summary.md` (this file).
* `Phase_K_W15/Bishop/db-serial-completion.md` (Bishop, this
  wave).

## Item-to-test traceability

| W15 item | Code under test | Contract tests |
| --- | --- | --- |
| Replay blob streaming | `ReplayController.GetBlob` | `ReplayBlobStreamingEndpointTests` |
| Per-tenant JWKS | `PerTenantJwksRotationPolicy*` | `PerTenantJwksRotationStoreTests` |
| DbSerial completion | 2 W9 Bishop test files | `DbSerialCompletionTests` |
| Tournament metrics | `TournamentQueryLatencyMetrics` | `TournamentQueryLatencyMetricsTests` |
| Cost forecast | `CommentaryCostController.Forecast` | `CommentaryCostForecastEndpointTests` |
| Spectator sweep | `SpectatorHandoffAuditRetentionSweep` | `SpectatorHandoffAuditRetentionSweepTests` |
| Replay store sweep | `ReplayStoreRetentionSweep` + `SweepByCompletedAtAsync` | `ReplayStoreRetentionSweepTests` |
| (cross-cutting) | All W15 Bishop surfaces existence | `BishopW15SelfLaneTests` |
