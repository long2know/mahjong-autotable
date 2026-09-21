# September 9, 2026 — authored inbox source archive (historical CI diagnosis)

Runtime-owned preservation by Scribe. Original August 12 texts follow their source labels. These observations retain their original PR/run/target provenance; they are not fresh September 9 environment or gate clearance. No workflow revision, timeout increase, retry, signature bypass or author re-admission is authorized by this archival merge. Current candidate limitations are in `sessions/2026-09-09-apone-candidate.md`; exact-candidate/full-provider gates remain pending.

## Source: decisions/inbox/bishop-changsha-e2e-ci-environment.md

# Decision — Changsha manual-deal E2E reds are CI-environment, not a HEAD regression

**Author:** Bishop (Backend / persistence release-gate)
**Date:** 2026-08-12
**Re:** Hudson's four `e2e` reds on PR run 31576171218 / job 94048669279 (head 3c2a624).
**Reviewer requested:** Ripley (lead) or Frost (parallel backend). CC Apone (workflow lane), Hudson/Hicks (test/frontend residual risk).

## TL;DR
Fresh-head reproduction (HEAD 672f6aa, product-identical to 3c2a624) shows **all four
CI-red cells PASS** in a healthy local env. The reds are **CI-environment/timing
sensitivity** on the cancelled 60-min single-worker `e2e` job — NOT a product regression.
**No backend/runtime/test edit made.** Backend is byte-clean at HEAD.

## Evidence
- Canonical Docker build blocked by the known aspnet-base apt/GPG layer → used the
  established local UAT build/run path (documented). Served build proven == HEAD
  (`/health` buildSha 672f6aa; served bundle sha == fresh `npm run build`).
- #2/#3/#4 (mobile): PASS on fresh head (do not reproduce).
- #1 (chromium `manual-deal-ceremony:89`): PASS 18/20 sequential-healthy; the only
  fails were 2 early contended runs + 1 under deliberate 6× parallel saturation.
- Temp diagnostic in `AutotableWsEndpoint.TryHandleDiscardActionAsync` (added→reverted)
  proved 23/23 discard frames ACCEPTED, 0 rejected; a parallel-fail game shows a bot
  chow-claim of seat-0's discard → the discard registered then was claimed/snapshot-lagged
  before the client read → `totalDiscards` transiently 0. Backend discard path is correct.
- Committed HEAD already carries both referenced fixes (bot-dealer hand-1 roll scheduling
  in `StartGameAsync`; `pickup.targetSlots` in `ChangshaToAutotableTranslator`).

## Ownership / next steps
- **Apone** owns any `e2e-playwright.yml` correction (worker parallelism / budget /
  splitting the ceremony tests out of the saturated smoke job). I did NOT touch it (per task).
- **Hudson/Hicks**: `manual-deal-ceremony.spec.ts:303` is load-fragile (bot-claim /
  snapshot-lag race). Harden WITHOUT weakening the assertion, adding sleeps, or retries.
- No relation to the approved Postgres-timeout commit 672f6aa; that stands unchanged.

Raw evidence (gitignored): `.squad/log/bishop-evidence/E2E-SUMMARY.md`,
`.../e2e-job-94048669279.log`, `.../uat-run/server-8092*.log`; run logs at
`/data/source/bishop-e2e-artifacts/*.out`.

## Source: decisions/inbox/bishop-db-providers-postgres-timeout.md

# Decision — db-providers Postgres cell: raise per-cell timeout 30 → 45

**Author:** Bishop (Backend / persistence release-gate)
**Date:** 2026-08-12
**Scope:** `.github/workflows/db-providers.yml` (Apone's DevOps lane — flagged for awareness)
**Reviewer requested:** Ripley (lead) or Frost (parallel backend) — per release-gate hand-off.

## Context

PR #161 head `3c2a6247f109de2b453e53059aebfea3c34c25fc`. Run `31576171279`
job `94048669445` `Test (Postgres)` reported **cancelled** at ~30m13. Other
cells green: Sqlite 8m50, SqlServer 17m20, drift-gate PASS.

## Finding — NOT a code/test defect, hang, or deadlock

The Postgres suite **completed successfully inside the cancelled step**:

```
08:27:39.965  Passed! - Failed: 0, Passed: 5867, Skipped: 2, Total: 5869, Duration: 28m45s
08:27:40.014  ##[error]The operation was canceled.   (0.05s later)
```

Job started 07:57:32Z; `timeout-minutes: 30` → deadline 08:27:32Z. The suite
needed ~30m07 of job wall-clock; the cap fired ~8s before the step could exit
cleanly / flush the TRX. Classification: **job-budget (wall-clock) exhaustion**.

Corroboration:
- **Continuous progress, no hang:** 1075 per-class schema-reset+migrate probes
  (`__EFMigrationsHistory does not exist`) spread evenly every minute 08:00→08:27
  (25–82/min, no gaps).
- **I/O saturation smoking gun:** Postgres checkpoint `total=274.993s;
  sync files=36436` at 08:27:19 — a 4.5-min fsync stall on the runner disk.
- **Variance trend (identical 5867/2 count), Postgres-cell job wall-clock:**
  13m53 → 14m10 → 14m34 → 15m09 (same branch, 10 min earlier, PASS) → 24m19
  (same branch) → 30m07 (capped). ~2.2x spread ⇒ runner I/O variance, not test
  growth. The same branch passed Postgres in 15m09 ten minutes before the cancel.

Root cause of the slowness is architectural and intended: the W23 isolation
harness drops+recreates `public` and re-runs full migrations once per
DB-touching class (`MAT_TEST_RESET_DB=1`), and `xunit.runner.json` serializes
all collections — fsync-bound and disk-I/O-sensitive by design.

## Local reproduction (exact CI command, fresh isolated PG16)

`Persistence__Provider=Postgres dotnet test -c Release --no-build` against a
throwaway PG16 container → **exit 0**, TRX counters `total=5869 passed=5867
failed=0 error=0 timeout=0 aborted=0`, 16m27s (tmpfs = best-case floor).
Focused `Auth` namespace: 94/94 passed.

## Fix (surgical, single functional line)

`.github/workflows/db-providers.yml` job `matrix-test`:
`timeout-minutes: 30 → 45` (+ inline evidence comment).

- 45 = 1.5× worst-observed completion (~30m) — headroom for an I/O-starved
  runner while still killing a genuine hang/deadlock.
- **No test skipped/disabled;** full suite retained per provider (design intent
  per workflow header + W23 note). No narrowing (architecture intends full
  matrix). Sqlite/SqlServer finish well inside 45 and exit early.

## Not done (deliberate)
- Did **not** push / commit (awaiting independent review).
- Did **not** rerun the GitHub job: a rerun pre-merge would use the old 30-min
  workflow and prove nothing about the fix; historical variance already
  documented. Recommend a fresh `db-providers` run after the workflow change
  merges.

Raw evidence preserved (gitignored): `.squad/log/bishop-evidence/`.
