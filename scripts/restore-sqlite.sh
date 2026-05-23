#!/usr/bin/env bash
# Phase J Wave 7 — Apone (DevOps).
#
# Restore a SQLite backup produced by `backup-sqlite.sh`. The target DB
# is moved aside (.pre-restore-${TIMESTAMP}) before the restore so an
# operator mistake is one `mv` away from rollback.
#
# Usage:
#   scripts/restore-sqlite.sh /backups/sqlite/mahjong-autotable-20260524T031500Z.db
#   MAHJONG_DB=/var/lib/mahjong/db.sqlite \
#     scripts/restore-sqlite.sh /backups/sqlite/mahjong-autotable-...db
#
# Exit codes: 0 success, 1 missing dependency, 2 backup file missing,
# 3 integrity_check failed on source backup, 4 restore I/O failed.

set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "usage: $0 <backup.db>" >&2
    exit 1
fi

SOURCE="$1"
DB_PATH="${MAHJONG_DB:-/data/mahjong-autotable.db}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
ROLLBACK="${DB_PATH}.pre-restore-${TIMESTAMP}"

if ! command -v sqlite3 >/dev/null 2>&1; then
    echo "❌ sqlite3 not on PATH" >&2
    exit 1
fi

if [ ! -f "$SOURCE" ]; then
    echo "❌ backup file not found: $SOURCE" >&2
    exit 2
fi

echo "==> validating source backup ($SOURCE)"
if ! sqlite3 "$SOURCE" "PRAGMA integrity_check;" | grep -q '^ok$'; then
    echo "❌ integrity_check failed on $SOURCE" >&2
    exit 3
fi

mkdir -p "$(dirname "$DB_PATH")"

if [ -f "$DB_PATH" ]; then
    echo "==> preserving existing DB at $ROLLBACK"
    cp -p "$DB_PATH" "$ROLLBACK" || { echo "❌ failed to snapshot existing DB" >&2; exit 4; }
fi

echo "==> restoring $SOURCE -> $DB_PATH"
# `.restore` overwrites the destination file with the backup's pages —
# atomic from the SQLite perspective (the destination DB is fsynced on
# close). The destination need not exist beforehand.
if ! sqlite3 "$DB_PATH" ".restore '$SOURCE'"; then
    echo "❌ sqlite3 .restore failed" >&2
    if [ -f "$ROLLBACK" ]; then
        echo "   restoring previous DB from $ROLLBACK"
        cp -p "$ROLLBACK" "$DB_PATH"
    fi
    exit 4
fi

# Final integrity check on the now-restored DB.
if ! sqlite3 "$DB_PATH" "PRAGMA integrity_check;" | grep -q '^ok$'; then
    echo "❌ integrity_check failed on restored DB $DB_PATH" >&2
    if [ -f "$ROLLBACK" ]; then
        echo "   restoring previous DB from $ROLLBACK"
        cp -p "$ROLLBACK" "$DB_PATH"
    fi
    exit 4
fi

echo "✅ restore complete: $DB_PATH"
echo "   rollback copy:    $ROLLBACK (safe to delete after verification)"
