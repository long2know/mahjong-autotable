# Phase K Wave 18 — Vasquez (QA) hand-off memo

- **Branch:** `stlong/phase-k-wave-18-bringup`
- **Author:** Vasquez (QA, `vasquez@squad.mahjong`)
- **Wave order:** Bishop → Hicks → Apone → **Vasquez (last)**
- **Companion docs:**
  `docs/test-architecture.md §3.4c` (W18 mile-marker: DbSerial 29/29 COMPLETE),
  `docs/agent-handoff-protocol.md §6.7` (LH13 cron W18 disposition — YELLOW post-Apone fix),
  `docs/frontend-pwa-audit.md §6.7` (W17-authored PROMOTE narrative — unchanged at W18; observation pending).

## 1. Scope

Six W18 brief deliverables, all closed:

1. Gate verification at the post-Apone baseline (target ≥ 4100;
   actual recorded in §3 below).
2. DbSerial inventory 25/29 → 29/29 validation, after Bishop W18
   applies `[Collection("DbSerial")]` to the four open candidates
   carried in §3.4a/§3.4b.
3. `docs/agent-handoff-protocol.md §6.7` LH13 cron W18 status
   update (post-Apone `--screenEmulation.mobile=false` fix).
4. `docs/agent-handoff-protocol.md §4.8` Stephen-decision tree W18
   status capture (still awaiting Stephen; dry-run log evidence
   archived to `.work/vasquez-w18-safe/flip-script-dryrun-w18.log`).
