# Deployment — Mahjong Autotable (Docker)

Single-image, single-port deployment of the Mahjong Autotable. The frontend
bundle (Parcel) and the .NET 10 ASP.NET backend are baked into one image and
served from the same Kestrel host on port `8080`.

Stephen's original ask:

> The frontend and backend should be packageable as a single docker image so
> that I can run in a container on my Linux server that I already have.

This runbook is the canonical answer.

---

## 1. Prerequisites

- A Linux host with Docker Engine **20.10+** (or any version that supports
  multi-stage builds and the `HEALTHCHECK` directive).
- ~1 GB free disk for the build cache; ~300 MB for the runtime image.
- Outbound network access at build time so Docker can pull
  `node:20-alpine`, `mcr.microsoft.com/dotnet/sdk:10.0`, and
  `mcr.microsoft.com/dotnet/aspnet:10.0`.

> **Note.** No host-side .NET SDK or Node toolchain is required at deploy
> time. Everything is built inside the image.

---

## 2. Image layout

The image is produced by [`/Dockerfile`](../Dockerfile) using three stages:

| Stage           | Base                                       | Produces                  |
| --------------- | ------------------------------------------ | ------------------------- |
| `frontend-build` | `node:20-alpine`                          | Parcel bundle at `/out/autotable/` |
| `backend-build`  | `mcr.microsoft.com/dotnet/sdk:10.0`       | Published API at `/out/api/`       |
| `runtime`        | `mcr.microsoft.com/dotnet/aspnet:10.0`    | Final image (`~300 MB`)            |

At runtime the layout inside the container is:

```
/app/                       # published .NET API (Mahjong.Autotable.Api.dll)
/frontend/autotable/        # Parcel-built bundle (served at /autotable/)
/data/                      # SQLite database — mount a volume here
```

The frontend lives at `/frontend/autotable/` because `Program.cs` computes
the static-files path as `Path.GetFullPath(Path.Combine(ContentRootPath,
"../../../frontend/autotable"))`. With `WORKDIR=/app` that collapses to
`/frontend/autotable`, so the in-container layout intentionally mirrors the
source tree. This keeps the custom `.glb` / `.gltf` MIME-type registrations
exercised on every request — no backend code change required for Docker.

---

## 3. Build

```bash
# From the repo root:
docker build -t mahjong-autotable:vX.Y.Z .
```

Pass the current git SHA so `/health` can report it back:

```bash
docker build \
    --build-arg BUILDKIT_INLINE_CACHE=1 \
    -t mahjong-autotable:$(git rev-parse --short HEAD) \
    .
```

> Tip — re-tag a `:latest` alias for the most recent build:
>
> ```bash
> docker tag mahjong-autotable:$(git rev-parse --short HEAD) mahjong-autotable:latest
> ```

### Build context

The `.dockerignore` aggressively trims the context: `node_modules/`, the
checked-in `src/frontend/autotable/` pre-build, `**/bin`, `**/obj`,
`src/backend/tests/`, `.git/`, `.squad/`, and assorted IDE / OS / log
noise are excluded. The build context is small (a few MB) so iteration is
fast.

---

## 4. Run

> **Required:** the image runs `ASPNETCORE_ENVIRONMENT=Production`, which
> refuses to boot without a **stable** JWT signing key
> (`Authentication__JwtSigningKeys__0`, base64 ~48 bytes). Generate it once
> and keep it stable across restarts/hosts — rotating it invalidates every
> previously issued JWT. Source it from your secrets manager; never commit or
> bake it into the image. See [`jwt-rotation.md`](jwt-rotation.md) §7.

### One-liner (`docker run`)

`docker run` does not read `.env`, so pass the key explicitly:

```bash
docker run -d \
    --name mahjong \
    --restart unless-stopped \
    -p 8080:8080 \
    -v mahjong-data:/data \
    -e BUILD_SHA="$(git rev-parse --short HEAD)" \
    -e Authentication__JwtSigningKeys__0="$JWT_SIGNING_KEY" \
    mahjong-autotable:vX.Y.Z
```

Then visit:

- Lobby: <http://localhost:8080/autotable/>
- Health: <http://localhost:8080/health>

### docker compose (preferred for local dev parity)

The one-time bootstrap writes a stable `JWT_SIGNING_KEY` into a gitignored
`.env` (idempotent — safe to re-run; compose injects it as
`Authentication__JwtSigningKeys__0`):

```bash
./scripts/compose-bootstrap.sh
docker compose up -d --build
```

