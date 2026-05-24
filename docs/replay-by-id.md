# Replay-by-id

Phase K Wave 12 — Bishop.

## Overview

`GET /api/replays/{replayId}` and `POST /api/replays` deliver durable
post-game play-by-play to Hicks's `?action=replay` URL shape.
Replays are addressed by a short, opaque `replayId` minted at ingest
time so the client URL is short and shareable without leaking the
internal `gameId`.

## Wire format

* **Synthetic id**: `r-{8 url-safe base64 chars}` — 6 random bytes
  encoded base64url-without-padding. ~2.8e14 distinct values; the
  collision probability at the configured ingest rate is negligible
  for the retention window.
* **Payload**: gzip-compressed UTF-8 JSON. The API returns the
  decompressed JSON envelope so clients never see the gzip wire.
* **Variant**: `changsha-v1` baseline; future variants persist
  verbatim so old replays continue to decode after a variant flip.

## Schema

| Column            | Type         | Notes                            |
| ----------------- | ------------ | -------------------------------- |
| `ReplayId`        | string PK    | `r-…` synthetic id               |
| `GameId`          | Guid         | Source game (indexed for W13)    |
| `CompletedAt`     | DateTime UTC | When the game finished           |
| `Variant`         | string       | Rule variant tag                 |
| `TurnCount`       | int          | Cheap meta for listings          |
| `CompressedPayload` | byte[]     | gzip-compressed JSON play-by-play |
| `IngestedAt`      | DateTime UTC | When the row was inserted        |
| `ExpiresAt`       | DateTime UTC | `CompletedAt + RetentionDays`    |

## Configuration

| Key                              | Default      | Notes                              |
| -------------------------------- | ------------ | ---------------------------------- |
| `Replays:StorageImpl`            | `InMemory`   | `"Ef"` for production              |
| `Replays:RetentionDays`          | 90           | Sweeper drops rows older than this |
| `Replays:MaxCompressedBytes`     | 8 MiB        | Hard cap on payload size           |
| `Replays:SweepIntervalHours`     | 6            | Background sweep cadence           |

## Endpoints

* `POST /api/replays` — body `{ gameId, variant, completedAt, payload }`. Returns the minted `replayId`.
* `GET /api/replays/{replayId}` — returns the decompressed JSON envelope. 404 when no row exists.

## Migration

Table `Replays` ships in W12 migration
`Phase_K_W12_Replays_Brackets_SignalRSeq` across Sqlite, Postgres,
SqlServer.

## Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W12/Bishop/ReplayStorePersistenceFacts.cs`:

* `ReplayOptions.DefaultRetentionDays == 90`.
* `ReplayOptions.MaxCompressedBytes == 8 MiB`.
* Both `InMemoryReplayStore` and `EfReplayStore` satisfy `IReplayStore`.
* `InsertAsync` mints a synthetic id when the caller leaves
  `ReplayId` blank; the minted id begins with `r-`.
* `ReplayIdGenerator.Mint()` returns a 10-character string of shape
  `r-XXXXXXXX`.
* Insert → Get round-trips the payload byte-for-byte (both stores).
* `CompressPayload` + `DecompressPayload` round-trip the JSON
  envelope.
* `InsertAsync` pins `ExpiresAt = CompletedAt + RetentionDays`.
* `SweepExpiredAsync` drops rows older than the cutoff (both
  stores).


## Admin gating on POST (Phase K Wave 13)

The W12 surface accepted `POST /api/replays` from any authenticated
caller. W13 narrows that gate so only callers with the admin role can
mint new replay rows. Read paths (`GET /api/replays/{replayId}`)
remain open to any authenticated caller — the gate is asymmetric on
purpose so spectator + post-match flows keep working without an
admin handshake.

### Configuration

| Key                              | Default | Notes                                |
| -------------------------------- | ------- | ------------------------------------ |
| `Replays:RequireAdminForPost`    | `true`  | Set `false` to revert to W12 behaviour |

### Failure shape

* Anonymous caller → HTTP 401.
* Authenticated non-admin caller → HTTP 403 with a structured
  `code = "replay.post.admin_required"` envelope.
* Admin caller → unchanged W12 mint flow.

### Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Bishop/ReplayPostAdminGateTests.cs`:

* `ReplayOptions.RequireAdminForPost` defaults to `true`.
* POST without auth returns 401.
* POST with a non-admin principal returns 403.
* POST with an admin principal completes the mint as in W12.
* Toggling `RequireAdminForPost = false` reverts to W12 behaviour
  (any authenticated caller can POST).
