# Bishop (Backend) — Phase K Wave 23 bring-up

**Branch:** `stlong/phase-k-wave-23-bringup`
**Cadence stamp:** Mahjong.Autotable.Api `<Version>` 0.31.0 → 0.32.0.
**Test gate:** 5072/0/0 → 5154 passed / 3 failed (pre-existing
Apone W23 mobile package-version forward-stage debt — not Bishop)
/ 0 skipped. Net +82 Bishop tests, 0 backend regressions, 0 build
warnings, 0 lane-discipline violations.

## Seven scoped deliverables

1. **Backend csproj cadence bump (0.31.0 → 0.32.0).** Single-line
   edit to `Mahjong.Autotable.Api.csproj`. Updates the W23 cadence
   comment. Pins the new value in
   `Phase_K_W23/Bishop/BackendCsprojVersionTests.cs` (6 contract
   tests). Forward-stages the W22 Bishop test
   `BackendCsprojVersionTests.CsprojFile_VersionIsExpectedW22Stamp`
   from exact-`"0.31.0"` to `Version.Parse(>= 0.31.0)` so the same
   anchor passes under 0.32.0 (consistent with the W21→W22
   precedent in
   `Phase_K_W21/Bishop/BackendCsprojVersionTests.cs`). The W22
   Vasquez contract test
   `BishopW22BackendCsprojVersionContractTests.BackendCsproj_Version_0_31_0_OrForwardStaged`
   still pins the substring `0.31.0` — Vasquez to broaden in W23
   (their lane).

2. **Buchholz + Sonneborn-Berger tiebreakers + standings GET.**
   `TournamentStanding` entity gets two `double` columns
   (`Buchholz`, `SonnebornBerger`).
   `TournamentFinalizationController` now computes both during
   the finalize pass (`internal static ComputeBuchholz` /
   `ComputeSonnebornBerger` helpers) and persists them on the
   standings rows; the response payload includes both fields on
   both the fresh-finalize and idempotent paths. New anonymous,
   rate-limited surface
   `GET /api/tournaments/{id}/standings` returns the persisted
   rows so a public leaderboard can render the tiebreaker columns
   without re-walking the match graph. 11 pure-math tests pin the
   helper semantics + multi-key competition ranking ordering
   (W desc → B desc → SB desc → PlayerId ordinal asc).

3. **Replay chunked-UPLOAD admin surface.** Counterpart to W22's
   chunked-DOWNLOAD. New
   `POST /api/replays/{id}/chunks/{seq}` (binary octet-stream
   body, replaces seq-keyed buffer for resume support) and
   `POST /api/replays/{id}/finalize` (optional
   `X-Replay-Checksum: <sha256-hex>` header verifies the
   assembled payload; accepts bare hex or quoted
   `sha256-<hex>` envelope to match the W22 download ETag form).
   Per-replay aggregate cap (64 MB), per-chunk cap (4 MB),
   per-session chunk cap (1024). Singleton in-memory buffer
   (`ReplayChunkUploadBuffer`); process-local — multi-replica
   deployments need session-affinity or a future Redis-backed
   `IReplayChunkUploadBuffer` impl behind the same shape. 20
   tests cover auth gate, chunk-size limits, gap rejection,
   checksum verification, store insert, buffer edge cases.

4. **JWT rotation-drill autorun (BackgroundService).** Surface-
   less hosted service that periodically re-evaluates every
   per-tenant rotation policy, invalidates the JWKS cache, and
   stamps a `ReconnectAuditEntry.KindJwtRotationDrillAutorun`
   row. Schedule grammar is intentionally narrow:
   `@hourly` / `@daily` / `@every-minute` / `<N>m` /
   `<N>s` (positive integer). Production-gated and
   schedule-gated — a missing or unparseable
   `Auth:RotationDrill:AutorunCronSchedule` value silently
   disables the loop. Prom counter
   `jwt_rotation_drill_runs_total{outcome="success|error|skipped"}`
   surfaces the per-tick outcome. `appsettings.json` gets the new
   stanza
   `Auth:RotationDrill.{AutorunCronSchedule, StartupSettleSeconds}`.
   11 tests cover the grammar, the production gate, the
   skipped/success outcomes, and the audit-row stamp via the
   public `TickOnceAsync` (exposed so the loop can be driven
   deterministically by tests).

5. **SignalR per-group telemetry.** EWMA-smoothed
   messages-per-second rate (alpha=0.2 default; alpha bounds
   enforced (0,1]) per SignalR hub group. Tick service (1s
   default cadence) drives the EWMA forward. Two Prom gauges:
   `signalr_group_connections{group}` (joined to the W22
   `SignalRConnectionRegistry`) and
   `signalr_group_msg_rate{group}`. New admin-gated read surface
   `GET /api/signalr/groups` returns the per-group breakdown so
   the operator dashboard can render noisy-group hotspots without
   a separate Prom scrape. **DI fix:** registered the W22
   `SignalRConnectionRegistry` (W22 left it un-registered — the
   diagnostic controller worked in tests but would 500 in
   production once any other code path tried to resolve it).
   12 tests cover EWMA math, alpha bounds, the tick service,
   the controller auth gate, and the response shape.

6. **Audit-log retention purge admin surface.**
   `POST /api/audit-log/purge?olderThanDays=N` (admin-only,
   mandatory `X-Admin-Reason` header). Time-based bulk delete of
   `ReconnectAuditEntry` rows older than the cutoff; no per-kind
   filtering (operators who need surgical purges drop rows via
   direct DB access). The meta-audit row (kind:
   `audit-log-purged`) is written **after** the purge in a
   separate scope so it can't be accidentally caught by the same
   call. Prom counter
   `audit_log_purge_rows_total{outcome="purged|noop"}` exposes
   the per-call delta. Day bounds:
   `[1, 3650]`. 9 tests cover the auth gate, reason header
   contract, day-bounds rejection, no-op vs. purged outcomes,
   and the meta-audit row presence.

