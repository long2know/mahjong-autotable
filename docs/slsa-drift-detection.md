# SLSA-3 drift detection — W22 sustaining workflow

> Phase K Wave 22 — Apone (DevOps).
> Audience: SRE / supply-chain on-call when the weekly
> `slsa-drift-detection` run goes RED. Companion to
> [`docs/slsa-pinning-w20-sweep.md`](./slsa-pinning-w20-sweep.md)
> (the W20 sweep ledger) and to
> [`docs/slsa-provenance.md`](./slsa-provenance.md) (the
> repo-wide SLSA-3 stance).

## 1. Background

W18 closed the apone-lane SHA-pinning sweep (191 pins / 39
workflows). W19 added 6 more (197 pins). W20 documented the
9 remaining un-pinned refs in vasquez-lane workflows for a
parallel lane-pure follow-up — bringing the repo to a
clean 206-pin posture at W20+ once the Vasquez-lane sweep
lands.

The sweep itself was a one-shot exercise. W22 adds the
**SUSTAINING SURFACE** — a weekly drift-detection workflow
that catches the regression where:

* A future PR adds a new workflow file with
  `uses: actions/checkout@v4` (semver tag, not SHA).
* A future PR upgrades an action and "forgets" the SHA
  pin, dropping back to `@v4` shape.
* A merge conflict resolution silently strips a SHA pin.

Without drift detection, the supply-chain posture
degrades silently between waves. With drift detection,
the regression surfaces within 7 days of the offending
commit.

## 2. The workflow

[`.github/workflows/slsa-drift-detection.yml`](../.github/workflows/slsa-drift-detection.yml)

### 2.1 Schedule

* **Weekly cron**: `0 7 * * 1` (Monday 07:00 UTC). One
  hour after the W17 lane-discipline-nightly run (06:00
  UTC daily) — the lane baseline is available for
  cross-reference when drift surfaces.
* **Manual**: `workflow_dispatch` with a `verbose` toggle
  that prints every checked ref (default off).

### 2.2 Algorithm

1. Walk every `.github/workflows/*.yml` file in the
   checked-out tree.