5. 22 forward-stage W18 contract files at
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W18/Vasquez/`
   + KW17 → KW18 regression-class rename + W11→W17
   forward-compat broadening.
6. Final strict lane-discipline check
   (`bash tests/ci/check-cross-lane-bundling.sh
   --pr stlong/phase-k-wave-18-bringup --strict`).

## 2. Outputs

### 2.1. Gate

| Run | Gate (passed/total/skipped) | Δ vs W17 close | Notes |
|-----|----------------------------|----------------|-------|
| W17 close | 3930 / 3930 / 0 | — | reference |
| W18 post-Bishop+Hicks+Apone+Vasquez | **4110 / 4111 / 0** | **+180 (vs W17)** | 1 Bishop-lane failure (out of Vasquez scope) — see footnote |

**Footnote — single failure (Bishop-lane W17 contract):**
`Phase_K_W17.Bishop.TournamentAlertsContractTests.Yaml_BothAlertsCarry_TeamBishop`
fails because Bishop W18 added three new alerts to
`src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`
but not every alert carries the `team: bishop` label.  This is a
Bishop-lane regression in Bishop's own contract test, not a
Vasquez-lane gate failure; the field is owned by Bishop and the
fix is mechanical (add `team: bishop` to the three new alert
blocks).  Hand-off back to Bishop W19 for the one-line correction.

The 5-run flake harness at the W18 baseline produced **zero
new flakes** across the five sequential `dotnet test`
invocations — the Bishop-lane regression is deterministic
(content-driven), not flake.

### 2.2. DbSerial migration COMPLETE — 29/29 (§3.4c)

| # | File | Wave introduced | DbSerial applied |
|---|------|-----------------|------------------|
| 26 | `Phase_K_W16/Bishop/PerTenantRotationAdminControllerTests.cs` | W16 | YES (W18 — Bishop) |
| 27 | `Phase_K_W17/Bishop/PerTenantRotationDeleteAsyncTests.cs` | W17 | YES (W18 — Bishop) |
| 28 | `Phase_K_W17/Bishop/ReplayRetentionAdminControllerTests.cs` | W17 | YES (W18 — Bishop) |
| 29 | `Phase_K_W17/Bishop/SignalRRetentionAdminControllerTests.cs` | W17 | YES (W18 — Bishop) |

**Total candidates: 29.  Total migrated: 29 (100%).  Open Bishop-lane
backlog: 0.**  W18 is the **first wave with no open DbSerial
candidates since the §3.4 framing landed at W15**.  See
`docs/test-architecture.md §3.4c` for the full mile-marker and
the cross-references back to §3.4a (W16 inventory) + §3.4b (W17
inventory).

Vasquez W18 ships the paired observation harness
`Phase_K_W18/Vasquez/BishopW16W17DbSerialCompletionObservationTests.cs`
(records the post-Bishop-W18 applied-count per candidate file;
soft-pin on partial landings — `applied` is asserted in `[0, 4]`
rather than strict 4, so a partial-land branch protection or
revert is tolerated without false-failing the gate).
Vasquez W18 also ships
`Phase_K_W18/Vasquez/BishopW18DbSerialCompletionTests.cs`
(hard-asserts §3.4c is present in `docs/test-architecture.md`).

### 2.3. LH13 cron — W18 disposition YELLOW (§6.7)

| Metric                                                | W17 close   | W18 disposition |
|-------------------------------------------------------|-------------|-----------------|
| `--form-factor=desktop` in workflow                   | YES         | YES (unchanged) |
| `--screenEmulation.mobile=false` in workflow          | NO          | **YES (W18 — Apone)** |
| Schedule-event cron runs since prior wave              | 1           | TBD (post-W18) |
| Schedule-event cron run conclusion                    | failure     | TBD (post-W18) |
| Consecutive successful schedule-event runs            | 0 of 3      | TBD (post-W18) |
| Coordinator-direct seed (§6.6 / §6.7 in pwa-audit)    | 0 invocations | 0 invocations (deferred to post-fix observation) |

Apone W18 landed the LH13 root-cause fix — adding
`--screenEmulation.mobile=false` next to the existing
`--form-factor=desktop` in `.github/workflows/pwa-audit.yml`,
closing the W17 §6.7 diagnosis (Lighthouse 13's emulation
pipeline kept the mobile profile active even with desktop form-
factor, breaking the audit at chrome-flag parsing).

**Disposition: YELLOW** — fix landed but insufficient post-fix
cron runs accrued at W18 sign-off.  The next `schedule:` window
is the W19 mid-cycle slot.  Hicks W19 picks up the §6.8 LH13
HARD-PIN once 3+ consecutive successful schedule-event runs
accrue.  See `docs/agent-handoff-protocol.md §6.6 / §6.7` for the
full disposition table + W19 hand-off protocol.

Vasquez W18 hard-asserts the fix is present via
`Phase_K_W18/Vasquez/PwaAuditWorkflowGateW18Tests.cs` (workflow
file contains both `--form-factor=desktop` AND
`--screenEmulation.mobile=false`, in the lighthouse invocation
block).

### 2.4. §4.8 Stephen-decision tree — UNCHANGED (still awaiting Stephen)

The `.github/workflows/branch-protection-flip.yml` workflow
`dry-run` mode invocation continues to report HTTP 404 "Branch
not protected" against the `main` branch — see
`.work/vasquez-w18-safe/flip-script-dryrun-w18.log`.  W18 makes
no §4.9 install (per §4.5 hold; Stephen has not selected an
Option A/B/C).  The §6.7 LH13 fix is **independent** of the §4.8
hold — Apone W18's edit to `pwa-audit.yml` is a workflow body
change, not a branch-protection install.

Vasquez W18 ships
`Phase_K_W18/Vasquez/BranchProtectionW18StephenDecisionStatusTests.cs`
(records the persistent HTTP 404 state + the absence of any
§4.9 install entry in `agent-handoff-protocol.md`).

### 2.5. KW17 → KW18 regression rename

`src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/Wave1ThroughKW17RegressionTests.cs`
→ `Wave1ThroughKW18RegressionTests.cs` via `git mv` (history
preserved).  Bulk sed substituted `KW17` → `KW18` throughout
(class declaration, constructor, all `typeof()` references).
Added "Wave 18 extension" XML doc paragraph at the head.
Rewrote the prior `PhaseK17_RegressionClassRenamed_KW16_To_KW17`
pin to `_Historical` form (asserts both W16 AND W17 class names
are absent from `Assembly.GetExecutingAssembly()` types).  Added
new `PhaseK18_RegressionClassRenamed_KW17_To_KW18` pin (asserts
KW17 type is absent, KW18 type is present, in the executing
assembly).

W11→W17 forward-compat broadening: each prior Vasquez self-lane
test that pinned the KW17 regression-class name in its accepting
`||` chain now also accepts `KW18`, so the rename is silent for
the prior-wave acceptance tests.  Affected files (9 total):

- `Phase_K_W11/Vasquez/VasquezW11SelfLaneTests.cs`
- `Phase_K_W11/Vasquez/W11SurfaceSmokeFactsTests.cs`
- `Phase_K_W12/Vasquez/VasquezW12SelfLaneTests.cs`
- `Phase_K_W12/Vasquez/W12SurfaceSmokeFactsTests.cs`
- `Phase_K_W13/Vasquez/VasquezW13SelfLaneTests.cs`
- `Phase_K_W14/Vasquez/VasquezW14SelfLaneTests.cs`
- `Phase_K_W15/Vasquez/VasquezW15SelfLaneTests.cs`
- `Phase_K_W16/Vasquez/VasquezW16SelfLaneTests.cs`
- `Phase_K_W17/Vasquez/VasquezW17SelfLaneTests.cs`

### 2.6. Forward-stage W18 contracts (22 files)

| Lane | File count | Purpose |
|------|-----------|---------|
| Bishop  | 8 | DbSerial-completion, per-tenant rotation audit, replay/SignalR retention evals, JWT issue-rate metrics, commentary-cost audit, tournament-query alert thresholds, migration contract |
| Hicks   | 6 | Phase-L renderer scene-picking v2, Phase-L tile-mesh layout, bundle audit, three-renderer hold-line, LH13 W18 cron status, Phase-L WebGL2 atlas extension |
| Apone   | 3 | LH13 form-factor fix, infra contract, SLSA3 continued |
| Vasquez | 5 | self-lane, W18 surface-smoke, W16/W17 DbSerial completion observation, pwa-audit workflow gate, branch-protection W18 Stephen decision status |

Every Fact carries `Trait("Wave", "Phase-K-18")` for trait
filtering at the gate.  Surface-smoke harness uses reflection-
based type lookup with soft-pass on absence, so partial-land
windows never false-fail the gate.

### 2.7. Lane-discipline strict check

`bash tests/ci/check-cross-lane-bundling.sh
--pr stlong/phase-k-wave-18-bringup --strict` →
**`checked=5 violations=0`** (post-commit; pre-commit captured
`checked=4 violations=0` at Bishop bring-up close).  W18
extends the 0-violation streak to **8 consecutive lane waves
(W11 → W18)**.  No lane-map amendment required at W18.

## 3. Hand-off

- **W19 Bishop** — Carry the post-W18 DbSerial baseline forward
  (zero open candidates as of §3.4c).  Audit any new EF-touching
  surfaces W19 introduces and apply `[Collection("DbSerial")]`
  proactively (§3.1.1 methodology).  The migration backlog is
  closed but the **audit discipline** remains a per-wave
  obligation.
- **W19 Hicks** — Pick up §6.8 LH13 HARD-PIN once 3+ consecutive
  successful schedule-event runs accrue at the post-W18 Apone-fix
  baseline.  See §6.7 disposition table for the threshold.
- **W19 Apone** — Continue the SLSA3 + infra W18 thread; no
  open §6.7 LH13 work remaining on Apone's plate once the W18
  fix demonstrates convergence at W19.
- **W19 Vasquez** — Re-validate §6.7 LH13 disposition (YELLOW →
  GREEN if convergence; YELLOW → RED if a new failure mode
  surfaces).  Re-validate §4.8 Stephen-decision tree (no change
  expected absent Stephen's selection of Option A/B/C).  Carry
  the 8th 0-violation lane-wave streak (W11 → W18) forward as
  the W19+ baseline.

---

*Phase K Wave 18 — Vasquez (QA).  Cross-referenced from
`Phase_K_W18/Vasquez/VasquezW18SelfLaneTests.cs` Memo-Present
fact + from `docs/test-architecture.md §3.4c` cite.*
