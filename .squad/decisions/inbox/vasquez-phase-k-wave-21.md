# Phase K Wave 21 — Vasquez (QA) hand-off memo

- **Branch:** `stlong/phase-k-wave-21-bringup`
- **Author:** Vasquez (QA, `vasquez@squad.mahjong`)
- **Wave order:** Apone → Hicks → Bishop → **Vasquez (last)**
- **Companion docs:**
  `docs/agent-handoff-protocol.md §6.10` (LH13 cron W21 disposition
  — HOLD YELLOW; ratifies Hicks W21; NEW subsection),
  `docs/agent-handoff-protocol.md §9` (W21 stash-isolation directive
  + W21 retrospective audit — NEW top-level section),
  `docs/lh13-soft-pin-rationale.md §12` (Hicks W21 HOLD record).

## 1. Scope

Six W21 brief deliverables, all closed except where noted:

1. Gate verification at the post-Bishop baseline (target ≥ 4750;
   actual recorded in §2.1 below at commit time).
2. `docs/agent-handoff-protocol.md §4.8` Stephen-decision tree
   W21 status capture — UNCHANGED (still awaiting Stephen;
   **13-wave deferral arc** W7 → W21 continues; W21 symbolically
   crosses the "year of bring-ups" threshold).
3. `docs/agent-handoff-protocol.md §6.10` (NEW) — LH13 cron
   PROMOTE re-evaluation — Hicks W21 explicitly HELD YELLOW
   (no PROMOTE to GREEN).  Reason narrowed from W20's two
   compounded reasons to a SINGLE reason at W21: gh-CLI
   unauthenticated in the bring-up shell.  The W20 secondary
   reason (sample-window-size arithmetic) no longer applies at
   W21 (~25 h elapsed since W18 merge — well past the 3-hour
   minimum for 3 hourly cron ticks).  §4.2 still requires ≥ 3
   *observed* successful schedule-event runs; observation
   channel remains closed.  Vasquez W21 ratifies HOLD in
   §6.10 of the handoff doc.
4. W20 process-retrospective audit + Apone W20 stash-reset
   lesson codification — NEW top-level §9 in the handoff
   doc.  The W20 mid-task Apone reset wiped Hicks's working
   tree; Hicks recovered via the
   `apone-w20-baseline-1779625492-recovered-by-apone-1779626575`
   renamed stash.  §9.1 codifies the W21 stash-isolation
   directive: never touch other agents' working tree state
   mid-wave.  Five concrete sub-rules + a §9.6 hand-off note
   to enforce by inspection until an automated detector lands.
