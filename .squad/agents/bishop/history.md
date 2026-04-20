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
