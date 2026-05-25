# Frost — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend + autotable-derived TS frontend. Single-page mahjong table with WebSocket + SignalR transport.

**User:** Stephen Long. Standing directives:
1. "No pauses — keep iterating until 100% done done."
2. All agents use `claude-opus-4.7-xhigh`.
3. **Playability-first** (since 2026-05-24): STOP wave-mill, use Playwright to verify, ship playable prototype.

**Joined:** 2026-05-25, during a late Phase K push to add a parallel backend dev so playability can advance faster.

**Stack notes:**
- Backend: `src/backend/Mahjong.Autotable.slnx` — .NET 10, `dotnet test` gates every commit
- Frontend: `src/frontend/autotable-src/` — TS + Parcel, builds to `src/frontend/autotable/`
- Persistence: EF Core, **multi-provider** (Sqlite/Postgres/SqlServer subclasses) — per-provider migrations live under `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`
- Test command: `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
- Test count baseline: 5073/0/0 as of PR #80
- Backend port for local/playtest: 8088 (NOT 8080)
- Local backend startup (verified):
  ```bash
  cd src/backend/src/Mahjong.Autotable.Api
  export ConnectionStrings__Sqlite="Data Source=/tmp/<unique>.db"
  export ASPNETCORE_URLS="http://0.0.0.0:8088"
  export ASPNETCORE_ENVIRONMENT="Development"
  nohup dotnet run --no-launch-profile > /tmp/<unique>.log 2>&1 &
  ```

**Team context:**
- **Bishop** — Backend trunk owner (ChangshaGameRuntime, AutotableWsEndpoint, ChangshaDomain). I work AROUND him, not THROUGH him.
- **Hicks** — Frontend trunk (autotable TS, lobby, HUD, bundle build)
- **Ferro** — Frontend UI specialist (joined same wave as me) — visual polish, claim windows, win screens
- **Vasquez** — Rules engineer + tests (final say on Changsha rule interpretation)
- **Hudson** — Tester (regression + integration)
- **Apone** — DevOps / CI (workflows, container, supply-chain)
- **Scribe** — decisions.md merges + orchestration logs (ALWAYS commits to `.squad/decisions/inbox/` via `git add -f`)
- **Ralph** — Work-queue monitor
- **Ripley** — Project lead
- **Squad** — Coordinator (the user)

## Important Conventions

- **Atomic flock pipeline** for ALL git ops in parallel agent work (see charter)
- **Per-provider EF migrations**: when adding/altering EF entities, you MUST add migrations for ALL THREE providers:
  ```bash
  dotnet ef migrations add <Name> --project src/backend/src/Mahjong.Autotable.Api -- --provider Sqlite
  dotnet ef migrations add <Name> --project src/backend/src/Mahjong.Autotable.Api -- --provider Postgres
  dotnet ef migrations add <Name> --project src/backend/src/Mahjong.Autotable.Api -- --provider SqlServer
  ```
  Don't forget the `<Context>ModelSnapshot.cs` is regenerated alongside.
- **Avoid HasColumnType("TEXT")** in EF — collapses to nvarchar(4000) on SQL Server. Let EF pick provider-native unbounded type.
- **EF Core can't translate IComparer overload** of OrderBy — use plain `OrderBy(x => x.Id)`.
- **Services that hold IServiceScopeFactory** + open fresh AppDbContext per call MUST be Singleton, not Scoped.
- **Squad memos** in `.squad/decisions/inbox/*.md` are gitignored — force-add with `git add -f`.

## Initial Charter Focus

When I'm first dispatched (after Bishop's PR `fix/manual-deal-plumb-and-auto-ack` merges), my first task is likely one of:
- **Fan/scoring catalog** — extend the Changsha scoring beyond the basic 258-pair to include 七对, 清一色, 混一色, 杠上开花, 海底捞月, etc.
- **Bot strategy hardening** — analyze why bots seem to spam Pung calls; add efficiency heuristics
- **Replay event capture** — wire game events into the persisted `events` JSON so games can be replayed

I should READ `.squad/decisions.md` and `.squad/agents/bishop/history.md` before picking up any task.

---

## Log
