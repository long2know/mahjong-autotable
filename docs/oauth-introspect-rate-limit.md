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

The W12 implementation is per-process — three replicas each enforce
the cap independently, so a hostile client gets `3 × Capacity` total
calls per window. This is acceptable for the W12 threat model (defence
against unintentional bursts + low-rate brute-force probes). The W13
Redis-backed swap closes the multi-replica gap.

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
