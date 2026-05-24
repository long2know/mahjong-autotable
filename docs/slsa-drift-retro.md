# SLSA-3 drift detection — W23 first-run retro

> Phase K Wave 23 — Apone (DevOps).
> Audience: Apone (next bring-up) + SRE / supply-chain
> on-call. Companion to
> [`docs/slsa-drift-detection.md`](./slsa-drift-detection.md)
> (the W22 workflow + algorithm reference) and to
> [`docs/slsa-pinning-w20-sweep.md`](./slsa-pinning-w20-sweep.md)
> (the W20 sweep ledger).

W22 shipped `.github/workflows/slsa-drift-detection.yml`
on the Monday-07:00-UTC cron. W23 lands this retro
covering the FIRST week's run (or — if the cron has not
yet fired by W23 bring-up day — the runbook for the
analysis when the first run completes).

## 1. First-run status

> **Status at W23 ship:** First run pending. The W22
> PR merged on `2027-02-26`; the first scheduled
> Monday-07:00-UTC cron fires on the first Monday
> ≥ W22-merge-day. W23 bring-up runs before that
> window closes, so this retro is the RUNBOOK FOR
> ANALYSIS rather than a post-hoc capture.

When the first run lands, the bring-up owner appends
the run summary to [§4](#4-first-run-summary) as a
table row + drops any drift-hit artefacts into
[§5](#5-drift-hit-archive).

If by W24 bring-up the first run has fired, W24's
owner does the capture and updates this doc — leaving
W23 as the runbook anchor.

## 2. Expected drift indicators

The W22 algorithm classifies a `uses:` line as
**drift** when:

1. The ref-part (post-`@`) does NOT match the
   40-character hex SHA regex `^[0-9a-fA-F]{40}$`.
2. AND the action prefix is NOT in the §4 allow-list
   in [`docs/slsa-drift-detection.md`](./slsa-drift-detection.md):
   - `slsa-framework/slsa-github-generator/` —
     SLSA-3 spec carve-out (tag-pin is the OIDC trust
     anchor).
   - `./` — local workflow references (no SHA to
     pin; ships in the same commit as the caller).

### 2.1 Drift shapes we expect to see (FAIL hits)

| Shape | Example | Likely cause |
| ----- | ------- | ------------ |
| **Semver tag** | `uses: actions/checkout@v4` | New workflow author skipped the pin; merge conflict resolved against a stale baseline; copy-paste from a third-party README. |
| **Major-only tag** | `uses: actions/setup-node@v3` | Same as above; common shape from action-publishing-templates. |
| **Branch ref** | `uses: actions/checkout@main` | Anti-pattern; should NEVER land in main. Surfaces a real supply-chain risk. |
| **Latest** | `uses: docker/setup-buildx-action@latest` | Anti-pattern; same as `@main` from a trust perspective. |
| **Pre-release** | `uses: foo/bar@v2.0.0-rc.1` | Rare; legitimate use-cases trigger §3 allow-list rationale.|

### 2.2 Drift shapes we DO NOT expect to see (PASS hits)

| Shape | Example | Why allow-listed |
| ----- | ------- | ---------------- |
| **40-char SHA** | `uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2` | The SLSA-3 canonical shape. Every apone-lane workflow ships this pattern post-W20 sweep. |
| **SLSA generator tag** | `uses: slsa-framework/slsa-github-generator/.github/workflows/generator_generic_slsa3.yml@v2.0.0` | SLSA-3 spec carve-out — tag is the OIDC trust anchor. |
| **Local workflow** | `uses: ./.github/workflows/X.yml` | Same-repo reference; no third-party trust surface. |

### 2.3 Baseline expectation at W22 ship

Per the W22 inbox memo + the W20 sweep ledger:

* `apone-lane` workflows are SHA-pinned (197 pins / 39
  workflows landed at W19/W20).
* `vasquez-lane` workflows have 9 documented un-pinned
  refs in the [W20 sweep ledger](./slsa-pinning-w20-sweep.md)
  pending a vasquez-lane follow-up sweep.

**Expected W23-first-run drift count: between 0 and 9**:

* **0** if the vasquez-lane sweep landed before the
  first cron fire.
* **9** if the vasquez-lane sweep has NOT landed.
* **Other count** indicates a NEW regression — not the
  documented vasquez carry — and triggers the §3
  remediation flow.

## 3. Remediation flow

When the weekly cron run goes RED:

### 3.1 Identify the hits

The workflow uploads `drift-hits.txt` as an artefact +
opens / updates a GitHub issue tagged `slsa-drift` +
`apone`. The artefact lists, one-per-line:

```
<workflow-file>:<line>: uses: <action>@<ref>
```

### 3.2 Classify each hit

For each `drift-hits.txt` row:

1. Is the hit one of the 9 documented vasquez-lane
   refs from [`docs/slsa-pinning-w20-sweep.md`](./slsa-pinning-w20-sweep.md)?
   → Track in the per-wave inbox memo; remediation
   waits for the vasquez-lane sweep.
2. Is the hit a NEW regression (a ref that landed
   AFTER W20)?
   → **High priority.** Walk §3.3 immediately.
3. Is the hit a legitimate new tag-pin carve-out
   (e.g. a new reusable workflow with the SLSA-3
   trust shape)?
   → Walk §3.4 (allow-list expansion).

### 3.3 Rewrite the pin

For each NEW regression:

```bash
# 1. Look up the SHA for the current tag.
gh api -X GET "/repos/<owner>/<action>/git/ref/tags/<tag>" \
   --jq '.object.sha'

# 2. Edit the workflow file:
#    uses: <owner>/<action>@<tag>
#  →
#    uses: <owner>/<action>@<sha40> # <tag>

# 3. Run actionlint to confirm parse.
.work/apone-w19-tools/actionlint <workflow-file>

# 4. Run the drift workflow locally via workflow_dispatch.
gh workflow run slsa-drift-detection.yml \
   -f verbose=true \
   --ref <branch>

# 5. Watch for green:
gh run watch
```

Land the rewrite as a single-file PR tagged `slsa-drift`.
Per the W22 hand-off, the 7-day grace window allows
landing the fix Mon-following-fail.

### 3.4 Allow-list expansion

If a new legitimate non-SHA pin needs to be carve-listed:

1. Edit `.github/workflows/slsa-drift-detection.yml`:
   add the new prefix to the allow-list block (`Allow-list
   N — <description>` comment header).
2. Edit [`docs/slsa-drift-detection.md §4`](./slsa-drift-detection.md):
   add the rationale row.
3. Re-run drift detection to confirm the new entry
   suppresses the FP.

The allow-list change MUST land in the same PR as the
new workflow that needs it — never an empty allow-list
expansion.

## 4. First-run summary

> Filled in by W23+ bring-up owner once the first cron
> run completes. Until then, this section reads
> "Pending."

| Field                | Value     |
| -------------------- | --------- |
| First-run timestamp  | _Pending_ |
| Workflows scanned    | _Pending_ |
| Total `uses:` lines  | _Pending_ |
| Drift hits           | _Pending_ |
| Vasquez-lane carry   | _Pending_ |
| New regressions      | _Pending_ |
| Issue link           | _Pending_ |

### 4.1 Expected summary shape (template)

When the first-run capture lands, expect a row like:

```
| First-run timestamp | 2027-03-01 07:00:00 UTC |
| Workflows scanned   | 46                      |
| Total `uses:` lines | 421                     |
| Drift hits          | 9                       |
| Vasquez-lane carry  | 9 (matches W20 sweep)   |
| New regressions     | 0                       |
| Issue link          | #71                     |
```

That shape is the GREEN-line — drift count matches
the documented carry, no new regressions. A RED line
shows `New regressions > 0`.

## 5. Drift-hit archive

> Cron-run drift artefacts archived here as plain-text
> files named `drift-run-<YYYY-MM-DD>.txt`. Filenames
> sort chronologically; the most recent file is the
> current week's baseline.

Until the first run lands, this section is empty.

## 6. W23 → W24 hand-off

* **W24 bring-up owner runs the capture.** If the
  first cron has fired by W24 bring-up day:
  * Append the first-run summary to [§4.1](#41-expected-summary-shape-template).
  * Drop the artefact into [§5](#5-drift-hit-archive).
  * If `New regressions > 0`: walk §3.3 + open a
    `slsa-drift` PR in the same wave.
* **If first cron has NOT fired by W24:** carry this
  retro forward unchanged; W24 bring-up owner adds a
  one-line "Carried from W23 — first cron still
  pending" note + sets a reminder for W25.
* **Long-term:** once the workflow has 4 consecutive
  GREEN runs, the retro graduates to a
  "sustaining-surface confirmed" status; subsequent
  RED runs are handled inline in the per-wave inbox
  memo rather than appending here.

## 7. Cross-references

* [`.github/workflows/slsa-drift-detection.yml`](../.github/workflows/slsa-drift-detection.yml)
  — the W22 workflow.
* [`docs/slsa-drift-detection.md`](./slsa-drift-detection.md)
  — the W22 algorithm + allow-list rationale.
* [`docs/slsa-pinning-w20-sweep.md`](./slsa-pinning-w20-sweep.md)
  — the W20 sweep ledger + the 9 vasquez-lane carry.
* [`docs/slsa-provenance.md`](./slsa-provenance.md)
  — repo-wide SLSA-3 stance.
* SLSA-3 reusable-workflow tag-pin carve-out:
  <https://slsa.dev/spec/v1.0/requirements#build-isolated>.
