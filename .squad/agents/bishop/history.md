# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Bishop as Backend Dev.
- Backend priorities: game state APIs, rule engine interfaces, and provider-flexible persistence.
- Added initial bot-play backend slice: 4-seat typed table state (human/bot), table create/get APIs, and deterministic bot advance endpoint with persisted `StateJson` + `StateVersion` for extensibility.
- Added backend unit tests around bot state engine behavior to lock current deterministic placeholder semantics while rules engine work remains pending.
- Replaced placeholder bot mutation with an authoritative draw/discard loop backed by seeded wall state, per-seat hands, discard pile tracking, and phase-aware turn semantics.
- Added strict human discard validation endpoint and routed bot advancement through the same discard application path to keep server authority and invariants aligned.
- Added action-sequence/state-version progression rules, canonical state hashing, and structured contract error responses (including optimistic concurrency conflicts) for discard and bot orchestration endpoints.
- Added replay verification support that re-simulates accepted discard actions from stored seed and compares canonical state hashes for deterministic integrity checks.
- Added durable append-only `TableSessionEvents` persistence, event-stream retrieval API, and state-hash stamping on emitted actions to improve replay/integrity auditability.
- Added replay-governance enforcement: mutating endpoints now run preflight replay checks and reject invariant mismatches as `STATE_INVARIANT_BROKEN`; replay verify supports strict conflict mode.
- Replaced the minimal modern placeholder page with a backend-driven control panel for table creation, human discard actions, bot advancement, replay verification, and persisted event inspection.
- Added explicit bot-advance API support for "run until next human turn or wall exhaustion" to remove client-side `maxActions` tuning from the primary gameplay loop.
- Added claim-window scaffolding metadata on discard state transitions with deterministic precedence selection (`hu > kong > pung > chow`) to prepare for upcoming claim-resolution flows.
- Added seat-scoped table projection contracts (`/api/tables/{id}/view?seatIndex=`) that hide opponent tiles and wall contents for privacy-safe multiplayer clients.
- Replaced bot minimum-tile discards with deterministic hand-shape heuristics so bots preserve pairs/sequences more realistically.
- Upgraded claim scaffolding into executable backend actions: discard now pauses on claim opportunities, `/api/tables/{id}/claims/resolve` applies deterministic `pass`/`take-selected` outcomes, and replay/integrity checks include claim-resolution actions.
- Completed Changsha backend gap audit: 3/18 behaviors implemented, 5 partial, 10 missing. Largest gaps are tile set (136 → 112), 红中 wildcard, win patterns, scoring, self-draw win, and banker rotation. 10 ordered work items documented in `docs/rules/changsha-backend-gap.md`. Build green, 38/38 tests pass (0 Changsha-specific).

📌 Team update (2026-05-05T17-00-21Z): Backend audit decision merged to `.squad/decisions.md`. Vasquez completed Changsha canonical spec at `docs/rules/changsha-spec.md` (108 tiles, dice break, batch deal, 258 pair rule, no dead wall). Hicks produced frontend plan with Option B selected (backend-authoritative + autotable viewport + Fluent UI) at `docs/rules/changsha-frontend-plan.md`. Hudson identified 80 test scenarios with 8 contradictions at `docs/rules/changsha-test-catalog.md`. Blockers on `/autotable/ws` endpoint confirmation and fan table delivery from Vasquez.

📌 Changsha v1 implementation wave (Bishop):
- Implemented full Changsha v1 backend in `Changsha/` namespace under `Mahjong.Autotable.Api`:
  - **Domain layer**: Tile/Suit/Wind enums, Meld, WinResult, ScoreResult, ChangshaGameState types
  - **ChangshaDeckBuilder**: 108-tile deck (3 suits × 9 ranks × 4 copies), tile ID 0–107
  - **DiceService**: 2d6 deterministic via seeded RNG
  - **BreakPointService**: wall selection and break point per spec §2 (counterclockwise count, right-end stack count)
  - **DealService**: batch-of-4 deal, dealer gets 14, others get 13, 55 remaining
  - **ChangshaWinDetector**: 4 patterns — Standard (258 pair rule), Seven Pairs, All Pungs, Full Flush
  - **ScoringService**: Small/Big Win payment calculator per spec §5 (1/2, 3/4, 6/7 tables, flush doubling)
  - **ClaimAdjudicator**: hu > kong = pung > chow priority, chow next-seat only
  - **ChangshaGameStateMachine**: pure-functional event-sourced transitions through all game phases
  - **ChangshaHub**: SignalR hub at `/hubs/changsha` with skeleton event/command structure
  - **ChangshaBotPolicy**: heuristic discard/claim/win AI, tested for legal play
  - **Persistence**: ChangshaGame + ChangshaGameEvent entities, AppDbContext config, DatabaseBootstrapper tables
  - **SignalR contract**: `docs/rules/changsha-signalr-contract.md` — TypeScript interfaces for Hicks
