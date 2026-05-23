# Lane-Discipline CI Gate (`tests/ci/`)

> **Owner:** Vasquez (QA).
> **Status:** Active. PR-blocking on `main` branch protection from Phase K Wave 7.

## TL;DR

Every PR into `main` MUST be single-lane. The CI gate (`lane-discipline`
workflow) fails the PR if any commit touches files outside the PR
author's lane.

```bash
tests/ci/check-cross-lane-bundling.sh --pr HEAD --base origin/main --strict
```

## Lane map

The per-agent regex map is documented at
[`tests/ci/lane-map.json`](../tests/ci/lane-map.json). The bash
script (`tests/ci/check-cross-lane-bundling.sh`) uses an equivalent
case-statement classifier for portability (no `jq` dep).

| Agent | Path prefixes |
|-------|---------------|
| Bishop  | `src/backend/src/`, `src/backend/Migrations/`, `Phase_K_W*/Bishop/`, `appsettings*.json`, `.squad/agents/bishop/`, `.squad/decisions/inbox/bishop-` |
| Hicks   | `src/frontend/`, `Phase_K_W*/Hicks/`, `.squad/agents/hicks/`, `.squad/decisions/inbox/hicks-` |
| Apone   | `.github/workflows/`, `infra/`, `helm/`, `Dockerfile`, `CHANGELOG.md`, certain `docs/*.md` (infra-flavoured), `.pre-commit-config.yaml`, `.squad/agents/apone/`, `.squad/decisions/inbox/apone-` |
| Vasquez | `src/backend/tests/`, `src/frontend/autotable-src/tests/`, `tests/ci/`, `docs/test-*.md`, `docs/contracts/`, `.github/workflows/lane-discipline.yml`, `.squad/agents/vasquez/`, `.squad/decisions/inbox/vasquez-` |

### `Phase_K_W*/<AgentName>/` attribution rule

Files under `Phase_K_W*/<AgentName>/` (anywhere in the tree) are
attributed to `<AgentName>`, NOT Vasquez. This lets each agent
author contract-style tests under their own subdirectory inside
the broader Vasquez test lane.

**Examples:**

- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/RS256HappyPathTests.cs` → **bishop**
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Hicks/BundlerSwapContractTests.cs` → **hicks** ← W7 refinement: lets Hicks own cross-pane test code Vasquez forward-stages.
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Apone/AponeW7InfraContractTests.cs` → **apone**

The W6 brief originally pinned this for `src/backend/tests/*/Phase_K_W*/<AgentName>/`;
W7 broadens to ANY depth — `Phase_K_W7/Hicks/*.cs` at the repo root
ALSO routes to Hicks's lane.

## Strict mode

`--strict` adds three guarantees on top of the PR-mode classifier:

1. `MODE` is forced to `pr` (enforces the per-commit lane check).
2. `tests/ci/lane-map.json` MUST exist and contain the `"lanes"` key.
3. Any violation hard-fails (no historical-warning escape).

CI invokes `STRICT=1 tests/ci/check-cross-lane-bundling.sh --pr "$PR_REF" --base "$BASE_REF" --strict`.

## How to add a new agent

1. Add an entry to `tests/ci/lane-map.json` `lanes.{agent}` with an
   anchored regex covering the agent's path prefixes.
2. Add a matching `case` arm to `agent_for_path()` in
   `tests/ci/check-cross-lane-bundling.sh`.
3. Add an `<agent>@squad.mahjong` author entry to
   `agent_for_author()` in the same script.
4. Confirm the gate passes locally:
   `STRICT=1 ./tests/ci/check-cross-lane-bundling.sh --pr HEAD --base origin/main --strict`

## How to debug a lane-discipline failure

If your PR fails with `✗ CROSS-LANE BUNDLE`:

1. Identify the offending commit SHA from the failure output.
2. Run `git show --name-only $SHA` to see which files were touched.
3. Split the commit by lane: stage only your owned paths into one
   commit, then a second commit (or PR) carries the other lane.
4. Per-invocation identity:
   ```bash
   git -c user.name="Vasquez (QA)" -c user.email="vasquez@squad.mahjong" \
       commit -m "your-commit-msg"
   ```
   so the author identity matches your lane.

If your PR fails with `✗ AUTHOR-LANE MISMATCH`:

1. Verify the commit was made with the per-invocation identity
   command above. The `.git/config` user.email is NOT respected
   under the race-prone concurrent-agent topology — use the
   per-invocation flag every time.

## History

- **W5** — Cross-lane bundling regression first observed
  (Apone's `b346157`). Documented in
  `docs/agent-handoff-protocol.md`.
- **W6** — First version of `tests/ci/check-cross-lane-bundling.sh`
  + `.github/workflows/lane-discipline.yml`. PR-non-blocking.
- **W7** — Promoted to PR-blocking via `--strict`. Added
  `tests/ci/lane-map.json` (machine-readable lane map).
  Generalised the `Phase_K_W*/<AgentName>/` attribution rule
  to any-depth.
