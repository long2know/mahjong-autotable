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

---

## 2025-05-08 — Changsha v1 Phase 2 (turn-on tests)

**Charter:** un-skip the 74 P0 tests and drive them GREEN against Bishop's shipped service interfaces. Acceptance bar ≥60 of 74 GREEN.

**Result:** **68 GREEN / 2 RED / 7 skipped of 77.**
Acceptance bar exceeded by 8.

**Work delivered:**
- New shared harness at `tests/.../Changsha/_TestHarness/`:
  - `ChangshaTestHelpers.cs` (Tid, Logical, Tiles, HandOf, NewGameDealtTo)
  - `BotMatchHarness.cs` (drives state machine + 4 ChangshaBotPolicy instances to hand-end)
- Re-wrote all 11 catalog files (CAT-A through CAT-K) with real assertions against shipped APIs (`ChangshaDeckBuilder`, `DiceService`, `BreakPointService`, `DealService`, `ChangshaWinDetector`, `ClaimAdjudicator`, `ScoringService`, `ChangshaGameStateMachine`, `ChangshaBotPolicy`).
- Two RED tests are documented Bishop bugs in `ScoringService` (see `.squad/decisions/inbox/hudson-changsha-v2-bugs.md`):
  1. `SmallWinSelfDrawBase = 2` flat (no dealer-involvement adjustment) — spec §5.1 wants 1/2.
  2. `flushMultiplier = 2` for Big Win Full Flush — spec v1 has no doubling.
- Seven skipped tests are deferred-v2 patterns (13-orphans, robbing-kong, stacking, decision-timeout API, optimistic concurrency API).

**Decision records:**
- `.squad/decisions/inbox/hudson-changsha-v2-tests.md` — coverage matrix.
- `.squad/decisions/inbox/hudson-changsha-v2-bugs.md` — two Bishop bugs with file/line, repro tests, suggested fixes.

**Verification:**
```
$ dotnet build  → 0 warnings, 0 errors
$ dotnet test --filter Category=Changsha
  Failed: 2, Passed: 68, Skipped: 7, Total: 77
```

**Skeptical notes:**
- The two RED tests will go GREEN automatically once Bishop applies the fixes; no test rewrites needed.
- BotMatchHarness uses `total = concealed + meldTileCount == 13 ? draw : discard` heuristic to handle post-claim states correctly. Watch for kong-replacement scenarios in v2.
- TurnFlow & PungKongChow tests inject paired tiles into a real seeded game state (rather than building bespoke game-state fixtures) — robust against future state-machine refactors as long as the public command surface stays the same.

📌 Team update (2026-05-08T19:51:39Z): Phase 2 bugfix tail shipped — both ScoringService bugs fixed (commit 9807b70). Tests now 70 GREEN, 0 RED, 7 skipped (v2 deferral). Changsha v1 Phase 2 complete: 179 API tests passed, 0 failed, 0 build warnings. Branch ready for merge. Deferred to v2: 13-orphans, kong-rob, stacking patterns, bot timeout-fallback API, optimistic concurrency API.

---

## 2026-05-13 — Changsha Playability Coverage Audit (read-only)

**Charter:** answer Stephen's deep-analysis question — "are we *finally* able to play Changsha mahjong with the autotable 3-D board?" Specifically: what is proven by tests vs. what is on faith. No new tests written this pass.

**Deliverable:** `.squad/decisions/inbox/hudson-changsha-coverage-audit.md` — per-category gap matrix (CAT-A..K), answers to 8 critical questions, top-5 ranked tests to add, skeptical notes.

**Baseline confirmed via test run:**
- `dotnet test --filter Category=Changsha` → 73 passed / 0 failed / 7 skipped of 80
- `dotnet test --filter Category=ChangshaHubE2E` → 3 passed / 0 failed
- `ChangshaServices/*` (no category trait) → 56 passed in unit form
- Frontend (`src/frontend/modern/`) → 0 tests; no vitest/jest dep; no `test` script

**Honest verdict:**
The backend Changsha v1 rules engine is meaningfully proven. The SignalR runtime is happy-path proven. The modern frontend (reducer / signalrClient / autotableBridge / useChangshaGame) is **entirely untested** — and that's exactly the layer the question is about.