`docker-compose.yml` builds the same image as `mahjong-autotable:local` and
mounts the `mahjong-data` named volume on `/data`. Without a key, compose
fails fast with a one-line fix instruction instead of crash-looping. Override
the host port or environment by adding a `docker-compose.override.yml` — it's
gitignored.

### Build SHA stamping

The image reads `BUILD_SHA` from the environment at request time, so a
single image can be deployed with different SHAs without rebuilding:

```bash
docker run ... -e BUILD_SHA=2026-05-22-rc1 mahjong-autotable:vX.Y.Z
curl http://localhost:8080/health
# {"status":"healthy","buildSha":"2026-05-22-rc1", ...}
```

When unset, `/health` reports `"buildSha":"dev"`.

---

## 5. Environment variables

| Variable                            | Default                                       | Notes |
| ----------------------------------- | --------------------------------------------- | ----- |
| `ASPNETCORE_URLS`                   | `http://+:8080`                               | Listening URL inside the container. Change in tandem with `-p` if you remap the port. |
| `ASPNETCORE_ENVIRONMENT`            | `Production`                                  | Set to `Development` to surface dev-only diagnostics. |
| `ConnectionStrings__Sqlite`         | `Data Source=/data/mahjong-autotable.db`      | EF Core / SQLite connection string. Must point at a path inside a writable volume. |
| `Persistence__Provider`             | `Sqlite`                                      | Switch to `PostgreSql` or `SqlServer` if you wire up an external DB (also set the matching connection string). |
| `BUILD_SHA`                         | `""` (empty → `/health` reports `"dev"`)      | Stamped into `GET /health` so deploys are identifiable. |
| `DOTNET_RUNNING_IN_CONTAINER`       | `true`                                        | Standard .NET container hint. Don't override. |
| `DOTNET_EnableDiagnostics`          | `0`                                           | Disables the diagnostics IPC server (saves ~30 MB RSS). |

PostgreSQL / SQL Server example:

```bash
docker run -d \
    -p 8080:8080 \
    -e Persistence__Provider=PostgreSql \
    -e Authentication__JwtSigningKeys__0="$JWT_SIGNING_KEY" \
    -e ConnectionStrings__PostgreSql="Host=db;Port=5432;Database=mahjong;Username=mahjong;Password=secret" \
    mahjong-autotable:vX.Y.Z
```

---

## 6. Persistence

A single named volume holds the SQLite database:

```bash
docker volume create mahjong-data    # optional; `-v mahjong-data:/data` auto-creates
docker volume inspect mahjong-data
```

The database file lives at `/data/mahjong-autotable.db`. EF Core auto-creates
the schema on first launch via `DatabaseBootstrapper.InitializeAsync`.

### Backup

```bash
docker run --rm \
    -v mahjong-data:/data \
    -v "$(pwd)":/backup \
    alpine \
    sh -c 'cp /data/mahjong-autotable.db /backup/mahjong-$(date +%Y%m%d-%H%M%S).db'
```

SQLite snapshots are safe to copy while the app runs because WAL mode
ensures readers don't block writers; for a strictly-consistent dump, stop
the container first.

### Restore

```bash
docker stop mahjong
docker run --rm \
    -v mahjong-data:/data \
    -v "$(pwd)":/backup \
    alpine \
    sh -c 'cp /backup/mahjong-20260522-180000.db /data/mahjong-autotable.db'
docker start mahjong
```

### Wipe

```bash
docker compose down -v        # removes the named volume
# or
docker volume rm mahjong-data
```

---

## 7. Healthcheck

The Dockerfile defines:

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health \
      || curl -fsS http://127.0.0.1:8080/api/health \
      || exit 1
```

Two probes for defence-in-depth:

1. **`/health`** (Bishop, Phase J Wave 3 Task 3) — canonical JSON probe with
   `status`, `buildSha`, `uptime`, `version`. This is the one Docker /
   Kubernetes / load balancers should hit.
2. **`/api/health`** — legacy short-form probe, kept for backwards
   compatibility (frontend, older deploys).

Inspect runtime health status:

```bash
docker inspect --format='{{.State.Health.Status}}' mahjong
# starting | healthy | unhealthy
```

The first health probe runs after the `start-period` of 20 s. EF Core
hydration on a populated DB plus Kestrel startup typically fits in ~3-5 s,
so 20 s leaves comfortable slack.

---

## 8. Day-2 operations

```bash
# Live logs
docker logs -f mahjong

# Last 200 lines
docker logs --tail 200 mahjong

# Open a shell
docker exec -it mahjong /bin/bash

# Inspect the SQLite DB inline (sqlite3 not installed in the image — use a sidecar)
docker run --rm -it -v mahjong-data:/data nouchka/sqlite3 /data/mahjong-autotable.db

