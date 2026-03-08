# Legacy `pr/*` Branch Retention Policy (Draft)

_Last reviewed: 2026-03-06 (PT)_

## Scope
Applies to legacy remote branches that are not active delivery branches and still carry unique commits ahead of `origin/main`.

Current candidates:
- `origin/pr/01-scaffold-solution` (`defa50f`)
- `origin/pr/02-domain-rules-core` (`ed6f10a`)
- `origin/pr/07-tests-docs` (`46ce3de`)

## Objective Retirement Criteria
A legacy branch can be retired only if **all** are true:
1. Branch has no open PR and no planned follow-up work.
2. Unique commit(s) are preserved by one of:
   - reachable from `main`, or
   - preserved by an annotated tag on the commit (`archive/<branch>/<yyyy-mm-dd>`), or
   - preserved by a patch artifact committed under `docs/archive/` (fallback).
3. A short rationale is recorded in task tracking (`TASKS.md` / `ISOLATED_WORK.md`) including branch name and commit SHA.
4. Stakeholder confirmation is captured for delete operations involving unique commits.

## Recommended Handling for Current Branches
Because each branch is `ahead 1` vs `origin/main`, treat them as unique-history branches and archive first.

### 1) `origin/pr/01-scaffold-solution` (`defa50f`)
- Content: initial scaffold and CI bootstrap; mostly superseded by current repository state.
- Recommendation: **tag-and-delete**.
- Tag: `archive/pr-01-scaffold-solution/2026-03-06`

### 2) `origin/pr/02-domain-rules-core` (`ed6f10a`)
- Content: early domain/rules implementation files; mostly superseded by later commits.
- Recommendation: **tag-and-delete**.
- Tag: `archive/pr-02-domain-rules-core/2026-03-06`

### 3) `origin/pr/07-tests-docs` (`46ce3de`)
- Content: `WallTests.cs` addition; likely partially superseded but still unique in ancestry.
- Recommendation: **tag-and-delete** after confirming no test-history retention requirement.
- Tag: `archive/pr-07-tests-docs/2026-03-06`

## Safe Execution Checklist
1. Fetch/prune and re-verify divergence:
   - `git fetch --all --prune`
   - `git rev-list --left-right --count origin/main...origin/pr/<id>`
2. Create annotated archive tags on unique tip commits.
3. Push tags to `origin`.
4. Delete remote legacy branches.
5. Log exact commands and rationale in task logs.

## Monitoring Helper
For recurring check-ins while waiting on stakeholder confirmation, use:

```bash
./scripts/branch_retention_status_snapshot.sh
```

This command runs the approval gate and prints up-to-date `behind/ahead` counts for each candidate legacy branch.

## Command Template
```bash
git fetch --all --prune

# tag unique tips
git tag -a archive/pr-01-scaffold-solution/2026-03-06 defa50f -m "Archive unique legacy branch tip"
git tag -a archive/pr-02-domain-rules-core/2026-03-06 ed6f10a -m "Archive unique legacy branch tip"
git tag -a archive/pr-07-tests-docs/2026-03-06 46ce3de -m "Archive unique legacy branch tip"

git push origin archive/pr-01-scaffold-solution/2026-03-06
git push origin archive/pr-02-domain-rules-core/2026-03-06
git push origin archive/pr-07-tests-docs/2026-03-06

# delete legacy remotes
git push origin --delete pr/01-scaffold-solution
git push origin --delete pr/02-domain-rules-core
git push origin --delete pr/07-tests-docs
```
