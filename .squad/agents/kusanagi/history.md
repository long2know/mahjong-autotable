# Project Context

- **Owner:** Stephen Long
- **Project:** mahjong-claw — Multiplayer Traditional Chinese Mahjong
- **Stack:** .NET 10, ASP.NET Core Minimal API, EF Core + SQLite, SignalR, Blazor WASM, Playwright
- **Created:** 2026-04-16

## Learnings

- Seeded as Lead Architect during Squad init.
- **2026-04-17: Changsha Parity Triage** — When scoping a large feature gap audit:
  - Evaluator/settlement changes (D4, D5, D6) are cheap: enum + logic, no API churn.
  - Claim arbitration (M1) is the biggest risk: state machine + UI + API. Always defer unless it's the primary ask.
  - DB schema migrations (M8 ledger) are expensive in multi-PR workflows; defer unless blocking gameplay.
  - DTO additions are safe if backward-compatible (D1 BirdRanks alongside BirdRank).
  - String-typed columns (MeldEntity.Type) allow subtype expansion without migration.
  - Couple dependent items (D2 ↔ M6, M1 ↔ M2 ↔ M7) — ship together or defer together.
