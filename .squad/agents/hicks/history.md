# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Hicks as Frontend Dev.
- Frontend approach starts from autotable behavior and adds targeted modernization only when it is low-risk.
- Modern frontend now uses a graphical 4-seat table layout with clickable tile faces and automatic bot progression until the next human turn.
- Modern UI now renders through seat-scoped table projections, with a perspective selector and explicit read-only behavior when viewing non-seat-0 hands.
- Modern UI now surfaces claim-window state, highlights precedence-selected opportunities, and exposes seat-0-only pass/take-selected resolution actions that re-enter the bot-to-human gameplay loop.
- Audited upstream autotable deal protocol: deal is entirely client-side (Setup.deal → shuffle → dice roll → break-point → slot placement), broadcast via WebSocket `things`/`match`/`dice` collections. Bundle has dice rendering (Center.drawDice via sprite sheet). No server authority in upstream.
- Audited backend deal: TableStateEngine.CreateInitialState deals atomically (no dice, no break-point, no batch draw). TableGameState has no banker/wind/dice/scoring fields.
- Audited modern React frontend: 943-line App.tsx with full playable loop (create table, discard, claim resolution, bot advance). No Changsha-specific UI (no dice modal, banker badge, wind indicator, or fan panel). Talks to backend via REST only.
- Produced Changsha frontend UX plan at docs/rules/changsha-frontend-plan.md. Recommended Option B: backend-authoritative deal with autotable as 3D viewport via WS bridge, Changsha chrome (dice, banker, scoring) in React Fluent UI panels. Five-phase roadmap from dice+banker components through full bridge interactivity.

📌 Team update (2026-05-05T17-00-21Z): Frontend plan decision merged to `.squad/decisions.md`. Vasquez completed Changsha canonical spec at `docs/rules/changsha-spec.md`. Bishop completed backend gap audit at `docs/rules/changsha-backend-gap.md` with 10-item roadmap and 38/38 tests passing. Hudson completed test catalog with 80 scenarios and 8 rule contradictions at `docs/rules/changsha-test-catalog.md`. Phase 1 (UI components) ready to start immediately; Phases 2 & 4 blocked on Bishop endpoint confirmation and Vasquez fan table delivery.

📌 Phase 1 implementation complete (changsha-v1 branch). Built 7 Changsha Fluent UI components + mock state hook + table page at `/changsha` route. Components: DiceRollModal (animated dice + break-point), BankerBadge, RoundWindIndicator, ChangshaHud (scores panel), FanBreakdownPanel (win pattern + payment table), PlayerHandPanel (Unicode tile glyphs + discard buttons), ClaimPromptModal (5s countdown). Types reconciled with Bishop's SignalR contract (numeric tile IDs 0-107, DiceResult/BreakPoint/MeldState/WinResult/ScoreResult/PaymentEntry, GamePhase enum). Dev-only demo controls cycle through all phases. 6 commits, build passes. Vitest not configured — tests skipped. Phase 2 deferred: live SignalR wiring, autotable iframe embed, real tile images, WS bridge.
