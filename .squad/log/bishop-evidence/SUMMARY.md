# db-providers Postgres cell — cancellation RCA (Bishop)

PR #161 head 3c2a6247f109de2b453e53059aebfea3c34c25fc
Failed run 31576171279, job 94048669445 (Test (Postgres)) — conclusion=CANCELLED.

## Termination classification: JOB-BUDGET (30-min wall) EXHAUSTION. Not a failure/hang/deadlock/regression.
- CI log line 381: `Passed! - Failed: 0, Passed: 5867, Skipped: 2, Total: 5869, Duration: 28m45s` @ 08:27:39.965
- CI log line 382: `##[error]The operation was canceled.` @ 08:27:40.014 (0.05s later)
- Job started 07:57:32Z; timeout-minutes:30 → deadline 08:27:32Z. Suite finished ~30m07 into job wall; cap tripped ~8s before clean step exit.
- Steady progress: 1075 per-class schema-reset+migrate probes spread evenly every minute 08:00→08:27 (25–82/min, NO gaps) → continuous progress, no hang.
- I/O saturation smoking gun: Postgres checkpoint `write=269.692s ... total=274.993s; sync files=36436` at 08:27:19.

## Trend (identical 5867/2 count, Postgres cell job wall-clock):
  13m53 (c7eea9d1) / 14m10 (2bdc5ef1) / 14m34 (beee9405) / 15m09 (5fb69072, SAME branch, 10 min earlier, PASS) / 24m19 (349dbd67, SAME branch) / 30m07 (3c2a6247, capped).
  ~2.2x spread for the same tests → runner I/O variance, not test growth.

## Local reproduction (exact CI command, fresh isolated PG16 @ :55432):
  `Persistence__Provider=Postgres dotnet test -c Release --no-build` → EXIT 0
  TRX counters: total=5869 passed=5867 failed=0 error=0 timeout=0 aborted=0; Duration 16m27s (tmpfs floor).
  Focused Auth namespace: 94/94 passed (1m). (task's "71/71" is a subset.)

## Fix: .github/workflows/db-providers.yml job `timeout-minutes: 30 → 45`
  45 = 1.5x worst-observed completion (~30m). Preserves hang/deadlock kill behavior; no test skipped/disabled;
  full suite per provider retained (design intent per workflow header + xunit.runner.json W23 note).
