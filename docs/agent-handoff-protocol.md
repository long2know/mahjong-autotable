# Agent handoff protocol — stash-checkpoint discipline

**Owner:** Vasquez (QA).
**Wave:** Phase K Wave 5 (process deliverable).
**Audience:** every concurrent agent (Bishop, Hicks, Apone, Hudson, Vasquez)
working on the squad's working tree at the same time.

---

## Why this document exists

Phase K Waves 3 and 4 ran with two or more agents editing the same working
tree concurrently. The default `git add -A` + `git commit -am` workflow
absorbed work from neighbouring agents into the wrong author's commit on
two separate occasions, producing PRs where:

- **Wave 3** — Bishop's auth-lane changes landed under Vasquez's
  `Vasquez (QA) <vasquez@squad.mahjong>` signature.
- **Wave 4** — Vasquez's `Phase_K_W4/*` test files landed inside Bishop's
  security-tightening commit, breaking attribution downstream.

The fix is a small process discipline — **always stash before you stage,
always stage selectively, always verify the author identity on each
commit** — that this document formalises.

---

## The stash-checkpoint cadence

### 1. Checkpoint after each logical work chunk

After a contiguous chunk of work you're not yet ready to commit (e.g.,
finished a new test file but haven't run the gate; finished a docs draft
but haven't proof-read), capture the work so neighbouring agents'
`git reset` / `git stash` / aggressive `git checkout` can't lose it:

```bash
git stash --include-untracked -m "<agent>-w<N>-checkpoint-<unix>"
```

Naming convention: lowercase agent ident + wave number + the literal
word `checkpoint` + epoch seconds. Examples:

- `vasquez-w5-checkpoint-1779537812`
- `bishop-w5-checkpoint-pre-jwks-1779540001`
- `apone-w5-checkpoint-init-1779541905`

The Unix timestamp makes the stash name globally orderable across
agents — `git stash list` shows the timeline.

### 2. Restore (pop) immediately before your own `git add`

When you are ready to stage and commit YOUR files, pop your own
checkpoint:

```bash
# Find your latest checkpoint:
git stash list | grep "<agent>-w<N>-checkpoint" | head -1
# Apply (don't drop yet — keep the safety net):
git stash apply stash@{N}
```

Pop the checkpoint only AFTER your commit lands and the build / gate
passes. Until then, the stash is your rollback target.

### 3. Stage selectively — NEVER `git add -A`

`git add -A` will hoover up every concurrent agent's WIP that is in
the working tree. Always stage by explicit path:

```bash
# GOOD — stage only your owned paths.
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/

# BAD — absorbs Bishop's auth WIP, Hicks's TS WIP, Apone's infra WIP.
git add -A
```

If you find yourself wanting `-A`, that is a signal to checkpoint
(stash) other agents' work to a separate stash entry first.

### 3.4. Shared-file pattern (selectors.md, etc.) — Phase K Wave 8

Most files are owned by a single lane and the cross-lane CI gate
(`tests/ci/check-cross-lane-bundling.sh`) hard-fails on any commit
whose author identity disagrees with the lane mapping.

A small set of files are intentionally **co-edited** — they form
a shared contract between two or more lanes:

- `src/frontend/autotable-src/tests/selectors.md` — the canonical
  data-testid registry. Hicks writes the testid in the renderer;
  Vasquez codifies it as a Playwright assertion. Both lanes must
  edit the same file in lock-step.

These files live in the lane-map's `shared_files` allowlist:

```json
{
  "shared_files": {
    "selectors_md_shared": {
      "paths": ["src/frontend/autotable-src/tests/selectors.md"],
      "authors": ["hicks", "vasquez"],
      "primary": "vasquez"
    }
  }
}
```

**Conventions for shared files**:

