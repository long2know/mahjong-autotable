# Vasquez — Phase K Wave 15 lane-map amendment

**Date:** 2026-11-13
**Branch:** `stlong/phase-k-wave-15-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`
**Scope:** targeted lane-map + matcher amendment (no other QA-lane
changes in this commit — the W15 QA bring-up `0a316d7` already
landed; this commit retro-broadens `shared_files` so the W15
DevOps + Frontend bring-up commits stop triggering false-positive
cross-lane bundling violations).

## Problem

Two W15 commits triggered lane-discipline violations on
`stlong/phase-k-wave-15-bringup` even though the cross-pane work
was legitimate per each agent's W15 prompt:

1. **Apone `b88a5a4`** edited
   `.github/workflows/lane-discipline-nightly.yml` (Vasquez-lane
   via the regex `.github/workflows/lane-discipline(-[a-z]+)?\.yml`).
   Apone's W15 prompt explicitly tasked him with fixing a heredoc
   bug in that workflow — the file is in Apone's
   `.github/workflows/` namespace but is QA harness owned, and the
   case is structurally identical to the W10
   `agent_handoff_protocol_md_shared` precedent (docs-shaped infra
   doc with split authorship).

2. **Hicks `173bb41`** edited
   `src/frontend/autotable-src/tests/e2e/manifest-screenshots-visual.spec.ts`
   and `src/frontend/autotable-src/tests/e2e/playwright.config.ts`
   (both Vasquez-lane via `src/frontend/autotable-src/tests/`).
   Hicks's W15 prompt explicitly tasked him with the Playwright
   `snapshotPathTemplate` migration, which inherently touches both
   files. Structurally parallel to W13's
   `visual_regression_baselines_shared` (Hicks-authored Playwright
   asset inside Vasquez's test-lane root).

Pre-amend lane-discipline output:

```
✓ 0a316d7569 — lane=vasquez author=vasquez
✓ e2986d2333 — lane=bishop  author=bishop
✗ CROSS-LANE BUNDLE: b88a5a4a00 (lanes=[apone vasquez], author=apone)
✗ CROSS-LANE BUNDLE: 173bb418ff (lanes=[hicks vasquez], author=hicks)
[lane-discipline] checked=4 violations=2
```

## Decisions

1. **Lane-map: `lane_discipline_nightly_yml_shared` is a 2-author
   shared file**, primary `vasquez` (QA harness owner), co-author
   `apone` (workflow runtime owner; W15 heredoc fix). Primary stays
   at `vasquez` (NOT `apone`) — direct parallel to
   `agent_handoff_protocol_md_shared`'s precedent that the QA-harness
   intent of the file overrides the `.github/workflows/`
   filesystem-location heuristic. Single path:
   `^\.github/workflows/lane-discipline-nightly\.yml$`.

2. **Lane-map: `playwright_visual_regression_shared` is a 2-author
   shared file**, primary `vasquez` (test-lane root owner),
   co-author `hicks` (Playwright-runtime owner; W15
   snapshotPathTemplate migration). Two paths in one entry:
     * `^src/frontend/autotable-src/tests/e2e/playwright\.config\.ts$`
     * `^src/frontend/autotable-src/tests/e2e/manifest-screenshots-visual\.spec\.ts$`

   Single registry entry covers both because the W15
   snapshotPathTemplate migration is one logical change spanning
   the spec + the Playwright config (both must move together for
   the snapshot baselines to keep resolving).

3. **Bash matcher mirrors the JSON.** Both new patterns were added
   to `is_shared_file()` AND `shared_file_authors()` in
   `tests/ci/check-cross-lane-bundling.sh`. The case-statement
   classifier is the runtime matcher; the JSON is the strict-mode
   invariant document. Mirroring is mandatory per the W11 §5.9
   shared-files registry policy.

4. **No primary-lane reshuffle.** The W11/W13 precedent that
   `primary` tracks intent over filesystem location is upheld for
   the nightly workflow (primary=vasquez even though it lives under
   `.github/workflows/`) and for the Playwright pair (primary=vasquez
   because the files live under `src/frontend/autotable-src/tests/`).
   Cross-lane bundling detection stays deterministic: a single
   commit that mixes either of the new shared files with a
   non-Vasquez-lane source still fails the bundle check.

## Affected paths (Vasquez-staged in this commit)

```
tests/ci/lane-map.json
tests/ci/check-cross-lane-bundling.sh
.squad/agents/vasquez/history.md
.squad/decisions/inbox/vasquez-w15-lane-map-amend.md   (force-add)
```

## Verification

```
$ bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-wave-15-bringup --strict
[lane-discipline] checking 4 commit(s) in mode=pr

✓ 0a316d7569 — lane=vasquez author=vasquez
✓ e2986d2333 — lane=bishop  author=bishop
✓ b88a5a4a00 — lane=apone   author=apone
✓ 173bb418ff — lane=hicks   author=hicks

[lane-discipline] checked=4 violations=0
[lane-discipline] OK
```

JSON valid (`python3 -m json.tool`); bash syntax valid
(`bash -n`); backend gate untouched (last known 3312 / 0 / 0 from
the W15 Vasquez bring-up `0a316d7`).

## Forward queue

None — the W15 QA bring-up already landed in `0a316d7`. This commit
is a pure registry retro-broadening so the W15 PR-branch
lane-discipline check goes green and the 5th-consecutive-wave
0-violation invariant is restored.
