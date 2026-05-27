# PlayerStats.LastGameAt schema mismatch — surgical hotfix

**By:** Drake (backend hotfix engineer), 2026-05-27

**Branch:** `fix/playerstats-lastgameat-nullable`

---

## Symptom

At runtime, Stephen hit:

```
Microsoft.Data.Sqlite.SqliteException : SQLite Error 19:
'NOT NULL constraint failed: PlayerStats.LastGameAt'.
```

The exception surfaces the moment a brand-new player profile is created
(`POST /api/identity` → `PlayerProfileService.GetOrCreateAsync` →
`db.PlayerStats.Add(new PlayerStats { PlayerId = playerId })`). The
`PlayerStats` model declares `public DateTime? LastGameAt { get; set; }`
(nullable until the player completes their first game) so EF sends
`NULL` for `LastGameAt`, but on the running dev SQLite the column was
declared `NOT NULL`.

## Initial triage (corrected on audit)

The initial triage memo from Stephen pointed at the EF migration
`Persistence/Migrations/20260523031206_AddPlayerProfileAndStats.cs` as
the source of the `NOT NULL` column. **That was wrong** — the migration
and all its mirrors are clean:

| Location | LastGameAt declaration |
|---|---|
| `Players/PlayerStats.cs:18` | `public DateTime? LastGameAt { get; set; }` ✅ |
| `Persistence/Migrations/20260523031206_AddPlayerProfileAndStats.cs:88` | `nullable: true` ✅ |
| `Persistence/Migrations/20260523031206_AddPlayerProfileAndStats.Designer.cs:159` | `Property<DateTime?>` ✅ |
| `Persistence/Migrations/AppDbContextModelSnapshot.cs:180` | `Property<DateTime?>` ✅ |
| `Persistence/Migrations/Sqlite/20260523051740_InitialSqlite.cs:102` | `nullable: true` ✅ |
| `Persistence/Migrations/Sqlite/SqliteAppDbContextModelSnapshot.cs:1346` | `Property<DateTime?>` ✅ |
| `Persistence/Migrations/Postgres/20260523051747_InitialPostgres.cs:103` | `nullable: true` ✅ |
| `Persistence/Migrations/Postgres/PostgresAppDbContextModelSnapshot.cs:1355` | `Property<DateTime?>` ✅ |
| `Persistence/Migrations/SqlServer/20260523051750_InitialSqlServer.cs:102` | `nullable: true` ✅ |
| `Persistence/Migrations/SqlServer/SqlServerAppDbContextModelSnapshot.cs:1355` | `Property<DateTime?>` ✅ |
| `Data/AppDbContext.cs:235–243` (fluent config) | no `.IsRequired()` on LastGameAt ✅ |

There is no `IEntityTypeConfiguration<PlayerStats>` file — the only
fluent configuration is the inline `modelBuilder.Entity<PlayerStats>`
block in `AppDbContext.cs`, which only sets the PK and FK and does
not constrain `LastGameAt`.

## Actual root cause

`src/backend/src/Mahjong.Autotable.Api/Data/DatabaseBootstrapper.cs`
line 301 (pre-fix) — a Phase J Wave 5 defensive **SQLite-only** bootstrap
function that issues `CREATE TABLE IF NOT EXISTS "PlayerStats" (...)`
to upgrade existing-prod SQLite databases that pre-date the
`AddPlayerProfileAndStats` migration. The hand-rolled SQL was wrong:

```sql
"LastGameAt" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
```

This shadowed the model on any SQLite DB where:

1. The database file already existed when the runtime first booted
   (so `EnsureCreatedAsync` was a no-op — it only creates schema if
   the DB is empty), AND
2. `PlayerStats` was missing (because the DB pre-dated Wave 5).

In that path, the `CREATE TABLE IF NOT EXISTS` from
`EnsureSqlitePlayerTablesAsync` was the only thing landing the table —
so the buggy hand-rolled schema won.

This explains why CI tests pass (CI's test fixtures always create a
fresh in-process SQLite file, so `EnsureCreatedAsync` lands the
correct EF schema and the bootstrap CREATE is a no-op) but Stephen's
dev box trips on it (his `mahjong.db` predates Wave 5).

