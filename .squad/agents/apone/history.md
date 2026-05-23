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
- `232d7db` — `ci: Phase J Wave 4 — Docker build + ghcr.io publish + nightly smoke`

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

## Phase J Wave 5 — Playwright E2E + /metrics + structured logging + secrets audit (2026-05-23)

**Commits authored:**
- `072fd00` — `feat(devops): Phase J Wave 5 — Playwright E2E + /metrics + structured logging + secrets audit`

**What shipped:**
- `src/frontend/autotable-src/tests/e2e/playwright.config.ts` (NEW) — `chromium` + Pixel-5 `mobile-chrome` projects, `baseURL` resolves from `E2E_BASE_URL` (default `http://localhost:8080/autotable/`), `'github'` reporter under CI / `'list'` locally.
- `src/frontend/autotable-src/tests/e2e/smoke.spec.ts` (NEW) — 4 tests (title, lobby visibility, Quick Match URL transition, mobile drawer toggle). Quick Match assertion is the lesson of the wave: clicked via `locator.evaluate(el => el.click())` JS-dispatch (mobile-chrome's touch synthesis was swallowing `force: true` clicks) and polls `page.url()` for `[?&]variant=` since `buildUrl()` in `src/lobby.ts:328–342` always emits the `variant` param. Viewport-portable across chromium + mobile-chrome at ~6s end-to-end.
- `src/frontend/autotable-src/tests/e2e/README.md` (NEW) — local quickstart, CI usage, troubleshooting (`force: true` rationale for the off-screen settings drawer at `right: -340px, z-index: 1080`), future-wave notes.
- `src/frontend/autotable-src/package.json` — added `e2e` + `e2e:install` scripts and `@playwright/test ^1.45.0` devDep. `package-lock.json` regenerated.
- `.github/workflows/e2e-playwright.yml` (NEW) — push-to-`main` + PR-to-`main` + `workflow_dispatch`. Pipeline: `actions/checkout@v4` → `actions/setup-node@v4` (node 20 + npm lockfile cache) → `npm ci` → `npx playwright install --with-deps chromium` → `docker build` (BUILD_SHA passthrough) → `docker run -d -p 8080:8080` → wait /health (30s) → `npm run e2e` → teardown → `actions/upload-artifact@v4` on failure (`playwright-report/`). actionlint v1.7.7 clean.
- `src/backend/src/Mahjong.Autotable.Api/Observability/MetricsEndpoint.cs` (NEW, ~114 lines) — three Prometheus gauges in canonical text/plain v0.0.4 exposition format: `mahjong_uptime_seconds` (anchored to `Process.GetCurrentProcess().StartTime.ToUniversalTime()` with try/catch fallback for AOT — *not* a static `DateTimeOffset.UtcNow` field because static fields lazily init on first type touch which produced ~0 uptime on the first scrape during early dev), `mahjong_active_games_total` (reads existing `IChangshaGameRuntime.GameCount` — no new interface surface), `mahjong_build_info{sha="..."} 1` (`BUILD_SHA` env, collapses null/empty → `"dev"` per `/health` contract). No new NuGet dep — pure `System.Diagnostics` + `System.Text`.
- `src/backend/src/Mahjong.Autotable.Api/Program.cs` — added `using Mahjong.Autotable.Api.Observability;` + `using System.Text.Json;`, env-aware logger config (`builder.Logging.ClearProviders()` then `AddJsonConsole` in Production with `JsonWriterOptions { Indented = false }` / `AddSimpleConsole` everywhere else, `IncludeScopes = true` in both so SignalR `ConnectionId` / `HubMethodName` surface in the structured payload), and `app.MapGet("/metrics", sp => MetricsEndpoint.Render(sp));`.
- `docs/observability.md` (NEW, ~230 lines) — endpoint catalog, metric definitions, sample exposition output (live-captured), PromQL examples (`rate()`, uptime alerts, build-info join), LogQL for Loki, KQL for Azure Log Analytics, runbook snippets.
- `docs/secrets.md` (NEW, ~275 lines) — audit findings (`appsettings.json` placeholder SqlServer password documented as needing env override pre-SQL-Server migration; no real secrets in tracked source), env-var contract table, recipes for Docker secrets / GHA encrypted secrets / k8s `Secret` / AWS Secrets Manager / Azure Key Vault / GCP Secret Manager, 90-day rotation baseline.

**Verified locally:**
- `dotnet test` over `src/backend` → **445 / 0 / 0** (includes Vasquez's untracked `MetricsEndpointTests.cs` contract tests that exercise the new `/metrics` route — they pass against my implementation).
- Built and ran Docker container in a separate worktree (`/data/source/mahjong-w5-verify`) on `localhost:8088` to avoid Bishop's parallel WIP polluting the build context:
  - `/health` → `{"status":"healthy","buildSha":"test-phase-j-w5","uptime":"…","version":"1.0.0.0"}` ✅
  - `/metrics` → valid Prometheus exposition; `mahjong_uptime_seconds` grew monotonically across successive scrapes (5.080s → 13.092s → 21.115s) confirming the `Process.StartTime` anchor works ✅
  - Production logs were one JSON document per line ✅
- Playwright smoke against the live container: **7 passed / 1 skipped** in 6.1–6.2s, two consecutive runs (skip is the chromium-only `mobile drawer toggle` guard on the mobile project).
- `actionlint v1.7.7` on `e2e-playwright.yml` → exit 0, no findings.

**Cross-lane:** No backend domain code touched — `MetricsEndpoint` consumes the existing `IChangshaGameRuntime.GameCount` Bishop added in Phase I Wave 2. No frontend `src/` code touched — smoke spec works against the bundle as-is and uses only testids that exist in HEAD's `index.html` (selectors.md is aspirational for many entries; the spec sticks to the live ones).

**Lane discipline:** Selective `git add` of exactly 10 files mine (the 3 e2e files, `package.json`, `package-lock.json`, `MetricsEndpoint.cs`, `Program.cs`, `e2e-playwright.yml`, `observability.md`, `secrets.md`). Explicitly avoided `git add -A` because the working tree carried Bishop's uncommitted WIP (`index.html`, `client-ui.ts`, `client.ts`, `lobby.ts`, regenerated bundle artifacts, his `MetricsEndpointTests.cs` contract tests) and pre-session `.github/workflows/squad-*.yml` files — all left untracked for their owners.

**Patterns locked for future DevOps work on this codebase:**
- **`Process.StartTime` over `DateTimeOffset.UtcNow` at static init** for any "since process start" anchor — static fields lazily init on first type touch which produces a near-zero diff on the first endpoint hit. Wrap `Process.GetCurrentProcess()` in `try/catch` for AOT scenarios.
- **`builder.Logging.ClearProviders()` is mandatory** before `AddJsonConsole` / `AddSimpleConsole` — otherwise the default Console provider double-emits and operators see each line twice. Confirmed empirically in the verify worktree.
- **Env-aware logger config** (`IsProduction()` switch) — Production gets JSON, everything else gets human-readable. Keeps `dotnet run` ergonomic without losing structured ingestion in deployment. `IncludeScopes = true` in both modes so SignalR scope state surfaces.
- **`force: true` is the canonical Playwright escape hatch** when the visual stack is correct but the hit-test layer disagrees. On `isMobile: true` projects, even `force: true` can be insufficient — `locator.evaluate(el => el.click())` to fire the JS `click` event directly is the next step and proved viewport-portable across chromium + mobile-chrome.
- **Prefer URL-shape assertions over DOM-state assertions** for navigation-triggering interactions: `window.location.replace(url)` reliably mutates `page.url()`, whereas which DOM nodes are visible afterwards can differ between viewports and CSS configurations.
- **Verify in a separate worktree** when another agent is writing to the same source tree in parallel. `git worktree add` off detached HEAD is the cheapest way to get a clean live-build environment without fighting concurrent edits.


