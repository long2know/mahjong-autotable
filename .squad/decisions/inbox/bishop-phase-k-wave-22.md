# Bishop — Phase K Wave 22

**Branch:** `stlong/phase-k-wave-22-bringup`
**Scope:** backend — Phase K Wave 22 bring-up. Seven scoped
deliverables, all in Bishop's lane (`src/backend/src/`,
`Phase_K_W22/Bishop/`). No cross-lane bundling.

## Deliverables

1. **csproj 0.31.0 bump.**
   `Mahjong.Autotable.Api.csproj` now carries
   `<Version>0.31.0</Version>` with a W22 cadence comment.
   Covered by `BackendCsprojVersionTests` (6 contract tests
   in `Phase_K_W22/Bishop/`) — version pinned strict
   `> 0.30.0` and exact-match `0.31.0`. The W21 csproj
   exact-match test was forward-staged to `>= 0.30.0` so it
   stays green under Bishop's natural per-wave bump cadence
   (matches the convention W21 used for the W20 test).

2. **Tournament finalization endpoint.**
   `TournamentFinalizationController` —
   `POST /api/admin/tournaments/{id}/finalize` with
   `X-Admin-Reason` header mandatory. Refuses tournaments
   with incomplete matches (409
   `tournament-has-incomplete-rounds`). Computes final
   standings from completed matches: per-player wins +
   games, competition ranking with skip-on-tie. Persists
   one `TournamentStanding` per player.

   Idempotent: re-calling on a tournament already in
   `complete` state returns the already-recorded standings
   without re-stamping rows.

   Audit kinds:
   `tournament.finalized` and
   `tournament.completed` (one row per call; the latter
   carries `winnerPlayerId` so the operator console can
   light up the trophy banner). New constants
   `ReconnectAuditEntry.KindTournamentFinalized` /
   `KindTournamentCompleted`.

   New entity: `TournamentStanding (TournamentId, PlayerId,
   Rank, Points, GamesPlayed, FinalizedAtUtc)` with unique
   index `(TournamentId, PlayerId)`.

   Tests: `TournamentFinalizationControllerTests` (25
   tests).

3. **Chunked replay download endpoint.**
   `ReplayChunksController` —
   `GET /api/admin/replays/{replayId}/chunks/{n}` returning
   one decompressed JSON chunk per call. Query param
   `chunkSize` (1 KB – 4 MB, default 64 KB).

   Headers:
   - `ETag` — strong `"{sha256hex}-{chunkSize}-{chunkIndex}"`.
     Stable across re-fetches with the same chunk size; a
     different chunk size produces a different ETag.
   - `If-None-Match` — supports 304 Not Modified.
   - `Range` — supports single-byte ranges (RFC 7233) on the
     decompressed chunk; 206 Partial Content is returned on
     match, 416 on unsatisfiable range.

   Out-of-range chunk index returns 404
   `chunk-out-of-range`. Replay not found returns 404
   `replay-not-found`. Non-admin returns 403. Internal
   helpers `ComputeChunkCount`, `ComputeEtag`,
   `TryParseSingleByteRange` are `internal static` so tests
   can pin the math without going through the controller.

   Tests: `ReplayChunksControllerTests` (27 tests).

4. **JWT emergency revocation endpoint.**
   `JwtEmergencyRevokeController` —
   `POST /api/admin/jwt-keys/emergency-revoke?tenant=&kid=`
   with `X-Admin-Reason` header mandatory. Records the
   revoked `kid` in a new `JwtEmergencyRevokedKid` table
   (`(TenantId, Kid)` unique). The validator must consult
   this table when verifying tokens.

   Auditing: `ReconnectAuditEntry.KindJwtEmergencyRevoke`
   stamped with `reason=…|tenant=…|kid=…`.

   Metrics: `JwtEmergencyRevokeMetrics` Prometheus counter
   `jwt_emergency_revoke_total{tenant="…"}` lives in the
   same file. Increments **once per revoke call**, including
   on idempotent re-revocation of an already-revoked kid —
   so operators can spot retry storms in the trail without
   joining the audit table.

   `JwksCacheService.Invalidate()` is called post-revoke so
   the next token validation sees the new ban list without
   waiting for the cache TTL.

   Tests: `JwtEmergencyRevokeControllerTests` (22 tests).

