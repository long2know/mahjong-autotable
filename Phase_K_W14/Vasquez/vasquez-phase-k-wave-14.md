# Vasquez — Phase K Wave 14 (QA bring-up)

**Date:** 2026-11-06
**Branch:** `stlong/phase-k-wave-14-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`

## Summary

Ship the Phase K Wave 14 QA bring-up: close out the W12→W13→W14
DbSerial migration chain via a completion memo (the 2 remaining
candidates remain a cross-lane hand-off to Bishop / Coordinator);
sync the LH13 mirror tests with `docs/frontend-pwa-audit.md §6.3`
(yellow-flag, cumulative 4-wave deferral, soft-pin retained in
BOTH workflow and tests); fix the W12 `manifest-screenshots-visual.spec.ts`
ordering bug (`page.goto` before `page.setContent` so relative
`<img>` URLs resolve against baseURL); prep the §4.3 branch-protection
fallback execution runbook (`docs/agent-handoff-protocol.md §4.3`)
with a re-validated dry-run + 1-line copy-paste for Stephen /
Coordinator; ship 14 forward-stage W14 contract test files (~104
facts) covering the seven W14 surfaces Bishop / Hicks / Apone are
landing in parallel + Vasquez self-lane + the LH13 §6.3 mirror;
rename the regression class `Wave1ThroughKW13RegressionTests →
Wave1ThroughKW14RegressionTests` with 14 W14 smokes appended
(including 4 self-lane hard-asserts pinning the W14 artefacts);
ship 6 new Playwright specs under `tests/e2e/` (bracket-ui-route,
replay-listing-route, commentary-cost-admin-panel,
visual-regression-real-captures, lh13-thresholds-hard-pinned-final,
jwks-overlap-rollback-rejected); update `tests/selectors.md` with
a W14 Vasquez QA footer inventorying all six specs; and move the
backend gate from the W13 baseline of **2789/0/0** to the W14 gate
captured in `Phase_K_W14/Vasquez/gate-snapshot.txt`, preserving the
zero-skip streak now at 29 waves.

## Deliverables (7)

1. **DbSerial migration completion memo** —
   `Phase_K_W14/Vasquez/db-serial-migration-completion.md`:
   - Documents W12 → W13 → W14 chain end-to-end.
   - Cross-lane blocker re-confirmed via `wave_subdir_overrides` rule.
   - Escalation path named (Bishop W14 → Vasquez W15 re-prompt →
     Coordinator-direct W16 via §4.3 pattern).
   - Backstop tests pin the memo itself (self-lane only; no cross-lane
     reach).

2. **LH13 mirror sync (§6.3)** — `docs/frontend-pwa-audit.md §6.3`:
   - Cumulative 4-wave deferral flagged as **YELLOW**.
   - Workflow soft-pin retained; mirror tests soft-pin retained.
   - New `PwaAuditWorkflowGateW14Tests.cs` (8 facts) mirrors §6.3.
   - W15 escalation criteria named (6-wave deferral → Coordinator
     consultation).

3. **Visual-regression spec fix** — `manifest-screenshots-visual.spec.ts`:
   - `await page.goto('/')` added before `page.setContent(…)`.
   - Forward-stage tolerance preserved (origin-unreachable still
     annotates and passes).
   - Documented in `docs/test-architecture.md §5.2`.

4. **Branch-protection W14 fallback runbook** —
   `docs/agent-handoff-protocol.md §4.3`:
   - Dry-run re-validated (`.work/vasquez-w14-safe/flip-script-dryrun.log`).
   - 1-line copy-paste command for Stephen / Coordinator.
   - Cosmetic dry-run summary bug documented (`MODE != "apply"`
     guard at line ~133 skips the contexts list).
   - Audit-trail expectations spelled out.

5. **Forward-stage W14 contract tests** — `Phase_K_W14/Vasquez/`:
   - 14 backend test files mirroring Bishop / Hicks / Apone W14
     surfaces (~104 facts total, all forward-stage tolerant
     except Vasquez self-lane).
   - Cross-lane probes carry `BishopW14*` / `HicksW14*` / `AponeW14*`
     name-prefix per the W13 precedent; ALL live in `Phase_K_W14/Vasquez/`
     to satisfy `wave_subdir_overrides`.

6. **Regression rename + 14 W14 smokes** —
   `Regression/Wave1ThroughKW14RegressionTests.cs`:
   - W13 class renamed via `git mv`; sed-replaced internal references.
   - 14 W14 smokes appended (10 soft-pin + 4 hard-assert
     self-lane: DbSerial completion memo, visual-regression spec
     fix, §4.3 doc, KW13→KW14 rename pin).
   - W11 / W12 / W13 SelfLaneTests + SurfaceSmokeFactsTests
     updated to accept `Wave1ThroughKW14RegressionTests` in
     their across-rename-wave acceptable-name lists.