# Restart
docker restart mahjong
```

---

## 9. Updating

```bash
# Pull / build the new image
git pull
docker build -t mahjong-autotable:vX.Y.Z+1 .

# Recreate with the new tag (volume preserved)
docker stop mahjong && docker rm mahjong
docker run -d \
    --name mahjong \
    --restart unless-stopped \
    -p 8080:8080 \
    -v mahjong-data:/data \
    -e Authentication__JwtSigningKeys__0="$JWT_SIGNING_KEY" \
    mahjong-autotable:vX.Y.Z+1

# Or with compose (bootstrap is idempotent — reuses your existing .env key):
./scripts/compose-bootstrap.sh && docker compose pull && docker compose up -d --build
```

EF Core's `DatabaseBootstrapper.InitializeAsync` upgrades the schema in
place on first boot of the new image. Older non-terminal games are
re-hydrated via `IChangshaGameRuntime.HydrateAsync` (Phase I Wave 2), so an
update mid-hand is non-destructive.

---

## 10. Troubleshooting

| Symptom | Diagnosis | Fix |
| ------- | --------- | --- |
| `Error: bind: address already in use` | Host port 8080 already taken. | Remap: `-p 18080:8080` (then browse `http://host:18080/autotable/`). |
| Container exits immediately, logs show `Permission denied` writing to `/data` | Bind-mounted host directory owned by a uid the container can't write to. | Prefer the named volume (`-v mahjong-data:/data`); or `chown` the host dir to uid `1654` (the .NET runtime user) before mounting. |
| `/autotable/` returns 404 | Static-files binding skipped because `/frontend/autotable/` didn't get copied. Usually a `.dockerignore` over-match. | `docker run --rm --entrypoint sh mahjong-autotable:vX.Y.Z -c 'ls /frontend/autotable/'` should list `index.html`. If empty, check `.dockerignore`. |
| `models.auto.*.glb` returns 404 or wrong content-type | The custom `.glb` / `.gltf` MIME registration in `Program.cs` only runs when the bundle is at `/frontend/autotable/`. | Confirm `WORKDIR /app` and the COPY destination match the Dockerfile shipped here. |
| `HEALTHCHECK` reports `unhealthy` | App not yet listening, or `/health` not yet wired. | `docker logs mahjong` for boot errors; the start-period is 20 s so brief startup unhealthiness is normal. |
| `Failed to bind to address http://[::]:8080: address already in use` *inside* the container | Two services contending for `+:8080`. | Should not happen with single-process container — check that nothing else is listening on `ASPNETCORE_URLS`. |
| Image build pulls fail with `manifest unknown` for `dotnet/sdk:10.0` | .NET 10 image tag rotated or daily-build only. | Pin a specific tag, e.g. `mcr.microsoft.com/dotnet/sdk:10.0.100-preview` once GA tags are published. |

---

## 11. Reference — quick commands

```bash
# Build
docker build -t mahjong-autotable:latest .

# Run
docker run -d --name mahjong --restart unless-stopped \
    -p 8080:8080 -v mahjong-data:/data \
    -e Authentication__JwtSigningKeys__0="$JWT_SIGNING_KEY" \
    mahjong-autotable:latest

# Compose (bootstrap writes/keeps a stable JWT key in .env)
./scripts/compose-bootstrap.sh && docker compose up -d --build
docker compose logs -f
docker compose down

# Health
curl http://localhost:8080/health
docker inspect --format='{{.State.Health.Status}}' mahjong
```

See also [`docs/docker.md`](docker.md) for the 5-minute quickstart.

---

## 12. Production with reverse proxy

The container ships HTTP only on port 8080; TLS + WebSocket upgrade
fidelity are deployment concerns. The repo carries ready-to-copy
samples for the two most common fronts:

- nginx — [`infra/nginx/mahjong.conf.example`](../infra/nginx/mahjong.conf.example)
- Caddy — [`infra/caddy/Caddyfile.example`](../infra/caddy/Caddyfile.example)

The full operator guide (TLS via Let's Encrypt, WebSocket upgrade
headers for SignalR + `/autotable/ws`, 24-hour `proxy_read_timeout`,
`X-Forwarded-For` propagation so the rate limiter sees real client
IPs) lives in [`docs/reverse-proxy.md`](reverse-proxy.md).

---

## 13. Production with systemd