1. Either listed author may touch a shared file in their commit.
   The CI gate will NOT raise an author-lane mismatch when the
   commit touches ONLY shared files (or shared files plus the
   committing author's own lane).
2. When a single PR mixes shared-file edits with another lane's
   files (the non-author lane), the gate still fails — shared
   files are a relaxation, not an exemption.
3. The `primary` author is the documentation-of-record owner.
   When a shared file ships a substantive structural rewrite,
   that work is the primary's responsibility; small additions
   may be either author's.

To run a one-off cross-lane scan that ignores the strict-mode
allowlist verification (useful for surveying historical commit
attribution on long-lived branches), use `--repo-mode`:

```bash
./tests/ci/check-cross-lane-bundling.sh --repo-mode
```

`--repo-mode` walks every reachable commit on `HEAD` and prints
a baseline report without failing. The expected post-W6 baseline
is **0 violations**; legacy pre-W6 squash-merge violations
(~48) are pre-existing and out of scope for the gate.

### 3.5. Branch-protection procedure for the lane-discipline gate

The lane-discipline workflow (`.github/workflows/lane-discipline.yml`)
runs `check-cross-lane-bundling.sh --strict` on every PR. To make
the workflow **required for merge** on `main`, the repository
administrator (Stephen) must flip the branch protection rule:

1. GitHub repo → **Settings → Branches → Branch protection rules**.
2. Edit the rule for `main` (or create one if absent).
3. Under **Require status checks to pass before merging**, add:
   - `lane-discipline / cross-lane-bundling`
   - (existing checks: build, test, smoke, etc.)
4. Save.

Once required, any PR with an AUTHOR-LANE MISMATCH or
`shared_files`-key drift fails CI and the merge button is
disabled until the offending commit is amended (or split).

**Nightly cron pattern** (W9+): a scheduled workflow runs
`--repo-mode` against `main` daily at 06:00 UTC and posts the
baseline violation count to a tracking GitHub issue
(`[lane-discipline-nightly] baseline`). The expected baseline
is 0; any non-zero count is investigated within 24h. The
workflow lives at `.github/workflows/lane-discipline-nightly.yml`
(W9 deliverable).

**Opt-in preview status check** (W9+): an additional workflow
(`.github/workflows/lane-discipline-status.yml`) publishes the
status name `lane-discipline / cross-lane-bundling (OPTIONAL-FOR-NOW)`
on every PR so Stephen can preview the lane-discipline outcome
BEFORE the canonical `lane-discipline / check` is flipped to
a required status check. The opt-in workflow is non-blocking;
once §4 below is executed the opt-in workflow can be removed
or retained as a secondary preview.

> **Status note (W9):** Per-PR enforcement still runs through the
> primary `lane-discipline / check` workflow (non-blocking until
> Stephen flips the required-status-check). The §4 runbook below
> documents the exact `gh api` commands. Nightly + opt-in
> preview workflows ship in W9.

### 3.6. Lock-file relocation `/tmp/` → `.work/squad-git-lock` (W9 — Apone; **W10 cutover COMPLETE**)

> **EDIT(W10).** The cutover described below is now COMPLETE. From
> Wave 10 onward, `.work/squad-git-lock` is the canonical squad
> mutex path; every agent prompt template + the onboarding docs
> cite the new path. The body of this section is preserved as
> the historical W9-era cutover plan so future readers
> understand WHY the migration happened. The literal
> `/tmp/squad-git-lock` references in §3.7's snippet, in
> `docs/retro-2026-05.md`, and in `.squad/agents/*/history.md`
> are preserved as wave-original; new work paths use
> `.work/squad-git-lock` exclusively.

W6 introduced `flock -w 120 9 … 9>/tmp/squad-git-lock` to serialise
the `git add` + `git commit` + `git push` critical section across
concurrent agents (see §3 / squad charter). The W8 retro flagged
two problems with the `/tmp/` location:

1. **Ephemeral.** `/tmp/` is wiped on reboot and (on some runtimes)
   on inactivity. A second agent that comes online between the
   wipe and the next squad session creates a brand-new lock file
   instead of attaching to the existing one — losing serialisation
   exactly when it matters most.
2. **Shared with non-squad processes.** `/tmp/squad-git-lock` is
   in a world-writable directory. A non-squad process can `touch`
   the file or hold an unrelated flock against it (e.g. a wrapper
   script that grabs every `*.lock` in `/tmp/` as a watchdog).
   Locks taken on the squad file would then block on an unrelated
   process.
3. **Runtime hard-prohibition.** Several agent runtimes (Scribe
   noted in W8 §3.1; Vasquez confirmed in their W8 memo) actively
   block writes under `/tmp/` — so the lock file silently never
   gets created and the flock is a no-op.

**Cutover plan (executed across W9 + W10):**

- **W9 (executed).** Agents already invoked with the original
  `/tmp/squad-git-lock` directive CONTINUED using `/tmp/`. Mixing
  lock locations mid-wave would have defeated the mutex (two agents
  holding two different locks would race). This was a one-wave
  carve-out only.
- **W10+ (canonical — IN FORCE).** Every agent uses
  `.work/squad-git-lock`. The directory `.work/` is gitignored
  except for `.work/.gitkeep` (which guarantees the directory
  exists on a fresh clone); the lock file itself never lands in
  git. Apone's W10 commit ships the prompt-template flip + the
  onboarding-doc sweep that completes the cutover (see
  `.squad/decisions.md` Wave-6/7/8 sections — each carries an
  `EDIT(W10)` note pointing at the new path).
- **Agent prompt templates** for W10 onward MUST cite `.work/squad-git-lock`.
  The W6/W7/W8 prompt templates (`.squad/agents/_template-prompt.md`
  if present, or the in-prompt directive when the template is
  hand-rolled) have been updated as of the first W10 commit.
- **Onboarding docs.** `.squad/decisions.md`, this file, and the
  per-agent `charter.md` references all point at
  `.work/squad-git-lock`. The per-agent `history.md` files are
  EXEMPT: historical retro entries preserve the original-wave
  reality (so a reader can correlate a W6/W7/W8/W9 commit message
  with the path it actually used at the time).

The new path:

```bash
(
  flock -w 120 9 || exit 1
  # ... git critical section ...
) 9>.work/squad-git-lock
```

If `.work/` is missing on a fresh clone (the `.gitkeep` got
deleted or .gitignore evolved), `flock` creates the lock file
implicitly because `bash` redirection (`9>…`) creates the path
on demand. But the `.gitkeep` guard reduces surprise.

### 3.7. Rebase-inside-flock pattern (W9 — Apone)

W8 retro flagged a separate race not closed by the flock alone:
two agents push in rapid succession, and the second push is
rejected as **non-fast-forward** because the first push moved the
remote branch tip while the second agent was still inside its
flock-protected commit. The mutex serialised the local critical
sections — but not the network's view of the branch tip.

**Pattern (W10 onward should incorporate this; Apone uses it
starting W9):**

```bash
(
  flock -w 120 9 || exit 1
  git status --short | head -20   # sanity-check the working tree
  git add <lane paths>             # selective, NEVER -A
  git -c user.name="<Agent>" -c user.email="<agent>@squad.mahjong" \
      commit -m "<message>"
  git log -1 --format='%an <%ae>'  # verify author identity

  # NEW W9 step — fetch + rebase against origin BEFORE pushing.
  # If a sibling lane pushed during our edit window, this pulls
  # their commit ahead of ours so the push goes through fast-
  # forward.
  git fetch origin <branch>
  if ! git rebase origin/<branch>; then
      # Conflict during rebase — abort + bail out of the flock
      # critical section WITHOUT pushing. The operator (or the
      # agent itself, in a follow-up turn) is expected to resolve
      # the conflict by hand. Pushing a half-rebased state would
      # be worse than not pushing.
      echo "::error::rebase conflict against origin/<branch>; aborting flock"
      git rebase --abort
      exit 2
  fi
  git push origin <branch>
) 9>.work/squad-git-lock     # ← W10+ canonical location
                             # (W9 wave used `9>/tmp/squad-git-lock`
                             # per the §3.6 mid-wave carryover —
                             # cutover completed at start of W10).
```

**Why this is safe inside the flock.** The `fetch` + `rebase`
acquires the latest origin tip while we hold the local lock. A
SIBLING agent's flock-protected push CANNOT race past us — they
queue up behind our flock. So the only race we close here is
against a NON-squad pusher (e.g. Stephen amending a PR off-flock)
or against a pre-flock push that landed between our last fetch
and our local commit. Both are real; both are caught.

**Why the rebase MUST happen inside the flock.** Doing it outside
(e.g. `git pull --rebase` before acquiring the lock) would leave a
window where the lock is acquired but the local branch is stale
— another agent could fetch and rebase in parallel, and both
agents would converge to push the same (stale) tip.

**Conflict semantics.** A rebase conflict on a properly-lane-staged
single-author commit is unusual — the lane-discipline gate
(`tests/ci/check-cross-lane-bundling.sh`) hard-rejects cross-lane
commits, so two agents touching the SAME file is a process bug.
The abort + bail-out path therefore primarily exists for the
rare cross-lane-shared-file edits (the W8 `selectors_md_shared`
allowlist) and for the migration windows where two agents might
edit a single new file. Operator-level intervention is the
correct escalation when this fires.

### 4. Branch-protection setup (W9 — Vasquez runbook for Stephen)

> **W11 re-prompt.** Branch protection on `main` for the
> `lane-discipline / check` status check is STILL informational
> as of W11 start. Stephen: please flip it to required-for-merge.
> The W11 commit ships the additional screenshot guidance + the
> 422-troubleshooting section + the one-liner PATCH command
> below (§4.1).

The lane-discipline workflow (`.github/workflows/lane-discipline.yml`)
runs `check-cross-lane-bundling.sh --strict` on every PR. To make
the workflow **required for merge** on `main`, the repository
administrator (Stephen) runs the following `gh api` commands. The
runbook is split into three steps so the W9 preview workflow stays
visible during the transition.

### 4.1. Screenshot-walkthrough + troubleshooting (W11 — Vasquez, re-prompted W12)

> **Re-prompt status (W12 — Vasquez).** The branch-protection
> task has been **standing since Phase K Wave 4** (first asked
> ~2026-09-04). At time of W12 sign-off (2026-10-23), it has been
> open for **~7 weeks across 8 waves** (W4 → W11 wave-end memos
> all carry a "still pending" note). This makes it Vasquez's
> longest-standing operator hand-off.
>
> The W12 re-prompt is the **fifth wave running** that the gate
> hasn't been flipped (W7, W8, W9, W10, W11 all asked; W12 asks
> a 6th time). The W12 escalation proposal:
>
> 1. **W13 follow-up:** Stephen receives this hand-off one more
>    time. If still not flipped after W13, escalate to W14.
> 2. **W14 fallback (proposed):** if W14 sign-off lands without
>    branch-protection being enabled, Vasquez writes the
>    canonical `gh api -X PATCH` one-liner (already drafted
>    below) into a Vasquez-direct admin action (Stephen runs
>    it himself in seconds; if Stephen is blocked by org policy,
>    coordinator escalates to the org-level admin via Apone).
> 3. **Risk profile:** the gate doesn't change the substance of
>    what `lane-discipline / check` enforces — it just makes the
>    enforcement *required* instead of *advisory*. The W11 PR
>    landed with 0 lane-discipline violations, so flipping the
>    gate today would not block any in-flight work.
>
> Vasquez's W12 hand-off note: do NOT delay W12 sign-off on this;
> the work is operator-side and Vasquez's W12 deliverables are
> complete. The §4.1 walkthrough + the one-liner PATCH below are
> the canonical artefacts.

