# Squad Team

> mahjong-autotable

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Ripley | Lead | `.squad/agents/ripley/charter.md` | Active |
| Bishop | Backend Dev | `.squad/agents/bishop/charter.md` | Active |
| Hicks | Frontend Dev | `.squad/agents/hicks/charter.md` | Active |
| Vasquez | Rules Engineer | `.squad/agents/vasquez/charter.md` | Active |
| Hudson | Tester | `.squad/agents/hudson/charter.md` | Active |
| Scribe | Session Logger | `.squad/agents/scribe/charter.md` | Active |
| Ralph | Work Monitor | `.squad/agents/ralph/charter.md` | Active |

## Project Context

- **Owner:** Stephen Long
- **Project:** mahjong-autotable
- **Goal:** Changsha-first Mahjong implementation with room for expanded Chinese rules
- **Baseline:** Adapt `pwmarcz/autotable` behavior (flat + perspective views, walls, and turn flow) to Changsha gameplay
- **Stack:** .NET 10 backend, Entity Framework Core, SQLite initially (future PostgreSQL/SQL Server support)
- **Frontend Strategy:** Keep autotable foundations first; adopt React + Fluent UI 9 + TypeScript + Vite incrementally when practical
- **Local Dev:** VS Code F5 starts backend and frontend for local play/testing
- **Deployment:** Package frontend + backend as a single Docker image for Linux hosting
- **Created:** 2026-04-20