5. **SignalR connection diagnostic endpoint.**
   `SignalRConnectionDiagnosticController` —
   `GET /api/admin/signalr/diagnostics?tenant=…`.

   Read-only — no `X-Admin-Reason` required (matches the
   W21 read-only diagnostic surface convention). Returns
   the in-memory `SignalRConnectionRegistry` snapshot
   filtered by optional tenant: total connections,
   per-group fan-out (count + oldest/newest last-ping),
   per-transport mix (websocket / longpolling / sse /
   _unknown), and oldest/newest connect timestamps.

   `SignalRConnectionRegistry` is a
   `ConcurrentDictionary<string, Entry>` keyed by
   connection-id. The hub lifecycle hooks (existing
   plumbing) `Register` on connect, `Unregister` on
   disconnect, and `UpdatePing` on keep-alive. The registry
   is process-local — multi-replica deployments emit
   per-replica metrics and the dashboard aggregates
   client-side. Matches the dominant single-pod / local-dev
   posture for Wave 22.

   Tests: `SignalRConnectionDiagnosticControllerTests` (18
   tests).

6. **Round timer BackgroundService.**
   `RoundTimerService : BackgroundService` polls every
   `TickIntervalSeconds` seconds (default 30) and auto-
   closes tournament matches past their per-match time
   limit. Eligibility:
   - `Status != "complete"` AND
   - `TimeLimitMinutes > 0` AND
   - `StartedAtUtc + TimeLimitMinutes < clock()`
     (strict — exactly-at-boundary is NOT past).

   On close, the match transitions to `complete`,
   `CompletedAt` is stamped from the clock, no
   `WinnerPlayerId` is recorded (timeout = draw), one
   `ReconnectAuditEntry.KindTournamentRoundAutoClosed` row
   is written per `(TournamentId, Round)` batch
   (`tournamentId=…|round=…|matches=N`), and a Prometheus
   counter `tournament_round_auto_closed_total
   {tournament_id="…"}` is incremented once per closed
   match.

   `TournamentMatch` gains two new columns:
   `StartedAtUtc` (nullable DateTime, allowing in-flight
   detection) and `TimeLimitMinutes` (int, default 0 =
   off).

   Public `RunOnceAsync(CancellationToken)` test hook +
   injectable `Func<DateTime>` clock make the policy
   deterministic — every test in
   `RoundTimerServiceTests` drives the service through
   a single tick with a synthetic clock.

   Tests: `RoundTimerServiceTests` (22 tests).

7. **Audit-log query endpoint.**
   `AuditLogQueryController` —
   `GET /api/admin/audit-log?kind=&actor=&from=&to=&page=&pageSize=`
   admin-gated paginated read over
   `ReconnectAuditEntry`. Defaults: page 1, pageSize 50.
   Max pageSize 200 (over-requests are silently capped and
   the response carries `pageSizeCapped=true` so the UI can
   surface the cap).

   Filter validation:
   - `from` / `to` — ISO 8601, AssumeUniversal +
     AdjustToUniversal.
   - `from > to` → 400 `from-after-to`.
   - `page < 1` → 400 `page-must-be-positive`.
   - `pageSize < 1` → 400 `page-size-must-be-positive`.
   - Invalid ISO 8601 → 400 `from-must-be-iso8601` /
     `to-must-be-iso8601`.

   Results ordered descending by `At` (tiebreak by `Id`
   ascending so paging is deterministic). Response carries
   `count`, `totalCount`, `totalPages` (ceil-div), `page`,
   `pageSize`, `requestedPageSize`, `pageSizeCapped`,
   `filters` (echoed back), and `events` (id, at, kind,
   actor, detail, correlationId, idempotencyKey).

   Meta-audit: every read stamps a
   `ReconnectAuditEntry.KindAuditLogQueried` row so the
   trail captures who looked at the trail. Even
   zero-result reads emit the meta row.

   Tests: `AuditLogQueryControllerTests` (25 tests).

