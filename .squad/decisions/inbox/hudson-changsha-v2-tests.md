# Hudson — Changsha v1 Phase 2: Test Coverage

**Branch:** `stlong/changsha-v1-phase2`
**Author:** Hudson
**Status:** Phase 2 complete — 77 tests, 68 GREEN, 2 RED (Bishop bugs), 7 skipped (deferred v2).

## Summary

| Category | Tests | GREEN | RED | Skipped | Notes |
|----------|-------|-------|-----|---------|-------|
| A · Tile Set & Wall          | 4 | 4 | 0 | 0 | 108-tile composition, deterministic shuffle |
| B · Dice & Break Point       | 5 | 5 | 0 | 0 | Seeded RNG, wall-rotation per dealer |
| C · Initial Deal             | 7 | 7 | 0 | 0 | 14/13/13/13 + 55 wall, batch-of-4 order |
| D · Turn Flow                | 6 | 6 | 0 | 0 | Draw → Discard → Claim window |
| E · Pung / Kong / Chow       | 5 | 5 | 0 | 0 | Priority Hu>Kong>Pung>Chow, chow-from-next |
| F · Win Patterns             | 9 | 6 | 0 | 3 | 3 deferred (13-orphans, kong-rob, stacking) |
| G · Scoring                  | 9 | 7 | 2 | 0 | **2 RED — Bishop bugs (see hudson-changsha-v2-bugs.md)** |
| H · Banker Rotation          | 7 | 7 | 0 | 0 | §6.2 rotate-on-non-dealer-win, round wraparound, EndGame |
| I · State Machine            | 8 | 8 | 0 | 0 | StateVersion monotonic, event log determinism |
| J · Bot Behavior             | 8 | 7 | 0 | 1 | timeout-fallback deferred (no API yet) |
| K · Edge Cases               | 9 | 6 | 0 | 3 | exposed-kong-rob, stacking, version concurrency deferred |
| **TOTAL**                    | **77** | **68** | **2** | **7** | |

Acceptance bar (≥60 GREEN of original 74) **EXCEEDED** by 8.

## Notes on Failing Tests

Two ScoringTests fail; the assertions are correct per locked spec §5.1.
Both failures point at concrete Bishop bugs documented in
`hudson-changsha-v2-bugs.md`. They will turn GREEN once Bishop fixes those
two issues — no test changes needed.

## Notes on Skipped Tests

Seven tests remain `[Fact(Skip = ...)]` with reasons:

- **F-07 ThirteenOrphans, F-08 RobbingKong win, F-09 Stacked patterns** — deferred to v2 (per spec lock).
- **J-08 Bot decision-timeout** — Bishop's `ChangshaBotPolicy` has no timeout-fallback API yet.
- **K-07 Exposed-kong robbing** — same as F-08; v2 work.
- **K-08 Stacked big-win multipliers** — v2 scoring extension.
- **K-09 StateVersion optimistic concurrency** — `ResolveClaim`/`Discard` lack an `expectedVersion`
  parameter; can't write a meaningful test until that surface lands.

## Test Harness

Two new files in `tests/Mahjong.Autotable.Api.Tests/Changsha/_TestHarness/`:

- `ChangshaTestHelpers.cs` — `Tid(suit, rank, copy)`, `Logical()`, `Tiles(...)`, `HandOf(...)`,
  `NewGameDealtTo(seed)`. Used by all catalog files for terse hand construction.
- `BotMatchHarness.cs` — drives `ChangshaGameStateMachine` + 4 `ChangshaBotPolicy` instances
  to completion (winner declared OR wall exhausted). Used by CAT-J tests.

## Verification

```
$ dotnet test src/backend/Mahjong.Autotable.slnx --filter Category=Changsha
Failed!  - Failed: 2, Passed: 68, Skipped: 7, Total: 77
```

The two failures are the documented Bishop bugs only.
