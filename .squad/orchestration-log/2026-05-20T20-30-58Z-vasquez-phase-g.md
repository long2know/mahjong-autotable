# Orchestration Log — Vasquez Phase G (2026-05-20T20-30-58Z)

**Agent:** Vasquez (Rules Engineer)
**Mode:** background
**Branch:** `stlong/phase-g-bot-scheduler-lobby`
**Commit:** efbbddc

## Task routed
Phase G tests — acceptance test suite for bot pickup scheduler + privacy-mask cleanup contracts.

## Why chosen
Vasquez owns the acceptance test suite. Phase G requires two new contract tests: (1) RunBotPickupAsync tick scheduler driving bot pickup during manual deal (verify timing, state transitions, cancellation), (2) FilterEntriesForViewer slot-parse fix (verify last-@ parsing, asymmetric hand-only rotation override, multi-@ handling, unparseable seats).

## Files authorized
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/BotPickupSchedulerAcceptanceTests.cs` (NEW)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/PrivacyMaskAcceptanceTests.cs` (NEW)

## Files produced
**Test suite (11 facts, 60 assertions):**
- `BotPickupSchedulerAcceptanceTests.cs` (6 facts, 31 assertions): Verify pickup scheduler phases, bot tick delay, cancellation on game teardown, auto-deal bypass.
- `PrivacyMaskAcceptanceTests.cs` (5 facts, 29 assertions): Verify slot-parse at last `@`, face-strip universal, rotation override for hand.* only, multi-@ handling, unparseable seats pass-through, spectator masking.

**Reflection-backed testing:**
- Both test files use reflection probes to reach private methods (ChangshaGameRuntime._games, AutotableConnectionManager.FilterEntriesForViewer) for hermetic verification.

## Verification
- `dotnet build ... --nologo` → 0/0.
- `dotnet test ... --filter "FullyQualifiedName~BotPickupScheduler|PrivacyMask" --nologo --no-build` → **12/0/0** (11 facts + xUnit discovery overhead).
- Full suite: **330/0/9/339** — no regressions, no flakes across 3 consecutive runs.

## Outcome
✅ Shipped. All 11 test facts green; Bishop's contracts locked; reflection-safe for future refactors.
