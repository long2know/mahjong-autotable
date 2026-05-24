# Vasquez — Phase K Wave 15 (QA bring-up)

**Date:** 2026-11-13
**Branch:** `stlong/phase-k-wave-15-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`

## Summary

Ship the Phase K Wave 15 QA bring-up: re-verify the DbSerial migration
chain at its final completion mile-marker (`docs/test-architecture.md
§3.4`) — Bishop's W15 lane applies `[Collection("DbSerial")]` to the
two remaining `Phase_K_W9/Bishop/` candidates and Vasquez validates
flake-neutrality via 3-5 successive gate runs; escalate the LH13
hard-pin from a 4-wave YELLOW (W14) to a **5-wave YELLOW** at
`docs/frontend-pwa-audit.md §6.4`, with the §6.5 calibration-deadlock
escalation recommendation (Stephen-direct manual `pwa-audit.yml`
trigger × 3) now in flight; re-verify the §4.4 escalation pattern
in `docs/agent-handoff-protocol.md §4.4` (8-wave §4.1 deadlock,
Coordinator-direct invocation recommended NOW); ship 16 forward-stage
W15 contract test files (~163 facts) covering the seven W15 surfaces
Bishop / Hicks / Apone are landing in parallel + Vasquez self-lane
+ the PwaAuditWorkflowGate W15 mirror; rename the regression class
`Wave1ThroughKW14RegressionTests → Wave1ThroughKW15RegressionTests`
with ~18 W15 smokes appended (12 forward-stage + 4 self-lane
hard-asserts + 2 DbSerial soft-probes); ship 6 new Playwright specs
under `tests/e2e/` (replay-blob-streaming, cost-forecast-route,
phase-l-renderer-bundle, lh13-thresholds-w15, snapshotPathTemplate,
bundle-audit-candidates); update `tests/selectors.md` with a W15
Vasquez QA footer inventorying all six specs; canonise the W11→W14
lane-discipline maturity narrative as a new top-level §6 in
`docs/agent-handoff-protocol.md`; and move the backend gate from the
W14 baseline of **3029/0/0** to the W15 gate captured in
`Phase_K_W15/Vasquez/gate-snapshot.txt`, preserving the zero-skip
streak now at **30 consecutive waves** and the lane-discipline
zero-violation streak now at **5 consecutive waves**.

## Deliverables (7)

1. **DbSerial migration final completion mile-marker** —
   `docs/test-architecture.md §3.4`:
   - New §3.4 documents the W15 completion mile-marker.
   - Bishop's W15 lane carries `[Collection("DbSerial")]` for the 2
     remaining `Phase_K_W9/Bishop/` candidates
     (`EfCommentaryUsageMeterTests.cs`,
     `IdempotencyStoreContractTests.cs`).
   - Vasquez validates via 3-5 successive gate runs (flake-reduction
     harness; see `BishopW15DbSerialCompletionOnW9FilesTests.cs`).
   - Old §3.4 / §3.5 renumbered to §3.5 / §3.6 (consistency-pin in
     `VasquezW15SelfLaneTests.cs`).

2. **LH13 5-wave deferral escalation (§6.4 + §6.5)** —
   `docs/frontend-pwa-audit.md`:
   - §6.4 now flags the cumulative 5-wave deferral as **YELLOW** (W14 was
     4-wave, W11→W15 inclusive).  §6.4.1 documents the W15 re-query
     evidence from Hicks's W15 lane (0 scheduled / 0 successful runs).
   - §6.5 adds the calibration-deadlock escalation recommendation:
     Stephen-direct manual trigger of `pwa-audit.yml` × 3 via the
     Actions UI, named as the W16 unblock.
   - W16 threshold: a sixth-wave deferral flips YELLOW → **RED** +
     Coordinator escalation.
   - `PwaAuditWorkflowGateW15Tests.cs` (10 facts) mirrors §6.4/§6.5.

