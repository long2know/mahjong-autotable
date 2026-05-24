# Bishop — Phase K Wave 16

**Branch:** `stlong/phase-k-wave-16-bringup`
**Scope:** backend — Phase K Wave 16 bring-up. Seven scoped
deliverables, all landed:

1. **Per-tenant JWKS rotation validator + admin controller** —
   The W15 surface landed the `PerTenantJwksRotationPolicies`
   table + the store seam but stopped short of an actual gate.
   W16 lands `PerTenantJwksRotationValidator` (six verdict
   kinds: `ToggleDisabled`, `NoPolicy`, `PolicyFresh`,
   `WithinOverlapWindow`, `Stale`, `StoreMissing`) +
   `EnforceSigningAsync` (throws
   `PerTenantRotationStaleException` when the policy has aged
   past its overlap window). Paired admin controller
   (`PerTenantRotationAdminController`) exposes
   `GET / POST / PUT / DELETE /api/admin/jwks-rotation/per-tenant`
   with the canonical 401 → 403 → 503 → 200/201/204 auth ladder.
   Every successful write emits a `ReconnectAuditEntry`
   (`auth.jwks.per-tenant.{created|updated|deleted}`). New
   per-row column `PerTenantJwksRotationPolicy.OverlapWindowDays`
   + `PerTenantJwksRotationOptions.DefaultOverlapDays`. Validator
   constant `DefaultOverlapDays = 7`. See
   `docs/per-tenant-jwks-rotation.md`.

2. **`DateTimeOffset` widening on JwtStagedRotationPolicy** —
   The W12 staged rotation policy carried `DateTime`-only
   surfaces. W16 adds the `DateTimeOffset` overloads of
   `IsWithinOverlapWindow` + `RemainingOverlapDays` and new
   properties `RotationStartUtcOffset` +
   `OverlapWindowEndsAtOffset` so multi-tenant call sites
   (which already speak `DateTimeOffset` from W15) don't have
   to round-trip through `DateTime`. The W12 `DateTime`
   members are preserved verbatim for backward compatibility.

3. **Grafana dashboard JSON** — Ships at
   `src/backend/src/Mahjong.Autotable.Api/Observability/dashboards/tournament-query-duration.json`.
   Seven panels: p50 / p95 / p99 over 5-minute windows, request
   rate per endpoint, p99 per-endpoint breakdown, 24h total
   queries, and a 5m/30m/1h burn-rate stat. Templating
   variables for `endpoint` and `page_size_bucket` (matching
   the W15 metric labels). Alert annotation
   `tournament-query-p99-over-500ms` (fires after 10 min of
   p99 > 500ms). The dashboard JSON is copied to the test
   output directory so contract tests can assert on it without
   a fragile project-relative path.

4. **Admin-gated CRUD surface for per-tenant policy** — See
   item 1 above. Carved out as a separate deliverable in the
   directive because the controller carries its own auth +
   audit + validation contract independent of the validator.

5. **SignalR sequence SLO document** —
   `docs/signalr-sequence-slo.md` formalises the
   **99.95% / 21.6 minutes-per-month** target for the W6→W15
   sequence-replay surface. PromQL good-event ratio,
   burn-rate expressions, paired fast-burn / slow-burn alerts
   (Google SRE Workbook 2-window structure), runbook for the
   on-call engineer, and a wave-history table connecting the
   W14 `signalr_seq_*` metrics to this SLO.

6. **Per-tenant replay retention** — New table
   `ReplayRetentionPolicies` keyed by tenant id;
   `ReplayRetentionPolicy.RetentionDays` per-tenant override.
   `IReplayRetentionPolicyStore` seam with InMemory + EF
   implementations. New `IReplayStore.SweepWithPerTenantPolicyAsync`
   method consulted by the W15 hourly sweep when an
   `IReplayRetentionPolicyStore` is registered (the W15
   global-only sweep path is preserved when no policy store is
   wired). `ReplayRecord.TenantId` nullable column lets the
   sweep route each row to its tenant policy with a fallback
   to the global `Replays:RetentionDays` window.

7. **Commentary cost budget hard-gate (HTTP 402)** —
   `CommentaryCostBudgetEnforcer` reads
   `CommentaryCostBudget.Evaluate(...)`. When the verdict is
   `BudgetState.Exhausted` the enforcer rejects the request
   with HTTP 402 Payment Required and the canonical reason
   `commentary-cost-budget-exhausted`. Admin override via the
   `X-Cost-Budget-Override: 1` request header AND the
   `Commentary:CostBudget:AdminOverride` toggle (default true)
   bypasses the gate and emits an `AdminOverride` verdict the
   audit dashboard can count separately. Healthy + Warning
   states pass through. Wired into
   `CommentaryController.Trigger`.

## Cross-cutting decisions