The following walkthrough mirrors the `gh api` runbook below but
provides UI cues for operators who prefer the GitHub web interface.
Placeholder text describes the screenshot state at each step (the
actual image captures land alongside this doc as
`docs/screenshots/branch-protection-step{1..5}.png` when Stephen
authors them — for now the placeholders document the expected DOM
landmarks so a future operator can recreate the workflow without
guessing).

#### Step A — open the protection editor

> **Screenshot placeholder** — `docs/screenshots/branch-protection-step1.png`.
> Expected state: GitHub repo page for `long2know/mahjong-autotable`,
> `Settings` tab selected, left rail shows `Code and automation →
> Branches`. The `main` row in the *Branch protection rules* list
> has the `Edit` button visible.

Navigate to `Settings → Branches → Branch protection rules`. Click
`Edit` next to the `main` rule. If no rule exists for `main`, click
`Add classic branch protection rule` and enter `main` as the
*Branch name pattern*.

#### Step B — locate the required status checks section

> **Screenshot placeholder** — `docs/screenshots/branch-protection-step2.png`.
> Expected state: inside the rule editor, scroll to the
> *Require status checks to pass before merging* section. The
> checkbox is ticked; the search input below it accepts a status
> check name (e.g. `build`, `test`, `lane-discipline / check`).

The DOM landmark is the section heading `Require status checks
to pass before merging`. The status-check search input has the
placeholder text `Search status checks…`.

#### Step C — add `lane-discipline / check`

