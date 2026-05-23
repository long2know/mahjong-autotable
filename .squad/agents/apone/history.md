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


## Phase J Wave 6 — Rate limiting + CORS + reverse-proxy / systemd / log-rotation (2026-05-23)

**Commits authored:**
- `408e0d1` — `feat(devops): Phase J Wave 6 — rate limiting + CORS + reverse-proxy / systemd / log-rotation guides`

**What shipped:**
- `src/backend/src/Mahjong.Autotable.Api/RateLimiting/RateLimitingExtensions.cs` (NEW, ~140 lines) — `AddMahjongRateLimiting(IConfiguration)` registers `Microsoft.AspNetCore.RateLimiting` with two **IP-partitioned named policies** via `RateLimitPartition.GetFixedWindowLimiter` / `GetTokenBucketLimiter` + `options.AddPolicy(name, httpContext => …)` (the simpler `AddFixedWindowLimiter("name", o => …)` overload creates a **single shared bucket** for all callers — quietly wrong for per-IP intent). `ResolvePartitionKey` prefers `X-Forwarded-For` so the partition key matches reality behind nginx / Caddy without depending on `ForwardedHeaders` middleware. Public policy-name constants (`AnonymousPolicy = "fixed-window-anonymous"`, `ApiPolicy = "token-bucket-api"`). Gated by `RateLimiting:Enabled` — returns `false` when off, so the caller skips `app.UseRateLimiter()` entirely (more defensive than wiring middleware with "unlimited" policies). 429 rejection contract: status 429, body `{"error":"too_many_requests"}`, `Retry-After` header populated from lease metadata.
- `src/backend/src/Mahjong.Autotable.Api/Program.cs` — added `using` for new namespace + `using System.Text.Json` (already there), wired `builder.Services.AddMahjongRateLimiting(builder.Configuration)` after the CORS block, conditional `app.UseRateLimiter()` only when the gate is on, swapped the hard-coded localhost CORS origin list for `Cors:AllowedOrigins` config-read (kept `AllowCredentials()` since the autotable bundle's `mahjong_pid` cookie + SignalR auth cookie need it, which precludes `AllowAnyOrigin()`). Endpoint conventions: `/health` + `/api/health` + `/metrics` got `.DisableRateLimiting()`; `/api/system/persistence`, `/api/changsha/pattern-ordering`, `MapControllers()` got `.RequireRateLimiting(ApiPolicy)`; hub + autotable-WS routes deliberately left un-policed (long-lived transports; the middleware only sees the handshake anyway).
- `src/backend/src/Mahjong.Autotable.Api/appsettings.json` — added `"Cors": { "AllowedOrigins": [...localhost x4] }` and `"RateLimiting": { "Enabled": false }`. The `false` default is what keeps the xUnit `WebApplicationFactory.UseEnvironment("Development")` harness off-policy.
- `src/backend/src/Mahjong.Autotable.Api/appsettings.Production.json` (NEW) — `"Cors": { "AllowedOrigins": [] }` + `"RateLimiting": { "Enabled": true }`. Empty origin list forces deploys to set `Cors__AllowedOrigins__0=https://<public-host>` explicitly.
- `infra/nginx/mahjong.conf.example` (NEW) — port 80 → 443 redirect, Let's Encrypt ACME challenge prefix on plain HTTP, TLS server block with WebSocket Upgrade locations (`map $http_upgrade $mahjong_connection_upgrade`) for `/hubs/` and `/autotable/ws` with 24-hour `proxy_read_timeout`, `X-Forwarded-For` + `X-Forwarded-Proto` propagation, commented-out basic-auth gate for `/metrics`.
- `infra/caddy/Caddyfile.example` (NEW) — auto-TLS via ACME, `reverse_proxy 127.0.0.1:8080` with explicit `header_up X-Forwarded-*`, 24-hour `transport http` timeouts, JSON rolling access log, commented-out `basicauth` gate for `/metrics`.
- `infra/systemd/mahjong-autotable.service.example` (NEW) — `Type=simple`, `After=docker.service network-online.target`, `Restart=on-failure`, `LimitNOFILE=65536`, `NoNewPrivileges=true`, `ProtectSystem=full`, `EnvironmentFile=-/etc/default/mahjong-autotable` (optional, tolerant of missing), `ExecStartPre=-/usr/bin/docker rm -f` + `docker pull` + `ExecStart=docker run --rm --name mahjong-autotable -p 8080:8080 -v mahjong-data:/data --log-opt max-size=10m --log-opt max-file=5 …` so the unit is idempotent across restarts and bakes rotation into the deploy.
- `docs/reverse-proxy.md` (NEW, ~130 lines) — operator guide: why a reverse proxy (TLS, WebSocket fidelity, real IPs), sample-config table, nginx + Caddy quick-start, certbot, `ForwardedHeaders` discussion + the partition-key fallback rationale.
- `docs/log-rotation.md` (NEW, ~165 lines) — Docker `json-file` `max-size` / `max-file` opts (recommended), daemon-wide default via `/etc/docker/daemon.json`, alternative `logrotate(8)` config with `copytruncate` for the rare bind-mounted-log case, verification commands.
- `docs/systemd.md` (NEW, ~135 lines) — install walk-through, redeploy workflow with `EnvironmentFile` bumps, troubleshooting matrix (image-pull timeout, name conflict, EnvironmentFile typo, host-side `LimitNOFILE` propagation), uninstall.
- `docs/deployment.md` — appended § 12 "Production with reverse proxy", § 13 "Production with systemd", § 14 "Log rotation", § 15 "CORS", § 16 "Rate limiting" (full policy table + 429 contract + IP attribution + toggle instructions).
- `docs/secrets.md` — appended "CORS origins (Phase J Wave 6)" subsection under § Audit findings, extended env-var contract table with `Cors__AllowedOrigins__0` (`no — public origin`) and `RateLimiting__Enabled` (`no — boolean toggle`), explicit "AllowCredentials precludes AllowAnyOrigin" note.

**Verified locally:**
- `dotnet test` over `src/backend` → **445 / 0 / 0** (unchanged from Wave 5 baseline). Tests run under `Development` env via `WebApplicationFactory.UseEnvironment("Development")`; the `false` default in `appsettings.json` short-circuits the middleware. Zero regressions, zero new tests touched (Vasquez's lane).
- Live smoke (Release publish, `ASPNETCORE_ENVIRONMENT=Production`, `RateLimiting:Enabled=true` from `appsettings.Production.json`):
  - `GET /health` → 200 with valid four-field JSON; JSON-line structured logs on stdout ✅
  - `GET /metrics` → 200 with valid Prometheus exposition; all three gauges present ✅
  - `GET /api/changsha/pattern-ordering` × 50 rapid: requests #1-30 returned 200, request #31 returned **429** ✅ (token-bucket capacity = 30 confirmed; the 5-tokens/sec replenish would unblock #32 after ~200 ms — not exercised in this smoke)
  - `GET /health` × 80 rapid → all 200 ✅ (probe endpoint deliberately unlimited)
  - `GET /metrics` × 80 rapid → all 200 ✅ (scrape endpoint deliberately unlimited)
- `dotnet build` clean — 0 warnings, 0 errors.

**Cross-lane:** No domain code touched — `RateLimitingExtensions` is a pure infrastructure extension and `Program.cs` edits are surgical (CORS section swap, AddMahjongRateLimiting call, endpoint convention `.RequireRateLimiting()` / `.DisableRateLimiting()` decorations). Hicks's Playwright specs run under `Development` so `RateLimiting:Enabled=false` keeps them off-policy — explicitly called out in the memo so Wave 7+ specs targeting Production env have the override pattern documented. Bishop's `MatchmakingController` (lone controller today) auto-inherits the token-bucket policy via `MapControllers().RequireRateLimiting(ApiPolicy)`; any future controller he adds picks up the same limit without code change. The `fixed-window-anonymous` policy is registered + documented but **not yet applied** — when Bishop ships `POST /api/identity` (or similar unauthenticated mutating endpoint), one-liner: `.RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)`. Vasquez has a clear test pattern in the memo (boots `WebApplicationFactory` with `b.UseEnvironment("Production")` + `b.UseSetting("RateLimiting:Enabled", "true")` → assert 200 × 30 then 429 on #31 + `Retry-After` header non-null).

**Lane discipline:** Selective `git add` of exactly 12 files mine. Explicitly avoided `git add -A` because the working tree carried Hicks's uncommitted WIP (`index.html`, `lobby.ts`, untracked `identity.ts` / `leaderboard.ts`) + pre-session scaffolding (`.github/workflows/squad-*.yml`, `.copilot/skills/error-recovery/`, `.tool-actionlint/`, `.work/`) — all left for their owners.

**Patterns locked for future DevOps work on this codebase:**
- **`options.AddPolicy("name", httpContext => RateLimitPartition.GetXxxLimiter(key, ...))` is the only correct way to get per-IP rate limiting.** The convenience `options.AddFixedWindowLimiter("name", o => ...)` overload creates a **single shared bucket** that all callers consume from — quietly wrong if intent is "N req/min/IP". Always use the partitioned form via `AddPolicy` + `RateLimitPartition.GetXxxLimiter(key, factory)`.
- **`X-Forwarded-For` first, `Connection.RemoteIpAddress` fallback** in the partition-key helper. Works whether or not the operator has wired `Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders`. Centralize in one `ResolvePartitionKey` static so every policy uses identical attribution and a future swap (e.g. to RFC 7239 `Forwarded`) is one-place.
- **Gate middleware registration on `RateLimiting:Enabled`** — `app.UseRateLimiter()` should NOT be called when the gate is off. This is more defensive than registering middleware with unlimited policies because (a) the per-request limiter check overhead is gone in dev / test, (b) any third-party `.RequireRateLimiting("policy-name")` metadata that references an unregistered policy is silently ignored when no middleware is wired (vs throwing at request time).
- **`.DisableRateLimiting()` on probe + scrape endpoints** even when the gate is off. The attribute is metadata-only when no middleware is wired, so it's free to apply unconditionally — and it keeps the intent in source so a future "let's turn the limiter on in dev too" change doesn't accidentally throttle the probe loop.
- **`AllowCredentials()` + enumerated origins ≠ `AllowAnyOrigin()`** — ASP.NET refuses the latter combo at policy-build time as a CSRF mitigation. Production deploys MUST enumerate origins; document explicitly in `docs/secrets.md` so it doesn't get refactored away on a "let's loosen CORS for testing" PR.
- **`--log-opt max-size=10m --log-opt max-file=5` on every `docker run`** — the default `json-file` driver with no cap is a known outage vector. The systemd sample bakes it in so operators inherit safety by following the docs; `docs/log-rotation.md` carries the daemon-wide variant for hosts that prefer one config in `/etc/docker/daemon.json`.
- **Sample configs ship in-repo under `infra/<tool>/<name>.example`.** Operators `install -m 0644 infra/.../<name>.example /etc/.../<name>` — single source of truth, no PR-comments / wiki / Notion sprawl. Mirror the file format the destination tool expects (so `nginx -t` runs cleanly on the `.example`).
- **Verify the partition-key path empirically.** `for i in $(seq 1 50); do curl -w "%{http_code} " /api/...; done` is the fastest way to confirm the limiter quota matches your config. The token-bucket capacity-then-429 transition is visually obvious in the output.


## Phase J Wave 7 — Multi-provider EF Core + k8s Kustomize tree + backup scripts + non-root container (2026-05-22)

**Commits authored:**
- `ca4ae14` — `feat(devops): Phase J Wave 7 — multi-DB provider + k8s + backup + non-root container`

**What shipped:**
- **Multi-provider EF Core** — `AppDbContext` rewired as a generic-options-aware base class; three concrete subclasses (`SqliteAppDbContext`, `PostgresAppDbContext`, `SqlServerAppDbContext`) under `src/backend/src/Mahjong.Autotable.Api/Persistence/` with sibling `IDesignTimeDbContextFactory<T>` implementations so `dotnet ef migrations add … --context <Sub>` works without booting the host. `ServiceCollectionExtensions.AddPersistence(IConfiguration)` reads `Persistence:Provider` (`Sqlite` default / `Postgres` / `SqlServer`, plus `postgres` / `PostgreSql` aliases), wires the matching driver from `ConnectionStrings:<Provider>`, and aliases the legacy `AppDbContext` to the chosen subclass via `AddScoped` so every existing `GetRequiredService<AppDbContext>()` call site keeps working. Missing connection string throws `InvalidOperationException` lazily on first DI resolve so a typo'd k8s ConfigMap fails fast with a clear stack trace. Per-provider migration sets under `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/` are isolated per subclass; Postgres + SqlServer run `MigrateAsync` at startup, Sqlite continues to use `EnsureCreatedAsync` + the defensive `CREATE TABLE IF NOT EXISTS` sweep. **`HasColumnType("TEXT")` dropped** from `StateJson` / `EventsJson` — EF Core now picks `TEXT` on SQLite, `text` on Postgres, `nvarchar(max)` on SQL Server (the Wave 6 hardcoded value would have collapsed to a 4000-char column on SQL Server).
- **Postgres compose overlay** — `docker-compose.postgres.yml` at repo root spins up a `postgres:16-alpine` sidecar gated on `pg_isready` healthcheck + flips the API container to `Persistence__Provider=Postgres`. Named volume for the PG data so `docker compose down` keeps rows; `down -v` wipes. `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` overridable via env.
- **Kubernetes manifests (Kustomize tree)** — Full `infra/k8s/` tree:
  ```
  base/{configmap,secret-template,pvc,deployment,service,ingress,hpa,kustomization}.yaml
  overlays/staging/  — 1 replica, staging hostname, SQLite
  overlays/prod/     — 3 replicas, prod hostname, Postgres
  ```
  2-replica RollingUpdate, `runAsNonRoot: true` + `runAsUser: 1000` + `readOnlyRootFilesystem: true` + `allowPrivilegeEscalation: false` + `capabilities.drop: [ALL]` + `seccompProfile: RuntimeDefault`; writes route to the 2Gi RWO `data` PVC + an `emptyDir` `/tmp` mount. **Sticky sessions mandatory** for `/hubs/changsha` (SignalR) and `/autotable/ws` (raw WS) — wired via nginx-ingress cookie-affinity annotations (`affinity: cookie`, `mahjong_aff`, 24h max-age); without it a WS upgrade can land on pod A and subsequent frames hit pod B → reset storm. Liveness + readiness both probe `/health` (Bishop's Wave-3 canonical endpoint). HPA: CPU 70% + memory 80%, min 2 / max 8. Image-pull secret `ghcr-pull` (dockerconfigjson) referenced by name.
- **Backup & restore scripts** under `scripts/` (all chmod +x):
  - `backup-sqlite.sh` — `sqlite3 .backup` (safe vs active writer) + `PRAGMA integrity_check` + retention via `RETAIN_COUNT` (default 14).
  - `restore-sqlite.sh` — snapshots existing DB to `.pre-restore-<TS>` for instant rollback + atomic move.
  - `backup-postgres.sh` — `pg_dump -Fc -Z 6 --no-owner --no-privileges`, PG* env, retention.
  - `restore-postgres.sh` — `pg_restore`, optional `RESTORE_CLEAN=1`, post-restore sanity-check on `ChangshaGames` + `PlayerProfiles`.
  Cron-friendly: timestamped output, `logger -t` ready. Quarterly restore-drill procedure documented in `docs/backup-restore.md`.
- **Container hardening** — `Dockerfile` creates GID/UID 1000 (`mahjong` user) and switches to `USER 1000:1000` after copying build artefacts. `/data` and `/app` `chown`'d so SQLite can write its DB without root. `groupadd` / `useradd` guarded by `getent` so the build is idempotent against base images that already ship UID 1000 (the post-2026 `aspnet:10.0` image now does). Verified end-to-end via `tests/smoke/docker-build-smoke.sh`: build ✅, `/health` ✅, all four contract fields present.
- **Multi-provider CI** — `.github/workflows/db-providers.yml` runs the full xUnit suite under a matrix of `[Sqlite, Postgres]` with a `postgres:16-alpine` service container. SqlServer intentionally omitted (heavy image, slow on hosted runners) — rely on the `SqlServerAppDbContextModelSnapshot` diff + Postgres CI as proxy.
- **Documentation** — `docs/database-providers.md` (provider selector, env contract, migration layout, `dotnet ef` recipes, EnsureCreated vs Migrate behaviour), `docs/kubernetes.md` (cluster assumptions, ghcr secret, cert-manager, sticky-session rationale, kustomize commands, observability), `docs/backup-restore.md` (script env, cron examples, off-site sync, quarterly restore-drill).
- Fixed one xunit-API drift in Vasquez's `ContainerHardeningTests.cs:90` (`Assert.NotEqual(..., ignoreCase: true)` overload no longer exists in xunit 2.9.3) — switched to `Assert.False(string.Equals(..., StringComparison.OrdinalIgnoreCase))`.

**Verified locally:**
- `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` → **554 passed / 0 failed / 0 skipped** (was 456/0/0 at Wave 6 head; +98 net from Vasquez's forward-staged Wave-7 contract tests + Bishop's backstops). Apone owns no production tests directly this wave — Vasquez's `Persistence/DbProviderSwitchingTests.cs` (8 facts), `Deploy/ContainerHardeningTests.cs` (6 facts) and `Deploy/K8sManifestSanityTests.cs` (12 facts) pin my contracts.
- `dotnet build` clean — 0 warnings, 0 errors.
- Docker smoke (`tests/smoke/docker-build-smoke.sh`) green.

**Cross-lane:** Strict-disjoint lanes preserved with Bishop (replay endpoint + palette + `/health` JSON), Hicks (replay viewer + a11y + settings drawer + profile page), Vasquez (tests + selectors). Bishop's Wave-7 `20260524000000_AddChangshaGameReplay` migration was scaffolded against the base `AppDbContext` only (manual, not against my per-provider subclasses) — flagged in his memo so my Wave-8 polish can regenerate it under `Migrations/{Sqlite,Postgres,SqlServer}/` once the multi-context migration story stabilises. Memo: `.squad/decisions/inbox/apone-phase-j-wave-7.md`.

**Patterns locked for future DevOps work on this codebase:**
- **Provider-specific DbContext subclasses with isolated migration sets** — Cleanest cross-provider EF strategy: the base context owns the shape (`OnModelCreating`, DbSets, value-conversions), subclasses just forward typed options. `IDesignTimeDbContextFactory<T>` lives next to each subclass so `dotnet ef migrations add … --context <Sub> --output-dir Persistence/Migrations/<Sub>` works without booting the host. Provider-specific column types (e.g. `nvarchar(max)` vs `text`) are deferred to EF Core's type mapper — never hardcode `HasColumnType` for portable code.
- **Lazy-throw on missing connection string at DI-resolve time, not at boot** — `AddDbContext` option lambda checks the resolved string and throws `InvalidOperationException` from inside; the lambda only fires on first `GetRequiredService<AppDbContext>()` call, so the host still starts and `/health?simple=1` reports the wiring problem cleanly. Boot-time throw would crash-loop with no JSON response for the operator.
- **`USER 1000:1000` + `getent`-guarded `groupadd/useradd`** — Idempotent against base images that already ship UID 1000 (the post-2026 aspnet runtime now does). Always `chown -R 1000:1000 /data /app` before switching user; otherwise SQLite write fails with EACCES + no helpful error message.
- **Kustomize base + overlays/{staging,prod} tree** — One canonical base, two overlays differentiated by replica count + hostname + provider. Common patches (resource limits, security context) live in the base; environment-specific edits (sealed secrets, image tags) land in the overlay. `kubectl kustomize infra/k8s/overlays/staging | kubectl apply -f -` is the single deploy command.
- **Sticky sessions via cookie-affinity annotations are mandatory for WS endpoints** — nginx-ingress `nginx.ingress.kubernetes.io/affinity: cookie` + `affinity-mode: persistent` + `session-cookie-name: mahjong_aff` + 24h `session-cookie-max-age`. Without these the WS upgrade can land on pod A and subsequent frames hit pod B → reset storm. Documented + alternatives for Traefik / AWS ALB in `docs/kubernetes.md`.
- **Postgres in CI service container, SqlServer skipped** — Hosted runners are too slow for the `mssql/server` image's ~3 min boot. SqlServer correctness is proxied by the `SqlServerAppDbContextModelSnapshot` diff on every PR + Postgres-against-real-driver CI; move to self-hosted runners or a faster matrix later.
- **`sqlite3 .backup` (not `cp`) for online SQLite backups** — Survives active writers; `cp` of an open SQLite file can produce a corrupt copy if a transaction is mid-write. `pg_dump -Fc -Z 6` (custom format, gzip-6) for Postgres — restores via `pg_restore` selectively (per-table, per-schema). Quarterly restore-drill documented in `docs/backup-restore.md` is the only way to know your backups actually work.

**Deferrals to Wave 8+:**
- **Sentry integration** — error aggregation + release tagging (deferred to Wave 8 backlog).
- **Cloudflare integration** — TLS terminator + DDoS shield in front of the ingress (deferred to Wave 8 backlog).
- **SQL Server in CI matrix** — when GitHub-hosted runners get faster (or we move to self-hosted), drop `SqlServer` into the matrix in `db-providers.yml`.
- **Legacy `AppDbContext`-tagged migrations** under `Persistence/Migrations/` (root, no provider subfolder) are now effectively orphaned — SQLite uses EnsureCreated, the provider-specific subclasses point at their own subfolder. Harmless but can be cleaned up in a follow-up. Bishop's `20260524000000_AddChangshaGameReplay` is currently in this root folder; should be regenerated under each provider's folder in Wave 8.
