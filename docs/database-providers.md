# Database providers

Phase J Wave 7 — Apone (DevOps).

The autotable API ships with three EF Core providers wired up and ready to
swap at deploy time:

| Provider     | Selector                     | Notes                                                                                |
| ------------ | ---------------------------- | ------------------------------------------------------------------------------------ |
| **SQLite**   | `Persistence:Provider=Sqlite` (default) | Single-file, single-replica. The dev / Docker-quickstart path.                       |
| **Postgres** | `Persistence:Provider=Postgres`         | Production recommended. Durable, backup-friendly. `Npgsql.EntityFrameworkCore.PostgreSQL`. |
| **SQL Server** | `Persistence:Provider=SqlServer`      | Production-capable. `Microsoft.EntityFrameworkCore.SqlServer`.                       |

> **Single-writer only — no multi-replica safety.** Choosing Postgres or
> SQL Server makes storage *durable*, but it does **not** make the app
> horizontally scalable. See
> [Concurrency & the single-writer constraint](#concurrency--the-single-writer-constraint)
> before running more than one API replica.

The selector is read by
[`Persistence/ServiceCollectionExtensions.AddPersistence`](../src/backend/src/Mahjong.Autotable.Api/Persistence/ServiceCollectionExtensions.cs).
One concrete subclass of `AppDbContext` is registered per provider —
`SqliteAppDbContext`, `PostgresAppDbContext`, or `SqlServerAppDbContext` —
and `AppDbContext` itself is aliased to whichever subclass is active. Every
existing `GetRequiredService<AppDbContext>()` call site continues to work
unchanged.

## Concurrency & the single-writer constraint

**The API is a single-writer, single-container application. Do not run more
than one replica against a shared database.**

`ChangshaGame.StateVersion` is a plain `int` column — **not** an EF Core
concurrency token (no `IsConcurrencyToken()`, no SQL Server `rowversion`, no
Postgres `xmin` mapping). All write serialization for a game comes from the
per-game in-process `SemaphoreSlim` held by `ChangshaGameRuntime`
(`instance.Lock`): the optimistic `StateVersion` check runs *inside* that lock,
before the mutation, and `PersistSnapshotAsync` writes the snapshot *after* the
`StateChanged` broadcast (and swallows DB errors so persistence never breaks
gameplay).

Because that lock lives in process memory, correctness holds **only** while a
game is owned by exactly one process:

- ✅ **Single container / single replica** (the shipped target). One writer,
  one lock, no split-brain. SQLite, Postgres, and SQL Server are all safe here.
- ❌ **Multiple API replicas behind a load balancer.** Two processes could each
  hold their *own* `SemaphoreSlim` for the same game and interleave writes; the
  plain `StateVersion` column would not reject the stale write, so the last
  writer silently wins (split-brain). This is unsupported on **every** provider,
  Postgres and SQL Server included — moving off SQLite buys durability, not
  horizontal scale.

Multi-replica support would require promoting `StateVersion` to a real DB
concurrency token (or a `rowversion`/`xmin` mapping) and a distributed lock or
sticky game-affinity router. That work is intentionally out of scope; until it
lands, keep the deployment single-writer.

## Environment contract

| Env var (k8s `envFrom`) | Required when               | Example                                                                                          |
| ----------------------- | --------------------------- | ------------------------------------------------------------------------------------------------ |
| `Persistence__Provider` | always (defaults to Sqlite) | `Postgres`                                                                                       |
| `ConnectionStrings__Sqlite`     | provider=Sqlite     | `Data Source=/data/mahjong-autotable.db`                                                         |
| `ConnectionStrings__PostgreSql` | provider=Postgres   | `Host=db;Port=5432;Database=mahjong_autotable;Username=mahjong;Password=…;SslMode=Require`       |
| `ConnectionStrings__SqlServer`  | provider=SqlServer  | `Server=db,1433;Database=mahjong_autotable;User Id=mahjong;Password=…;TrustServerCertificate=true;Encrypt=true` |

Missing connection strings surface as `InvalidOperationException` at
DI-resolution time (Vasquez's `DbProviderSwitchingTests` pins this), so a
typo in a k8s ConfigMap or systemd `Environment=…` line fails fast on
startup with a clear stack trace.

## Switching the provider

### Local Docker Compose — Postgres

The `mahjong` service runs in Production posture, so provision the JWT key
first (idempotent), then bring up the overlay:

```bash
./scripts/compose-bootstrap.sh
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d --build
```

The `docker-compose.postgres.yml` overlay spins up a `postgres:16-alpine`
sidecar, gates the API on `pg_isready`, and rewrites
`ConnectionStrings__PostgreSql` to point at it. Override credentials via
`POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` env vars before
`up`.

### Local Docker Compose — SQL Server

WP-C / #118 ships a `docker-compose.sqlserver.yml` overlay (twin of the
Postgres one) that runs a `mcr.microsoft.com/mssql/server:2022-latest` sidecar,
gates the API on a `sqlcmd "SELECT 1"` healthcheck, and points
`ConnectionStrings__SqlServer` at it:

```bash
./scripts/compose-bootstrap.sh
docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d --build
```

SQL Server enforces a password-complexity policy; override the dev default via
`MSSQL_SA_PASSWORD` (and, because the base compose runs
`ASPNETCORE_ENVIRONMENT=Production`, supply `Authentication__JwtSigningKeys__0`)
before `up`. The API's startup `MigrateAsync()` creates the `mahjong_autotable`
database and applies the SqlServer migration set on first boot.

### Kubernetes

Set `Persistence__Provider` in the ConfigMap (`infra/k8s/base/configmap.yaml`)
and the matching connection string in the Secret (`secret-template.yaml`).
The `overlays/prod/kustomization.yaml` example flips to Postgres by default.

## Migrations

EF Core migration sets live per-provider under
`src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/`:

```
Migrations/
  README.md                                 ← read before running `dotnet ef migrations`
  (DORMANT root AppDbContext migrations + snapshot — a regeneration trap; see below)
  Sqlite/         InitialSqlite             ← tagged for SqliteAppDbContext
  Postgres/       InitialPostgres           ← tagged for PostgresAppDbContext
  SqlServer/      InitialSqlServer          ← tagged for SqlServerAppDbContext
```

The runtime picks the right set via the subclass identity. EF Core's
`__EFMigrationsHistory` table records which migrations have been applied
per provider (Postgres uses the `public` schema explicitly via
`MigrationsHistoryTable`).

> **Dormant root migrations (regeneration trap).** The files directly in
> `Migrations/` (not in a provider sub-folder) target the *base* `AppDbContext`
> and predate the Phase J Wave 7 provider split. They are inert at runtime and
> retained only for history. Never run `dotnet ef migrations add …
> --context AppDbContext`: it would diff against the stale
> `AppDbContextModelSnapshot.cs` and emit a corrupt catch-up migration. Always
> pass an explicit provider `--context`. See
> [`Migrations/README.md`](../src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/README.md).

### Adding a new migration

```bash
cd src/backend/src/Mahjong.Autotable.Api

# Pick the context to match the provider you're targeting.
dotnet ef migrations add MyChange \
    --context PostgresAppDbContext \
    --output-dir Persistence/Migrations/Postgres

dotnet ef migrations add MyChange \
    --context SqlServerAppDbContext \
    --output-dir Persistence/Migrations/SqlServer

dotnet ef migrations add MyChange \
    --context SqliteAppDbContext \
    --output-dir Persistence/Migrations/Sqlite
```

Each design-time `IDesignTimeDbContextFactory<…>` resolves a connection
string from `appsettings.json`. You can override via env, e.g.
`ConnectionStrings__PostgreSql="…" dotnet ef migrations add …`.

### Applying migrations

For Postgres and SQL Server, the API container runs `MigrateAsync()` at
startup automatically (`Data/DatabaseBootstrapper.InitializeAsync`).

For Sqlite the bootstrap continues to use `EnsureCreatedAsync` plus a
defensive `CREATE TABLE IF NOT EXISTS` sweep so existing dev DBs (which
pre-date the migration set) keep booting without a manual `dotnet ef
database update`. The Sqlite-tagged migration set is generated for
parity but is **not** applied automatically — operators who want
migrations-managed SQLite can wire it up by changing the conditional in
`DatabaseBootstrapper`.

Because the SQLite runtime bootstraps via `EnsureCreated` rather than the
migration runner, model/migration drift on the SQLite context used to be
invisible. The `drift-gate` CI job (below) now runs
`dotnet ef migrations has-pending-model-changes` for **all three** provider
contexts on every PR, so any model edit that isn't captured in a paired
migration fails CI regardless of provider. Run it locally before pushing:

```bash
cd src/backend/src/Mahjong.Autotable.Api
for ctx in SqliteAppDbContext PostgresAppDbContext SqlServerAppDbContext; do
  dotnet ef migrations has-pending-model-changes --context "$ctx"
done
```

## CI

The `.github/workflows/db-providers.yml` workflow runs the full test suite on
every PR against **all three providers**:

- **Sqlite** — the default, no service container.
- **Postgres** — a `postgres:16-alpine` service container.
- **SQL Server** — a real `mcr.microsoft.com/mssql/server:2022-latest`
  container, started via a `docker run` step scoped to the SqlServer matrix
  cell so the heavy image never slows the other cells (WP-C / #118). The
  `SqlServerTestDatabaseLifetime` harness provisions a throwaway per-process
  database and drops it on exit, mirroring the Postgres isolation harness.

A separate **`drift-gate`** job runs `has-pending-model-changes` per provider
context (no database required) so model/migration drift fails the build.

## Backups

See [`backup-restore.md`](backup-restore.md) for `pg_dump`/`pg_restore`
scripts (`scripts/backup-postgres.sh`, `scripts/restore-postgres.sh`)
and a cron-friendly retention policy.
