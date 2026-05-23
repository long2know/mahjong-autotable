# Apone — Phase J Wave 7 memo

**Branch:** `stlong/phase-j-wave-7-polish`
**Date:** 2026-05-24
**Author:** Apone (DevOps / Platform Engineer)

---

## What shipped

### Task 1 — Multi-provider EF Core (Sqlite / Postgres / SqlServer)

`AppDbContext` is now an abstract-style base class wrapped by three
concrete subclasses — `SqliteAppDbContext`, `PostgresAppDbContext`,
`SqlServerAppDbContext` — under
`src/backend/src/Mahjong.Autotable.Api/Persistence/`. The runtime
registers exactly one of those subclasses based on
`Persistence:Provider` (env: `Persistence__Provider`) and aliases the
legacy `AppDbContext` to it via `AddScoped`, so every existing
`GetRequiredService<AppDbContext>()` call site keeps working without
a code edit.

| Provider     | Selector             | NuGet driver                                  |
| ------------ | -------------------- | --------------------------------------------- |
| Sqlite       | `Sqlite` (default)   | `Microsoft.EntityFrameworkCore.Sqlite`        |
| Postgres     | `Postgres`           | `Npgsql.EntityFrameworkCore.PostgreSQL`       |
| SqlServer    | `SqlServer`          | `Microsoft.EntityFrameworkCore.SqlServer`     |

**Migration sets** live per-provider under
`Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`. Each is tagged
for its subclass so `dotnet ef migrations add --context
PostgresAppDbContext --output-dir Persistence/Migrations/Postgres`
produces an isolated set. The legacy `AppDbContext`-tagged migrations
remain in the directory root for back-compat (Sqlite uses
`EnsureCreatedAsync` + a defensive `CREATE TABLE IF NOT EXISTS` sweep,
identical to Wave 6 behaviour). Postgres and SqlServer use
`MigrateAsync` at startup.

**`StateJson` and `EventsJson` are now provider-typed.** Removed
`HasColumnType("TEXT")` from `Data/AppDbContext.cs` — EF Core now
picks `TEXT` on SQLite, `text` on Postgres, `nvarchar(max)` on SQL
Server. The Wave 6 monolith literally hardcoded `"TEXT"`, which would
have collapsed to a 4000-char column on SQL Server.

**Connection-string contract.** Missing strings throw
`InvalidOperationException` at DI-resolve time (lazy throw inside
the `AddDbContext` option lambda) — Vasquez's
`DbProviderSwitchingTests.AddPersistence_PostgreSqlWithoutConnectionString_ThrowsOnResolve`
pins this. A typo in a k8s ConfigMap fails fast on startup with a
clear stack trace instead of silently falling through to SQLite.

**Design-time factories.** Each subclass has an
`IDesignTimeDbContextFactory<T>` in the same directory so
`dotnet ef migrations add` works without spinning up the host. They
read `ConnectionStrings:<Provider>` from the standard config ladder
(env > user secrets > appsettings.{Env}.json > appsettings.json) with
sensible local-dev fallbacks.

### Task 2 — Postgres compose overlay

`docker-compose.postgres.yml` at repo root spins up a Postgres-16-alpine
sidecar gated on `pg_isready` healthcheck, and flips the API container
to `Persistence__Provider=Postgres`. Usage:

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d --build
```

Override `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` env
vars for non-dev runs. Postgres data volume is a separate named volume
so `docker compose down` keeps the rows; `down -v` wipes.

### Task 3 — Kubernetes manifests

Full Kustomize tree under `infra/k8s/`:

```
infra/k8s/
  base/
    configmap.yaml          — non-secret env (provider, CORS, rate limit)
    secret-template.yaml    — Postgres / SqlServer connection strings (placeholders)
    pvc.yaml                — 2Gi RWO PVC for SQLite path
    deployment.yaml         — 2-replica RollingUpdate, runAsNonRoot UID 1000
    service.yaml            — ClusterIP, named `http` port
    ingress.yaml            — TLS + sticky-cookie session affinity
    hpa.yaml                — CPU 70% + memory 80%, min 2 / max 8
    kustomization.yaml
  overlays/
    staging/                — 1 replica, staging-mahjong-autotable.*, SQLite
    prod/                   — 3 replicas, prod-mahjong-autotable.*, Postgres
```

**Sticky sessions are mandatory** for `/hubs/changsha` (SignalR) and
`/autotable/ws` (raw WS). Wired via nginx-ingress cookie-affinity
annotations (`affinity: cookie`, cookie name `mahjong_aff`, 24h max-age).
Without this, a WS upgrade can land on pod A and subsequent frames hit
pod B → reset storm. Documented + alternatives for Traefik / AWS ALB
in `docs/kubernetes.md`.

**Probes both hit `/health`.** Liveness + readiness both target
Bishop's Wave-3 canonical endpoint. Vasquez's
`K8sManifestSanityTests` pins this against regression.

**Security context.** `runAsNonRoot: true`, `runAsUser: 1000`,
`readOnlyRootFilesystem: true`, `allowPrivilegeEscalation: false`,
`capabilities.drop: [ALL]`, `seccompProfile: RuntimeDefault`. Writes
go to the `data` PVC + an `emptyDir` `/tmp` mount.

**Image-pull secret** (`ghcr-pull`, dockerconfigjson) is referenced
by name from `deployment.yaml`; create per-namespace once:

```bash
kubectl create secret docker-registry ghcr-pull \
  --docker-server=ghcr.io \
  --docker-username=<github-user> \
  --docker-password=<pat-with-read:packages> \
  -n mahjong-staging
