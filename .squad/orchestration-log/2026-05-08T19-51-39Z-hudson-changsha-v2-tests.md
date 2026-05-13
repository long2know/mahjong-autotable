# Orchestration Log: hudson-changsha-v2-tests

**Agent:** Hudson  
**Task:** Changsha v1 Phase 2 Test Coverage  
**Started:** 2026-05-08  
**Status:** Completed (with 2 blocking findings)  
**Branch:** stlong/changsha-v1-phase2  

## Deliverables

- Un-skipped 70 of 77 P0 tests to GREEN
- Test infrastructure: ChangshaTestHelpers, BotMatchHarness
- Coverage across 11 categories: A–K (Tiles, Dice, Deal, Turn Flow, Melds, Wins, Scoring, Banker, State Machine, Bots, Edge Cases)
- Final: 68 GREEN, 2 RED (Bishop bugs), 7 skipped (v2 deferral)
- Acceptance bar (≥60 of 74) exceeded by 8

## Commits

- 0e07c53
- 05e7781
- 66e4b4f
- d3021ab
- fb3610c

## Blocking Findings

**2 ScoringService bugs discovered** (test assertions correct per spec §5.1):

1. Small Win self-draw: non-dealer pays 1, dealer pays 2 — currently all pay 2
2. Full Flush: Big Win should not double — currently pays 2×

Tests remain RED until Bishop applies fixes. See hudson-changsha-v2-bugs.md for details.
