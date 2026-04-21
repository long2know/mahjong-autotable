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

- **2026-04-17: Autotable Client Architecture** — Planning a new three.js client:
  - Server API is already JS-friendly: System.Text.Json camelCase, Guid-as-string, string enums.
  - No physics needed for MVP — server-authoritative state means deterministic tween animations.
  - SignalR client (@microsoft/signalr) is first-class TypeScript; CORS already permissive.
  - Vite proxy handles cross-origin dev; standalone dev server is faster than StaticFiles mount.
  - Changsha 258 wall = 108 tiles (3 suits × 9 ranks × 4 copies); dead wall for kong replacements.
  - Tile texture atlas: 27 unique faces for Changsha; plan for 34 if ChineseOfficial support needed.
  - **Coordination for Togusa:** He leads implementation (not Blazor dev). Phase 0 is scaffolding only — Vite + three.js + SignalR connection. No interaction. Saito handles test coverage.
  - Autotable (pwmarcz) is MIT; architecture reference is fine, but source our own tile assets — autotable art license unclear.
  - Keep Blazor client alive during build; retire only after Phase 5 parity proven.
