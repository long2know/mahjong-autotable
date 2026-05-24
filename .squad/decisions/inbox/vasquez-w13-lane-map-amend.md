# Vasquez — Phase K Wave 13 lane-map amendment

**Date:** 2026-10-30
**Branch:** `stlong/phase-k-wave-13-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`
**Scope:** targeted lane-map + matcher amendment (no other QA-lane
changes in this commit — full W13 QA bring-up follows in a separate
memo).

## Problem

Hicks's W13 frontend bring-up commit `7ccd2fea5e` legitimately
introduced two file kinds that were NOT in the W11 `shared_files`
registry, producing a `checked=4 violations=1` cross-lane bundling
failure on `stlong/phase-k-wave-13-bringup`:

1. `.github/workflows/bundle-health.yml` — new per-PR bundle-size
   sticky-comment CI workflow. Hicks-authored (W13 deliverable #5)
   but lives in Apone's `.github/workflows/` namespace.
2. `src/frontend/autotable-src/tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/*.png`
   — Playwright visual-regression baselines captured by Hicks via
   the W13 `scripts/capture-visual-baselines.js` side-channel (W13
   deliverable #3), inside Vasquez's `src/frontend/autotable-src/tests/`
   test-lane root.

Both are textbook cross-pane contracts: the author lane and the
file's primary lane do not match, but the cross-author co-ownership
is legitimate and documented.

## Decisions

1. **Lane-map: `bundle_health_workflow_shared` is a 2-author shared
   file**, primary `apone` (workflow runtime owner), co-author
   `hicks` (frontend bundle author). Direct parallel to W11's
   `pwa_audit_workflow_shared`. Single path:
   `^\.github/workflows/bundle-health\.yml$`.

2. **Lane-map: `visual_regression_baselines_shared` is a 2-author
   shared file**, primary `vasquez` (QA harness owner, the
   `src/frontend/autotable-src/tests/` test-lane root), co-author
   `hicks` (Playwright-runtime baseline capture via the W13 side-
   channel). Path:
   `^src/frontend/autotable-src/tests/e2e/__screenshots__/.*\.png$`
   — wildcarded so the spec-named subdirectory hierarchy and any
   future visual specs are covered without a registry edit.

3. **Bash matcher mirrors the JSON.** Both new patterns were added
   to `is_shared_file()` and `shared_file_authors()` in
   `tests/ci/check-cross-lane-bundling.sh` (the case-statement
   classifier is the runtime matcher; the JSON is the strict-mode
   invariant document). Mirroring is mandatory per the W11 §5.9
   shared-files registry policy.

4. **No primary-lane reshuffle.** Both new entries follow the W11
   precedent that `primary` tracks the file's filesystem location
   (workflows → apone; tests/ root → vasquez), NOT the author who
   most recently touched it. This keeps the cross-lane bundling
   detector deterministic: a single commit that mixes
   `bundle-health.yml` with a non-`.github/workflows/` file in a
   non-apone lane STILL fails the bundle check.

## Affected paths (Vasquez-staged in this commit)

```
tests/ci/lane-map.json
tests/ci/check-cross-lane-bundling.sh
.squad/agents/vasquez/history.md
.squad/decisions/inbox/vasquez-w13-lane-map-amend.md   (force-add)
```

## Verification

```
$ bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-wave-13-bringup --strict
[lane-discipline] checking 4 commit(s) in mode=pr

✓ 45dc823b41 — lane=bishop author=bishop
✓ efae89798b — lane=vasquez author=vasquez
✓ 6b1e71f8f1 — lane=apone author=apone
✓ 7ccd2fea5e — lane=hicks author=hicks

[lane-discipline] checked=4 violations=0
[lane-discipline] OK
```

JSON valid (`python3 -m json.tool`); bash syntax valid
(`bash -n`); backend gate untouched (last known 2789 / 0 / 0).

## Forward queue

Full W13 QA bring-up (DbSerial flake harness wiring, LH13 hard-pin
cadence-trigger evaluation, KW12→KW13 regression rename, W13
Playwright specs + contract tests) ships in the next Vasquez
commit on this branch.
