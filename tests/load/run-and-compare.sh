#!/usr/bin/env bash
# Phase K Wave 1 — Apone (DevOps).
#
# Wrapper around `lobby-flood.js` that:
#   1. Runs the load test against $BASE_URL (default http://localhost:8080).
#   2. Persists the JSON summary as a timestamped artefact under .work/loadtest.
#   3. Loads the previous run's summary (if any) and compares p99 latency
#      per-workload.
#   4. If the current p99 for ANY workload regresses by more than $REGRESSION_PCT
#      (default 25) percent vs prior, exits non-zero so the calling CI workflow
#      fails — which in turn fires the email + Sentry alert.
#   5. Appends a Markdown row to $HISTORY_FILE (default
#      docs/load-test-results-history.md) describing the run.
#
# Idempotent: if no prior run exists, the comparison is skipped (first-run
# behaviour). The script never crashes on missing dirs / missing jq input —
# but it DOES treat any /unparsable/ prior result as a hard alert (better
# false-positive than silent baseline-shift).
#
# Inputs (env vars):
#   BASE_URL         (default http://localhost:8080) — target Mahjong API
#   DURATION_S       (default 60)                    — lobby-flood --duration
#   LOBBY_CONCURRENCY    (default 100)               — lobby workers
#   JOIN_CONCURRENCY     (default 25)                — join workers
#   TOURNAMENT_CONCURRENCY (default 5)               — tournament workers
#   REGRESSION_PCT   (default 25)                    — % p99 regression threshold
#   HISTORY_FILE     (default docs/load-test-results-history.md)
#   WORK_DIR         (default .work/loadtest)        — artefact + prior-run storage
#   SENTRY_DSN       (optional)                      — emit Sentry alert event on regression
#   GIT_SHA          (optional)                      — annotate the history row
#
# Exit codes:
#   0 — load test completed; no regression beyond threshold
#   1 — setup / runtime / parse failure
#   2 — REGRESSION DETECTED (one or more workload p99 grew > REGRESSION_PCT %)
#
# This script is deliberately written without jq's --argjson because GitHub
# Actions runners ship jq 1.6 which is fine, but local devs sometimes have
# old jq versions; we use plain `--arg` + numeric parsing only.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

BASE_URL="${BASE_URL:-http://localhost:8080}"
DURATION_S="${DURATION_S:-60}"
LOBBY_CONCURRENCY="${LOBBY_CONCURRENCY:-100}"
JOIN_CONCURRENCY="${JOIN_CONCURRENCY:-25}"
TOURNAMENT_CONCURRENCY="${TOURNAMENT_CONCURRENCY:-5}"
REGRESSION_PCT="${REGRESSION_PCT:-25}"
HISTORY_FILE="${HISTORY_FILE:-docs/load-test-results-history.md}"
WORK_DIR="${WORK_DIR:-.work/loadtest}"
GIT_SHA="${GIT_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo unknown)}"

mkdir -p "$WORK_DIR"

TS=$(date -u +%Y%m%dT%H%M%SZ)
THIS_JSON="$WORK_DIR/result-$TS.json"
LATEST_LINK="$WORK_DIR/latest.json"
PREV_JSON=""
if [ -L "$LATEST_LINK" ] || [ -f "$LATEST_LINK" ]; then
    # `readlink -f` not portable everywhere; prefer the symlink resolution
    # only when it actually points somewhere readable.
    PREV_JSON="$(readlink -f "$LATEST_LINK" 2>/dev/null || echo "")"
    [ -f "$PREV_JSON" ] || PREV_JSON=""
fi

echo "==> [load] target $BASE_URL — duration ${DURATION_S}s — lobby=$LOBBY_CONCURRENCY join=$JOIN_CONCURRENCY tournament=$TOURNAMENT_CONCURRENCY"

# Ensure `ws` is installed in tests/load — first run on a fresh CI runner
# won't have node_modules/. Idempotent on subsequent runs.
if [ ! -d "tests/load/node_modules/ws" ]; then
    echo "==> [load] installing tests/load deps (one-time)"
    (cd tests/load && npm install --no-audit --no-fund --silent)