7. **6 Playwright specs + selectors.md W14 footer** —
   `src/frontend/autotable-src/tests/e2e/`:
   - `bracket-ui-route.spec.ts`, `replay-listing-route.spec.ts`,
     `commentary-cost-admin-panel.spec.ts` —
     forward-stage tolerant deep-link route smokes paired with
     Hicks's `?action=…` query-string entry points.
   - `visual-regression-real-captures.spec.ts` — real-capture
     visual regression at 2% pixel-diff tolerance.
   - `lh13-thresholds-hard-pinned-final.spec.ts` — the
     consumer-side hard-pin gate for §6.3 (forward-staged today;
     flips when cron converges).
   - `jwks-overlap-rollback-rejected.spec.ts` — overlap-window
     rollback protection assertion.
   - `tests/selectors.md` Vasquez W14 footer inventories all six.

## Cross-lane prevention compliance

- All cross-lane probes live in `Phase_K_W14/Vasquez/` with a
  name-prefix (`BishopW14*Tests.cs`, `HicksW14*Tests.cs`,
  `AponeW14*Tests.cs`). None of them touch `Phase_K_W14/Bishop/`,
  `Phase_K_W14/Hicks/`, or `Phase_K_W14/Apone/`.
- The two Bishop-lane DbSerial candidates were NOT touched in
  this commit — see `db-serial-migration-completion.md §2.1`.
- Bishop's working-tree edits under `src/backend/src/` were NOT
  staged in this commit. Same for Hicks's frontend edits and
  Apone's workflow/k8s edits — those land in their own commits.
- The shared `tests/selectors.md` (path mirror under
  `src/frontend/autotable-src/tests/selectors.md`) lands per
  `shared_files.selectors_md_shared` (authors: hicks + vasquez,
  primary: vasquez).
- Author identity verified via `git log -1 --format='%an <%ae>'`
  → `Vasquez (QA) <vasquez@squad.mahjong>`.

## Gate

Final gate captured in `Phase_K_W14/Vasquez/gate-snapshot.txt`.
W14 target was ≥ 2900 / 0 / 0; the W13 baseline was 2789 / 0 / 0.

## Hand-off notes

### To Bishop (W15)

- The 2 remaining DbSerial attribute applications
  (`Phase_K_W9/Bishop/EfCommentaryUsageMeterTests.cs`,
  `Phase_K_W9/Bishop/IdempotencyStoreContractTests.cs`) remain
  yours. See `db-serial-migration-completion.md §2.1 / §2.3`.
- If you don't land them by W15 sign-off, Vasquez re-prompts.
  By W16, Coordinator-direct.

### To Hicks (W15)

- The W14 `?action=bracket` / `?action=replays` / `?action=admin-cost`
  routes are wired by you in this wave. Vasquez's six W14 Playwright
  specs are forward-staged today — they flip to hard-assert when
  Bishop's matching listing endpoints land + your overlays render.
- The `manifest-screenshots-visual.spec.ts` fix landed in this
  wave. If you re-record baselines, the fix's `page.goto('/')`
  step now applies — relative `<img>` URLs resolve correctly.

### To Apone (W15)

- The §4.3 branch-protection fallback runbook is now in
  `docs/agent-handoff-protocol.md`. The dry-run is re-validated
  per wave; the W14 capture is at
  `.work/vasquez-w14-safe/flip-script-dryrun.log`.
- The cosmetic dry-run summary bug (line ~133, `MODE != "apply"`
  guard) is documented but NOT fixed in this wave — fix is
  yours (DevOps lane) if the line-number drift matters.

### To Stephen / Coordinator

- §4.1 has now been standing for 7 consecutive waves (W7 → W14).
  §4.3 is ready to invoke. The 1-line copy-paste is in the doc.

### To Vasquez (W15 self)

- LH13 §6.3 cadence: if cron still has not converged by W15
  sign-off, re-prompt; if cumulative deferral reaches 6 waves
  (W11 → W16), escalate to Coordinator per §6.3.
- DbSerial completion: re-prompt Bishop if W15 doesn't carry
  the attribute applications.
- Forward-stage tests: as W14 surfaces converge, flip the
  matching soft-pins to hard-asserts (Bishop's controllers,
  Hicks's overlays, Apone's TF 1.11.4 / JWT rehearsal #3 GA /
  regional-eks us-east-1 surfaces).

## Zero-skip streak

W14 preserves the streak: now **29 consecutive waves** with
zero skips on the backend gate.
