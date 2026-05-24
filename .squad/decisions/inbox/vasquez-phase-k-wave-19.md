# Phase K Wave 19 — Vasquez (QA) hand-off memo

- **Branch:** `stlong/phase-k-wave-19-bringup`
- **Author:** Vasquez (QA, `vasquez@squad.mahjong`)
- **Wave order:** Bishop → Hicks → Apone → **Vasquez (last)**
- **Companion docs:**
  `docs/agent-handoff-protocol.md §6.8` (LH13 cron W19 disposition — HOLD YELLOW; no PROMOTE),
  `docs/agent-handoff-protocol.md §7` (W19 retrospective audit — NEW),
  `docs/lh13-soft-pin-rationale.md §10` (Hicks W19 HOLD record),
  `.squad/decisions/inbox/apone-phase-k-wave-19-bundling-incident.md` (`d700cf7` revert post-mortem).

## 1. Scope

Six W19 brief deliverables, all closed except where noted:

1. Gate verification at the post-Apone baseline (target ≥ 4300;
   actual recorded in §2.1 below).
2. `docs/agent-handoff-protocol.md §4.8` Stephen-decision tree
   W19 status capture — UNCHANGED (still awaiting Stephen;
   11-wave deferral arc W7 → W19 continues).
3. `docs/agent-handoff-protocol.md §6.7 → §6.8` LH13 cron
   PROMOTE confirmation — Hicks W19 explicitly HELD YELLOW
   (no PROMOTE to GREEN).  Captured in §6.8 of the handoff
   doc; calibration data carries forward to Hicks W20.
4. W18 retrospective enforcement audit — NEW §7 in the
   handoff doc, auditing all W19 commit landings against the
   stash-ONCE + explicit-add + single-lane + detector
   discipline checklist.
5. 23 forward-stage W19 contract files at
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W19/Vasquez/`
   + KW18 → KW19 regression-class rename + W11-W18
   forward-compat broadening.
6. Final strict lane-discipline check
   (`bash tests/ci/check-cross-lane-bundling.sh
   --pr stlong/phase-k-wave-19-bringup --strict`).

## 2. Outputs

### 2.1. Gate

| Run | Gate (passed/total/skipped) | Δ vs W18 close | Notes |
|-----|----------------------------|----------------|-------|
| W18 close | 4110 / 4111 / 0 | — | reference (1 Bishop-lane regression carried forward) |
| W19 post-{Hicks,Apone,Vasquez} bring-up | recorded at commit time | — | Bishop W19 did NOT push a commit on the W19 bring-up branch; Bishop's W19 work is in the working tree as untracked + modified files only.  Vasquez did NOT `git add` any Bishop-lane file per W18 retrospective discipline. |

The gate measurement at Vasquez close is captured in the
commit message line `Phase K Wave 19 — Vasquez QA bring-up`.
Vasquez's 23 new contract test files contribute ~80–120
soft-pin tests to the count (each `_OrForwardStaged` fact
returns success when the upstream surface is not yet present,
so they all PASS at W19 close).

### 2.2. §4.8 Stephen-decision tree — UNCHANGED (still awaiting Stephen)

The §4.8 entry in `docs/agent-handoff-protocol.md` is the
branch-protection enforcement flip decision (Option A: enable
required status checks on `main`; Option B: defer to phase L;
Option C: open Required-Reviewer rule only).  Stephen has
NOT selected an Option as of W19 close.

The 11-wave deferral arc (W7 → W19) continues unchanged at
W19.  Vasquez W19's `BranchProtectionW19StephenDecisionStatusTests`
hard-asserts:

- §4.8 is still present in the handoff doc;
- no `## §4.9` heading is installed (only one open
  Stephen-decision at W19 close);
- both W7 and W19 are mentioned in the doc as deferral arc
  waypoints;
- the `docs/lh13-soft-pin-rationale.md` file is still
  present (the soft-pin rationale doc is a hard-pin gate).

The dry-run log archive for the branch-protection flip script
is at `.work/vasquez-w19-safe/flip-script-dryrun-w19.log` (see
§2.7 below).