**Biggest hole:** zero frontend test infrastructure. `autotableBridge.ts` translates backend state to the 3-D viewport's protocol — single most fragile module in the stack, no automated proof.

**Honest second-biggest hole:** `Bot_CompletesFullHand_WithoutIllegalMoves` accepts `WinnerDeclared OR WallExhausted` and seed-42 produces washout. The Hu → Score → RotateBanker chain through bots is not separately proven.

**Other concrete findings:**
- Two duplicated claim-priority tables exist (ClaimAdjudicator + ChangshaGameRuntime). They can drift; neither runtime-level multi-claim race is tested.
- Big-win dealer-self-draw and dealer-discarder-to-non-dealer scoring branches are not separately asserted — exactly Bishop's recent bug class.
- Reconnect E2E proves rehydration message arrives; it does NOT prove the resumed game is playable (no follow-up Discard or tile-equality assertion).
- `ClaimAsync` vs `PassAsync` post-window-close behaviour is inconsistent (throws vs. silent return) and untested.
- `CreateGame` hard-codes `DealerSeatIndex = 0` — no random-dealer logic, no test for H-01.
- 6 of 7 currently-skipped tests are honest v2 deferrals; 1 (`Bot_TimeoutFallback_DeferredV2`) sits on a false-blocker premise and can be written now.
- `StateJson` persistence round-trip is unverified — schema-evolution risk is invisible.