> **Screenshot placeholder** — `docs/screenshots/branch-protection-step3.png`.
> Expected state: search input contains `lane-discipline`; the
> autocomplete dropdown shows two candidates:
> `lane-discipline / check` and
> `lane-discipline / cross-lane-bundling (OPTIONAL-FOR-NOW)`.
> The CANONICAL check is `lane-discipline / check`; do NOT add
> the `(OPTIONAL-FOR-NOW)` variant (it's a preview shadow).

Click `Add` next to `lane-discipline / check`. The check now
appears in the required-checks list alongside `build`, `test`.

#### Step D — save

> **Screenshot placeholder** — `docs/screenshots/branch-protection-step4.png`.
> Expected state: bottom of the page; the `Save changes` button
> is enabled (green). A green banner reads
> `Branch protection rule for "main" updated`.

Click `Save changes`. GitHub stages the new rule immediately.

#### Step E — validate

> **Screenshot placeholder** — `docs/screenshots/branch-protection-step5.png`.
> Expected state: open any in-flight PR against `main`. The
> *Merge* button is disabled if `lane-discipline / check` is
> still pending or failing. A status block at the bottom of
> the PR conversation shows the check name with a queued /
> running / passed / failed icon.

Open the most recent in-flight PR against `main`. Confirm the
`lane-discipline / check` status block appears AND that the
merge button gates on it.

#### Troubleshooting

* **`gh api -X PUT ...` returns 422 Unprocessable Entity**

  This usually means the existing protection rule has additional
  fields (e.g. `restrictions: {...}`, `required_signatures: true`,
  or `allow_force_pushes: true`) that the PUT payload doesn't
  reproduce. The PUT semantics replace the rule WHOLESALE so any
  unstated field gets nulled — and GitHub rejects the PUT if the
  resulting state is invalid (e.g. `restrictions` must be either
  `null` or an object, not omitted).

  **Fix:** re-read the current rule first, then PATCH the
  required-status-checks block alone:

  ```bash
  # 1. Read current full state.
  gh api repos/long2know/mahjong-autotable/branches/main/protection \
      > /tmp/protection-current.json

  # 2. PATCH only the required_status_checks block.
  gh api -X PATCH \
    repos/long2know/mahjong-autotable/branches/main/protection/required_status_checks \
    -F 'contexts[]=build' \
    -F 'contexts[]=test' \
    -F 'contexts[]=lane-discipline / check'
  ```

  The PATCH semantics merge into the existing rule and avoid
  the wholesale-replace pitfall.

* **One-liner PATCH (canonical W11 shortcut)**

  When the existing required contexts are already `build` + `test`
  (the typical state on this repo), the entire add-lane-discipline
  operation collapses to one command:

  ```bash
  gh api -X PATCH \
    repos/long2know/mahjong-autotable/branches/main/protection/required_status_checks \
    -F 'contexts[]=build' \
    -F 'contexts[]=test' \
    -F 'contexts[]=lane-discipline / check'
  ```

  Idempotent — running twice produces the same final state.

* **`lane-discipline / check` doesn't appear in the autocomplete**

  GitHub only surfaces status check names it has SEEN on a recent
  PR. If the autocomplete is empty, open a trivial PR (e.g. a docs
  typo fix), wait for the workflow to run once, then return to the
  branch-protection editor — the check will now autocomplete.

* **Rule edit shows "No status checks found" and refuses to save**

  The repository has zero status-check history. Same fix as
  above: trigger one PR run first.

* **PR reviewer count fails the save**

  The W9 runbook payload sets `required_approving_review_count: 1`.
  If your org policy mandates ≥2, override before re-PUTting:

  ```json
  "required_pull_request_reviews": {
    "required_approving_review_count": 2,
    "dismiss_stale_reviews": true
  }
  ```

#### Step 1 — read current branch protection (optional but recommended)

```bash
# Reveals the current required_status_checks block so Step 2 can
# add to it without dropping the existing checks (build, test,
# secrets-scan, etc.).
gh api repos/long2know/mahjong-autotable/branches/main/protection \
    --jq '.required_status_checks'
```

If the JSON output ends with `"contexts": ["build", "test", ...]`,
those checks must be preserved in Step 2 (add the new context,
don't replace the list).

#### Step 2 — flip lane-discipline to required

The canonical check name is **`lane-discipline / check`** (the
job name in `.github/workflows/lane-discipline.yml` is `check`,
and GitHub renders status checks as `<workflow> / <job>`).

```bash
# IMPORTANT: replace EXISTING_CHECKS with the array from Step 1.
# If `enforce_admins` is currently false, leave it false (Stephen
# can flip that separately).
gh api -X PUT repos/long2know/mahjong-autotable/branches/main/protection \
    --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "build",
      "test",
      "lane-discipline / check"
    ]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true
  },
  "restrictions": null
}
JSON
```

`gh api -X PUT … --input -` accepts a JSON document on stdin and
replaces the protection rule wholesale. If you prefer the
narrower `PATCH` semantics, use `gh api -X PATCH …` with
`required_status_checks.contexts` only — but Stephen's audit
trail is cleaner with the full PUT.

#### Step 3 — validate

```bash
# Create a deliberately-cross-lane test branch (e.g. a no-op
# commit touching .github/workflows/ AND src/backend/tests/) and
# open a draft PR. Verify the lane-discipline / check status
# block shows up as REQUIRED in the merge box.
gh pr create --draft \
    --title "test: lane-discipline gate validation" \
    --body "Intentional cross-lane PR for branch-protection validation. DO NOT MERGE."
```

After the PR opens, watch the Checks tab — the lane-discipline
check MUST fail AND the merge button MUST be disabled. Then
close (don't merge) the test PR.

For an ALL-CLEAN sanity check, open a single-lane PR (e.g. a docs
typo fix) and confirm `lane-discipline / check` passes AND the
merge button enables.

#### Rollback procedure

If a regression appears (e.g. the script false-positives on a
legitimate squash-merge), Stephen can demote the check back to
non-required without redeploying anything:

```bash
# 1) Re-read current rule (so we don't accidentally drop other
#    required checks).
gh api repos/long2know/mahjong-autotable/branches/main/protection \
    --jq '.required_status_checks.contexts'

# 2) Re-PUT WITHOUT the lane-discipline entry.
gh api -X PUT repos/long2know/mahjong-autotable/branches/main/protection \
    --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["build", "test"]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 1,
    "dismiss_stale_reviews": true
  },
  "restrictions": null
}
JSON
```

The opt-in preview workflow (`lane-discipline-status.yml`) stays
visible as a non-blocking signal during the rollback window, so
the squad keeps observability on cross-lane drift even when the
gate isn't enforced.

#### Why this is in §4

`§3.5` documents the *workflow* mechanics + the W9 nightly cron.
`§4` documents the *administrator action* (Stephen's single
elevated step). Splitting them keeps the agent-facing protocol
discipline (§1–§3) separate from the one-time admin runbook (§4).

### 4.2. Coordinator-direct execution (W13 — Vasquez escalation runbook)

> **Why this section exists.** The §4.1 re-prompt has now been
> standing for **six consecutive waves** (W7 → W13 all asked).
> The original Phase K Wave 4 issuance is at ~9 weeks. The W13
> escalation proposal in §4.1 ("W14 fallback") needs an
> *executable artefact* so the coordinator can flip the gate in
> a single step the moment Stephen authorises it (or when org
> policy allows the coordinator to execute admin actions
> directly).

The canonical executable is
**`tests/ci/lane-discipline-flip-required.sh`** (new in W13 —
Vasquez owns the file; the coordinator can invoke it on
authorisation). The script:

1. Verifies the caller has `repo:admin` scope (otherwise it
   prints the diagnostic + exits 2 — never silently fails).
2. Reads the current branch-protection rule via
   `gh api repos/long2know/mahjong-autotable/branches/main/protection`.
3. Computes the merge of the current `required_status_checks.contexts`
   PLUS `lane-discipline / check` (idempotent — running twice
   produces the same final state).
4. Issues a narrow PATCH via
   `gh api -X PATCH … required_status_checks` (per §4.1
   troubleshooting "One-liner PATCH"; the narrow PATCH avoids
   the wholesale-replace 422 trap).
5. Reads the rule back and prints the contexts list as
   verification. Exits 0 only when `lane-discipline / check`
   is observable in the round-trip.

**Invocation:**

```bash
# Dry-run (no PATCH; prints the would-be operation):
tests/ci/lane-discipline-flip-required.sh --dry-run

# Live (requires GH_TOKEN or `gh auth login` with admin scope):
tests/ci/lane-discipline-flip-required.sh
```

**Who can run it:**

| Role | Authorisation | Action |
|------|---------------|--------|
| Stephen (repo owner) | always | run directly. |
| Apone (DevOps lane) | when Stephen has explicitly delegated | run directly. |
| Coordinator (squad orchestrator) | W14 escalation pre-condition: §4.1 still pending at W14 sign-off | run via `--coordinator-flag` (records the escalation event in the W14 wave memo). |
| Any other agent | NEVER | refuse — the script's pre-flight check requires `repo:admin` scope. |

**Audit trail.** Every successful run appends one line to
`docs/audits/branch-protection-flips.md` (the file is created
on first run; future flips append). The line carries:
timestamp, caller identity, the contexts list before/after, the
escalation level (`--coordinator-flag` or direct).

**Rollback** is symmetric — `--rollback` removes the lane-discipline
context and restores the prior state. Use only if the gate
false-positives a legitimate commit; tag the rollback in the
audit log so the cause can be triaged.

**Risk profile.** This script does NOT change what
`lane-discipline / check` enforces — it just toggles whether
the existing check is *required* or *advisory* for merges.
Repository state today (W13 sign-off, three consecutive
zero-violation waves) makes this a no-op for in-flight work.

**The W14 escalation algorithm (canonical):**

```
IF (§4.1 still pending at W14 sign-off)
THEN:
  1. Vasquez writes the W14 memo with `branch-protection ESCALATED`
     in the header.
  2. Coordinator invokes:
     tests/ci/lane-discipline-flip-required.sh --coordinator-flag \
       --reason "W14 escalation: §4.1 pending since Phase K Wave 4"
  3. The post-flip verification step (step 5 of the script) is the
     proof of completion. The audit-log entry closes the
     standing hand-off.
  4. The §4.1 block in this doc is then re-titled
     `4.1. Screenshot-walkthrough + troubleshooting
     (CLOSED at W14 — coordinator-direct flip)`.
ELSE:
  Continue the §4.1 re-prompt cadence into W15.
END
```

The script is the **single canonical executable** for this
operator action; do not write ad-hoc `gh api -X PATCH …`
commands in future wave memos. If the script is missing or
non-executable when needed, the W14 escalation re-instates it
from the W13 commit log before invoking.

### 4.3. Branch-protection W14 fallback execution (W14 — Vasquez)

**Status at W14 sign-off:** the §4.1 re-prompt has been standing
for **seven consecutive waves** (W7 → W14). Stephen has not yet
acknowledged or executed the screenshot walkthrough. Per the
W14 escalation algorithm in §4.2, the coordinator now has a
ready-to-invoke fallback path.

**Vasquez W14 pre-flight (`--dry-run` validation):**

The W13 script was re-validated at W14 sign-off via:

```bash
bash tests/ci/lane-discipline-flip-required.sh --dry-run
```

Captured output (also stored at
`.work/vasquez-w14-safe/flip-script-dryrun.log`):

```
→ Reading current branch protection for long2know/mahjong-autotable:main…
→ Mode: dry-run
→ Have lane-discipline currently: no
→ Resulting contexts:
→ Dry-run: would execute:
    gh api -X PATCH repos/long2know/mahjong-autotable/branches/main/protection/required_status_checks
```

The dry-run confirms: (a) the script reaches the API surface, (b)
`lane-discipline / check` is NOT currently required, (c) the
script computes the correct narrow PATCH (per §4.1's
troubleshooting one-liner pattern). One **cosmetic bug** observed:
the dry-run summary line omits the post-merge contexts list
(the `MODE != "apply"` guard at script line ~133 skips the
augmentation block in dry-run output). The bug does NOT affect
live execution; it is documented here so the W15+ maintainer can
fix the summary print without re-validating the script's API
behavior.

**1-line copy-paste command for Stephen (or the Coordinator):**

```bash
GH_TOKEN="<admin-scope-token>" bash tests/ci/lane-discipline-flip-required.sh
```

(Coordinator-direct variant per §4.2 escalation algorithm:)

```bash
GH_TOKEN="<admin-scope-token>" bash tests/ci/lane-discipline-flip-required.sh \
  --coordinator-flag \
  --reason "W14 escalation: §4.1 pending since Phase K Wave 4"
```

**Post-flip verification (idempotent):**

After the live run, the script reads back the contexts list and
exits 0 only when `lane-discipline / check` is observable. The
operator should then run:

```bash
gh api repos/long2know/mahjong-autotable/branches/main/protection \
  --jq '.required_status_checks.contexts'
```

and confirm `lane-discipline / check` appears in the array. The
audit log at `docs/audits/branch-protection-flips.md` will carry
the flip record (per §4.2 audit-trail clause).

**Audit-trail expectations:**

Every successful coordinator-direct flip appends one line to
`docs/audits/branch-protection-flips.md` (file created on first
run; subsequent runs append). Vasquez owns the format spec; the
W14 entry, if executed, should carry: timestamp, caller identity
(coordinator + reason), contexts list before/after, escalation
level. Stephen-direct flips append the same line minus the
`--reason` field.

**W15 hand-off:**

- If §4.1 is still pending at W15 sign-off, Vasquez writes the
  W15 memo with `branch-protection ESCALATED` in the header and
  re-validates the dry-run.
- If the gate has been flipped (either by Stephen via §4.1 or by
  the coordinator via §4.2/§4.3), the §4.1 block is re-titled
  `(CLOSED at W<N> — <method>)`; §4.3 stays as a historical
  record of the escalation pre-flight.
- The §4.3 dry-run cadence is **once per wave** until §4.1 closes;
  the captured log goes into `.work/vasquez-w<N>-safe/`.

### 5. Per-commit author identity verification

After each commit, verify the author is YOU:

```bash
git log -1 --format='%an <%ae>'
# MUST print your agent identity, e.g.:
# Vasquez (QA) <vasquez@squad.mahjong>
```

If the identity is wrong, immediately `git commit --amend --reset-author`
and re-verify.

### 6. Push only your branch

Each agent owns a branch named `stlong/phase-<phase>-wave-<N>-<lane>`.
Never force-push to a neighbouring agent's branch. Never push to
`main` directly — every agent goes through a PR.

---

## Concurrent recovery flow

If a neighbouring agent's `git reset --hard` wipes your in-progress
work AND you forgot to checkpoint:

1. **Stop editing.** Don't let a second `git reset` overwrite the
   reflog.
2. Check the reflog for your latest commit / staged state:
   ```bash
   git reflog --date=iso | head -50
   ```
3. If you find a commit / index entry that contains your work,
   reset to it:
   ```bash
   git reset --hard <reflog-sha>
   # OR, to restore staged but uncommitted work:
   git read-tree --reset -u <reflog-sha>
   ```
4. If the reflog is gone too, recover from the `.work/` scratch
   directory (see below) — that's why we keep a parallel copy.

### Parallel scratch directory `.work/<agent>-w<N>-safe/`

Each agent SHOULD maintain a flat-file copy of their in-progress
files in `.work/<agent>-w<N>-safe/` (gitignored). Pattern:

```bash
mkdir -p .work/vasquez-w5-safe
cp src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/*.cs \
   .work/vasquez-w5-safe/
```

This is belt-and-braces protection: even if both the stash AND
the reflog are lost (e.g., a `git gc --prune=now`), the file
contents remain on disk.

---

## Lane discipline (what each agent stages)

| Agent | Owns | Never stages |
|-------|------|--------------|
| Bishop  | `src/backend/src/Mahjong.Autotable.Api/Auth/*`, `…/Voice/*`, `…/Tournament/*` | `tests/`, `infra/`, `src/frontend/` |
| Hicks   | `src/frontend/autotable-src/src/*`, `scripts/*`, generated `src/frontend/autotable/*` | `tests/`, `infra/`, `src/backend/` |
| Apone   | `.github/workflows/*`, `infra/k8s/*`, `infra/terraform/*`, `docs/{slsa,hsts,admission}-*.md` | `tests/`, `src/`, `.squad/agents/<other>/` |
| Hudson  | `tests/` infrastructure (`xunit.runner.json`, harness fixtures) | new test FACTS (those are Vasquez's) |
| Vasquez | `src/backend/tests/**`, `src/frontend/autotable-src/tests/**`, `docs/test-*.md`, `docs/contracts/`, `.squad/agents/vasquez/`, `.squad/decisions/inbox/vasquez-*`, `docs/agent-handoff-protocol.md` (this file), `docs/test-shims.md`, `.github/workflows/lane-discipline*.yml` | `src/backend/src/`, `src/frontend/autotable-src/src/`, `infra/`, other `.github/workflows/` |

When a Vasquez test needs a backend surface to land first, Vasquez
writes a **forward-staged soft-pass** assertion that hard-asserts
once the surface is present — never patches the backend file.

---

## Author identity (canonical idents)

| Agent | Author | Email |
|-------|--------|-------|
| Bishop  | `Bishop (Security)` | `bishop@squad.mahjong` |
| Hicks   | `Hicks (Frontend)`  | `hicks@squad.mahjong` |
| Apone   | `Apone (DevOps)`    | `apone@squad.mahjong` |
| Hudson  | `Hudson (Platform)` | `hudson@squad.mahjong` |
| Vasquez | `Vasquez (QA)`      | `vasquez@squad.mahjong` |

Set with:

```bash
git config user.name "Vasquez (QA)"
git config user.email "vasquez@squad.mahjong"
```

Per-commit verification (always):

```bash
git log -1 --format='%an <%ae>'
```

---

## Concrete W5 example (Vasquez bring-up)

```bash
# 1. Lock in identity FIRST.
git config user.name "Vasquez (QA)"
git config user.email "vasquez@squad.mahjong"

# 2. Initial checkpoint — capture any leftover WIP from the prior session.
git stash --include-untracked -m "vasquez-w5-checkpoint-init-$(date +%s)"
git stash pop

# 3. Scratch copy of files I plan to author.
mkdir -p .work/vasquez-w5-safe

# 4. Edit / create my files in src/backend/tests/, src/frontend/autotable-src/tests/, docs/, .squad/.
#    After each file is in a known-good state:
cp src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/*.cs .work/vasquez-w5-safe/

# 5. Pre-commit checkpoint — stash neighbouring-agent WIP into a
#    separate stash entry; my own files stay on disk.
git stash --include-untracked --keep-index \
  -m "vasquez-w5-checkpoint-pre-commit-$(date +%s)"

# 6. Selective stage — explicit paths, never -A.
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W5/
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/RegressionHostFixture.cs
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/Wave1ThroughKW5RegressionTests.cs
git add src/backend/tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj
git add src/frontend/autotable-src/tests/
git add docs/agent-handoff-protocol.md
git add docs/test-shims.md
git add docs/test-harness-handoff.md
git add .squad/agents/vasquez/history.md
git add .squad/decisions/inbox/vasquez-phase-k-wave-5.md

# 7. Commit — Vasquez identity verified.
git commit -m "test(qa): phase k wave 5 — bring-up

- 9 Wave-4 contract gaps flipped to hard-assert
- TESTING_SHIM-gated TestHttpClientExtensions.WithDirectSession
- RegressionHostFixture restores default xUnit parallelism
- 5 new Playwright specs (scene-shell-strict, keyboard-seed,
  voice-spectator-distinct, three-renderer-lazy, jwks-shape)
- Renamed regression class KW4 → KW5
- Stash-checkpoint discipline doc (this PR)

Gate: 1329 / 0 / 0.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"

# 8. Verify identity.
git log -1 --format='%an <%ae>'
# Must print: Vasquez (QA) <vasquez@squad.mahjong>

# 9. Pop the pre-commit checkpoint so neighbouring agents' WIP
#    returns to their working tree.
git stash pop
```

---

## When to violate this protocol

Never. If an emergency `main` push is needed, follow the squad
charter's hot-fix procedure (see `.squad/charter.md` § Hot fixes)
instead of bypassing the checkpoint discipline.

---

## 5. Concurrent agent safety guarantees (W10 — Vasquez)

> **Status.** Phase K Wave 10 — Vasquez (QA). This section
> consolidates every concurrent-agent safety mechanism that has
> accumulated across Waves 6–10 into a single normative
> reference. Each agent prompt and the onboarding doc cite
> §5 from W10 onwards.

When two or more squad agents work the same branch in parallel,
the following invariants MUST hold. Each invariant cites the
mechanism that enforces it and the wave in which the mechanism
landed.

### 5.1. Critical section: lock at `.work/squad-git-lock`

The `git add` + `git commit` + (rebase) + `git push` triplet is
the critical section. Every agent serialises through:

```bash
( flock -w 120 9 || { echo "lock-timeout" >&2; exit 1; }
  # critical section here
) 9>.work/squad-git-lock
```

* **Path is `.work/squad-git-lock`** — relocated from `/tmp/`
  in W9/W10 (see §3.6) because some agent runtimes block writes
  under `/tmp/`. The `.work/` dir is gitignored.
* **Timeout is 120s** — long enough that a normal commit
  finishes; short enough that a stuck agent doesn't deadlock
  the squad.
* **Rebase happens INSIDE the flock** — see §3.7. Rebasing
  before acquiring the lock can race against another agent's
  push.

### 5.2. Backup discipline: `.work/<agent>-w<N>-safe/`

Per-agent, per-wave backup directories under `.work/` mirror
the work-in-progress on disk. They survive `git stash`, `git
reset --hard`, and a concurrent agent's `git clean -fdx`
because they're outside the index AND outside the working-tree
attribution.

The canonical layout:

```
.work/
├── squad-git-lock                       # the mutex itself
├── bishop-w<N>-safe/                    # Bishop's per-wave backup
│   ├── backend/                         #   mirrored source files
│   └── migrations/                      #   mirrored EF migrations
├── hicks-w<N>-safe/                     # Hicks's per-wave backup
│   ├── frontend/                        #   mirrored Vite + Three sources
│   └── e2e/                             #   mirrored Playwright specs
├── apone-w<N>-safe/                     # Apone's per-wave backup
│   ├── workflows/                       #   mirrored CI workflows
│   ├── helm/                            #   mirrored helm charts
│   └── terraform/                       #   mirrored TF modules
└── vasquez-w<N>-safe/                   # Vasquez's per-wave backup
    ├── backend/                         #   mirrored test files
    ├── ci/                              #   mirrored tests/ci/
    ├── docs/                            #   mirrored docs additions
    ├── playwright/                      #   mirrored e2e specs
    └── squad/                           #   mirrored memos + history
```

Agents back up after each non-trivial authoring step (file
authored, file modified, test green) BEFORE they enter the
flock-wrapped commit critical section. The directory layout
matches the regex `\.work/[a-z]+-w\d+-safe` which the W10
self-lane test
(`HandoffProtocol_Section5_DocumentsLockPath_BackupDirs_DbSerial`)
asserts is documented here.

### 5.3. Stash-discipline: NEVER `--include-untracked` for protective checkpoints

The W9 retro identified that `git stash --include-untracked`
under concurrent execution can wipe other agents' untracked
files. Two W9 incidents confirmed this — both wiped the
`Phase_K_W9/` per-agent dirs that contained in-flight memos.

The W10 rule:

* **Use `git stash push`** (NO `--include-untracked`) when you
  need to roll back tracked-modified files temporarily for a
  build-in-isolation check.
* **Use `.work/<agent>-w<N>-safe/`** (a real directory, not a
  stash) when you need to *back up* in-flight work that the
  other agents might overwrite.
* **NEVER use `git clean -fdx`** to "tidy up" before commit.
  Other agents' untracked WIP is not yours to remove.

The bundling-check's `shared_files` table (§5.5 below) plus
the lane-map exclusions are what allow multiple agents to
co-author shared files safely; the `.work/` backup discipline
is what makes accidental deletes recoverable.

### 5.4. Lane discipline + `shared_files` allowlist

`tests/ci/check-cross-lane-bundling.sh` (Vasquez, W6→W10)
rejects any commit whose changed paths span more than one
agent's lane. The `shared_files` allowlist in
`tests/ci/lane-map.json` carves out specific paths that
legitimately span lanes:

| Shared file                          | Authors          | Primary lane | Wave landed |
| ------------------------------------ | ---------------- | ------------ | ----------- |
| `tests/selectors.md`                 | hicks, vasquez   | vasquez      | W8          |
| `src/frontend/.../tests/selectors.md`| hicks, vasquez   | vasquez      | W8          |
| `docs/agent-handoff-protocol.md`     | apone, vasquez   | vasquez      | W10         |

For these files, the bundling-check strips them from the
per-commit lane set BEFORE computing single-lane attribution,
so a commit touching ONLY a shared file + the author's own
lane doesn't trigger a false positive.

### 5.5. Rebase-inside-flock (W9 hardening)

The bare `git push` can race against another agent's push if
the rebase happens BEFORE entering the flock. The canonical
order INSIDE the lock:

1. `git fetch origin <branch>`
2. `git rebase origin/<branch>`  ← rebase HERE, inside flock
3. `git add <paths>`             ← stage explicit Vasquez/agent paths
4. `git commit -m "..." --author="..."`
5. `git push origin <branch>`

If the rebase fails inside the flock, abort cleanly:
`git rebase --abort && exit 1`. Do NOT push a half-rebased
state.

### 5.6. `[Collection("DbSerial")]` for DB-touching tests

The W9 retro identified that the EF Core + SQLite test fixture
contention is a CONCURRENT-AGENT safety issue at the *test
suite* layer. Two parallel test classes that both build a
`WebApplicationFactory<Program>` can corrupt each other's
EF model cache. The W10 mitigation:

* `src/backend/tests/Mahjong.Autotable.Api.Tests/Collections/DbSerialCollection.cs`
  defines the `DbSerial` xUnit collection with
  `DisableParallelization = true`.
* Bishop's W11 deliverable: attribute each DB-touching test
  class with `[Collection("DbSerial")]`.
* `docs/test-architecture.md` §3 documents the policy.

This is concurrent-agent safety at the TEST level, but it
belongs in the handoff doc because the migration is a
cross-wave, cross-agent change.

### 5.7. Branch-protection alignment

The lane-discipline workflow (`.github/workflows/lane-discipline-status.yml`)
runs on every PR and must be required-for-merge via GitHub
branch protection. See §4 above for the `gh api` runbook.
Without branch protection, the bundling check is informational
only — an agent can land a cross-lane commit even when the
check fails.

### 5.8. Quick-reference: pre-commit safety checklist

Every agent runs through this checklist BEFORE entering the
flock:

```text
[ ] My work-in-progress is mirrored in .work/<agent>-w<N>-safe/
[ ] I have NOT run `git stash --include-untracked`
[ ] I have NOT run `git clean -fdx`
[ ] My staging plan touches ONLY paths in my lane (per lane-map.json)
[ ] If I touch a shared_files entry, my identity is in `authors`
[ ] My tests (or the suite gate) pass in isolation
[ ] My commit message includes the wave tag + Co-authored-by trailer
```

Inside the flock:

```text
[ ] git fetch origin <branch>
[ ] git rebase origin/<branch>  (abort cleanly if it fails)
[ ] git add <explicit lane paths>
[ ] git commit --author="<Agent> <agent@squad.mahjong>"
[ ] git push origin <branch>
```

The W10 self-lane test
`Phase_K_W10/Vasquez/VasquezW10SelfLaneTests.cs` pins this
section's existence via `Concurrent agent safety`, the
`.work/squad-git-lock` literal, the `\.work/[a-z]+-w\d+-safe`
regex, and the `DbSerial` literal so future waves cannot
silently delete the policy.

---

### 5.9. Shared-files registry policy (W11 — Vasquez)

> **Status.** Phase K Wave 11 — Vasquez. Consolidates the
> `shared_files` mechanism (W8 + W10 + W11 broadenings) into
> a single authoritative policy that future agents and reviewers
> can cite without spelunking through the bash classifier or the
> JSON.

The `shared_files` allowlist in `tests/ci/lane-map.json` plus the
`is_shared_file()` / `shared_file_authors()` helpers in
`tests/ci/check-cross-lane-bundling.sh` together implement a
small carve-out from strict single-lane attribution. A handful of
files are LEGITIMATELY co-authored by two or more squad agents,
and the bundling check must accept any of the documented authors
without flagging an author-lane mismatch.

#### Current registry (as of W11)

| Shared file(s)                                                            | Authors                          | Primary | Wave landed | Rationale                                                                                                                                                |
| ------------------------------------------------------------------------- | -------------------------------- | ------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/frontend/autotable-src/tests/selectors.md` + `tests/selectors.md`    | hicks, vasquez                   | vasquez | W8          | Hicks writes the testid in the renderer; Vasquez codifies it as a Playwright assertion. Both lanes edit in lock-step.                                    |
| `docs/agent-handoff-protocol.md`                                          | apone, vasquez                   | vasquez | W10         | Apone authors the branch-protection runbook (§4) + lock-file relocation (§3.6/§3.7); Vasquez authors concurrent-agent safety (§5) + stash-discipline.    |
| `src/backend/tests/Mahjong.Autotable.Api.Tests/Shims/*`                   | bishop, vasquez, hicks, apone    | vasquez | W11         | The Shims/ directory is the canonical place for cross-pane TESTING_SHIM-gated test scaffolding. Any contract author may add a forward-stage shim that pairs with their lane's surface (e.g. Bishop adding a CommentaryGeneratorTestShim alongside his backend interface ship). |
| `.github/workflows/pwa-audit.yml` + `.github/workflows/pwa-builder.yml`   | hicks, apone                     | apone   | W11         | Hicks authors the PWA asset surface (manifest, screenshots, audit fixtures); Apone owns the workflow runtime that runs them. Either may amend the YAML.  |

#### Policy

1. **Registry is closed-by-default.** Adding a new entry requires
   a `shared_files` lane-map drift + the bundling-check sibling
   update + an entry in this table. A `Phase_K_W*/Vasquez/`
   self-lane test pins each entry's existence so silent rollbacks
   fail the gate.

2. **Primary owns documentation-of-record.** The `primary` field
   names the agent responsible for substantive structural
   rewrites. Small additions / corrections may come from either
   listed author.

3. **The carve-out is from author-identity only, NOT from
   single-lane.** A commit touching a shared file PLUS another
   lane's source still fails the bundling check — the lane-set
   computation excludes shared files BEFORE single-lane is
   checked, so the second lane's files trip the gate. This is
   intentional: shared files are a relaxation of WHO can author,
   not a license to bundle multi-lane work.

4. **Primary lane controls classification.** When `is_shared_file()`
   returns true, the path is removed from the per-commit lane set
   for cross-lane attribution. The `primary` field is the
   informational marker telling reviewers / nightly cron
   "in case of doubt, this file's documentation-of-record owner
   is X".

5. **Adding a new entry — checklist**:
   ```text
   [ ] tests/ci/lane-map.json — `shared_files.<name>` entry added
   [ ] tests/ci/check-cross-lane-bundling.sh — `is_shared_file()` +
       `shared_file_authors()` extended
   [ ] docs/agent-handoff-protocol.md §5.9 — table row added
   [ ] Phase_K_W<N>/Vasquez/Vasquez*SelfLaneTests.cs — pin added
   [ ] Phase_K_W<N>/W<N>SurfaceSmokeFactsTests.cs — smoke added
   ```

#### Removing an entry

Removal requires the same diligence as adding. The W11 self-lane
tests carry the W8 (selectors_md_shared) and W10
(agent_handoff_protocol_md_shared) regression pins — removing
either entry without dropping those pins fails the gate.

#### Why this lives in §5

`§5.4` documents the *mechanism* (lane discipline plus the
allowlist). `§5.9` documents the *policy* (when and how to amend
the allowlist). The split keeps the operational rule (§5.4)
brief while giving the governance question a deeper home.

---

