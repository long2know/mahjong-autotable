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

## Phase J Wave 3 — Docker single-image deployment (2026-05-22)

**Commits authored:**
- `ea2c991` — `chore(devops): Phase J Wave 3 — single-image Docker deployment`

**What shipped:**
- `Dockerfile` (repo root) — 3-stage multi-build: Node 20 → .NET 10 SDK → .NET 10 ASP.NET runtime. ~300 MB final image, 47 s cold / 16 s warm. HEALTHCHECK targets Bishop's `/health` with `/api/health` fallback.
- `docker-compose.yml` (repo root) — builds `mahjong-autotable:local`, named volume `mahjong-data` on `/data`, `BUILD_SHA` passthrough.
- `.dockerignore` (repo root) — trims context from ~2.5 GB to a few MB.
- `docs/deployment.md` — full Linux runbook (11 sections: prereqs, build/run, env vars, persistence/backup, healthcheck, day-2 ops, updates, troubleshooting).
- `docs/docker.md` — 5-minute quickstart.
- `README.md` — replaced stale "Docker (single image)" section (referenced deleted `modern/` frontend) with a "Deploy via Docker" pointer.
- `.gitignore` — added `docker-compose.override.yml`, `.env.local` patterns.

**Verified locally:** Docker build succeeded on the runner; container reports `healthy` at startup AND after first probe cycle; `/health`, `/api/health`, `/autotable/`, and `.glb` MIME-type registration all return correct responses.

**Cross-lane:** Bishop's `/health` endpoint (`9235859`) had already landed when I finalised the HEALTHCHECK — no blocker. Stage 1's `COPY src/frontend/autotable-src/ ./` automatically picks up Hicks's pending `sounds/` directory + new TS modules. Exact build + run + assertion commands for Vasquez's smoke test documented in the memo.

**Lane discipline:** Stayed strictly within DevOps scope. Did not touch `src/backend/**`, `src/frontend/**`, `src/backend/tests/**`, or `.vscode/`. Only edited my own files plus `README.md` (replaced one obsolete section) and `.gitignore` (added Docker artifacts).

**Pattern locked for future Docker work on this codebase:**
- `WORKDIR=/app` + `COPY frontend → /frontend/autotable/` is the ONLY layout that exercises Program.cs L65's path resolution without a backend code change. If anyone moves the bundle elsewhere, they MUST also patch Program.cs.
- Connection-string env-var name is `ConnectionStrings__Sqlite` (not `__DefaultConnection`) — verified against `Persistence/ServiceCollectionExtensions.cs` L30.
- Parcel build invariant: `--public-url .` is mandatory (decisions.md L1864).