```

### Task 4 — Backup & restore scripts

Four scripts under `scripts/` (all chmod +x):

| Script                  | Notes                                                 |
| ----------------------- | ----------------------------------------------------- |
| `backup-sqlite.sh`      | `sqlite3 .backup` (safe vs active writer), `PRAGMA integrity_check`, retention via `RETAIN_COUNT` (default 14). |
| `restore-sqlite.sh`     | Snapshots existing DB to `.pre-restore-<TS>` for instant rollback; atomic move. |
| `backup-postgres.sh`    | `pg_dump -Fc -Z 6 --no-owner --no-privileges`, PG* env, retention. |
| `restore-postgres.sh`   | `pg_restore`, optional `RESTORE_CLEAN=1`, post-restore sanity-check on `ChangshaGames` + `PlayerProfiles`. |

Cron-friendly: timestamped output, `logger -t` ready. Quarterly
restore-drill procedure documented in `docs/backup-restore.md`.

### Task 5 — Container hardening (non-root)

`Dockerfile` now creates GID/UID 1000 (`mahjong` user) and switches
to `USER 1000:1000` after copying the build artefacts. `/data` and
`/app` are `chown`'d so SQLite can write its DB file without root.
The `groupadd/useradd` commands are guarded with `getent` so the
build is idempotent against base images that already ship UID 1000
(e.g. the post-2026 `aspnet:10.0` image now does).

Verified end-to-end via `tests/smoke/docker-build-smoke.sh`:
✅ build succeeded, ✅ /health responding, all four contract fields
present.

### Task 6 — Multi-provider CI

`.github/workflows/db-providers.yml` runs the full xUnit suite under
a matrix of `[Sqlite, Postgres]` with a `postgres:16-alpine` service
container. SqlServer is intentionally omitted (heavy image, slow on
hosted runners); rely on the `SqlServerAppDbContextModelSnapshot`
diff + Postgres CI as proxy.

### Task 7 — Documentation

| Doc                          | Covers                                                            |
| ---------------------------- | ----------------------------------------------------------------- |
| `docs/database-providers.md` | Provider selector, env contract, migration layout, `dotnet ef` recipes, EnsureCreated vs Migrate behaviour. |
| `docs/kubernetes.md`         | Cluster assumptions, ghcr secret, cert-manager, sticky-session rationale, kustomize commands, observability. |
| `docs/backup-restore.md`     | Script env, cron examples, off-site sync, quarterly restore-drill procedure. |

---

## Test gate

- **Baseline (Wave 6 head):** 456 passing.
- **After Wave 7:** 526 passing / 1 pre-existing failure
  (`GameReplayEndpointTests.GameReplay_Events_AreOrderedByTurnAscending`
  — Bishop's replay endpoint returns events in seed order, not turn
  order; this is **not my code** and was failing before my changes
  too).

The new test surfaces all came from Vasquez's pre-existing untracked
Wave 7 contract tests:
- `Deploy/{ContainerHardeningTests, K8sManifestSanityTests}.cs`
- `Persistence/DbProviderSwitchingTests.cs`
- `Players/AvatarColorPaletteTests.cs`
- `Replay/GameReplayEndpointTests.cs`

I fixed one xunit-API drift in `ContainerHardeningTests.cs:90`
(`Assert.NotEqual(..., ignoreCase: true)` overload no longer exists
in xunit 2.9.3) — switched to
`Assert.False(string.Equals(..., StringComparison.OrdinalIgnoreCase))`.

---

## New env vars (k8s ConfigMap / Secret / systemd `Environment=`)

| Var                                | Notes                                                       |
| ---------------------------------- | ----------------------------------------------------------- |
| `Persistence__Provider`            | Required. `Sqlite` / `Postgres` / `SqlServer`. Default `Sqlite`. |
| `ConnectionStrings__Sqlite`        | Required when provider=Sqlite. Default `Data Source=data/mahjong-autotable.db`. |
| `ConnectionStrings__PostgreSql`    | Required when provider=Postgres.                            |
| `ConnectionStrings__SqlServer`     | Required when provider=SqlServer.                           |

---

## Breaking changes

1. **`USER 1000:1000` in Dockerfile** — anyone running a custom
   `docker run … -v /host/data:/data` mount must `chown -R 1000:1000
   /host/data` first or SQLite will fail to open the DB. Compose
   users with the default named volume are unaffected.
2. **`HasColumnType("TEXT")` removed** from `StateJson` /
   `EventsJson`. On existing SQLite DBs this is a no-op (SQLite is
   dynamically typed). On Postgres / SqlServer this is the *correct*
   provider-native type; there's no existing prod DB to migrate.
3. **`AppDbContext` constructor signature** — changed from
   `DbContextOptions<AppDbContext>` to non-generic `DbContextOptions`
   so the subclasses can forward their typed options. Binary-compatible
   for callers (`DbContextOptions<T> : DbContextOptions`).

---

## Pointers for the next wave

- **One pre-existing failure to chase** —
  `GameReplay_Events_AreOrderedByTurnAscending`. Likely a missing
  `OrderBy(e => e.Turn)` in Bishop's replay controller. Out of my
  scope.
- **The legacy `AppDbContext`-tagged migrations** under
  `Persistence/Migrations/` (root, no provider subfolder) are now
  effectively orphaned — SQLite uses EnsureCreated, and the
  provider-specific subclasses point at their own subfolder. They're
  harmless but can be cleaned up in a follow-up.
- **SQL Server in CI** — when GitHub-hosted runners get faster (or
  we move to self-hosted), drop `SqlServer` into the matrix in
  `db-providers.yml`.

---

— Apone
