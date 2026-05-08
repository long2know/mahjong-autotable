# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Hudson as Tester.
- Quality focus areas: rule correctness under edge conditions, API contract stability, and end-to-end gameplay regression.

## Work Log

### 2026-04-20: Changsha Test Catalog
**Mission:** Produce comprehensive test scenario catalog for Changsha Mahjong rules derived from source materials.

**Sources Analyzed:**
- MahjongPros.com (S1) — primary, detailed beginner's guide
- Baidu Baike (S2) — scoring specifics and technical rules
- Reddit r/Mahjong (S3) — inaccessible (verification wall)

**Deliverable:** `docs/rules/changsha-test-catalog.md`
- 80 total scenarios across 14 categories (tile set, deal, turn flow, melds, win patterns, scoring, bird catching, state machine, bots, API)
- 47 P0 (critical), 21 P1 (high), 12 P2 (medium) priority
- Every scenario structured: ID, description, setup, trigger, expected, source, priority

**Critical Findings:**
- **8 rule contradictions identified** between sources (see decisions inbox)
- **Top blockers for implementation:**
  1. Bird tile count: 1 vs. 2 tiles drawn (scoring impact)
  2. Scoring model: 1/6/7 point vs. 10/20/60/70 point system (fundamental)
  3. Multiple win resolution: single winner (proximity) vs. all win simultaneously
  4. Starting instant win continuation: hand ends vs. hand continues

**Skeptical Notes:**
- Baidu source references "Red Dragon" for dealer tiebreaker, but Changsha excludes all dragons from tile set—self-contradiction suggests adaptation error from another variant
- S1 emphasizes "258 Generals" pair requirement, S2 adds "random eye" exemptions for Big Wins—both valid but need clear hierarchy
- Kong replacement draw rules vary significantly between sources (dice optional vs. gated by ready state)

**Decision Record:** `.squad/decisions/inbox/hudson-changsha-test-catalog.md` created for Vasquez to resolve contradictions.

**Status:** Catalog complete. Awaiting spec resolution before test code generation. Ready to convert scenarios to xUnit/NUnit tests once rules finalized.

📌 Team update (2026-05-05T17-00-21Z): Test catalog decision merged to `.squad/decisions.md`. Vasquez completed Changsha canonical spec at `docs/rules/changsha-spec.md` addressing most rule areas; 11 open questions and 8 contradictions identified in catalog require product direction. Bishop completed backend audit at `docs/rules/changsha-backend-gap.md` (3/18 implemented, 10 missing). Hicks completed frontend plan at `docs/rules/changsha-frontend-plan.md` (Option B selected). Ready to convert 80 scenarios to code tests once high-priority contradictions resolved by Vasquez.

### 2026-05-08: Changsha v1 Test Suite — P0 Coverage Complete
**Mission:** Convert P0 catalog scenarios into executable xUnit tests for Changsha v1 implementation wave.

**Deliverable:** 77 xUnit tests across 11 test classes in `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/`
- **CAT-A-K coverage:** Tile set, dice/break, deal, turn flow, meld claims, win patterns (4 core), scoring, banker rotation, state machine, bot behavior, edge cases
- **Status:** 74 tests skipped (awaiting Bishop's services), 3 deferred to v2 (instant wins, kong robbing)
- **Build:** Compile-clean, zero failures, all properly gated with Skip attributes

**Test Discipline:**
- TDD naming convention: `{Behavior}_When_{Condition}_Then_{Expected}`
- Deterministic with seed injection for replay integrity
- One assertion per test where practical
- Trait `[Category="Changsha"]` for filtering

**Rule Resolutions Applied:**
- Scoring model: 1/6/7 simplified model (Small Win 1pt, Big Win 6/7pt with dealer bonus)
- Multiple wins: proximity rule (closest CCW from discarder)
- 258 pair: exempted for All Pungs, Full Flush, Seven Pairs Big Win patterns
- Chow: next-seat only (immediate CCW neighbor from discarder)

**Critical Fix:**
- Created `WinDetector.cs` stub to resolve Bishop's `ClaimAdjudicator.cs` compilation error (missing interface reference)

**Blockers:**
All 74 active tests blocked on Bishop's service implementations in critical path order:
1. ChangshaDeckBuilder (4 tests)
2. DiceService (5 tests)
3. DealService (7 tests)
4. ChangshaGameStateMachine (21 tests)
5. WinDetector (6 tests)
6. ScoringService (9 tests)
7. ChangshaBot (8 tests)

**Decision Record:** `.squad/decisions/inbox/hudson-changsha-v1-tests.md` created with full coverage matrix, blocker analysis, and recommended TDD workflow.

**Skeptical Notes:**
- Tests assume Bishop will provide seed-injectable constructors for determinism — failure to do so breaks replay tests
- Bot tests require seat-scoped view projection (no privileged tile access) — if Bishop shortcuts this for bots, tests will fail and expose fairness gap
- State machine tests expect optimistic concurrency via StateVersion field — if Bishop omits this, concurrent action conflicts undetectable
- Win detector must validate 258 pair requirement strictly for Small Win, with explicit Big Win exemptions — catalog contradictions suggest risk of loose validation

**Status:** P0 test suite COMPLETE and pushed (commit `132400f`). Ready for Bishop to begin TDD workflow: uncomment one test, implement until green, repeat. Tests serve as executable specification and regression safety net. DO NOT merge Bishop's code without corresponding green tests.
