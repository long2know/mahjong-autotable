# Database providers

Phase J Wave 7 — Apone (DevOps).

The autotable API ships with three EF Core providers wired up and ready to
swap at deploy time:

| Provider     | Selector                     | Notes                                                                                |
| ------------ | ---------------------------- | ------------------------------------------------------------------------------------ |
| **SQLite**   | `Persistence:Provider=Sqlite` (default) | Single-file, single-replica. The dev / Docker-quickstart path.                       |
| **Postgres** | `Persistence:Provider=Postgres`         | Production recommended. Multi-replica safe. `Npgsql.EntityFrameworkCore.PostgreSQL`. |
| **SQL Server** | `Persistence:Provider=SqlServer`      | Production-capable. `Microsoft.EntityFrameworkCore.SqlServer`.                       |

The selector is read by
[`Persistence/ServiceCollectionExtensions.AddPersistence`](../src/backend/src/Mahjong.Autotable.Api/Persistence/ServiceCollectionExtensions.cs).
One concrete subclass of `AppDbContext` is registered per provider —
`SqliteAppDbContext`, `PostgresAppDbContext`, or `SqlServerAppDbContext` —
and `AppDbContext` itself is aliased to whichever subclass is active. Every
existing `GetRequiredService<AppDbContext>()` call site continues to work
unchanged.

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

```bash
docker compose -f docker-compose.yml -f docker-compose.postgres.yml up -d --build
```

The `docker-compose.postgres.yml` overlay spins up a `postgres:16-alpine`
sidecar, gates the API on `pg_isready`, and rewrites
`ConnectionStrings__PostgreSql` to point at it. Override credentials via
`POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` env vars before
`up`.

### Local Docker Compose — SQL Server

There's no shipped overlay yet (SQL Server's official image is hefty), but
the manual incantation is:

```bash
docker network create mahjong-net
docker run -d --name sqlserver --network mahjong-net \
  -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='YourStrong!Passw0rd' \
  -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest

docker run -d --name mahjong --network mahjong-net -p 8080:8080 \
  -e Persistence__Provider=SqlServer \
  -e 'ConnectionStrings__SqlServer=Server=sqlserver,1433;Database=mahjong_autotable;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true' \
  mahjong-autotable:local
```

### Kubernetes

Set `Persistence__Provider` in the ConfigMap (`infra/k8s/base/configmap.yaml`)
and the matching connection string in the Secret (`secret-template.yaml`).
The `overlays/prod/kustomization.yaml` example flips to Postgres by default.

## Migrations

EF Core migration sets live per-provider under
`src/backend/src/Mahjong.Autotable.Api/Persistence/Migrations/`:

```
Migrations/
  (legacy AppDbContext-tagged migrations — Sqlite-only, kept for back-compat)
  Sqlite/         InitialSqlite             ← tagged for SqliteAppDbContext
  Postgres/       InitialPostgres           ← tagged for PostgresAppDbContext
  SqlServer/      InitialSqlServer          ← tagged for SqlServerAppDbContext
```

The runtime picks the right set via the subclass identity. EF Core's
`__EFMigrationsHistory` table records which migrations have been applied
per provider (Postgres uses the `public` schema explicitly via
`MigrationsHistoryTable`).

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

## CI

The `.github/workflows/db-providers.yml` workflow spins up a Postgres
service container on every PR and re-runs the test suite with
`Persistence:Provider=Postgres`, so a schema regression that breaks on
the non-SQLite path is caught before merge. SQL Server's official image
is too heavy for the GitHub-hosted runners; rely on
`SqlServerAppDbContextModelSnapshot` review + the Postgres CI as proxy.

## Backups

See [`backup-restore.md`](backup-restore.md) for `pg_dump`/`pg_restore`
scripts (`scripts/backup-postgres.sh`, `scripts/restore-postgres.sh`)
and a cron-friendly retention policy.