## Fix (this branch)

Single file changed: `Data/DatabaseBootstrapper.cs`,
`EnsureSqlitePlayerTablesAsync`:

1. Corrected the CREATE TABLE — `"LastGameAt" TEXT NULL`.
2. Added a defensive remediation pass: introspect the existing
   `PlayerStats` table via `PRAGMA table_info`, and if the
   `LastGameAt` column still has `notnull=1`, rebuild the table
   using the SQLite-recommended pattern
   (`CREATE TABLE PlayerStats_new + INSERT SELECT + DROP + RENAME`,
   wrapped in `PRAGMA foreign_keys=OFF; BEGIN; … COMMIT; PRAGMA
   foreign_keys=ON;`). The `INSERT SELECT` maps the buggy sentinel
   default `'0001-01-01 00:00:00'` back to `NULL` so historical rows
   come out semantically correct.

This is fully idempotent: a fresh `EnsureCreatedAsync` DB never trips
the remediation (notnull=0 already), and a re-bootstrap on an
already-fixed DB is also a no-op.

## Why no new migration

The EF migration set and model snapshots are **already correct** —
every provider declares `LastGameAt` as nullable. Adding a
`NullablePlayerStatsLastGameAt` migration would only introduce
no-op churn (Postgres/SqlServer would generate empty migration
bodies) and lengthen the chain Frost's Dealing rework has to rebase
through. SQLite is the only provider whose runtime schema actually
diverged from the model, and that divergence lives in the hand-rolled
bootstrap, not in the migrations.

## What I did NOT touch

Strict lane discipline per Stephen's brief:

- `Changsha/Runtime/**` (Bishop)
- `Changsha/Dealing/**` (Frost — new, WIP)
- `Changsha/Bot/**` / `Changsha/Scoring/**` (Frost)
- `Autotable/**` (Bishop)
- Frontend
- Workflows
- `TestInfrastructure/**` (Vasquez)
- All EF migration files (since they were already correct)

## Verification

### Build + targeted tests
- `dotnet build src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj` — 0 errors, 0 warnings.
- `dotnet test … --filter "FullyQualifiedName~PlayerStats|FullyQualifiedName~PlayerProfile|FullyQualifiedName~DatabaseBootstrap"` — **11/11 passed**.

### Full suite
- `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` — **5219 passed, 0 skipped relevant to my scope, 2 flaky autotable tests** that re-passed 8/8 in isolation (`Autotable/MultiGameRoutingTests`, Bishop's lane; flakes were caused by a concurrent test-runner from another squad agent racing on the shared SQLite test DB during my full-suite run).

### Runtime smoke — fresh DB
- `rm -f .work/drake-fix.db && dotnet run` → `GET /health` 200 OK.
- `POST /api/identity` 200 OK → row inserted with `LastGameAt = NULL`. No `SqliteException`.
- `sqlite3` introspection: `LastGameAt TEXT NULL` (notnull=0). ✅

### Runtime smoke — broken-DB remediation
- Hand-seeded `.work/drake-broken.db` with the old buggy `LastGameAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'` schema + two rows (one with real timestamp, one with sentinel default).
- Booted the backend against it.
- Post-boot introspection:
  - Schema: `LastGameAt TEXT NULL` (notnull=0) ✅
  - Real-timestamp row preserved: `('abc-existing-1', 3, 1, 100, …, '2026-05-15T12:34:56Z')` ✅
  - Sentinel row remapped: `('abc-pre-default', 0, …, None)` ✅
- `POST /api/identity` 200 OK → new row inserted with `LastGameAt = NULL`. ✅

## Future-proofing notes

- If a fourth provider is ever added (MySQL? CockroachDB?), the same
  pattern applies: trust the EF migration for that provider, and only
  add a defensive bootstrap if existing-prod databases need to be
  upgraded without `dotnet ef database update`.
- The defensive bootstrappers in `DatabaseBootstrapper.cs` are
  effectively a hand-rolled migration chain for SQLite. Any future
  schema change to `PlayerStats` (or anything else with a defensive
  `EnsureSqlite…` helper) must update BOTH the EF migration AND the
  hand-rolled SQL — otherwise this exact class of bug recurs.
