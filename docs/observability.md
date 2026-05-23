# Observability

Phase J Wave 5 (Apone, DevOps) introduced the minimum observability
surface a self-hosted deployment of `mahjong-autotable` needs to be
operable in production: a Prometheus-style `/metrics` endpoint, JSON
structured logging in `Production`, and the existing `/health` probe
documented from Phase J Wave 3.

This document is the source of truth for operators. Each metric and
log shape below is contract-grade — renaming or removing one is a
breaking change that requires a coordinated memo before merging.

## Endpoints summary

| Endpoint | Surface | Auth | Format | Owner |
|---|---|---|---|---|
| `GET /health` | Container probe (Docker `HEALTHCHECK`, k8s liveness) | none | JSON | Bishop (Phase J Wave 3) |
| `GET /api/health` | Legacy short-form probe (frontend boot check) | none | JSON | Bishop (Wave 3) |
| `GET /metrics` | Prometheus scrape target | none | text/plain; version=0.0.4 | Apone (Phase J Wave 5) |
| `GET /api/system/persistence` | Diagnostic — which EF provider is active | none | JSON | Bishop |

> **Security note.** `/metrics` exposes operationally-sensitive data
> (uptime, active-game count, build SHA). When deploying outside a
> private network, terminate the metrics endpoint behind a reverse-proxy
> auth check (Caddy / nginx `auth_request` / Traefik basic-auth) — the
> application itself does NOT authenticate it.

## `/metrics`

Returns the canonical Prometheus text exposition format. A `prometheus`
scrape config can target it without conversion:

```yaml
scrape_configs:
  - job_name: mahjong-autotable
    scrape_interval: 30s
    static_configs:
      - targets: ['mahjong-autotable:8080']
```

### Metric catalog

#### `mahjong_uptime_seconds` (gauge)

Process uptime in seconds since the API container started. Anchored on
`Observability.MetricsEndpoint.ProcessStartTime` (set at type-init). Use
this to confirm a deploy "took" — the value drops back to ~0 right after
a successful restart.

```promql
# Detect a restart within the last 5 minutes.
mahjong_uptime_seconds < 300
```

#### `mahjong_active_games_total` (gauge)

Currently active in-memory Changsha games. Read from
`IChangshaGameRuntime.GameCount`, which is the count of non-terminal
`ChangshaGameInstance` entries hydrated either from a `CreateGame` call
or by `HydrateAsync` on startup. The terminal phases (`GameComplete`
and the alias-merged `EndGame`) drop out via the normal game lifecycle.

```promql
# Sustained high load — investigate cleanup / disconnect handlers.
max_over_time(mahjong_active_games_total[1h]) > 50

# Alert: a fresh deploy lost in-flight games.
delta(mahjong_active_games_total[5m]) < -5
```

#### `mahjong_build_info` (gauge)

Always `1`. The `sha="<value>"` label carries the build identifier
sourced from the `BUILD_SHA` environment variable (CI sets it to the
commit SHA via the `docker-build` workflow). When unset or empty, the
label value is `dev`.

```promql
# What's actually running in each environment right now?
mahjong_build_info{sha!="dev"}
```

### Sample output

```
# HELP mahjong_uptime_seconds Process uptime in seconds since the API container started.
# TYPE mahjong_uptime_seconds gauge
mahjong_uptime_seconds 12.345
# HELP mahjong_active_games_total Currently active in-memory Changsha games (non-terminal).
# TYPE mahjong_active_games_total gauge
mahjong_active_games_total 3
# HELP mahjong_build_info Build identifier surfaced as a label. Always 1; the sha="..." label carries the value.
# TYPE mahjong_build_info gauge
mahjong_build_info{sha="abc123def456"} 1
```

### Manual probe

```bash
curl -s http://localhost:8080/metrics
```

### Extension points

The current endpoint is intentionally a **no-dependency** baseline.
When the team needs counters / histograms / observable instruments:

- **Recommended path:** add `prometheus-net.AspNetCore` and replace
  `MetricsEndpoint.Render` with the library's `UseHttpMetrics` +
  `MapMetrics` middleware. Keep the three existing gauges for
  backwards compatibility (gauge names are public contract).
- Counters worth landing first: hand-completions, claim-window
  expirations, WS disconnects, bot decision timeouts.
- Histograms worth landing first: hand duration (seconds), bot
  decision latency (milliseconds), SignalR broadcast fanout.

## Structured logging

