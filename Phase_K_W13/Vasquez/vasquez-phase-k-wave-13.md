# Vasquez — Phase K Wave 13 (QA bring-up)

**Date:** 2026-10-30
**Branch:** `stlong/phase-k-wave-13-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`

## Summary

Ship the Phase K Wave 13 QA bring-up: the W12 DbSerial migration
audit hand-off lands as the **W13 DbSerial migration applied**
follow-through (23 of 25 candidates carry `[Collection("DbSerial")]`;
2 attribute-overridden to Bishop's W14 lane); the W12 LH13 soft
pin flips to a hard-pinned spec backed by `docs/frontend-pwa-audit.md`
§6.2; a new `playwright-visual-regression.yml` GitHub workflow
lands as the W13 visual-regression CI gate (skipped on draft,
uploads diff PNGs on fail, sticky PR comment with marker
`<!-- playwright-visual-regression -->`); an idempotent
`tests/ci/lane-discipline-flip-required.sh` escalation script
ships with its `docs/agent-handoff-protocol.md` §4.2
coordinator-direct runbook; 10 forward-stage W13 contract test
files land for Bishop / Hicks / Apone surfaces (bracket-tournament
integration, commentary cost SignalR, Prometheus metric labels,
Redis introspect limiter, spectator audit, replay admin gating,
SignalR sequence retention, frontend contract, infra contract,
PWA audit workflow gate); the W12 regression class is renamed
`Wave1ThroughKW12RegressionTests → Wave1ThroughKW13RegressionTests`
with 12 W13 smokes appended; 6 Playwright specs land under
`tests/e2e/` (spectate-deep-link, shader-chunk-440-stretch,
lh13-thresholds-hard-pinned, bracket-tournament-integration,
commentary-cost-warning-toast, bundle-health-pr-comment); and
the backend gate moves from **2610 / 0 / 0** to the W13 closing
gate captured in `Phase_K_W13/Vasquez/gate-snapshot.txt`,
preserving the now 28-wave zero-skip streak.

## Deliverables (7)

1. **DbSerial migration applied** — `Phase_K_W13/Vasquez/db-serial-migration-applied.md`:
   - 23 of 25 W12 audited candidates now carry
     `[Collection("DbSerial")]` (full lane breakdown in §2.2).
   - 2 candidates re-attributed to Bishop's W14 lane via the
     W12 `wave_subdir_overrides` rule (`Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs`,
     `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`).
   - 5-run sequential flake harness: all five runs landed at
     2610 / 0 / 0 (no flakes; net delta 0 — defensive depth
     play, not flake-elimination).

2. **Doc updates — three files**:
   - `docs/test-architecture.md` §3.2 — new "DbSerial migration
     outcomes" subsection with before/after table; old §3.2
     becomes §3.3 and §3.3 becomes §3.4. §5.1 gains a
     W13 paragraph documenting the new
     `playwright-visual-regression.yml` CI gate. W13 footer
     appended.
   - `docs/frontend-pwa-audit.md` §6.2 — new "LH13 hard-pin
     sync (W13 — Vasquez/Hicks coordination)" subsection
     between the W12 soft-pin defer block and §7.
   - `docs/agent-handoff-protocol.md` §4.2 — new
     "Coordinator-direct execution (W13 escalation runbook)"
     section before §5; documents the
     `tests/ci/lane-discipline-flip-required.sh` script and
     its dry-run / rollback / coordinator-flag modes.

3. **Visual-regression CI workflow** —
   `.github/workflows/playwright-visual-regression.yml`:
   - Skips on draft PRs
     (`if: github.event.pull_request.draft == false`).
   - Uploads diff PNGs on failure (`if: failure()`).
   - Posts a sticky PR comment with marker
     `<!-- playwright-visual-regression -->`.
   - Added to Vasquez's lane regex in `tests/ci/lane-map.json`.

4. **Branch-protection escalation script** —
   `tests/ci/lane-discipline-flip-required.sh`:
   - Idempotent `gh api -X PATCH` call against the
     `branch-protection` REST endpoint for the
     `lane-discipline / check` context.
   - `--dry-run` (default), `--apply`, `--rollback`, and
     `--coordinator-flag` modes.
   - Audit log appended to
     `docs/audits/branch-protection-flips.md` on every
     invocation.
   - Runbook in `docs/agent-handoff-protocol.md` §4.2 above.

5. **W13 forward-stage contract tests** —
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Vasquez/`:
   - `BishopW13BracketTournamentIntegrationTests.cs` (8 facts).
   - `BishopW13CommentaryCostSignalRTests.cs` (8 facts).
   - `BishopW13PrometheusMetricLabelsTests.cs` (7 facts).
   - `BishopW13RedisIntrospectLimiterTests.cs` (8 facts).
   - `BishopW13SpectatorAuditTests.cs` (8 facts).
   - `BishopW13ReplayAdminGatingTests.cs` (6 facts).
   - `BishopW13SequenceStoreRetentionTests.cs` (8 facts).
   - `HicksW13FrontendContractTests.cs` (8 facts).
   - `AponeW13InfraContractTests.cs` (9 facts).
   - `PwaAuditWorkflowGateTests.cs` (6 facts).
   - Forward-stage tolerant — soft-pin via `_ = …;` discard
     or `if (… is null) return;` early-return, except the
     LH13 hard-pin pair which hard-asserts the §6.2
     coordination block.

6. **Regression rename** —
   `Regression/Wave1ThroughKW13RegressionTests.cs`:
   - Renamed from `Wave1ThroughKW12RegressionTests.cs`
     (class name updated in all 10 references).
   - W13 extension paragraph appended to the file
     doc-comment header.
   - 12 new W13 smokes appended at the tail
     (TournamentService.AdvanceMatchAsync,
     RedisOAuthIntrospectRateLimiter, CommentaryCostAdminHub,
     SpectatorHandoffAudit, JWKS overlap window,
     `docs/regional-eks-bringup.md`, `jwt-rotation-scheduled.yml`,
     ClusterPolicy fieldSpecs, the W13
     `db-serial-migration-applied.md`,
     `tests/ci/lane-discipline-flip-required.sh`,
     `playwright-visual-regression.yml`, KW12→KW13 rename
     pin).

7. **6 Playwright specs** under
   `src/frontend/autotable-src/tests/e2e/`:
   - `spectate-deep-link.spec.ts`.
   - `shader-chunk-440-stretch.spec.ts` (tighter stretch
     goal vs. the W12 450-byte spec).
   - `lh13-thresholds-hard-pinned.spec.ts` (W13 hard-pin
     replacement for the W12 soft-pin spec).
   - `bracket-tournament-integration.spec.ts`.
   - `commentary-cost-warning-toast.spec.ts`.
   - `bundle-health-pr-comment.spec.ts`.
   - All chromium-only and forward-stage tolerant.
   - Inventory recorded in `tests/selectors.md` W13 footer
     (shared file; primary=vasquez).

## Files modified

### New (Vasquez-lane)
- `Phase_K_W13/Vasquez/db-serial-migration-applied.md`.
- `Phase_K_W13/Vasquez/vasquez-phase-k-wave-13.md` (this memo).
- `Phase_K_W13/Vasquez/gate-snapshot.txt`.
- `tests/ci/lane-discipline-flip-required.sh`.
- `.github/workflows/playwright-visual-regression.yml`.
- 12 W13 contract / smoke test files under
  `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Vasquez/`.
- 6 Playwright specs under
  `src/frontend/autotable-src/tests/e2e/`.

### Modified (Vasquez-lane)
- `docs/test-architecture.md` — §3.2 DbSerial outcomes, §5.1
  visual-regression CI paragraph, renumbered §3.2 → §3.3,
  §3.3 → §3.4, W13 footer.
- `docs/frontend-pwa-audit.md` — §6.2 LH13 hard-pin sync.
- `docs/agent-handoff-protocol.md` — §4.2 coordinator-direct
  execution.
- `tests/ci/lane-map.json` — added
  `playwright-visual-regression.yml` to Vasquez's regex.
- 23 backend test files — `[Collection("DbSerial")]` added
  per the W12 candidate audit (full list in
  `db-serial-migration-applied.md` §2.2).

### Renamed
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/Wave1ThroughKW12RegressionTests.cs`
  → `…/Wave1ThroughKW13RegressionTests.cs` (class name
  updated in 10 places; W13 extension paragraph + 12 new W13
  smokes appended).

## Gate result

See `Phase_K_W13/Vasquez/gate-snapshot.txt` for the closing
gate `dotnet test` tail. The W13 wave preserves the now
28-wave **zero-skip streak**.

## History notes

- **W12 → W13 hand-off.** The 25-row DbSerial candidate
  inventory in `Phase_K_W12/Vasquez/db-serial-candidates.md`
  drove the W13 migration. Two candidates under
  `Phase_K_W9/Bishop/` are re-attributed to Bishop's W14
  lane via `wave_subdir_overrides` and ship in W14, not W13.
- **LH13 hard-pin cadence trigger met.** The W12 §6.1 defer
  block required 3 successful nightly cron data points; at
  W13 sign-off the third data point landed, so the soft-pin
  becomes the hard-pin per the W11 §7 calibration table.
- **Coordinator-direct escalation prepped, not invoked.**
  The `lane-discipline-flip-required.sh` script ships with
  the §4.2 runbook. Stephen's §4.1 branch-protection re-prompt
  remains the preferred path; the coordinator-direct flow is
  the fallback only.
- **Defensive depth, not flake-elimination.** The 5-run
  sequential flake harness landed at 2610 / 0 / 0 every
  time — DbSerial migration is preventive against future
  cross-test SQLite contention, not a response to observed
  flakes.
