# Replay blob streaming

> **Phase K Wave 15 — Bishop.** Byte-level streaming surface for
> the replay-by-id store. Pairs with the W12 metadata-only `GET`
> + W14 listing to give callers a resumable download path for the
> decompressed JSON play-by-play. See `docs/replay-by-id.md` for
> the metadata surface and `docs/bracket-shape.md §6` for the
> latency histogram that observes this endpoint.

## §1 — Why a separate endpoint?

The W12 `GET /api/replays/{replayId}` returns a structured JSON
envelope with the decompressed payload reified as a nested
object. That shape is convenient for an SDK consumer that wants
typed access to a 16-hand championship game but **forces the
client to buffer the entire payload** (~1 MB worst case)
before the parser can start. Tournament dashboards downloading
many replays back-to-back cannot stream the parse.

`GET /api/replays/{replayId}/blob` returns the same payload as
`application/octet-stream` bytes. The endpoint:

* Honours RFC 7233 single-range `Range: bytes=<start>-<end>`,
  `bytes=<start>-`, and suffix `bytes=-<N>` requests so resumed
  downloads work against the same byte offsets a fresh GET
  would see.
* Stamps `Content-Length` + `Accept-Ranges: bytes` so
  well-behaved clients can advertise resumability up-front.
* Returns `206 Partial Content` for ranged responses and
  `416 Range Not Satisfiable` for malformed / multi-range
  requests (multipart/byteranges is intentionally not
  supported).
* Stamps `X-Replay-Id` + `X-Replay-Variant` response headers so
  the caller can trace the underlying row without re-fetching
  the JSON envelope.

## §2 — Wire shape

```
GET /api/replays/{replayId}/blob
GET /api/replays/{replayId}/blob   Range: bytes=0-99
GET /api/replays/{replayId}/blob   Range: bytes=-256
GET /api/replays/{replayId}/blob   Range: bytes=128-
```

Responses:

| Status | When                                       | Body / headers           |
| ------ | ------------------------------------------ | ------------------------ |
| 200    | Full GET, no Range header                  | Full decompressed bytes  |
| 206    | Valid single-range request                 | Sliced bytes + `Content-Range: bytes <s>-<e>/<total>` |
| 404    | No row for `replayId`                      | `{ "error": "replay-not-found", "replayId": "…" }` |
| 416    | Malformed / multi-range / out-of-range     | `Content-Range: bytes */<total>` |
| 500    | Stored payload fails decompression         | `{ "error": "payload-decompression-failed", "replayId": "…" }` |

## §3 — Range parsing

The single-range parser lives in `ReplayController.TryParseSingleByteRange`.
It accepts the canonical RFC 7233 shapes:

* `bytes=0-9` — first 10 bytes.
* `bytes=10-` — from offset 10 to end-of-file.
* `bytes=-5` — last 5 bytes (suffix range).

Any of the following return false → 416:

* Multi-range (`bytes=0-1,3-4`).
* Non-`bytes` unit (`items=0-10`).
* Inverted range (`bytes=50-10`).
* Start at or beyond end-of-file.
* Empty / malformed value.

End offsets greater than the payload length are silently
clamped to `length - 1` rather than rejected — this matches the
common client behaviour of requesting a generous chunk size.

## §4 — Configuration

No new toggles. The endpoint reuses `Replays:MaxCompressedBytes`
to bound the decompressed buffer worst-case
(~64 MB at the documented 8 MB compressed ceiling).

## §5 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Bishop/ReplayBlobStreamingEndpointTests.cs`:

* 404 for unknown replay.
* 200 + `Accept-Ranges: bytes` + `Content-Length` for a full GET.
* `application/octet-stream` content type.
* 206 + `Content-Range` for a `bytes=0-9` request.
* Suffix `bytes=-N` returns the last N bytes.
* Open-ended `bytes=N-` returns from offset N.
* Multi-range / malformed → 416.
* `X-Replay-Id` + `X-Replay-Variant` headers stamped.
* `TryParseSingleByteRange` rejects multi-range, inverted ranges,
  non-`bytes` units, zero-length payloads.
* `TryParseSingleByteRange` clamps end offsets greater than the
  payload length.