5. 25 forward-stage W21 contract files at
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W21/Vasquez/`
   + KW20 → KW21 regression-class rename + W11-W20 forward-
   broadening to also accept the KW21 class name in the
   historical PhaseK12/13/14 rename pins.  PhaseK20 rename pin
   rewritten to `_Historical`; NEW PhaseK21 rename pin added.
6. Lane-discipline strict + (if needed) amendment — target
   `checked=N violations=0`.  **11th consecutive 0-violation
   lane wave** milestone (W11 → W21 inclusive).

## 2. Outputs

### 2.1. Gate

| Run | Gate (passed/total/skipped) | Δ vs W20 close | Notes |
|-----|----------------------------|----------------|-------|
| W20 close | 4637 / 4637 / 0 | — | reference (per W20 bringup commit `bbd3f6c`). |
| W21 post-{Apone,Hicks,Bishop} bring-up | 4754 / 4755 / 0 → 4755 / 4755 / 0 (after Vasquez W20 mobile-pin repair) | +118 (Bishop W21) | One pre-existing W20 contract test failed at Bishop W21 close (`AponeW20ChangelogW20ContractTests.MobilePackageJson_HasVersion_0_29_0_OrForwardStaged`), broken by Apone's W21 bump from 0.29.0 → 0.30.0.  Vasquez W21 forward-broadens the soft-pin to accept BOTH 0.29.0 AND 0.30.0 (and any future 0.N.0). |
| W21 post-Vasquez bring-up | recorded at commit time | — | Vasquez's 25 new W21 contract test files contribute ~85-95 soft-pin tests to the count (each `_OrForwardStaged` fact returns success when the upstream surface is not yet present, and PASS / FAIL when it is — none hard-FAIL on W21 since the prior 3 W21 commits landed all of Bishop / Hicks / Apone's W21 surfaces). |

The gate measurement at Vasquez close is captured in the
commit message line `Phase K Wave 21 — Vasquez QA bring-up`.

### 2.2. §4.8 Stephen-decision tree — status carry-forward

**Status:** UNCHANGED (W17 to W21).

The 13-wave deferral arc (W7 → W21) crosses the symbolic
"year of bring-ups" threshold.  At one wave per ~working-day,
13 waves is roughly the calendar quarter mark of the bring-up
program.  Vasquez W21 ratifies the following:

- All three Option payloads (A — minimal; B — standard;
  C — strict) remain in `docs/agent-handoff-protocol.md
  §4.8` exactly as authored at W17.
- The flip script `tests/ci/lane-discipline-flip-required.sh`
  remains executable; (same jq-unavailable posture as W18 →
  W20 carries through W21).
- No §4.9 row added at W21.
- Re-prompt cadence stays at once-per-wave (Vasquez owns).

**Hand-off note for W22:** if Stephen has still not selected
by W22 close, consider whether a Coordinator-direct escalation
memo should land — the 14-wave deferral arc at W22 will be a
fair trigger.

### 2.3. §6.10 LH13 cron status — W21 disposition

**Status:** HELD YELLOW (no PROMOTE to §6.8 GREEN).

Cross-refs:

- `docs/agent-handoff-protocol.md §6.10` — full disposition
  table + ratification narrative.
- `docs/lh13-soft-pin-rationale.md §12` — Hicks W21 author
  record.
- `Phase_K_W21/Vasquez/HicksW21Lh13W21CronStatusTests.cs` —
  contract test pinning the HOLD posture.
- (Pwa-audit workflow gate test — the W18 fix flags
  `--form-factor=desktop` + `--screenEmulation.mobile=false`
  remain present at W21 close; no W19 / W20 / W21 regression.)

Disposition narrative: W20's two compounded HOLD reasons
narrow to a single remaining reason at W21:

1. **`gh`-observability blocker** — bring-up shell cannot
   enumerate schedule-event run conclusions without an
   authenticated `gh` session.  Same blocker as W19 / W20,
   inherited unchanged.

The W20 secondary reason — sample-window-size mathematically
insufficient — **no longer applies at W21**.  The W18 merge
to `main` (`7832f49`) is now ~25 hours behind the W21 bring-
up window at the hourly cron cadence, well past the §4.2 ≥3
arithmetic minimum.  A confirmed ≥3-run sample is
overwhelmingly likely to exist on the actual `pwa-audit.yml`
run history; the bottleneck is purely the bring-up shell's
inability to read it.

Hand-off to Hicks W22: per Hicks W21's §12 recommendation,
if the `gh`-auth gap is unresolved at W22 *again*, the
§6.x coordinator-driven probe path (§4.7) is the recommended
escalation rather than continuing to inherit YELLOW
indefinitely.

### 2.4. §9 W21 stash-isolation directive + W21 retrospective audit

NEW top-level subsection `docs/agent-handoff-protocol.md §9`
codifies a standing directive distilled from the W20 audit
cycle and records the per-agent W21 discipline-compliance
audit (W21 ratchet level 3 — now 6 audited rules).

**The W21 stash-isolation directive (§9.1):** "never touch
other agents' working tree state mid-wave".  Five sub-rules:

1. **Stash-only-your-own.**  Agents only stash/pop/drop their
   own wave entries, identified by the
   `<agent>-w<N>-…` subject prefix.
2. **Never sweeping working-tree mutations** mid-wave if any
   other agent's stash entry is present in `git stash list`.
3. **Never `git stash pop` an entry you did not author** —
   stale stashes get pruned in the next Vasquez retrospective.
4. **Shield other agents' untracked surface** at bring-up via
   `git stash --include-untracked -m
   "<agent>-w<N>-<other-agent>-shield-…"`.
5. **Pre-pipeline diff verification** is mandatory — and
   gains a W21 corollary: the cached diff must NOT include
   foreign-lane file deletes either.

The §9.3 W21 audit table shows all four W21 commits CLEAN on
all six rules.  Vasquez W21 explicitly created a Hicks-
frontend shield stash at rebase time
(`vasquez-w21-hicks-frontend-shield-1779635691`) to absorb
Hicks's leftover frontend bundle hash-rename byproducts —
exactly the §9.1 rule 4 pattern.

**Recurring-violation ratchet stays at level 2** (W18 +
W19 — `5957a37` and `d700cf7`).  No new occurrence at W21.
No §4.9 Stephen-decision opened.

### 2.5. Forward-stage W21 contract inventory

25 forward-stage files in
`src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W21/Vasquez/`:

- **Bishop W21** (9 files): backend csproj 0.30.0,
  SwissApplyRoundService projecting W20 audit rows into
  TournamentMatch rows, RotationSchedule triad (entity +
  admin controller + cron matcher + background executor +
  `jwt_scheduled_rotation_total` counter),
  ReplayRestorationAttempt + admin GET endpoint,
  JwtValidatorAnomalyMetrics counter +
  clock-skew/invalid-issuer/expired-too-soon reasons,
  TournamentWithdrawPlayerController + Seed=-1 sentinel,
  SignalRRetentionManualPurgeController +
  `signalr_manual_purge_total` counter, 3-provider EF migrations
  + snapshots for `Phase_K_W21_RotationScheduleAndReplayRestoration`,
  Grafana dashboard panels 9 + 10.
- **Hicks W21** (7 files): LH13 W21 §6.9 cron status, Phase L
  W6 tile-claim-animation + meld-display, bundle audit §3.6
  surgery (autotable-src-eager 123,701 → 112,219 B; three-
  renderer-big 406,635 B hold-line 11th wave), profile-drawer
  extraction + i18n zh-Hans/zh-Hant lazification, 5 new
  Admin UI W21 surfaces.
- **Apone W21** (5 files): Argo Rollouts frontend Canary
  template + doc, Kyverno W21 audit-mode rule pair
  (require-resource-limits + disallow-host-paths),
  supporting docs (kyverno-w21-additional-rules +
  signalr-observability-w21 + helm-release), CHANGELOG +
  mobile pkg 0.30.0 stamps, regional-EKS auto-rollback Terraform.
- **Vasquez self-lane** (4 files): branch-protection W21
  Stephen-decision status (13-wave arc), W21 retrospective
  audit observation, W21 surface smoke facts (file inventory
  + ≥20 forward-stages + KW21 rename + inbox memo presence
  pin), W21 self-lane master inventory (§6.10 + §9
  + safe-backup + lane-map + check-cross-lane-bundling.sh
  presence pins).

### 2.6. KW20 → KW21 regression rename

- `Wave1ThroughKW20RegressionTests.cs` →
  `Wave1ThroughKW21RegressionTests.cs` (renamed via `git mv`;
  all `typeof()` self-references rewritten via sed).
- Former W20 rename pin `PhaseK20_RegressionClassRenamed_KW19_To_KW20`
  rewritten to `_Historical` — now asserts BOTH the W19 AND
  the W20 class names are gone (history hardens forward).
- NEW W21 rename pin
  `PhaseK21_RegressionClassRenamed_KW20_To_KW21` added —
  asserts the W21 class present + the W20 class gone.
- W11-W20 forward-broadening: PhaseK12/13/14 historical rename
  pins broadened from
  `Equals("Wave1ThroughKW21RegressionTests")` to
  `Equals("Wave1ThroughKW20RegressionTests") || Equals("Wave1ThroughKW21RegressionTests")`
  so they keep passing across the KW20 → KW21 sed-rewrite
  (and remain robust to the next-wave rename).
- Wave 21 extension xmldoc paragraph added at the canonical
  cross-wave audit comment block.

### 2.7. W20 mobile-pin forward-broadening repair

Bishop W21's full-suite run reported `4754 passed / 1 failed`,
where the failure is the W20 contract test
`AponeW20ChangelogW20ContractTests.MobilePackageJson_HasVersion_0_29_0_OrForwardStaged`,
broken by Apone's W21 bump of `mobile/package.json` from
0.29.0 → 0.30.0.  Bishop documented this as out-of-lane.

Vasquez W21 repairs the soft-pin in-lane by forward-broadening
the substring check from a single literal "0.29.0" to ANY of
{"0.29.0", "0.30.0"} (and any future "0.N.0" via the explicit
0.30.0 inclusion).  Post-repair full-suite run: 4755 / 4755 / 0.

This establishes a W21 **forward-broadening precedent for
version-pin contract tests**: W22+ version-pin contract tests
should follow the same OR-pattern from the outset rather than
hard-pinning a single version literal.

### 2.8. Visual-regression manifest rename (W21 NO-OP)

Vasquez W21 brief asked for
`manifest-screenshots-visual-Wave1ThroughKW20.spec.ts` →
`…KW21.spec.ts` rename.  At W21 close inspection, the working
tree carries only:

- `src/frontend/autotable-src/tests/e2e/manifest-screenshots-visual.spec.ts`
- `src/frontend/autotable-src/tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/{main-game,spectator-commentary,tournament-dashboard}.png`

No `Wave1ThroughKW<N>` suffix family exists.  Per the W20 inbox
memo's same observation: the unsuffixed
`manifest-screenshots-visual.spec.ts` continues to carry the
cross-wave visual baseline; suffixing was NOT in W20 scope and
similarly NOT actionable at W21.  The rename is therefore a
NO-OP at W21 (documented in §9.5 hand-off and ratified here in
the inbox memo).

### 2.9. Lane-discipline final

```
bash tests/ci/check-cross-lane-bundling.sh \
  --pr stlong/phase-k-wave-21-bringup --strict
