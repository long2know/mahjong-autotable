# Backup & restore

Phase J Wave 7 — Apone (DevOps).

Scripts live under [`scripts/`](../scripts/) and cover both the SQLite
dev/single-replica DB and Postgres production deployments. All four
scripts honour a consistent env contract and emit timestamped artifacts
named `<db>-YYYYMMDDTHHMMSSZ.{db,dump}`.

| Script                       | Purpose                                           |
| ---------------------------- | ------------------------------------------------- |
| `scripts/backup-sqlite.sh`   | Online SQLite backup via `sqlite3 .backup`.       |
| `scripts/restore-sqlite.sh`  | Restore SQLite from a `.db` produced above.       |
| `scripts/backup-postgres.sh` | `pg_dump -Fc` custom-format Postgres backup.      |
| `scripts/restore-postgres.sh`| `pg_restore` from the `.dump` above.              |

## SQLite

```bash
# Defaults: MAHJONG_DB=/data/mahjong-autotable.db, BACKUP_DIR=/data/backups/sqlite, RETAIN_COUNT=14
sudo MAHJONG_DB=/var/lib/mahjong/db.sqlite \
     BACKUP_DIR=/srv/backups/mahjong/sqlite \
     RETAIN_COUNT=30 \
     /opt/mahjong/scripts/backup-sqlite.sh
```

The script uses SQLite's online `.backup` command — safe against an
active writer (the autotable API can be live during this call). The
output `.db` file is itself validated with `PRAGMA integrity_check;`
before retention prunes older backups.

Restore (preserves the existing DB at `<db>.pre-restore-<TIMESTAMP>` so
operator mistakes are one `mv` away from rollback):

```bash
sudo MAHJONG_DB=/var/lib/mahjong/db.sqlite \
     /opt/mahjong/scripts/restore-sqlite.sh \
         /srv/backups/mahjong/sqlite/mahjong-autotable-20260524T031500Z.db
```

## Postgres

```bash
# Defaults: PGHOST=localhost, PGUSER=mahjong, PGDATABASE=mahjong_autotable
# Authentication: prefer ~/.pgpass; PGPASSWORD env works too.
PGHOST=postgres.internal \
PGUSER=mahjong \
PGDATABASE=mahjong_autotable \
BACKUP_DIR=/srv/backups/mahjong/postgres \
RETAIN_COUNT=30 \
/opt/mahjong/scripts/backup-postgres.sh
```

Uses `pg_dump -Fc -Z 6 --no-owner --no-privileges`:
- **`-Fc`** (custom format) is the only format `pg_restore --list` can
  preview without unpacking and is what `--clean --if-exists` selective
  restores require.
- **`--no-owner --no-privileges`** keeps the dump portable — when
  restoring into a freshly-provisioned role you don't want the dump to
  re-assert the source DB's GRANT statements.

Restore (append-only by default; `RESTORE_CLEAN=1` drops every object in
the target DB first):

```bash
# Safe append (will surface duplicate-object warnings if rows exist)
PGHOST=postgres.internal PGUSER=mahjong PGDATABASE=mahjong_autotable \
    /opt/mahjong/scripts/restore-postgres.sh \
        /srv/backups/mahjong/postgres/mahjong_autotable-20260524T031500Z.dump

# Destructive — wipes existing schema/data first
RESTORE_CLEAN=1 \
PGHOST=postgres.internal PGUSER=mahjong PGDATABASE=mahjong_autotable \
    /opt/mahjong/scripts/restore-postgres.sh \
        /srv/backups/mahjong/postgres/mahjong_autotable-20260524T031500Z.dump
```

The restore script ends with a sanity-check that re-queries
`to_regclass('public."ChangshaGames"')` and `to_regclass('public."PlayerProfiles"')`
so a half-successful restore (e.g. transient connection loss) fails loud
instead of leaving an empty DB labelled "ok".

## Cron

Daily 03:15 (SQLite) + 03:30 (Postgres) — staggered so they don't race
on a shared backup partition. Both pipe through `logger -t` so the
output lands in syslog where it's already retention-managed:

```cron
15 3 * * * /opt/mahjong/scripts/backup-sqlite.sh 2>&1 | logger -t mahjong-backup
30 3 * * * /opt/mahjong/scripts/backup-postgres.sh 2>&1 | logger -t mahjong-pgbackup
```

For Kubernetes deploys, run the equivalent as a `CronJob` against the PVC
(SQLite) or a sidecar `pg_dump` container (Postgres) — see
[`docs/kubernetes.md`](kubernetes.md) for the manifests.

## Off-site

The scripts only write locally. For off-site copies, pipe through
`aws s3 cp`, `gsutil cp`, `b2`, `rclone`, etc. as a downstream cron step.
Example:

```cron
35 3 * * * aws s3 sync /srv/backups/mahjong/postgres/ s3://mahjong-backups/postgres/ \
           --no-progress 2>&1 | logger -t mahjong-backup-s3
```

## Restore drills

We recommend a quarterly restore drill:

1. Provision a throwaway Postgres + autotable container pair.
2. `pg_restore` the most recent off-site dump into the throwaway DB.
3. Boot the autotable against it and hit `/health` + replay a recent
   completed game (Bishop's Wave-7 replay endpoint at
   `/api/games/{gameId}/replay`).
4. Tear down.

This proves the **whole** restore pipeline (offsite → restore → app
boot → end-user-visible read) rather than just the `pg_restore` exit
code.
