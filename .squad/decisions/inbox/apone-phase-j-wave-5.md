# Phase J Wave 5 — Apone (DevOps): Playwright E2E + /metrics + structured logging + secrets audit

**By:** Apone (DevOps / Platform Engineer), 2026-05-23

**Branch:** `stlong/phase-j-wave-5-completion`
**Primary commit:** `072fd00` — `feat(devops): Phase J Wave 5 — Playwright E2E + /metrics + structured logging + secrets audit`

---

## Wave goal

Close the four DevOps gaps surfaced by the Wave-4 retro for the
Changsha pre-1.0 launch checklist:

1. **End-to-end browser coverage.** The repo has unit/contract/SignalR
   tests but no `npm` script that actually clicks the UI. A regressing
   bundle could ship green CI today.
2. **CI hook for that coverage.** Even with a smoke spec on disk, we
   need a workflow that runs it on every PR against `main`.
3. **Operator-visible observability.** Wave-3 added `/health` (boolean
   liveness) but operators have no scrape endpoint and no structured
   logs to ship to Loki / CloudWatch / Splunk. A 4 a.m. incident has
   no breadcrumbs.
4. **Secrets posture.** No checked-in audit of where credentials live,
   what the env-var contract is, or what secret-store recipes the next
   deployment should pick up.

## What shipped

### 1. Playwright E2E scaffold (`src/frontend/autotable-src/tests/e2e/`)

- **`playwright.config.ts`** — `baseURL` resolves from `E2E_BASE_URL`
  (default `http://localhost:8080/autotable/`). Two projects:
  `chromium` (desktop) + `mobile-chrome` (Pixel 5 device descriptor,
  `isMobile: true`, `hasTouch: true`). Reporter is `'github'` in CI,
  `'list'` locally — surfaces failures cleanly in the GHA log.
- **`smoke.spec.ts`** — 4 tests, ~6s end-to-end:
  - `loads the autotable shell` — title `/autotable/i`, body visible.
  - `lobby controls are reachable` — `lobby-quick-match` and three
    `lobby-seat-preview-N` testids visible. Sticks to testids that
    actually live in HEAD's `index.html` (selectors.md is aspirational
    for many entries Hicks's WIP will add later).
  - `Quick Match starts a game shell` — clicks the Quick Match button
    via `el.click()` JS-dispatch (mobile-chrome touch synthesis was
    swallowing the click without it) and polls `page.url()` until it
    matches `/[?&]variant=/`. `buildUrl()` in `src/lobby.ts:328–342`
    *always* emits `variant=`, so the post-click URL is a viewport-
    portable signal that the Quick Match handler ran end-to-end.
  - `mobile drawer toggle is visible on Pixel 5` — chromium-only
    `test.skip()` guard so the test only fires on the mobile project.
- **`README.md`** — local quickstart (`npm run e2e:install` then
  `npm run e2e`), CI usage, troubleshooting (notable: `force: true`
  rationale for the off-screen settings drawer at `right: -340px,
  z-index: 1080`), future-wave notes.

`package.json` gains two scripts (`e2e`, `e2e:install`) and one
devDependency (`@playwright/test ^1.45.0`). `package-lock.json`
regenerated.

### 2. `.github/workflows/e2e-playwright.yml`

- **Triggers:** push to `main`, PR to `main`, `workflow_dispatch`.
- **Steps:**
  1. `actions/checkout@v4`.
  2. `actions/setup-node@v4` with `node-version: '20'` and
     `cache: npm` keyed on the frontend lockfile.
  3. `npm ci` in `src/frontend/autotable-src`.
  4. `npx playwright install --with-deps chromium` — single-browser
     install keeps the runner footprint small.
  5. `docker build` of the repo-root `Dockerfile` (BUILD_SHA passthrough
     so `/health` surfaces the GH run sha).
  6. `docker run -d -p 8080:8080`.
  7. Wait up to 30s for `/health` to return `200`.
  8. `E2E_BASE_URL=http://localhost:8080/autotable/ npm run e2e`.
  9. Tear down the container (always runs).
  10. Upload `playwright-report/` as an artifact when the test step
      failed (`if: failure()`).
