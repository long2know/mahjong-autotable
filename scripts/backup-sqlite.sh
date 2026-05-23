#!/usr/bin/env bash
# Phase J Wave 7 — Apone (DevOps).
#
# Backup the SQLite database to a timestamped file using the online
# `.backup` command (NOT a raw `cp`, which races with active writers).
# Honours a retention policy so cron deployments don't grow unbounded.
#
# Usage:
#   scripts/backup-sqlite.sh                              # uses defaults
#   MAHJONG_DB=/var/lib/mahjong/db.sqlite \
#     BACKUP_DIR=/backups/sqlite \
#     RETAIN_COUNT=14 \
#     scripts/backup-sqlite.sh
#
# Cron example (daily at 03:15, retain 14 days, log to syslog):
#   15 3 * * * /opt/mahjong/scripts/backup-sqlite.sh 2>&1 | logger -t mahjong-backup
#
# Exit codes: 0 success, 1 missing dependency, 2 source DB missing,
# 3 sqlite3 backup failed.

set -euo pipefail

DB_PATH="${MAHJONG_DB:-/data/mahjong-autotable.db}"
BACKUP_DIR="${BACKUP_DIR:-/data/backups/sqlite}"
RETAIN_COUNT="${RETAIN_COUNT:-14}"
TIMESTAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="${BACKUP_DIR}/mahjong-autotable-${TIMESTAMP}.db"

if ! command -v sqlite3 >/dev/null 2>&1; then
    echo "❌ sqlite3 not on PATH (apt install sqlite3 / brew install sqlite)" >&2
    exit 1
fi

if [ ! -f "$DB_PATH" ]; then
    echo "❌ source DB not found: $DB_PATH" >&2
    exit 2
fi

mkdir -p "$BACKUP_DIR"

# `.backup` runs an online backup safe against concurrent writers (the
# autotable API may be live during this call). The output is a single
# .db file, identical schema, that can be opened directly with sqlite3.
echo "==> backing up $DB_PATH -> $DEST"
if ! sqlite3 "$DB_PATH" ".backup '$DEST'"; then
    echo "❌ sqlite3 .backup failed" >&2
    exit 3
fi

# Integrity check on the backup so we don't quietly retain a torn file.
if ! sqlite3 "$DEST" "PRAGMA integrity_check;" | grep -q '^ok$'; then
    echo "❌ integrity_check failed on $DEST" >&2
    rm -f "$DEST"
    exit 3
fi

SIZE_BYTES="$(stat -c %s "$DEST" 2>/dev/null || stat -f %z "$DEST")"
echo "✅ backup ok: $DEST ($SIZE_BYTES bytes)"

# Retention — keep the newest $RETAIN_COUNT files, delete the rest.
# `ls -1t` is portable across GNU + BSD; tail -n +N+1 drops the head.
RETAIN_PLUS_ONE=$((RETAIN_COUNT + 1))
DELETED=0
# shellcheck disable=SC2012
ls -1t "$BACKUP_DIR"/mahjong-autotable-*.db 2>/dev/null | tail -n "+${RETAIN_PLUS_ONE}" | while read -r OLD; do
    rm -f "$OLD"
    DELETED=$((DELETED + 1))
    echo "   pruned: $OLD"
done
echo "   retention: keep ${RETAIN_COUNT} newest in ${BACKUP_DIR}"