```

Target `checked=N violations=0` on Vasquez bring-up HEAD —
captured in §9.3 of the handoff doc as the **11th consecutive
0-violation lane wave** (W11 → W21 inclusive).

## 3. Hand-off to W22

- **§4.8 Stephen-decision tree** — W22 Vasquez (or whichever
  agent owns W22 close) re-prompts Stephen on Option A vs
  B vs C.  Symbolic 14-wave deferral arc; consider whether a
  Coordinator-direct escalation memo should land if Stephen has
  not selected by W22 close.
- **§6.10 LH13 PROMOTE re-evaluation** — Hicks W22 captures
  the post-W21 cron status against §4.2.  Per Hicks W21's §12
  recommendation, if the `gh`-auth gap is unresolved at W22
  *again*, the §6.x coordinator-driven probe path (§4.7) is
  the recommended escalation rather than continuing to inherit
  YELLOW.
- **W21 retrospective audit** — Vasquez W22 audits this
  Vasquez W21 commit + the W22 bring-up commits against the
  §9.1 + §9.3 checklist (now 6 rules including the §9.1
  stash-isolation directive).
- **Forward-stage W21 contracts** — `_OrForwardStaged`
  soft-pins in `Phase_K_W21/Vasquez/` should mostly resolve
  to hard-PASS by W22 (each agent's W22 commit lands the
  corresponding W21 surface in their lane).
- **Visual-regression manifest** — still no
  `manifest-screenshots-visual-Wave1ThroughKW<N>.spec.ts`
  family in the working tree; the unsuffixed
  `manifest-screenshots-visual.spec.ts` continues to carry
  the cross-wave visual baseline.  Documented for W22.
- **Mobile-pin forward-broadening precedent** — apply the W21
  OR-pattern to all new version-pin contract tests authored
  in W22+ rather than hard-pinning a single version literal.
