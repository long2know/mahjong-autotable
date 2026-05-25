# SKILL — CI noise management

**Owner:** Apone (DevOps).
**Applies to:** Any agent triaging a "CI failure email flood" report
from Stephen. The skill is a methodology + checklist, not a script.

## When to use

Stephen reports one of:

* "I'm getting too many CI failure emails."
* "CI is red on closed PRs / on main."
* "The build is breaking on every push."

OR a fresh `gh run list --limit 100` shows >10 failure conclusions
across 1–2 days.

## When NOT to use

* When ONE specific gate fails for a specific code-correctness reason
  (e.g. dotnet test fails because of a bug). Fix the bug, not the gate.
* When the failure is on a `main` push from a supply-chain workflow
  (container-scan, sbom, slsa-provenance, sign-image, secrets-scan,
  slsa-drift-detection, gitleaks). Those are real findings.
* When the failure blocks an in-flight playability PR — fix the gate
  or coordinate with the lane owner; don't disable underneath them.

## Methodology

### Step 1 — Inventory (5 min)

```bash
export GH_TOKEN=$(printf "protocol=https\nhost=github.com\n\n" \
  | git credential fill 2>/dev/null | awk -F= '/^password=/ {print $2}')

# Last 100 runs, failures only.
gh run list --limit 100 --json conclusion,event,workflowName,headBranch,createdAt \
  --jq '.[] | select(.conclusion=="failure") | "\(.event)\t\(.workflowName)\t\(.headBranch)\t\(.createdAt)"'

# Aggregate by workflow + trigger.
gh run list --limit 200 --json conclusion,event,workflowName --jq '.[] | select(.conclusion=="failure") | "\(.event) \(.workflowName)"' | sort | uniq -c | sort -rn
```

Group failures into FOUR buckets:

| Bucket                                | Signal                                                                                          | Action                                                       |
| ------------------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| (A) Real bug in production-path gate  | docker-build / pre-commit-check (on real content drift) / supply-chain                          | **FIX the bug.** Never disable. Page the relevant lane.      |
| (B) Wave / policy artifact            | lane-discipline / per-wave checks that no longer apply                                          | **Remove PR trigger.** Keep `workflow_dispatch:` for audits. |
| (C) Test infra bug beyond your scope  | db-providers / e2e flake / visual regression with intentional drift                             | **`continue-on-error: true`** + diagnostic memo + lane hand-off. |
| (D) Scheduled probe with no target    | hsts-readiness-check against placeholder URL / load-test against nonexistent stack / prod-health-check | **Comment out `schedule:`**, keep `workflow_dispatch:`.  |

### Step 2 — Per-failure diagnosis (10 min)

For each failing workflow, fetch the latest failed run log:

```bash
gh run list --workflow=<wf> --status=failure --limit 1 --json databaseId --jq '.[0].databaseId' | xargs -I{} gh run view {} --log-failed | tail -80
```

Look for:

* `error CS####` / `Test failed` — code bug (Bucket A).
* `permission denied` / `connection refused` / `host not found` —
  infrastructure (Bucket D, usually).
* `duplicate key value violates unique constraint` / `no such table` —
  test isolation (Bucket C).
* `lane-discipline` / `cross-lane bundle` / `wave-N policy` — Bucket B.

### Step 3 — Atomic suppression PR

ONE branch. ONE commit. Concise commit message that names every
workflow touched. Sample template at
`.squad/decisions/inbox/apone-ci-noise-iter2.md`.

### Step 4 — Validate

```bash
# Lint every workflow you touched (binary at .work/apone-w*-tools/actionlint).
.work/apone-w21-tools/actionlint .github/workflows/<file1> .github/workflows/<file2> ...

# Confirm yaml parses (no actionlint binary? fallback):
python3 -c "import yaml; [yaml.safe_load(open(f)) for f in ['<file1>', '<file2>']]"
```

After merge:

```bash
# Watch the next 5–10 cron windows. Expected scheduled-fail count: 0
# (modulo real supply-chain findings).
gh run list --limit 50 --json conclusion,event,workflowName,createdAt \
  --jq '.[] | select(.event=="schedule") | "\(.conclusion)\t\(.workflowName)\t\(.createdAt)"'
```

## Hard rules (Apone charter alignment)

1.  **Never disable supply-chain workflows** even if they're noisy:
    `container-scan`, `sign-image`, `sbom`, `slsa-provenance`,
    `secrets-scan` / `gitleaks`, `slsa-drift-detection`. A real
    leak / drift / unsigned image is a security incident, not noise.
2.  **Never delete a workflow file** without a comment explaining
    why. Comment out the trigger; keep `workflow_dispatch:`. The
    workflow body is still useful as an audit reference.
3.  **Always pair Bucket-C suppression with a diagnostic memo** to
    the relevant lane owner. The lane owner needs a repro + flag-
    flip instruction to re-enable. Filename pattern:
    `.squad/decisions/inbox/apone-<workflow>-stuck.md`.
4.  **Comment the suppression with re-enable instructions.** Two-line
    revert (uncomment the cron / remove `continue-on-error:` / etc.)
    is the bar. Future-Apone has to find it without grep wizardry.
5.  **Lock-step the PR with `flock`** on `.work/squad-git-lock` so
    parallel agents don't race the working tree.

## Anti-patterns

* ❌ Disabling pre-commit globally because one hook is firing on a
  content bug. Pre-commit is the floor — fix the content, not the
  config. (See iter1 PR #71 history.)
* ❌ Deleting the failing workflow without a `workflow_dispatch:`
  retention so on-demand auditing remains possible.
* ❌ Setting `continue-on-error: true` on EVERY step in a workflow
  instead of at the job level. Step-level is harder to revert.
* ❌ Adding a global `if: false` at workflow level — actionlint will
  complain about an empty trigger; better to comment out the
  `schedule:` block + keep `workflow_dispatch:`.
* ❌ Staging another agent's uncommitted changes when branching off
  `origin/main` — always use explicit `git add <path>`, NEVER
  `git add -A` / `git add .`.

## Recurring patterns to watch for

* **Wave-machine artifacts** that linger after Stephen pivots the
  team. Lane discipline, wave-N policy, cross-lane bundling, per-
  agent file-ownership gates. When the policy is killed, the gates
  must follow.
* **Placeholder URLs** in scheduled probes (`mahjong.example.com`,
  `api.mahjong-autotable.com` — both placeholders today). These
  ALWAYS fail and ALWAYS email Stephen.
* **Shared-state CI databases** (db-providers) that work for one
  test at a time but race on parallel xUnit collections. Test-infra
  bug, not migration drift.
* **Visual baselines** during producer-side churn. Visual regression
  is a producer-side acceptance test — it MUST be paired with a
  baseline-regeneration cadence or made non-blocking until the
  producer stabilizes.

## History — Apone iter1 (PRs #70/#71/#72/#74)

Iter1 fixed: pre-commit multi-doc YAML + binary excludes (PR #71),
prod-health-check schedule disabled (PR #70), heredoc YAML collisions
(PR #72), one-off workflow-dispatch shape regression (PR #74). The
suppression methodology was implicit. Iter2 (this skill) codifies it.

## Cross-references

* `.squad/agents/apone/charter.md` — DevOps lane boundaries.
* `.squad/decisions/inbox/apone-ci-noise-iter2.md` — the iter2 inbox
  memo (concrete worked example).
* `.squad/decisions/inbox/apone-db-providers-stuck.md` — hand-off
  template for a Bucket-C diagnostic.
