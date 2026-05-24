# Spectator handoff

Phase K Wave 13 — Bishop.

## §1 — Overview

`POST /api/spectator/handoff` mints a short-lived JWT scoped to a
single game so the frontend can hand off a player session to the
spectator view without re-authenticating. The token carries
`scope = spectator:{gameId}` and is consumed by
`SpectatorLivestreamHub`'s join filter.

The W12 surface returned the token but left no durable trail. The W13
work lands a persisted **audit row** for every mint so the security
review can reconstruct issuance history without parsing the
application log stream.

## §2 — Mint endpoint

* **Route**: `POST /api/spectator/handoff`
* **Auth**: cookie/session — the controller resolves the caller's
  `userId` from the standard auth middleware.
* **Body**: `{ gameId: string }`
* **Response**: `{ token: string, scope: string, expiresAt: string }`

The handoff is rate-limited per session via the existing W11 admission
policy (see `docs/admission-policy.md`); the audit log captures both
allowed and denied attempts (allowed mints only — denied requests are
rejected before reaching the audit insert).

## §3 — Audit

### §3.1 — Why a durable audit

Spectator tokens are short-lived (≤ 60s) but carry game-scoped
read access. A compromised session that mints a spectator token can
exfiltrate live play-by-play to a third-party viewer. The audit row
gives operators the join-key (JWT `jti`) to correlate a leaked stream
with the originating session.

### §3.2 — Schema

| Column      | Type         | Notes                                                |
| ----------- | ------------ | ---------------------------------------------------- |
| `Id`        | Guid PK      | Surrogate                                            |
| `UserId`    | string       | Resolved subject (auth principal at mint time)       |
| `GameId`    | Guid         | Game scope                                           |
| `TokenJti`  | string       | JWT `jti` claim — **unique index**                   |
| `IssuedAt`  | DateTime UTC | Mint timestamp                                       |
| `Scope`     | string       | Resolved scope literal (`spectator:{gameId}`)        |
| `ClientIp`  | string       | Best-effort remote IP — empty when transport hides   |
| `UserAgent` | string       | Truncated to 256 chars                               |

**Indices**:

* `(TokenJti)` — unique. Powers revocation lookups.
* `(GameId, IssuedAt)` — per-game audit listing.
* `(UserId)` — per-user audit listing.
* `(IssuedAt)` — sweep predicate.

### §3.3 — Configuration

| Key                                | Default      | Notes                              |
| ---------------------------------- | ------------ | ---------------------------------- |
| `Spectator:Audit:StorageImpl`      | `InMemory`   | `"Ef"` for production              |
| `Spectator:Audit:RetentionDays`    | 30           | Cutoff for `SweepExpiredAsync`     |

### §3.4 — Write semantics

The controller writes the audit row **after** `JwtIssuingService.IssueAsync`
returns but **before** the response is flushed. A try/catch wraps the
insert so an audit-store failure does not strand the caller — the token
is still returned, and the failure is logged. The worst case is an
orphan audit row whose token never reaches the client; this is
operationally indistinguishable from a token issued + immediately
dropped.

### §3.5 — Migration

Table `SpectatorHandoffAuditRecords` ships in W13 migration
`Phase_K_W13_SpectatorHandoffAudit` across Sqlite, Postgres, and
SqlServer.

### §3.6 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Bishop/SpectatorHandoffAuditTests.cs`:

* Both `InMemorySpectatorHandoffAuditStore` and
  `EfSpectatorHandoffAuditStore` satisfy `ISpectatorHandoffAuditStore`.
* `InsertAsync` round-trips every column.
* `TokenJti` is the unique natural key — a second insert with the
  same JTI throws.
* `ListByGameAsync` orders rows by `IssuedAt` descending.
* `SweepExpiredAsync` drops rows older than the cutoff.
* `CountAsync` returns the total row count.
* Controller writes an audit row on a successful mint.
* Controller does NOT write an audit row when the mint fails.
* Controller swallows audit-store failures (token still returned).

## §4 — Audit query API (Phase K Wave 14)

The W13 audit trail persists every mint into
`SpectatorHandoffAuditRecords`; W14 adds the admin-facing query
endpoint that surfaces the rows for the security console.

### §4.1 — Endpoint

```
GET /api/spectator/handoff/audit
    ?gameId={guid?}
    &from={iso8601-utc?}
    &to={iso8601-utc?}
    &skip={int?}
    &limit={int?}
```

* **Auth**: admin-only. Anonymous → HTTP 401; non-admin session →
  HTTP 403 with `{ "error": "admin-required" }`.
* **Filters**: every query parameter is optional. `gameId` pins
  the per-game listing; `from` / `to` apply against `IssuedAt`.
  Bad timestamp values return HTTP 400.
* **Ordering**: `IssuedAt` descending (most-recent first).
* **Page-size**: clamped to `[1, Spectator:Audit:MaxPageSize]`.
  Default is `Spectator:Audit:PageSize` (50). Hard upper bound is
  200; larger client `limit` values are silently clamped.

### §4.2 — Response

```json
{
  "items": [
    {
      "id": "…",
      "userId": "…",
      "gameId": "…",
      "tokenJti": "…",
      "issuedAt": "…",
      "scope": "spectator:…",
      "clientIp": "…",
      "userAgent": "…"
    }
  ],
  "count": 12,
  "skip": 0,
  "limit": 50,
  "pageSize": 50
}
```

The token itself is **not** included — the audit row is for
forensic review, not token re-issuance. The `tokenJti` field is
the natural unique key from the W13 audit row and pairs with the
revocation table (forward-work).

### §4.3 — Store-unavailable shape

When the audit store is not wired (defence-in-depth — the
container always registers one of the two impls, but the
controller takes the dependency as optional so test fixtures stay
flexible), the endpoint returns HTTP 503 with
`{ "error": "audit-store-unavailable" }` rather than an empty
list. This makes a real mis-configuration loud instead of silent.

### §4.4 — Configuration

| Key                              | Default | Notes                                          |
| -------------------------------- | ------- | ---------------------------------------------- |
| `Spectator:Audit:PageSize`       | 50      | Server-side default; clamped to `[1, 200]`     |

### §4.5 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W14/Bishop/SpectatorAuditQueryEndpointTests.cs`:

* Anonymous → 401.
* Non-admin → 403.
* Admin gets the paged audit shape with `items` / `count` /
  `skip` / `limit` / `pageSize` fields.
* `from` / `to` filter the row set by `IssuedAt`.
* `gameId` filter narrows to a single game.
* Bad timestamp → 400.
* `limit` above 200 clamps to 200; below 1 clamps to 1.