- **actionlint v1.7.7:** exit 0, no findings.

### 3. `/metrics` Prometheus endpoint

- **New file:** `src/backend/src/Mahjong.Autotable.Api/Observability/MetricsEndpoint.cs`
  (~114 lines). Three gauges in canonical Prometheus text exposition
  format v0.0.4:
  - `mahjong_uptime_seconds` — anchored to
    `Process.GetCurrentProcess().StartTime.ToUniversalTime()` rather
    than the static-init-time `DateTimeOffset.UtcNow` (the latter races
    with first-scrape lazy-init and produced ~0 uptime on the first
    request during early dev). `try/catch` falls back to `UtcNow` for
    sandboxed AOT runtimes where `GetCurrentProcess()` throws.
    `Math.Max(0.0, …)` clamps defensively.
  - `mahjong_active_games_total` — reads
    `IChangshaGameRuntime.GameCount` from DI (Bishop's Phase I Wave 2
    addition; no extra runtime surface needed).
  - `mahjong_build_info{sha="..."} 1` — `BUILD_SHA` env var with
    `string.IsNullOrEmpty(sha) => "dev"` collapse so dashboards never
    display a blank label. Mirrors `/health`'s buildSha contract.
- **No new NuGet dependency** — written against
  `System.Diagnostics`, `System.Globalization`, `System.Text`. A heavier
  instrument lib (`prometheus-net.AspNetCore`) is reserved for a
  follow-up wave per the rationale comment in the file.
- **Wiring:** `Program.cs` adds `app.MapGet("/metrics", sp =>
  MetricsEndpoint.Render(sp));` returning `Results.Text(..,
  "text/plain; version=0.0.4")`.

### 4. Structured JSON logging

- **`Program.cs`** — adds an env-aware logger block immediately after
  `WebApplication.CreateBuilder(args)`:
  - `builder.Logging.ClearProviders()` first (otherwise the default
    Console provider double-emits each entry alongside the JSON one —
    confirmed empirically in the verify worktree).
  - `IsProduction()` branch → `AddJsonConsole` with
    `IncludeScopes = true`, `UseUtcTimestamp = true`,
    `TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ "`, and
    `JsonWriterOptions { Indented = false }` so each entry is a single
    line a log shipper (Loki promtail, Vector, CloudWatch agent) can
    ingest without buffering.
  - Non-production branch → `AddSimpleConsole` with `SingleLine = true`
    + `IncludeScopes = true` so `dotnet run` / `docker compose up`
    stays developer-friendly.
- `IncludeScopes = true` in both modes — SignalR's `ConnectionId` /
  `HubMethodName` scopes surface in the JSON payload, which is the
  delta that makes a 4 a.m. WebSocket-drop investigation tractable.

### 5. `docs/observability.md`

- Endpoint catalog (`/health`, `/metrics`, future `/api/system/*`).
- Metric definitions for the three gauges with units / cardinality /
  expected ranges.
- Sample exposition output (live-captured from the verify container).
- PromQL examples (`rate(mahjong_uptime_seconds[5m])`, alerting on
  uptime drop, build-info label join for "what version is up").
- LogQL examples for Loki (`{app="mahjong-autotable"} | json |
  ConnectionId="…"`).
- KQL example for Azure Log Analytics / Application Insights.
- Runbook snippets — "operator gets paged, what do they look at".

### 6. `docs/secrets.md`

- **Audit findings:**
  - `appsettings.json` ships a placeholder SqlServer password
    (`YourStrong!Passw0rd`) — not a real credential but documented as
    needing env-var override when migrating off the default SQLite
    provider.
  - No real secrets in tracked source. Confirmed via
    `git grep -i "password\|secret\|token\|api[_ -]?key"` over
    `src/`, `docs/`, `.github/`.
- **Env-var contract:** `BUILD_SHA`, `ASPNETCORE_ENVIRONMENT`,
  `ConnectionStrings__Sqlite`, `ConnectionStrings__SqlServer`,
  `Persistence__Provider`, `ChangshaRuntime__*`. Documented with
  defaults, format, and required-vs-optional table.
