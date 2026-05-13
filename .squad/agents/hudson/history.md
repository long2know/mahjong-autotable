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


[Previous history archived to history-archive.md on 2026-05-13 at 17,677 bytes. Summarized below.]

## Recent Highlights (2026-05-13 Phase 3)

**Phase 3 Frontend Test Infrastructure Wave**
- Vitest infra landed: vitest@^4.1.6, jsdom, @testing-library/react@^16.3.2, jest-dom matchers, userEvent.
- First wave: 47 tests GREEN across reducer, bridge, signalrClient, useChangshaMockGame (jsdom + React 19 compatible).
- Coverage: state-transition correctness (reducer 19 tests), wire-protocol contract (bridge 5, signalrClient 19, mock hook 4).
- Blockers: useLiveChangshaGame refactor needed (HubConnection in module scope); component tests deferred.
- Scripts: `npm test` (single-run), `npm run test:watch`, `npm run test:ui` (browser-based).
- Next wave: hook testability refactor, component tests (1d), bridge diffAndSend tests (0.5d), property tests for reducer phases (0.5d).

**Phase 3 Status:** Merged to main in PR #25 (SHA a03feda). 47 tests covering state machine correctness and API contract stability—regression guard against future reducer/bridge/client changes.

## Learnings — Phase 5a Frontend Test Coverage (2026-05-13)

**Wave:** Phase 5a — Strategy C wire-up (iframe URL params, camera-toggle bridge, sidebar hide when embedded). Tests written against Hicks's actual Phase 5a commit `1c1bd4a` (landed mid-session via `git fetch`).

**Frontend test count:** 48 → 60 active + 2 skipped = 62 total (delta +12 active, +2 intentionally skipped).

**New test files (4):**
- `changshaTablePage.iframeUrl.test.tsx` — 4 active tests. Imports Hicks's exported `buildAutotableIframeSrc(gameId, seatIndex?)` from `pages/ChangshaTablePage.tsx` and pins the canonical `/autotable/?gameId=&embedded=1&seat=` URL format. Spectator path (omit `seat` when `seatIndex` undefined) verified. Value-stability (same inputs → identical strings) underwrites Hicks's `useMemo([state.gameId, userSeat])` in `AutotableViewport`.
- `autotableBridge.cameraToggle.test.ts` — 3 active tests. Loads `changsha-bridge-receiver.js` via `require('node:fs')` + `eval` inside jsdom (module-level one-time load to avoid stacked window listeners). Asserts `{ proto:'changsha-bridge/1', type:'camera-toggle' }` → synthetic `keydown(key='p', code='KeyP', bubbles:true)` on `document`. Negative control confirms wrong-proto messages are dropped.
- `autotableBridge.embedded.test.ts` — 2 active + 2 skipped. **Active:** static fixture check parses `src/frontend/autotable/index.html` and asserts the inline `<script>` reads `URLSearchParams(...).has('embedded')` and sets `data-changsha-embedded="1"` on `<html>`, plus a CSS rule `html[data-changsha-embedded="1"] #sidebar { display:none }`. This guards against an accidental upstream re-mirror clobbering Hicks's edits. **Skipped (permanent):** manual e2e repro for runtime behavior (jsdom can't navigate to bundle HTML). **Skipped (not-implemented):** fallback receiver-based path — only used if a future refactor moves the logic into the receiver.
- `changshaReducer.signalrIntegration.test.ts` — 3 active tests. Regression guard: snapshots the 20-discriminator `GameAction` union (alphabetically sorted), verifies `reset` restores the expected `initialChangshaState` shape (4 `SeatHand` entries with empty `concealed[]`, 4 `SeatInfo` with default east/south/west/north winds), and module-export smoke for `useChangshaGame` / `useLiveChangshaGame` / `useChangshaMockGame`.

**Seams that needed mocking / scaffolding:**
1. **Receiver script in jsdom.** Vite's `?raw` suffix is blocked by `server.fs.allow` because `src/frontend/autotable/` is outside the modern frontend root. Fell back to `require('node:fs')` with local ambient declarations (`declare const require: ...`, `declare const __dirname: string`) — frontend `tsconfig.json` intentionally omits `@types/node` to keep app code browser-only, so the test files pull node types at runtime without polluting wider build.
2. **Receiver IIFE listener stacking.** The receiver registers `window.addEventListener('message', ...)` anonymously. Re-loading per-test would stack listeners. Solved with a module-level `receiverLoaded` guard + `ensureReceiverLoaded()` so the receiver loads exactly once per test file (per-worker isolation handles cross-file safety).
3. **Index.html fixture parsing.** The canonical sidebar-hide implementation lives in an inline `<script>` in `index.html` — unreachable from jsdom. Static-fixture regex assertions cover the source-level invariant; the manual repro covers runtime behavior.

**Skip-marked tests + un-skip instructions (2 total):**
- `embedded: ?embedded=1 hides upstream sidebar (manual/index.html)` — **stays skipped permanently**. Manual repro path inlined in test body (verify via `http://localhost:5114/autotable/?embedded=1`). Covered statically by the index.html-parse test.
- `embedded: fallback path via receiver` — **stays skipped** unless a future refactor moves sidebar-hide into the receiver script instead of `index.html`.

**Phase 5b followups filed:** None code-blocking. Future component-render test for iframe-src memoization (mock `useChangshaGame`, render `ChangshaTablePage`, verify identity across re-renders) is noted in the decisions inbox as an optional Phase 5b hardening.

**Test count delta:** +12 tests (well above 7-8 target — extras are negative controls + fixture checks that pinned more contract surface than the brief required).


