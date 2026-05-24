# Bishop — Phase K Wave 17

**Branch:** `stlong/phase-k-wave-17-bringup`
**Scope:** backend — Phase K Wave 17 bring-up. Seven scoped
deliverables, all landed:

1. **JWKS validator wired into JwtIssuingService + Prometheus
   counter** — The W16 surface landed
   `PerTenantJwksRotationValidator.EnforceSigningAsync` as a
   side-channel gate but the issuing call site never invoked
   it (the validator was reachable only through the admin
   controller). W17 lands `JwtIssuingService.IssueForTenantAsync`
   which calls the validator on every per-tenant token issue
   and short-circuits before signing when the verdict is
   `Stale` or `StoreMissing`. Each block stamps a new
   `JwtIssueBlockedMetrics` Prometheus counter
   (`jwt_issue_blocked_total{reason}` with wire-stable
   labels `stale_per_tenant_policy` / `per_tenant_store_missing`)
   and a `ReconnectAuditEntry`
   (`auth.jwt.issue.blocked.stale_per_tenant_policy`). The
   collector is rendered by `MetricsEndpoint` on every scrape
   (HELP + TYPE preambles emit unconditionally so the schema
   is visible even at zero counts).

2. **`DeleteAsync` on PerTenantJwksRotationPolicies** — W16's
   admin controller stamped a "deleted" sentinel marker via
   `UpsertAsync` (the store had no `DeleteAsync` seam). W17
   adds `IPerTenantJwksRotationStore.DeleteAsync` + both impls
   (InMemory + EF). The admin controller's `Delete` now calls
   the real hard-delete path. The W16 backward-compat audit
   kind `auth.jwks.per-tenant.deleted` is preserved verbatim
   so existing W16 tests still pass; a new
   `auth.jwks.per-tenant.hard-deleted` constant is introduced
   for future call sites that want to distinguish the two.

3. **Admin CRUD surface for ReplayRetentionPolicy** — W16
   landed the `ReplayRetentionPolicies` table + the store seam
   without an operator UX. W17 ships
   `ReplayRetentionAdminController` at
   `/api/admin/replays/retention` with full
   `GET / POST / PUT / DELETE` CRUD, the canonical
   401 → 403 → 503 → 200/201/204 auth ladder, mandatory
   `X-Admin-Reason` header on every write (empty / whitespace
   → 400), and per-write `ReconnectAuditEntry` rows
   (`replays.retention.{created|updated|deleted}`). Audit
   `Detail` field captures `"{tenantId}|{reason}"` verbatim.

4. **Commentary `X-Admin-Reason` header unification** —
   `CommentaryController` already accepted the legacy
   `X-Cost-Budget-Override: 1` header for the budget-cap
   bypass but the dashboard had no operator-supplied reason
   field. W17 lands the unified `X-Admin-Reason` convention
   (the same header used by both retention admin surfaces and
   the per-tenant rotation admin surface). `ResolveAdminOverride`
   returns a triple `(engaged, reason, badEmptyReason)` so a
   request that supplies the header with an empty value fails
   closed (400) rather than silently engaging the override.
   Audit row `commentary.admin.override` is stamped on every
   engaged override (kind constant exported off
   `ReconnectAuditEntry`).

5. **`DateTimeOffset` widening round 2** — Round 1 (W16)
   covered `JwtStagedRotationPolicy`. W17 round 2 covers
   `PlayerAuthIdentity`, `PlayerAuthSession`,
   `ReconnectAuditEntry`, and `SignalRSequenceEntry` via the
   extension-based `DateTimeOffsetWideningR2` projections
   (zero schema impact — the underlying `DateTime` columns
   stay put). New `CacheAgeOffset` helper clamps negative
   deltas to zero so a cache miss never reports a negative
   age. Wave tag: `phase-k-w17-r2`. New
   `[NotMapped] CreatedAtOffset` / `UpdatedAtOffset`
   projections on `ReplayRetentionPolicy`,
   `PerTenantJwksRotationPolicy`, and `SignalRRetentionPolicy`
   give the admin controllers offset-aware JSON projections
   for free.