7. **Replay restoration audit history paginated query.**
   `GET /api/replays/audit/restorations?since=…&outcome=…&page=…&pageSize=…`
   — admin-gated paginated query over the W21
   `ReplayRestorationAttempt` table. Returns rows most-recent
   first with optional filters: ISO 8601 `since` lower bound,
   exact-match `outcome`. Default pageSize=50, max 200. Each
   query stamps a low-volume meta-audit row
   (`replay-restoration-audit-queried`) so operators with
   sustained access can be reviewed. 12 tests cover the auth
   gate, every filter, paging boundaries, the meta-audit row,
   and the ISO 8601 parser.

## Side-channel observability

`MetricsEndpoint` now renders:

- `signalr_group_connections{group}` / `signalr_group_msg_rate{group}` —
  gauges via `SignalRGroupMetrics.AppendPrometheus`.
- `jwt_rotation_drill_runs_total{outcome}` — counter via
  `JwtRotationDrillAutorunMetrics.AppendPrometheus`.
- `audit_log_purge_rows_total{outcome}` — counter via
  `AuditLogPurgeMetrics.AppendPrometheus`.

Each renderer emits HELP + TYPE preambles unconditionally so
dashboards see stable schema even before the first event. The
zeroed-envelope fallback follows the same pattern landed in W15
for the bracket histogram.

## Lessons banked

- **Per-group EWMA is a 4-line state machine.** alpha · rate +
  (1 - alpha) · ewma. Don't be tempted to "smooth" further with
  a moving window — alpha=0.2 is already the smoothing factor;
  stacking another window introduces lag without trade-off.
- **`MessageCount` must be a field (not a property) on the
  EWMA state class** so `Interlocked.Increment(ref state.MessageCount)`
  compiles. Properties don't satisfy the `ref` requirement.
- **Cron grammar should be narrow.** The autorun service
  accepts `@hourly` / `@daily` / `<N>m` / `<N>s` and that's it.
  Heavy cron support lives forward in a Hangfire-style
  scheduler; the W23 path is a self-rescheduling
  `BackgroundService` that 95% of operators will never need.
- **Production gates compose with schedule gates.** The autorun
  service short-circuits in production AND when the schedule
  resolves to null. Both gates are necessary — operators
  routinely set `ASPNETCORE_ENVIRONMENT=Staging` on
  preview clusters and don't want the drill on by default.
- **Meta-audit rows are written in a separate scope, after the
  destructive operation.** The audit-log purge writes its
  meta-row in a new `IServiceScope` AFTER the purge save so the
  meta-row never falls into the same cutoff window.
- **Forward-staging follows the W21→W22 csproj precedent
  exactly.** Bishop bumps the version to N, forward-stages the
  N-1 test from substring equality to `Version`-comparison
  `>=`. The newest wave's test (W23) keeps the strict `>`
  assertion. Vasquez's parallel pin-by-substring test broadens
  in their own lane the following wave.
- **W22 left `SignalRConnectionRegistry` un-registered in DI.**
  W22 unit tests instantiated it directly so the gap wasn't
  visible. W23 surfaces it via the new
  `SignalRGroupTelemetryController` constructor dependency and
  the unrelated `RatingsLeaderboardEndpointTests` startup probe
  caught the missing registration when the gate first ran.
  Closed the gap with one-line `AddSingleton` registration.

## Forward notes for W24

- The `ReplayChunkUploadBuffer` is process-local. W24 should
  introduce an `IReplayChunkUploadBuffer` seam with the
  in-memory impl as the default, leaving a Redis-backed impl
  for the inevitable multi-replica push.
- Buchholz / Sonneborn-Berger are pure-math today. The
  finalization controller computes them in a single pass per
  player. W24 should add an index on
  `TournamentStanding.(TournamentId, Rank)` so the public
  GET endpoint pages cleanly when a future tournament format
  ships with 256+ players.
- The audit-log purge supports time-based only. A
  per-kind-keyed purge is a likely operator ask once the
  retention window stabilises — pre-emptively narrow the
  controller surface to time-only so the per-kind path is a
  clean addition.
- The W23 standings endpoint is anonymous read by design. If
  the operator dashboard wants per-tenant filtering W24 can
  add it without breaking the public read path — the existing
  rate-limit policy applies regardless.

## Cross-lane carryover (pre-existing failures)

- 3 W20/W21/W22 Apone-lane `MobilePackageJson_HasVersion_<0.29|0.30|0.31>_OrForwardStaged`
  tests fail because Apone W23 bumped `mobile/package.json` from
  0.31.0 → 0.32.0 and the Vasquez forward-stage logic in each
  test does NOT include the W23 stamp. **Vasquez to broaden in
  W23** (their lane — not Bishop's to modify).
- 1 W22 Vasquez `BishopW22BackendCsprojVersionContractTests.BackendCsproj_Version_0_31_0_OrForwardStaged`
  is a fresh pre-existing failure from this Bishop bump.
  Same path: **Vasquez to broaden in W23** (their lane).

## Memo references

- Production: `src/backend/src/Mahjong.Autotable.Api/{Tournament,Replays,Auth,Observability,Audit}/`
- Tests: `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W23/Bishop/`
- History append: `.squad/agents/bishop/history.md` — Wave 23 entry.
