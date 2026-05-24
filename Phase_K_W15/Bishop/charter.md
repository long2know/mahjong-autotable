# Phase K Wave 15 — Bishop (Backend) charter

> Companion to the
> [Vasquez forward-stage marker](README.md) in the same
> directory. This file carries the Bishop-authored design
> rationale for the W15 backend bring-up.

## Scope

Seven surfaces. All land under Bishop-lane paths
(`src/backend/**`, the documented `docs/{…}.md` subset,
`Phase_K_W15/Bishop/**`, and migrations under
`Persistence/Migrations/{Sqlite,Postgres,SqlServer}`).

1. **Replay blob streaming** — `GET /api/replays/{replayId}/blob`
   with RFC 7233 single-range support, suffix ranges, 416 on
   malformed / multi-range. The W12 metadata-only `GET` and W14
   listing both stay. See `docs/replay-streaming.md`.

2. **Per-tenant JWKS rotation** — `PerTenantJwksRotationPolicies`
   table keyed by `TenantId`, opt-in toggle
   (`JwksRotation:PerTenant:Enabled`, default false),
   `RotationStartUtc` + `RotationCompleteUtc` typed as
   `DateTimeOffset` (W14 → W15 type widening). InMemory + Ef
   store implementations. Migrations land in all three EF
   providers. Validator integration deferred to W16. See
   `docs/per-tenant-jwks.md`.

3. **DbSerial completion** — Apply `[Collection("DbSerial")]`
   to the two remaining W9 Bishop tests
   (`EfCommentaryUsageMeterTests`, `IdempotencyStoreContractTests`)
   identified by the W14 Vasquez migration memo. See
   `db-serial-completion.md`.

4. **Tournament page-size histogram** —
   `tournament_query_duration_seconds{endpoint, page_size_bucket}`
   Prometheus histogram. Wired into the three W14 paginated
   endpoints (bracket-records, replay-list,
   spectator-audit-query) + rendered through the existing
   `/metrics` endpoint. See `docs/bracket-shape.md §6`.

5. **Commentary cost forecast** —
   `GET /api/commentary/cost/forecast?days=<n>`. Admin-gated
   (401 → 403 → 200). Linear extrapolation by days-elapsed in
   the current calendar month; confidence bucket on
   `daysOfDataUsed`. See `docs/commentary-llm.md §7`.

6. **Spectator handoff audit retention sweep** — hosted
   background service (`SpectatorHandoffAuditRetentionSweep`)
   running every 5 minutes by default. See
   `docs/spectator-handoff.md §5`.

7. **Replay store retention sweep** — hosted background service
   (`ReplayStoreRetentionSweep`) running hourly, evaluating
   `CompletedAt < utcNow - RetentionDays` against the current
   options (operator-dialled retention is honoured on the next
   tick). The W12 `ExpiresAt`-based sweep runs alongside. See
   `docs/replay-by-id.md §4`.

## Cross-cutting decisions

### Why DateTimeOffset for per-tenant rotation?

The W14 path used `DateTime` for `RotationStartUtc` +
`RotationCompleteUtc`. Operators scheduling rotations in their
local timezone saw the offset stripped on serialisation —
correct semantically but misleading at the dashboard render.
W15 widens to `DateTimeOffset` so a `2026-01-01T09:00:00-08:00`
input persists verbatim.

### Why a second retention sweep on replays?

The W12 `ReplayRetentionSweepService` evaluates `ExpiresAt`
(computed once at insert). A runtime change to
`Replays:RetentionDays` does not retro-apply to existing rows.
The W15 `ReplayStoreRetentionSweep` evaluates `CompletedAt`
against the **current** retention each tick, so dialling down
retention takes effect on the next tick rather than waiting for
the old expiry. The two sweeps are orthogonal — the second pass
over a row already deleted by the first is a no-op.

### Why an opt-in toggle for per-tenant JWKS?

Single-tenant deployments would see no benefit from the new
table and a small perf cost on every JWT validation. The W15
surface lands the **table + opt-in toggle + store seam only** —
validator integration is deferred to a future wave so the
surface boundary is reviewable in isolation. Multi-tenant
operators flip `JwksRotation:PerTenant:Enabled = true` and
populate the table; the validator wiring will land separately.

### Why a side-channel latency collector?

The tournament-scale latency histogram is intentionally
side-channel — endpoints optionally resolve the collector from
DI and record observations through `ObserveDuration`. A test
fixture that wires only the controller still works (the
collector is null and the recording is a no-op). This avoids
forcing every consumer to thread the collector through the
constructor.

### Why a 5-minute audit sweep cadence?

The spectator handoff audit table is the security-review trail
for `spectator:` tokens. A leaked token's audit row should
vanish quickly once retention is dialled down (the cadence is
the upper bound on the post-dial-down delay). 5 minutes is
short enough that a 30-minute incident window comfortably
covers the eviction.

## Out of scope (deferred to W16+)

* Per-tenant JWKS rotation **validator integration** — the
  table + opt-in toggle + store seam land in W15; the JWT
  validator wiring to consult the per-tenant row before the
  global window is deferred.
* `multipart/byteranges` for the replay blob endpoint —
  single-range only in W15 (a multi-range request returns 416).
* HTTP-level rate limiting on `GET /api/replays/{id}/blob` —
  uses the existing `ApiPolicy` (same as the W12 metadata GET).
