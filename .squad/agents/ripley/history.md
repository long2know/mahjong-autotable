# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Ripley as Lead.
- Immediate focus is aligning Changsha rule flow with autotable interaction and backend contracts.
- Established a two-track frontend structure: `src/frontend/autotable` (baseline) plus optional `src/frontend/modern` (React + Fluent UI 9 + TS + Vite).
- Created backend foundation at `src/backend/src/Mahjong.Autotable.Api` with EF Core provider switching (`Sqlite`, `PostgreSql`, `SqlServer`) via `Persistence:Provider`.
- Standardized local startup with VS Code configs in `.vscode/launch.json` and `.vscode/tasks.json`, including one-key full-stack F5 compound.
- Added single-image Docker targets in `infra/docker/Dockerfile`: `runtime-autotable` and `runtime-modern` for incremental UI rollout.