6. **Tournament-query-duration Prometheus alerts** — Ships
   `src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`
   carrying two rails:
   - `TournamentQueryDurationP99HighPage` (severity `page`,
     p99 > 500ms sustained 5m).
   - `TournamentQueryDurationP95HighTicket` (severity
     `ticket`, p95 > 250ms sustained 15m).

   Both alerts carry `team: bishop` + `wave: phase-k-w17`
   labels and a `runbook_url` pointing at the new
   `docs/tournament-query-duration-runbook.md` operator
   runbook (anchors `#p99-page` + `#p95-ticket`). The
   apone-owned Prometheus copy under `infra/` is generated
   from this file as part of the W18 fold — this file is the
   authoritative source-of-truth in Bishop's lane so the
   contract tests can pin the shape + thresholds in CI.

7. **SignalR per-tenant retention** — W14 landed the global
   `SignalRSequenceStoreOptions.SequenceRetention` knob.
   Tournament operators flagged that the global default is
   too aggressive for free-tier (which doesn't need long
   reconnect windows) and too tight for enterprise (which
   wants 24h+). W17 lands:
   - `SignalRRetentionPolicy` entity +
     `ISignalRRetentionPolicyStore` (InMemory + EF impls)
     mirroring the W16 replay-retention shape.
   - `TenantId` column on `SignalRSequenceEntry` so the sweep
     can group by tenant. Empty `TenantId` rows follow the
     global fallback (back-compat).
   - `ISignalRSequenceStore.SweepExpiredWithPerTenantPolicyAsync`
     consulted by the existing
     `SignalRSequenceRetentionSweep` background loop.
   - `SignalRRetentionAdminController` at
     `/api/admin/signalr/retention` — mirrors the replay
     retention admin controller exactly.

   Audit kinds (`signalr.retention.{created|updated|deleted}`)
   stamp on every successful write.

## Schema / migration

A single migration —
`Phase_K_W17_AdminCrudAndPerTenantRetention` — is generated
across all three providers (Sqlite, Postgres, SqlServer). It
captures:

- W17 deltas: `SignalRRetentionPolicies` table, `TenantId`
  column on `SignalRSequenceEntries`, index on the new column.
- W16 schema drift the previous wave shipped without
  migrating: `OverlapWindowDays` column on
  `PerTenantJwksRotationPolicies`, `ReplayRetentionPolicies`
  table, `TenantId` column on `Replays` (+ index).

Migration timestamps:

- Sqlite: `20260524063003`
- Postgres: `20260524063014`
- SqlServer: `20260524063026`

DbSerial bookkeeping for the wave: 26th (this wave). The W18
fold will pick up the 27th serial.

## Hand-offs to W18 (cross-lane)

- **Apone (infra):** Promote
  `src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`
  into the prometheus-operator copy under `infra/prometheus/alerts/`.
  No additional shape massaging required — the file is already
  Prometheus-operator-shaped.
- **Hicks (frontend):** Wire up the operator UI for the
  three admin surfaces (per-tenant JWKS, replay retention,
  signalr retention). The contract is identical across all
  three (auth ladder + `X-Admin-Reason`) so a single shared
  component should suffice.
- **Vasquez (game logic):** When per-tenant `TenantId`
  begins flowing through the SignalR seam, sweep callers
  need to stamp the tenant on `SignalRSequenceEntry.TenantId`
  at create time. Empty string is the safe back-compat
  default.

## Lane-discipline notes

- All migrations live under
  `src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/`
  (covered by the `src/backend/src/` prefix in Bishop's lane).
- The tournament alerts YAML is placed at
  `src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/`
  (stays in Bishop's lane). The apone-owned promotion copy
  under `infra/prometheus/alerts/` is intentionally NOT
  touched by this commit — that's the W18 fold's job.
- `docs/tournament-query-duration-runbook.md` is a
  cross-lane shared file under `docs/`.
- Commit pushed under the `flock .work/squad-git-lock` lock
  with explicit `-c user.name="Bishop (Backend)"` /
  `-c user.email="bishop@squad.mahjong"` overrides (no
  `git config user.name` mutation of the worktree).

## Test gate

The W17 backend wave is gated at ≥3800 tests. The 11 new
test files under
`src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W17/Bishop/`
contribute 185+ contract tests covering the surface above.

— Bishop (Backend), Phase K Wave 17
