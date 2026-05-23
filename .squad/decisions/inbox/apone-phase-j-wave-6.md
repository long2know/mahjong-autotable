# Apone — Phase J Wave 6 memo

**Branch:** `stlong/phase-j-wave-6-completion`
**Commit:** `408e0d1` — `feat(devops): Phase J Wave 6 — rate limiting + CORS + reverse-proxy / systemd / log-rotation guides`
**Date:** 2026-05-23
**Author:** Apone (DevOps / Platform Engineer)

---

## What shipped

### Task 1 — Production rate limiting

Wired `Microsoft.AspNetCore.RateLimiting` (the framework built-in since
.NET 7) with two **IP-partitioned** named policies. All policy
configuration lives in a single, well-documented extension class so
future maintenance is straightforward.

| Policy name (constant) | Backing limiter | Quota | Applied to |
| --- | --- | --- | --- |
| `fixed-window-anonymous` (`AnonymousPolicy`) | `FixedWindowRateLimiter`, partitioned by client IP | 10 req / minute / IP | Reserved — Bishop's future `POST /api/identity` profile-create surface. Apply via `.RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)`. |
| `token-bucket-api` (`ApiPolicy`) | `TokenBucketRateLimiter`, partitioned by client IP | 30-token bucket, 5 tokens/sec refill (≈300 req/min/IP, 30-burst) | `MapControllers()` + minimal-API `/api/system/persistence` + `/api/changsha/pattern-ordering` (via `.RequireRateLimiting(ApiPolicy)`). |

**Off-policy (deliberately unlimited):**

- `GET /health`, `GET /api/health` — probe surfaces. `.DisableRateLimiting()`.
- `GET /metrics` — Prometheus scrape. `.DisableRateLimiting()`.
- `/hubs/changsha` — SignalR long-lived transport.
- `/autotable/ws` — raw WS transport (same reasoning as SignalR).

**Configuration gate.** `RateLimiting:Enabled` controls whether the
middleware is registered at all:

- `appsettings.json`: `false` (so `Development` + xUnit's
  `WebApplicationFactory.UseEnvironment("Development")` harness skip
  the middleware — **no regression on the 445 test gate**).
- `appsettings.Production.json` (NEW): `true`.
- Override via env: `RateLimiting__Enabled=false` (for stress tests).

**Rejection contract.** 429 status, body `{"error":"too_many_requests"}`,
`Retry-After` header populated from the limiter lease metadata.

**Partition key.** Prefers `X-Forwarded-For` first (so the limiter
sees the real client behind nginx / Caddy without needing the
`ForwardedHeaders` middleware), falls back to
`Connection.RemoteIpAddress`, then to `"unknown"`. See
`RateLimitingExtensions.ResolvePartitionKey`.

### Task 2 — Config-driven CORS

Replaced the hard-coded localhost-only list with
`Cors:AllowedOrigins` from configuration:

| Env | Default |
| --- | --- |
| `appsettings.json` (base) | `["http://localhost:5114", "https://localhost:7135", "http://localhost:5173", "http://localhost:8080"]` |
| `appsettings.Production.json` | `[]` (empty — production deploys must set the public origin) |

Production override:

```bash
docker run ... -e Cors__AllowedOrigins__0=https://mahjong.example.com
```

`AllowCredentials()` retained — the autotable bundle needs the
`mahjong_pid` cookie + SignalR auth cookie. ASP.NET refuses to combine
`AllowCredentials()` with `AllowAnyOrigin()` as a CSRF mitigation, so
the origins must be enumerated explicitly. Documented in `docs/secrets.md`.

### Task 3 — Reverse-proxy / systemd / log-rotation samples

| File | Purpose |
| --- | --- |
| `infra/nginx/mahjong.conf.example` | nginx config — TLS on 443, plain → HTTPS redirect on 80, Let's Encrypt challenge prefix, WebSocket Upgrade locations for `/hubs/` + `/autotable/ws` with 24-hour `proxy_read_timeout`, `X-Forwarded-For` / `X-Forwarded-Proto` propagation, commented-out basic-auth gate for `/metrics`. |
| `infra/caddy/Caddyfile.example` | Caddy v2 config — auto-TLS via ACME, `reverse_proxy 127.0.0.1:8080`, 24-h transport timeouts for long-lived WS, JSON access log with rolling, commented-out basicauth gate for `/metrics`. |
| `infra/systemd/mahjong-autotable.service.example` | systemd unit — `Type=simple`, `Restart=on-failure`, `LimitNOFILE=65536`, `NoNewPrivileges=true`, `ProtectSystem=full`, `EnvironmentFile=-/etc/default/mahjong-autotable`, `--log-opt max-size=10m max-file=5` for built-in rotation. |

### Task 4 — Documentation

