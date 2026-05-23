#!/usr/bin/env bash
# Phase J Wave 7 — Apone (DevOps).
#
# Postgres backup using `pg_dump` (custom format = `-Fc`, the only format
# `pg_restore` accepts for selective restores). Honours the same
# RETAIN_COUNT / BACKUP_DIR contract as `backup-sqlite.sh` so a single
# cron entry can wrap both.
#
# Env contract (all optional except a way to authenticate):
#   PGHOST     (default: localhost)
#   PGPORT     (default: 5432)
#   PGUSER     (default: mahjong)
#   PGDATABASE (default: mahjong_autotable)
#   PGPASSWORD (no default — prefer ~/.pgpass or PG_SERVICE)
#   BACKUP_DIR (default: /data/backups/postgres)
#   RETAIN_COUNT (default: 14)
#
# Cron example (daily 03:30, retain 14 backups):
#   30 3 * * * /opt/mahjong/scripts/backup-postgres.sh 2>&1 | logger -t mahjong-pgbackup
#
# Exit codes: 0 success, 1 missing dependency, 3 pg_dump failed.

set -euo pipefail

export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-5432}"
export PGUSER="${PGUSER:-mahjong}"
export PGDATABASE="${PGDATABASE:-mahjong_autotable}"

BACKUP_DIR="${BACKUP_DIR:-/data/backups/postgres}"
RETAIN_COUNT="${RETAIN_COUNT:-14}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="${BACKUP_DIR}/${PGDATABASE}-${TIMESTAMP}.dump"

if ! command -v pg_dump >/dev/null 2>&1; then
    echo "❌ pg_dump not on PATH (apt install postgresql-client)" >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR"

# `-Fc` (custom binary format) + `-Z 6` (zstd-ish gzip level) keeps the
# dump small and lets `pg_restore --list` peek without unpacking.
# `--no-owner --no-privileges` makes the dump portable across roles —
# critical when restoring into a freshly-provisioned target DB.
echo "==> pg_dump $PGUSER@$PGHOST:$PGPORT/$PGDATABASE -> $DEST"
if ! pg_dump -Fc -Z 6 --no-owner --no-privileges -f "$DEST"; then
    echo "❌ pg_dump failed" >&2
    rm -f "$DEST"
    exit 3
fi

SIZE_BYTES="$(stat -c %s "$DEST" 2>/dev/null || stat -f %z "$DEST")"
echo "✅ backup ok: $DEST ($SIZE_BYTES bytes)"

# Retention — keep newest $RETAIN_COUNT dumps. `ls -1t` is the portable
# newest-first sort across GNU + BSD.
RETAIN_PLUS_ONE=$((RETAIN_COUNT + 1))
# shellcheck disable=SC2012
ls -1t "$BACKUP_DIR"/${PGDATABASE}-*.dump 2>/dev/null | tail -n "+${RETAIN_PLUS_ONE}" | while read -r OLD; do
    rm -f "$OLD"
    echo "   pruned: $OLD"
done
echo "   retention: keep ${RETAIN_COUNT} newest in ${BACKUP_DIR}"
