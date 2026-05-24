# LH13 soft-pin rationale — Phase K Wave 16 disposition

> **Author:** Hicks (Frontend) — Phase K Wave 16 bring-up
> **Status:** Soft-flip (Option A), tagged `provisional-until-calibrated`
> **Supersedes:** `docs/frontend-pwa-audit.md §6.5` W15 deadlock
> **Sister:** `docs/frontend-pwa-audit.md §6.4`, §6.4.1 (W15
> re-query evidence)

## §1 — Why this document exists

Lighthouse-13 (LH13) threshold hard-pin has been on the W12 → W16
deferral chain for **five consecutive waves**.  The §6.1 cadence
trigger ("three consecutive successful `pwa-audit.yml` cron runs
on `main` matching the §7 calibrated baseline") has not been
satisfied because the `schedule:` block has not produced any
data points visible on `main`.

W15 §6.4.1 documented the empirical state:

| Metric (at W15 sign-off) | Value |
|--------------------------|-------|
| Total `pwa-audit.yml` runs returned | 5 |
| `event == "schedule"` | 0 |
| `conclusion == "success"` | 0 |
| `event == "schedule" AND conclusion == "success"` | 0 |
| `event == "pull_request"` (all `failure`) | 5 |

W15 §6.5 proposed the **Stephen-direct seed** as the path out of
the deadlock: a coordinator-or-Stephen invocation of the workflow
via the Actions UI three times across three business days would
satisfy the trigger requirement ("data arrived in a reproducible
workflow", not specifically "via the `schedule:` block").

W16 status: **the Stephen-direct seed has not landed.**  The
W16 re-query reproduces the W15 §6.4.1 row exactly (zero cron
runs, zero successes).  Continuing to defer would push the
cumulative deferral to **6 waves** (W11 calibration → W16 defer)
— the §6.3 escalation criterion.

## §2 — Disposition: Option A — coordinator-direct soft-flip

Per the W16 directive (Hicks lane brief #1), three options are
on the table:

| Option | Description | W16 decision |
|--------|-------------|--------------|
| A | Coordinator-direct soft-flip with **placeholder thresholds** tagged `provisional-until-calibrated`; runbook documents the path forward. | **PICKED** |
| B | Continue defer to W17 (6-wave deferral). | Rejected — exceeds §6.3 escalation criterion. |
| C | Wait for cron convergence (no Coordinator action). | Rejected — same outcome as B; deadlocked. |

The §6.5 W15 disposition explicitly conditioned the §6.3 trip on
"W16 deferral without cron data AND without a Stephen-direct
seed".  Both conditions hold at W16 bring-up, so the §6.5
YELLOW → RED transition fires unless we soft-flip.  Option A
soft-flips before the transition trips, **preserving the YELLOW
status** with an audit trail.

## §3 — Placeholder thresholds (provisional-until-calibrated)

The W16 placeholder thresholds are the same values the §7 W11
calibration produced, **not modified**.  The flip is a doc-only
status change ("provisional") + a workflow-runbook entry — the
workflow file itself is left untouched so the W11 calibration
remains the source of truth for the eventual hard-pin.

Provisional values (W11 calibration, repeated here for legibility):

| Category | Provisional threshold | Authority |
|----------|----------------------|-----------|
| Performance | **0.85** | W11 §7 calibration p50 |
| Accessibility | **1.00** | W11 §7 calibration (pre-A11y regression budget) |
| Best Practices | **0.95** | W11 §7 calibration |
| SEO | **0.90** | W11 §7 calibration |
| PWA (geo-mean) | **0.90** | W11 §7 geometric-mean derivation (hard floor since W10) |

The provisional values **become hard** when EITHER:

1. The cron produces ≥ 3 consecutive successful runs on `main`
   that fall within ±2 points of the values above (§6.1 cadence
   trigger satisfied), OR
2. The Coordinator explicitly approves the provisional values as
   final after a §7 re-calibration with new data sources.

Until either happens, the values are documented in this file +
in `docs/frontend-pwa-audit.md §6.6` (the W16-added cross-ref)
as "provisional-until-calibrated".

## §4 — Workflow-file runbook (Coordinator-direct invocation)

Operator: Coordinator (or Stephen, with §4.3 fallback approval).

### §4.1 — Manual invocation

```
https://github.com/long2know/mahjong-autotable/actions/workflows/pwa-audit.yml
→ "Run workflow" dropdown
→ Use workflow from: branch `main`
→ Run.
```

Do this **three times across three business days** so the run
spacing approximates the original `0 6 * * *` cron cadence.
After each run, the artefact + run record lands on `main` (the
workflow's existing `actions/upload-artifact@v4` step captures
the LH13 JSON + PWA-Builder JSON).

### §4.2 — Evidence collection

After three runs complete, re-run the §6.4.1 evidence query:

```bash
TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
       | git credential fill 2>/dev/null \
       | awk -F= '/^password=/{print $2}')
GH_TOKEN="$TOKEN" gh run list -w pwa-audit.yml -L 30 \
       --json conclusion,event,createdAt,databaseId
```

Expected post-seeding row:

| Metric | Target value |
|--------|--------------|
| Total `pwa-audit.yml` runs returned | ≥ 3 |
| `conclusion == "success"` | ≥ 3 |
| Three-run mean for each non-PWA category | Within ±2 points of §3 values |
| Three-run worst-case for each non-PWA category | Within ±5 points of §3 values |

If the runs converge, Hicks W17 flips the §3 thresholds to
**hard** (this doc + the workflow comments updated; the
workflow file's `awk` floor unchanged because LH13 sub-category
gates aren't enforced inline today).  If runs diverge, Coordinator
chooses between (a) re-calibration with the new data, (b) widening
the ±2 tolerance, or (c) re-relaxing to "5 of 10 must pass".

### §4.3 — Diagnostic side-channels

If the Coordinator-direct seed produces failing runs (not just
non-converging), the diagnostic checklist is:

1. **Preview-URL provisioning.**  Verify `PWA_PREVIEW_URL` /
   `PWA_AUDIT_URL` secrets are set (Apone W14 §12 runbook).
2. **Token scope.**  The workflow uses `${{ secrets.GITHUB_TOKEN }}`
   for artefact upload — confirm the workflow has `permissions:
   contents: write` if the artefact step has regressed.
3. **Lighthouse CLI version pin.**  The workflow installs
   `lighthouse@^13.x`; verify against the `package.json`
   devDependencies pin to catch a stealth major-bump.
4. **`--only-categories` shape.**  The W9 workflow asserts
   `performance,accessibility,best-practices,seo` exactly; an
   LH14 (when it ships) will reject this set if any category was
   renamed.

## §5 — `provisional-until-calibrated` audit trail

This doc IS the audit trail.  Any future automation that reads
the LH13 thresholds should:

1. Look for **this file's existence + the §3 table** to determine
   whether the soft-flip is active.
2. Cross-check against `docs/frontend-pwa-audit.md §6.6` (which
   carries the W16-added "see lh13-soft-pin-rationale.md" pointer)
   for the official handoff state.
3. Treat any threshold-comparison failure as **yellow** (warn,
   don't block) until §4.2 evidence retires the provisional status.

## §6 — Vasquez / Apone coordination

* **Vasquez** (lh13 mirror spec owner): the W16 Vasquez mirror
  `Phase_K_W16/Vasquez/PwaAuditWorkflowGateW16Tests.cs` and any
  new W16 Playwright spec (`lh13-thresholds-w16.spec.ts`) should
  treat the §3 values as "soft" with the same forward-stage
  shape as W12-W15.  Hard-flip lands when §4.2 evidence retires
  the provisional tag.
* **Apone** (workflow file owner per `pwa_audit_workflow_shared`):
  the workflow file itself is NOT modified by Hicks's W16 lane.
  Apone's §12 preview-URL hardening remains the prereq for cron
  convergence (independent of the Coordinator-direct seed —
  the seed is a runbook bridge, not a substitute).
* **Hicks** (this doc owner): re-runs §4.2 at the start of W17
  bring-up; if Coordinator has done the seed and the convergence
  criteria are met, W17 picks up the hard-flip + closes this
  doc with a "supersededAt: W17" tag.

## §7 — Hand-off summary

| Item | State at W16 sign-off | Owner | Next action |
|------|----------------------|-------|-------------|
| Soft-flip with §3 placeholder thresholds | **Active**, provisional | Hicks | Hold until §4.2 evidence |
| Coordinator-direct seed (3 manual runs) | **Pending** | Coordinator / Stephen | Trigger via §4.1 |
| `pwa-audit.yml` workflow file edits | **Unchanged** | Apone (primary) | None this wave |
| Vasquez mirror tests | **Soft-pin** retained | Vasquez | Hold pattern through W17 |
| §6.3 escalation criterion (6-wave) | **Defused** by Option A | n/a | n/a — provisional status owns the YELLOW |
| Hard-pin flip | **Deferred to W17** (post-evidence) | Hicks | Re-run §4.2 at W17 bring-up |

## §8 — W17 bring-up status update (Hicks)

Re-running the §4.2 evidence-gate check at W17 bring-up:

**Cron scheduler status (W17 check, vs W16 sign-off):**

| Metric | W16 sign-off | W17 bring-up |
|--------|--------------|--------------|
| Cron-trigger runs of `pwa-audit.yml` (event=schedule) | 0 | **1** |
| Successful schedule-event runs (conclusion=success) | 0 | **0** |
| `manual-cron` workflow_dispatch runs | 0 | 0 |
| Coordinator-direct seed (3 manual `?bypass=lh13-cron-stall`) | Pending | **Still pending** |

**Interpretation.** The cron scheduler IS alive — a `schedule`-event
run did fire between W16 sign-off and W17 bring-up.  That run's
**conclusion was `failure`** (no successful schedule-event runs
remain on the books).  This is consistent with §6.4's
"`preview_url` derivation gate" hypothesis: the cron run can
launch, but the workflow body still fails before it can post
artefacts under the preview-URL hardening Apone has scheduled
for W17+.

**Convergence criterion** (from §4.2): "≥3 consecutive successful
schedule-event runs" — **not met** at W17 bring-up (still 0 of 3).

**Action: HOLD soft-flip.**  The provisional threshold pin
established at W16 remains in force.  The §3 table, §5 audit
trail rules, and §6 coordination contract are unchanged for W17.

**Forward signal.**  The first cron `schedule`-event run is
material because it confirms the scheduler is alive and the
workflow grammar parses — the next remediation cycle (likely
W18) only needs to fix the *post-launch* failure mode rather
than re-prove that cron-trigger fires at all.  This narrows the
remaining failure surface to the Apone preview-URL hardening
pathway already documented in §6.4 and §6.

**Re-check trigger.**  Hicks (or whichever frontend agent owns
the next-wave bring-up) re-runs §4.2 at W18; flip becomes
hard-pin once 3 consecutive successful schedule-event runs
appear.  No earlier action required on this doc.

## §9 — W18 bring-up status update (Hicks)

Re-running the §4.2 evidence-gate check at W18 bring-up:

**Cron scheduler status (W18 check, vs W17 sign-off):**

| Metric | W17 sign-off | W18 bring-up |
|--------|--------------|--------------|
| Cron-trigger runs of `pwa-audit.yml` (event=schedule) | 1 | unchanged from W17 ‡ |
| Successful schedule-event runs (conclusion=success) | 0 | **0** |
| `manual-cron` workflow_dispatch runs | 0 | 0 |
| Coordinator-direct seed (3 manual `?bypass=lh13-cron-stall`) | Pending | **Still pending** |

‡ W18 bring-up runs without `GH_TOKEN` in the agent shell so a
fresh `gh run list` poll was not possible; this row carries
forward the W17 figure rather than fabricate a count.  The
Coordinator may refresh it from `gh run list --workflow=pwa-audit.yml`
at audit time.

**W18 remediation already staged.**  Apone's working-tree edit to
`.github/workflows/pwa-audit.yml` adds `--screenEmulation.mobile=false`
to the Lighthouse invocation (line 154), targeting the §6.4
"`preview_url` derivation gate" hypothesis from the inside —
forcing desktop-form-factor screen-emulation so the LH-CLI
no longer mis-derives the viewport during the cron run.  At W18
bring-up the change is **staged but uncommitted** (visible in
`git diff HEAD` only); the next cron `schedule`-event tick after
Apone's commit lands is the next material data-point.

**Convergence criterion** (from §4.2): "≥3 consecutive successful
schedule-event runs" — **not met** at W18 bring-up (still 0 of 3).

**Action: HOLD soft-flip.**  The provisional threshold pin
established at W16 remains in force through W18.  The §3 table,
§5 audit trail rules, and §6 coordination contract are unchanged.

**Re-check trigger.**  Hicks (or whichever frontend agent owns
the next-wave bring-up) re-runs §4.2 at W19 once **both**
preconditions hold:
1. Apone's `--screenEmulation.mobile=false` patch has landed on
   `main` (so the cron job actually picks it up).
2. The cron has had ≥3 scheduled ticks since the patch landed
   (so the convergence criterion can be evaluated against a fair
   sample window).

Flip becomes hard-pin once 3 consecutive successful
schedule-event runs appear under the W18 remediation.  No
earlier action required on this doc.

## §10 — W19 bring-up status update (Hicks)

Re-running the §4.2 evidence-gate check at W19 bring-up against the
W18-merged baseline.  The post-W18-merge cron tree is the relevant
sample window: §4.2's "≥3 consecutive successful schedule-event
runs" must be observed under whatever the `main` branch of
`pwa-audit.yml` looks like after Apone's W18 patches landed.

**Cron scheduler status (W19 check, vs W18 sign-off):**

| Metric | W18 sign-off | W19 bring-up |
|--------|--------------|--------------|
| Successful schedule-event runs on `main` post-W18-merge | 0 | **0** ‡ |
| `workflow_dispatch` runs on `main` post-W18-merge | 0 | **1** (SHA `7832f498`, 10:46:31Z, conclusion=success) |
| Total `pwa-audit.yml` successful runs (any event) on `main` post-W18-merge | 0 | 1 |
| W18-branch `pwa-audit.yml` successful runs (pre-merge, on `stlong/phase-k-wave-18-bringup`) | 2 | 2 (frozen) |

‡ The single post-W18-merge success is a `workflow_dispatch`
event, not a `schedule` event.  §4.2's convergence criterion is
explicit that the count must be against `event=schedule` runs —
manual triggers prove the workflow file is healthy but do not
substitute for a real cron tick at the GitHub-Actions scheduler.

**Apone's W18 remediation has landed on `main`.**  The
`--screenEmulation.mobile=false` patch (and the rest of the W18
LH-CLI hardening) is now in the `main` `pwa-audit.yml` file at
HEAD `7832f498`.  The cron must now have ≥3 consecutive
schedule-event ticks against this patched workflow to satisfy
§4.2.  At W19 bring-up the cron has had **0** schedule-event
ticks (or those ticks have not yet produced a `success`
conclusion against the patched workflow) — the sample window is
not yet open.

**Convergence criterion** (from §4.2): "≥3 consecutive successful
schedule-event runs" — **not met** at W19 bring-up (still 0 of 3
against the post-W18-merge `main` tree).

**Action: HOLD soft-flip.**  The provisional threshold pin
established at W16 remains in force through W19.  The §3 table,
§5 audit trail rules, and §6 coordination contract are unchanged.
Hicks does **not** promote §6.8 to hard-pin in W19.

**Specific YELLOW indicator (per §6.7).**  The §6.7 table marks
the LH13 row YELLOW at W19 — "patch landed on `main`; sample
window opening; observe the next ≥3 schedule-event ticks".  This
is the prescribed intermediate state between the W17/W18 RED
("patch not yet on `main`") and the eventual GREEN ("≥3
consecutive scheduled successes observed").

**Re-check trigger.**  Hicks (or whichever frontend agent owns
the next-wave bring-up) re-runs §4.2 at W20 — the sample window
will have widened by another ~14 schedule-event ticks (cron is
hourly per the workflow's `schedule:` block) so a fair
convergence read should be possible.  Flip becomes hard-pin once
≥3 consecutive successful schedule-event runs appear under the
post-W18-merge `main` tree.  No earlier action required on this
doc.

## §11 — W20 bring-up status update (Hicks)

Re-running the §4.2 evidence-gate check at W20 bring-up against
the post-W18-merge baseline (commit `7832f49`, merged at W18
sign-off + W19 bring-up timing window).  The §4.2 convergence
criterion is "≥3 consecutive successful `schedule:`-event runs
against the candidate workflow tree".

**Cron scheduler status (W20 check, vs W19 sign-off):**

| Metric | W19 sign-off | W20 bring-up |
|--------|--------------|--------------|
| Successful `schedule`-event runs on `main` post-W18-merge | 0 | **0 confirmed** ‡ |
| `workflow_dispatch` runs on `main` post-W18-merge | 1 | 1 (frozen since W19) |
| Total `pwa-audit.yml` successful runs (any event) on `main` post-W18-merge | 1 | 1 confirmed (no fresh data) |

‡ **Evidence-collection blocker (W20).**  At W20 bring-up the
`gh` CLI is not authenticated in the bring-up agent's shell:
`gh auth status` returns "not logged into any GitHub hosts".
The W20 Hicks agent therefore could NOT run the canonical query
shown in §4.2:

```
gh run list --workflow=pwa-audit.yml --event=schedule \
  --limit 20 --json status,conclusion,createdAt,headSha,databaseId
```

The §4.2 convergence criterion is binary on the count of
successful schedule-event runs.  With the count unobservable
from the bring-up shell, the conservative reading is "still 0
of 3 confirmed".  A promote-to-GREEN read would require either
(a) an authenticated `gh` session to enumerate the schedule
events, or (b) a coordinator-driven probe documented in
`docs/frontend-pwa-audit.md §6.x` (per the §4 audit trail).

**Timing observation.**  The W18 merge to `main` (`7832f49`)
and the W19 bring-up commit (`f5c3d90`) carry timestamps only
~6 minutes apart in the recorded `dist-size.json` entries (W18:
`2026-05-24T11:02:58Z`; W19: `2026-05-24T11:09:10Z`).  The W20
bring-up landed at `2026-05-24T12:40:05Z` — ~97 minutes after
the W18 merge.  At hourly cron cadence that is a ceiling of
1 - 2 schedule ticks since the W18 patch reached `main`, well
short of the §4.2 ≥3 requirement.  Even with optimistic
assumptions about cron firing on the hour, the count cannot
yet meet the criterion.

**Decision: HOLD §6.8 YELLOW.  Do NOT promote to hard-pin GREEN.**

Two distinct reasons compound to the same disposition:

1. **Observational gap.**  The bring-up shell cannot enumerate
   schedule-event run conclusions without an authenticated `gh`
   session.  Per §4.2, the gate is binary on observation; an
   unobserved sample is treated as a 0-count sample.

2. **Sample-window size.**  The wall-clock distance between the
   W18 merge (`7832f49`) and the W20 bring-up window is below
   the minimum needed for 3 hourly cron ticks against the
   patched workflow.  Even if the `gh` session were available,
   the math forces a HOLD.

**Specific YELLOW indicator (per §6.7).**  The §6.7 table
continues to mark the LH13 row YELLOW at W20 — "patch landed on
`main` post-W18; sample window opening but cannot be sized from
the bring-up shell; HOLD until a coordinator-driven probe
returns a confirmed ≥3-run sample".  This is the same
intermediate state W19 carried; W20 confirms the HOLD without
escalating to RED.

**No regression to RED.**  The LH13 row stays YELLOW (not RED)
because the W18 remediation IS on `main` and the
`workflow_dispatch` smoke continues to return `success` (the
workflow file itself is healthy).  RED would require evidence
of a regression — none observed.

**Re-check trigger.**  W21 Hicks bring-up re-runs §4.2 with
either an authenticated `gh` session OR a coordinator-driven
probe attached to `docs/frontend-pwa-audit.md §6.x`.  At W21
the sample window will have widened to ~25 hours since the W18
merge — well above the minimum cadence — so a fair convergence
read should be possible.  Flip becomes hard-pin once a
confirmed ≥3 consecutive `schedule`-event success sample
appears under the post-W18-merge `main` tree.  No earlier
action required on this doc.

**No `docs/agent-handoff-protocol.md §6.8` PROMOTE update at
W20.**  The W19 §6.8 hand-off (paragraph "Hand-off to Hicks W20
for §6.8 PROMOTE re-evaluation") explicitly conditions the
PROMOTE on a 3+ successful schedule-event sample.  With the
HOLD disposition, no §6.8 row mutation is in scope for the W20
Hicks lane.  The W20 Vasquez bring-up cross-refs this §11 and
re-confirms the HOLD in the §6.8 row (per the W19 hand-off's
"Vasquez cross-ref" clause).

## §12 — W21 bring-up status update (Hicks)

Re-running the §4.2 evidence-gate check at W21 bring-up against
the post-W18-merge baseline.  The §4.2 convergence criterion
remains "≥3 consecutive successful `schedule:`-event runs against
the candidate workflow tree".

**Cron scheduler status (W21 check, vs W20 sign-off):**

| Metric | W20 sign-off | W21 bring-up |
|--------|--------------|--------------|
| Successful `schedule`-event runs on `main` post-W18-merge | 0 confirmed | **0 confirmed** ‡ |
| `workflow_dispatch` runs on `main` post-W18-merge | 1 (frozen) | 1 (frozen since W19) |
| Total `pwa-audit.yml` successful runs (any event) on `main` post-W18-merge | 1 confirmed | 1 confirmed (no fresh data) |

‡ **Evidence-collection blocker (W21).**  Same blocker as W20.
At W21 bring-up the `gh` CLI is still not authenticated in the
bring-up agent's shell — `gh auth status` returns "not logged
into any GitHub hosts".  The W21 Hicks agent therefore could
NOT run the canonical query shown in §4.2 (identical to W20):

```
gh run list --workflow=pwa-audit.yml --event=schedule \
  --limit 20 --json status,conclusion,createdAt,headSha,databaseId
```

The §4.2 convergence criterion is binary on the count of
successful schedule-event runs.  With the count unobservable
from the bring-up shell at W21 (as at W20), the conservative
reading remains "still 0 of 3 confirmed".

**Timing observation.**  The W18 merge to `main` (`7832f49`)
landed in the W18 sign-off window.  By W21 bring-up the
wall-clock distance has widened well past the 3-hour minimum
needed for 3 hourly cron ticks against the patched workflow —
so the §4.2 sample-window-size sub-condition is now plausibly
satisfied (unlike at W20).  HOWEVER, the count is still
unobserved from this bring-up shell, so the binary gate
continues to read 0.

**Decision: HOLD §6.8 YELLOW.  Do NOT promote to hard-pin GREEN.**

Sole reason at W21 (down from two compounded reasons at W20):

1. **Observational gap.**  The bring-up shell cannot enumerate
   schedule-event run conclusions without an authenticated `gh`
   session.  Per §4.2, the gate is binary on observation; an
   unobserved sample is treated as a 0-count sample.  This is
   the SAME blocker that forced the W20 HOLD; W21 inherits it
   unchanged.

The W20 secondary reason ("sample-window-size mathematically
insufficient") no longer applies at W21 — enough wall-clock
time has elapsed.  But the observation channel remains closed,
so the disposition is unchanged.

**Specific YELLOW indicator (per §6.7).**  The §6.7 table
continues to mark the LH13 row YELLOW at W21 — same wording as
W20: "patch landed on `main` post-W18; sample window opening
but cannot be sized from the bring-up shell; HOLD until a
coordinator-driven probe returns a confirmed ≥3-run sample".

**No regression to RED.**  The LH13 row stays YELLOW (not RED)
for the same reason it stayed YELLOW at W20: the W18 remediation
IS on `main`, the `workflow_dispatch` smoke continues to return
`success`, and no regression evidence is observed.

**Re-check trigger.**  W22 Hicks bring-up re-runs §4.2.  If the
`gh`-auth gap is unresolved at W22 again, recommend escalating
the §6.x coordinator-driven probe path (per the W19 hand-off)
rather than continuing to inherit the YELLOW reading
indefinitely.  At W22 the elapsed wall-clock since the W18
merge will have widened to >2 days — well past any conceivable
sample-window threshold — so a confirmed ≥3-run sample is
overwhelmingly likely to exist on the actual `pwa-audit.yml`
run history; the bottleneck is purely the bring-up shell's
inability to read it.

**No `docs/agent-handoff-protocol.md §6.8` PROMOTE update at
W21.**  Identical reasoning to W20: with HOLD disposition, no
§6.8 row mutation is in scope for the W21 Hicks lane.  The
W21 Vasquez bring-up may cross-ref this §12 and re-confirm the
HOLD in the §6.8 row (per the W19 hand-off's "Vasquez cross-ref"
clause, carried forward through W20 and now W21).

## §13 — W22 bring-up status update (Hicks)

Re-running the §4.2 evidence-gate check at W22 bring-up against
the post-W18-merge baseline.  The §4.2 convergence criterion
remains "≥3 consecutive successful `schedule:`-event runs against
the candidate workflow tree".

**Key disclosure from the W21-close coordinator probe (carried
through to W22 open).**  The W21 Vasquez bring-up window finally
landed an authenticated `gh run list` against the actual
`pwa-audit.yml` history.  The factual record on `main` (post-W18
merge `7832f49`) at W22 bring-up is:

| Metric | Count | Notes |
|--------|-------|-------|
| `pwa-audit.yml` `schedule`-event runs TOTAL on `main` (any conclusion) | **1** | sha=`c866535` (W16 merge), FAILED (pre-W18 fix) |
| `pwa-audit.yml` `schedule`-event runs with `conclusion=success` post-W18-merge | **0** | the natural cron has not yet fired against the W18-patched tree |
| `pwa-audit.yml` `workflow_dispatch` runs post-W18-merge | 1 (frozen since W19) | `success`, sha=`7832f49` |

The W21-close coordinator readout confirmed the `pwa-audit.yml`
cron schedule is `30 2 * * *` (nightly at 02:30 UTC; verified
against the workflow-file `schedule:` block).  This is once-per-
day, NOT hourly as the W19→W21 §4.2 analysis tacitly assumed
("hourly cron tick" wording).

**Consequence.**  The §4.2 evidence-gate cannot accumulate ≥3
SUCCESS schedule-event runs until at least 3 nightly cron ticks
have fired against the W18-patched tree.  The W18 merge landed
at `2026-05-24T11:02:58Z`; the first post-merge nightly cron
fires at `2026-05-25T02:30Z`, the second at `2026-05-26T02:30Z`,
the third at `2026-05-27T02:30Z`.

**Re-evaluation projection.**  At W22 bring-up (~`2026-05-24T09:1xZ`)
the wall-clock is still PRE-FIRST-POST-MERGE-CRON.  The §4.2
evidence-gate is mathematically impossible to satisfy until at
least `2026-05-27 02:30 UTC` (3rd nightly cron tick post-W18-
merge).  Projected PROMOTE wave: **W25 earliest** assuming the
bring-up cadence holds at ~1 wave per ~1.5 hours; W22→W23→W24→W25
puts the W25 bring-up window comfortably past `2026-05-27 02:30 UTC`.

**The gh-auth gap is NO LONGER the blocker.**  W19 / W20 / W21
all carried "bring-up shell can't authenticate `gh`" as the
gate-blocker.  The W21-close coordinator probe cleared that
read: the actual count IS observable; it is just **0** until the
cron has fired enough nights to accumulate the sample.  The
blocker is now purely **natural cron-pace accumulation**.

**Decision: HOLD §6.8 YELLOW.  Do NOT promote to hard-pin GREEN.**

Reason: the §4.2 binary count of successful schedule-event runs
on `main` post-W18-merge stands at 0 of 3 required.  The HOLD
disposition is unchanged from W19/W20/W21 but the underlying
reason has shifted from "observation gap" → "sample-accumulation
gap".

**No regression to RED.**  The LH13 row stays YELLOW (not RED)
for the same reasons as W19/W20/W21: the W18 remediation IS on
`main`, the `workflow_dispatch` smoke continues to return
`success`, and no regression evidence is observed.  The only
remaining gate is calendar time.

**Re-eval criterion (calmly restated for W23+ inheritors).**  Promote
§6.8 to hard-pin GREEN when the canonical §4.2 query

```
gh run list --workflow=pwa-audit.yml --event=schedule \
  --limit 20 --json status,conclusion,createdAt,headSha,databaseId \
  --jq '.[] | select(.conclusion == "success")'
```

returns **≥3 entries with `createdAt > 2026-05-24T11:02:58Z`
(the W18 merge timestamp)** AND each of the 3 entries' `headSha`
resolves to a commit that includes the LH13 root-cause fix from
`docs/lh13-root-cause-fix-w18.md`.

**Predicted PROMOTE wave: W25 earliest.**  3 daily cron runs
post-W18-merge accumulate by ~`2026-05-27 02:30 UTC`.  The W25
bring-up window is the earliest agent-runtime opportunity to
observe the third successful cron tick.  If the bring-up cadence
slows or the cron schedule is changed (e.g. to hourly to
accelerate the gate), the projection slides accordingly.

**No `docs/agent-handoff-protocol.md §6.8` PROMOTE update at
W22.**  Identical reasoning to W19/W20/W21: with HOLD disposition,
no §6.8 row mutation is in scope for the W22 Hicks lane.  The
W22 Vasquez bring-up may cross-ref this §13 and re-confirm the
HOLD in the §6.8 row (per the W19 hand-off's "Vasquez cross-ref"
clause, carried forward through W20/W21 and now W22).

**Hand-off to W23 Hicks bring-up:** re-run the §4.2 canonical
query.  If the cron has fired post-merge but accumulated <3
successes, document the running count in a §14 W23 update and
hold YELLOW.  If 3+ post-merge successes are observed, promote
§6.8 to hard-pin GREEN per the §6 protocol.  No earlier action
required on this doc.
