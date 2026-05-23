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

### 4. Per-commit author identity verification

After each commit, verify the author is YOU:

```bash
git log -1 --format='%an <%ae>'
# MUST print your agent identity, e.g.:
# Vasquez (QA) <vasquez@squad.mahjong>
```

If the identity is wrong, immediately `git commit --amend --reset-author`
and re-verify.

### 5. Push only your branch

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
| Vasquez | `src/backend/tests/**`, `src/frontend/autotable-src/tests/**`, `docs/test-*.md`, `docs/contracts/`, `.squad/agents/vasquez/`, `.squad/decisions/inbox/vasquez-*`, `docs/agent-handoff-protocol.md` (this file), `docs/test-shims.md` | `src/backend/src/`, `src/frontend/autotable-src/src/`, `infra/`, `.github/workflows/` |

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
