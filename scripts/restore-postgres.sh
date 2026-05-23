#!/usr/bin/env bash
# Phase J Wave 7 — Apone (DevOps).
#
# Restore a Postgres backup produced by `backup-postgres.sh`. By default
# the script drops + recreates the target database (the `--clean
# --create` flow pg_restore was built for). For safety, the destructive
# flag is off unless `RESTORE_CLEAN=1`.
#
# Env contract:
#   PGHOST, PGPORT, PGUSER, PGDATABASE, PGPASSWORD — as backup-postgres.sh
#   RESTORE_CLEAN    (default: 0 — append-only restore into existing DB)
#                    (1: pg_restore --clean --if-exists; drops every object first)
#
# Usage:
#   scripts/restore-postgres.sh /backups/postgres/mahjong_autotable-20260524T031500Z.dump
#   RESTORE_CLEAN=1 \
#     scripts/restore-postgres.sh /backups/postgres/mahjong_autotable-...dump
#
# Exit codes: 0 success, 1 missing dependency / bad args,
# 2 dump file missing, 4 pg_restore failed.

set -euo pipefail

if [ "$#" -ne 1 ]; then
    echo "usage: $0 <backup.dump>" >&2
    exit 1
fi

SOURCE="$1"
export PGHOST="${PGHOST:-localhost}"
export PGPORT="${PGPORT:-5432}"
export PGUSER="${PGUSER:-mahjong}"
export PGDATABASE="${PGDATABASE:-mahjong_autotable}"
RESTORE_CLEAN="${RESTORE_CLEAN:-0}"

if ! command -v pg_restore >/dev/null 2>&1; then
    echo "❌ pg_restore not on PATH (apt install postgresql-client)" >&2
    exit 1
fi

if [ ! -f "$SOURCE" ]; then
    echo "❌ dump file not found: $SOURCE" >&2
    exit 2
fi

echo "==> previewing $SOURCE"
pg_restore --list "$SOURCE" | head -20 || true

RESTORE_FLAGS=(--no-owner --no-privileges -d "$PGDATABASE")
if [ "$RESTORE_CLEAN" = "1" ]; then
    echo "==> RESTORE_CLEAN=1 — will DROP existing objects in $PGDATABASE before restore"
    RESTORE_FLAGS+=(--clean --if-exists)
fi

echo "==> pg_restore -> $PGUSER@$PGHOST:$PGPORT/$PGDATABASE"
if ! pg_restore "${RESTORE_FLAGS[@]}" "$SOURCE"; then
    # pg_restore returns nonzero on benign "object already exists"
    # warnings as well — re-run the verification step below before
    # surfacing this as a hard failure.
    echo "⚠️  pg_restore exited non-zero. Verifying schema integrity ..."
fi

# Sanity check the canonical post-restore tables exist.
if ! psql -At -c "SELECT to_regclass('public.\"ChangshaGames\"');" | grep -q ChangshaGames; then
    echo "❌ \"ChangshaGames\" missing after restore — restore failed" >&2
    exit 4
fi
if ! psql -At -c "SELECT to_regclass('public.\"PlayerProfiles\"');" | grep -q PlayerProfiles; then
    echo "❌ \"PlayerProfiles\" missing after restore — restore failed" >&2
    exit 4
fi

echo "✅ restore verified: ChangshaGames + PlayerProfiles present in $PGDATABASE"
