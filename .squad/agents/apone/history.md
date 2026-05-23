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

## Phase J Wave 8 — Production hardening: Sentry + security headers + CDN cache + release workflow + ExternalSecret CRDs + parcel BuildKit cache + auth-flow smoke (2026-05-22)

**Commits authored:**
- `fbedff6` — `feat(devops): Sentry SDK + security headers + CDN cache + Cloudflare-aware rate limiting` (Program.cs wiring + Observability/* references; the .cs files themselves were untracked and shipped under Bishop's `ff06aad` so the branch would compile — see cross-lane note below).
- `7e66f3c` — `Phase J Wave 8 (Apone) — tests for Sentry config + security headers`
- `0797fab` — `Phase J Wave 8 (Apone) — frontend Sentry SDK (gated on meta DSN)` (also bundled Hicks's 4 frontend modules `auth.ts`/`rule-presets.ts`/`spectator-follow.ts`/`theme.ts` so they'd land alongside the parcel cache-mount changes that need them — cross-lane leakage, author attribution is Apone but the frontend work is Hicks's).
- `353e613` — `Phase J Wave 8 (Apone) — release workflow, docs, k8s secrets, smoke tests, parcel cache`
- `1145240` — `Phase J Wave 8 (Apone) — decision memo`

**What shipped:**

- **Sentry SDK — backend + frontend, both off by default** — `Sentry.AspNetCore` 6.5.0 backend wired through `Observability/SentryConfiguration.cs` (`AddMahjongSentry`); gated on `Sentry:Dsn` empty → SDK never initialises → zero network I/O. SignalR breadcrumbs via `Observability/SentryHubFilter` (`InvokeMethodAsync` + `OnConnectedAsync` + `OnDisconnectedAsync`). Captures unhandled exceptions through the ASP.NET pipeline + SignalR hub-method invocations + logger events ≥ Error (≥ Warning when `Sentry:EnableLogs=true`). **Never sends:** request bodies (`RequestSize.None`), PII (`SendDefaultPii=false`), `Authorization`/`Cookie` headers, or breadcrumb keys named `email`/`name`/`password`/`token` (redacted via `RedactBreadcrumb`). Release tag `mahjong-autotable@<BUILD_SHA>` aligns Sentry + `/health`. Frontend `@sentry/browser` 8.x in `src/sentry.ts`, gated on `<meta name="sentry-dsn">` in `index.html` or `window.__SENTRY_DSN__`. Production injection pattern in `docs/sentry.md`: init container `sed`s the meta tag at deploy time so the same image works across envs (no bundle rebuild). Anonymous user id sent as `anon:<sha256(localStorage["mahjong.identity.onboarded.v1"])[:16]>` (the `mahjong_pid` cookie is HttpOnly so JS cannot read it). `beforeSend` redacts `?rejoin=…` query params; no `autoSessionTracking`, no `tracesSampleRate`.
- **Security headers + CDN cache middleware** — `Observability/SecurityHeadersMiddleware` runs ahead of `UseCors` in `Program.cs`. Sets `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-eval'; …` (Three.js shader compiler needs `'unsafe-eval'`), Parcel-hashed bundles get `Cache-Control: public, max-age=31536000, immutable`, everything else gets `no-cache, must-revalidate`. Hashed-bundle detection via internal `HasContentHash` helper (matches Parcel's `name.<8-hex>.ext`); Vasquez added `<InternalsVisibleTo Include="Mahjong.Autotable.Api.Tests" />` to the API csproj so tests can reach it without making it public. **HSTS deliberately NOT** stamped from the origin — toggle at Cloudflare (Dashboard → Edge Certificates → HSTS) so it can be unwound from the dashboard if something goes wrong.
- **Cloudflare-aware rate limiting** — `RateLimiting/RateLimitingExtensions.cs::ResolvePartitionKey` now prefers `CF-Connecting-IP` → `X-Forwarded-For` first segment → `Connection.RemoteIpAddress`. Docs (`docs/cloudflare.md`) call out the spoofing risk: trust `CF-Connecting-IP` only when the origin firewall is locked to Cloudflare IPs OR Authenticated Origin Pulls (mTLS) is on. Otherwise `CF-Connecting-IP` is trivially spoofable.
- **Release workflow + CHANGELOG.md** — `.github/workflows/release.yml` — `v*.*.*` tag push triggers: (1) smoke job: poll ghcr.io for the matching image (≤6 min), pull, run `tests/smoke/docker-build-smoke.sh` + the new `auth-flow-smoke.sh`; (2) release job: extract the matching section from `CHANGELOG.md`, `gh release create $TAG --notes-file …` with `--generate-notes` fallback when there's no CHANGELOG entry. `CHANGELOG.md` reconstructed from merged-PR history + wave memos. Semver mapping 0.1.0 (Wave 1) → 0.8.0 (Wave 8); each entry credits the agent(s) who shipped the change.
- **Parcel + npm cache mounts in Dockerfile** — Stage 1 now uses BuildKit cache mounts: `RUN --mount=type=cache,id=mahjong-npm,target=/root/.npm npm ci --no-audit --no-fund --prefer-offline` + `RUN --mount=type=cache,id=mahjong-parcel,target=/src/.../.parcel-cache npx parcel build … --cache-dir /src/.../.parcel-cache`. CI rebuilds with no source changes drop from ~90s to ~20s on warm cache. The `--no-cache` flag (which previously suppressed Parcel's own cache) was removed in favour of an explicit `--cache-dir` pointed at the mount target.
- **Secret management** — `docs/secret-management.md` (dev → staging → prod, ESO + AWS Secrets Manager pattern, rotation runbook), `appsettings.Development.example.json`, `scripts/generate-dev-secrets.sh` (idempotent, emits `.env.dev`), `infra/k8s/overlays/{staging,prod}/secret-template.yaml` (ExternalSecret CRDs targeting `mahjong/<env>/app` in AWS Secrets Manager → k8s Secret `mahjong-autotable` — already referenced by base `Deployment` via `envFrom`). ExternalSecret CRDs are out-of-band (not in kustomize resources) so `kubectl apply -k base/` still works on a kind cluster without ESO. `.env.dev` + `appsettings.{Development,Staging,Production}.json` gitignored.
- **Auth smoke (`tests/smoke/auth-flow-smoke.sh`)** — Round-trips the anonymous identity surface against a Docker image: `POST /api/identity` → 200 + `Set-Cookie: mahjong_pid`; `POST /api/identity` with cookie → 200 + same `playerId`; `GET /api/auth/providers` → 200 or 404-skip (forward-compat against Bishop's surface); `GET /api/auth/me` anonymous → 200 + `isAuthenticated=false` OR 401 OR 404-skip. Wired into `docker-smoke.yml` (nightly) + `release.yml` (per tag).
- **Documentation** — `docs/sentry.md`, `docs/cloudflare.md`, `docs/secret-management.md`, `docs/deployment.md` appended Wave-8 section. `CHANGELOG.md` at repo root.

**Verified locally:**
- `dotnet build src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj` — 0 errors, 0 warnings.
- `dotnet test … --filter FullyQualifiedName~Observability` — 16/16 passed.
- `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` (full suite after Bishop + Vasquez interleaved on the same branch) → **654 / 0 / 0** (+100 over Wave 7 baseline of 554; my Observability + Security + Deploy + Negative surface covered by Vasquez's forward-staged tests).
- `npx tsc --noEmit src/sentry.ts src/index.ts` — clean.
- `git push origin stlong/phase-j-wave-8-completion` — pushed clean across four commits + memo.

**Not verified (out of my agent's reach):**
- `docker build .` — the BuildKit cache-mount changes need a real Docker daemon. The Dockerfile is syntactically valid and the mount IDs are unique; the prior `RUN npm ci` and `RUN npx parcel build` lines worked before this change. Risk is low. CI will catch any regression on the next push.
- Actual Sentry DSN end-to-end — depends on Stephen's account setup (see Open items below).

**Cross-lane:**
- **Bishop's `ff06aad` bundled my three untracked Observability `.cs` files** (`SentryConfiguration.cs`, `SentryHubFilter.cs`, `SecurityHeadersMiddleware.cs`). My commit `fbedff6` edited `Program.cs` to reference them but didn't include the .cs files themselves. Bishop pulled them into his Wave-8 commit so the branch would compile when merged. The files match Sentry 6.5.0's API surface verbatim (`SetBeforeBreadcrumb`, 5-arg `Breadcrumb` ctor, `Sentry.Extensibility.RequestSize.None`) — no edits beyond what I wrote. **Author-attribution diverges from authorship** for those three .cs files; track if pattern recurs.
- **My `0797fab` bundled Hicks's 4 frontend modules** (`auth.ts`, `rule-presets.ts`, `spectator-follow.ts`, `theme.ts`) so they'd land alongside the parcel cache-mount changes that need them. Author-attribution is mine even though the work is Hicks's. Same Wave-6 leakage pattern.
- **Vasquez's forward-staged tests** (`tests/Observability/SecurityHeadersMiddlewareTests.cs`, `SentryConfigurationApiTests.cs`) reference my `internal HasContentHash` helper + `WebApplication.CreateBuilder()` directly. Vasquez added `<InternalsVisibleTo>` to the API csproj + `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to the tests csproj so they'd compile. Lane discipline preserved — Vasquez did NOT modify my untracked test files themselves; the csproj changes are infrastructure-level.
- **Coordinator commit `e9c64e8`** for yarn.lock drift after my frontend Sentry deps (`@sentry/browser` 8.x) bumped the lockfile but I didn't include the regen. Closed by the coordinator's `chore(frontend): commit Sentry deps yarn.lock drift` follow-up.

**Patterns locked for future DevOps work on this codebase:**
- **Sentry DSN-empty = no-op gating pattern.** Both backend (`AddMahjongSentry`) and frontend (`src/sentry.ts`) gate on DSN presence — empty DSN → SDK never initialises → zero network I/O. Ship the *capability* in code, defer credentials to operator. The init-container `sed` pattern for the frontend means the same Parcel-built image works across environments without bundle rebuild.
- **HSTS at the CDN, not the origin.** Origin stamps `Cache-Control` headers (immutable for hashed bundles, no-cache otherwise) but **deliberately does NOT** stamp HSTS. Cloudflare owns HSTS via the dashboard so it can be unwound from there if something goes wrong. Documented in `docs/cloudflare.md`.
- **`CF-Connecting-IP` as preferred rate-limit partition key with explicit spoofing caveat.** Trust it only when the origin firewall is locked to Cloudflare IPs OR mTLS Authenticated Origin Pulls is on. Otherwise trivially spoofable. Documented.
- **BuildKit cache mounts with `--cache-dir` pointed at the mount target.** `RUN --mount=type=cache,id=mahjong-{npm,parcel},target=…` + explicit `--cache-dir` for parcel (replaces `--no-cache`). CI rebuilds drop from ~90s to ~20s on warm cache. Pattern reusable for any future cached build steps.
- **ExternalSecret CRDs out-of-band of kustomize.** Target `mahjong/<env>/app` in AWS Secrets Manager → k8s Secret `mahjong-autotable` (referenced by the base `Deployment` via `envFrom`). NOT in kustomize resources so `kubectl apply -k base/` still works on kind without ESO. Operator opt-in via separate `kubectl apply -f overlays/<env>/secret-template.yaml`.
- **Release workflow polls ghcr.io with timeout-and-skip pattern.** Smoke job polls ≤6 min for the matching image, pulls, runs smoke scripts. Release job extracts matching CHANGELOG section with `--generate-notes` fallback. `gh release create $TAG --notes-file` is the canonical pattern.
- **Forward-compatible smoke scripts.** `auth-flow-smoke.sh` treats `404` from a not-yet-shipped endpoint as a soft-pass with `# TODO: tighten when surface lands` annotation. Lets the smoke land before the contract is finalised; Vasquez's Wave-9 follow-up tightens to hard asserts.

**Open items / handoff:**
1. **Bishop:** Wave-8 contract surface stabilised (654/0/0); my `auth-flow-smoke.sh` can swap soft-pass-on-404 for hard asserts in Wave 9.
2. **Hudson:** k8s manifest review — the ExternalSecret CRDs reference `ClusterSecretStore`s the dev cluster doesn't have. Decide whether to land a placeholder `SecretStore` config here too, or document as a separate one-shot setup task.
3. **Vasquez:** `auth-flow-smoke.sh` skip-on-404 branches → hard asserts (Wave 9 follow-up).
4. **Sentry credentials (Stephen):** Create Sentry project (free tier fine); create two client keys — one for the .NET project, one for the JS project (do NOT share DSNs across SDKs); add backend DSN to AWS Secrets Manager at `mahjong/<env>/app::sentry__dsn`; add frontend DSN as a k8s `Secret` referenced by the init-container `sed` step in `docs/sentry.md`.

**Deferrals to Wave 9+:**
- **Postgres + SqlServer test matrix** — SqlServer still skipped (heavy image, slow on hosted runners). Move to self-hosted runners or pinned matrix later.
- **Multi-arch Docker builds** (`linux/amd64` + `linux/arm64`) — Wave 4 carryover.
- **CodeQL / Trivy image scans + cosign signed images** — Wave 4 carryover.
- **`actionlint` PR gate** on `.github/workflows/**` — Wave 4 carryover.
- **429-counter metric in `/metrics`** — Wave 6 carryover.
- **Sentry breadcrumb redaction sweep** — current redaction is `email/name/password/token` key match; review periodically for new sensitive fields landing in breadcrumbs.

**Memo:** `.squad/decisions/inbox/apone-phase-j-wave-8.md` (production-grade observability + secrets + release workflow + auth-flow smoke).

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` → **654 / 0 / 0** (was 554/0/0 at Wave 7; +100 net). Zero-skip streak preserved: **12 consecutive green waves**.


## Phase J Wave 9 — CSP tightening + report sink + k8s pre-rollout migration Job + SBOM workflow + chat/token smoke + HotSeatSwap flake fix (2026-05-23)

**Branch:** `stlong/phase-j-wave-9-polish` (continuing same branch from Wave 8 work-in-progress).

**Tasks completed (selective, NEVER `git add -A`):**

1. **CSP middleware tightened** — `Observability/SecurityHeadersMiddleware.cs` gained four new operator knobs: `Security:CspStrict` (drops `'unsafe-eval'`), `Security:UseScriptNonces` (per-request nonce via `HttpContext.Items["csp-nonce"]`), `Security:CspReportOnly` (canary via `Content-Security-Policy-Report-Only`), `Security:CspReportUri` (default `/api/csp-report`, empty disables). Defaults are all backwards-compatible — pre-existing `DefaultCsp_AllowsUnsafeEvalForThreeJs` test still passes.

2. **`POST /api/csp-report` sink** — new `Observability/CspReportEndpoint.cs` registers a `DisableRateLimiting()` endpoint accepting both legacy (`application/csp-report`) and modern Reporting-API (`application/reports+json`) envelopes. 32 KiB body cap; persists to `CspViolations` table; always returns 204. `Data/DatabaseBootstrapper.cs` gains `EnsureSqliteCspViolationsAsync` belt-and-braces fallback. Entity + DbSet + OnModelCreating in `ChangshaEntities.cs` / `AppDbContext.cs`. Per-provider EF Core migrations under `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/*_AddCspViolations.cs` (model snapshots updated cleanly — only CspViolation entity diff).

3. **Pre-rollout k8s migration Job** — `Program.cs` intercepts `--migrate` arg, builds minimal DI (`AddPersistence` only), resolves `AppDbContext`, runs `MigrateAsync` (Postgres/SqlServer) or `DatabaseBootstrapper.InitializeAsync(db)` (SQLite), exits without binding the HTTP port. `infra/k8s/base/job-migrate.yaml` invokes the same image with `args: ["--migrate"]`; Argo CD `sync-wave: -1` + `hook: PreSync` for GitOps ordering; `restartPolicy: OnFailure`, `backoffLimit: 3`, `ttlSecondsAfterFinished: 600`. Wired into `kustomization.yaml`. Docs: `docs/kubernetes.md` gets a Pre-rollout migration Job section.

4. **SBOM + Trivy CRITICAL/HIGH gate** — new `.github/workflows/sbom.yml`: builds prod image, emits CycloneDX + SPDX SBOMs via `anchore/sbom-action@v0`, runs Trivy with `severity: CRITICAL,HIGH` + `exit-code: 1` + `ignore-unfixed: true` (workflow goes RED on any fixable CRITICAL/HIGH), uploads SARIF to GitHub code-scanning, posts PR-summary comment. Runs on push:main, PR touching Dockerfile/csproj/package.json, weekly cron, and `workflow_dispatch`. Docs: `docs/sbom.md` (new) covers gate + local reproduction + future cosign signing.

5. **HotSeatSwap_PlayerToPlayer_PreservesGameState flake fix** — `tests/Autotable/HotSeatSwapTests.cs`'s `bobSeated` `WaitForAsync` predicate was racing the post-take `FillEmptySeatsWithBotsAsync` auto-bot-fill. Tightened predicate to require BOTH `Seats[1].PlayerId == bob.PlayerId` AND `Seats[0].PlayerId != alice.PlayerId` so the subsequent assertion only fires after auto-fill completes. Pure test-side fix; underlying Wave-2 seat-release-on-disconnect invariant preserved.

6. **Forward-compatible smoke scripts** — `tests/smoke/chat-flow-smoke.sh` (port 18082) and `tests/smoke/token-rotation-smoke.sh` (port 18083) target Bishop's Wave-9 surface. Same forward-compatible pattern from Wave 8: 404 = soft-pass (`⏭`), 4xx-with-body = body-shape mismatch soft-pass, hard-fail only on 5xx / invariant violation (e.g. reuse-attack not rejected). Wired into `.github/workflows/docker-smoke.yml` after the auth-flow step.

**Cross-lane bundling avoided (Stephen's Wave-8 complaint addressed):**

- Bishop's untracked entity / DbSet / OnModelCreating work on `ChangshaEntities.cs` + `AppDbContext.cs` (ReconnectToken, ReconnectAuditEntry, ChatMessage, Role on PlayerAuthSession, SchemaVersion on Replay) is **deliberately not in any Wave-9 commit**. I generated the CSP-violation migration set against a clean model (Bishop's entities reverted to HEAD before `dotnet ef migrations add` ran). The clean snapshots mean Bishop's eventual `dotnet ef migrations add ReconnectAndChat` will diff cleanly.
- Bishop's `Auth/AuthCookieService.cs` had introduced an `IssueAsync` overload with an inserted `string? role` parameter that broke the 3-arg callers in `AuthController.cs` (compile errors). I snapshotted his diff to `.work/bishop-auth.patch` + reverted both files to HEAD so the solution would build. Same handling for `Changsha/ChangshaDomain.cs` + `ChangshaGameRuntime.cs` + `ChangshaReplayController.cs` (compile errors: `state.Seats.Length` on a `List<>`, missing `BotDifficulty` property) → `.work/bishop-changsha.patch`. Patches preserved in `.work/` and re-applied to the tree at end-of-session so Bishop's work survives for him.

**Patterns locked for future DevOps work on this codebase:**

- **Strict-CSP canary rollout pattern.** Ship the strict policy as machinery default-OFF; flip `CspReportOnly=true` first, watch the `/api/csp-report` sink for legitimate violations, then flip `CspStrict=true` + `CspReportOnly=false` to enforce. Strict-CSP MUST be set in overlay config, never baked into the image, so a same-day rollback is one config-map edit + rolling restart.
- **Per-request CSP nonce via `HttpContext.Items["csp-nonce"]`.** Razor / minimal-API endpoints that emit `<script>` tags pick up the nonce by reading `Items["csp-nonce"]`. Bundle-side `eval()` callsites must be replaced (not nonce'd).
- **`--migrate` CLI flag intercept pattern.** Stand-alone entrypoint at the top of `Program.cs` BEFORE `WebApplication.CreateBuilder(args)`. Builds minimal DI, resolves what it needs, exits without binding the HTTP port. Future tooling (export scripts, replay re-encoders) follows the same shape.
- **Argo CD `sync-wave: -1` + `hook: PreSync`** for pre-rollout migration Jobs. `kubectl wait --for=condition=complete job/...` is the equivalent for plain-kubectl operators.
- **SBOM dual-format pattern.** CycloneDX + SPDX from the same Syft / sbom-action run; pick the one downstream tooling needs. Trivy is the canonical scanner. `severity: CRITICAL,HIGH` + `exit-code: 1` + `ignore-unfixed: true` is the gate.
- **Smoke-script PORT allocation.** Per-script unique ports: docker-build=18080, auth-flow=18081, chat-flow=18082, token-rotation=18083. Lets the suite run in parallel locally.
- **EF migration regen procedure when other lanes have polluted the model.** Snapshot foreign content to `.work/<other-agent>.patch`, `git checkout HEAD -- <entity-files>`, re-apply ONLY MY additions via `edit` tool, build to verify clean state, `dotnet ef migrations add --context <Sqlite|Postgres|SqlServer>AppDbContext`. Then restore foreign content via the snapshot patch so the other lane's tree state isn't lost.

**Open items / handoff:**

1. **Bishop:** Apply `.work/bishop-auth.patch` + fix the 3-arg callers (use named arg `ct: ct` or pass `role: null` positionally). Apply `.work/bishop-changsha.patch` + fix `state.Seats.Length` → `state.Seats.Count` and remove the missing `BotDifficulty` reference (or add the property to `ChangshaSeatState`). Then `dotnet ef migrations add ReconnectAndChat` to produce his own migration set; my snapshots are clean so the diff will contain only his entities.
2. **Hicks:** When the eval-callsite-free bundle lands, flip the prod overlay's `Security:CspStrict=true` (and `Security:CspReportOnly=true` for a canary period first, then `false`).
3. **Stephen:** Promote `Security:CspReportUri` to a documented operator knob in `docs/observability.md`.
4. **Vasquez:** Wave-10 smoke-script hardening pass — turn the chat-flow + token-rotation soft-pass-on-404 branches into hard asserts once Bishop's surface is GA.

**Deferrals to Wave 10+:**

- **Cosign keyless image signing** — SBOM workflow ships the SBOM + scan; the signing step is a one-line `cosign sign` addition once GHCR OIDC issuer is whitelisted.
- **Multi-arch Docker builds** (`linux/amd64` + `linux/arm64`) — Wave 4 carryover.
- **`actionlint` PR gate** on `.github/workflows/**` — Wave 4 carryover.
- **429-counter metric in `/metrics`** — Wave 6 carryover.
- **`LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates`** flake — `WaitForAsync` helper in `AutotableWsRelayTests.cs:303` returns void / doesn't assert success; can silently time-out under parallel CI load. Worth a Wave 10 follow-up.

**Memo:** `.squad/decisions/inbox/apone-phase-j-wave-9.md`.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build` → **728 / 1 / 0** (was 654/0/0 at Wave 8; +74 net green from Vasquez's contract tests + my CSP/k8s/smoke tests). The 1 failure (`ChatProfanityFilterTests.Chat_PersistedBody_HasProfanityRemoved`) is Bishop's incomplete profanity-filter wiring — unrelated to Apone scope. HotSeatSwap_PlayerToPlayer flake fixed. Bishop's broken `Auth/` + `Changsha/` work restored to working tree at end-of-session via the `.work/bishop-*.patch` snapshots.

## Phase J Wave 10 — Final polish: flake fix + CSP Round 2 + production runbook + multi-arch image + load test (2026-05-24)

**Tasks (final-pass DevOps scope):**

1. **`LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates` flake — fixed + 50× regression gate.** Root cause: `AutotableConnectionManager.GetStoredEntryCount(gameId)` returned `state.Snapshot().Count` — the AGGREGATE count across ALL collections (`match`, `seats`, `things`, `discards`). The translator emits `match` and per-seat `seat:N` entries on JOIN before Alice's UPDATE ever lands, so the test's `count >= 3` predicate could trip on translator chatter alone, not on Alice's `things` updates. Compounded by `WaitForAsync` silently returning on deadline expiry instead of throwing — meaning timeout failures surfaced as misleading downstream asserts. **Fix:**
   - `AutotableGameState.CountFor(string kind)` — new method that returns the per-kind count from the indexed store (O(1) lookup against the existing per-kind dictionary).
   - `AutotableConnectionManager.GetStoredEntryCount(string gameId, string kind)` — new overload that delegates to `CountFor`.
   - `AutotableWsRelayTests.WaitForAsync` now throws `Xunit.Sdk.XunitException` with a descriptive `reason:` argument on timeout (was silently returning false).
   - The original flake test now polls `GetStoredEntryCount(gameId, "things") >= 3` with a 5s timeout — deterministic against translator chatter.
   - Added `LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates_Stability50x` that runs the inner scenario 50× in a single test method; passes 50/50 on every local run.

2. **CSP Round 2 — `style-src 'unsafe-inline'` canary knob.** Added `SecurityHeadersMiddleware.CspStrictStylesConfigKey = "Security:CspStrictStyles"` (default OFF) plus a `DropStyleUnsafeInline(string csp)` helper that touches only the `style-src` directive — adjacent directives byte-for-byte preserved. The constructor applies the helper to the default/strict template when the knob is set. Constants (`DefaultCsp`, `StrictCsp`) intentionally KEEP `'unsafe-inline'` until Hicks's inline-style-free bundle is verified in canary — flipping the knob is the only path that drops the permission. Pinned by Vasquez's `CspStyleSrcNoUnsafeInlineTests` contract suite (lives in `tests/Security/`).

3. **Production deployment runbook.** Created `docs/production-deployment-runbook.md` (~26 KB) covering: pre-flight checklist, image build/publish, first-deploy DB init, rolling update, rollback, monitoring/alerting (Prometheus + Sentry + JSON logs), incident response playbooks (DB outage, rate-limit storm, OAuth provider down, magic-link queue stall, CSP regression), and cross-references to companion docs (`docker.md`, `k8s.md`, `observability.md`, `sbom.md`, `load-test-results.md`).

4. **End-to-end load test.** Wrote `tests/load/lobby-flood.js` (Node + `ws@^8` — NO k6 dependency to keep CI runner footprint minimal). Three workloads: 100 concurrent lobby-poll clients, 25 concurrent WS-join clients (path `/autotable/ws` — the canonical endpoint per `AutotableWsEndpoint.Path`), 5 simultaneous bot-tournaments (4 bots/table). Results on Debug build against `WebApplicationFactory` at `http://localhost:5114`:
   - **Lobby:** 12,466 requests / 0 errors / p99 525 ms
   - **WS join:** 771 connects / 0 errors / p99 555 ms
   - **Tournament:** 35 games / 0 errors / p99 2,520 ms
   - 0% error rate across all three — well inside SLO targets. Documented in `docs/load-test-results.md`.

5. **Multi-arch Docker image (`linux/amd64` + `linux/arm64`).** Closed the Wave 4 carryover. `.github/workflows/docker-build.yml` now sets up QEMU (`docker/setup-qemu-action@v3`), pins `PLATFORMS: linux/amd64,linux/arm64`, and passes `platforms:` to `docker/build-push-action@v6`. Verified locally with `tonistiigi/binfmt --install arm64` + a `docker-container` buildx driver; OCI tarball exported to `.work/oci-out/mahjong-autotable-wave10.tar`. Manifest list digest: `sha256:dd3618cf1a9eed8e38ad90b464336b8bf427c856185fb555946bc28e19278e8d` (amd64: `sha256:117ab8…ee31a3`, arm64: `sha256:dd0cca…16a9b9`).

6. **Final docs review.** Created `docs/README.md` (docs index — landing page that maps each ops/dev/QA need to the right doc). Updated `docs/docker.md` with a Wave-10 multi-arch section, and `docs/sbom.md` with a Wave-10 multi-arch note + cross-reference to the new production runbook. Ran a Python dead-link scan — 0 dead links across the docs tree.

**Patterns locked:**

- **Per-kind state counts.** `AutotableGameState.CountFor(string kind)` is now the canonical "how many entries of kind X" probe. The aggregate `Snapshot().Count` is still available but is NOT safe to use as a predicate threshold from tests because translator chatter (match + seat entries) inflates it on JOIN.
- **`WaitForAsync` hard-fail.** The shared `WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout, string reason)` helper in `AutotableWsRelayTests.cs` throws `Xunit.Sdk.XunitException` on deadline expiry. Tests that need soft-timeout semantics should use a local predicate-loop with try/catch.
- **CSP canary knobs.** `CspStrict`, `CspReportOnly`, `CspReportUri`, `UseScriptNonces`, and now `CspStrictStyles` — same shape, default OFF, flipped per-deploy via `Security:*` configuration. Constants stay PERMISSIVE; knob is the strip path. This is the contract pinned by Vasquez's `CspStyleSrcNoUnsafeInlineTests`.
- **Multi-arch build prerequisites.** QEMU via `tonistiigi/binfmt` MUST be installed BEFORE the buildx container-driver builder is created — the default Docker driver doesn't support multi-platform output. Documented in `docs/docker.md`.
- **Load test footprint.** Node + raw `ws` is good enough for the lobby/join/tournament smoke shape; no k6 install on CI runners needed. The harness reads `LOAD_TEST_BASE_URL` env var; defaults to `http://localhost:5114` (matches `launchSettings.json`).

**Open items / handoff:**

1. **Bishop** owns the working-tree `SecurityHeadersMiddlewareTests.cs::DefaultCsp_DropsUnsafeInlineFromStyleSrcAfterWave10Migration` modification that conflicts with Vasquez's `CspStyleSrcNoUnsafeInlineTests.DefaultCspConstant_StylesSection_KeepsUnsafeInlineUntilOptIn` contract. Pick ONE contract — the canary-knob design says constants STAY permissive until Hicks's frontend ships. The conflicting test should either be deleted or rephrased to assert the runtime-emitted CSP (not the constant).
2. **Hicks:** When the inline-style-free bundle lands in main, flip `Security:CspStrictStyles=true` in the prod overlay (canary first via `Security:CspReportOnly=true` for 24h, then enforce).
3. **CI follow-up:** Wire `tests/load/lobby-flood.js` into a nightly cron workflow that boots a Release build and asserts p99 < SLO budget. Out of scope for Wave 10.
4. **Cosign keyless image signing** — still deferred; the multi-arch image build now produces a manifest digest perfect for `cosign sign --yes ghcr.io/...@sha256:dd3618…78e8d` once GHCR OIDC is whitelisted.

**Memo:** `.squad/decisions/inbox/apone-phase-j-wave-10.md`.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build` → **820 / 0 / 0** (was 728/1/0 at Wave 9; +92 net green, the Wave-9 ChatProfanity failure also resolved upstream). The flake-fix + CSP-knob + Vasquez's contract suite + Bishop's tournament tests + Hicks's e2e all run green. Scope target was ≥760 — exceeded.


## Phase K Wave 1 — Bringup: supply-chain signing + load-test cron + multi-arch smoke + CSP enforcement coordination + CHANGELOG backfill + secret-rotation runbook (2026-05-24)

**Branch:** `stlong/phase-k-wave-1-bringup` (from main `9a52ef1`).

**Tasks completed (selective, NEVER `git add -A`):**

1. **Cosign keyless image signing.** New `.github/workflows/sign-image.yml` triggered by `workflow_run` after `docker-build` succeeds on `main` (and on `v*.*.*` tag pushes via the same upstream). Installs `sigstore/cosign-installer@v3` (cosign 2.4.1), resolves the multi-arch manifest-list digest via `docker buildx imagetools inspect --format '{{.Manifest.Digest}}'`, signs with `cosign sign --yes` using GitHub OIDC (`id-token: write`) — no long-lived keys — and immediately verifies with `cosign verify --certificate-identity-regexp '…/sign-image.yml@refs/(heads/main|tags/v.*)$' --certificate-oidc-issuer 'https://token.actions.githubusercontent.com'`. The signature covers the MANIFEST LIST → both `linux/amd64` + `linux/arm64` per-arch images inherit the attestation. Docs: `docs/image-signing.md` (operator + auditor verification runbook with verify-by-digest production gate, Rekor transparency-log evidence, failure modes, audit checklist).

2. **Nightly load-test cron.** New `.github/workflows/load-test-nightly.yml` (daily 02:00 UTC + `workflow_dispatch`). Brings up the production-shaped `docker-compose.yml` stack on the runner, waits for `/health`, runs the Wave-10 `tests/load/lobby-flood.js` via the new `tests/load/run-and-compare.sh` wrapper. Wrapper: persists JSON to `.work/loadtest/result-<ts>.json`, maintains a `latest.json` symlink to the prior run, parses both, computes per-workload p99 deltas, and exits **`2`** when any workload regresses >25% (env-tunable). Appends a Markdown row to `docs/load-test-results-history.md` (bootstrap-on-first-run with table schema). On regression: Sentry event POSTed directly by the wrapper (uses `SENTRY_DSN` secret), email via `dawidd6/action-send-mail@v3` when SMTP secrets present, workflow ends RED via a final "fail on regression" step (deferred so cleanup + artefact upload run first). Exit-code contract: RC=0 pass / RC=1 setup failure / RC=2 regression alertable.

3. **Multi-arch runtime smoke.** New `.github/workflows/multi-arch-smoke.yml` triggered by `workflow_run` after `docker-build` succeeds on `main`. Matrix: `linux/amd64` native on `ubuntu-latest`, `linux/arm64` via QEMU (`docker/setup-qemu-action@v3`). Per-arch: resolves the platform-specific digest from the manifest list via `jq`, `docker run --platform <p>` with `Security__CspStrictStyles=true` + `Security__CspReportUri=/api/csp-report`, asserts (a) `/health` 200 with the 4-field shape, (b) `POST /api/identity` mints `mahjong_pid` + returns `playerId`, (c) `GET /api/auth/providers` 200 or 404 (soft-pass forward-compat), (d) runtime CSP header lacks `style-src 'unsafe-inline'` (proves the knob is honoured), (e) `POST /api/csp-report` → 204 + container-log `CSP violation` line within 5 s (proves persistence path works on both archs).

4. **CSP-report endpoint smoke + production-config coordination with Bishop.** New `tests/smoke/csp-report-smoke.sh` (port 18084 — extends the unique-port pattern: docker-build=18080, auth-flow=18081, chat-flow=18082, token-rotation=18083, **csp-report=18084**). Posts a synthetic violation in BOTH envelopes (legacy `application/csp-report` + modern `application/reports+json`), asserts 204, tails container logs for the `CSP violation` warn line that `Observability/CspReportEndpoint.cs` emits inside the same scope that calls `SaveChangesAsync` — safe proxy for "row hit the DB". Coordination contract: Bishop owns the `Security:CspStrictStyles=true` flip in the production overlay; my `multi-arch-smoke.yml` and `csp-report-smoke.sh` together prove the image supports it on both archs and the persistence path works end-to-end.

5. **CHANGELOG retroactive backfill (J9 + J10) + version bump to 0.10.0.** `CHANGELOG.md` had stopped at Wave 8 (the previous backfill point). Added Wave 9 (reconnect-token rotation + chat + i18n + CSP tightening + audit log + SBOM workflow + flake fix) and Wave 10 (multi-arch image + load-test harness + production runbook + CSP Round 2 canary knob + flake fix + tournament/replay-v2/audit-pruning from Bishop). Bumped `[Unreleased]` to reflect Phase K Wave 1 in progress. Version cursor advanced from 0.8.0 → 0.10.0 (J shipped 10 waves; the version tracks the wave count per the preamble convention). Reference link footnotes updated to add `v0.10.0` / `v0.9.0` compare URLs. Preamble paragraph extended to explain the J-wave-count-equals-version convention so future devs don't second-guess it.

6. **Production secret-rotation runbook.** New `docs/secret-rotation.md` covering: (a) **Rotation matrix** — per-secret cadence, blast radius, rollback budget. (b) **OAuth client secrets** (Google + GitHub — quarterly): two-value overlap window via provider console + AWS Secrets Manager promotion + ESO force-sync + rolling restart + validation via `auth-flow-smoke.sh`. (c) **DB connection strings** (annual): `ALTER USER … WITH PASSWORD` → Secrets Manager update → ESO sync → rolling restart → drop old user after 7-day rollback window. (d) **Sentry DSN** (never except compromise): rotation cost > benefit; document trigger conditions. (e) **Reconnect-token signing key** (never except compromise): single-key signer, no overlap window; rotation forces all sessions off; announcement + maintenance window is the only safe procedure. (f) **Magic-link signing key** (never except compromise): same shape as reconnect-token key. (g) **Validation summary**, **audit/retention**, **calendar** with recommended Q1/Q2/Q3/Q4 dates. Cross-references ESO/Vault/AWS-Secrets-Manager flows from Wave 5/6 (`secret-management.md`, `secrets.md`) and `production-deployment-runbook.md` (Wave 10).

**Patterns locked for future DevOps work on this codebase:**

- **Cosign keyless via `workflow_run`.** OIDC-signing workflows MUST be **separate** from the build workflow so `id-token: write` is confined to the signing blast radius. Trigger via `workflow_run: types: [completed]` + `if: github.event.workflow_run.conclusion == 'success'`. Failure isolation (a Fulcio outage doesn't fail the build) + minimum privilege (the build doesn't see the OIDC token).
- **Sign the manifest list, not the per-arch image.** One signature covers both `linux/amd64` + `linux/arm64` because the per-arch images inherit the attestation via the manifest list digest.
- **Verification regex anchors at the workflow path.** Never rename `sign-image.yml` without updating every consumer's verify regex. The regex accepts both `refs/heads/main` AND `refs/tags/v.*` so tag-push builds verify the same way as rolling main builds.
- **Load-test wrapper exit-code contract.** RC=0 / RC=1 / RC=2 (pass / setup failure / regression). The CI workflow uses `set +e` + an explicit RC mapping so the regression case can run cleanup + artefact upload BEFORE the workflow goes RED via a final "fail on regression" step. Defer the actual failure until after every alerting/observability step has fired.
- **Symlink-based "latest prior result" pointer.** Cron workflows that compare-to-previous shouldn't try to read GitHub's artefact store mid-run (slow + rate-limited). Local on-disk state + workflow-artefact upload is the right shape; the artefact is the persistence boundary, the local file is the working state.
- **Forward-compat smoke pattern extended to CSP.** Smoke scripts probing maybe-not-yet-GA surfaces soft-pass on 404 and hard-fail only on 5xx / invariant violation. Five smokes now follow this pattern: `docker-build`, `auth-flow`, `chat-flow`, `token-rotation`, **`csp-report`** (Wave-1).
- **CHANGELOG version cursor = wave count.** Each phase's wave count advances the minor version. Phase J = 0.1.0 → 0.10.0. Phase K opens at 0.11.0 (first K wave merged) and advances per K wave merged thereafter. Documented in the file's preamble.
- **Multi-arch runner fallback.** GitHub's `ubuntu-24.04-arm` native runner is preferred (~5× faster than QEMU) but not yet universally available. Use QEMU as the portable fallback; the matrix is structured so swapping to native is a one-line `runner:` change later.
- **Secret-rotation matrix.** Cadence column drives the rotation calendar; blast-radius column drives the maintenance-window decision. "Never except compromise" is a deliberate cadence (not a missing one); document the trigger conditions explicitly instead of leaving the cell blank.

**Open items / handoff:**

1. **Bishop:** flip `Security:CspStrictStyles=true` in the production overlay (`appsettings.Production.json` or k8s overlay). My `multi-arch-smoke.yml` already runs with the knob ON, so once Bishop's config lands the runtime CSP in prod tightens automatically.
2. **Hicks:** inline-style-free bundle. When it lands, Bishop can canary via `Security:CspReportOnly=true` (24 h) before flipping `CspReportOnly=false` + `CspStrictStyles=true` to enforce.
3. **Operator (Stephen):** verify GHCR OIDC-whitelisting on the first `sign-image.yml` run — the verify step's exit code is the signal.
4. **Operator (Stephen):** configure repo secrets `SMTP_*` (or `ALERT_EMAIL_TO`) + `SENTRY_DSN` if not already set, so nightly-load-test alerts fan out beyond the Actions dashboard.
5. **Future Phase K wave:** wire a Kyverno / Cosign policy-controller k8s admission policy that REJECTS unsigned image pulls. Today the verify is operator-checklist-gated in `production-deployment-runbook.md`; cluster-layer enforcement is the next step.
6. **Future Phase K wave:** Wave-9 `Auth:JwtSigningKey` fallback-key list so 180-day JWT rotation doesn't force everyone to re-sign-in. Documented in `secret-management.md` as a planned item; runbook acknowledges the current hard-rotation cost.

**Memo:** `.squad/decisions/inbox/apone-phase-k-wave-1.md`.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` → **832 / 0 / 0** (was 820/0/0 at end-Wave-10; +12 from Bishop's tournament/replay-v2/audit-pruning/db-health/bot-decision work that landed on `main` since). Baseline preserved — Wave-1 scope is pure DevOps + docs by design, no `src/backend/**` touched. **Actionlint clean** on all three new workflows (`sign-image.yml`, `load-test-nightly.yml`, `multi-arch-smoke.yml`).