fi

set +e
BASE_URL="$BASE_URL" \
    DURATION_S="$DURATION_S" \
    LOBBY_CONCURRENCY="$LOBBY_CONCURRENCY" \
    JOIN_CONCURRENCY="$JOIN_CONCURRENCY" \
    TOURNAMENT_CONCURRENCY="$TOURNAMENT_CONCURRENCY" \
    node tests/load/lobby-flood.js > "$THIS_JSON" 2> "$WORK_DIR/run-$TS.err"
NODE_RC=$?
set -e

if [ "$NODE_RC" -ne 0 ]; then
    echo "❌ lobby-flood.js exited with $NODE_RC; stderr:"
    cat "$WORK_DIR/run-$TS.err"
    exit 1
fi

if ! jq -e . "$THIS_JSON" >/dev/null 2>&1; then
    echo "❌ lobby-flood.js produced non-JSON output:"
    head -50 "$THIS_JSON"
    exit 1
fi

CUR_LOBBY_P99=$(jq -r '.lobby.latency.p99 // empty' "$THIS_JSON")
CUR_JOIN_P99=$(jq -r '.join.latency.p99 // empty' "$THIS_JSON")
CUR_TOUR_P99=$(jq -r '.tournament.latency.p99 // empty' "$THIS_JSON")
CUR_LOBBY_ERR=$(jq -r '.lobby.errorRate // 0' "$THIS_JSON")
CUR_JOIN_ERR=$(jq -r '.join.errorRate // 0' "$THIS_JSON")
CUR_TOUR_ERR=$(jq -r '.tournament.errorRate // 0' "$THIS_JSON")

echo "==> [load] this run — p99 lobby=$CUR_LOBBY_P99 join=$CUR_JOIN_P99 tournament=$CUR_TOUR_P99"

REGRESSION_LINES=()
COMPARE_TO_REF="—"
if [ -n "$PREV_JSON" ] && [ -f "$PREV_JSON" ]; then
    if jq -e . "$PREV_JSON" >/dev/null 2>&1; then
        COMPARE_TO_REF="$(basename "$PREV_JSON")"
        for workload in lobby join tournament; do
            prev=$(jq -r ".${workload}.latency.p99 // empty" "$PREV_JSON")
            curr=$(jq -r ".${workload}.latency.p99 // empty" "$THIS_JSON")
            if [ -z "$prev" ] || [ -z "$curr" ] || [ "$prev" = "null" ] || [ "$curr" = "null" ]; then
                continue
            fi
            # Avoid div-by-zero on a flawless prior p99=0 (rare but possible
            # on a near-empty workload window).
            if awk -v a="$prev" 'BEGIN{exit !(a+0 <= 0)}'; then
                continue
            fi
            grew=$(awk -v c="$curr" -v p="$prev" 'BEGIN{printf "%.2f", ((c-p)/p)*100}')
            # Negative grew = improvement. Only alert on positive growth above threshold.
            if awk -v g="$grew" -v t="$REGRESSION_PCT" 'BEGIN{exit !(g+0 > t+0)}'; then
                REGRESSION_LINES+=("- **${workload} p99 regressed ${grew}%** — was ${prev} ms, now ${curr} ms (threshold ${REGRESSION_PCT}%)")
            fi
        done
    else
        REGRESSION_LINES+=("- ⚠ prior result ($PREV_JSON) unparsable — treating as alertable baseline shift")
    fi
fi

# Append a row to the history file. We use a fenced detail block per run so the
# file stays scannable but full JSON metadata is one click away.
mkdir -p "$(dirname "$HISTORY_FILE")"
if [ ! -f "$HISTORY_FILE" ]; then
    cat > "$HISTORY_FILE" <<'HISTORY_HEADER'
# Load test results — rolling history

> Phase K Wave 1 — Apone (DevOps). Append-only.
>
> Each row corresponds to one run of the
> [`load-test-nightly.yml`](../.github/workflows/load-test-nightly.yml)
> workflow against the production-shaped docker-compose stack. The
> harness is [`tests/load/lobby-flood.js`](../tests/load/lobby-flood.js).
> See [`load-test-results.md`](load-test-results.md) for the SLO budget
> reference, workload mix, and reproduction instructions.

