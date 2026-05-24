# Bishop — Phase K Wave 15

**Branch:** `stlong/phase-k-wave-15-bringup`
**Scope:** backend — Phase K Wave 15 bring-up. Seven scoped
deliverables, all landed:

1. **Replay blob streaming endpoint** —
   `GET /api/replays/{replayId}/blob` with RFC 7233 single-range
   support. Pairs with the W12 metadata GET to give callers a
   resumable byte-stream of the decompressed JSON payload.
   Honours `Range: bytes=<start>-<end>`,
   `bytes=<start>-`, and `bytes=-<N>` (suffix); multi-range and
   malformed values return 416. See `docs/replay-streaming.md`.

2. **Per-tenant JWKS rotation table** —
   `PerTenantJwksRotationPolicies` keyed by `TenantId`, opt-in
   toggle (`JwksRotation:PerTenant:Enabled`, default false).
   `RotationStartUtc` + `RotationCompleteUtc` typed as
   `DateTimeOffset` so a tenant scheduling rotations in their
   local timezone keeps the offset intact across persistence
   (the W14 `DateTime` path stripped the offset on
   serialisation). InMemory + Ef store implementations.
   Migrations land in all three EF providers. **Validator
   integration is deferred to W16** — W15 lands the table +
   opt-in toggle + store seam only so the surface boundary is
   reviewable in isolation. See `docs/per-tenant-jwks.md`.

3. **DbSerial completion on W9 Bishop tests** —
   `[Collection("DbSerial")]` applied to
   `EfCommentaryUsageMeterTests.cs` and
   `IdempotencyStoreContractTests.cs`. Closes the W14 Vasquez
   migration memo
   (`Phase_K_W14/Vasquez/db-serial-migration-completion.md`).
   See `Phase_K_W15/Bishop/db-serial-completion.md`.

4. **Tournament page-size latency histogram** —
   `tournament_query_duration_seconds{endpoint, page_size_bucket}`
   Prometheus histogram. Bucketed labels:
   `bracket-records` / `replay-list` / `spectator-audit-query`
   × `small` (≤25) / `medium` (≤75) / `large` (≤100). Surfaced
   through the existing `/metrics` endpoint. Side-channel
   observation: each endpoint optionally resolves the collector
   from DI so test fixtures still work when the metric is
   not wired. See `docs/bracket-shape.md §6`.

5. **Commentary cost forecasting endpoint** —
   `GET /api/commentary/cost/forecast?days=<n>` admin-gated.
   Returns
   `{ projectedMonthEndCost, confidence (low|medium|high),
   daysOfDataUsed, projectionMethodology }`. Linear
   extrapolation by days-elapsed in the current calendar month;
   confidence bucket on `daysOfDataUsed`. See
   `docs/commentary-llm.md §7`.

6. **Spectator handoff audit retention sweep** — hosted
   `SpectatorHandoffAuditRetentionSweep` running every
   `Spectator:Audit:SweepIntervalMinutes` (default 5). Deletes
   `SpectatorHandoffAuditRecord` rows older than
   `Spectator:Audit:RetentionDays`. See
   `docs/spectator-handoff.md §5`.

7. **Replay store retention sweep** — hosted
   `ReplayStoreRetentionSweep` running every
   `Replays:StoreSweepIntervalMinutes` (default 60). Evaluates
   `CompletedAt < utcNow - RetentionDays` against the **current**
   options each tick — operator can dial retention down (or up)
   and the next tick honours the new window (the W12
   `ExpiresAt`-based sweep keeps running alongside). See
   `docs/replay-by-id.md §4`.

## Cross-cutting decisions

* **`DateTimeOffset` widening for rotation edges.** The W14
  rotation policy used `DateTime`. Operators in non-UTC
  timezones saw the offset stripped on dashboard renders. W15
  widens to `DateTimeOffset` for the per-tenant table only —
  the global `JwtStagedRotationPolicy` remains `DateTime` for
  W14 wire compatibility.

