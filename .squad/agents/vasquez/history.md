# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Vasquez as Rules Engineer.
- Rule priorities: Changsha wall/draw behavior, turn transitions, and compatibility seams for expanded Chinese rules.
- Current repository state is scaffolding-only; `RuleSet = changsha` exists, but no executable game-state machine or action arbitration yet.
- Bot readiness requires one authoritative pipeline for human and bot actions, plus seat-scoped state privacy and replayable deterministic logs.
- Changsha implementation should be frozen as a versioned profile (`changsha-v1`) with explicit ambiguity list before coding transitions.
- First implementation contract should keep draw as server-owned and discard as the only external seat action to avoid turn-race ambiguity.
- A minimal but durable state contract must include seed/algorithm, ordered wall, action sequence, and canonical state hash so deterministic replay is verifiable.
- For the initial slice, ending on live-wall exhaustion is the only supported round termination; claims/kong/settlement stay explicitly out-of-scope.
- Completed canonical Changsha rules spec at `docs/rules/changsha-spec.md` (v1.0, 2026-04-22).
  - Cross-referenced three sources: MahjongPros (primary), Reddit community overview, Baidu/Tencent QQ rules.
  - Key findings: 108 tiles (no honors/flowers), 258 pair rule on standard wins, chow IS allowed (next-seat only), two-tier scoring (small/big win), bird catching post-win mechanic, no dead wall.
  - Current engine uses 136 tiles and generic hu detection — both require critical changes.
  - Deal flow must implement dice roll → break point → batch-of-4 dealing (currently 1-at-a-time).
  - Kong replacement must draw from back of wall (currently front).
  - 11 open questions documented requiring product direction.
  - Decisions recorded to `.squad/decisions/inbox/vasquez-changsha-spec.md`.

📌 Team update (2026-05-05T17-00-21Z): Changsha spec decision merged to `.squad/decisions.md` (Active Decisions section). Backend audit at `docs/rules/changsha-backend-gap.md`, frontend plan at `docs/rules/changsha-frontend-plan.md`, test catalog with 8 contradictions identified at `docs/rules/changsha-test-catalog.md`. High-priority rule contradictions (bird count, scoring model, multi-win resolution, instant win continuation) must be resolved before implementation begins. Orchestration logs filed to `.squad/orchestration-log/`.
