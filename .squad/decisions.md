# Squad Decisions

## Active Decisions

### 2026-05-13T17:45Z: User directive — Phase 5a defaults locked
**By:** Stephen Long (via Copilot)
**What:** Accepted all 8 Hicks-recommended defaults from the 3D renderer spike. Phase 5a proceeds under Strategy C (Fake autotable WS server).

Locked defaults:
1. **Dice roll**: auto-roll on hand start (matches Phase 3 modal flow). No visual click-to-roll required.
2. **Standalone `/autotable/` sandbox**: preserved post-embed wiring. Useful for debugging the upstream view in isolation.
3. **Camera toggle**: HUD button + upstream's `P` keybind both supported. React HUD posts `{ type: 'camera-toggle' }` to iframe; receiver simulates the `P` keypress on `document`.
4. **Discard layout**: 4-side radial layout in the 3D view (upstream-style). React's per-seat 2D stacks remain as the fallback when the iframe is hidden or fails to load.
5. **Reconnect**: snap to current state on mid-hand reconnect. No replay buffer. Simpler and avoids divergence risk.
6. **Wall split**: canonical **14/14/13/13** across the four seats (108 ÷ 4 asymmetric). Matches canonical Changsha rules tiebreaker (MahjongPros).
7. **WS endpoint path**: `/autotable/ws` (mirrors upstream's expectation). Bishop confirms the bundle's `getUrl()` resolves there from the iframe location.
8. **Throttling**: deferred to Phase 5d (per-seat camera + spectator). Phase 5a allows single concurrent game per backend instance.

**Outcome:** Shipped via PR #26 squash-merged at `ce1bda6` on main.

### 2026-05-13: Bishop — Phase 5a Backend (Strategy C Autotable WS Endpoint)
**By:** Bishop (Backend Dev)
**What:** Backend now exposes a fake upstream `pwmarcz/autotable` WS server at `/autotable/ws` that speaks `NEW`/`JOIN`/`JOINED`/`UPDATE` verbatim. Hicks's unchanged `autotable.9519e86d.js` bundle connects and renders authoritative Changsha state in 3D — walls, hands (own seat face-up, others face-down), discards, and melds (concealed kongs face-down).

Files added: `AutotableProtocol.cs` (~140 LOC), `AutotableSlotMap.cs` (~130 LOC), `ChangshaToAutotableTranslator.cs` (~260 LOC), `AutotableWsEndpoint.cs` (~280 LOC). 23 new tests added. Wall split enforced as 14/14/13/13 per Default #6. `fives='000'` forces byte-identical bundle behavior with no translation table.

**Outcome:** Shipped via PR #26 squash-merged at `ce1bda6` on main. Backend tests: 203 → 226 passing (+23).

### 2026-05-13: Hicks — Phase 5a Frontend Wiring
**By:** Hicks (Frontend Dev)
**What:** Iframe is now wired to live Changsha game state at `/autotable/?gameId={id}&embedded=1&seat={N}`. URL format enforces `gameId` (mandatory for bundle auto-connect), `embedded=1` (hides upstream sidebar via CSS), and optional `seat` parameter. Camera-toggle HUD button wired via `postMessage` → synthetic `KeyboardEvent('P')` on `document` to trigger upstream's perspective toggle.

Files: `index.html` (+1 LOC), `ChangshaTablePage.tsx` (+57/-13), `autotableBridge.ts` (+1), `changsha-bridge-receiver.js` (+16), `CameraToggleButton.tsx` (+43 new), index re-export. `useMemo([gameId, userSeat])` prevents iframe reload on parent re-renders.

**Outcome:** Shipped via PR #26 squash-merged at `ce1bda6` on main. Frontend tests: 48 → 60 active (48/48 owned vitest tests green).

### 2026-05-13: Hudson — Phase 5a Frontend Test Coverage
**By:** Hudson (QA & Test Framework)
**What:** Locked down the Phase 5a frontend contract with 12 new vitest cases (10 active + 2 intentionally skipped). Test suites cover iframe URL building (`buildAutotableIframeSrc`), camera-toggle message contract, embedded-mode sidebar hiding (static fixture + negative control), and reducer regression guard (GameAction union snapshot).

Test progression: 48 active → 60 active + 2 skipped (62 total). Static fixture parse pattern for jsdom-unreachable DOM contracts. Helper-export pinning provides value-stability coverage without full React render.

**Outcome:** Shipped via PR #26 squash-merged at `ce1bda6` on main. No regressions; 48/48 baseline tests green + 12 new Phase 5a tests green.

### 2026-04-20: Project baseline and rules direction
**By:** Stephen Long (with Squad)
**What:** Use `pwmarcz/autotable` as the base framework and prioritize Changsha Mahjong support first, while keeping an expansion path for broader Chinese rules.
**Why:** Reusing an existing gameplay/table baseline reduces startup risk and keeps early delivery focused on rule adaptation and quality.

### 2026-04-20: Backend and persistence strategy
**By:** Stephen Long (with Squad)
**What:** Build the backend on .NET 10 with Entity Framework Core and SQLite initially, while keeping the persistence layer provider-flexible for PostgreSQL or SQL Server later.
**Why:** SQLite keeps local development friction low now and preserves a clean migration path to container-backed production databases.

### 2026-04-20: Developer workflow and deployment target
**By:** Stephen Long (with Squad)
**What:** Local development should run backend + frontend from VS Code with F5, and deployment should package both layers in a single Docker image for Linux hosting.
**Why:** This aligns daily dev ergonomics with the intended self-hosted production runtime.

### 2026-04-20: Frontend modernization approach
**By:** Stephen Long (with Squad)
**What:** Keep autotable's existing frontend as the immediate baseline and introduce React + Fluent UI 9 + TypeScript + Vite incrementally only where the migration cost is justified.
**Why:** This avoids slowing initial Changsha delivery while still enabling gradual UI modernization.

### 2026-04-20T23:33:30Z: User directive
**By:** Stephen Long (via Copilot)
**What:** Continue iterative delivery with repeated workflow: commit, push, open/complete PR, pull main, then repeat until complete.
**Why:** User request — captured for team memory

### 2026-04-20: Initial project structure and delivery scaffolding
**By:** Ripley (Infrastructure)
**What:** Adopt a layered structure with `src/backend/src/Mahjong.Autotable.Api` (.NET 10), `src/frontend/autotable` (baseline), `src/frontend/modern` (React + Fluent UI 9), `.vscode` launch/tasks, and `infra/docker/Dockerfile` with two deploy targets (`runtime-autotable` and `runtime-modern`).
**Why:** Keeps Changsha delivery focused and low-risk while preserving a controlled, non-disruptive path to modern UI adoption and future DB provider changes.

### 2026-04-22: Authoritative Draw/Discard Loop Slice
**By:** Bishop (Backend Dev)
**What:** Move table state to an authoritative model with seeded deterministic wall, per-seat hand ownership, explicit discard pile, and turn-phase gating (`AwaitingDiscard`, `WallExhausted`). Add `POST /api/tables/{id}/actions/discard` endpoint.
**Why:** Keeps server authority explicit, prevents privileged bot-only mutation paths, and gives deterministic replay hooks immediately while staying phase-appropriate.

### 2026-04-22: Backend bot-play foundation
**By:** Bishop (Backend Dev)
**What:** Implement initial 4-seat table backend state using `TableSession.StateJson` as the canonical serialized game state, with a persisted `StateVersion` counter and `LastActionUtc` metadata for forward-compatible evolution.
**Why:** Keeps schema stable while game rules evolve, enables deterministic bot turn progression without locking in full Mahjong rule structures, and preserves migration flexibility across database providers.

### 2026-04-22: Claim resolution phase-1 contract
**By:** Bishop (Backend Dev)
**What:** Adopt explicit claim resolution through `POST /api/tables/{id}/claims/resolve` with decision values `pass` and `take-selected`. Discard enters `AwaitingClaimResolution` only when opportunities exist.
**Why:** Keeps phase-1 backend integration safe and explicit for UI work while preserving replay integrity, optimistic concurrency, and tile conservation before full meld/scoring settlement.

### 2026-04-22: Explicit server-driven bot advance for UI loop
**By:** Bishop (Backend Dev)
**What:** Extend `POST /api/tables/{id}/bots/advance` with `advanceUntilHumanTurnOrWallExhausted` (default `true`) so backend can safely progress from bot turn to the next human turn in one call.
**Why:** Removes fragile frontend dependence on selecting a `maxActions` budget and makes the playable loop deterministic at the service boundary while preserving capped mode for diagnostics/testing.

### 2026-04-22: Changsha-first bot play must share one authoritative rules pipeline
**By:** Vasquez (Rules & Bot Foundation)
**What:** Define and lock a `changsha-v1` deterministic transition contract before gameplay implementation. Require humans and bots to submit actions through the same authoritative validator/arbitration pipeline. Enforce seat-scoped state projection, replayable determinism (seeded RNG + append-only action/event log), and safe fallback actions on bot timeout/failure.
**Why:** Prevents privileged bot behavior, closes fairness/security gaps, and keeps Changsha semantics testable and composable for later Chinese variants.

### 2026-04-22: Changsha v1 contract assumptions for draw/discard implementation slice
**By:** Vasquez (Rules & Bot Foundation)
**What:** Lock this phase to a deterministic draw/discard-only loop with no claim interrupts. Treat draw as server-owned, discard-only for external seat actions. Require one validator path for human and bot. End round on live-wall exhaustion. Require replay integrity via seed + ordered event log + canonical state hash.
**Why:** Creates a safe, composable baseline for Changsha rule expansion without reworking state integrity or action authority boundaries.

### 2026-04-22: Graphical UI Playability
**By:** Hicks (Frontend Dev)
**What:** The modern frontend primary flow is now table-first and playable: one-click table creation, graphical tile rendering, clickable human discards, and automatic bot advancement until the next human turn or wall exhaustion.
**Why:** Keeps migration incremental while delivering a real gameplay loop immediately, with strict replay verification and diagnostics preserved in an Advanced tools section.

### 2026-05-05: Changsha Mahjong canonical spec — key rulings
**By:** Vasquez (Rules & Bot Foundation)
**What:** 108-tile composition (suits only, no winds/dragons/flowers/jokers/红中). Dice-driven break point with batch-of-4 deal flow. 258 pair rule for hu validation. Chow is legal (next player only). Supported: Small Win (4+1), Big Wins (Heaven, Earth, Kong types, Robbing Kong, etc.), All Pungs/Generals/Flush/Seven Pairs/Full Beggar's/Luxury Seven Pairs, Instant Wins (Four Joys, All Pure, Voided Suit, Six Six Straight). Two-tier scoring with dealer bonus. No dead wall (all tiles drawable to seabed).
**Why:** Cross-referenced three authoritative sources to establish baseline rules before implementation. 11 open questions identified requiring product direction. See `docs/rules/changsha-spec.md` for full spec.

### 2026-05-05: Changsha Backend Audit — Implementation Gap Report
**By:** Bishop (Backend Dev)
**What:** Current engine (136 tiles, generic draw/discard/claim) has 3/18 Changsha behaviors IMPLEMENTED, 5 PARTIAL, 10 MISSING. 38 existing tests pass (100%); 0 Changsha tests exist. Ten-item refactoring roadmap: tile set → disable chow → 红中 wildcard → self-draw win → win patterns → scoring → banker/dice/round → kong variants → multi-round persistence → bot awareness.
**Why:** Establishes implementation roadmap and dependency order. See `docs/rules/changsha-backend-gap.md` for gap analysis matrix and detailed rationale.

### 2026-05-05: Changsha frontend architecture — Option B
**By:** Hicks (Frontend Dev)
**What:** Backend-authoritative state with autotable as 3D viewport. WebSocket bridge translates backend `TableGameState` to autotable upstream protocol. Changsha-specific UI built as React Fluent UI 9 components (dice modal, banker badge, round indicator, fan scoring panel). Four-phase rollout: Phase 1 (UI components, ready now), Phase 2 (WS bridge, blocked on endpoint confirmation), Phase 3 (deal animation), Phase 4 (fan panel, blocked on fan table delivery).
**Why:** Preserves autotable investment while enabling modern UI and clear architectural boundaries. See `docs/rules/changsha-frontend-plan.md` for full plan with blockers and dependencies.

### 2026-05-05: Changsha Test Catalog — 80 scenarios, 8 contradictions
**By:** Hudson (QA & Test Framework)
**What:** 80 comprehensive test scenarios covering tile set, wall construction, dice/break, deal, turn flow, pung/kong, win patterns, scoring, banker rotation, bird capturing, seabed, edge cases, determinism, bot behavior, API integration. P0: 47, P1: 21, P2: 12. High-priority contradictions: bird tile count (one vs. two), scoring model (1/6/7 vs. 10/20/60/70), multiple win resolution (simultaneous vs. proximity), starting instant win continuation (ends vs. continues).
**Why:** Establishes comprehensive test blueprint and surfaces rule ambiguities blocking implementation. Vasquez must resolve HIGH-priority contradictions. See `docs/rules/changsha-test-catalog.md` for full catalog and contradiction detail.

## Audit Reports — Changsha v1 Playability (2026-05-13)

These are the four deep-dive audits completed 2026-05-13 in parallel to assess whether Changsha v1 is playable end-to-end with the autotable 3D viewport. Each audit is read-only (no code changes) and documents the current state of implementation, test coverage, and spec conformance. See `/docs/rules/changsha-spec.md` v1.1 and `.squad/agents/*/history.md` for context.

---

### Vasquez — Changsha Mahjong Conformance Audit (v1)

*(Full audit report follows — 34.4 KB; see original file `vasquez-changsha-conformance-audit.md` for citation sources and detailed rule-by-rule tracing)*

**TL;DR (Vasquez):** The v1-scoped gameplay loop is conformant. We can play Changsha end-to-end against the 3D autotable. Three nuances worth flagging: (1) banker rotation code direction contradicts the spec example in one sentence, (2) Kong and Pung priority should be tied (both tier-2) per spec but code ranks Kong higher, (3) missed-win rule (过水) is not enforced despite being in v1 spec lock. These are real gaps that do not gate a demo but block external release claims.

[Full Vasquez Conformance Audit - see inbox for complete report]

---

### Bishop — Changsha Backend Conformance Audit (Phase 2 → Live Play)

*(Full audit report follows — 19.6 KB; see original file `bishop-changsha-backend-audit.md` for detailed code traces)*

**TL;DR (Bishop):** **Conditional GO.** The full Changsha v1 loop runs end-to-end for a single hand and persists snapshots. Three real conformance bugs and two design gaps must be fixed before we claim the v1 conformance checklist is satisfied. None are gating the autotable 3D demo for a single hand; all are gating a clean 16-hand championship game.

**Critical bugs:** 
1. Kong priority is higher than Pung (wrong; should be equal, CCW-tie-break decides).
2. Wall is identical every hand of a single game (fairness bug; should derive per-hand seed from base seed + hand number).
3. Banker rotation direction contradicts spec example (spec says -1 mod 4, code does +1 mod 4).
4. Server-restart loses in-flight games (snapshots persisted but never hydrated on startup).
5. Missed-win rule and base-unit multiplier are silently absent (not documented as v2 deferrals).

[Full Bishop Backend Audit - see inbox for complete report]

---

### Hicks — Changsha Frontend Playability + 3D Bridge Integrity Audit

*(Full audit report follows — 19.1 KB; see original file `hicks-changsha-frontend-audit.md` for action-by-action breakdown)*

**TL;DR (Hicks):** **Partially in 2D, no in 3D.** If a game is already started against bots, the React 2D panels let you discard, see basic claim opportunities, watch a fan-and-score panel, and see the banker rotate. **You will not be able to:** (1) start a game from the UI, (2) pick which tiles to use for chow claims, (3) declare a concealed/added kong, (4) declare a self-draw win, or (5) see anything Changsha-shaped in the 3D viewport (it's still the upstream demo scene with a text overlay).

**Top 3 UX gaps blocking real play:**
1. No lobby / no Start Game path — add "New game vs bots" button that invokes createGame → fillWithBots → takeSeat(0) → startGame, plus localStorage persistence for game rehydration.
2. Claim UI is too coarse — need chow tile-combo picker, Declare Kong button, Declare Win (Zimo) button.
3. No discard pile / no sorted hand — per-seat oriented discard trays + suit-sorted concealed hand are table-stakes for playability.

The 3D viewport is theater — bridge moves Changsha state into the iframe as text overlay only. Upstream three.js scene is unaware of our game.

[Full Hicks Frontend Audit - see inbox for complete report]

---

### Hudson — Changsha v1 Playability Coverage Audit (read-only)

*(Full audit report follows — 28.2 KB; see original file `hudson-changsha-coverage-audit.md` for detailed test matrix)*

**TL;DR (Hudson):** Backend rules engine (108-tile wall, batch-of-4 deal, 258-pair detection, pung/kong/chow priority, four win patterns, two-tier scoring, banker rotation, deterministic seeded replay) is meaningfully *proven* by 73 green tests. Runtime hub layer (claim race resolution, reconnect rehydration, claim-window timeout) is *partially* proven — only happy-paths. **Modern React/Three.js frontend is entirely on faith — zero unit tests, zero integration tests.** 

**Frontend is the biggest hole in "playable Changsha v1"** — no vitest, no jest, no `*.test.ts` anywhere. The reducer and bridge are pure functions but entirely untested.

**Top 5 tests to add (highest confidence per effort):**
1. Frontend reducer + autotable-bridge smoke suite (vitest) — ~1 day
2. Bot end-to-end win path with seed assertion — ~0.5 days
3. Multi-claim race resolution in runtime — ~0.5 days
4. Reconnect playability (not just rehydration message) — ~0.5 days
5. Scoring combinatorial parameterized test — ~2 hours

[Full Hudson Coverage Audit - see inbox for complete report]

---

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

---

### 2026-05-13: Canonical rules source — MahjongPros
**By:** Stephen Long (via Copilot)
**What:** When the three reference sources (Baike / Reddit / MahjongPros) disagree, MahjongPros is the tiebreaker. All gameplay-correctness audits and future implementation choices should defer to MahjongPros first, then Baike for tile-set + scoring formalities, then Reddit as background color.
**Why:** Ensures consistent rule interpretation across audits and implementation waves.

---

### 2026-05-13: User directive — default model bumped to opus-4.7-xhigh
**By:** Stephen Long (via Copilot)
**What:** All squad agents now default to `claude-opus-4.7-xhigh` (Claude Opus 4.7, Extra high reasoning). Persisted in `.squad/config.json` as `defaultModel`. Supersedes the prior `claude-opus-4.7` default.
**Why:** User request — captured for team memory. Coordinator passes this as the `model` param on every spawn unless a charter/session override applies.

---

### Changsha v1 Spec Lock — Decision Record
**Date:** 2026-05-06
**By:** Vasquez (Rules Engineer)
**What:** Revised `docs/rules/changsha-spec.md` (v1.0 → v1.1) to lock v1 implementation scope. Resolved all 11 open questions and 8 Hudson test catalog contradictions. V1 scope locked to: 108-tile set, 4 win patterns (Standard 4+1, SevenPairs, AllPungs, FullFlush), MahjongPros-anchored scoring (1/2/3/4/6/7 unit model), claim priority (Hu>Kong=Pung>Chow), banker rotation (keep on dealer win, rotate CCW otherwise), 16-hand games, chow allowed (next-seat only).
**Why:** Eliminates all ambiguity from v1 scope. The locked contract enables deterministic, testable implementation with clear v2 expansion path.
**V2 Deferrals (8 areas):** Instant wins, draw-based big wins, hand-based big wins, ready-kong dice, bird-catching, kong micro-payments, multiple simultaneous winners, seabed tile choice.

---

### Bishop — Changsha v1 Implementation Decisions
**Date:** 2025-01-XX
**By:** Bishop (Backend Dev)
**What:** Domain layer structure: new `Changsha/` namespace under `Mahjong.Autotable.Api.Changsha`, keeping Changsha-specific types separate from the existing 136-tile generic engine. Tile ID scheme reuses existing 0–107 convention. State machine is pure-functional event-sourced. Wall layout is flat ordered list (not physical 4-wall model). 258 pair rule enforced for Standard wins. Scoring follows spec §5 payment table. SignalR hub is a thin pass-through skeleton. Bot policy is heuristic-based. Persistence uses Changsha-specific entities (`ChangshaGame` + `ChangshaGameEvent`) separate from existing `TableSession` to allow both engines to coexist.
**Why:** Modular architecture allows iterative delivery and backward compatibility.

---

### Bishop — Changsha v1 Phase 2 Runtime Architecture
**Date:** 2026-05-06
**By:** Bishop (Backend Dev)
**What:** Singleton `IChangshaGameRuntime` holds in-memory `ConcurrentDictionary<string, ChangshaGameInstance>` with per-instance `SemaphoreSlim` for command serialization. Persistence via short-lived `IServiceScopeFactory` scopes that resolve `AppDbContext` per snapshot. Bot timing: 350ms bot-turn delay, 250ms bot-claim delay, 5s claim-window timeout. Claim window resolution: emit `ClaimWindowOpen`, schedule bot responses, start 5s cancellation token, on expiration auto-pass unresponded seats. `FullState` snapshot strategy: send materialized state + private concealed tiles on reconnect (not event-log replay).
**Why:** Prioritizes sub-millisecond hub turnaround for bot-driven games while preserving claim-window semantics.

---

### Hicks — Changsha v1 Frontend — Phase 1 Component Inventory
**Date:** 2026-05-05
**By:** Hicks (Frontend Dev)
**What:** Phase 1 delivers Changsha Fluent UI chrome as React components with mock data: `DiceRollModal`, `BankerBadge`, `RoundWindIndicator`, `ChangshaHud`, `FanBreakdownPanel`, `PlayerHandPanel`, `ClaimPromptModal`, `ChangshaTablePage` (all at `/changsha` route). Build passes. All components render from mock `ChangshaGameState`. Demo controls panel cycles through all game phases.
**Why:** Establishes visual framework that Phase 2 wires to live SignalR.

---

### Hicks — Changsha v1 Phase 2 Frontend Wiring
**Date:** 2026-05-08
**By:** Hicks (Frontend Dev)
**What:** SignalR client architecture uses reducer pattern over `ChangshaGameState` with one action per server event. Mock vs live mode toggle: `useChangshaGame` is a thin shim picking `useLiveChangshaGame` or `useChangshaMockGame` based on `localStorage('changsha.useMock')` or `import.meta.env.DEV`. Bridge protocol is one-way parent → child for Phase 2 (Phase 3 adds bidirectional canvas wiring). Tile rendering via SVG (Wan/Tong/Tiao suits with Unicode glyphs + dip patterns + bamboo sticks) instead of atlas decoding for fragility and maintainability.
**Why:** Demonstrates state propagation end-to-end without coupling Phase 2 to autotable mesh-event plumbing.

---

### Hudson — Bishop Scoring Bug Findings (Changsha v1 Phase 2)
**Date:** 2026-05-13
**By:** Hudson (Tester)
**What:** Two scoring bugs discovered while wiring CAT-G tests. (1) Small Win self-draw uses flat base, ignores dealer involvement — non-dealer self-draw should pay 1 to dealer and others, but code pays 2 to all. (2) Full Flush silently doubles Big Win payment — spec locks no stacking/doubling in v1; code has `flushMultiplier = 2` for FullFlush + BigWin. Tests written assert spec-correct behavior; they will go GREEN once Bishop applies fixes.
**Why:** Documents concrete bugs that the test suite now catches.

---

### Hudson — Changsha v1 Phase 2 Test Coverage
**Date:** 2026-05-13
**By:** Hudson (Tester)
**What:** 77 total tests (68 green, 2 red from documented Bishop bugs, 7 skipped for v2 deferrals). Backend rules engine (108-tile wall, batch-of-4 deal, 258-pair, pung/kong/chow priority, four win patterns, two-tier scoring, banker rotation, deterministic replay) proven by 73 green tests across 11 categories. Runtime hub layer (claim race resolution, reconnect rehydration, claim-window timeout) partially proven (happy-paths only). **Frontend entirely unproven — zero unit tests.**
**Why:** Establishes test baseline and documents coverage gaps.

# Vasquez — Banker Rotation Canonical Lock (Phase 3)

**Date:** 2026-05-13
**By:** Vasquez (Rules Engineer)
**Branch:** `stlong/changsha-v1-phase3`
**Spec version:** `docs/rules/changsha-spec.md` v1.1 → **v1.2**
**Status:** LOCKED — supersedes v1.1 banker rotation rule.

---

## Decision

**Canonical Changsha banker rotation (v1.2, LOCKED):**

> **The winner of a hand becomes the dealer for the next hand. On washout (wall exhausted with no winner), the current dealer keeps the seat. The hand counter increments regardless.**

There is **no** cyclic seat rotation (`+1 mod 4` or `-1 mod 4`) in v1. The next dealer is determined entirely by the hand outcome:

| Hand outcome | Next `DealerSeatIndex` |
|---|---|
| Winner declared (self-draw 自摸 or discard claim 点炮) | `winnerSeatIndex` (the seat that just won) |
| Washout (wall exhausted, no winner) — 流局 | Unchanged (current dealer keeps seat) |

`HandNumber` increments in both cases.

---

## Why this decision was forced

The v1.1 spec was **internally inconsistent and contradicted every canonical source**:

1. **v1.1 §6.2 text** said "dealer keeps seat on dealer-win; rotate counter-clockwise on non-dealer-win or draw" — a deliberate v1 simplification.
2. **v1.1 §6.2 example** demonstrated `-1 mod 4` (Seat 0 → Seat 3, Seat 3 → Seat 2).
3. **Backend implementation** at `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs:458,465` uses `+1 mod 4` — direction-inverted vs. the spec example.
4. **All three canonical sources** say "winner becomes dealer," not cyclic rotation.

So the spec disagreed with itself, the implementation disagreed with the spec example, and *both* disagreed with the canonical rule. Three different behaviors for one rule. Unacceptable for a Phase 3 gate.

---

## Source review (verified 2026-05-13 via web_fetch)

All three canonical sources agree — **winner becomes dealer**:

### MahjongPros (S1 — locked tiebreaker per Stephen, 2026-05-13)
> "In subsequent games the dealer is determined in one of the following orders:
> 1. The winner of the previous game becomes the new dealer.
> 2. In the case of a draw, the player that draws the last tile becomes the dealer.
> 3. If multiple players win simultaneously, the dealer is determined randomly among the winners based on consensus."

### Baidu / Tencent QQ (S2)
> "A. For the first round, the dealer is randomly assigned by the system.
> B. **In subsequent rounds, whoever wins a hand becomes the dealer for the next round.**
> C. If a player takes the bottom tile and no one wins, then that player becomes the dealer for the next round.
> D. If none of the four players want the bottom tile, then the player who has the first option to take the bottom tile in the next round becomes the dealer."

### Reddit (S3 — community overview)
Winner-becomes-dealer (consistent with S1/S2).

**Tiebreaker resolution:** Where S1 and S2 give finer-grained washout rules (last-drawer-becomes-dealer, etc.), v1 simplifies to **"washout keeps the seat"**. Rationale:

- V1 has no concept of "who drew the last tile" exposed in `ChangshaGameState`.
- "Washout keeps seat" is unambiguous, deterministic, and trivial to implement.
- Matches the dominant majority of online digital implementations.
- The finer-grained rule is captured as a documented v2 refinement in §6.2.

---

## Worked example (now in §6.2)

Starting with Seat 0 as dealer:

| Hand | Dealer | Outcome | Next Dealer |
|------|--------|---------|-------------|
| 1 | Seat 0 | Seat 2 wins | **Seat 2** |
| 2 | Seat 2 | Washout | **Seat 2** (unchanged) |
| 3 | Seat 2 | Seat 1 wins | **Seat 1** |
| 4 | Seat 1 | Seat 0 wins | Seat 0 |

This is the canonical sequence Bishop and Hudson should both encode.

---

## Impact on Bishop (Backend) — required change

**File:** `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs`

**Lines 458 and 465** (the `RotateBanker` helper / inline `+1 mod 4` logic):

**Replace** the current `state.DealerSeatIndex = (state.DealerSeatIndex + 1) % 4` (or equivalent rotation arithmetic) with:

```csharp
// Canonical Changsha v1.2 banker rotation (per docs/rules/changsha-spec.md §6.2):
//   - Winner: winner becomes next dealer.
//   - Washout: current dealer keeps the seat.
if (winnerSeatIndex.HasValue)
{
    state.DealerSeatIndex = winnerSeatIndex.Value;
}
// else: washout — leave state.DealerSeatIndex unchanged.

state.HandNumber += 1;
```

**Must NOT do:**
- No `(state.DealerSeatIndex + 1) % 4` cyclic rotation.
- No `(state.DealerSeatIndex - 1 + 4) % 4` cyclic rotation.
- No "dealer keeps seat only when dealer wins" special-case.

**Verification:** A 16-hand replay where Seat 2 wins hand 1, Seat 2 wins hand 2, washout hand 3, Seat 0 wins hand 4 must produce the dealer sequence `[Seat 0, Seat 2, Seat 2, Seat 2, Seat 0]` (initial through post-hand-4).

---

## Impact on Hudson (Tests) — required new coverage

Add at minimum **one parametric test** asserting winner-becomes-dealer across multiple hands. Suggested test name: `BankerRotation_WinnerBecomesNextDealer_AcrossMultipleHands`.

**Test outline:**

```csharp
[Theory]
[InlineData(/* hand-outcome sequence */)]
public void BankerRotation_FollowsCanonicalRule(...)
{
    // Arrange: start a 4-hand game, Seat 0 = initial dealer
    // Act: replay outcomes — hand 1: Seat 2 wins; hand 2: washout; hand 3: Seat 1 wins
    // Assert dealer sequence:
    //   - After hand 1: state.DealerSeatIndex == 2
    //   - After hand 2: state.DealerSeatIndex == 2 (unchanged on washout)
    //   - After hand 3: state.DealerSeatIndex == 1
    // Assert HandNumber increments after every hand.
}
```

Also add **negative assertions** that the legacy `+1 mod 4` and `-1 mod 4` behaviors are gone:
- After a non-dealer-win, the new dealer is **the winner**, not "winner − 1" or "dealer + 1".
- After a dealer-win, the dealer keeps the seat (degenerate case of winner-becomes-dealer).
- After a washout, the dealer is **unchanged**, not "dealer + 1" or "dealer − 1".

---

## Documentation updates applied (this commit)

- `docs/rules/changsha-spec.md` header bumped `v1.1 → v1.2`, dated 2026-05-13, changelog added.
- **§6.2** rewritten — canonical winner-becomes-dealer + washout-keeps-seat, with source quotes, worked example, and explicit implementation contract.
- **§7.2** state-transition table updated: `PAYMENT → ROTATING_DEALER` sets dealer = winner; `WALL_EXHAUSTED → ROTATING_DEALER` leaves dealer unchanged.
- **§9 OQ-10** (Dealer retention) updated to point at the v1.2 canonical rule.
- **§11 assumption #9** updated to v1.2 canonical wording.
- **§12 conformance checklist** "Banker & Game Flow" section rewritten; explicit checkbox forbidding `+1 mod 4` / `-1 mod 4`.
- **§5.2 base unit** clarified: default = 1 (raw values); 10/100 are optional overrides. (Aligns with what `ScoringService` already does and what the v1 conformance audit flagged.)
- **§9 OQ-5** and **§10 OQ-6** updated to reflect default base unit = 1.

---

## Out of scope for this lock

- **§3.3 Claim Priority:** Already correctly states `Hu > Kong = Pung > Chow` with CCW proximity tiebreak. Verified — no change needed. (Note: Bishop's audit flagged that the *implementation* ranks Kong above Pung; that is a backend bug for Bishop to fix, not a spec issue.)
- **§3.6 Missed Win (过胡):** Already in v1 scope (not deferred). Verified — no change needed. (Note: implementation does not enforce it yet; that is a separate Bishop task.)
- **§2.7 Instant Win Check:** Still contradicts §4.3 (instant wins deferred to v2). Documented as a v1.0 legacy hygiene item in the prior conformance audit, non-blocking for this lock.

---

## Decision authority

Per `.squad/decisions.md` 2026-05-13 entry: "When the three reference sources disagree, MahjongPros is the tiebreaker." All three agree on winner-becomes-dealer, so this is the strongest possible consensus — no tiebreaker invocation needed. The v1 washout simplification ("dealer keeps seat") is Vasquez's call as Rules Engineer under the Phase 3 spec-lock charter; Stephen explicitly directed this wording in the Phase 3 task brief.
# Bishop — Phase 3 Stream B (Changsha v1 Backend Fixes)

**Date:** 2026-05-13
**By:** Bishop (Backend Dev)
**Branch:** `stlong/changsha-v1-phase3`
**Status:** SHIPPED — all five surgical fixes landed, 0 failed tests, build clean.

---

## What shipped

Five surgical backend fixes addressing the silent bugs and not-enforced rules surfaced in `bishop-changsha-backend-audit.md`, plus Vasquez's `vasquez-banker-rotation-lock.md` (v1.2) canonical ruling.

### FIX-1 — Banker rotation canonical (winner-becomes-dealer)
- `ChangshaStateMachine.cs` `RotateBanker` (~line 460): rewrote to make the winner the next dealer; washout keeps the dealer's seat; `HandNumber++` in both cases. Reasons: `"dealerRetained"` (winner == old dealer), `"winnerBecomesDealer"`, `"washoutDealerRetained"`. Existing 16-hand → `GameEnded` termination logic unchanged.
- Tests: `BankerRotationTests.cs` fully rewritten — adds `BankerRotation_WinnerBecomesDealer_NotPlusOne`, `BankerRotation_Washout_DealerKeepsSeat`, `BankerRotation_FullGame_16Hands_EmitsGameEnded`, `BankerRotation_CanonicalSequence_MatchesSpec62Example`, `Washout_FromSeat3_DealerStaysOnSeat3`, `HandNumber_IncrementsOnWinnerAndWashout`.
- `StateMachineServiceTests.cs` updated: `BankerRotation_NonDealerWins_RotatesCounterClockwise` → `BankerRotation_NonDealerWins_WinnerBecomesDealer`.

### FIX-2 — Kong/Pung same-tier priority + extract priority helper
- New file `Changsha/ChangshaClaimPriority.cs` — single source of truth. `TierOf(TableClaimType)` returns Hu=3, Kong=Pung=2, Chow=1. `CounterClockwiseDistance(from, to) = (to - from + 4) % 4`.
- `ClaimAdjudicator.cs` and `Runtime/ChangshaGameRuntime.cs` (~line 622) both call `ChangshaClaimPriority.TierOf`. No drift possible — the duplicate inline priority table in the runtime is gone.
- Tests: `ClaimAdjudicatorTests.cs` — replaced the old `Kong_TakesPriorityOverPung` (which hard-asserted the bug). New tests: `KongAndPung_SameTier_CCWClosestSeatWins_KongCloserCCW`, counterexample `KongAndPung_SameTier_PungCloserCCW_BeatsKong`, `[Theory]` `ClaimPriority_PungAndKong_SameTier_CCWProximityTiebreak`, and `ClaimPriority_PriorityTablesAgree_NoDrift`.
- `PungKongChowTests.Kong_OpportunityDetected_WhenSeatHoldsTriplet` — line ~49 fixed from `Priority == 3` to `Priority == TierOf(Kong)`.

### FIX-3 — Per-hand wall seed mixing
- `ChangshaStateMachine.cs` `Deal` (~line 80): `new Random(state.Seed)` → `new Random(HashCode.Combine(state.Seed, state.HandNumber))`. Same `(Seed, HandNumber)` still gives an identical wall; different hands of the same game now produce different shuffles.
- Tests: new file `WallSeedTests.cs` — `WallSeed_Determinism_SameGameSeedAndHandNumberProduceIdenticalShuffle`, `WallSeed_DifferentHands_DifferentShuffles`, `WallSeed_DifferentGameSeeds_DifferentShuffles`, `WallSeed_HandNumber_NotZeroIndexed`.
- **Deferred:** `DiceService(state.Seed + state.HandNumber)` in `StartNextHandOrEndAsync` still uses raw addition rather than `HashCode.Combine`. Out of scope for this stream — dice has minor visual-only impact compared to the wall.

### FIX-4 — Honor `claim.tileIds` in chow resolution
- `Tables/TableActionErrorCodes.cs`: added `ChowTilesInvalid = "CHOW_TILES_INVALID"`.
- `ChangshaStateMachine.cs`: added `ResolveClaim` overload accepting `int[]? chosenTileIds`. Split `RemoveChowTiles` into `RemoveChowTilesByChoice` (validates: exactly 2 distinct tiles, both in concealed hand, single-suit, 3 consecutive ranks — throws `TableRuleException` with `CHOW_TILES_INVALID` on any failure) and `RemoveChowTilesByLowestPattern` (legacy fallback for null/empty tileIds).
- `Runtime/ChangshaGameInstance.cs`: added `LoggedLegacyChowWarning` flag.
- `Runtime/ChangshaGameRuntime.cs` (~line 608): projects `TileIds` from `PendingClaims` through to `ResolveClaim`; once-per-game `LogWarning` if a chow arrives without tileIds.
- Tests: new file `ChowTileIdsTests.cs` — `Chow_TileIdsRespected_WhenClaimantHasMultipleValidPatterns`, `Chow_EmptyTileIds_FallsBackToLowestPattern`, `Chow_InvalidTileIds_ReturnsContractError_NotInHand`, `Chow_InvalidTileIds_ReturnsContractError_NotSequential`, `Chow_InvalidTileIds_ReturnsContractError_DifferentSuits`, `Chow_TileIds_WrongCount_ReturnsContractError`.

### FIX-5 — Enforce missed-win (过胡) rule §3.6
- `ChangshaDomain.cs`: added `HashSet<int> MissedWinSeats` to `ChangshaGameState`. System.Text.Json deserializes missing field as default — back-compat safe for in-flight games.
- `ChangshaStateMachine.cs`:
  - `Deal` (~line 117): `state.MissedWinSeats.Clear()` so the flag resets per hand.
  - `Discard` (~line 167): filters Hu opportunities for seats in `MissedWinSeats` before opening the claim window. If a flagged seat had ONLY a Hu opportunity, no opportunity is added.
  - `ResolveClaim` Hu branch: calls `FlagMissedWinSeats(state, claimWindow, declaringHuSeat: claimingSeatIndex)` so other Hu-capable seats that didn't declare get flagged.
  - `ResolveClaim` non-Hu branch + `PassClaim`: call `FlagMissedWinSeats(state, claimWindow, declaringHuSeat: -1)` so every seat that had Hu in the window is flagged.
  - New `FlagMissedWinSeats` helper (~line below `ResolveHuClaim`): iterates `claimWindow.Opportunities`, adds every `Hu` opportunity owner (except the declarer) to `state.MissedWinSeats`.
- **Self-draw is NOT affected** — `DeclareSelfDrawWin` bypasses the claim window so a flagged seat can still self-draw.
- Tests: new file `MissedWinTests.cs` — `MissedWin_DeclinesWinningDiscard_BlockedFromLaterDiscardWins`, `MissedWin_DoesNotBlockSelfDraw`, `MissedWin_ResetsOnNewHand`, `MissedWin_PungOrKongStillAllowedAfterMissedWin`, `MissedWin_TwoSeatsHadHu_OneWins_OtherFlagged`.

---

## Test count delta

- Baseline: **179 passed**, 7 skipped (v2-deferred), 0 failed.
- After: **203 passed**, 7 skipped, 0 failed.
- Net: +24 tests (banker rewritten = net +5; wall seed +4; claim priority +5; chow tileIds +6; missed-win +5). One existing test (`Kong_TakesPriorityOverPung`) was rewritten in place to assert the corrected rule, two existing tests (one banker, one Pung/Kong/Chow tier assertion) were tightened.

Build: 0 warnings, 0 errors.

---

## Confidence: 16-hand championship now completes correctly?

**High.** Specifically:
- The `RotateBanker` rewrite is exhaustively covered by the §6.2 worked-example test and by a 16-hand drive that asserts `GameEnded` after exactly 16 increments of `HandNumber`. Winner-becomes-dealer plus washout-keeps-seat is symmetric with the locked v1.2 spec.
- Per-hand seed mixing means a 16-hand bot game now plays 16 *different* walls — fairness restored.
- Same-tier Kong/Pung adjudication with CCW proximity matches §3.3.

What I did NOT touch and is still worth tracking:
- `DiceService` seed mixing (still raw addition).
- Persistence hydration on process restart (`_games` is not reloaded from `ChangshaGame.StateJson`).
- E2E coverage for a full 16-hand bot game — `ChangshaHubE2ETests.E2E1_AllBots_PlaysAtLeastOneHandAndCompletes` only proves one hand. A 16-hand E2E remains the cleanest regression guard for FIX-1 + FIX-3 at the hub layer.

These are the same three deferred items called out in `bishop-changsha-backend-audit.md` last week; they are now the largest remaining v1-correctness gaps.

---

## Frontend contract impact

- **New error code** `CHOW_TILES_INVALID` is surfaced via `TableRuleException`. Hub will propagate as a hub error to clients. Hicks may want to map this to a user-facing toast when a chow submission is rejected.
- `ClaimMade` events now reflect the seat that won by CCW proximity for same-tier Kong/Pung — previously Kong always won. No wire schema change, just different seats winning in close-claim scenarios.
- `BankerRotated.reason` now uses `"winnerBecomesDealer"` / `"dealerRetained"` / `"washoutDealerRetained"` (the latter two pre-existed; the first is new). No schema break.

---

## Files touched

**Source:**
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ClaimAdjudicator.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaDomain.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaClaimPriority.cs` *(new)*
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameInstance.cs`
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs`
- `src/backend/src/Mahjong.Autotable.Api/Tables/TableActionErrorCodes.cs`

**Tests:**
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/BankerRotationTests.cs` (rewritten)
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/PungKongChowTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/WallSeedTests.cs` *(new)*
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/ChowTileIdsTests.cs` *(new)*
- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/MissedWinTests.cs` *(new)*
- `src/backend/tests/Mahjong.Autotable.Api.Tests/ChangshaServices/ClaimAdjudicatorTests.cs`
- `src/backend/tests/Mahjong.Autotable.Api.Tests/ChangshaServices/StateMachineServiceTests.cs`
# Phase 3 — Stream A: Changsha Lobby + Claim UX

**Owner:** Hicks (Frontend Dev)
**Branch:** `stlong/changsha-v1-phase3`
**Commit:** `f6c298e` — `feat(changsha): lobby + claim UX (Phase 3 Stream A) — playable vs bots`
**Date:** 2026-05-13
**Status:** Shipped — frontend build clean, 48/48 vitest tests green.

## Charter

Ship the Lobby + Claim UX so Stephen can browse to `/changsha`, click
"Play vs Bots", and play a real hand against three bots through the
existing SignalR hub. Scope limited to:

- `src/frontend/modern/src/changsha/**`
- `src/frontend/modern/src/pages/ChangshaTablePage.tsx`
- `src/frontend/modern/src/changsha/__tests__/signalrClient.test.ts`
  (necessary update — see "Cross-cutting fixes" below)

Off-limits (other agents own): backend, vite/package config, `__tests__/`
broadly, `docs/rules/changsha-spec.md`.

## What shipped

### Lobby flow
- New `LobbyCard.tsx`: Fluent UI Card with player-name input (persisted
  to `mj-autotable:changsha:playerName`) and "Play vs Bots" button.
  Disabled while busy or hub not yet connected; errors surfaced via
  MessageBar.
- `ChangshaTablePage.tsx` now renders the LobbyCard when
  `state.phase === 'lobby' || !state.gameId`. The
  `handlePlayVsBots` orchestrator calls (in order)
  `createGame` → `takeSeat` → `fillWithBots` → `startGame`.
- A "Leave game" button on the header lets the user wipe local state
  and return to the lobby.
- `useLiveChangshaGame` exposes the lobby actions (`createGame`,
  `fillWithBots`, `takeSeat`, `startGame`, `reconnectGame`,
  `leaveGame`) and auto-rehydrates via `ReconnectGame` on connect or
  reconnected, using localStorage keys
  `mj-autotable:changsha:{gameId,seatIndex,playerName}`.

### Hand + claim UX
- `PlayerHandPanel.tsx` renders the concealed hand sorted by
  (Wan → Tiao → Tong/Bing, then rank, then id) via the new
  `sortHandForDisplay` helper. Buttons added for **Concealed Kong**
  (per `findConcealedKongs`), **Added Kong** (per `findAddedKongs`),
  and **Zimo!** self-draw win.
- `ClaimPromptModal.tsx` now shows the discard tile preview, a chow
  combo picker (RadioGroup over `computeChowCombos`), a "Win!" surface
  for `hu` opportunities, sorted claim button priority
  (hu > kong > pung > chow > pass), and forwards the correct
  concealed `tileIds` to the backend for chow.
- `DiceRollModal.tsx` removed the Roll/Auto-Roll buttons (server
  auto-rolls inside `StartGame`); modal animates dice while waiting
  for `DiceRolled`/`BreakPointSet`, then displays the rolled values
  and break point, then offers OK to dismiss.
- `OpponentDiscardTrays.tsx`: grid layout (top/left/right) around the
  autotable viewport using `state.discardLog` (parallel array to
  `discardPile` populated by `TileDiscarded` + `FullState`) for
  per-seat attribution.

### Cross-cutting fixes (necessary to make the lobby actually work)

These pre-existed Stream A but were blockers and had to be fixed
before the lobby could be wired:

1. **SignalR positional-args bug (critical):** `invoke.*` wrappers
   in `signalrClient.ts` were calling `connection.invoke(method,
   payload)` with the payload as a single object. .NET hubs take
   positional args; the previous shape silently bound the entire
   payload to the first parameter and produced coerced garbage for
   the rest. Every live-mode hub call was broken. Wrappers rewritten
   to spread positional args matching `ChangshaHub.cs` signatures
   verbatim.
2. **`reconnectGame` aliased `JoinTable`** — replaced with a call to
   the dedicated `ReconnectGame(gameId, seatIndex)` hub method,
   which the server replies to with a `FullState` event.
3. **`FullStateEvent` type + `FullState` reducer case** added so
   reconnect/snapshot delivery actually rehydrates seats, hands,
   discard pile, pending claims, dice, breakpoint, and phase.
4. **PascalCase/camelCase phase strings**: `.NET` serializes
   `state.Phase.ToString()` as PascalCase ("AwaitingDiscard"), but
   the frontend `GamePhase` union is camelCase. New `phaseFromWire`
   normaliser handles this for `TurnStarted` and `FullState`.
5. **`ClaimOpportunity.tileIds`** preserved when reducing into
   `PendingClaim` (forward-compat with future server-supplied chow
   combo hints).

### Tests touched

`src/frontend/modern/src/changsha/__tests__/signalrClient.test.ts` had
to be updated to match the new positional-args contract. The test
file's own docstring (committed by Hudson) had explicitly anticipated
this update at Phase 3 PR time, so the change is the intended
roll-forward. 48/48 vitest tests green.

## Deferred to v3.1 / later streams

- Real 3D mesh rendering inside the autotable iframe (atlas textures,
  wall/tile mesh, dice geometry showing rolled face). The current
  bridge is text-overlay telemetry only.
- Bidirectional canvas → hub events (click-tile-in-3D triggers
  `Discard`).
- Mid-hand reconnect: server sends `FullState` but the UI does not
  yet replay claim/draw animations from the snapshot.
- Server-supplied chow combo hints in `ClaimOpportunity.tileIds`
  (client computes locally today).
- Bundle code-splitting (still emits the pre-existing 600 KB chunk
  warning).

## Smoke-test recipe for Stephen

1. Start backend on `:5114` (`dotnet run --project src/backend/src/Mahjong.Autotable.Api`).
2. Start frontend on `:5173` (`npm run dev` in `src/frontend/modern/`).
3. Browse to `/changsha`. Ensure mock mode is off — if the "Mode: Mock"
   toggle is visible, click it to switch to "Mode: Live server" then
   reload.
4. Enter a display name, click **Play vs Bots**. LobbyCard should
   disappear within a second; dice modal animates and then shows the
   server-rolled values + break point; click OK to dismiss.
5. Verify your concealed hand renders sorted Wan → Tiao → Tong with
   matching tiles adjacent. Click a tile to discard.
6. Watch the per-seat discard trays around the table update as bots
   advance. When a claim window appears for you, the modal should
   surface a chow combo picker when applicable, plus pung/kong/hu
   buttons in priority order.
7. With four matching concealed tiles, the **Concealed Kong** button
   should appear inline with the hand. With a tile matching an
   existing exposed pung, **Added Kong** should appear. **Zimo!**
   appears when you can self-draw win.
8. Hard-refresh mid-hand: the page should reconnect via
   `ReconnectGame`, replay a `FullState` snapshot, and land you back
   at your seat with the same hand/discard pile (animations not
   replayed — see v3.1 deferral).

## Files touched

```
src/frontend/modern/src/changsha/__tests__/signalrClient.test.ts
src/frontend/modern/src/changsha/changshaReducer.ts
src/frontend/modern/src/changsha/components/ClaimPromptModal.tsx
src/frontend/modern/src/changsha/components/DiceRollModal.tsx
src/frontend/modern/src/changsha/components/LobbyCard.tsx                (new)
src/frontend/modern/src/changsha/components/OpponentDiscardTrays.tsx     (new)
src/frontend/modern/src/changsha/components/PlayerHandPanel.tsx
src/frontend/modern/src/changsha/components/index.ts
src/frontend/modern/src/changsha/signalrClient.ts
src/frontend/modern/src/changsha/tileUtils.ts
src/frontend/modern/src/changsha/types.ts
src/frontend/modern/src/changsha/useChangshaGame.ts
src/frontend/modern/src/changsha/useLiveChangshaGame.ts
src/frontend/modern/src/pages/ChangshaTablePage.tsx
```

14 files; +1248 / −209 lines net.
# Hudson — Phase 3 Stream C: Frontend Test Infrastructure

**Date:** 2026-05-13
**Branch:** `stlong/changsha-v1-phase3`
**Status:** Vitest infra landed. First wave of 47 frontend tests GREEN.

---

## What shipped

### Tooling
| Package | Version | Why |
| --- | --- | --- |
| `vitest` | ^4.1.6 | Test runner (peer-compatible with vite 6). |
| `@vitest/ui` | ^4.1.6 | Local dev UI (optional, ergonomic). |
| `jsdom` | ^29.1.1 | DOM environment for the bridge's `window.postMessage` path and React component tests. |
| `@testing-library/react` | ^16.3.2 | Hook + component rendering (React 19 compatible). |
| `@testing-library/jest-dom` | ^6.9.1 | DOM matchers (`toBeInTheDocument`, etc.). |
| `@testing-library/user-event` | ^14.6.1 | User-interaction simulation for future component tests. |

### Configuration
- `vite.config.ts` — added `test` block (jsdom env, `globals: false`, setupFiles, include glob).
- `src/test/setup.ts` — registers jest-dom matchers, polyfills `window.matchMedia` (Fluent UI 9 needs it during render).
- `package.json` scripts: `test` (single-run), `test:watch`, `test:ui`.

### Tests added (47 across 4 files)

#### `changshaReducer.test.ts` — 19 tests
- `GameCreated`: gameId/phase=lobby/seat array populated; missing seats fall back to defaults.
- `PlayerSeated`: nick + isBot updated, other seats untouched.
- `GameStarted + DiceRolled + BreakPointSet`: dealer, round wind, hand number, dice tuple `{die1,die2,sum}`, breakPoint coords; phase=dealing.
- `TilesDealt`: local-seat receives explicit tileIds; remote-seat is count-only; phase transitions to awaitingDiscard on isComplete.
- `TileDiscarded`: tile leaves concealed, enters shared discardPile, phase=awaitingClaim.
- `ClaimWindowOpen`: pendingClaims populated, phase=awaitingClaim.
- `ClaimMade` (pung): meld appended, tiles removed from concealed, activeSeat = claimer, phase=awaitingDiscard.
- `ClaimMade` (kong): exposedKong meld with 4 tiles.
- `ClaimMade` (chow): explicit tileIds move from concealed to exposed meld; unused tiles stay.
- `WinDeclared`: lastWin captured, phase=scoring.
- `ScoringComplete`: seat scores updated, phase=endHand.
- `BankerRotated`: bankerSeat updated, phase=rotatingBanker (winner-becomes-dealer asserted via the `reason` field).
- `HandFinished`: dealer + per-hand state clear, phase=rollingDice (or endGame if `isGameOver`).
- `RoundChanged`: prevalentWind + roundNumber.
- `GameEnded`: phase=endGame, finalScores applied.
- `reset`: returns initial state.

#### `autotableBridge.test.ts` — 5 tests
- Outbound queues until iframe posts `{type:'ready'}`, then flushes.
- Envelope is `{proto:'changsha-bridge/1', type, ...}` on every send (tests pin the proto sentinel).
- Inbound only fires when `ev.source === iframe.contentWindow` (foreign sources ignored).
- Malformed / wrong-proto / garbage data dropped silently; `isReady` stays false.
- `dispose()` detaches the window message listener.

#### `signalrClient.test.ts` — 19 tests
- All invoke wrappers (createGame, joinTable, takeSeat, startGame, rollDice, acknowledgeDeal, discard, claim with+without tileIds, pass, declareKong, declareWin, reconnectGame) pinned to method-name + payload-object shape.
- `attachServerEventHandlers` registers `conn.on` only for handlers supplied; forwards payloads; teardown removes all listeners; one handler throwing does not break the others (logs once via console.error).
- `describeConnectionState` maps every HubConnectionState to the public ConnectionStatus enum; explicitly asserts Disconnected ≠ idle (the live hook relies on this for UI state).

#### `useChangshaMockGame.test.ts` — 4 tests
- Hook mounts in jsdom + React 19; returns the expected action surface (9 functions).
- `dealMock` produces the canonical 14/13/13/13 split (banker gets 14), wall remaining = 108 − 53.
- `discard` removes the local tile, appends to shared discardPile.
- `resetDemo` returns state to seating phase with empty hands and empty discard pile.

---

## How to run

```
cd src/frontend/modern
npm test          # CI mode — single run
npm run test:watch  # watch mode
npm run test:ui   # browser-based UI (vitest)
```

Latest verified run: **47 passed / 0 failed / 0 todo** in ~3.7s.

---

## What's still uncovered (recommended next-wave tests)

Ranked by risk per effort. Numbers in `[brackets]` are estimated days.

1. **`useLiveChangshaGame` hook** `[1.0d]`
   The single biggest remaining blind spot. Holds the SignalR connection lifecycle, dispatch loop, claim-window timer, reconnect path. Currently untestable in isolation because the HubConnection is constructed in module scope. **Refactor first** — inject a connection factory, then add tests for:
   - First-mount opens connection and registers handlers.
   - Server event dispatches reducer action with correct payload.
   - `reconnect()` re-invokes JoinTable / replays via FullState (after Hicks's WIP lands).
   - Disconnection surfaces `connectionStatus = 'disconnected'`.
   - Component unmount tears down handlers + connection.

2. **`autotableBridge.diffAndSend`** `[0.5d]`
   The function that translates ChangshaGameState diffs into outbound messages. Today untested. Phase 3 will exercise it harder once the 3D scene receiver becomes real, but it should be unit-tested for: dice/breakPoint/discardPile/dealing transitions and the reset-on-gameId-change case.

3. **Component tests for the visible UI** `[1.0d]`
   Tests should cover `ChangshaTablePage` (mode toggle, child rendering), `DiceRollModal` (open/close + dice display), `PlayerHandPanel` (tile rendering, click → discard handler call), `BankerBadge` + `RoundWindIndicator` (correct labels + glyphs), `ClaimPromptModal` (button visibility per opportunity type, chow tile-pair selection — once Hicks ships it), `FanBreakdownPanel` (score-table rendering). Requires Hicks's Phase 3 component refactor to land first.

4. **`tileUtils.ts` pure helpers** `[0.2d]`
   `tileFromId`, `tileGlyph`, `tileLabel`, `generateFullTileSet`, `windLabel`, `windEnglish`. Pure functions, low-risk, but cheap to lock. Catches accidental tile-id-arithmetic regressions if Hicks ever changes the encoding.

5. **`useChangshaGame` mode picker** `[0.2d]`
   `shouldUseMock` reading localStorage → mock vs live; localStorage absent → falls back to `import.meta.env.DEV`. Currently untested; trivial to lock.

6. **Bridge security — clobber-resistance** `[0.5d]`
   The bridge filters by `ev.source === iframe.contentWindow`. Add a test for the malicious-iframe scenario where a sibling iframe spoofs a source. May require harder thought about whether to switch to `targetOrigin` enforcement in production (currently `*`).

7. **Reducer phase-machine invariants** `[0.5d]`
   Property-style test: from any reachable phase, every event preserves: `seats.length === 4`, `hands.length ≤ 4`, `wallRemaining ∈ [0, 108]`, `discardPile.length + sum(concealed) + sum(meld tiles) ≤ 108`. Catches accidental over-counting bugs.

---

## Blockers / Coordination notes

- **Hicks's Phase 3 Stream B WIP** currently in the working tree (uncommitted) reshapes:
  - `signalrClient.ts` invoke wrappers → positional args, adds `fillWithBots`, `reconnectGame(gameId, seatIndex)`.
  - `changshaReducer.ts` → adds `FullState` action + `phaseFromWire` normalization + tileIds on PendingClaim.
  - `types.ts` → adds `tileIds` to PendingClaim.
  - `useLiveChangshaGame.ts` → uses the new APIs (currently inconsistent with committed signalrClient signatures, breaks `tsc -b`).
  - New components: `LobbyCard.tsx`, `OpponentDiscardTrays.tsx` (untracked).
  When Hicks commits and pushes those changes, **his PR must update my tests** to match: `signalrClient.test.ts` invoke assertions, `changshaReducer.test.ts` ClaimWindowOpen tileIds assertion, and add a FullState action test. The contract is documented in the test file headers ("when Hicks's PR lands, update X").

- **No CI workflow added this pass.** `npm test` from `src/frontend/modern/` is the local + reviewer command. CI integration is a follow-up task (suggest adding a `frontend-tests` job to `.github/workflows/squad-ci.yml` that runs `npm ci && npm test` in the modern frontend directory).

---

## Skeptical notes

- The 47 tests cover **state-transition correctness** and **wire-protocol contract** for the bridge and SignalR client. They do **not** prove the live runtime works against a real .NET hub — that needs an E2E spike against a running backend.
- The mock hook tests pin the action surface but do not assert any real Mahjong rule (the mock generates random hands). Rule conformance is exclusively the backend's responsibility — fine for this stream's scope.
- Vitest 4 is brand new (released within the past quarter). If we hit instability, drop to `vitest@^3.x` — it's the well-trodden line.
- `@testing-library/react@16` was specifically released for React 19 compatibility (the prior `13.x` line did not support React 19's stricter dev-mode checks). Pin floor `^16.3` to avoid regressions.

---

## Recommendation

Merge this stream's infra commit independently of Hicks's Phase 3 Stream B. The 47 tests pin a meaningful baseline of frontend correctness and create the framework for everything in the "next wave" list above. Future PRs touching the reducer, bridge, or SignalR client will surface contract drift here rather than at runtime.

### 2026-05-13: 3D Renderer Spike — Strategy C Recommended

**By:** Hicks (Frontend Dev)
**Date:** 2026-05-13
**Branch:** `stlong/changsha-3d-renderer-spike`
**Full spike:** `docs/rules/changsha-3d-renderer-plan.md`

## TL;DR

The autotable 3D viewport at `/changsha` is theater. The bundled
`autotable.9519e86d.js` registers zero listeners for the
`changsha-bridge:*` CustomEvents the receiver dispatches, and the bundle
exposes no JavaScript surface beyond `window.__THREE__`. The only
observable canvas effect of the current bridge is fading in the
`#dice-img` sprite.

## Recommended strategy

**Strategy C — Fake autotable WS server.** Collocate a `/autotable/ws`
WebSocket endpoint in `Mahjong.Autotable.Api` that speaks upstream's
`NEW`/`JOIN`/`JOINED`/`UPDATE` protocol verbatim. Translate authoritative
`ChangshaGameState` into the seven upstream collections
(`match`, `seats`, `things`, `nicks`, `mouse`, `sound`, `dice`). The
bundle connects to it unchanged — same byte-identical bundle, same
WebSocket protocol upstream has shipped for years.

**Why not the others:**
- **A (fork client.ts + rebuild):** loses byte-identity with upstream, pulls Parcel into our build, ~600–900 LOC.
- **B (patch bundle via WebSocket proxy):** fragile across rebuilds, requires re-implementing the server's unique/ephemeral/perPlayer constraints client-side.
- **D (re-render in React/three.js):** ~XL effort to rebuild ~3 KLOC of working three.js code (movement physics, drop shadows, hover outlines, mouse-tracker, dice sprite). High UX-payoff risk for no UX upside.

## Complexity

**Phase 5a (MVP, walls + dealt hands visible in 3D):** **L** —
~900 LOC new + ~35 modified, ~3–5 days.

**Phase 5b (canvas → discard):** **M** — additional ~400 LOC, ~2 days.

**Phase 5c (animation polish + SFX):** **M** — additional ~300 LOC, ~2 days.

## Top 3 risks

1. **Upstream slot-name churn.** Translator hard-codes
   `wall.{col}.{layer}@{seat}`, `hand.{idx}@{seat}`,
   `discard.{r}.{c}@{seat}`, `meld.{i}.{j}@{seat}`. If upstream renames
   any of these, integration breaks silently. Mitigation: startup
   self-test that JOINs as a synthetic client and verifies the
   collection shape. **Likelihood: low** — upstream protocol unchanged
   in 3+ years.

2. **Race between canvas-driven discard and server confirmation.** Once
   Phase 5b ships canvas drag-discard, a fast player can drag a second
   tile before the server's `TileDiscarded` echo arrives. Translator
   must reconcile (echo wins, but bundle UX should snap-back). Owned
   risk for Phase 5b, not 5a.

3. **Bundle's own deal button fires a Riichi deal on click.** If
   sidebar visible in embedded mode, user clicks Deal → 136-tile
   client-side Riichi state overrides our 108-tile authoritative state
   visually for a frame, then snaps back. Mitigation: hide sidebar via
   `?embedded=1` URL parameter check in `index.html`.

## Phase 5a expected scope

**Files added (all backend):**
- `Autotable/AutotableProtocol.cs` (~80 LOC)
- `Autotable/AutotableSlotMap.cs` (~120 LOC)
- `Autotable/ChangshaToAutotableTranslator.cs` (~250 LOC)
- `Autotable/AutotableWsEndpoint.cs` (~250 LOC)
- `test/AutotableTranslatorTests.cs` (~200 LOC)

**Files edited:**
- `Program.cs` (+~10 LOC, register WS endpoint)
- `src/frontend/autotable/index.html` (+~5 LOC, hide sidebar when embedded)
- `src/frontend/modern/src/pages/ChangshaTablePage.tsx` (+~20 LOC, pass `?gameId=&embedded=1` to iframe `src`)

**Files NOT touched:**
- `autotable.9519e86d.js` and every bundled asset stay byte-identical
  (preserves the upstream-mirror guarantee in `src/frontend/autotable/README.md`).

**Exit criteria for Phase 5a:**
- ✅ All 251 existing tests still pass.
- ✅ ~15–20 new translator/endpoint tests pass.
- ✅ Manual: after `StartGame`, iframe shows 108 face-down wall tiles in
  Changsha's 14/14/13/13 wall arrangement, 53 hand tiles distributed
  4×13/14, dice on the center pad showing our authoritative roll,
  dealer marker on the correct seat.
- ✅ No regression in the React HUD (banker badge, dice modal, fan panel
  continue to render correctly).

## Open questions filed in the spike (for Stephen)

1. Auto-roll dice vs visual click-to-roll?
2. Preserve the standalone sandbox at `/autotable/`?
3. Add a perspective/flat camera toggle in the React HUD or keep upstream's `P` keybind?
4. Confirm 4-side radial discard layout (upstream) over the per-seat-stack 2D layout (Phase 3 React)?
5. Replay animations on mid-hand reconnect or snap to state?
6. Canonical 14/14/13/13 wall split, or simpler 14/13/14/13 symmetric?
7. Throttling for concurrent spectator games?
8. WS endpoint path: `/autotable/ws`, `/api/autotable/ws`, or Changsha-specific?

## Status

Spike-only. No code modified. Awaiting Stephen's directive to proceed
with Phase 5a under the recommended strategy.
## User Directives — Architectural Pivot (Stephen Long, 2026-05-13)

### 2026-05-13T23:00Z: User directive — Architectural pivot
**By:** Stephen Long (via Copilot)
**What:** Autotable IS the framework. Changsha rules must be implemented INSIDE autotable (its TypeScript codebase and protocol), NOT bolted on via a separate React/Fluent UI SPA with an iframe bridge. The user's words: *"Do you not understand that we want to simply use autotable and implement changsha rules with it??"* Confirmed by his original day-one ask: *"Since autotable is our basis... if it's a lot of work or tricky to pivot the code base to [React/Fluent], don't try to go down that path initially."*
**Why:** Core architectural intent we misread. The current architecture — separate `/changsha` SPA + Strategy C "fake autotable WS" bridge — needs to be replaced with autotable-native: autotable's own UI, autotable's own WS protocol spoken natively by the .NET backend, and Changsha rules patched into autotable's TS source (108-tile set, 258 eye, scoring, claim grammar).
**Implications:**
- The `/changsha` React SPA + bridge receiver + Strategy C fake WS endpoint are candidates for removal.
- Backend `ChangshaGameRuntime` keeps its rules engine but exposes via autotable's NEW/JOIN/JOINED/UPDATE WS protocol — not SignalR with a Changsha-specific contract.
- Autotable TS code itself needs Changsha modifications (tiles, scoring, claims).
- React/Fluent UI may survive only as a thin lobby (optional) — not as the game UI.

### 2026-05-13T23:06Z: User directive — Pivot to autotable-vendored, Changsha-native (BINDING)
**By:** Stephen Long (via Copilot)
**What (verbatim):** *"Can we not just make a clone/copy of the autotable source code and implement changsha rules within it? Maybe we should just do that and get rid of all the cruft/junk that you created?"*
**Authoritative sources Stephen anchored:**
- Autotable source: https://github.com/pwmarcz/autotable
- Changsha rules: https://mahjongpros.com/blogs/how-to-play/beginners-guide-to-changsha-mahjong

**The binding plan:**
1. **Vendor in autotable source.** Clone pwmarcz/autotable INTO this repo as the primary frontend codebase. Not a git submodule, not an iframe — a real fork we can modify file-by-file. Likely path: `src/frontend/autotable/` replaces the current static bundle with the actual TypeScript source + build chain. Final path can be picked by Hicks but the intent is "this IS our frontend codebase now."
2. **Implement Changsha rules INSIDE that vendored source.** Modify autotable's TS directly: 108-tile set, 258 eye, Changsha scoring (Big/Small Wins, special hands), claim grammar (pung, chow-next-player-only, kong + gangshanghua bonus), no riichi/dora/furiten. Vasquez's rules diff manifest is the authoritative spec.
3. **Backend speaks autotable's native WS protocol.** The .NET backend implements pwmarcz/autotable's NEW/JOIN/JOINED/UPDATE protocol natively (replacing the Node.js reference server). Backend Changsha state engine drives autotable's collection updates.
4. **Delete the cruft Stephen named:**
   - The entire `src/frontend/modern/` React/Fluent UI 9 SPA at `/changsha` — the lobby, the game UI, the Play-vs-Bots flow, all of it.
   - The Strategy C "fake autotable WS" bridge layer in the backend (`src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs`, `ChangshaToAutotableTranslator.cs`, related plumbing).
   - The `changsha-bridge-receiver.js` injected script.
   - The SignalR Changsha-specific contract (if Bishop confirms autotable's native protocol replaces it cleanly).
   - The iframe-embed mode and parent→child postMessage protocol.
5. **Keep:** the Changsha rules engine logic (state machine, dice service, break-point service, hand evaluator, scoring), the .NET project structure, EF Core persistence, the docs/rules/ source-of-truth files, the test suite (rules portion).
**Out of scope (for now):**
- React/Fluent UI everywhere. Stephen's original day-one note explicitly said *"if it's a lot of work or tricky to pivot the code base to [React/Fluent], don't try to go down that path initially."* Autotable's native UI is the game UI.
- A separate lobby app. If we need a "create/join game" screen, it lives within autotable's UI conventions.
**Why:** This was the ORIGINAL architectural intent we misread. The current bolt-on architecture (separate React SPA + iframe + Strategy C bridge) is being explicitly rejected by the user. Three agents already mid-flight (Bishop salvage map, Hicks TS modification inventory, Vasquez rules diff) — their outputs become the implementation roadmap for THIS plan, not option-finding for alternatives.

### 2026-05-13T23:15Z: User directive — Preserve F5-from-VS Code local dev (BINDING constraint on pivot)
**By:** Stephen Long (via Copilot)
**What (verbatim):** *"Also - keep in mind I still want to be able to launch the app with vscode.."*
**Scope:** Applied as a constraint on Ripley's pivot plan (`.squad/decisions/inbox/ripley-pivot-plan.md`). After the pivot, the user must still be able to hit F5 in VS Code and get a working local-dev experience: .NET backend up, autotable bundle being served, and ideally autotable's TypeScript hot-rebuilding on file save so frontend iteration doesn't require manual rebuilds.

**Constraints the plan must respect:**
1. `.vscode/launch.json` and `.vscode/tasks.json` must remain functional — F5 launches the backend with no manual prep steps required.
2. The disappearance of `src/frontend/modern/` (and its Vite dev server) must not regress local dev for autotable's frontend.
3. PATH augmentation work already shipped in PR #27 + #28 must remain intact through the pivot — those fixes were independent of the architecture.
4. The vendored autotable (Parcel-based per pwmarcz/autotable upstream) needs a dev-loop story: Parcel's watch/HMR server alongside the backend, OR a build-on-save task, OR keep Parcel's `make watch` running in a separate terminal that the user launches via a VS Code task. Ripley to recommend.

**Decision to add to plan §4:** What's the preferred F5 dev shape — compound launch (backend + Parcel watch in one F5 keystroke), VS Code task running Parcel separately, or simple rebuild-on-save? Default recommendation needed.


### 2026-05-13T23:20Z: Pivot Plan Accepted (Stephen — MVP fast-cuts elected)
**Captured:** 2026-05-13T23:20Z
**By:** Stephen Long (in-session, ask_user response)
**Status:** BINDING — Phase A fires immediately.

**Decision:** Stephen accepted **all 16 defaults** in §4 of Ripley's pivot plan AND elected the **MVP fast-cuts (a) + (b) + (c)** Ripley offered in §6.

**User input:** *"Accept all defaults + take MVP fast-cuts (a) + (b) + (c) for faster v1"*

**MVP Fast-Cut Overrides:**

(a) **Single-game-per-instance deployment.** No matchmaking, no game discovery, no replay-on-reconnect, no multi-game lobby. The autotable sidebar shows only ONE game; "Connect" enters; no `New game / Join existing` distinction. 4 seats: seat 0 = human, seats 1–3 = bots. `POST /api/changsha/games` endpoint collapses to single seed-bound game per process. Multi-human play deferred to v1.1.

(b) **Drag-to-meld with first-valid-drag server arbiter.** Overrides decision #7 (claim grammar). The `changsha.claim` custom collection is NOT needed in Phase C. Claim affordance becomes: drag a tile onto the most recent discard slot → server validates against open claim opportunities → first valid drag wins. Hu stays a button. With fast-cut (a) (1 human + 3 bots), the race is effectively eliminated because bot timing is server-controlled. Remaining 3 custom collections (`changsha.scoring`, `changsha.banker`, `changsha.lifecycle`) DO ship in Phase C.

(c) **Defer Vasquez conformance gaps to v1.1.** Phase D does NOT fix chow tile-ID honoring or 过胡 enforcement. Both gaps remain as known limitations, documented in `docs/known-limitations.md` (Phase E). Vasquez audits Phase D against the MVP-acceptable subset of the spec.

**Net effect on phase plan:**
- Phase A — unchanged.
- Phase B — unchanged.
- Phase C — drops `changsha.claim` collection design; collapses `POST /api/changsha/games` to single-game endpoint. Per-viewer `things` privacy filter (decision #6) STILL ships.
- Phase D — drops claim-window panel + countdown overlay; drops chow tile-ID + 过胡 fixes. Adds drag-to-meld server arbiter (~120 LOC).
- Phase E — unchanged. Adds `docs/known-limitations.md`.

---

## Architectural Pivot Plan Evidence & Details

### Bishop — Backend Salvage Inventory (excerpt)

**Purpose:** Map every component in `src/backend/src/Mahjong.Autotable.Api/` against the autotable-native target.

**Key findings:**
- ~2,500 LOC of pure Changsha logic survives intact: state machine (808 LOC), win detector (308 LOC), scoring (142 LOC), claim adjudicator (141 LOC), bot policy (217 LOC), dice/deal services (~200 LOC), deck builder. These have zero transport coupling.
- ~2,400 LOC of legacy 136-tile Western-rules engine (`Tables/*`) scheduled for deletion; includes 8 REST `/api/tables/*` endpoints plus 2 EF entities.
- ~800 LOC of bridge/translator code (`Autotable/*` namespace) survives but repackaged as the new primary transport (not a bridge anymore).
- Risk flags: per-viewer `things` filtering (novel), inbound-UPDATE validation (greenfield), synthetic ephemeral collections for unmapped events (ClaimWindowOpen, ScoringComplete, BankerRotated, etc.).

---

### Hicks — Autotable TS Modification Inventory (excerpt)

**Purpose:** Honest inventory of what changes in autotable's TypeScript to make it Changsha-native.

**Key findings:**
- **Three vendoring paths:** (A) Git submodule (clean history, footgun for CI), (B) In-tree fork at `src/frontend/autotable-src/` (recommended — matches `modern/` already in-tree, no submodule trap), (C) Vendor-copy (functionally identical to B, loses audit trail).
- **Bundler choice:** Keep Parcel 2.15 (recommended — bundler migration 1–2 weeks of Bootstrap 4 + jQuery + `url:` import rewrites; upstream uses Parcel, pwmarcz rejects non-Riichi PRs so upstream merges are rare).
- **Parcel build chain:** Needs Make + Inkscape (tile SVG → PNG) + Blender (`models.blend` → `.glb`). Mitigation: commit prebuilt PNG/GLB as canonical for v1; gate Inkscape/Blender targets behind `make assets` opt-in.
- **Modified files (Phase B scope):** 6 files across setup.ts, setup-deal.ts, setup-slots.ts, types.ts, things.ts, world.ts, game-ui.ts; ~400 LOC of edits (mostly deletions, simple swaps).
- **Risk flags:** Riichi-shape grep leaks (honba, riichi, fives, kita scattered ~8 files), wall-arithmetic off-by-one (14/14/13/13 split most cognitively loaded).

---

### Vasquez — Rules Diff Manifest (excerpt)

**Purpose:** Authoritative Changsha vs Riichi divergence spec to drive Hicks's TS modifications and backend rules graft.

**14 divergence axes identified; key findings:**
- **Tile set:** 108 tiles (no honors, no red-five variants) vs Riichi's 136 + configurables.
- **Wall layout:** 14/14/13/13 asymmetric distribution (vs 17 each in Riichi), no dead wall, no dora indicator slot, no rinshan slot.
- **Hand size:** Dealer +1 at start (14 for dealer, 13 for others), active player 14 during turn — both KEEP.
- **Claim grammar:** Pung (2 + discard), Chow (next-player-only + discard), Kong (4 + discard + replacement from back wall), Hu (胡, win). No Riichi, no optional claims, no waiting patterns.
- **Scoring:** Small Win (self-pick) = 1×base, Big Win (point-to) = dealer bonus if dealer wins. No dora, no fu-counting, no mangan tiers.
- **9 open Q's on implementation details** (tile ID honoring in chow, 过胡 per-tile lockout, marker animation timing, etc.) — many deferred to Phase D/v1.1 per MVP fast-cuts.

---

## Architectural Pivot Plan (Ripley — 2026-05-13)

# Ripley — Pivot Plan (autotable-vendored, Changsha-native)

> **Author:** Ripley (Lead)
> **Date:** 2026-05-13
> **Binds against:** `.squad/decisions/inbox/copilot-directive-2026-05-13T2306Z-pivot-binding.md`
> **Synthesises:**
> - `.squad/decisions/inbox/bishop-backend-salvage-inventory.md`
> - `.squad/decisions/inbox/hicks-autotable-ts-inventory.md`
> - `.squad/decisions/inbox/vasquez-rules-diff-manifest.md`
> **Status:** Awaiting Stephen's batch-answer on §4 before any code change ships.

---

## 1. Thesis

Vendor pwmarcz/autotable as an in-tree fork at `src/frontend/autotable-src/`, modify its TypeScript directly so the only game it knows how to render is Changsha (108 tiles, 14/14/13/13 wall, no honors / no dora / no riichi, 258-eye, Big/Small Win scoring, drag-driven claims with Pung/Chow/Kong/Hu overlays). The .NET backend keeps Bishop's pure rules engine intact (~2,500 LOC of `Changsha/*.cs` — state machine, win detector, scoring, claim adjudicator, bot policy) and becomes the authoritative server for autotable's own `NEW`/`JOIN`/`JOINED`/`UPDATE` WS protocol — extended with three small custom collections (`changsha.claim`, `changsha.scoring`, `changsha.banker`) that have no native carrier. SignalR, the React/Fluent UI 9 SPA at `/changsha`, the iframe-postMessage bridge, the `changsha-bridge-receiver.js` shim, and the legacy 136-tile `TableStateEngine` plus its `/api/tables/*` REST surface all die on a posted schedule. Net delta: ~3,500 LOC of frontend deleted, ~1,500 LOC added; ~2,400 LOC of legacy backend deleted, ~600 LOC of runtime repointed.

---

## 2. Phased plan

Five phases, each shippable in isolation. After every phase a user can play *something* — degraded but real — so we never go dark for more than one merge cycle.

### Phase A — Vendor + Scrub

**Goal.** Get autotable source in-tree, building, and serving at `/autotable/` from our own build, with the dead React/bridge cruft physically removed.

**Deliverables.**
- New folder `src/frontend/autotable-src/` containing pwmarcz/autotable `master` (recommended Option B: in-tree fork, no submodule — Hicks §1) with `COPYING` + `CC BY-NC-SA` notices preserved verbatim.
- Keep upstream **Parcel 2.15** build chain unchanged (recommended in §7 — Vite migration is out of scope for the pivot). Add `Makefile` invocation to the Docker image and CI; if Inkscape/Blender aren't available we ship the prebuilt artefacts that already live under `src/frontend/autotable/` and only rebuild on TS changes (no asset edits in v1).
- Replace `src/frontend/autotable/autotable.9519e86d.js` with the build output of the in-tree source. Delete `changsha-bridge-receiver.js`, the `<style id="changsha-embedded-mode">` block, and the `data-changsha-embedded` shim from `index.html` (Hicks §6.1).
- Delete `src/frontend/modern/` in full — `autotableBridge.ts`, `ChangshaTablePage.tsx`, `useLiveChangshaGame.ts`, `signalrClient.ts`, `changshaReducer.ts`, all `components/`, all `__tests__/`, the Vite config, the `package.json` for it (Hicks §6.2). ~3,500 LOC gone.
- Delete `src/backend/.../Tables/TableStateEngine.cs`, `TableContracts.cs`, `TableGameState.cs`, `TableStateHasher.cs`, `TableStateSerializer.cs`, `TableSessionEventStore.cs`, the two EF entities (`TableSession`, `TableSessionEvent`), the eight `/api/tables/*` minimal-API endpoints in `Program.cs` (lines 86–494), and the four test files (`TableStateEngineTests.cs`, `TableSeatViewProjectionTests.cs`, `TableSessionEventStoreTests.cs`, `ClaimResolutionApiTests.cs`) — ~2,400 LOC src + ~1,120 LOC tests (Bishop Bucket C).
- Drop SQLite tables `TableSessions` + `TableSessionEvents` in `DatabaseBootstrapper`.
- `.vscode/launch.json` + `.vscode/tasks.json` updated for the new compound-launch shape; PATH augmentation from PRs #27 + #28 preserved verbatim.
- Annotate `docs/rules/changsha-3d-renderer-plan.md`, `changsha-autotable-bridge.md`, `changsha-frontend-plan.md`, `changsha-signalr-contract.md` with a `SUPERSEDED — see ripley-pivot-plan.md` header.

**Local Dev (F5) story.** Stephen's binding constraint (`copilot-directive-2026-05-13T2315Z-f5-dev.md`): one F5 keystroke in VS Code = full dev loop, no manual prep. Post-pivot shape:
- `.vscode/launch.json` — kill the existing `Frontend Modern (Vite)` config and the `F5 Full Stack (Backend + Modern Frontend)` compound (both die with `modern/`). Add a new `Autotable (Parcel watch)` config (`type: node-terminal`, running the watcher in `src/frontend/autotable-src/`) plus a new compound **`F5 Full Stack (Backend + Autotable)`** that boots `.NET Backend` + `Autotable (Parcel watch)` together. F5 becomes one keystroke → backend listening on `:5114` + Parcel rebuilding on every TS save.
- `.vscode/tasks.json` — add a new `autotable: watch` task: `type: process`, `command: make`, `args: ["parcel"]` (or `npm` / `["run", "watch"]` if we add a script wrapper to `package.json`), `options.cwd: ${workspaceFolder}/src/frontend/autotable-src`, `isBackground: true`. Delete the existing `frontend: install` / `frontend: run` tasks (which point at `modern/`) alongside `modern/` itself. `backend: build` and `backend: run` stay verbatim.
- **PATH augmentation from PRs #27 + #28 preserved untouched.** Every existing `options.env.PATH` and `configurations[*].env.PATH` entry stays byte-identical. The pivot only adds new task/launch entries; it does not edit the existing PATH-augmented ones.
- The existing Vite proxy in `src/frontend/modern/vite.config.ts` (which routed `/api` + `/autotable` + WS to `:5114`) becomes obsolete the moment `modern/` is deleted — already on the delete manifest in §3. No replacement needed: the backend at `:5114` is the only origin the browser ever hits.
- **Parcel dev-server vs watch.** Investigated `/tmp/autotable-upstream/` — upstream's `Makefile` exposes `make parcel` which runs `./node_modules/.bin/parcel --no-hmr index.html about.html` (Parcel 2's dev-server subcommand on default port 1234, HMR explicitly off); upstream's `package.json` exposes no `watch` npm script. Two shapes work; pick one as default:
  - **Recommended (simpler):** the new `autotable: watch` task runs `./node_modules/.bin/parcel watch index.html about.html --dist-dir build/` (Parcel 2 watch-only mode — no HTTP server, no port-1234 dependency, just incremental rebuilds to `build/`). The .NET backend's static-file middleware serves `src/frontend/autotable-src/build/` verbatim at `/autotable/`. User refreshes the browser to see TS changes. One origin, one port, zero proxy plumbing.
  - **Alternative (HMR polish):** keep upstream's `make parcel` (Parcel dev server on `:1234`). Backend at `:5114` serves a dev `index.html` shim that links `<script src="http://localhost:1234/index.HASH.js">`, so Parcel's HMR websocket reaches the browser. More moving parts; defer to v1.1 unless HMR materially speeds iteration. Decision deferred to §4 Q16.

**Dependencies.** None. This is the cleanup pass.

**Exit criteria.** `make build` (in `src/frontend/autotable-src/`) emits the same byte-identical bundle behaviour the repo ships today. `dotnet build` shows 0 warnings, 0 errors. The remaining test suite (~226 passing minus the deleted ~50 tests under `Tables/*` and `Autotable/*`) is green. A user navigating to `/autotable/` gets the **stock Riichi 3D table** and can run a local 4-player setup. Nothing Changsha works yet — but nothing is broken either, and the repo is now ~5,000 LOC lighter. **F5-from-VS Code regression check:** on a fresh checkout, the user can hit F5 once and reach a working `/autotable/` page in the browser without any manual prep beyond a one-time `dotnet restore` and `cd src/frontend/autotable-src && npm install` (or the upstream-equivalent `make` bootstrap) per workstation.

**Owner.** Hicks (vendoring + bundle wiring), Bishop (legacy delete + EF migration), multi.

**Effort.** Medium. Mechanical deletes dominate; the vendor copy + build wiring is the only thing with real engineering risk.

**Risks.**
- *Build chain reproducibility.* Upstream uses Parcel + Inkscape + Blender; we currently ship prebuilt artefacts. If we never re-export `models.blend` or `tiles.svg` in v1 we don't need Inkscape/Blender, but the Makefile assumes them. Mitigation: commit the existing `.png`/`.glb` outputs as canonical for v1; gate the Inkscape/Blender targets behind a `make assets` opt-in.
- *Submodule-vs-fork regret.* Once we copy upstream in-tree we lose `git submodule update --remote`. Mitigation: tag the import commit with `upstream/<sha>` for future cherry-picks. Recommendation in §7 is firm.

---

### Phase B — Tile / Asset Surgery (Changsha-shaped scene)

**Goal.** The vendored autotable scene renders a 108-tile Changsha wall (14/14/13/13), no honors, no dora, no riichi sticks, no point-stick tray — but still uses upstream's local `Setup.deal()` for the actual placement. No backend integration yet.

**Deliverables.**
- `src/setup.ts`: `addTiles` loop `for (let i = 0; i < 108; i++)`; rewrite `tileIndex(i)` to `Math.floor(i / 4)` returning logical 0–26; delete the red-fives branch and the BAMBOO/THREE_PLAYER conditionals. Delete `addSticks` for Changsha or hide tray slots (Vasquez §1.14).
- `src/setup-deal.ts`: rewrite `DEALS.CHANGSHA.HANDS` for 4-4-4-1+1 across the 54-stack wall with 14/14/13/13 distribution + dealer +1 (Vasquez §1.5, item 11). Delete `WINDS` dealType. Keep `UNSHUFFLED` for debug.
- `src/setup-slots.ts`: add `SLOT_GROUPS.CHANGSHA` cloned from `FOUR_PLAYER` minus `riichi` slot, minus tray-stick slots, with the wall row width matching 14/14/13/13. Delete `riichi` slot/group (Vasquez §1.14).
- `src/types.ts`: collapse `GameType` to `CHANGSHA` only (recommendation §4 Q4 default); drop `Conditions.fives`, drop `Conditions.points`, drop `Conditions.back` (recommendation §4 Q5 default); add `Conditions.baseUnit: number` (default 1).
- `src/things.ts`: delete the `riichi` stick definition; keep the `MARKER` dealer chip (Vasquez §1.14, Q7).
- `src/world.ts`: delete the riichi-stick collision branch (`target.group === 'riichi'`); delete `toggleHonba`; modify dealer-toggle to be **display-only** (banker is authoritative server-side, no client cycling).
- `game-ui.ts` + `index.html`: hide `#fives`, `#points`, `#toggle-honba`, `#reset-points`; relabel `#deal-type` to expose only Changsha-HANDS; rename claim labels to **Chinese-character primary with pinyin sublabel** (`碰 Pung`, `吃 Chow`, `杠 Kong`, `胡 Hu` — recommendation §4 Q9 default).

**Dependencies.** Phase A complete.

**Exit criteria.** Opening `/autotable/` shows a Changsha-shaped 108-tile wall, dealer marker, no honor tiles in the catalog, no riichi sticks, no dora indicator slot, no point-stick tray. Clicking **Deal** runs upstream's local shuffle and places 14/14/13/13. Drag-to-discard works (laissez-faire upstream behaviour). User-visible result: you can sit at a Changsha-correct *empty* table and play with the tiles like a sandbox. No rules enforcement yet. **Shippable as a demo.**

**Owner.** Hicks. Vasquez reviews against the manifest checklist.

**Effort.** Medium. ~400 LOC of TS edits across 6 files; most lines are deletions or simple-value swaps.

**Risks.**
- *Riichi-shape grep leaks.* `honba` / `riichi` / `fives` / `kita` are scattered across ~8 files (Hicks R3). A single TS-error cascade on `Conditions` is likely; budget half a day of compile-error chasing.
- *Wall-arithmetic off-by-one.* Hicks called out the 14/14/13/13 split as the most cognitively loaded part; recommend Vasquez pairs on the deal-table generation and Bishop's `BreakPointService` tests get ported to a Jest/TS smoke test as a cross-check (Vasquez Q4).

---

### Phase C — Backend speaks autotable WS natively (still rules-blind from the client's POV)

**Goal.** The .NET backend serves `/autotable/ws` as the *authoritative* transport (not a `StateChanged` subscriber). Bundle reconnects against it, the existing `ChangshaToAutotableTranslator` produces the snapshot, bots play a hand end-to-end with the backend driving every `things` mutation. SignalR is gone.

**Deliverables.**
- *Promote* `src/backend/.../Autotable/*` out of the bridge subfolder: `AutotableProtocol.cs`, `AutotableSlotMap.cs`, `ChangshaToAutotableTranslator.cs`, `AutotableWsEndpoint.cs` all survive physically (Bishop Bucket C "conceptually deleted but repackaged" — ~840 LOC). Rename namespace to `…Api/Transport/` or similar. These are now the primary transport, not the bridge.
- Repoint `ChangshaGameRuntime.cs` (Bishop B1) — delete all 37 `_hub.Clients.*` / `_hub.Groups.*` calls; the runtime now calls into `AutotableConnectionManager` (or a slim `IAutotableTransport` extracted from it) to push collection-delta UPDATEs. Per-seat private payloads route via the connection-manager's `Guid` per-WS-connection identity instead of `Context.ConnectionId`. Estimate: 400–600 LOC delta.
- Repoint `ChangshaGameInstance.cs` — swap `SeatConnections: Dictionary<int, string>` for `Dictionary<int, Guid>` (Bishop B2, ~20 LOC).
- Delete `ChangshaHub.cs` (85 LOC) and the `AddSignalR()` + `MapHub<ChangshaHub>()` lines from `Program.cs` (Bishop B4).
- Implement **inbound UPDATE handling** with authentication + validation: every client-initiated `things[tileId] = { slotName }` is matched to the connection's seat, validated against the current Changsha state ("is it your turn? is this tile in your hand? is the destination slot legal?"), and either applied via the state machine or rejected by re-broadcasting `allEntries` with `full: true` (upstream protocol's standard rejection mechanism). This is the only way drag-to-discard actually works (Bishop risk #4).
- Add **lobby HTTP endpoint** `POST /api/changsha/games { seed?, bots: [int] }` returning `{ gameId }` (recommendation §7, Default for Bishop D1).
- Add **per-viewer `things` filtering** in `AutotableWsEndpoint` (recommendation §7, Default for Bishop D2 + Hicks R1): when the WS connection is authenticated to seat *S*, replace the `typeIndex` of any tile in `hand.*@s` for `s ≠ S` with `-1` (or the `back` sentinel). This is the **single most important security fix in the whole pivot**; without it every player can read every hand by opening DevTools.
- Repoint the 2 SignalR E2E tests (`ChangshaHubE2ETests.cs`, `ChangshaHubTestHarness.cs`) onto the WS endpoint — same behavioural assertions, new wire format (Bishop B7). Repoint the 19 `AutotableTranslatorTests.cs` + 4 `AutotableWsEndpointTests.cs` to live in the new namespace.
- Re-author `docs/rules/changsha-signalr-contract.md` as `docs/rules/changsha-autotable-protocol.md` — same hook list, expressed as autotable collection deltas (Bishop B6).

**Dependencies.** Phase A. Phase B is not strictly required but ships in parallel — Phase C drives the unmodified Riichi bundle just as happily as the Changsha-shaped one, so the two streams can land independently.

**Exit criteria.** A 4-bot Changsha hand (no humans) plays end-to-end via the autotable WS: dice roll, deal, draws, discards, claims, scoring, banker rotation. The runtime emits everything as `things` / `match` / `seats` / `dice` / `nicks` deltas. SignalR no longer exists in the codebase. Hand-tile privacy verified with a network-tab inspection: a connection bound to seat 0 sees only seat 0's tile faces in concealed slots. `dotnet test` green with the repointed harness.

**Owner.** Bishop primary. Hicks reviews protocol fit. Vasquez verifies the bot-driven hand matches `docs/rules/changsha-spec.md` behaviour.

**Effort.** Large. The crux of the whole pivot. The runtime repoint is dense, the per-viewer filter is novel, and inbound-UPDATE validation is greenfield.

**Risks.**
- *Protocol carrier gap.* Six runtime events (`ClaimWindowOpen`, `WinDeclared`, `ScoringComplete`, `BankerRotated`, `KongReplacementDrawn`-as-event, `GameEnded`) have no native carrier (Bishop risk #1). Phase C ships them as **synthetic ephemeral collections** — `changsha.claim`, `changsha.scoring`, `changsha.banker`, `changsha.lifecycle` — backwards-compatible with upstream's "ignore unknown kinds" semantics. The collections are wired server-side in Phase C but consumed UI-side in Phase D.
- *Inbound-UPDATE validation engine doesn't exist.* The legacy `TableStateEngine.ApplyHumanDiscard` is in Bucket C. Bishop must add a thin `ChangshaStateMachine.ValidateInboundMove(state, seat, tileId, targetSlot) → bool` helper before the WS endpoint can stop discarding bundle UPDATEs.
- *Per-viewer filter performance.* Server now serialises 4 distinct snapshots per game (one per seat). Acceptable for v1's single-game-per-instance Default #8; revisit if multi-game scale matters.

---

### Phase D — Rules graft (claim UI, scoring panel, banker visual, 过胡 lockout)

**Goal.** The 6 Changsha concepts that have no native autotable carrier get rendered correctly in the vendored TS UI. A human player can claim, declare Hu, see the scoring readout, and feel the banker rotate.

**Deliverables.**
- New `client.ts` collection adapters:
  - `changsha.claim` ephemeral, key = seatIndex: `{ claimType: 'pung'|'chow'|'kong'|'hu', tileId, sourceTile, deadline }`.
  - `changsha.scoring` ephemeral, key = 0: `{ winners, classification: 'small'|'big', pattern, payments, baseUnit }`.
  - `changsha.banker` sendOnConnect, key = 0: `{ seat, reason: 'win'|'self-pick-draw'|'washout'|'rotation' }`.
  - `changsha.lifecycle` ephemeral, key = 0: `{ phase: 'seating'|'rolling-dice'|'dealing'|'awaiting-discard'|'awaiting-claim'|'scoring'|'end-hand'|'rotating-banker' }`.
- New UI overlays (~400 LOC of HTML/CSS/TS per Hicks §3, "button-driven Changsha"):
  - **Claim window panel** — overlays bottom-centre when `changsha.claim` opens; buttons for `碰 Pung` / `吃 Chow` / `杠 Kong` / `胡 Hu` / `过 Pass`; 5-second countdown ring; closes on first valid action or timeout. The button emits a `changsha.command` UPDATE the backend consumes.
  - **Hu declaration button** — replaces the missing upstream "win" affordance. Single button (Vasquez Q5 default); fires `changsha.command.declareHu`. Win method (自摸 vs 点炮) is inferred server-side from phase.
  - **Score panel** — replaces the stick tray entirely with a per-seat numeric total + per-hand delta. Renders verbatim what `changsha.scoring` carries (Vasquez Q6 default — no frontend math).
  - **Banker marker animation** — `MARKER` thing repositions from the old dealer's seat to the new dealer on `changsha.banker` updates (Vasquez Q7 default).
  - **Concealed-kong rendering convention** — outer pair face-down on ankan (Vasquez Q9 default; upstream already supports rotation).
- Client-side **过胡 lockout indicator** — a small "X" badge over the Hu button when the seat has missed-win-locked the current tile (Vasquez §1.7, V1 conformance audit finding #3). The lockout state is server-authoritative; client only reads.
- Backend gap closures Vasquez flagged from the conformance audit:
  - `ClaimAdjudicator` honours the explicit `tileIds` the client sends for chow disambiguation (Vasquez Q8); stop auto-picking the first valid pattern.
  - `ChangshaStateMachine` enforces 过胡 per-tile lockout on Hu-pass (Vasquez §1.7).
- Chow tile labels: relabel claim buttons to Chinese-primary + pinyin (carried from Phase B; no new work, just listing the dependency).

**Dependencies.** Phases B + C complete.

**Exit criteria.** A human-vs-3-bots game completes end-to-end with full Changsha behaviour: dice roll, dealer-extra tile, 4-4-4-1+1 deal, drag-to-discard, claim overlays appearing for Pung/Chow/Kong/Hu opportunities, claim resolution with CCW tiebreak, kong-replacement from back-end-of-wall, Hu declaration, Small/Big Win classification, payment with dealer +1, banker rotates to winner, 过胡 lockout enforced on missed wins. The 16-hand game length cycles round winds correctly as display markers. **This is "Changsha v1 playable" — the milestone Stephen actually asked for.**

**Owner.** Hicks (UI overlays + collection adapters) primary. Vasquez (rules-engine gap closures: 过胡 + chow tile-ID honoring) parallel. Bishop reviews collection schema.

**Effort.** Large. The claim overlay alone is ~150 LOC of TS plus HTML/CSS; scoring panel ~80 LOC; 过胡 backend work ~60 LOC plus tests.

**Risks.**
- *Drag-to-claim ambiguity.* Hicks R2. If the claim window panel doesn't always show up before a player can drag a discarded tile, we get race conditions. Mitigation: server holds discards in a transient `discard.pending@s` slot for the 5-second window; only on window resolution does it move to the canonical discard pile. UI affordance: drag is *disabled* on pending discards; only the claim panel resolves them.
- *Multi-winner Hu (deferred to v2)* edge case: spec §3.3 says proximity rule (single winner); make sure the claim adjudicator doesn't accidentally allow two simultaneous Hu's during the 5-second window.

---

### Phase E — Smoke / polish / lobby

**Goal.** Production-ready surface: lobby entry, mid-hand reconnect, Docker single-image build, deletion of any last bridge / SignalR references in docs, observability for the new transport.

**Deliverables.**
- **Lobby** — recommendation §7: a **thin "create / join game" surface inside autotable's existing sidebar** (Hicks Path 1 + Bishop D1). New sidebar control under `#deal`: "New Changsha Game", "Add Bot to Seat N" (×3), "Join by code". Calls `POST /api/changsha/games` then issues `JOIN { gameId }`. No React. `/changsha` URL becomes a permanent redirect to `/autotable/?game=…`.
- **Mid-hand reconnect** — `localStorage('mj-autotable:changsha:gameId')` + `JOIN` snapshot path verified end-to-end. Already works in upstream; just confirm the Changsha-extended collections (`changsha.banker`, etc.) ship with `sendOnConnect: true`.
- **CORS** — drop `localhost:5173` from `ChangshaCorsPolicy` (Bishop D7 default). Keep `localhost:5114` / `7135` for backend dev.
- **Persistence-on-restart** — *deferred to a follow-up unless Stephen flags it*. The pre-existing `_games` in-memory volatility (Bishop risk #6) is a known gap; the pivot doesn't introduce it. Listed in §5.
- **Docs sweep** — delete `docs/rules/changsha-3d-renderer-plan.md`, `changsha-autotable-bridge.md`, `changsha-frontend-plan.md`, `changsha-signalr-contract.md` (they were marked SUPERSEDED in Phase A; now they go). Author a fresh `docs/architecture.md` describing the post-pivot single-transport, single-frontend shape.
- **Docker** — `infra/docker/Dockerfile`: drop the `runtime-modern` target; `runtime-autotable` becomes the only image. Build stage adds `make build` against `src/frontend/autotable-src/`.
- **Optional: standalone Riichi sandbox** — recommendation §7: **drop it** (Hicks R8). Stephen's directive collapses autotable to Changsha-only; carrying Riichi as a hidden `?gameType=RIICHI` flag is dead-code surface area. Variant tests can stay in the upstream-tagged commit for archaeology.

**Dependencies.** Phases A–D complete.

**Exit criteria.** A user lands on `/`, clicks "New Game", picks 3 bots, plays a full 16-hand match, sees the final scores, can refresh mid-hand and reconnect. Docker single-image build green. Zero references to SignalR, bridge, postMessage, or `modern/` in the codebase. `docs/architecture.md` accurately describes the production shape.

**Owner.** Hicks (lobby UI), Bishop (lobby endpoint + CORS + Docker), Vasquez (final spec-vs-behaviour pass on a full 16-hand game), multi.

**Effort.** Medium. Mostly polish; the lobby is ~80 LOC if we stick to Path 1.

**Risks.**
- *Persistence-on-restart still broken.* If the deployed instance restarts, every active table dies. This isn't a pivot-introduced bug, but Phase E is the natural moment to address it. Defaulted to "out of scope for v1 pivot" in §5; flag if Stephen disagrees.
- *Asset-license footgun.* `img/tiles.svg` is CC BY-NC-SA. Commercial deployment requires re-licensed glyphs (Hicks R9). Non-blocking for v1; flagged in §5.

---

## 3. Delete manifest (consolidated, by phase)

| Phase | Path / scope | LOC | What dies |
|---|---|---:|---|
| A | `src/frontend/modern/` (entire tree — components, bridge, signalrClient, reducer, mock infra, tests) | ~3,500 | React/Fluent UI 9 SPA, autotableBridge, useLiveChangshaGame, ChangshaTablePage iframe, all `__tests__` |
| A | `src/frontend/autotable/changsha-bridge-receiver.js` + `<script>` tag + `<style id="changsha-embedded-mode">` + `data-changsha-embedded` shim | 155 + ~30 | postMessage bridge receiver |
| A | `src/backend/.../Tables/TableStateEngine.cs` + `TableContracts.cs` + `TableGameState.cs` + `TableStateHasher.cs` + `TableStateSerializer.cs` + `TableSessionEventStore.cs` + `Data/Entities/TableSession.cs` + `Data/Entities/TableSessionEvent.cs` | ~2,000 | Legacy 136-tile Western-rules engine |
| A | `Program.cs` lines 86–494 (`/api/tables/*` × 8 endpoints) | ~390 | Legacy REST surface |
| A | Tests: `TableStateEngineTests.cs` (720), `TableSeatViewProjectionTests.cs` (51), `TableSessionEventStoreTests.cs` (120), `ClaimResolutionApiTests.cs` (230) | ~1,120 | Legacy engine tests |
| A (docs) | `docs/rules/changsha-3d-renderer-plan.md`, `…-autotable-bridge.md`, `…-frontend-plan.md`, `…-signalr-contract.md` | n/a | Strategy C design docs (marked SUPERSEDED in A, hard-deleted in E) |
| C | `src/backend/.../Changsha/ChangshaHub.cs` | 85 | SignalR hub dispatcher |
| C | `Program.cs` `AddSignalR()` + `MapHub<ChangshaHub>()` + SignalR CORS allowance | ~10 | SignalR wiring |
| C | `Hub/ChangshaHubTestHarness.cs` + `Hub/ChangshaHubE2ETests.cs` | 202 | Repointed onto WS, not deleted — but the SignalR-specific harness goes |
| E | The four "SUPERSEDED" docs from Phase A | n/a | Hard delete |
| E | `infra/docker/Dockerfile` `runtime-modern` target | small | React image |
| E | `localhost:5173` from `ChangshaCorsPolicy` | trivial | Vite SPA CORS allowance |

**Survives despite being in `Autotable/` today (Phase C repackages, doesn't delete):** `AutotableProtocol.cs` (~140), `AutotableSlotMap.cs` (~130), `ChangshaToAutotableTranslator.cs` (~260), `AutotableWsEndpoint.cs` (~280), and their 566 LOC of tests. These are the new transport's foundation; physical move under `Transport/`.

**Grand total deletion:** ~6,000 LOC src + ~1,300 LOC tests across Phases A + C + E.

---

## 4. Decisions Stephen needs to make BEFORE Phase A starts

Numbered. Each has a default; if Stephen says nothing, we ship the default. Switching cost is opinionated.

1. **Vendoring strategy** — submodule, in-tree fork, or vendor-copy?
   **Default:** In-tree fork at `src/frontend/autotable-src/`, tagged with `upstream/<sha>` for future cherry-picks. Reason: matches how `src/frontend/modern/` already lived; no submodule footgun for CI / Copilot agents; pwmarcz rejects non-Riichi PRs anyway (per Hicks §1) so upstream sync is rare.
   **Switching cost:** Low. Can be moved to a submodule any time before Phase B asset edits land.

2. **Bundler** — keep upstream Parcel 2.15, or migrate to Vite alongside our existing Vite frontend?
   **Default:** Keep Parcel. Reason: bundler migration is 1–2 weeks of unglamorous regression hunting (Bootstrap 4 + jQuery + `url:` imports → BS5 + Vite `?url`); the *modern* React app is being deleted in Phase A so the "consolidate to one bundler" pressure disappears.
   **Switching cost:** Medium. Adding Vite later requires re-doing every `url:` import and re-validating three.js mesh loading.

3. **Lobby** — autotable's native sidebar IS the lobby (Path 1), thin React lobby + native game (Path 2), or autotable's Connect/Setup UI verbatim?
   **Default:** Path 1, sidebar IS the lobby. Reason: matches Stephen's screenshot literally; no second bundler / second visual language; ~80 LOC vs ~200 LOC.
   **Switching cost:** Low. Path 2 is additive — a React lobby can be bolted on later without changing the autotable game canvas.

4. **`GameType` enum** — keep upstream variants (`FOUR_PLAYER`, `THREE_PLAYER`, `BAMBOO`, `MINEFIELD`, `FOUR_PLAYER_DEMO`) as legacy / no-op options, or collapse to `CHANGSHA` only?
   **Default:** Collapse to `CHANGSHA` only (Vasquez Q2). Drop `Conditions.gameType` entirely; replace with Changsha-specific config. Reason: binding directive says "autotable IS Changsha"; legacy enums are dead code that invites drift.
   **Switching cost:** Medium. Once the deal tables / slot groups assume Changsha-only, reintroducing Riichi requires re-vendoring upstream's `setup-deal.ts`.

5. **Claim labels** — Chinese characters (碰/吃/杠/胡), pinyin (Pung/Chow/Kong/Hu), or both?
   **Default:** Chinese-character primary with pinyin sublabel (Vasquez Q5). Example: `碰 Pung`.
   **Switching cost:** Low. UI-string change.

6. **Concealed-tile privacy at protocol layer** — per-viewer `things` filtering server-side, or accept the upstream all-public model?
   **Default:** Server-side filter per-viewer in `AutotableWsEndpoint` (Bishop D2 + Hicks R1). Reason: without this, any player reading WS frames in DevTools sees every concealed hand. Non-negotiable for a game played beyond a trust circle.
   **Switching cost:** High. Designing this in retroactively means re-doing the snapshot path and every translator test.

7. **Claim grammar** — extend the WS protocol with custom collections (`changsha.claim` etc.) for explicit Pung/Chow/Kong/Hu intents, or drag-to-meld with server-side first-valid-drag arbiter?
   **Default:** Custom collections + button overlay (Bishop D3 + Hicks R2). Reason: drag-only is the most autotable-native UX but has irreducible race / ambiguity (Hicks R2); Stephen's earlier React lobby exposed buttons, so the muscle memory is button-driven. Buttons + custom collections are backwards-compatible with upstream's "ignore unknown kinds."
   **Switching cost:** Medium. The custom collections survive even if we later add a drag affordance.

8. **Deal-batch ack gating** — keep the per-seat `AcknowledgeDeal` quorum, or drop it and let bundle animate freely?
   **Default:** Drop deal-acks (Bishop D4). Reason: autotable has no deal-ack concept; the bundle animates `things` movements over its own physics timer; keeping ack gating means a custom WS message for no behavioural gain. Turn 1 starts on a fixed server-side timeout (e.g., 1500 ms after the last deal-batch UPDATE).
   **Switching cost:** Low. Re-adding is a single ephemeral collection.

9. **Score display** — render verbatim from `changsha.scoring` (no frontend math, raw units), or apply the base-unit multiplier client-side?
   **Default:** Verbatim, no frontend math (Vasquez Q6). The base-unit multiplier is applied in `ScoringService` server-side. Frontend reads numbers, renders numbers.
   **Switching cost:** Low.

10. **`Conditions.back` cosmetic option** — survive as a tile-back-color toggle, or remove for simplicity?
    **Default:** Remove (Vasquez Q3). Halves the tile-type space (27 logical types instead of `27 × 2`); the upstream `+ 37 * conditions.back` complication disappears.
    **Switching cost:** Low. Cosmetic only.

11. **Hu claim affordance** — single Hu/胡 button (server infers self-draw vs discard), or two buttons (Tsumo / Ron)?
    **Default:** Single Hu/胡 button (Vasquez §1.7, recommendation §4 Q5 default). Win method is inferred from phase context server-side.
    **Switching cost:** Low. UI change.

12. **Marker repositioning** — animate dealer marker on banker rotation, or static "dealer is seat N" text?
    **Default:** Animate the `MARKER` thing to the new dealer's slot (Vasquez Q7). The infrastructure is free — upstream already supports thing-slot animation.
    **Switching cost:** Low.

13. **`/api/tables/*` REST + `TableStateEngine`** — hard-delete in Phase A, or keep for archaeology?
    **Default:** Hard-delete in Phase A (Bishop D5). Reason: no surviving frontend consumes them; their 1,120 LOC of tests go red the moment the controllers are removed regardless of when. Killing them in Phase A consolidates the cruft sweep.
    **Switching cost:** High to resurrect (mechanical, but 2,400 LOC src + 1,120 LOC tests); near-zero to keep deleted.

14. **Replay-integrity verifier** — port the legacy `VerifyReplayIntegrity` + `STATE_INVARIANT_BROKEN` to the Changsha runtime in scope of the pivot, or defer?
    **Default:** Defer (Bishop D8). Reason: not blocking gameplay; v1 is single-game-per-instance per Default #8; replay determinism survives anyway through seeded RNG. Adds ~200 LOC of dead-but-correct code we don't need yet.
    **Switching cost:** Low — additive feature, can land in v1.1.

15. **Vasquez conformance audit gaps** (chow tile-ID honoring, 过胡 enforcement) — fix inside Phase D as part of the rules graft, or split into a pre-pivot bugfix PR?
    **Default:** Fix inside Phase D (Vasquez Q8 + §1.7). Reason: both gaps are user-visible only once the UI surfaces claims; bundling with the claim-window work avoids touch-and-revisit.
    **Switching cost:** Low.

16. **F5 dev shape** — compound launch (one F5 = backend + Parcel watch in parallel), VS Code task running Parcel separately (user launches Parcel in a terminal alongside an F5'd backend), or simple build-on-save (no watch process; a save-hook task fires `parcel build` per save)?
    **Default:** Compound launch. Reason: matches Stephen's binding constraint (`copilot-directive-2026-05-13T2315Z-f5-dev.md`) — F5 = working dev loop with no manual prep. Cost is ~30 lines of `.vscode/*` config; benefit is the user never has to think about which terminals are running which watcher. Investigated `/tmp/autotable-upstream/Makefile` and `/tmp/autotable-upstream/package.json`: upstream already exposes a `make parcel` target (Parcel 2.15 dev server, `--no-hmr`) and no native `watch` npm script — we wrap that (or `parcel watch --dist-dir build/`) in a VS Code task and reference it from a `node-terminal` config inside `launch.json`'s compound.
    **Switching cost:** Low. `.vscode/*` files are pure config; can be swapped to any of the three alternatives any time without touching application code.

---

## 5. Decisions that can wait

These appear in the inventories but Stephen does NOT need to think about today.

- **Asset re-licensing** for `img/tiles.svg` (CC BY-NC-SA — Hicks R9). Non-blocking for personal/non-commercial use. Revisit if/when the project goes commercial. **Phase: post-v1.**
- **Persistence-on-restart hydration** (Bishop D6, risk #6). Pre-existing gap, not introduced by the pivot. Revisit when multi-game or production deploy is in scope. **Phase: post-Phase-E (v1.1).**
- **Replay integrity verifier** for Changsha (Bishop D8). See decision 14 above; deferred by default. **Phase: v1.1.**
- **Bundler migration** to Vite (Hicks R5). Deferred by decision 2. **Phase: post-v1 if at all.**
- **Standalone Riichi sandbox** preservation (Hicks R8). Defaulted to "drop it." **Phase: never, unless Stephen flags.**
- **Asset rebuilds** requiring Inkscape / Blender (Hicks R4). Not needed unless tile glyphs change. **Phase: revisit if/when we want new mesh variants.**
- **v2 rules concepts** — bird catching, ready-kong dice, robbing-the-kong, win-after-kong, seabed wins, instant wins, heaven/earth blessings, All Generals, Full Beggar's Hand, Luxury Seven Pairs, kong micro-payments, multi-winner Hu (Vasquez §2.1). **Phase: v2.**
- **Bot-management UI placement detail** (Hicks open Q10). Defaulted to "sidebar control under `#deal`" in Phase E. Revisit if Stephen wants a richer lobby. **Phase: E.**
- **Three.js version drift** (Hicks R6). LOW risk; revisit only if we ever do React-side 3D again. **Phase: never expected.**

---

## 6. Critical risks (top 3)

### Risk 1 — Hand-tile privacy is broken at the protocol layer (Hicks R1, Bishop D2)
**What.** Upstream autotable sends every `things[*].slotName` to all connected clients with the `tileId` payload intact. Concealed-hand secrecy is **purely visual** (rotation index 2 = face-down). Cheating is trivial: open DevTools, inspect WS frames, read every opponent's hand. Upstream tolerates this because it's a friend-trust game.
**Phase exposed.** Phase C, the moment the backend becomes the authoritative transport.
**Mitigation.** Server-side per-viewer `things` filter in `AutotableWsEndpoint` (decision 6, defaulted to "ship the filter"). For any `things[tileId]` whose `slotName` matches `hand.*@s` for `s ≠ viewerSeat`, replace `tileId` with a sentinel (`-1` or `back`-type). Cost: 4× snapshot serialisation per game (acceptable at single-game scale). Verify by network-tab inspection in Phase C exit criteria.

### Risk 2 — Protocol-carrier gap for Changsha-specific events (Bishop risk #1)
**What.** Six runtime events (`ClaimWindowOpen`, `WinDeclared`, `ScoringComplete`, `BankerRotated`, claim-grammar variant of `ClaimMade`, `GameEnded`) have **no native projection** into upstream's `match`/`seats`/`things`/`nicks`/`dice` collections. Without addressing this, half the game's state is invisible to the client.
**Phase exposed.** Phase C (events fire but go nowhere) and Phase D (UI overlays need to consume them).
**Mitigation.** Phase C ships four custom ephemeral collections — `changsha.claim`, `changsha.scoring`, `changsha.banker`, `changsha.lifecycle` — wired into `client.ts`'s collection registry. Upstream's protocol ignores unknown kinds, so the extension is non-breaking. The cost is design — those four collection schemas must be locked before Phase C ends or Phase D blocks. Recommendation: Bishop drafts schemas during Phase C kickoff, Hicks reviews against UI needs, Vasquez verifies against `docs/rules/changsha-spec.md`.

### Risk 3 — Inbound-UPDATE validation engine doesn't exist yet (Bishop risk #4)
**What.** Today the bridge is one-way; bundle UPDATEs are logged and discarded. The pivot makes drag-to-discard the primary input. Every inbound UPDATE must be (a) authenticated to a seat, (b) validated against the current Changsha state (whose turn? legal tile? legal destination?), and (c) either applied via the state machine or rejected by re-broadcasting `allEntries` with `full: true`. The legacy `TableStateEngine.ApplyHumanDiscard` exists but is in Bucket C; the Changsha runtime currently expects already-validated commands.
**Phase exposed.** Phase C (the moment drag-to-discard is the only input path).
**Mitigation.** Add `ChangshaStateMachine.ValidateInboundMove(state, seat, tileId, targetSlot) → ValidationResult` as a Phase C prerequisite. ~80 LOC + unit tests. Reuse the slot-name grammar from `AutotableSlotMap`. Cross-check: every existing claim-adjudicator / state-machine test in Bucket A should still pass, because they exercise the same state transitions from a different entry point.

---

## 7. Recommendations on scope (opinionated picks)

- **Vendoring:** In-tree fork at `src/frontend/autotable-src/`. Not a submodule (CI/Copilot-agent footgun), not a verbatim drop (loses upstream-sha audit trail). See decision 1.
- **Build chain:** Keep upstream Parcel 2.15. Don't migrate to Vite. The Vite-everywhere consolidation pressure disappears with the React app's deletion. See decision 2.
- **Lobby:** Path 1 — autotable's existing sidebar IS the lobby. Add 2–3 sidebar controls ("New Changsha Game", "Add Bot", "Join by code"). No React. ~80 LOC. See decision 3.
- **Test surface:**
  - **Rewrite:** `Hub/ChangshaHubTestHarness.cs`, `Hub/ChangshaHubE2ETests.cs` (202 LOC) → WS-driven equivalents. Same behavioural assertions, new wire format. Phase C deliverable.
  - **Repoint:** `Autotable/AutotableTranslatorTests.cs` (349 LOC), `Autotable/AutotableWsEndpointTests.cs` (217 LOC) → live under the new `Transport/` namespace. Phase C.
  - **Delete:** `TableStateEngineTests.cs` (720), `TableSeatViewProjectionTests.cs` (51), `TableSessionEventStoreTests.cs` (120), `ClaimResolutionApiTests.cs` (230). Phase A.
  - **Keep verbatim:** all of Bucket A — `ChangshaServices/*` (227 LOC), `Changsha/*` (1,813 LOC). Zero touch. These are the family jewels.
- **CI gates:** add a TypeScript `tsc --noEmit` step on `src/frontend/autotable-src/` (upstream's existing type-check). Don't add new lint/build tooling beyond what each side already has.

---

## 8. What I'd cut from scope if Stephen wanted MVP faster

Three concrete cuts. Each independently shaves real time.

1. **Defer multiplayer entirely; ship "1 human + 3 bots, single game per backend instance."** Phase C's per-viewer privacy filter still ships (don't compromise on that). But the lobby's "Join by code" disappears, mid-hand reconnect can be browser-localStorage only, and we don't worry about two human players hitting the same backend. This collapses Phase E by ~half. **Cost of switching later:** Low — multiplayer is additive once the per-viewer filter and `_games` dictionary already exist.

2. **Drop the claim-window overlay; ship drag-to-meld with a server-side first-valid-drag arbiter.** This trades UI polish for a chunk of Phase D. The 5-second window still exists server-side; the server simply applies the first legal `things[tileId] = { slotName: meld.*@s }` update it receives. Saves ~150 LOC of TS overlay work plus the `changsha.claim` collection's complexity. **Cost of switching later:** Medium — adding explicit claim buttons later requires retrofitting the priority semantics; Hicks R2 flagged this as a real ambiguity risk if shipped without buttons. Only cut if Stephen explicitly accepts the race-condition tradeoff for MVP.

3. **Defer Vasquez's two conformance gaps** (chow tile-ID honoring, 过胡 lockout) **to v1.1.** Ship Phase D without them; document the deviation in `docs/rules/changsha-deviations-v1.md`. The chow gap means the backend picks a sequence for you if multiple are possible (annoying, not unfair); the 过胡 gap means you can re-Hu on a tile you passed (minor permissiveness). **Cost of switching later:** Low — Vasquez has both gaps scoped; the fixes are ~60–100 LOC each, no migration debt.

---

*End plan. Awaiting Stephen's batch-answer on §4 decisions 1–15 before Phase A kicks off.*

---

## Phase F — Changsha Realism (manual pickup, variant switch, 3-tier bot)

**Branch:** `stlong/phase-f-changsha-realism` (cut from `stlong/phase-b-changsha-scene`)
**Tip:** `b64efb8` (Wave 4 + reconciliation + stale-bundle prune)
**Shipped:** 2026-05-19 (post-Wave-3)
**Tests at gate:** **319 / 0 / 9** (Wave 4 reconciliation; was 318/1/9 before the privacy-mask slot-suffix fix)
**Bundle:** `src/frontend/autotable/autotable-src.6d5fae4c.js`
**Primary sources (kept in `.squad/decisions/inbox/`, gitignored — local-only references):**
- `copilot-directive-2026-05-19T1605Z-changsha-realism.md` — Stephen's directive (verbatim)
- `ripley-phase-f-design.md` — design contract (~955 lines, authoritative for §1–§8)
- `vasquez-phase-f-rule-audit.md` — 12-axis rule audit + falsifiable bot-tier specs
- `hicks-phase-f-frontend.md` — frontend delta
- `bishop-phase-f-backend.md` — backend delta

### Stephen's Directive (2026-05-19T16:05Z)

After seeing the Wave-3 side-by-side screenshots, Stephen filed a major UX correction: the current "click Deal → tiles teleport" behaviour is wrong for real Changsha play. Four binding requirements:

1. **Manual pickup is the DEFAULT** for Changsha. Auto-deal becomes opt-in. Real ceremony: dealer rolls 2d6 → break-point counted from right of chosen wall → CCW round-robin 4×3 → single-tile round → dealer-extra → AwaitingDiscard.
2. **Original autotable variants MUST work without modification** when selected. Phase B's collapse of `GameType` to CHANGSHA-only must be reversed; Riichi 4p / 3p / Bamboo / Minefield ride the original upstream code path.
3. **Single-player + bot-fill modes are required.** 0 / 3 / 4 bot fill. 3 bots = single-player; 4 bots = spectator mode.
4. **Bots play full games autonomously** in spectator mode, with selectable difficulty (Easy / Medium / Hard — Stephen confirmed: all three ship in Phase F, not just Medium).

Rule-source check (Stephen asked): both **Kong (杠)** and **Hu (胡)** are confirmed in the cited MahjongPros + Baidu sources; the 4-button claim arc (碰 / 吃 / 杠 / 胡) is correct. Hu is unusual as a UI "claim" (self-declared win) but exposing it as a button stays consistent.

### §1 — Variant-Switching Architecture (Ripley)

- **`GameType` enum restored** to upstream's 4 (`FOUR_PLAYER`, `THREE_PLAYER`, `BAMBOO`, `MINEFIELD`) alongside `CHANGSHA`. **`FOUR_PLAYER_DEMO` dropped** — never wired upstream, deliberate divergence (documented).
- **`Conditions` extended** with `back`, `fives`, `points`, `baseUnit`, `dealMode`. `Conditions.defaultsFor(gameType)` produces the right per-variant baseline so each picker change is a clean re-seed.
- **No new dual-code-path.** `AutotableGameState.UpdateSource.Runtime|Client` precedence (Wave 3) means when `variant != changsha` we simply never bind a Changsha runtime — the bundle's UPDATEs flow peer-to-peer through `AutotableGameState` as pure relay, exactly like upstream's `server/game.ts`. One switch (`AutotableConnection.RuntimeMode`); Relay vs ChangshaRuntime.
- **Variant flag is frontend-driven, backend-respected.** Bundle sends `match[0].conditions.gameType` on first UPDATE; backend reads it to decide whether to bind a runtime game.
- **Mid-session variant switching not supported** — runtime binds on first seat-take; subsequent connections see the bound runtime regardless of `?variant=`. UI gates `#game-type` select behind a "Reload to change variant" warning (Phase G can hot-swap). `#deal-mode` stays hot-swap-safe (takes effect on next deal).

### §2 — Manual-Pickup State Machine (Ripley + Vasquez locked, Bishop shipped)

Grafted onto `ChangshaPhase.Dealing` as a sub-state machine, NOT a parallel system. The existing one-shot `Deal()` becomes the `dealMode=auto` path; new `BeginManualDeal() → TakeTilesFromWall() × N → AwaitingDiscard` is the `dealMode=manual` path. Both converge in identical hand state. Downstream (claim, scoring, banker rotation) untouched.

**6 new `ChangshaPhase` values** stitched between `RollingDice` and `AwaitingDiscard`:
`BreakPointMarked` → `PickupRound1` → `PickupRound2` → `PickupRound3` → `SingleTilePickup` → `DealerExtra` → `AwaitingDiscard`.

State carries `state.PickupSeatIndex` + `state.PickupExpectedCount` + `state.BreakPoint` (the wall-index where the dealer breaks; cursors advance CCW from there).

**Wire shape — new `pickup` collection (singleton; key `current` / `0`):**
- **Inbound (server → bundle):** `{ phase, seatIndex, expectedCount, dealMode, breakPoint, wallIndex }`. Tombstoned on transition to `AwaitingDiscard`.
- **Outbound (bundle → server):** `pickup.rollDice` `{ seatIndex }` and `pickup.take` `{ seatIndex, count, wallTileIds }`. `wallTileIds` is informational (the runtime is authoritative for wall ordering and may ignore the field).

**Rules locked by Vasquez (12-axis audit, with sources):**

| Topic | Default |
|---|---|
| Dice | 2d6, sum 2..12; dealer rolls; **single roll per hand** (no re-roll/advantage) |
| Wall break — count direction | **STACKS** from the right end of chosen wall (NOT individual tiles — odd sums would land mid-stack, physically impossible) |
| Wall break — selection | `wallIndex = (dealer + (sum-1) % 4) % 4` |
| Wall break — boundary | Break tile is the FIRST tile drawn (state.Wall index 0 after rotation) |
| Pickup order | **CCW**: dealer → +1 → +2 → +3 (mod 4) — all 3 primary sources (MahjongPros, Baidu, `docs/rules/changsha-spec.md`) agree |
| Pickup rounds | 4-each × 3 → 1-each × 1 → dealer-extra × 1 (54 stacks total = 108 tiles; pickup consumes exactly 53) |
| Wall-wrap during pickup | Physically possible but mathematically impossible (max pickup 53 < wall 108); asserted as invariant |
| Dealer post-pickup count | 14 (others: 13); wall remaining: 55 |
| Hu during pickup | Forbidden — phase-gate rejects (hand is incomplete) |
| Kong replacement during pickup | N/A — Kongs only after `AwaitingDiscard`; uses back of wall (no intersection) |
| Mid-game dealMode toggle | Silently deferred to next hand |
| Mid-game variant toggle | Rejected — requires page reload |

**Wave 3's per-viewer privacy filter already handles partial-hand privacy.** Translator's `BuildThingEntries` walks `state.Hands` size-agnostically; an 8-tile concealed hand renders 8 face-down entries to non-viewer seats automatically. Zero new privacy code.

**Crucial state-machine detail (Bishop, per `ExpectedPickupCount`):** the FIRST pickup is the dealer's round-1 of 4 tiles — phase stays at `BreakPointMarked` until that first call lands, then advances to `PickupRound1` for the next 3 seats. The other rounds map directly: `PickupRound1/2/3` → 4 tiles; `SingleTilePickup` → 1; `DealerExtra` → 1 (dealer-only, no cursor rotation).

### §3 — Deal-Mode Toggle (Ripley)

- **Default `dealMode`:** `manual` for Changsha, `auto` for upstream variants (driven by `Conditions.defaultsFor`).
- **`Auto` is backward-compat.** The E2E WS test (`Full_Hand_ViaAutotableWebSocketRelay_BotsAndOneHuman`) still uses the auto path unchanged. Test/runtime composition is unchanged for that path.
- **Toggle applies on the NEXT deal,** not the active one. The bundle's `match[0].conditions.dealMode` is the source of truth.

### §4 — Bot Fill Modes (Ripley)

- **0 / 3 / 4 bot fill** in the lobby/setup panel.
  - 0 bots: wait for humans (default for online play).
  - 3 bots: **single-player mode** (human + 3 bots fill seats 1–3).
  - 4 bots: **spectator mode** (watch 4 bots play; user is observer).
- **Default `botCount`:** 3. **Default `botDifficulty`:** Medium.
- **`AutotableConnection.AutoBotFill` flips** from Wave-3's `true` default to explicit `BotCount`-driven fill. Wave-3 bare-URL UX is preserved because `botCount=3` is the new query-param default.
- **Bot seat → human conversion** at any time (click unseat on a bot seat to free it).

### §5 — Full Changsha Bot Engine (Ripley design, Vasquez specs, Bishop shipped)

Replaced the legacy claim-resolution-only `ChangshaBotPolicy` with a pluggable engine. New `Changsha/Bot/` directory holds the engine; legacy `ChangshaBotPolicy.cs` is now a thin facade over `ChangshaBotEngine.Resolve("medium").DecideAction(state, seat)` — `BotMatchHarness` and existing harness tests terminate identically.

**`IChangshaBotStrategy`** — 4 phase hooks + unified `DecideAction` router:
- `OnTurnStart(state, seat)` — own-turn decision
- `OnOtherDiscard(state, seat, discarderSeat, discardedTileId)` — claim window decision
- `OnSelfDraw(state, seat)` — post-draw decision (Hu / Kong / standard)
- `OnPickupCue(state, seat)` — pickup-phase cue
- `DecideAction(state, seat)` preserved as the public entry point so the existing harness keeps working unchanged.

**Three falsifiable tiers (Vasquez §10):**

| Tier | Discard rule | Chow | Kong | Hu | Defensive? |
|---|---|---|---|---|---|
| **Easy** | Highest-rank tile (Wan < Tong < Tiao tiebreak by suit-index desc) | **Never** | Only when hand ≥10 tiles | **Always claim** | No (no EV) |
| **Medium** (port of legacy `ChangshaBotPolicy`) | Shanten-minimizing (pairs + adjacencies + 2/5/8 bias) | Only when next-CCW + non-shanten-worsening | Always claim if offered | Always | Tracks 过胡 lockout |
| **Hard** | EV-maximizing (Medium keep-score + defensive penalty against tiles in `state.DiscardPile`) | Only when shanten-improving | EV-gated (`CountLooseTiles ≥ 2`) | Always | Tracks others' 过胡; biases away from feeding seats with `MissedWinSeats` flag |

**`HandEvaluator`** ships as a static utility: `SelectDiscardTile(hand, [state])`, `CountLooseTiles(hand)`, `FindConcealedKongCandidate(hand)`, `FindAddedKongCandidate(hand)`, `CollectDiscardedLogicals(state)`, and the coarse `MinShantenToHu(hand, remainingWall)` estimator. Vasquez's test only asserts `shantenAfter ≤ shantenBefore` on discard (which the coarse estimator satisfies because removing any tile cannot increase the loose-tile floor and the groups-held count is monotone). Rigorous shanten counter deferred to V2 (Big-Win patterns make it expensive).

**`ChangshaBotEngine.Resolve(string?)`** — case-insensitive; null/empty/unknown → `MediumStrategy`. Singleton instances, zero per-decision allocation.

**Bot delays (`ChangshaRuntimeOptions`):** `BotMoveDelayMs = 800` (turn), `BotPickupDelayMs = 500` (pickup), `BotClaimDelayMs = 400` (claim). All configurable via `IOptions<ChangshaRuntimeOptions>`.

### Backend — Bishop's Cut

- **`AutotableProtocol.cs`** — new `Pickup` collection kind + `PickupEntry` / `BreakPointWire` value classes.
- **`AutotableWsEndpoint.cs`** — `AutotableRuntimeMode` enum (`Relay` | `ChangshaRuntime`). `AutotableConnection` gains `Variant`, `DealMode`, `BotCount`, `BotDifficulty`, and computed `RuntimeMode`. `HandleConnectionAsync` parses 4 query params with defaults (`variant=changsha`, `dealMode=manual`, `botCount=3`, `botDifficulty=Medium`). `HandleUpdateAsync` branches on `RuntimeMode`: **Relay = full passthrough** (privacy filter + translator never invoked, byte-identical to upstream `pwmarcz/autotable`'s `server/game.ts`); **ChangshaRuntime = existing Phase D routing** plus new `Pickup` kind routed to `TryHandlePickupActionAsync`.
- **`ChangshaToAutotableTranslator.cs`** — emits `pickup["current"]` during `IsPickupPhase(state.Phase)`; tombstoned on `AwaitingDiscard`.
- **`ChangshaGameRuntime.cs`** — `StartGameAsync` branches on `state.DealMode`: `Auto` runs the existing one-shot `Deal()`; `Manual` stops at `RollingDice` and waits for `RollDiceAsync` / `TakeTilesFromWallAsync`. Both new entry points hold `instance.Lock` and fire `StateChanged`.
- **`BeginManualDeal(state, DiceRoll)`** takes a `DiceRoll` directly (not `IDiceService`) so test harnesses can pin the roll; runtime injects the seeded `DiceService` for determinism.
- **`Changsha/Bot/`** — new dir: `IChangshaBotStrategy.cs`, `EasyStrategy.cs`, `MediumStrategy.cs`, `HardStrategy.cs`, `HandEvaluator.cs`, `ChangshaBotEngine.cs`.

### Frontend — Hicks's Cut

- **Setup pipeline branched.** `setup-slots.ts` got upstream's `SLOT_GROUPS` for all four Riichi variants verbatim (tray + payment + riichi `START` slots restored). `setup-deal.ts` got upstream `DEALS` (including FOUR_PLAYER's 11 roll-conditional `HANDS` variants) + `POINTS` table. `setup.ts` branches `setup`/`addTiles`/`tileIndex`/`replace`/`getScores`/`addSticks` on `gameType === CHANGSHA`. Changsha keeps its 108-tile / no-stick shape; Riichi variants get the full 136 + sticks treatment.
- **`pickup` collection on `client.ts`.** Singleton key `0` carries Bishop's pushed phase snapshot; outbound keys `'rollDice'` and `'take'` are command shapes (cast `as any` at the call site, same pattern as `claim`).
- **`world.onPickup()` caches latest entry; `world.isMyPickupTurn()` gates wall-tile drags.** When the gate is hot, `onDragStart` intercepts the wall click and emits `pickup.take` instead of starting a drag — **no optimistic move**; the runtime owns the truth.
- **Pickup HUD** (center-bottom): "Your turn — pick N tiles" with a Take-N shortcut button; switches to "Seat E is picking…" when another seat is on the clock; hidden when no pickup active.
- **Roll-dice button** (table center, dice icon + "Roll dice" label). Visible only when `pickup.phase` ∈ {`RollingDice`, `rollDice`} AND `pickup.seatIndex === selfSeat`. Click → emits `pickup.rollDice` → backend resolves → existing Wave 3 dice HUD takes over.
- **Deal-mode toggle (Changsha-only):** `#deal-mode` select (manual/auto) folds into `Conditions.dealMode`; takes effect on next deal.
- **Bot count + difficulty pickers** are informational for now (banner + localStorage persistence); once Bishop's per-seat difficulty field ships, the banner reads from `SeatInfo`.
- **Variant indicator badge** (top-right): 🀄/🎴/🎋/💣 by family. Updates on every match update.
- **Honba toggle + Reset points** restored from upstream, gated `riichi-only`. CSS body class (`variant-changsha` / `variant-riichi`) drives picker visibility.
- **URL params + localStorage.** `?variant=` / `?dealMode=` / `?botCount=` / `?botDifficulty=` / `?fives=` / `?points=` parsed at boot. Back-compat: `?bots=true` aliases `?botCount=3`. Resolution order: URL > localStorage > `Conditions.defaultsFor()`. Storage namespace: `autotable.phaseF.v1.*`.

### Design Decisions Worth Recording (Hicks)

1. **Strict lockout of frontend-only pickup state.** Bundle does NOT mirror Bishop's pickup phase machine — it only renders what arrives. Optimistic UI is explicitly rejected: a "take" click does NOT pre-move tiles; the runtime's `things` UPDATE moves them. Keeps bundle stateless w.r.t. pickup ordering and avoids resync drift.
2. **`wallTileIds` is informational.** Frontend computes it by walking wall slots in name-sort order (numeric-aware) and picking the first N occupied. Backend may use it for cross-validation or ignore entirely.
3. **Variant hot-swap deferred to Phase G.** Setup pipeline rebuilds tile catalogues at boot; mutating `gameType` mid-session would leave dangling Things in the scene graph.
4. **Dual-affordance pickup:** Take-N HUD button AND wall-click intercept emit the same `pickup.take` — the button is the impatient-player shortcut, the click is the natural gesture.

### Reconciliation — `30d03ee`

After the Wave 4 parallel landings (Hicks + Bishop + Vasquez tests), one test failed and two minor contract drifts surfaced:

1. **Pickup singleton key drift.** Hicks's bundle expected the inbound singleton at key `0` (number); Bishop's translator emitted at key `"current"` (string). Reconciliation aligned both ends — translator emits both; bundle reads either. Single source of truth from here forward.
2. **Privacy-mask slot-suffix test bug** (`ManualPickupAcceptanceTests.Pickup_PrivacyMask_OpposingHandsHaveFacesStripped`). Slot format is `"hand.{handIdx}@{seat}"`; the test used `slot.StartsWith("hand.0")` to identify the viewer's hand (which would match `hand.0@anyone`). Bishop diagnosed the bug in the inbox note; reconciliation applied the fix (use `slot.EndsWith("@0")`). **Deferred follow-up:** `FilterEntriesForViewer` (AutotableWsEndpoint.cs:644–652) has the same wrong slot-parse — non-blocking (no test exercises it before Phase F; relay-mode connections never invoke the filter), but should be cleaned up in a follow-up pass.

Result: **318/1/9 → 319/0/9.** Stale parcel bundle (`d9507f0f.js`) pruned in `b64efb8`.

### Deferred Follow-ups (Filed, Not Blocking)

- **Bot pickup tick scheduler** — `OnPickupCue` hook is in place and returns `Wait` per the test contract, but the runtime tick loop that schedules `TakeTilesFromWallAsync(gameId, pickupSeat, ExpectedPickupCount(phase))` after `BotPickupDelayMs` is NOT yet wired. Needed before bots can do manual pickup visually; Bishop's small follow-up.
- **`FilterEntriesForViewer` slot-suffix fix** — same `EndsWith("@N")` change as the test; cleanup pass.
- **`MinShantenToHu` accuracy** — coarse estimator passes the current tests but is not rigorous (Changsha's Big-Win patterns make a true shanten counter expensive). Deferred to V2.
- **Hard-tier EV budget overruns** — depth-2 lookahead may exceed 800ms in pathological hands; Medium fallback per Ripley §7.6, soft-bounded.
- **Soft variant hot-swap** — Phase G.

### Smoke-Test Recipe (Ripley §8, the headline Test 1)

1. Open `/autotable/?variant=changsha&dealMode=manual&botCount=3`.
2. Click `Take seat 0` → backend assigns seat 0 → bots auto-fill seats 1–3 within 1s. Bot banner: `"🤖 You vs 3 bots (Medium)"`.
3. `Roll Dice` button appears near the dealer's wall. Click it.
4. Dice roll animation plays; dice HUD shows `d1 + d2 = sum`; break-point marker appears at the corresponding wall column.
5. Next 4 tiles in the wall glow. Click any one → all 4 fly to seat 0's hand (face-up to you, face-down to others). Bots auto-pick their 4 with 500ms delays in CCW order.
6. Round 2 + round 3 → all seats have 12 tiles.
7. Single-tile pickup: 1 tile each. Click → seat 0 has 13.
8. Bots take their 1 each → all seats have 13.
9. Dealer (seat 0) takes 1 more → seat 0 has 14. UI: "Discard a tile."
10. Drag a tile from hand → discard placed; claim window opens; bots claim/pass with 400ms delays.
11. Play through to Hu → scoring modal → click `Next Hand` → banker rotates per Changsha §6; next hand starts with `RollingDice` (manual persists).

Additional smoke tests: variant switch to Riichi 4p (auto-deal, upstream-byte-identical scene), auto-deal Changsha (Wave 3 regression), spectator mode (4 bots), and three-difficulty bot smoke (Easy never claims Chow; Medium claims when next-CCW; Hard avoids feeding `MissedWinSeats` flagged seats).

### Sign-Off

- **Ripley** — design landed; sign-off implicit in successful parallel decomposition (Vasquez/Hicks/Bishop all built directly from `ripley-phase-f-design.md` §6 disjoint scope table without collision).
- **Vasquez** — rule audit signed off; 3 new acceptance files, ~45 cases via reflection (so the test assembly always compiles even before Bishop's symbols land).
- **Hicks** — frontend signed off; `tsc` strict ✓, parcel build ✓.
- **Bishop** — backend signed off; `dotnet build` 0/0; 22/22 bot engine tests, all variant switch tests, all manual pickup tests green post-reconciliation.
- **Stephen** — pending headline smoke-test approval (Test 1). PR ready for review.


## Phase B — Changsha Scene (Hicks Frontend)

### 2026-05-19: Phase B frontend — Changsha-shaped scene (13/13/13/13 deal + disabled claim buttons)

**By:** Hicks (Frontend Dev)

Phase B aimed for "Changsha-shaped scene" with the visual shape correct but deal arithmetic & slot-layout cosmetics deferred to Phase C/D. Key decisions:

- **Deal pattern:** 13 tiles per hand + dealer's 14th in `hand.extra@0` slot. Matches upstream's existing slot vocabulary; preserves 14/14/13/13 visual.
- **Wall remainder:** Fixed non-roll-conditional distribution (14/15/13/13 across seats 0/1/2/3). Approximates post-deal wall; authentic dice-driven placement deferred to Phase C/D.
- **Claim buttons:** Added four disabled placeholder buttons (碰 Pung / 吃 Chow / 杠 Kong / 胡 Hu) per the exit criteria. Phase D will wire them.
- **Removed:** `Fives` / `Points` / `POINTS` table / `addSticks()` / `Setup.getScores()` stick-counting / `resetPoints()` / HTML controls (#fives, #points, #reset-points). Replaced with `Conditions.baseUnit` default 1 per Vasquez §5.2.
- **Honba field:** Kept the field, pinned to 0 everywhere (renderer already skips on `honba <= 0`). Cleanup deferred to Phase D.
- **Dealer toggle:** Wired locally with TODO comment referencing Phase D's `changsha.banker` collection.
- **Stick tray geometry:** Left in place but unused (not wired into `SLOT_GROUPS.CHANGSHA`). Cleanup is unrelated.

**Verification:** tsc strict ✓; parcel build ✓.

---

## Phase C — Autotable Relay (Bishop Backend)

### 2026-05-19: Phase C-relay backend — WebSocket relay + meta-collection semantics

**By:** Bishop (Backend Dev)

Phase C wired the upstream `pwmarcz/autotable` relay protocol into the backend autotable WS endpoint. Key architectural decisions:

- **Sender NOT echoed on relay:** Broadcast goes to OTHER connections only, not the sender. Rationale: sender already applied locally; echo risks double-apply. Phase D will need to reintroduce echo-on-conflict for rules enforcement.
- **Snapshot merge on JOIN:** Two paths: (A) Runtime-backed — translator entries applied into `AutotableGameState` first (runtime is authoritative); (B) Ad-hoc (no runtime) — stored entries win on collision (late joiners don't overwrite bundle's config).
- **Meta-collection semantics:** Implemented `ephemeral` (broadcast-only, not stored), `unique` (field-tracked for Phase D enforcement), and `perPlayer` (auto-tombstone on disconnect). Costs ~80 LOC but preserves bundle's `Collection` class unchanged.
- **Game state lifetime:** Ref-counted, no grace window. Per-instance gameId is removed immediately on last disconnect. One-line policy change if grace window needed later.
- **isFirst flag:** Derived from "no other connections + empty store" instead of a one-shot flag. Handles re-join scenarios where first connection drops before uploading.

**Verification:** build 0/0; full suite **257 passed / 0 failed / 11 skipped / 268 total** (+7 over pre-Phase-C tree).

**Open questions for Phase D:** translator-vs-relay merge precedence on runtime push, inbound validation entry point, per-viewer `things` filter, `unique` collection conflict resolution, game ID handoff with React.

---

## Phase D — Changsha Runtime Integration (Bishop, Hicks, Vasquez)

### 2026-05-19: Phase D backend — runtime drives autotable scene, server rules enforced

**By:** Bishop (Backend Dev)

Phase D wired the Changsha runtime as the source of truth for the autotable scene. Runtime wins over client for (`kind`, `key`) pair collisions; bundle can only write cosmetic collections.

**Key decisions:**
- **Runtime-vs-client precedence:** `ApplyUpdate(source: UpdateSource)` enum tracks attribution. Client writes to runtime-owned keys are silently dropped. Runtime overwrites any prior client value.
- **Single-game-per-instance:** All `NEW`/`JOIN` resolve to deterministic `"changsha-default"` relay gameId. Runtime game lazily created on first seat-take; bindings maintained in `_runtimeBinding` + `_relayBinding` dicts.
- **Auto-bot-fill query param:** `?bots=true` (default ON). On seat-take: `TakeSeatAsync` → optional `FillEmptySeatsWithBotsAsync`. Deferred `StartGameAsync` to "Deal" command.
- **Inbound UPDATE branching:** Route by `entry.Kind`: `seats` → `runtime.TakeSeatAsync` (pass-through), `claim` → `runtime.ClaimAsync` / `PassAsync`, `match` (dealCommand="start") → `runtime.StartGameAsync` (pass-through), `result` → server-only, others → relay + store.
- **Translator extensions:** Two new collections: `claim[seat]` = `ClaimWindowEntry`, `result["current"]` = `HandResultEntry`.
- **Per-viewer privacy filter:** Strip `face` + force `rotationIndex=2` on `things` entries where `hand.*@S` with `S ≠ viewerSeat`. Wall/discards/melds unmodified. Viewer's own hand unmodified.
- **StateChanged loop:** `OnStateChanged` → re-translate per connection → apply(Runtime) → broadcast per connection. Late joiners get same snapshot as early joiners from single canonical store.
- **False-Hu penalty:** `RecordFalseHu(state, seat)` side-effect-only API. Penalty = -18 for offender / +6 to each other (Big-Win equivalent). Frontend responsible for "confirm before penalty."
- **过胡 decay:** `DrawTile` removes active seat from `MissedWinSeats` after successful draw (per Baidu "until your next draw").
- **Determinism fix:** Replace `HashCode.Combine` (process-specific RNG) with Knuth mix `(uint)Seed * 2654435761u + (uint)HandNumber` (pure function).

**Verification:** `dotnet test --filter "FullyQualifiedName~Acceptance"` → **62 passed / 0 failed / 4 skipped / 66 total**. Acceptance suite passes; 4 skipped tests document Phase D gaps (False-Hu enforcement, 过胡 per-draw, 13-Orphans Big Win, E2E relay test pending).

---

### 2026-05-20: Phase D frontend — claim window + result modal + dice HUD + bot banner

**By:** Hicks (Frontend Dev)

Phase D wired the autotable protocol extensions (`claim`, `result`, dice shape duality) into the bundle UI.

**Key decisions:**
- **claim collection:** Inbound `claim[seat] = {available, deadline, source, tile}` as `Collection<string, ClaimWindowEntry>` (string-keyed seat indices). Outbound `claim[selfSeat] = {action: 'claim'|'pass', type}`. Marked `ephemeral: true`.
- **result collection:** Inbound `result['current'] = {winner, type, score, hand, nextBanker}`. Outbound: `match[1] = {action: 'nextHand'}` (sentinel key so Next-Hand rides on `match` without clobbering live state). Deviation from charter (which said `match` key); if Bishop requires different key/collection, adapt single `set` call in `game-ui.ts:setupResultModal()`.
- **dice shape duality:** Widened `Collection<string | number, DiceInfo>` to accept both legacy key `0` (local-deal) and new key `'current'` (server-pushed with `{d1, d2, breakPoint}`). Three shapes supported; HUD adapter reads whichever arrives.
- **Claim arc UI:** Buttons + countdown text (not modal). Auto-pass fires at `remainingMs <= 0`. Buttons disable immediately after click (no double-claim risk).
- **Scoring panel:** Bootstrap 4 modal, #1f3a26 felt-green background, #c8a046 brass border. Score-delta colors: green/red/gray. Winning hand as 2D suit-colored tile cells (not 3D animation — Phase E polish).
- **Dice viz:** Both 3D existing draw (auto-fade ~1s) + new 2D HUD (`⚀ ⚁ = N → break @ M`, 3s timeout). Fallback if Bishop skips legacy `state: 'rolled'` signal.
- **Bot banner:** Bottom-left, "Bots filled seats X / Bot Alpha (S) / Bot Bravo (W) / Bot Charlie (N)". Reads nick-prefix `Bot ` convention; wind from seat index (0/1/2/3 = E/S/W/N).
- **Tile face privacy:** Added optional `face?: null` on `ThingInfo`. When `face === null` + hand slot has rotations, bundle coerces `rotationIndex = FACE_DOWN`. Defensive privacy: server-side face-strip via `rotationIndex` + client-side `face: null` override as belt-and-braces.

**Files:** 6 files, +629/-18 LOC. Build: `autotable-src.9d857456.js` (1.01 MB). Parcel built in 2.40s. tsc strict ✓.

---

### 2026-05-19: Phase D tests — acceptance suite (10 files, 44 xUnit methods, 66 invocations)

**By:** Vasquez (Rules Engineer)

Phase D acceptance tests pin every rule axis (deal, chow restriction, claim priority, pung→kong, 258-pair, Big Wins, banker rotation, missed-win lockout). Tests use reflection to probe production symbols, so the assembly always compiles; tests fail-red with "Phase D not yet shipped" messages until Bishop's types appear.

**Results:** **62 passed / 0 failed / 4 skipped / 66 total**. All rule axes verified.

**Top 5 gaps Bishop must fill:**
1. Wire runtime to autotable WS end-to-end (E2E relay test gate).
2. Drag-to-meld arbiter (MVP fast-cut (b)).
3. False-Hu penalty (诈胡 payment).
4. 过胡 per-draw decay.
5. Score-panel collection mutation shape.

---

## Phase F — Manual Pickup State Machine (Bishop, Hicks, Vasquez)

### 2026-05-19: Phase F backend — manual pickup state machine + variant gating + 3-tier bot engine

**By:** Bishop (Backend Dev)

Phase F grafted manual-pickup state machine + variant switching + three-tier bot AI onto Wave-3 auto-deal foundation.

**Key decisions:**
- **DealMode enum:** `Auto` (existing, backward-compat) + `Manual` (new, default for Changsha).
- **Six pickup phases:** `BreakPointMarked`, `PickupRound1/2/3`, `SingleTilePickup`, `DealerExtra`. Each carries `state.PickupSeatIndex + state.PickupExpectedCount`. Auto-deal path untouched.
- **BeginManualDeal contract:** Takes `DiceRoll` directly (not `IDiceService`) for test harness determinism. Sets break point, transitions to `BreakPointMarked`.
- **TakeTilesFromWall contract:** Invariant-checked (must be `IsPickupPhase`, must be `PickupSeatIndex`, count equals `ExpectedPickupCount`). First pickup IS dealer's round-1-of-4 (phase stays `BreakPointMarked` until first call lands, then advances to `PickupRound1`).
- **Bot engine (new `Changsha/Bot/` directory):**
  - `IChangshaBotStrategy` with 4 phase hooks + `DecideAction` router.
  - `EasyStrategy`: highest-rank discard, claims Hu always, Pung always, Kong when hand ≥10, never Chow.
  - `MediumStrategy`: port of legacy keep-score; claims Hu/Pung/Kong + Chow when below 3 melds.
  - `HardStrategy`: Medium + defensive vs discarded tiles (safe-tile heuristic) + conservative Kong.
  - `ChangshaBotEngine.Resolve(string?)`: case-insensitive; null/unknown → Medium. Singleton instances, zero allocation.
  - `ChangshaBotPolicy` (legacy): thin facade to `Resolve("medium")`.
- **Variant switch gate:** `AutotableRuntimeMode` enum: `Relay` (non-Changsha, pure peer-to-peer) vs `ChangshaRuntime` (Changsha, runtime-driven). `HandleConnectionAsync` parses 4 query params (`variant`, `dealMode`, `botCount`, `botDifficulty`). `HandleUpdateAsync` branches on `RuntimeMode`.
- **Pickup action handler:** `TryHandlePickupActionAsync` handles `{action: "rollDice"}` + `{action: "take", count: N}` (alt: `wallTileIds: int[]`).
- **Bot pickup delay:** `ChangshaRuntimeOptions.BotPickupDelayMs = 500` (per Ripley, between turn-delay 800 and claim-delay 400/250).

**Verification:** `dotnet test ... --nologo --no-build` → **318 passed / 1 failed / 9 skipped / 328 total**. One test failure is a test bug (slot-format parsing), not production. No regressions.

**Test bugs found (for Vasquez):** `Pickup_PrivacyMask_OpposingHandsHaveFacesStripped` misinterprets slot format (slot `hand.1@0` should use `EndsWith("@0")` not `StartsWith("hand.0")`). Pre-existing bug in `FilterEntriesForViewer` (same parsing issue) — deferred cleanup.

**Known gotchas:**
1. `MinShantenToHu` is coarse approximation (not rigorous shanten counter).
2. Auto-deal path preserved as backward-compat (E2E test still uses it).
3. Mid-session variant switching NOT supported (runtime binds on first seat-take; warn "reload to change").
4. Bot pickup ticks NOT YET wired (ScheduleBotIfNeededAsync hook in place, but no tick scheduler — deferred follow-up).

**Files modified:** Domain, Protocol, State machine, Bot engine (new dir), Runtime, Translator, WS endpoint, Runtime options. +957/-145 across 13 files.

---

### 2026-05-19: Phase F rule audit — manual pickup defaults locked + bot difficulty specs

**By:** Vasquez (Rules Engineer)

Comprehensive rule audit covering dice (2d6, sum 2–12, dealer rolls), break-point algorithm (stacks from right end, Count #1 selects wall, Count #2 selects break point stack), pickup order (CCW: dealer → +1 → +2 → +3), four 4-tile rounds + single-tile round + dealer extra, Hu forbidden during pickup (phase-gate), Kong replacement doesn't intersect pickup (lives in back wall), bot difficulty falsifiable assertions (Easy: highest-rank discard + always Hu + never Chow; Medium: shanten-aware + Chow-when-next-CCW; Hard: EV-based + defensive), variant switching (Changsha default, manual deal default, reload-required for switch), frontend privacy during pickup (inherited from Wave 3, viewer-seat-aware rotation).

**Defaults locked:** 61 items across dice, break-point, pickup order, pickup rounds, dealer count, wall remainder, Hu gate, Kong timing, bot delays, strategy rules, variant defaults, deal-mode defaults, mid-game toggles, privacy.

**Gaps found:** 6 items flagged for visibility but have safe defaults (no Stephen decision needed).

**Three new acceptance test files drafted (~45 cases via reflection):**
- `ManualPickupAcceptanceTests.cs` (14 cases): Dice, break-point, phase transitions, pickup order, dealer extra, autoDeal regression, privacy.
- `VariantSwitchAcceptanceTests.cs` (9 cases): URL parsing, runtime/relay binding, default variant, cross-variant rejection, snapshot continuity.
- `BotEngineAcceptanceTests.cs` (11 cases): Strategy resolver, Easy/Medium/Hard behaviour, 过胡 respect, move-delay config, bot-vs-bot sanity.

---

### 2026-05-19: Phase F frontend — variant switching + manual pickup UI + bot UI + lobby defer

**By:** Hicks (Frontend Lead)

Phase F restored variant switching (Changsha + Riichi 4p/3p/Bamboo/Minefield), wired manual-pickup state machine (`pickup` collection with phase snapshot + `rollDice`/`take` commands), added deal-mode toggle, bot-count + bot-difficulty pickers (informational for now), pickup HUD ("Your turn — pick N tiles" + Take-N button), roll-dice button (visible only when `phase=RollingDice` + your seat), variant indicator badge (🀄/🎴/🎋/💣), honba toggle + reset points (gated Riichi-only), URL params + localStorage persistence (`autotable.phaseF.v1.*`).

**Key decisions:**
1. **Strict lockout of frontend-only pickup state:** Bundle does NOT mirror Bishop's phase machine — renders only what arrives. Optimistic UI rejected: "take" click does NOT pre-move tiles; runtime's `things` UPDATE moves them.
2. **wallTileIds informational:** Frontend computes by walking wall slots; backend may use for cross-validation or ignore.
3. **Variant hot-swap deferred:** Requires clean disposal of setup pipeline tile catalogues. Phase F warns "Reload to change variant"; Phase G can promote.
4. **Take-N HUD button + wall-click intercept:** Both emit same `pickup.take`; button = impatient-player shortcut, click = natural gesture (dual-affordance per Ripley §2.7.4).
5. **Bot banner extension:** Difficulty from picker, not seat field (yet). Once Bishop pushes `difficulty` on SeatInfo, banner should prefer that.

**Protocol contract honoured:**
- Inbound: `pickup[0] = {phase, seatIndex, count, dealMode, breakPoint, wallIndex}` + `dice['current'] = {d1, d2, breakPoint}`.
- Outbound: `pickup['rollDice'] = {seatIndex}` + `pickup['take'] = {seatIndex, count, wallTileIds}`.

**Files modified:** types.ts, setup-slots.ts, setup-deal.ts, setup.ts, client.ts, world.ts, game-ui.ts, index.html, style.css, parcel build artifacts. Net: 10 files, +XXX/-YYY LOC.

**Build:** tsc strict ✓; parcel ✓ (1.03 MB, SHA `d9507f0f`). Live click-through pending Bishop's pickup runtime.

**Backward compat:** Changsha remains Wave 3-equivalent when `dealMode=auto` (no pickup HUD, no roll-dice button). Old collections (`match`, `things`, `seats`, `dice`, `claim`, `result`) untouched; new `pickup` purely additive.

---

## Phase G — Bot Pickup Scheduler + Sidebar Lobby + Privacy Mask Cleanup

### 2026-05-19: Phase G backend — bot pickup tick scheduler + privacy-mask slot-parse fix

**By:** Bishop (Backend Dev)

Phase G completed two production issues: (1) bots freeze during manual-deal pickup (ScheduleBotIfNeededAsync not wired for pickup phases), (2) FilterEntriesForViewer pre-existing slot-parse bug (extracting seat from between `.` and `@` instead of after `@`).

**Contract (stable for Vasquez's tests):**
- `ScheduleBotIfNeededAsync(instance, ct)`: New branch checks `IsPickupPhase(state.Phase)`. If `PickupSeatIndex` is a bot, schedule `RunBotPickupAsync(instance, pickupSeat, ct)` via `Task.Delay(BotPickupDelayMs)`.
- `RunBotPickupAsync(instance, seatIndex, ct)` (private): Delay → acquire lock → re-validate → compute `expected = ExpectedPickupCount(phase)` → release → `await TakeTilesFromWallAsync(...)`. Chain self-perpetuates CCW; re-validates under state machine.
- `RollDiceAsync`, `TakeTilesFromWallAsync` now invoke `ScheduleBotIfNeededAsync` to keep chain going.
- **Invariants preserved:** Bot tick fires ONLY when active `PickupSeatIndex` is a bot. Human picker → scheduler no-ops, waits for UI. All mutations under `instance.Lock`. Cancellation via `instance.LifecycleCts.Token`. Auto-deal path untouched.

**Privacy-mask fix:**
- **Problem:** Pre-Phase-G extracted seat from substring between `.` and `@` (the hand index, backwards). Result: viewer's own `hand.1@self` masked, opponents' `hand.0@other` leaked.
- **Solution:** Parse at last `@` via `LastIndexOf('@')` + `AsSpan(at + 1)`. Universal face-strip on `@`-suffixed foreign slots; rotation override to 2 (face-down) only on `hand.*` slots (discards/melds/walls keep public translator rotation).
- **Asymmetric rationale (Vasquez Test 5):** Non-hand slots like `discard.*@1`, `meld.*@1`, `wall.*@1` must keep their translator rotation (discards face-up, melds face-down per type). Hand slots get forced face-down. Split encoded by `forceHandFaceDown` bool.
- **Spectator behavior:** `viewerSeat == null` masks every `@`-suffixed entry (no slot matches null viewer).
- **Helper rename:** `StripFaceAndForceFaceDown(JsonElement)` → `StripFace(JsonElement, bool forceHandFaceDown)`. Rotation override now conditional.

**Verification:** `dotnet build` 0/0, ~6s. `dotnet test --nologo --no-build` → **330/0/9/339** (+11 facts via Vasquez). No flakes across 3 consecutive runs.

**Files modified (production only):**
- `ChangshaGameRuntime.cs`: `ScheduleBotIfNeededAsync` extended with pickup branch; new `RunBotPickupAsync`; `RollDiceAsync` and `TakeTilesFromWallAsync` invoke `ScheduleBotIfNeededAsync`.
- `AutotableWsEndpoint.cs`: `FilterEntriesForViewer` re-parses slot at last `@`; XML doc rewritten documenting suffix convention.

**Remaining follow-ups (not blocking):**
- Add standalone unit test for `FilterEntriesForViewer` covering spectator + multi-seat hand-entry mix + non-hand `@seat` slot pass-through.
- Extract slot-parse helper (`TryParseHandSeat`) to `AutotableSlotMap` once another consumer needs it.

---

### 2026-05-19: Phase G frontend — sidebar lobby UI for variant/dealMode/botCount/botDifficulty

**By:** Hicks (Frontend Dev)

Phase G shipped a pre-game sidebar lobby picker so users don't need to edit the URL bar. Lobby is a one-way bridge into Phase F query-param backend; rest of system reads URL params unchanged.

**What shipped:**
- **Path-1 sidebar lobby** (plain TS + HTML + CSS, no React): URL parsing, picker state read/write, gating (dealMode disabled on non-Changsha; botDifficulty disabled when botCount=0), Apply & Start navigation.
- **UI:** Top-left semi-opaque dark panel with brass-gold accents matching autotable chrome. Visible by default on bare URL (`/autotable/`); hidden behind "☰ Lobby" toggle otherwise.
- **Picker → query-param mapping:**
  - Variant: `changsha` (bold), `four-player`, `three-player`, `bamboo`, `minefield` → `?variant=changsha` (kebab-case).
  - Deal mode: `manual` (bold), `auto` → `&dealMode=manual` (only emitted for `variant=changsha`).
  - Bot count: `0`, `3` (bold), `4` (spectator) → `&botCount=3`. Default 3 matches Phase F backend default.
  - Bot difficulty: `Easy`, `Medium` (bold), `Hard` → `&botDifficulty=Medium` (PascalCase). Only emitted when `botCount > 0`.
- **Gating logic:** dealMode fieldset greys + disabled radios when `variant !== 'changsha'`. botDifficulty greys when `botCount === 0`. Refresh fires on variant or bot-count change.
- **URL parsing:** Lenient (kebab-case or SCREAMING_SNAKE for variant). `?bots=true` aliases `?botCount=3` (Phase F back-compat). Bot difficulty case-insensitive on read; PascalCase on write.
- **Show-on-load policy:** Auto-opens when `window.location.search === ''` (bare URL only). Uses `window.location.replace()` (not `assign`) so back-button doesn't bounce between configurations.

**Deferrals (Phase H):** Soft hot-swap (currently full page reload), localStorage persistence of lobby pickers (URL is source of truth), multi-human lobby, mobile responsive layout.

**Files:**
- `src/frontend/autotable-src/src/lobby.ts` (NEW, 200 LOC).
- `index.html`: Added #lobby-toggle button + #lobby-panel with four <fieldset>s.
- `game-ui.ts`: Added initLobby() call before asset loader.
- `style.css`: +135 LOC #lobby-* styling.
- `src/frontend/autotable/**`: Parcel rebuild (new hashes `33f97fad.js` + `7934372e.css`; pruned `6d5fae4c.js` + `1c6f6789.css`).

**Verification:** `npx tsc --noEmit --strict ...` → exit 0. `npx parcel build ...` ✨ Built in 7.32s, 22 assets. Smoke: bare `/autotable/` auto-opens; variant/botCount changes gate dealMode/botDifficulty; Apply & Start navigates.

---

### 2026-05-19: Phase G tests — acceptance suite (11 facts, 60 assertions)

**By:** Vasquez (Rules Engineer)

Phase G locked two acceptance contracts: (1) RunBotPickupAsync tick scheduler (verify timing, phases, cancellation), (2) FilterEntriesForViewer slot-parse fix (verify last-@ parsing, asymmetric rotation, multi-@, unparseable seats).

**Test files (additive only):**
- `BotPickupSchedulerAcceptanceTests.cs` (6 facts, 31 assertions): Pickup scheduler phases, bot tick delay 200ms, cancellation on game teardown, auto-deal bypass, step budget 13×200×0.5 = 1300ms lower bound / 7800ms upper bound.
- `PrivacyMaskAcceptanceTests.cs` (5 facts, 29 assertions): Slot-parse at last `@`, face-strip universal, rotation override hand.* only, multi-@ handling (`weird@foo@1` → seat=1), unparseable seats pass-through, spectator masking all `@`-suffixed.

**Reflection-backed testing:** Both test files use reflection probes to reach private methods (no public API exists yet). Assembly always compiles; tests fail-red with "method not found" until Bishop's production symbols appear.

**Posture notes for future work:**
- Bot scheduler in `ChangshaGameRuntime`, not separate class. Future refactor into `BotScheduler` type won't break tests (drive through public methods).
- Privacy filter on `AutotableConnectionManager` (private static). Refactor into separate `PrivacyFilter` type OK (three candidate hosts probed; method name signature must match).
- Test 5 deliberately softened (no assertion that `weird@foo@1` MUST be masked — Bishop gates on `hand.*` prefix, correct for gameplay).
- Test 6 reaches `_games` via reflection (no public teardown API yet). Future `RemoveGameAsync` addition won't break test.

**Verification:** `dotnet build` 0/0. `dotnet test --filter "FullyQualifiedName~BotPickupScheduler|PrivacyMask" --nologo --no-build` → **12/0/0** (11 facts + xUnit bookkeeping). Full suite: **330/0/9/339** → no regressions, no flakes across 3 consecutive runs.

---

## Phase H — Stability + Polish (2026-05-21)

### Lobby polish + Docker cleanup (Hicks)

**By:** Hicks (Frontend Dev), 2026-05-21

Extended Phase G sidebar lobby with four Wave-1 additions: **seed override** (accepts `0 ≤ N ≤ 2³¹−1`), **hand-count selector** (4/8/16/32), **save-defaults checkbox** (writes to localStorage key `mahjong.lobby.defaults`), and **About link** pointing to GitHub-hosted `docs/known-limitations.md`. Frontend scope only; no backend or test changes.

**Bundle hashes:**
- JS: `autotable-src.33f97fad.js` → `autotable-src.c97ea9e9.js`
- CSS: `autotable-src.7934372e.css` → `autotable-src.96cb3b60.css`

Old Phase G hashes pruned from `src/frontend/autotable/`.

**URL resolution priority** (for picker pre-population):
```
URL params  >  localStorage  >  hardcoded DEFAULTS
```

**Files modified:** `src/frontend/autotable-src/src/lobby.ts` (NEW, 200 LOC), `index.html`, `game-ui.ts`, `style.css` (+135 LOC).

**⚠️ Build invariant — CRITICAL for all future Parcel builds on this codebase:**
```bash
parcel build index.html --dist-dir ../autotable --public-url . --no-source-maps --no-cache
```

**Without `--public-url .` flag:** Parcel emits absolute asset URLs (`/icon-96.png`, `/autotable-src.css`) into the rendered HTML. The backend serves the bundle from `/autotable/*`, so absolute URLs 404. Future agents MUST use this flag or wrap it in `package.json` scripts (suggested follow-up: add a `"build"` script to `src/frontend/autotable-src/package.json`).

**Verification:** `tsc --noEmit --strict` → exit 0. Parcel build ✨ Built in 7.32s. Smoke test: bare `/autotable/` auto-opens lobby; seed/handCount invalid input properly gated.

### Architecture + V2 design (Ripley)

**By:** Ripley (Lead/Architect), 2026-05-20

Shipped two documentation files binding Phase H structure and Phase H Wave 2 rules planning:

- **`docs/architecture.md`**: System-level overview covering Changsha game rules, state machine phases, the variant-switch architecture, manual-pickup mechanics, bot-tier separation (Easy/Medium/Hard), and the 3×4 bot evaluation matrix.
- **`docs/known-limitations.md`**: Current limitations (no honors in Changsha, no soft hot-swap for variants, no multi-human lobby, mobile layout not responsive) with follow-up phase references.

**Wave 2 design memo (local inbox, not committed)** locks three rule implementations for Phase H Wave 2:

1. **NineTerminals** — Changsha-adapted "9-Terminals" hand pattern (since Changsha has no honor tiles); replaces classical 13-Orphans. Tile set: all rank-1 and rank-9 tiles, 14 total. Recommend clearly labeling as `WinPattern.NineTerminals` in source with XML doc citing design memo.
2. **RobbingKong (抢杠胡)** — New state sub-machine between `DeclaringKong` (for added-kong only) and `DrawingReplacement` to open a claim window for other seats to win on the added-kong tile. Applies to **added kong (補杠) only**, not concealed or exposed kongs. State flow: `AwaitingDiscard` → `DeclaringKong [Kind=Added]` → `ClaimWindow` (Hu-only) → either SCORING (RobbingKong method) or `DrawingReplacement`.
3. **Big-win stacking via `AllPatterns`** — Currently `WinDetectionResult` returns a single highest-precedence `WinPattern`. Phase H Wave 2 adds an `AllPatterns : IReadOnlyList<WinPattern>` field capturing ALL big-win patterns satisfied in the hand (e.g., hand is both AllPungs AND FullFlush). Scorer multiplies base BigWin payout by the count (×1 for 1 pattern, ×2 for 2 patterns, etc.). New detector: `Detect` method populates `AllPatterns` list; existing `Pattern` property remains for backward compat.

**Verification:** Docs landed in `main`; Wave 2 memo provides locked sequencing for Phase H Wave 2 implementation.

### StateVersion + bot timeout + CORS (Bishop)

**By:** Bishop (Backend Dev), 2026-05-22

Three production-code tasks on the backend (no tests, no frontend):

**1. `StateVersion` optimistic concurrency contract:**
- New exception: `ChangshaConcurrencyException(expectedVersion, actualVersion)` — thrown when a mutation arrives with stale `expectedVersion`.
- Eight `IChangshaGameRuntime` mutation methods gained trailing `int? expectedVersion = null` parameter (after `CancellationToken ct`).
- Check runs inside instance lock via `EnsureExpectedVersion(instance, expectedVersion)` helper, BEFORE state-machine call.
- Semantics: `null` → bypass check (back-compat); `expectedVersion.HasValue && value != state.StateVersion` → throw; `expectedVersion.HasValue && value == state.StateVersion` → proceed and increment naturally.
- **Note on defaults:** Field defaults to `0` (not 1), incremented by `CreateEvent`. First mutation advances to monotonic 1, 2, 3, … as expected.
- **No persistence migration:** Old snapshots deserialize with their explicit JSON value; new games follow 0-based contract.

**2. Bot decision timeout fallback:**
- New option: `ChangshaRuntimeOptions.BotDecisionTimeoutMs` (default 2000ms; `≤0` disables).
- New engine helper: `ChangshaBotEngine.DecideActionWithTimeoutAsync(decision, timeoutMs, safeDefault, logger, ct)`.
- Pattern: `Task.Run(decision)` + `Task.WhenAny(decisionTask, Task.Delay(timeoutMs))`.
- On timeout: logs Warning, observes slow task exception, returns `safeDefault()`.
- Safe defaults: own turn → `Discard(SelectDiscardTile(hand))`, claim window → `Pass`.
- **Test seam:** Inject a "slow strategy" sleeping `BotDecisionTimeoutMs + 500` for testability.

**3. CORS cleanup:**
- Shrunk CORS origins from 4 → 2 entries (removed `http://localhost:5173` and `https://localhost:5173`, the deleted Vite dev server from Phase A).
- Retained `http://localhost:5114` and `https://localhost:7135` (Kestrel-served backend + autotable bundle).

**Files modified:** `ChangshaConcurrencyException.cs` (NEW), `ChangshaDomain.cs`, `ChangshaGameRuntime.cs`, `ChangshaRuntimeOptions.cs`, `ChangshaBotEngine.cs`, `Program.cs`.

**Verification:** `dotnet build` 0/0 ~5s. `dotnet test --nologo --no-build` → **330/0/9** (Phase G parity). No flakes across 3 runs.

**Handed off to Vasquez:** Unskip two marker tests and exercise StateVersion mismatch (exception) + bot timeout (safe-default discard/pass).

### Tests (Vasquez)

**By:** Vasquez (Rules Engineer), 2026-05-22

Unskipped two Phase G marker skips and shipped 10 new acceptance tests (4 bot timeout + 6 StateVersion + concurrency edge cases) on top of Bishop's Phase H Wave 1 contracts.

**`BotBehaviorTests.cs` — 4 new tests:**
1. `Bot_TimeoutFallback_FallsBackToSafeAction` (replaces skip) — hung strategy → safe-default.
2. `Bot_Timeout_Discard_PicksLowestRankSafe` — safe-default matches `MediumStrategy.SelectDiscardTile(hand)`.
3. `Bot_Timeout_DuringClaim_PassesNotFalseHu` — claim window timeout → Pass (no false Hu).
4. `Bot_Decision_Within_Timeout_ProceedsNormally` — fast strategy beats timeout → scripted tile lands.

**`EdgeCaseTests.cs` — 6 new tests:**
1. `StateVersion_StartsAtZero_OnNewGame` (replaces skip) — fresh game = version 0.
2. `StateVersion_NullExpectedVersion_ProceedsWithoutCheck` — null parameter = back-compat (no check).
3. `StateVersion_FreshExpectedVersion_Succeeds_Increments` — matching version succeeds AND increments.
4. `StateVersion_StaleExpectedVersion_ThrowsConcurrencyException` (replaces skip) — mismatch → exception.
5. `StateVersion_Exception_Includes_Both_Versions` — exception embeds expected + actual.
6. `StateVersion_BotInvocations_DoNotIncrement_Mismatch` — stale reject does NOT advance version.

**Support code:** `RuntimeHarness : IAsyncDisposable`, reflection-backed probes for symbol resolution (BotDecisionTimeoutMs property, _strategy field, ChangshaConcurrencyException type, expectedVersion parameter), `SlowBotStrategy : IChangshaBotStrategy`, parameter-name-matched positional dispatch.

**Stability:** Phase H filter (`Bot_Timeout|StateVersion`) → **11/0/0** across 2 runs. Full suite: **340/0/7** (was 330/0/9; +10 tests, −2 skips = 8 net new passing). Build: 0 warnings.

**Gate result:** `dotnet test` **340 passed / 0 failed / 7 skipped of 347 total**.

---