* **Why a second retention sweep on replays?** The W12
  `ReplayRetentionSweepService` uses `ExpiresAt` computed once
  at insert; a runtime retention change does not retro-apply.
  The W15 sweep evaluates `CompletedAt` against the current
  retention each tick, so dialling down retention takes effect
  on the next tick. The two sweeps are orthogonal (no
  double-counting on already-deleted rows).

* **Opt-in toggle for per-tenant JWKS.** Single-tenant
  deployments pay no per-validation cost. Multi-tenant
  operators flip `JwksRotation:PerTenant:Enabled = true` and
  populate the table; the validator wiring lands in a future
  wave so this PR's surface stays narrow.

* **Latency collector is side-channel.** Each consumer
  optionally resolves `TournamentQueryLatencyMetrics` from DI
  and records via `ObserveDuration`. A null collector (test
  fixtures that don't register the singleton) is a no-op. This
  avoids forcing every consumer to thread the collector through
  the constructor.

## Verification

* `dotnet build src/backend/Mahjong.Autotable.slnx --nologo` →
  0 warnings, 0 errors.
* `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build
  --filter "FullyQualifiedName~Phase_K_W15.Bishop"` →
  **111 passed / 0 failed / 0 skipped**.
* Full backend gate: 3307 passed / 5 failed / 0 skipped — the
  5 failures are forward-staged Vasquez-lane markdown probes
  scheduled to land in the Vasquez W15 commit. Bishop cannot
  modify Vasquez-lane files per cross-lane discipline.

## Files (Bishop lane)

* `src/backend/src/Mahjong.Autotable.Api/Auth/PerTenantJwksRotationPolicy.cs` (new)
* `src/backend/src/Mahjong.Autotable.Api/Observability/TournamentQueryLatencyMetrics.cs` (new)
* `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/{Sqlite,Postgres,SqlServer}/...Phase_K_W15_PerTenantJwksRotation*.cs` (new × 3)
* `src/backend/src/Mahjong.Autotable.Api/Replays/ReplayController.cs` (modified — `GetBlob`)
* `src/backend/src/Mahjong.Autotable.Api/Replays/ReplayStore.cs` (modified — sweep + interface)
* `src/backend/src/Mahjong.Autotable.Api/Spectator/SpectatorHandoffAudit.cs` (modified — sweep)
* `src/backend/src/Mahjong.Autotable.Api/Spectator/SpectatorHandoffController.cs` (modified — latency observation)
* `src/backend/src/Mahjong.Autotable.Api/Tournament/TournamentController.cs` (modified — latency observation)
* `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryCostController.cs` (modified — `Forecast`)
* `src/backend/src/Mahjong.Autotable.Api/Data/AppDbContext.cs` (modified — new DbSet)
* `src/backend/src/Mahjong.Autotable.Api/Observability/MetricsEndpoint.cs` (modified — render new histogram)
* `src/backend/src/Mahjong.Autotable.Api/Program.cs` (modified — wire W15 services)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs` (modified — `[Collection("DbSerial")]`)
* `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs` (modified — `[Collection("DbSerial")]`)
* 8 new contract test files under
  `tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Bishop/`.
* `docs/replay-streaming.md` (new), `docs/per-tenant-jwks.md` (new).
* Append-only `§` additions to `docs/bracket-shape.md`,
  `docs/commentary-llm.md`, `docs/spectator-handoff.md`,
  `docs/replay-by-id.md`.
* `Phase_K_W15/Bishop/{charter,history,bishop-w15-test-summary,db-serial-completion}.md`
  (new).
* `.squad/decisions/inbox/bishop-phase-k-wave-15.md` (this
  file).
* `.squad/agents/bishop/history.md` (W15 section appended).

## Forward roadmap (W16 entry points)

* Wire the per-tenant JWKS rotation store into
  `JwtValidationService` — the validator should resolve the
  per-tenant row first and fall back to
  `JwtStagedRotationPolicy` when no row matches.
* Consider widening the global `JwtStagedRotationPolicy`
  rotation edges to `DateTimeOffset` for symmetry with W15.
* Surface the tournament-latency histogram on a Grafana
  dashboard alongside the W14 SignalR + commentary cost
  metrics.
