# Bishop — Phase K Wave 21

**Branch:** `stlong/phase-k-wave-21-bringup`
**Scope:** backend — Phase K Wave 21 bring-up. Seven scoped
deliverables, all in Bishop's lane (`src/backend/src/`,
`Phase_K_W21/Bishop/`). No cross-lane bundling.

## Deliverables

1. **csproj 0.30.0 bump.**
   `Mahjong.Autotable.Api.csproj` now carries
   `<Version>0.30.0</Version>` with a W21 cadence comment.
   Covered by `BackendCsprojVersionTests` (5 contract tests
   in `Phase_K_W21/Bishop/`) — version pinned strict
   `> 0.29.0` and exact-match `0.30.0`.

2. **Swiss apply-round service + admin endpoint.**
   Closes the loop on the W20 preview path. The W20
   `SwissPairingService` writes one `SwissPairingAuditEntry`
   per board; the W21 `SwissApplyRoundService` projects those
   rows into `TournamentMatch` rows so the live tournament UI
   surfaces the pairings. Idempotent — re-calling with the
   same `(tournamentId, round)` returns the existing matches
   (`Created=false`) and writes no new audit row.

   Wire-stable error codes: `tournament-not-found`,
   `not-swiss-format`, `round-not-paired`,
   `round-out-of-range`.

   Surface:
   `POST /api/admin/tournaments/{id}/swiss-apply-round`
   with `X-Admin-Reason` header mandatory and body
   `{ Round: int }`.

   Audit kind: `tournament.swiss-pairing.applied`
   (`ReconnectAuditEntry.KindTournamentSwissRoundApplied`).

   Tests: `SwissApplyRoundServiceTests` (13) +
   `SwissApplyRoundControllerTests` (9) = 22 tests.

3. **Per-tenant scheduled JWKS rotation cadence.**
   Adds an admin-gated cron-style scheduling surface on top
   of the W15 per-tenant rotation policy. New entity
   `RotationScheduleEntity (TenantId, CronExpression,
   Enabled, Notes, ...)` with `TenantId` as the natural key
   and `(Enabled, UpdatedAtUtc)` indexed for the operator
   dashboard.

   - `RotationScheduleAdminController` —
     `POST /api/admin/per-tenant-jwks-rotation-policies/{tenantId}/schedule`,
     create-or-replace semantics, X-Admin-Reason mandatory.
     Audit kind: `auth.jwks.rotation.scheduled`.
   - `RotationScheduledExecutorService : BackgroundService` —
     60s tick, evaluates every enabled schedule via the
     `SimpleCronMatcher` parser (5- or 6-field cron, supports
     `*`, ranges `A-B`, steps `*/N` + `A-B/N`, comma-lists,
     wildcards). Per-tick metric stamp:
     `jwt_scheduled_rotation_total{tenant,status}` with
     statuses `success` / `error` / `skipped`. Per-tick
     idempotency — a schedule that already ran this UTC
     minute is skipped. Successful executes advance the
     matching `PerTenantJwksRotationPolicy.RotationStartUtc`
     30 days forward and stamp
     `auth.jwks.rotation.scheduled.executed`.

   Tests: `SimpleCronMatcherTests` (8) +
   `RotationScheduleAdminControllerTests` (11) +
   `RotationScheduledExecutorServiceTests` (7) +
   `JwtScheduledRotationMetricsTests` (5) = 31 tests.

4. **Replay restoration attempt audit log.**
   Operators chasing a "did this replay restore correctly?"
   question previously had only the W19 integrity-audit
   checksum projection. W21 adds a per-replay restoration-
   attempt trail. New entity `ReplayRestorationAttempt
   (ReplayId, OperatorId, Outcome, DetailMessage,
   AttemptedAtUtc)` with `(ReplayId, AttemptedAtUtc)` +
   `AttemptedAtUtc` indexed.

   Surface:
   `GET /api/admin/replays/{replayId}/restoration-audit` —
   returns the last 10 rows (most-recent-first) and stamps a
   self-record `read` attempt + `replays.restoration.attempt`
   audit row on every call.

   Outcome wire-names: `read`, `restored`, `not-found`,
   `integrity-failure`, `unauthorised`.

   Tests: `ReplayRestorationAuditControllerTests` (10).

5. **JWT validator anomaly counter.**
   New Prometheus counter
   `jwt_validator_anomaly_total{tenant,reason}` exposed via
   `MetricsEndpoint`. Reasons: `clock-skew`,
   `invalid-issuer`, `expired-too-soon`. The validator
   takes the collector as a nullable side-channel so legacy
   tests that wire only the issuer still work
   (no-op recording).

   Anomaly windows:
   - `expired-too-soon`: token `exp` is in the past AND
     `(now - exp) ≤ 300s` (sub-5-minute stale).
   - `clock-skew`: token `iat > now + 60s` (premature
     beyond the 60s tolerance).
   - `invalid-issuer`: token `iss` claim does not match the
     configured `expectedIssuer` arg (opt-in — null issuer
     skips the check).

   New 5-arg `JwtValidationService` constructor threads the
   collector + issuer; the existing 1/2/3-arg overloads
   remain so prior call sites are unmodified.

   Grafana dashboard
   `Observability/dashboards/jwt-validator-metrics.json`
   gains panels 9 (anomalies-by-reason) + 10 (scheduled-
   rotations-by-status), each filterable by the `$tenant`
   dashboard variable.

   Tests: `JwtValidatorAnomalyMetricsTests` (15).

