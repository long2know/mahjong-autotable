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
