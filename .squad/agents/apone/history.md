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

## Phase J Wave 4 — Docker CI publish + nightly smoke (2026-05-22)

**Commits authored:**
- `<filled-in-after-commit>` — `ci: Phase J Wave 4 — Docker build + ghcr.io publish + nightly smoke`

**What shipped:**
- `.github/workflows/docker-build.yml` (NEW) — push-to-`main` + tag + `workflow_dispatch` build that pushes `ghcr.io/long2know/mahjong-autotable:{latest,sha-<sha>,<tag-when-tag-push>}` via `docker/build-push-action@v6`. Uses `docker/setup-buildx-action@v3` + `docker/login-action@v3` (auto `GITHUB_TOKEN`, no PAT) + `docker/metadata-action@v5` for dynamic tag set + GHA cache (`type=gha,mode=max`) for layer reuse. Permissions `contents: read, packages: write`. The `latest` tag is gated to `main` so a dispatch from a feature branch can't clobber prod.
- `.github/workflows/docker-smoke.yml` (NEW) — nightly cron `0 8 * * *` UTC + `workflow_dispatch` that runs Vasquez's `tests/smoke/docker-build-smoke.sh`. On failure: collects `smoke.log`, `tests/smoke/.run-*` (if trap didn't clean it), `docker ps -a`, and `docker images` snapshots into `smoke-logs/` and uploads via `actions/upload-artifact@v4` as `docker-smoke-failure-<run-id>` (14-day retention). Failure surface = artifact, NOT auto-filed issue — picked artifact over issue to avoid flaky-cron issue-tracker spam (rationale documented in `docs/ci.md`). 30-min timeout.
- `Dockerfile` — added `ARG BUILD_SHA=""` + `ENV BUILD_SHA=${BUILD_SHA}` immediately after `WORKDIR /app` in the runtime stage; removed the old `BUILD_SHA=""` line from the lower `ENV` block (would have overridden the new ARG-driven ENV). Surgical change — local `docker build .` still produces `"buildSha":"dev"` thanks to Bishop's Wave-3 `489d86f` `IsNullOrEmpty` widening; CI build with `--build-arg BUILD_SHA=${{ github.sha }}` produces `"buildSha":"<real-sha>"`.
- `docs/ci.md` (NEW) — end-to-end docs for both workflows: trigger matrix, tag scheme, manual run instructions (UI + `gh workflow run`), `docker pull` examples, how to enumerate published versions (`gh api /users/long2know/packages/container/mahjong-autotable/versions`), the one-time "make package public" GHCR settings step, required secrets (none), failure surface rationale, local pre-PR verification snippet. Explicitly calls out that pre-session `squad-*.yml` workflows are out-of-scope for this document.

**Verified locally:**
- YAML `safe_load` on both workflows — clean.
- `actionlint v1.7.7` on both workflows — exit 0, no findings.
- `docker build --build-arg BUILD_SHA=test123` → `/health` returns `"buildSha":"test123"` ✅.
- `docker build` (no build-arg) → `/health` returns `"buildSha":"dev"` ✅ (Wave-3 behavior preserved).
- `tests/smoke/docker-build-smoke.sh` end-to-end against the modified Dockerfile — 🎯 PASSED, /health responded after 3s, all four fields present, full trap-driven teardown confirmed.

**Cross-lane:** No backend / frontend / test changes — Bishop's `489d86f` empty-string widening already lets the runtime ARG default to `""` without breaking the `?? "dev"` contract. Vasquez's smoke script is unmodified.

**Lane discipline:** Selective `git add` (Dockerfile + the two new workflows + `docs/ci.md`) — explicitly avoided `git add -A` because the working tree carried untracked pre-session `.github/workflows/squad-*.yml` files + `scratch/` + `.copilot/skills/error-recovery/` that are not mine to commit.

**Pattern locked for future CI work on this codebase:**
- Same-repo ghcr push needs **only** `permissions: { contents: read, packages: write }` + `secrets.GITHUB_TOKEN` — no PAT, no manually-managed secrets. Confirmed via the `docker/login-action@v3` happy path.
- `docker/metadata-action@v5` is the canonical tag-set builder. Use `type=raw,enable=${{ github.ref == ... }}` to gate `latest` to `main` so feature-branch dispatches can't clobber the rolling tag.
- GHA cache (`type=gha,mode=max`) is free and dramatically cuts build time — every multi-stage workflow should opt in.
- Workflow-level "post a failure issue" is **discouraged** for flaky-prone schedules — artifact upload + the red dashboard square is enough signal, and avoids a perpetually-flapping issue thread.

