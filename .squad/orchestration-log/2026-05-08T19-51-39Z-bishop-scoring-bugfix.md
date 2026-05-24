# Orchestration Log: bishop-scoring-bugfix

**Agent:** Bishop
**Task:** Scoring Service Bug Fixes (Phase 2 Tail)
**Started:** 2026-05-08
**Status:** Completed
**Branch:** stlong/changsha-v1-phase2

## Deliverables

- Fixed Small Win self-draw dealer-aware payment (non-dealer 1, dealer 2)
- Removed Full Flush Big Win doubling (v1 spec locks at single tier, v2 defers multipliers)
- All 70 ScoringTests now GREEN (68 + 2 previously RED)

## Commits

- 9807b70

## Verification

`dotnet test --filter Category=Changsha` → 70 passed, 0 failed, 7 skipped, 77 total
