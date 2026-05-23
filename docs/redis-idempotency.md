# Redis Idempotency Store

**Owner:** Bishop (Backend) · **Wave:** Phase K Wave 10

The `RedisIdempotencyStore` is the production-grade backing for the
`IdempotencyMiddleware` replay-protection gate. It replaces the W9
wrapper-around-EF stub with a real `StackExchange.Redis` client so
multi-replica deployments share a single low-latency replay-window
store instead of round-tripping through the backing RDBMS.

This document is the operator runbook. See
`src/backend/src/Mahjong.Autotable.Api/Audit/EfIdempotencyStore.cs`
(despite the name, the file hosts both the EF and Redis
implementations) for the source.

## §1 — Connection

The `IConnectionMultiplexer` is built from a configuration string
read by `Program.cs` in this priority order:

1. `Idempotency:Redis:ConnectionString` — preferred. Example:
   ```json
   {
     "Idempotency": {
       "StoreImpl": "Redis",
       "Redis": {
         "ConnectionString": "redis-prod.example.com:6379,password=...,ssl=true,abortConnect=false"
       }
     }
   }
   ```
2. `ConnectionStrings:Redis` — legacy fallback (Apone's W10 Terraform
   uses this key by default).
3. `Idempotency:RedisConnection` — older W9 key, retained for
   backward compatibility.

When `Idempotency:StoreImpl = "Redis"` but no connection string is
configured, the host falls back to the `EfIdempotencyStore` and logs
a warning — the deployment doesn't silently lose its durability
contract.

The `ConnectionMultiplexer` is registered as a singleton (one TCP
connection per replica, multiplexed across requests). The same
instance is reused for any future Redis consumers (the W11 Janus
mountpoint registry is on this hand-off list).

## §2 — Key Format

Every idempotency entry is stored under the key prefix
`mahjong:idem:` so the store doesn't collide with other Redis
consumers sharing the same logical database:

```
mahjong:idem:<idempotency-key>
```

The value is a pipe-delimited envelope versioned at v1 so the wire
format can evolve mid-rotation without bricking deployments:

```
v1|<status>|<recorded-at-utc-ticks>|<content-type>|<payload-hash>|<response-body>
```

Pipes and backslashes inside any field are escaped (`|` → `\p`,
`\` → `\\`) so legitimate response bodies containing pipes survive
the round-trip cleanly. The envelope keeps the Redis value compact
(under 4 KB even at the EF store's max response body length).

## §3 — TTL Semantics

Every `SET` uses `NX EX <ttl>` — atomic insert-if-absent with TTL.
The default TTL is `RedisIdempotencyStore.DefaultReplayWindow` (5
minutes, matching `EfIdempotencyStore.DefaultReplayWindow` and the
Stripe convention).

* **Atomic insert** — two replicas racing on the same key collapse
  cleanly: the loser's SET returns false, and the middleware
  re-reads to get the canonical entry. The payload-hash check inside
  the middleware then drives the 409 payload-mismatch envelope as
  authoritative.
* **Expiry** — Redis handles expiration internally via its
  keyspace-level TTL thread. The application-side `Sweep(cutoffUtc)`
  is a no-op for the Redis store because the work is already done.
  This is in contrast to `EfIdempotencyStore.Sweep`, which the
  background sweeper invokes on a slow cadence to drop expired rows.
* **Refresh** — when the same key is `Record()`'d again (legitimate
  retry within the window), the TTL is refreshed by a second SET
  (without NX) so steady-state replay traffic keeps the entry warm
  for its full 5-minute window.

## §4 — Failure Handling

The store is degradation-safe. Every Redis call is wrapped in a
try/catch:

* **Read failure** (timeout, connect error) — falls back to the
  registered `EfIdempotencyStore` if present, returns null otherwise.
  Logged at warning level so operators see the degradation.
* **Write failure** — falls back to the `EfIdempotencyStore.Record`
  path. The downstream `IdempotencyMiddleware` doesn't fail the
  request: the worst case is that a retry within the replay window
  is processed twice (the original Wave-8 behaviour before the
  durability guarantee).
* **Remove failure** — falls back to `EfIdempotencyStore.Remove`.

`abortConnect=false` is the recommended connection-string flag so a
transient Redis-cluster outage at boot doesn't crash the host.

## §5 — Observability

The store logs at `Information` level on construction (endpoints +
database + TTL + fallback presence) and at `Warning` level on every
fallback. Operators monitor the `RedisIdempotencyStore` logger:

* `Information` rate ≈ 1/process boot — expected.
* `Warning` rate > 0/min sustained — Redis is degraded; investigate
  cluster health.

The standard `StackExchange.Redis` event tracing (connection state,
reconnect attempts) flows through the host's `IConnectionMultiplexer`
event handlers — the operator's Prometheus scrape picks them up via
the `Microsoft.AspNetCore.Hosting` meter.

## §6 — Tests

* `Phase_K_W10/Bishop/RedisIdempotencyStoreContractTests` — pure
  unit tests against a stub `IConnectionMultiplexer` + `IDatabase`.
  Pin the SET-NX-EX semantics, key prefix, serialization round-trip,
  and fallback behaviour without a live Redis dependency. Always
  run.
* `Phase_K_W10/Bishop/RedisIdempotencyStoreLiveTests` — opt-in
  Testcontainers-style live tests. The harness checks for a live
  Redis URL via the `MAHJONG_REDIS_LIVE_URL` environment variable.
  When unset (the default on CI), the tests soft-pass via an early
  `return` so the gate stays green without Docker. Local operators
  export the URL before running to exercise the real TCP path.