- **Recipes:** Docker secrets (`--secret`, `_FILE` convention), GitHub
  Actions encrypted secrets, Kubernetes `Secret` + projected volume,
  AWS Secrets Manager (sidecar + `ECS_CONTAINER_METADATA_URI`), Azure
  Key Vault (Workload Identity), GCP Secret Manager (CSI driver).
- **Rotation policy:** baseline 90-day cadence + the exact paths the
  next deployment needs to flip.

## Verification

- **Backend tests:** `dotnet test` over `src/backend` →
  **445 / 0 / 0** (includes Vasquez's untracked
  `MetricsEndpointTests.cs` contract tests that exercise the new
  `/metrics` route — they pass against my implementation).
- **Live Docker:** Built in a separate worktree
  (`/data/source/mahjong-w5-verify`) for clean verification away from
  Bishop's parallel WIP. Container ran on `localhost:8088`:
  - `GET /health` → `{"status":"healthy","buildSha":"test-phase-j-w5","uptime":"00:00:13.0921…","version":"1.0.0.0"}`.
  - `GET /metrics` → valid Prometheus exposition; `mahjong_uptime_seconds`
    grew monotonically across successive scrapes (5.080s → 13.092s →
    21.115s) confirming the StartTime anchor works.
  - Production logs were one JSON document per line.
- **Playwright smoke against the live container:** two consecutive
  runs → **7 passed / 1 skipped** in 6.1–6.2s. The skip is the
  intentional chromium-only `mobile drawer toggle` guard on the
  mobile project.
- **actionlint v1.7.7:** clean on `e2e-playwright.yml`.

## Sample `/metrics` output

```
# HELP mahjong_uptime_seconds Seconds since the API process was started.
# TYPE mahjong_uptime_seconds gauge
mahjong_uptime_seconds 21.115

# HELP mahjong_active_games_total Number of Changsha games currently tracked by the runtime.
# TYPE mahjong_active_games_total gauge
mahjong_active_games_total 0

# HELP mahjong_build_info Constant gauge labelled with the build SHA baked into the image.
# TYPE mahjong_build_info gauge
mahjong_build_info{sha="test-phase-j-w5"} 1
```

## Cross-lane notes

- **No backend domain code touched.** `MetricsEndpoint.cs` consumes
  the existing `IChangshaGameRuntime.GameCount` surface Bishop added
  in Phase I Wave 2 — I did **not** add an `ActiveGameCount` property
  to the interface, which would have collided with Bishop's parallel
  hub/runtime work.
- **No frontend `src/` code touched.** The smoke spec works against
  the bundle as-is and uses only testids that exist in HEAD's
  `index.html`.
- **`selectors.md` is aspirational** for ~13 of the 19 entries
  Vasquez documented (`lobby-toggle`, `lobby-players-*`, `lobby-apply`,
  the picker fieldsets, `connection-banner*`). The smoke spec uses
  only the ones currently in the bundle. Future waves can broaden
  coverage as testids land.

## Patterns locked for future DevOps work

- **`force: true` is the canonical Playwright escape hatch** when the
  visual stack is correct but the hit-test layer disagrees (off-screen
  drawers with high `z-index`). On `isMobile: true` projects, even
  `force: true` can be insufficient — `locator.evaluate(el =>
  el.click())` to fire the JS `click` event directly is the next step
  and proved viewport-portable across chromium + mobile-chrome.
- **`Process.StartTime` over `DateTimeOffset.UtcNow` at static init**
  for any "since process start" anchor — static fields lazily init on
  first type touch which produces a near-zero diff on the first
  endpoint hit. Wrap in `try/catch` for AOT scenarios.
- **`builder.Logging.ClearProviders()` is mandatory** before
  `AddJsonConsole` / `AddSimpleConsole` — otherwise the default
  Console provider double-emits and operators see each line twice.
- **Env-aware logger config** (`IsProduction()` switch) — Production
  gets JSON, everything else gets human-readable. Keeps `dotnet run`
  ergonomic without losing structured ingestion in deployment.
- **Verify in a separate worktree** when Bishop / another agent is
  writing to the same source tree in parallel. `git worktree add`
  off detached HEAD is the cheapest way to get a clean live-build
  environment without fighting concurrent edits.