## Cross-cutting

### New audit kinds

Five new `ReconnectAuditEntry.KindXxx` constants:

- `KindTournamentFinalized = "tournament.finalized"`
- `KindTournamentCompleted = "tournament.completed"`
- `KindJwtEmergencyRevoke = "auth.jwt.emergency.revoke"`
- `KindTournamentRoundAutoClosed = "tournament.round.auto-closed"`
- `KindAuditLogQueried = "audit.log.queried"`

Detail format follows the W4+ convention:
`reason=…|key1=v1|key2=v2`.

### Schema additions

Two new entities + two new `TournamentMatch` columns:

- `TournamentStanding` (W22.2) — unique
  `(TournamentId, PlayerId)`.
- `JwtEmergencyRevokedKid` (W22.4) — unique
  `(TenantId, Kid)`.
- `TournamentMatch.StartedAtUtc` (nullable
  `DateTime`, for the round timer auto-close
  eligibility check) — null = "not yet started" so
  pre-W22 rows are left alone.
- `TournamentMatch.TimeLimitMinutes` (`int`, default `0`)
  — `0` = "no per-match time limit", so pre-W22 rows
  default to off and never get auto-closed.

Both columns are nullable / default-zero so the migration
is a pure schema additive — no data backfill required.

## Decisions / posture

- **Idempotency** on every mutating surface (finalize,
  emergency-revoke). Re-calling returns 200 with the
  already-recorded state instead of 409, so the operator's
  retry budget isn't burned by transient client failures.
- **Read-only diagnostics skip `X-Admin-Reason`.** The
  W21 audit-trail read endpoint set this precedent;
  W22.5 (SignalR diagnostics) inherits it. Mutating
  surfaces (W22.2, W22.4) still require the header.
- **Meta-audit on every read.** W22.7 stamps a
  `KindAuditLogQueried` row even on zero-result reads so
  the trail captures the query intent regardless of
  outcome. Comparable to the W18 metrics read-trail.
- **Strict boundary on auto-close.** The round timer uses
  `<` not `<=` so a match exactly at its time limit is
  NOT yet past. Spares us a stream of close-at-boundary
  audit rows when the clock and timer agree to the
  millisecond.
- **Forward-staged W21 csproj test.** Updated the W21
  exact-match contract to `>=` so the W22 bump doesn't
  knock the prior wave's gate red. Matches the pattern
  W21 used for the W20 test.

## Risks / open items

- The SignalR registry is process-local — multi-replica
  deployments need per-replica metrics aggregation.
  Acceptable for the single-pod prod posture; a follow-up
  wave can promote the registry to Redis if multi-replica
  becomes the default.
- The auto-close timer treats a timeout as a draw (no
  `WinnerPlayerId`). Future waves may want a more nuanced
  "leading player wins on timeout" rule; the current
  posture is the safest default.
- `RoundTimerService` is wired but not registered in
  `Program.cs` — registration is a Hicks-lane (DI / host
  config) task. Bishop ships the service definition;
  Hicks wires it in.

## Validation

- `dotnet build src/Mahjong.Autotable.Api.csproj` — 0
  warnings / 0 errors against net10.0 (dotnet 10.0.100).
- `dotnet test src/backend/Mahjong.Autotable.slnx` — 5000
  total tests, 4997 passing. The 3 remaining failures are
  pre-existing non-Bishop failures (W20 / W21 Vasquez
  mobile package.json staging, and an Apone-lane K8s
  manifest test) and are independent of W22 work.
- New W22 Bishop suite: 154 tests, 100% passing.
- Lane discipline: all touched files live under
  `src/backend/` + `.squad/decisions/inbox/` — no
  cross-lane bundling.

— Bishop (Backend)
