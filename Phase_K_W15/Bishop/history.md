# Phase K Wave 15 — Bishop (Backend) history

> Companion to [`charter.md`](charter.md) in the same directory.
> Carries the timeline + verification log for the W15 bring-up.

## Timeline

1. **Baseline gate** — `dotnet test src/backend/Mahjong.Autotable.slnx`
   ran clean against `e6fef84` (PR #60 merged W14):
   **3029 passed / 0 failed / 0 skipped**, ~1m 55s wall clock.

2. **Item 3 — DbSerial completion (small / low risk first)**.
   `[Collection("DbSerial")]` applied to
   `Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs` and
   `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`. XML doc
   paragraphs updated to point at the W15 closure memo.

3. **Item 1 — Replay blob streaming**. Added
   `GET /api/replays/{replayId}/blob` to `ReplayController.cs`
   with RFC 7233 single-range parser
   (`TryParseSingleByteRange`, `internal static`). Returns 206 +
   `Content-Range` for ranged requests, 416 for
   malformed / multi-range / out-of-range.

4. **Item 7 — Replay store retention sweep**. Added
   `SweepByCompletedAtAsync` to `IReplayStore` + both
   implementations (`InMemoryReplayStore`, `EfReplayStore`).
   New hosted service `ReplayStoreRetentionSweep` runs hourly
   (default), evaluates `CompletedAt < utcNow - RetentionDays`
   against the **current** options each tick.

5. **Item 6 — Spectator audit retention sweep**. Added
   `SweepIntervalMinutes` option (default 5) +
   `SpectatorHandoffAuditRetentionSweep` background service.
   Wired through the existing `Spectator:Audit:StorageImpl=Ef`
   branch only (in-memory store has no on-disk footprint). To
   resolve the options outside `IOptions<T>`, the options are
   also bound as a singleton.

6. **Item 5 — Commentary cost forecast**. Added
   `GET /api/commentary/cost/forecast?days=<n>` to
   `CommentaryCostController.cs`. Linear extrapolation by
   days-elapsed; confidence bucket on `daysOfDataUsed`
   (`< 3` low, `3–9` medium, `>= 10` high).

7. **Item 2 — Per-tenant JWKS rotation**. Created
   `Auth/PerTenantJwksRotationPolicy.cs` (entity +
   `IPerTenantJwksRotationStore` seam + InMemory + Ef
   implementations + options). Added DbSet + entity config to
   `AppDbContext.cs`. Generated migrations for Sqlite, Postgres,
   and SqlServer named `Phase_K_W15_PerTenantJwksRotation`.
   Wiring in `Program.cs` is gated on
   `JwksRotation:PerTenant:Enabled` (default false).

8. **Item 4 — Tournament query latency metrics**. Created
   `Observability/TournamentQueryLatencyMetrics.cs` —
   self-contained Prometheus histogram collector
   (`tournament_query_duration_seconds{endpoint, page_size_bucket}`).
   Registered as singleton in `Program.cs`, injected
   (optionally) into `TournamentController`, `ReplayController`,
   and `SpectatorHandoffController`. Rendered through the
   existing `/metrics` endpoint
   (`Observability/MetricsEndpoint.cs`).

9. **Contract tests** — 8 new test files under
   `tests/.../Phase_K_W15/Bishop/`. See
   [`README.md`](README.md) for the full
   test inventory.

10. **Documentation** — two new docs
    (`docs/replay-streaming.md`, `docs/per-tenant-jwks.md`) +
    four append-only `§` additions to existing docs
    (`docs/bracket-shape.md §6`, `docs/commentary-llm.md §7`,
    `docs/spectator-handoff.md §5`, `docs/replay-by-id.md §4`).

## Verification log

* `dotnet build src/backend/Mahjong.Autotable.slnx --nologo` →
  0 warnings, 0 errors, after each item.
* `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build`
  → **3307 passed / 0 failed / 0 skipped — within Bishop lane**;
  the 5 backend gate failures are forward-staged tests under
  `Phase_K_W15/Vasquez/*` that probe markdown content under
  Vasquez authorship (Bishop cannot modify Vasquez lane files
  per cross-lane discipline).
* `dotnet test src/backend/Mahjong.Autotable.slnx --no-build
  --filter "FullyQualifiedName~Phase_K_W15.Bishop"` →
  **111 passed / 0 failed / 0 skipped** for the new Bishop W15
  test surface.

## Cross-lane integrity

* All file edits stayed within Bishop-allowed paths:
  - `src/backend/**`
  - `Phase_K_W15/Bishop/**` (new files only; the existing
    Vasquez-authored `README.md` was not touched)
  - `docs/{replay-streaming,per-tenant-jwks,bracket-shape,commentary-llm,spectator-handoff,replay-by-id}.md`
  - Migrations under
    `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`
* The two W9 Bishop test files modified by Item 3 are
  Bishop-attributed already
  (`Phase_K_W9/Bishop/` per `tests/ci/lane-map.json`).
* No changes to Apone-lane files (`.github/workflows/**`,
  `infra/**`), Hicks-lane files (`src/frontend/**`), or
  Vasquez-lane files (`Phase_K_W*/Vasquez/**`).