6. **Tournament withdraw-player flow.**
   Admin-gated mid-event withdraw surface. Sets the
   matching `TournamentRegistration.Seed = -1` sentinel so
   downstream Swiss + round-robin pairing services exclude
   the player from future rounds (the W19 forfeit sentinel
   pattern). In-progress / pending matches involving the
   player are dropped so the W21 apply-round + W20
   swiss-pair-next-round surfaces can re-pair them.
   Completed matches are untouched — historical record
   preserved.

   Surface:
   `POST /api/admin/tournaments/{id}/withdraw-player`
   with `X-Admin-Reason` header mandatory and body
   `{ PlayerId, Reason? }`.

   Wire-stable error codes: `tournament-not-found`,
   `player-not-registered`, `already-withdrawn`.

   Audit kind: `tournament.player.withdrawn`.

   Tests: `TournamentWithdrawPlayerControllerTests` (14).

7. **SignalR retention manual-purge surface.**
   Targeted operator surface for post-incident cleanup,
   distinct from the W17 automatic retention sweep. New
   `SignalRManualPurgeMetrics` Prometheus counter
   `signalr_manual_purge_total{tenant}` rendered by
   `MetricsEndpoint`. Per-tenant scoping when `tenant`
   query param is supplied; cross-tenant when omitted.

   Surface:
   `POST /api/admin/signalr/retention-purge?tenant=...&before=ISO8601`
   with `X-Admin-Reason` header mandatory. The cutoff
   `before` must be ISO 8601 and strictly in the past.
   Bulk-deletes from `SignalRSequenceEntries` where
   `CreatedAt < before` (and optionally `TenantId = tenant`).

   Audit kind: `signalr.retention.manual-purge`.

   Tests: `SignalRManualPurgeMetricsTests` (5) +
   `SignalRRetentionManualPurgeControllerTests` (13) = 18
   tests.

## Persistence

- 3-provider migration `Phase_K_W21_RotationScheduleAndReplayRestoration`
  added for Postgres / Sqlite / SqlServer (each `.cs` +
  `.Designer.cs`). Model snapshots refreshed in all three
  provider directories.
- New `DbSet<RotationScheduleEntity>` +
  `DbSet<ReplayRestorationAttempt>` on `AppDbContext` with
  the per-provider column types preserved by EF's snapshot
  generator.

## Audit kinds added (wire-stable)

| Kind | Constant |
|------|----------|
| `tournament.swiss-pairing.applied` | `KindTournamentSwissRoundApplied` |
| `auth.jwks.rotation.scheduled` | `KindAuthJwksRotationScheduled` |
| `auth.jwks.rotation.scheduled.executed` | `KindAuthJwksRotationScheduledExecuted` |
| `tournament.player.withdrawn` | `KindTournamentPlayerWithdrawn` |
| `replays.restoration.attempt` | `KindReplayRestorationAttempt` |
| `signalr.retention.manual-purge` | `KindSignalRManualPurge` |

## Test gate

- 118 new W21 Bishop tests
  (5 + 22 + 31 + 10 + 15 + 14 + 18 = 113 deliverable tests
  plus 5 csproj contract tests for Deliverable 1 surface).
- Full solution: **4754 passed, 1 failed, 0 skipped**
  on `dotnet test src/backend/Mahjong.Autotable.slnx`.
  The single remaining failure is the pre-existing
  `Phase_K_W20/Vasquez/AponeW20ChangelogW20ContractTests.MobilePackageJson_HasVersion_0_29_0_OrForwardStaged`
  test — broken by Apone's W21 mobile/package.json bump
  to `0.30.0` (the Vasquez paired contract pinned the
  substring `0.29.0`). Not in Bishop's lane; flagged for
  Vasquez W21 cleanup.

## Service wiring (Program.cs)

```csharp
builder.Services.AddSingleton<JwtValidatorAnomalyMetrics>();
builder.Services.AddSingleton<JwtScheduledRotationMetrics>();
builder.Services.AddSingleton<SignalRManualPurgeMetrics>();
builder.Services.AddSingleton<SwissApplyRoundService>();
builder.Services.AddHostedService<RotationScheduledExecutorService>();
```

`JwtValidationService` DI factory updated to thread the new
anomaly collector through the W21 5-arg constructor.

## Out of scope / follow-ups

- The W21 mobile/package.json `0.29.0` substring pin in
  `Phase_K_W20/Vasquez/AponeW20ChangelogW20ContractTests.cs`
  is a pre-existing breakage from Apone's W21 commit and
  needs Vasquez W21 cleanup.
- The `SimpleCronMatcher` is intentionally narrow — it
  covers the operator-facing 5/6-field cron grammar without
  pulling in a full Cronos-style dependency. If operators
  start asking for `L`, `W`, `#` extensions, swap to the
  Cronos library; the matcher is wrapped behind a single
  static so the swap is a 30-line patch.
- The `SwissApplyRoundService` projects pairings into
  `TournamentMatch` rows but does not implement the auto-
  start-match flow (which would push the match to
  `in-progress` and notify the table); follow-up wave is
  the natural home for that.