2. For each `uses: <action>@<ref>` line, extract the ref.
3. Skip the ref if it matches the **allow-list**:
   * `slsa-framework/slsa-github-generator/...` — the
     SLSA-3 reusable workflow; spec carves out the
     tag-pin shape (see [§4](#4-allow-list-rationale)).
   * `./*` — local workflow references; the workflow file
     itself is in the same repo, no SHA to pin.
4. Otherwise, the post-`@` portion MUST be a 40-character
   hex SHA (case-insensitive). Anything else (semver
   tag `v4`, branch name `main`, `latest`, etc.) counts
   as drift.
5. Drift hits are collected into `drift-hits.txt`,
   uploaded as a workflow artefact, and surfaced as:
   * a failed CI run (exit 1 on `drift_count > 0`);
   * a new / updated GitHub issue tagged `slsa-drift` +
     `apone` when triggered by the scheduled cron.

### 2.3 Output shape

Clean run (no drift):

```
==== No drift — every `uses:` ref is a 40-char SHA pin (or allow-listed) ====
```

Drift run:

```
::error::SLSA-3 drift detected — 3 non-SHA `uses:` ref(s) outside the allow-list
==== Drift report ====
DRIFT: .github/workflows/lane-discipline.yml:42:        uses: actions/checkout@v4
DRIFT: .github/workflows/playwright-visual-regression.yml:68:        uses: actions/checkout@v4
DRIFT: .github/workflows/lane-discipline-status.yml:35:        uses: actions/checkout@v4
```

The `<file>:<lineno>:` prefix makes the report directly
clickable when surfaced through the GitHub Actions UI.

## 3. Operator runbook — on drift

1. **Acknowledge the issue.** The scheduled run will
   open / update the `slsa-drift` tracking issue with
   the drift report inline.
2. **Identify the offending commit.** `git log -p --
   .github/workflows/<file>` on the drift-hit file
   reveals when the `@<semver>` shape was introduced.
3. **Decide the canonical SHA.** Cross-reference the
   action against existing pins in the repo:

   ```bash
   $ grep -rE "actions/checkout@[0-9a-f]{40}" .github/workflows/ \
       | awk -F '@' '{print $2}' \
       | awk '{print $1}' \
       | sort -u
   11bd71901bbe5b1630ceea73d27597364c9af683
   ```

   When a single SHA dominates, that's the canonical pin.
   When multiple SHAs are in flight, pick the one with
   the highest semver tag suffix (`# v4.2.2` beats
   `# v4.2.0`).
4. **Land the pin rewrite.** Update the offending file in
   the offending agent's lane:
   * `lane-discipline*.yml` + `playwright-visual-
     regression.yml` are **vasquez-lane** — open a
     vasquez-lane PR.
   * Apone-owned workflows (everything else) — apone-
     lane PR.
5. **Re-run the drift detection** via
   `workflow_dispatch` to confirm the fix.

## 4. Allow-list rationale

### 4.1 SLSA reusable workflow

The repo has exactly one non-SHA `uses:` reference that
is INTENTIONAL:

```yaml
.github/workflows/slsa-provenance.yml:306:
  uses: slsa-framework/slsa-github-generator/.github/workflows/generator_generic_slsa3.yml@v2.0.0
```

This is the SLSA-3 generator. Pinning by SHA would break
the verifiable-build chain — the in-toto attestation
predicate is signed by an OIDC issuer that verifies the
reusable workflow's *tag* against its trust policy, not
its SHA. SLSA-3 spec explicitly carves this out at
[`slsa.dev/spec/v1.0/requirements#build-isolated`](https://slsa.dev/spec/v1.0/requirements#build-isolated).

The GitHub Actions runtime enforces tag-pinning on
reusable workflows that ship signed attestations; the
`@v2.0.0` tag is functionally equivalent to a SHA pin
from the supply-chain-trust perspective.

The drift workflow's allow-list explicitly skips any
`uses:` whose action prefix is
`slsa-framework/slsa-github-generator/` — covering the
current ref plus any future SLSA-shaped reusable workflow.

### 4.2 Local workflow references

Workflows that reference their own repo via
`uses: ./.github/workflows/X.yml` carry no third-party
trust surface (the workflow file ships in the same commit
as its caller). The allow-list skips refs starting with
`./` for this reason.

## 5. False-positive handling

The drift detector is deliberately strict — any ref that
is not a 40-char hex SHA + outside the allow-list is
flagged. If a new legitimate semver-tag carve-out
appears (e.g. a new reusable workflow with the same
trust shape as SLSA-3), the procedure is:

1. **Land the carve-out in the workflow's allow-list
   block.** Edit the `# Allow-list N — <description>`
   block in `slsa-drift-detection.yml` to add the prefix.
2. **Document the carve-out here** under §4 — every
   allow-list entry MUST have a documented rationale.
3. **Re-run drift detection** to confirm the new entry
   suppresses the false positive.

## 6. Failure semantics

| Trigger | Behaviour on drift | Operator action |
| ------- | ------------------ | --------------- |
| `schedule` (weekly cron) | Fails run + creates/updates `slsa-drift` issue | Land pin rewrite within 7 days |
| `workflow_dispatch` | Fails run + uploads artefact (no issue update) | Used by Apone for pre-PR validation |

The 7-day grace mirrors the lane-discipline-nightly's
24-hour grace — supply-chain drift is a slower-moving
class of regression than lane-discipline drift, so the
weekly cadence is appropriate.

## 7. W22 → W23 hand-off

* The drift-detection workflow joins the existing
  sustaining-surface workflows:
  * `lane-discipline-nightly.yml` — lane-bundling drift.
  * `bundle-health.yml` — frontend-bundle-size drift.
  * `hsts-readiness-check.yml` — HSTS preload drift.
* W23+ MAY add additional allow-list entries when new
  reusable workflows with documented tag-pin trust
  shapes land in the repo. Every new entry MUST have a
  §4 rationale block.
* Failing the drift run for >7 days WITHOUT a fix in
  flight should escalate to the SLSA-3 supply-chain
  triage bug in the squad-decisions inbox.

## 8. Cross-references

* [`.github/workflows/slsa-drift-detection.yml`](../.github/workflows/slsa-drift-detection.yml)
  — the workflow itself.
* [`docs/slsa-pinning-w20-sweep.md`](./slsa-pinning-w20-sweep.md)
  — the W20 sweep ledger (canonical SHA source).
* [`docs/slsa-provenance.md`](./slsa-provenance.md)
  — repo-wide SLSA-3 stance.
* [`.github/workflows/lane-discipline-nightly.yml`](../.github/workflows/lane-discipline-nightly.yml)
  — the cadence-parallel sustaining surface.