**Top-5 tests to add (ranked, do NOT write in this pass):**
1. Frontend vitest suite for `changshaReducer` + `autotableBridge` + `signalrClient` (the gap that most directly maps to Stephen's question).
2. Bot win-path assertion with seed-search lock-in (covers Hu → Score → RotateBanker that washout-only path skips).
3. Runtime multi-claim race + proximity tie-break test (closes the two-priority-tables drift risk).
4. Reconnect-resumes-playable-game extension to the existing E2E (asserts hand equality + follow-up command works).
5. Scoring `[Theory]` over (pattern × method × dealer-involvement × full-flush) with zero-sum invariant per row.

**Skeptical notes:**
- The bot harness uses `total = concealed + meldTileCount == 13 ? draw : discard` heuristic; correct under v1 but watch for kong-replacement edge cases in v2 where total can exceed 13 momentarily.
- The Hub E2E configures `BotTurnDelayMs=1, ClaimWindowTimeoutMs=50` — real-network timing bugs (e.g., out-of-order delivery) are invisible at these speeds.
- The catalog labels (A..N) drift from the task labels (A..K); audit explicitly maps them.

**Status:** Audit complete. Awaiting Stephen's verdict on whether (a) v1 ships with current backend confidence + zero frontend confidence, or (b) we pause for the top-5 before claiming "playable Changsha v1". No code changes made.


### 2026-05-13: Audit fan-out — Peer verdicts
- **Vasquez:** v1-scoped gameplay loop is conformant (three nuances: banker rotation, kong priority, missed-win rule)
- **Bishop:** Three real conformance bugs (kong priority, per-hand seed, banker rotation direction)
- **Hicks:** Frontend unplayable from UI (no lobby, no tile selection, 3D is theater)

## Learnings

### 2026-05-13 — Frontend test infrastructure (Phase 3 Stream C)
**Mission:** Establish a working vitest test runner for the modern React frontend and ship a first wave covering the layers driving the autotable 3D viewport.

**Delivered:**
- vitest 4.1.6 + @vitest/ui + jsdom 29 + @testing-library/{react,jest-dom,user-event} installed as devDependencies in `src/frontend/modern/package.json`.
- `vite.config.ts` extended with a `test` block (jsdom env, explicit imports — `globals: false`, setupFiles pointing to `src/test/setup.ts`, include `src/**/*.{test,spec}.{ts,tsx}`).
- `src/frontend/modern/src/test/setup.ts` registers `@testing-library/jest-dom/vitest` matchers and polyfills `window.matchMedia` (Fluent UI 9 hits it during render).
- npm scripts added: `test`, `test:watch`, `test:ui`.
- 47 tests across 4 files in `src/frontend/modern/src/changsha/__tests__/`:
  - `changshaReducer.test.ts` — 19 tests across all 18 reducer actions (GameCreated, PlayerSeated, GameStarted/DiceRolled/BreakPointSet, TilesDealt with per-seat hand splitting, TileDiscarded, ClaimWindowOpen, ClaimMade for pung/kong/chow with explicit tileIds, WinDeclared, ScoringComplete, BankerRotated, HandFinished, RoundChanged, GameEnded, reset).
  - `autotableBridge.test.ts` — 5 tests covering queue-then-flush on ready, envelope shape (`proto: 'changsha-bridge/1'` sentinel), source-of-message filtering, malformed/garbage drop, dispose teardown.
  - `signalrClient.test.ts` — 19 tests covering every invoke wrapper (createGame, joinTable, takeSeat, startGame, rollDice, acknowledgeDeal, discard, claim with/without tileIds, pass, declareKong, declareWin, reconnectGame), `attachServerEventHandlers` registration/forwarding/teardown/exception isolation, and `describeConnectionState` mapping.
  - `useChangshaMockGame.test.ts` — 4 tests asserting hook mounts, exposes the expected action surface, dealMock produces the canonical 14/13/13/13 hand split, discard removes the local tile and appends to the shared discard pile, resetDemo wipes state.

**Verification:**
- `cd src/frontend/modern && npm test` → 47 passed / 0 failed / 0 todo in 3.7s.
- `cd src/frontend/modern && npm run build` → green (verified in isolation; pre-existing Hicks Phase 3 WIP for `useLiveChangshaGame.ts` / new components currently in the working tree have internal inconsistencies that Hicks needs to land coherently — out of my stream's scope).

**Discipline calls:**
- Naming in the task brief used aspirational event names (PungClaimed/KongClaimed/ChowClaimed, DealCompleted, DiscardMade, etc.). The actual committed reducer dispatches on `ClaimMade` (with `meld.type` discriminating), `TilesDealt`, `TileDiscarded`, etc. I tested **current behavior**, not the brief's wording, per the task's "adjust the test to match current behavior" clause. The reducer was NOT modified.
- The autotableBridge envelope is `{ proto: 'changsha-bridge/1', type, ... }` with origin enforced by `ev.source === iframe.contentWindow` and inbound dispatch via callback (not CustomEvent). Tests pin that real shape, not the brief's `{ type, version, payload }` description.
- `reconnectGame` is committed as a `JoinTable` alias in `signalrClient.ts`; Hicks's uncommitted WIP retypes this to `ReconnectGame(gameId, seatIndex)`. Tests pin the committed alias — when his Phase 3 PR lands he must update tests in the same diff.

**Coverage gaps left for next wave:**
- `useLiveChangshaGame.ts` — Hicks's stream, not yet test-friendly (uses real SignalR client construction in module scope).
- `useChangshaGame.ts` — mock/live picker; trivially covered by separate hooks tests in future.
- React components (DiceRollModal, BankerBadge, ChangshaHud, PlayerHandPanel, ChangshaTablePage, etc.) — Hicks's territory; should be tested via @testing-library/react after his Phase 3 refactor lands.
- `tileUtils.ts` — pure helpers, low-risk; deferred.
- `diffAndSend` in autotableBridge — Phase 3 will exercise this far harder once 3D scene wiring is real; deferred to next wave.

**Skeptical notes:**
- The bridge ignores messages by `source !== iframe.contentWindow` not by `origin`. That's the right call in jsdom (jsdom does not enforce same-origin on iframe.contentWindow), but in production this still leaves the bridge unprotected if a malicious page injects an iframe that shares the same contentWindow reference via clobbering. Worth a Phase 3 review.
- `useChangshaMockGame` exposes 9 actions; `useLiveChangshaGame` exposes a superset (`declareKong`, `declareWin`, `pass`). The unified `useChangshaGame` typing has optional keys for those — components that call them must guard with `?.()`. Not test-covered today; failure mode is silent no-op.