* **Validator is side-channel, not threaded through
  `JwtIssuingService`.** The W14 issuing service has no
  tenant parameter and rewriting its surface would balloon
  the W16 diff. Instead the multi-tenant call site (which
  doesn't exist yet) resolves the validator from DI and
  calls `EnforceSigningAsync` before invoking the issuing
  service. The validator's `ValidatorEnabled` property
  lets call sites short-circuit cleanly when the toggle is
  off.

* **Admin controller emits a soft-delete sentinel row.** The
  W15 `IPerTenantJwksRotationStore` contract does not expose
  a hard-delete method. Rather than widen the store seam in
  W16 — which would touch every implementation including
  the EF impl + Sqlite test wiring — the admin controller's
  DELETE handler upserts a sentinel row with
  `RotationCompleteUtc = utcNow` and `OverlapWindowDays = 0`,
  which the validator immediately treats as stale. A future
  wave can replace this with a proper hard-delete + a
  `KindHardDeleted` audit kind. The current
  `auth.jwks.per-tenant.deleted` audit row remains
  observable.

* **402 vs 429 for cost overruns.** The W9 enforcer threw
  429 Too Many Requests when the monthly token cap was
  exceeded. W16's USD-based cap is semantically different —
  it's a billing decision, not a rate-limit decision — so
  the wire status flips to 402 Payment Required (RFC 7231
  §6.5.2). The 429 path is preserved for the token-count
  cap; both gates exist in the controller and apply
  independently.

* **Per-row override precedence.** Three layers, validator
  applies them in order:
  1. `PerTenantJwksRotationPolicy.OverlapWindowDays > 0` → use the row.
  2. `PerTenantJwksRotationOptions.DefaultOverlapDays > 0` → use the option.
  3. Else → `PerTenantJwksRotationValidator.DefaultOverlapDays` = 7.

* **`ReplayRetentionPolicy` has no admin UX in this wave.**
  Operator-facing CRUD for replay-retention policy is
  carved out for a future wave; the W16 surface is the
  store + the sweep wiring only. The `ReplayRetentionPolicy`
  table is populated through direct EF writes (e.g. via a
  migration seed) for the initial rollout.

## Verification

* `dotnet build src/backend/Mahjong.Autotable.slnx --nologo` →
  0 warnings, 0 errors.
* `dotnet test src/backend/Mahjong.Autotable.slnx --nologo
  --no-build --filter "FullyQualifiedName~Phase_K_W16.Bishop"`
  → **172 passed / 0 failed / 0 skipped**.
* Full backend gate: see CHANGELOG section for the wave.

## Files (Bishop lane)

* `src/backend/src/Mahjong.Autotable.Api/Auth/PerTenantJwksRotationValidator.cs` (new)
* `src/backend/src/Mahjong.Autotable.Api/Auth/PerTenantRotationAdminController.cs` (new)
* `src/backend/src/Mahjong.Autotable.Api/Auth/PerTenantJwksRotationPolicy.cs`
  (modified — `OverlapWindowDays`, `DefaultOverlapDays`)
* `src/backend/src/Mahjong.Autotable.Api/Auth/JwtStagedRotationPolicy.cs`
  (modified — `DateTimeOffset` overloads)
* `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryCostBudgetEnforcer.cs` (new)
* `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryOptions.cs`
  (modified — `CostBudget.AdminOverride`)
* `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryController.cs`
  (modified — 402 wiring)
* `src/backend/src/Mahjong.Autotable.Api/Replays/ReplayRetentionPolicy.cs` (new)
* `src/backend/src/Mahjong.Autotable.Api/Replays/ReplayRecord.cs`
  (modified — `TenantId` column)
* `src/backend/src/Mahjong.Autotable.Api/Replays/ReplayStore.cs`
  (modified — `SweepWithPerTenantPolicyAsync` + sweep wiring)
* `src/backend/src/Mahjong.Autotable.Api/Data/AppDbContext.cs`
  (modified — new DbSet + indexes)
* `src/backend/src/Mahjong.Autotable.Api/Program.cs`
  (modified — wire W16 services)
* `src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj`
  (modified — copy `Observability/dashboards/*.json`)
* `src/backend/src/Mahjong.Autotable.Api/Observability/dashboards/tournament-query-duration.json` (new)
* `docs/signalr-sequence-slo.md` (new, shared lane)
* `docs/per-tenant-jwks-rotation.md` (new, shared lane)
* 8 new contract test files under
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W16/Bishop/`.

## Wave 17 hand-off

* Wire the validator into the actual multi-tenant
  `JwtIssuingService` path (W16 ships the seam; W17
  consumes it).
* Add `IPerTenantJwksRotationStore.DeleteAsync` + the
  paired `KindHardDeleted` audit kind so the admin
  controller's DELETE handler stops using the sentinel-row
  workaround.
* Admin CRUD surface for `ReplayRetentionPolicy`.
* Wire commentary admin override into the existing
  `X-Admin-Reason` header used by the W14 audit dashboard.