| File | Notes |
| --- | --- |
| `docs/reverse-proxy.md` (NEW) | Operator guide for nginx + Caddy. Why a reverse proxy, sample configs, certbot quick start, X-Forwarded-* discussion. |
| `docs/log-rotation.md` (NEW) | Docker `json-file` `max-size` / `max-file` opts (recommended), daemon-wide default in `/etc/docker/daemon.json`, alternative `logrotate(8)` config for bind-mounted log files. |
| `docs/systemd.md` (NEW) | Unit-install walkthrough, redeploy workflow, troubleshooting matrix, uninstall. |
| `docs/deployment.md` | Appended sections § 12-16: Reverse proxy / systemd / log rotation / CORS / Rate limiting. |
| `docs/secrets.md` | Added "CORS origins (Phase J Wave 6)" subsection + extended env-var table with `Cors__AllowedOrigins__0` and `RateLimiting__Enabled`. |

---

## Test gate

```
$ dotnet test src/backend/Mahjong.Autotable.slnx --nologo
Passed!  - Failed:     0, Passed:   445, Skipped:     0, Total:   445, Duration: 14 s
```

✅ **445 / 0 / 0** — identical to the Wave 5 baseline. Tests run under
`Development` env via `WebApplicationFactory.UseEnvironment("Development")`;
the `RateLimiting:Enabled=false` default in `appsettings.json` means the
middleware never registers in test, so no rate-limit interference.

---

## Live verification

Published Release build of the API, ran with
`ASPNETCORE_ENVIRONMENT=Production`, then:

- `GET /health` → 200, valid JSON, JSON-line structured logs on stdout ✅
- `GET /metrics` → 200, valid Prometheus exposition ✅
- `GET /api/changsha/pattern-ordering` × 50 rapid: requests #1-30 → 200,
  request #31 → 429 ✅ (token bucket capacity = 30 confirmed)
- `GET /health` × 80 rapid → all 200, never throttled ✅
- `GET /metrics` × 80 rapid → all 200, never throttled ✅

Scratch dir (`scratch/apone-w6/`) deleted after smoke; not committed.

---

## Cross-lane coordination

### Hicks (frontend)

**Playwright specs run under `Development` → `RateLimiting:Enabled=false`
by default.** No spec changes required for the Wave 6 ship. If Hicks
adds Production-env specs in a future wave, two options:

1. Override `RateLimiting__Enabled=false` in the e2e docker-run command
   (recommended — keeps the spec aligned with intended production
   behavior except for the limit).
2. Configure the spec to use a single deterministic IP partition key,
   then keep request counts within the policy ceiling.

### Bishop (backend)

- The token-bucket policy is auto-applied to `MapControllers()` —
  **every new MVC controller Bishop adds under `/api/*` inherits the
  300-req-per-minute-per-IP limit**. If he ships a high-volume
  endpoint (e.g. a polling/lobby endpoint) that needs to exceed the
  ceiling, opt that endpoint out with `.DisableRateLimiting()` on the
  controller / action attribute, or apply a new policy.
- The `fixed-window-anonymous` policy is registered but not yet
  applied — when Bishop ships `POST /api/identity` (or similar
  unauthenticated mutating endpoint), apply it via:
  ```csharp
  app.MapPost("/api/identity", ...)
     .RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy);
  ```
- SignalR hub methods (`SetGamePublic`, `JoinRandom`, etc.) are NOT
  rate-limited — the limiter middleware only sees the WS handshake,
  not subsequent hub invocations. If hub-method-level throttling is
  needed, that's a Bishop call (add an `IHubFilter` decorator).
- `GameCompleted` broadcasts go through the hub, so they're unaffected.

### Vasquez (tests)

**Test pattern for rate-limit behavior:**

```csharp
public Task InitializeAsync()
{
    _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        // Switch to Production so the gate flips on
        b.UseEnvironment("Production");
        b.UseSetting("RateLimiting:Enabled", "true");
        b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
        // ChangshaRuntimeOptions overrides as before
    });
    return Task.CompletedTask;
}

[Fact]
public async Task ApiPolicy_Returns429AfterBurst()
{
    using var client = _factory!.CreateClient();
    // Hit /api/system/persistence 30 times — all should succeed
    for (int i = 0; i < 30; i++)
    {
        var ok = await client.GetAsync("/api/system/persistence");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }
    // Request #31 → 429
    var rejected = await client.GetAsync("/api/system/persistence");
    Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    Assert.True(rejected.Headers.RetryAfter is not null);
}
```

Suggested wave-6 test names (NOT mine to write — Vasquez owns):

- `RateLimitingTests.ApiPolicy_Returns429AfterBurst`
- `RateLimitingTests.AnonymousPolicy_Returns429After10InOneMinute`
- `RateLimitingTests.HealthEndpoint_IgnoresPolicy`
- `RateLimitingTests.MetricsEndpoint_IgnoresPolicy`
- `RateLimitingTests.Disabled_NoLimitApplied` (boots with `RateLimiting:Enabled=false`, hits 100x, never 429)

