# Spectator Livestream (Phase K Wave 2 stub)

Phase K Wave 2 reserves the `/api/replay/{id}/livestream.m3u8` route as a
stub.  The full HLS pipeline lands in Phase L.

## Current behaviour (Wave 2)

```
GET /api/replay/{replayId}/livestream.m3u8
→ 404 Not Found
  Content-Type: application/json
  {
    "error": "spectator-livestream-not-implemented",
    "replayId": "<the supplied id>",
    "message": "HLS livestream lands in Phase L; this endpoint is reserved."
  }
```

The route is wired into the API rate-limit policy (`ApiPolicy`) so a
runaway poller can't drown the service.  The response is deliberately
JSON (not HLS playlist text) so a misconfigured client crashes loudly
rather than silently treating the empty body as a valid playlist.

## Wave-2 server-side seam

`SpectatorService` ships today with:

- `NotImplementedEnvelope(replayId)` — the canonical 404 payload shape
  consumed by the route handler.
- `ShouldEmitTileFlip()` — 30 Hz token-bucket debounce used by the
  future runtime hook `EmitTileFlippedAsync`.  The constant
  `MaxTileFlipsPerSecond = 30` matches the brief's "≤30 Hz" cap.

The service is registered as a singleton in `Program.cs` so a future
hook from `ChangshaGameRuntime` can resolve it without re-plumbing DI.

## Wave-3+ plan

1. Tile-flip events fan into `SpectatorService.EmitTileFlippedAsync`,
   debounced through `ShouldEmitTileFlip`.
2. A background encoder service consumes the debounced stream, renders
   one frame per tile flip into a fixed canvas, and feeds an FFmpeg
   pipe.
3. FFmpeg emits a rolling HLS playlist + segment files into the
   replay storage volume.
4. The `/api/replay/{id}/livestream.m3u8` route flips from stub to
   serving the live playlist; segments stream under
   `/api/replay/{id}/livestream/{seq}.ts`.
5. CDN cache: live segments use `Cache-Control: no-cache`; the
   eventual VOD playlist (`/livestream.vod.m3u8`) gets a long TTL.

Until Phase L lands, clients SHOULD treat a 404 from this route as a
signal that livestream is disabled, fall back to the existing replay
JSON, and surface a "live spectator coming soon" affordance.