- 68 new service-level unit tests in `ChangshaServices/` (all passing)
- Build: 0 warnings, 0 errors; Tests: 106 passed, 0 failed, 77 skipped (Hudson's awaiting integration)
- Deferred to v2: bird-catching, ready-kong dice, instant wins, pao chains

📌 Changsha v1 Phase 2 — full hub lifecycle (Bishop, branch stlong/changsha-v1-phase2):
- New `IChangshaGameRuntime` singleton service (`Changsha/Runtime/`) that owns in-memory `ChangshaGameInstance` records, drives the full state-machine lifecycle, schedules bot decisions (350 ms turn / 250 ms claim / 5 s window), opens & resolves claim windows with priority adjudication, and persists JSON snapshots to `ChangshaGames` after every transition via `IServiceScopeFactory`.
- Rewrote `ChangshaHub` as a thin command dispatcher; implemented every command from `docs/rules/changsha-signalr-contract.md`: CreateGame, JoinTable, TakeSeat, FillWithBots, StartGame, AcknowledgeDeal, Discard, Claim, Pass, DeclareKong, DeclareWin, ReconnectGame.
- Wire-shaped events emitted to clients: GameCreated, PlayerSeated, GameStarted, DiceRolled, BreakPointSet, TilesDealt (4 batches × 4 seats with seat-private tile id routing), TurnStarted, TileDrawn, TileDiscarded, ClaimWindowOpen, ClaimMade, KongReplacementDrawn, WinDeclared, ScoringComplete, BankerRotated, RoundChanged, HandFinished (via ScoringComplete isDraw), GameEnded, FullState.
- Reconnection: `ReconnectGame` re-binds the connection ID to the seat and emits a `FullState` snapshot with seat-private concealed tiles.
- Determinism: `CreateGame` accepts optional `seed`; subsequent hand dice seeds derive deterministically (`seed + handNumber`).
- CORS policy `ChangshaCors` allow-lists localhost:5173 / 5114 / 7135.
- Three SignalR E2E tests in `tests/.../Hub/`: 4-bot full hand to win/draw, human discard → TileDiscarded, reconnect → FullState. All pass in <3s.
- Build: 0 warnings, 0 errors. Tests: 109 passing (was 106), 77 skipped (Hudson's). Decisions captured in `.squad/decisions/inbox/bishop-changsha-v2-runtime.md`.

📌 Team update (2026-05-08T19:51:39Z): Phase 2 shipped — full ChangshaHub lifecycle with 12 client commands, in-memory game-instance management, claim window resolution with 350ms turn / 250ms claim / 5s timeout, FullState reconnection, wire-event contract compliance, E2E SignalR tests (3 GREEN). Frontend wired live SignalR (reducer pattern + reconnect strategy), built 27-tile SVG component, shipped autotable iframe bridge (one-way parent→child Phase 2). Tests uncovered 2 ScoringService bugs (dealer-aware Small Win payment, Full Flush doubling) — both fixed via commit 9807b70. Final: 179 passed, 0 failed, 7 deferred (v2), 0 build warnings. Branch ready for merge.

## Learnings
- **Dealer-aware payment pattern (Changsha §5.1):** every payment branch — Small Win and Big Win, self-draw and discard — must check `dealerInvolved = (payer == dealer || winner == dealer)` and add the +1 dealer bonus. The SmallWin self-draw branch had previously been written as a flat constant; mirror the BigWin shape across all four branches to keep the table symmetric.
- **v1 no-stacking rule:** Big Win categories (AllPungs, SevenPairs, FullFlush) all pay the same flat amount in v1. No FullFlush doubling, no multi-pattern stacking. Multipliers are deferred to v2; do not reintroduce a `flushMultiplier` or similar without a spec change.