| Run (UTC) | Commit | Duration | Lobby p99 | Join p99 | Tournament p99 | Error rate (max) | Regression vs prior | Notes |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
HISTORY_HEADER
fi

# Compute the worst error rate across the three workloads for the summary cell.
WORST_ERR=$(awk -v a="$CUR_LOBBY_ERR" -v b="$CUR_JOIN_ERR" -v c="$CUR_TOUR_ERR" 'BEGIN{x=a; if (b>x) x=b; if (c>x) x=c; printf "%.4f", x}')

if [ "${#REGRESSION_LINES[@]}" -eq 0 ]; then
    REGRESSION_CELL="✅ within ${REGRESSION_PCT}% threshold"
else
    REGRESSION_CELL="🚨 see notes"
fi

# Notes column: list each regressed workload, or "—" if all clear. Kept on one
# line so the row stays scannable.
if [ "${#REGRESSION_LINES[@]}" -eq 0 ]; then
    NOTES_CELL="ref: ${COMPARE_TO_REF}"
else
    NOTES_CELL=""
    for line in "${REGRESSION_LINES[@]}"; do
        NOTES_CELL+="${line#- } "
    done
    NOTES_CELL="${NOTES_CELL%% }"
fi

printf '| %s | %s | %ss | %s ms | %s ms | %s ms | %s | %s | %s |\n' \
    "$TS" "$GIT_SHA" "$DURATION_S" \
    "${CUR_LOBBY_P99:-—}" "${CUR_JOIN_P99:-—}" "${CUR_TOUR_P99:-—}" \
    "$WORST_ERR" "$REGRESSION_CELL" "$NOTES_CELL" \
    >> "$HISTORY_FILE"

# Refresh the latest symlink so the NEXT run compares against THIS run.
ln -sf "$(basename "$THIS_JSON")" "$LATEST_LINK"

if [ "${#REGRESSION_LINES[@]}" -gt 0 ]; then
    echo
    echo "🚨 Load-test regression detected:"
    for line in "${REGRESSION_LINES[@]}"; do
        echo "    $line"
    done

    # Best-effort Sentry alert via the public store endpoint. We emit a
    # synthetic "message" event so Sentry's existing alert rules (which
    # fan out to email / PagerDuty / Slack) catch it without needing a
    # new alert config. SENTRY_DSN unset → no-op.
    if [ -n "${SENTRY_DSN:-}" ]; then
        if command -v node >/dev/null 2>&1; then
            REG_JSON=$(printf '%s\n' "${REGRESSION_LINES[@]}" | jq -Rs .)
            node -e '
                const dsn = process.env.SENTRY_DSN;
                if (!dsn) process.exit(0);
                const m = dsn.match(/^https:\/\/([^@]+)@([^/]+)\/(\d+)/);
                if (!m) { console.error("bad SENTRY_DSN"); process.exit(0); }
                const [, key, host, project] = m;
                const event = {
                    message: `load-test p99 regression (>${process.env.REGRESSION_PCT}%)`,
                    level: "error",
                    logger: "load-test-nightly",
                    tags: { workflow: "load-test-nightly", git_sha: process.env.GIT_SHA || "unknown" },
                    extra: { regressions: process.env.REG_JSON_RAW, ts: process.env.TS },
                };
                const url = `https://${host}/api/${project}/store/`;
                const auth = `Sentry sentry_version=7, sentry_client=apone-load/1.0, sentry_key=${key}`;
                fetch(url, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "X-Sentry-Auth": auth },
                    body: JSON.stringify(event),
                }).then(r => { console.error(`sentry-alert: HTTP ${r.status}`); })
                  .catch(e => { console.error("sentry-alert: " + e.message); });
            ' || echo "    (sentry alert step exited non-zero — continuing)"
        fi
    fi

    exit 2
fi

echo "✅ Load test passed — no p99 regression beyond ${REGRESSION_PCT}%"
exit 0