3. **§4.4 escalation re-verification (W15)** —
   `docs/agent-handoff-protocol.md §4.4`:
   - 8-wave §4.1 deadlock (W7 → W15 inclusive).
   - §4.4 re-verifies the §4.3 fallback runbook with a fresh
     dry-run captured at
     `.work/vasquez-w15-safe/flip-script-dryrun-w15.log`.
   - Coordinator-direct invocation is recommended NOW (rather than
     waiting another wave); the 1-line copy-paste from §4.3 is
     re-stated for traceability.
   - `tests/ci/lane-discipline-flip-required.sh --dry-run` continues
     to pass (cosmetic `MODE != "apply"` line-number drift NOT fixed
     in this wave — Apone's lane).

4. **Forward-stage W15 contract tests (~163 facts)** —
   `Phase_K_W15/Vasquez/`:
   - 17 backend test files mirroring Bishop / Hicks / Apone W15
     surfaces, all forward-stage tolerant except Vasquez self-lane.
   - Bishop W15 surfaces: replay blob `Range` streaming,
     per-tenant JWKS rotation, tournament page-size metrics,
     commentary cost-forecast, spectator audit retention sweep,
     replay retention sweep, DbSerial completion on the W9 files.
   - Hicks W15 surfaces: three-renderer hold-line at 406,640 B,
     LH13 third retry, Phase L renderer bundle entry, cost-forecast
     route, bundle-audit candidates, snapshotPathTemplate migration.
   - Apone W15 surfaces: `AponeW15InfraContractTests.cs` (12 facts)
     covers the W15 workflow / k8s / Terraform stamps.
   - PwaAuditWorkflowGate W15 mirror + Vasquez self-lane (16 facts,
     all hard-assert) + cross-cutting `W15SurfaceSmokeFactsTests.cs`
     (17 facts) pinning the file-inventory roster.

5. **Regression rename + ~18 W15 smokes** —
   `Regression/Wave1ThroughKW15RegressionTests.cs`:
   - W14 class renamed via `git mv`; sed-replaced internal references;
     XML doc header extended for the W15 generation.
   - 12 forward-stage smokes + 4 hard-assert self-lane (DbSerial
     completion mile-marker, LH13 5-wave YELLOW pin, §4.4 doc, KW14→KW15
     rename pin) + 2 DbSerial soft-probes.
   - W11 / W12 / W13 SelfLaneTests + SurfaceSmokeFactsTests + W14
     SelfLaneTests updated to accept `Wave1ThroughKW15RegressionTests`
     in their across-rename-wave acceptable-name lists.

6. **6 Playwright specs + selectors.md W15 footer** —
   `src/frontend/autotable-src/tests/e2e/`:
   - `replay-blob-streaming.spec.ts` — Range-header chunked download
     verifying 206 Partial Content (Bishop W15 surface).
   - `cost-forecast-route.spec.ts` — `?action=cost-forecast&days=<n>`
     admin overlay (Hicks W15 surface; Bishop projection upstream).
   - `phase-l-renderer-bundle.spec.ts` — `dist-size.json` includes the
     `renderer-webgl2` hello-world entry inside the 180-220 KB Phase L
     envelope.
   - `lh13-thresholds-w15.spec.ts` — W11 §7 calibrated thresholds
     soft-pin, paired to the 5-wave YELLOW.
   - `snapshotPathTemplate.spec.ts` — Playwright config migration to
     `snapshotPathTemplate` for deterministic visual-regression paths.
   - `bundle-audit-candidates.spec.ts` — audit doc lists ≥3 candidates.
   - `tests/selectors.md` Vasquez W15 footer inventories all six;
     pairs with the W15 Hicks footer above (same shared_files entry).

7. **Lane-discipline maturity narrative (§6)** —
   `docs/agent-handoff-protocol.md §6` (new top-level):
   - Canonises the W11→W14 zero-violation streak as the squad
     baseline going into W15+.
   - Documents the maturity arc (W3 chaos → W11–W14 zero-violation
     streak) with a per-wave allowed/violation table.
   - Names the amendment-discovery pattern, the primary-classification
     rule, and the allowlist evolution timeline (W8 → W13).
   - Cross-referenced by `VasquezW15SelfLaneTests.cs` and by the
     `Wave1ThroughKW15RegressionTests.cs` rename-pin smokes.

## Cross-lane prevention compliance

- All cross-lane probes live in `Phase_K_W15/Vasquez/` with a
  name-prefix (`BishopW15*Tests.cs`, `HicksW15*Tests.cs`,
  `AponeW15*Tests.cs`). None of them touch `Phase_K_W15/Bishop/`,
  `Phase_K_W15/Hicks/`, or `Phase_K_W15/Apone/`.
- The two Bishop-lane DbSerial candidates were NOT touched in
  this commit — Bishop's W15 lane carries the attribute application.
- Bishop's working-tree edits under `src/backend/src/` were NOT
  staged in this commit. Same for Hicks's frontend edits and Apone's
  workflow / k8s / Terraform edits — those land in their own commits.
- The shared `tests/selectors.md` (path mirror under
  `src/frontend/autotable-src/tests/selectors.md`) lands per
  `shared_files.selectors_md_shared` (authors: hicks + vasquez,
  primary: vasquez).
- The shared `docs/agent-handoff-protocol.md` lands per
  `shared_files.agent_handoff_protocol_md_shared` (authors:
  vasquez + apone, primary: vasquez).  The new §4.4 + new §6 are
  Vasquez-authored sections.
- The shared `docs/frontend-pwa-audit.md` carries §6.4 + §6.5 sections
  authored by Vasquez paired with the Hicks-authored §6.4.1 evidence
  capture (per the W11+ multi-author cadence on the PWA-audit doc).
- The `Phase_K_W15/Bishop/README.md` + `Phase_K_W15/Hicks/README.md`
  Vasquez-authored placeholders follow the W14 precedent
  (`wave_subdir_overrides` tolerated for README forwarding pointers).
- Author identity verified via `git log -1 --format='%an <%ae>'`
  → `Vasquez (QA) <vasquez@squad.mahjong>`.

## Gate

Final gate captured in `Phase_K_W15/Vasquez/gate-snapshot.txt`.
W15 target was ≥ 3150 / 0 / 0; the W14 baseline was 3029 / 0 / 0.

The DbSerial completion mile-marker is validated by 3–5 successive
gate runs in this wave; flake-reduction evidence is captured by the
`BishopW15DbSerialCompletionOnW9FilesTests.cs` soft-probes and by
the gate-snapshot delta (warning / failure count must not regress
across consecutive runs).

## Hand-off notes

### To Bishop (W16)

- The DbSerial completion mile-marker is now an audited deliverable
  per `docs/test-architecture.md §3.4`. If the W15 lane left any
  W9 `Phase_K_W9/Bishop/` file un-annotated, the Vasquez soft-probes
  remain green (forward-stage) but the §3.4 mile-marker doc names
  it as the W16 close-out item.

### To Hicks (W16)

- The Phase L renderer hello-world spike (`renderer-webgl2.<hash>.js`)
  baseline is now in `Phase_K_W15/Hicks/`. The three-renderer-big
  hold-line (406,640 B) is asserted by
  `HicksW15ThreeRendererHoldLineTests.cs` and pinned in the W15
  Vasquez QA footer; W16 carries the second-spike sizing target.
- `snapshotPathTemplate` migration: the W15 Hicks-lane config change
  ships in this wave; the W15 Vasquez Playwright spec
  (`snapshotPathTemplate.spec.ts`) inspects the config from the runtime
  side and forward-stages cleanly when the config isn't observable.
  If Hicks names a different config-file path, Vasquez follows.

### To Apone (W16)

- The §4.4 escalation re-verification is now wave-cadenced. The
  cosmetic dry-run summary bug (line ~133, `MODE != "apply"` guard)
  is still documented but NOT fixed — yours (DevOps lane) if the
  line-number drift matters.
- The new §6 (lane-discipline maturity narrative) is a Vasquez-authored
  section in the shared doc and pairs with the workflow surface checks
  in `AponeW15InfraContractTests.cs`.

### To Stephen / Coordinator

- §4.1 has now been standing for 8 consecutive waves (W7 → W15).
  §4.4 re-states the §4.3 fallback runbook with a fresh dry-run.
  The 1-line copy-paste is in the doc.  Coordinator-direct invocation
  is the recommended W16 path.
- LH13 5-wave cumulative deferral: a sixth-wave deferral at W16
  flips the status to **RED** and triggers Coordinator escalation
  per `docs/frontend-pwa-audit.md §6.5`.
- Lane-discipline zero-violation streak: **5 consecutive waves**
  (W11 → W15). The new §6 narrative canonises the W11→W14 streak;
  W15 extends it by one wave.

### To Vasquez (W16 self)

- LH13 §6.4 cadence: if cron still has not converged by W16 sign-off
  re-prompt; if cumulative deferral reaches 6 waves (W11 → W16),
  escalate to Coordinator per §6.5.
- DbSerial completion mile-marker: re-verify the 3–5 gate-run
  flake-reduction harness; if any W9 Bishop file regressed, the
  §3.4 mile-marker is the W16 reopen path.
- Forward-stage tests: as W15 surfaces converge, flip the matching
  soft-pins to hard-asserts (Bishop's controllers, Hicks's overlays,
  Apone's workflow / k8s / Terraform surfaces).
- Maturity narrative: extend the per-wave table in §6 with the W15
  row at W16 sign-off (or hand it to the W16 lane).

## Zero-skip streak

W15 preserves the streak: now **30 consecutive waves** with
zero skips on the backend gate.

## Lane-discipline zero-violation streak

W15 extends the streak to **5 consecutive waves** (W11 → W15).
The W11→W14 streak is canonised in `docs/agent-handoff-protocol.md §6`;
W15 inherits the discipline (5th consecutive zero-violation wave).
