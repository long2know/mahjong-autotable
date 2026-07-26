#!/usr/bin/env bash
# Phase L WP-G (#121) — Apone (DevOps).
#
# Idempotent first-run bootstrap for the default `docker compose` quickstart.
#
# The shipped image runs `ASPNETCORE_ENVIRONMENT=Production`, which refuses to
# boot without a STABLE JWT signing key (a per-process random key would
# silently invalidate every JWT on restart — see JwtSigningKeyProvider.cs /
# docs/jwt-rotation.md §7). docker-compose.yml consumes `JWT_SIGNING_KEY` from
# the auto-loaded `.env` and injects it as `Authentication__JwtSigningKeys__0`.
#
# This script guarantees `.env` exists and carries a stable `JWT_SIGNING_KEY`:
#   * First run: seeds `.env` from `.env.example` (if present) and appends a
#     freshly generated key.
#   * Subsequent runs: a NO-OP when a non-empty key already exists, so the key
#     stays stable across restarts (previously issued JWTs keep validating).
#
# It NEVER commits or bakes a secret: `.env` is gitignored and dockerignored.
# Safe to run before every `docker compose up` (idempotent by design).
#
# Usage:
#   ./scripts/compose-bootstrap.sh            # ensures ./.env has a JWT key
#   ./scripts/compose-bootstrap.sh && docker compose up -d --build

set -euo pipefail

# Resolve repo root from this script's location so it works from any CWD.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

ENV_FILE=".env"
EXAMPLE_FILE=".env.example"

# 1. Seed .env from the template on first run.
if [[ ! -f "$ENV_FILE" ]]; then
    if [[ -f "$EXAMPLE_FILE" ]]; then
        cp "$EXAMPLE_FILE" "$ENV_FILE"
        echo "Created $ENV_FILE from $EXAMPLE_FILE."
    else
        : > "$ENV_FILE"
        echo "Created empty $ENV_FILE."
    fi
fi

# 2. If a non-empty, uncommented JWT_SIGNING_KEY already exists, keep it
#    (stable across restarts). A commented (# ...) or empty (=) line does
#    NOT count as set.
if grep -Eq '^[[:space:]]*JWT_SIGNING_KEY=[^[:space:]]' "$ENV_FILE"; then
    echo "JWT_SIGNING_KEY already set in $ENV_FILE — leaving it unchanged."
    echo "Next: docker compose up -d --build"
    exit 0
fi

# 3. Generate a strong base64 key (~48 bytes). Prefer openssl; fall back to
#    /dev/urandom so the script works on minimal hosts.
if command -v openssl >/dev/null 2>&1; then
    KEY="$(openssl rand -base64 48 | tr -d '\n')"
else
    KEY="$(head -c 48 /dev/urandom | base64 | tr -d '\n')"
fi

# 4. Append the active key. Ensure a trailing newline separation.
[[ -s "$ENV_FILE" && -n "$(tail -c1 "$ENV_FILE")" ]] && printf '\n' >> "$ENV_FILE"
printf 'JWT_SIGNING_KEY=%s\n' "$KEY" >> "$ENV_FILE"

echo "Wrote a new stable JWT_SIGNING_KEY to $ENV_FILE (gitignored — never commit it)."
echo "Next: docker compose up -d --build"
