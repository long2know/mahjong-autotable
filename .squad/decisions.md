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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