Wired in `Program.cs` (Phase J Wave 5) via
`builder.Logging.ClearProviders()` + environment-aware provider
selection.

### Production — JSON

When `ASPNETCORE_ENVIRONMENT=Production` (the Docker default), every
log line is single-line JSON:

```json
{"EventId":0,"LogLevel":"Information","Category":"Mahjong.Autotable.Api.Changsha.Runtime.ChangshaGameRuntime","Message":"Created game ABC123 with 3 bots","State":{"Message":"Created game ABC123 with 3 bots","gameId":"ABC123","botCount":3},"Scopes":[{"Message":"GameId:ABC123"}]}
```

Required fields (.NET emits these automatically):

| Field | Meaning |
|---|---|
| `EventId` | Numeric event id (0 when not set explicitly) |
| `LogLevel` | `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical` |
| `Category` | Source type's full namespace + class name |
| `Message` | Formatted message string |
| `State` | Structured key-value pairs from the log-message template |
| `Scopes` | All open `BeginScope(...)` frames at log time |
| `Exception` | (if present) full exception string with stack trace |

`IncludeScopes = true` is set so SignalR's `ConnectionId` / `HubMethodName`
scopes surface in every line emitted inside a hub method.

### Development — SimpleConsole

When `ASPNETCORE_ENVIRONMENT` is unset or `Development`, the human-
readable single-line formatter is used:

```
14:32:08 info: Mahjong.Autotable.Api.Changsha.Runtime.ChangshaGameRuntime[0] => GameId:ABC123 Created game ABC123 with 3 bots
```

This matches what `dotnet run` developers see in the VS Code debug
console.

### Sample queries (Loki / LogQL)

```logql
# All warnings + errors from the Changsha runtime.
{container="mahjong-autotable"} | json | LogLevel="Warning" or LogLevel="Error"
| Category =~ "Mahjong.Autotable.Api.Changsha.Runtime.*"

# Per-game timeline — pull every log line for a single game id.
{container="mahjong-autotable"} | json | State_gameId="ABC123"

# Bot decision timeout rate (count over 5 min).
sum(rate({container="mahjong-autotable"}
         | json
         | Message =~ ".*bot decision timeout.*" [5m]))
```

### Sample queries (Elastic / Kibana KQL)

```kql
LogLevel: ("Warning" or "Error") and Category: "Mahjong.Autotable.Api.*"
LogLevel: "Information" and State.gameId: "ABC123"
Message: *"claim window expired"*
```

## `/health` (recap)

Documented in detail in `docs/deployment.md`. Returns:

```json
{
  "status": "healthy",
  "buildSha": "abc123…",   // or "dev" when BUILD_SHA env unset/empty
  "uptime": "00:00:12.345",
  "version": "1.0.0.0"
}
```

`/metrics` and `/health` agree on `buildSha` / `uptime`; the two
endpoints share the same `BUILD_SHA` env-var contract and module-load
anchor pattern but capture independent `DateTimeOffset.UtcNow` ticks
during startup, so the absolute uptime values may differ by a few
milliseconds — the metric should be treated as an approximation, not
a precise clock.

## Operational runbook snippets

### Confirm a deploy reached the runtime

```bash
EXPECTED_SHA="<git rev-parse HEAD on main>"
curl -s http://prod-host:8080/metrics | grep '^mahjong_build_info' | grep "sha=\"${EXPECTED_SHA}\""
```

### Detect a runtime that's been up suspiciously long (forgot-to-restart)

```bash
curl -s http://prod-host:8080/metrics | awk '/^mahjong_uptime_seconds/ {print $2}'
# If the value > 30 days, schedule a redeploy.
```

### Alert: active games stuck at zero

If the platform has actual users but `mahjong_active_games_total` reads
`0` for more than 5 minutes, the runtime probably failed to hydrate
from `ChangshaGames` (see `IChangshaGameRuntime.HydrateAsync`). Check
the logs for `Failed to hydrate game` warnings.

## Cross-references

- `docs/deployment.md` — Docker / Linux deploy runbook
- `docs/docker.md` — 5-minute quickstart
- `docs/secrets.md` — secrets handling (env vars, Docker / k8s secrets)
- `docs/ci.md` — CI workflow catalog (Phase J Wave 4)
- `src/backend/src/Mahjong.Autotable.Api/Observability/MetricsEndpoint.cs`
  — endpoint source
- `src/backend/src/Mahjong.Autotable.Api/Program.cs` — logger config
