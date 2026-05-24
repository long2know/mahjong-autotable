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