For deployments that prefer `systemctl restart mahjong-autotable` over
`docker compose up -d`, the sample unit
[`infra/systemd/mahjong-autotable.service.example`](../infra/systemd/mahjong-autotable.service.example)
wraps the canonical `docker run` line in a hardened unit (`Type=simple`,
`Restart=on-failure`, `LimitNOFILE=65536`, `NoNewPrivileges=true`,
optional `EnvironmentFile=/etc/default/mahjong-autotable` override).
The walk-through is in [`docs/systemd.md`](systemd.md).

---

## 14. Log rotation

The default `docker run` from § 4 lets `json-file` logs grow
unboundedly. For self-hosted production, set the rotation opts on the
log driver:

```bash
docker run -d \
    --log-driver json-file \
    --log-opt max-size=10m \
    --log-opt max-file=5 \
    ...
```

Caps disk usage at `10m × 5 = 50 MiB` per container. The full guide
(daemon-wide default in `/etc/docker/daemon.json`, alternative via
`logrotate(8)` for bind-mounted log files) is in
[`docs/log-rotation.md`](log-rotation.md). The systemd unit at
[`infra/systemd/mahjong-autotable.service.example`](../infra/systemd/mahjong-autotable.service.example)
already wires the recommended rotation opts.

---

## 15. CORS

The runtime CORS policy reads `Cors:AllowedOrigins` from
configuration:

| Environment                | Default                                                                                       |
| -------------------------- | --------------------------------------------------------------------------------------------- |
| `Development` / base       | `http://localhost:5114`, `https://localhost:7135`, `http://localhost:5173`, `http://localhost:8080` |
| `Production` (override)    | `[]` — empty list, **no origins allowed by default**                                          |

Production deployments MUST set the real public origin via env var so
the browser can issue credentialled requests against the API:

```bash
docker run ... \
    -e Cors__AllowedOrigins__0=https://mahjong.example.com \
    ...
```

Setting the same value via systemd's `EnvironmentFile` or compose's
`environment:` block works identically. See
[`docs/secrets.md`](secrets.md) § "CORS origins" for the secret-store
patterns and rationale for why `AllowCredentials()` precludes
`AllowAnyOrigin()`.

---

## 16. Rate limiting

The Phase J Wave 6 rate limiter (Apone, DevOps) wires two policies on
top of `Microsoft.AspNetCore.RateLimiting`:

| Policy name                  | Backing limiter                         | Quota               | Applied to                                                                       |
| ---------------------------- | --------------------------------------- | ------------------- | -------------------------------------------------------------------------------- |
| `fixed-window-anonymous`     | `FixedWindowRateLimiter`, partitioned by IP | 10 req / minute / IP | Reserved for future unauthenticated mutating endpoints (e.g. profile create). Apply via `.RequireRateLimiting(RateLimitingExtensions.AnonymousPolicy)`. |
| `token-bucket-api`           | `TokenBucketRateLimiter`, partitioned by IP | 30-token bucket, 5 tokens/sec refill (≈300 req/min/IP with 30-burst) | Auto-applied to MapControllers + the per-endpoint minimal-API `/api/system/persistence` and `/api/changsha/pattern-ordering`. |

Rejected requests get **HTTP 429** with body `{"error":"too_many_requests"}` and a
`Retry-After` header.

**Not rate limited** (deliberately):

- `/health`, `/api/health` — probes need to hit hard during boot loops.
- `/metrics` — Prometheus scrapes every 15–60 s; the limiter would
  produce false alerts.
- `/hubs/changsha` — SignalR persistent transport; the limiter only
  sees the handshake.
- `/autotable/ws` — raw WS transport; same reasoning as SignalR.

### Toggle

The middleware is gated by `RateLimiting:Enabled`:

| File                         | Value        | Effect                                  |
| ---------------------------- | ------------ | --------------------------------------- |
| `appsettings.json`           | `false`      | Off in dev + test (xUnit boots `Development`). |
| `appsettings.Production.json`| `true`       | On in production deploys.               |

Override with `RateLimiting__Enabled=false` (e.g. for a load / stress
test):

```bash
docker run ... -e RateLimiting__Enabled=false ...
```

### Identifying the limit source

When a client gets a 429, check the JSON log line for the failing
endpoint. The structured log includes `RequestId` + `RequestPath` so
operators can correlate. A custom `429` response counter can be added
in a future wave (Phase K candidate).

### IP attribution behind a reverse proxy

The limiter partitions by the client IP, preferring `X-Forwarded-For`
when set. Both [`infra/nginx/mahjong.conf.example`](../infra/nginx/mahjong.conf.example)
and [`infra/caddy/Caddyfile.example`](../infra/caddy/Caddyfile.example)
forward `X-Forwarded-For`, so the partition key matches reality
without additional middleware. See
[`docs/reverse-proxy.md`](reverse-proxy.md) for the full discussion.