The xunit `IClassFixture` pattern (or just per-test factory) keeps the
per-IP partition fresh between tests.

---

## Lane discipline

Selective `git add` — exactly 12 files mine:

```
docs/deployment.md                                                              (M)
docs/log-rotation.md                                                            (A)
docs/reverse-proxy.md                                                           (A)
docs/secrets.md                                                                 (M)
docs/systemd.md                                                                 (A)
infra/caddy/Caddyfile.example                                                   (A)
infra/nginx/mahjong.conf.example                                                (A)
infra/systemd/mahjong-autotable.service.example                                 (A)
src/backend/src/Mahjong.Autotable.Api/Program.cs                                (M, surgical)
src/backend/src/Mahjong.Autotable.Api/RateLimiting/RateLimitingExtensions.cs    (A)
src/backend/src/Mahjong.Autotable.Api/appsettings.Production.json               (A)
src/backend/src/Mahjong.Autotable.Api/appsettings.json                          (M, Cors + RateLimiting sections)
```

**Untracked files explicitly NOT staged** (other agents own them):

- `src/frontend/autotable-src/{index.html,src/lobby.ts,src/identity.ts,src/leaderboard.ts}` — Hicks WIP
- `.github/workflows/squad-*.yml` — pre-session scaffolding
- `.copilot/skills/error-recovery/`, `.tool-actionlint/`, `.work/` — local tooling

**Did NOT touch** (per scope rule): production domain code
(Matchmaking, Players, Changsha, RateLimiting policies for game actions
— Bishop's lane), frontend (Hicks's lane), tests (Vasquez's lane).

---

## Patterns locked for future DevOps work

- **`RateLimitPartition.GetFixedWindowLimiter` + `AddPolicy` callback** is
  the only correct way to get **per-IP** rate limiting. The framework's
  simpler `AddFixedWindowLimiter("name", o => ...)` overload creates a
  **single shared bucket** for all callers — quietly wrong if your
  intent is "N req/min/IP". Partition by IP via the `httpContext =>`
  callback every time.
- **`X-Forwarded-For` first, `RemoteIpAddress` fallback** is the
  partition-key shape that works whether or not
  `ForwardedHeaders` middleware is wired. Centralize in one
  `ResolvePartitionKey` helper so every policy uses identical
  attribution.
- **Config gate + middleware short-circuit.** Don't wire
  `app.UseRateLimiter()` unconditionally — gate on `RateLimiting:Enabled`
  so dev / test envs skip the middleware entirely. This is more
  defensive than "register middleware but configure unlimited
  policies" because it avoids the per-request limiter check overhead
  in dev.
- **`.DisableRateLimiting()` on probe + scrape endpoints** is metadata
  that's safe whether or not the middleware is registered. Apply it
  even when the gate is off, so the intent stays in the source.
- **`AllowCredentials()` + `AllowAnyOrigin()` is a compile-time policy
  error**, but CORS config bugs typically slip past code review.
  Documented explicitly in `docs/secrets.md` so future changes don't
  reintroduce it.
- **Sample configs ship in-repo under `infra/`** — operators can
  `install -m 0644 infra/.../*.example /etc/.../`. Don't hide
  deployment know-how in PR comments / GH wiki / Notion; the repo is
  the source of truth.
- **systemd unit wraps `docker run`, not `docker compose up`**.
  Compose is great for dev parity; systemd is what production hosts
  actually run. The two coexist — the same image works under both
  drivers.
- **`--log-opt max-size + max-file` should be on every Docker
  deployment.** Default `json-file` driver with no cap is a known
  outage vector; the systemd sample carries the rotation opts so
  operators inherit safety by following the docs.

---

## Phase K candidates (Wave 6 → backlog handoff)

1. `Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders` middleware so
   `RemoteIpAddress` reflects the real client across the entire
   request pipeline (logs, SignalR connection state) — not just the
   rate-limiter partition key (Apone, future wave).
2. `[HubFilter]` for per-method rate-limiting on SignalR (Bishop call;
   the WS middleware path doesn't see hub invocations).
3. 429-counter metric in `/metrics` so operators can alert on
   sustained throttling (Apone, after Bishop ships the identity endpoint).
4. `infra/k8s/` Helm chart or kustomize overlays — currently Docker /
   compose / systemd only (Apone, later wave).
5. Replace the inline 50-MiB Docker log cap with a structured log
   pipeline (Vector / Fluent Bit → Loki) for the multi-host case.

(Existing carryovers from Wave 5 — Postgres / SqlServer migration
pipeline, multi-arch Docker, PR-time docker dry run, ghcr.io retention,
DST-aware cron, actionlint PR gate, image scanning — all still open and
will be picked up on Wave 7+.)
