# Apone — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend + autotable-derived TS frontend (Parcel-bundled). Single-page mahjong table with WS + SignalR transport, in-memory game runtime, EF Core SQLite persistence.

**User:** Stephen Long. Standing directives: (1) "No pauses — keep iterating until 100% done done." (2) All agents use `claude-opus-4.7-xhigh`.

**Joined:** 2026-05-22, during Phase J Wave 3. Brought in to handle the Docker single-image packaging Stephen originally requested.

**Stack notes:**
- Backend: `src/backend/Mahjong.Autotable.slnx` — .NET 10, dotnet test gates each wave
- Frontend: `src/frontend/autotable-src/` — TS + Parcel, builds to `src/frontend/autotable/`
- Persistence: EF Core SQLite; ChangshaGame entity hydrated on startup
- VS Code F5: `.vscode/tasks.json` + `launch.json` prepend dotnet path candidates so F5 works across install styles

**Team context I should know:**
- Bishop owns backend code (Changsha rules, bots, runtime)
- Hicks owns frontend (autotable TS, lobby, HUD, bundle build)
- Vasquez owns tests (acceptance + integration + regression)
- Scribe handles decisions.md merges and orchestration logs
- Ralph monitors the work queue (mostly dormant in this project so far)

## Phase J Wave 3 — Joining
- Created during the same session that fired Wave 3
- First task: Docker single-image deployment (multi-stage Dockerfile combining frontend bundle + .NET backend, docker-compose.yml, healthcheck endpoint coordination with Bishop)
