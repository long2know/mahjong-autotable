# OAuth introspect rate limiting

Phase K Wave 12 — Bishop.

## Why

The RFC 7662 token-introspection endpoint at `POST /api/auth/introspect`
is a high-value attack surface — a brute-force loop against a single
client credential could probe thousands of tokens per second. Wave 11
shipped Basic-auth + per-client allowlists; Wave 12 layers a
per-client sliding-window rate limit on top so a misbehaving (or
compromised) client can't saturate the endpoint.

## Surface

* Interface: `Auth/OAuthIntrospectRateLimiter.cs::IOAuthIntrospectRateLimiter`.
* Decision envelope: `OAuthIntrospectRateLimitDecision(bool Allowed, int Remaining, int RetryAfterSeconds)`.
* Default implementation: in-memory sliding-window keyed by clientId. A multi-replica deployment swap to a Redis-backed implementation lands in W13; the interface seam stays unchanged.

## Configuration

| Key                                | Default | Notes                          |
| ---------------------------------- | ------- | ------------------------------ |
| `OAuth:Introspect:RateLimitPerClient` | 100  | Calls allowed per window       |
| `OAuth:Introspect:WindowSeconds`      | 60   | Sliding window in seconds      |

## Response shape

A throttled request returns HTTP 429 with the canonical headers:

* `X-RateLimit-Limit` — configured `RateLimitPerClient`.
* `X-RateLimit-Remaining` — 0 on a deny.
* `X-RateLimit-Window` — configured `WindowSeconds`.
* `Retry-After` — seconds until the oldest in-window request rolls off.

Allowed responses also stamp `X-RateLimit-*` so well-behaved clients
can self-pace.

## Algorithm

Per-client `Queue<DateTimeOffset>`. On each `TryAcquire`:

1. Drop timestamps older than `now - WindowSeconds`.
2. If the queue is full → deny with `Retry-After = ceil(oldest + window - now)`.
3. Otherwise → enqueue the current timestamp, return `Remaining = Capacity - Count`.

The whole operation is O(W) where W ≤ capacity ≤ 100 by default.

## Multi-replica considerations

### §2.1 — Per-process default (W12)

The W12 implementation is per-process — three replicas each enforce
the cap independently, so a hostile client gets `3 × Capacity` total
calls per window. This is acceptable for the W12 threat model (defence
against unintentional bursts + low-rate brute-force probes).

### §2.2 — Redis-backed limiter (Phase K Wave 13)

Wave 13 lands `RedisOAuthIntrospectRateLimiter` — a shared
sliding-window keyed on a single Redis sorted-set per client, so
every replica reads + writes the same rolling count. Opt in via:

| Key                                          | Default      | Notes                                |
| -------------------------------------------- | ------------ | ------------------------------------ |
| `OAuth:Introspect:LimiterImpl`               | `InMemory`   | `"Redis"` for multi-replica          |
| `OAuth:Introspect:RedisConnectionString`     | (empty)      | Standard `IConnectionMultiplexer` URI |
| `OAuth:Introspect:RedisDatabaseIndex`        | -1           | Default DB; override for isolation   |

### §2.3 — Redis script

Three commands per `TryAcquire`:

1. `ZREMRANGEBYSCORE key -inf cutoff` — drops timestamps older than
   `now - WindowSeconds`.
2. `ZCARD key` — current rolling count.
3. On allow: `ZADD key now unique-token` + `EXPIRE key window` so
   abandoned client buckets self-evict.

Each replica writes a unique token (`{nowMs}:{Guid.NewGuid()}`) so
concurrent calls from different replicas can't collide on the sorted-set
score and over-write each other.

### §2.4 — Failure mode

On any Redis error the limiter falls back to an in-process
`OAuthIntrospectRateLimiter` constructed with the same capacity +
window. A transient outage degrades to per-replica enforcement
rather than failing open — the operator sees a structured warning
log and can investigate without the limiter dropping every request.

### §2.5 — Key namespacing

`KeyPrefix = "mahjong:oauth-introspect:"` — the limiter does not
collide with other Redis consumers sharing the same logical
database.

### §2.6 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Bishop/RedisOAuthIntrospectRateLimiterTests.cs`:

* `OAuthIntrospectRateLimitOptions.LimiterImpl` defaults to `"InMemory"`.
* `RedisOAuthIntrospectRateLimiter` satisfies `IOAuthIntrospectRateLimiter`.
* Redis key for client `foo` is `mahjong:oauth-introspect:foo`.
* `TryAcquire` falls back to the in-memory limiter when Redis
  throws (no exception escapes; allow/deny semantics preserved).
* `RequestsPerWindow` / `WindowSeconds` mirror the configured
  options for the controller's response headers.

## Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W12/Bishop/OAuthIntrospectRateLimitFacts.cs`:

* `OAuthIntrospectRateLimitOptions` defaults: `RateLimitPerClient = 100`, `WindowSeconds = 60`.
* First call is allowed.
* `Remaining` decrements per successful call.
* Exceeding the capacity denies further calls.
* Denied decisions carry `RetryAfterSeconds > 0`.
* The limiter surfaces `RequestsPerWindow` + `WindowSeconds` for the controller's response headers.
* Distinct client ids have independent buckets.
* Elapsing the window clears the bucket (sliding-window behaviour).
