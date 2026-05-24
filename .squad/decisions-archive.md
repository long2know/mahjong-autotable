# Squad Decisions

## Active Decisions

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