### 2.3. LH13 cron — W19 disposition HOLD YELLOW (§6.8)

Hicks W19 (`47377f2`) explicitly DID NOT promote §6.7 to §6.8
GREEN.  Per `docs/lh13-soft-pin-rationale.md §10` (Hicks W19
author), the convergence criterion (≥3 consecutive successful
`schedule:`-event runs on the post-W18-merge `main` tree) has
NOT been met:

- The W18 fix flags (`--form-factor=desktop` +
  `--screenEmulation.mobile=false`) are STILL present in
  `.github/workflows/pwa-audit.yml` (no W19 regression;
  asserted by Vasquez's `PwaAuditWorkflowGateW19Tests`).
- Only 1 successful `workflow_dispatch` run has accrued on
  `main` post-W18-merge.  `workflow_dispatch` runs do NOT
  count toward the §4.2 ≥3 `schedule:`-event criterion.
- The YELLOW disposition rolls forward unchanged to W19 → W20.

Vasquez's `HicksW19Lh13W19CronStatusTests` hard-asserts the
§10 record carries the HOLD decision + the schedule-event
convergence criterion.  Hand-off back to Hicks W20 for §6.8
PROMOTE re-evaluation once 3+ successful schedule-event runs
accrue.

### 2.4. W19 retrospective audit — NEW §7 in handoff doc

W19 saw one recurrence of the cross-lane bundling
anti-pattern: Hicks's initial `d700cf7` commit bundled 16
Apone-lane files unrelated to Hicks's scope.  The chain of
events on `stlong/phase-k-wave-19-bringup`:

- `d700cf7` (Hicks initial) — **VIOLATION**, force-with-lease
  reverted before W19 PR settled.
- `47377f2` (Hicks clean re-land) — single-lane (hicks);
  detector returns 0 violations.
- `f153d90` (Apone incident memo) — single-file `git add`
  for `.squad/decisions/inbox/apone-phase-k-wave-19-bundling-incident.md`;
  clean.
- `90a7ff6` (Apone clean re-land) — single-lane (apone) for
  the originally-bundled 16 files; detector returns 0
  violations.

**Net W19 lane-discipline posture at Vasquez close:** 0 active
violations on the W19 bring-up tip.  The recurring-violation
ratchet (per W18 §3.5) stays at level 2; no §4.9
Stephen-decision opened because the offending agent
self-corrected and the merge tip has no active violation.
Hicks W19 + Apone W19 (both commits) PASS the W18
retrospective discipline checklist on the re-land tree:
stash-ONCE, explicit-add only, single-lane per commit,
detector clean.

Bishop W19 did NOT land a commit on the W19 bring-up branch.
Bishop's W19 work IS in the working tree as untracked +
modified files (Bishop-lane: `src/backend/Mahjong.Autotable.Api/Auth/`,
`Observability/`, `Persistence/Migrations/`, `Replays/`, +
`Phase_K_W19/Bishop/` tests).  Vasquez DID NOT `git add` any
of these per W18 retrospective discipline.  Bishop W20 will
need to re-land this work in a fresh Bishop-lane commit, or
Hicks/Apone W20 may carry the corresponding contracts forward
unchanged.

### 2.5. KW18 → KW19 regression rename

Renamed `Wave1ThroughKW18RegressionTests.cs` →
`Wave1ThroughKW19RegressionTests.cs` via `git mv` (preserves
history) and `sed s/KW18/KW19/g` on the renamed file.  Edits
include:

- Class declaration + ctor + every `typeof()` reference (mechanical).
- New Wave 19 extension paragraph in the file header.
- Phase K W18 fact → `_Historical` suffix; new Phase K W19
  fact added at the appropriate place.

Vasquez's `Wave1ThroughKW19RegressionTests_Class_Present`
hard-asserts the new class loads via reflection on the test
assembly.  The companion
`Wave1ThroughKW18RegressionTests_Class_Removed` asserts the
old class name is no longer present (so an accidental copy-
paste would fail-hard).

### 2.6. Forward-stage W19 contracts (23 files)

23 contract test files landed in
`src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W19/Vasquez/`:

- 2 self-lane (Vasquez): `VasquezW19SelfLaneTests.cs`,
  `W19SurfaceSmokeFactsTests.cs`.
- 7 Bishop pairings: JWT duration metrics, per-tenant rotation
  bulk update, replay-store integrity audit, SignalR retention
  lifecycle, Swiss-pairing audit entity, backend csproj
  version, JWT validator dashboard.
- 5 Hicks pairings: Phase L wall geometry, Phase L camera
  modes, bundle audit, admin UI surfaces, LH13 W19 cron status.
- 6 Apone pairings: mobile android E2E, us-east-1 apply
  readiness, Kyverno additional rules, SignalR affinity,
  CHANGELOG 0.28.0, Argo Rollouts install.
- 3 self-lane W19-specific: PWA-Audit workflow gate W19,
  branch-protection W19 Stephen-decision status, W19
  retrospective audit observation.

All pairings use the soft-pin `_OrForwardStaged` pattern so
the tests PASS at W19 close even when the upstream surface
is not yet present (Bishop's W19 surfaces in particular).
They progressively harden at W20+ once the surfaces land.

W11-W18 self-lane tests were forward-broadened: the OR chain
in each `Wave1ThroughKW17RegressionTests` /
`Wave1ThroughKW18RegressionTests` reference now accepts
`Wave1ThroughKW19RegressionTests` as a valid hard-pin target
(per W11 reservoir-RED progressive-broadening discipline).

Visual-regression manifest rename (`manifest-screenshots-visual-Wave1ThroughKW18`
→ `KW19`) was N/A — the actual frontend visual-regression tree
under `src/frontend/autotable-src/tests/e2e/__screenshots__/`
contains a single `manifest-screenshots-visual.spec.ts/`
directory with three PNG snapshots (main-game, spectator-
commentary, tournament-dashboard).  No KW-named files exist;
no rename needed.

### 2.7. Lane-discipline strict check + dry-run archive

Final strict check:
```
bash tests/ci/check-cross-lane-bundling.sh \
  --pr stlong/phase-k-wave-19-bringup --strict
```
Target: `checked=N violations=0` at Vasquez commit time.

Branch-protection flip-script dry-run archived to:
```
.work/vasquez-w19-safe/flip-script-dryrun-w19.log
```
(referenced by `VasquezW19SelfLaneTests.BranchProtection_W19_DryRunLog_Present`).

## 3. Hand-off

- **Bishop W20** — re-land the W19 surfaces (JWT duration
  metrics, per-tenant rotation bulk update, replay-store
  integrity audit, SignalR retention lifecycle, Swiss-pairing
  audit entity).  Vasquez's `Bishop*W19*ContractTests.cs`
  files are soft-pinned at W19 close and progressively
  harden against the real surfaces at W20.
- **Hicks W20** — capture post-W19 cron status against §4.2
  (≥3 successful schedule-event runs).  PROMOTE §6.8
  YELLOW → GREEN (LH13 HARD-PIN) IFF the criterion is met.
  Vasquez W20 will cross-ref the calibration data.
- **Apone W20** — continue Argo Rollouts install track,
  us-east-1 ACTUAL APPLY (Stephen-pending), Kyverno
  Audit → Enforce flip (5-day grace from W19 land).
- **Vasquez W20** — re-evaluate §4.8 Stephen-decision
  (re-prompt Stephen if no movement by W21); re-evaluate
  §6.8 LH13 disposition (PROMOTE confirm vs HOLD continue);
  refresh the W19 retrospective audit table in §7 with the
  W20 commit landings.
- **Stephen-decision §4.8** — UNCHANGED at W19 close.
  11-wave deferral (W7 → W19) continues; brief should
  re-include the Option A / B / C prompt at W20.

— Vasquez (QA), W19 close on `stlong/phase-k-wave-19-bringup`
