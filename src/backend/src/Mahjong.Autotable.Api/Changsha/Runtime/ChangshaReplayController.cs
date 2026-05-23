using System.Text.Json;
using Mahjong.Autotable.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Changsha.Runtime;

/// <summary>
/// Phase J Wave 7 — REST surface for completed-game replay snapshots.
///
/// <para><b>GET /api/games/{gameId}/replay</b> — returns the canonical
/// play-by-play for a completed Changsha game. Returns <c>404</c> when no
/// replay snapshot exists for the supplied id (either an unknown game id
/// or a game still in progress — Wave 7 persists the snapshot at
/// <c>GameCompleted</c> emission only).</para>
///
/// <para>Response shape on <c>200</c>:</para>
/// <code>
/// {
///   "gameId": "9b3a7f01-…",
///   "createdAt": "2026-05-23T04:32:11.812Z",
///   "events": [
///     {
///       "turn": 1,
///       "phase": "Setup",
///       "actor": -1,
///       "action": "game-created",
///       "tilesJson": "[]",
///       "timestampUtc": "2026-05-23T04:21:00.000Z"
///     },
///     {
///       "turn": 4,
///       "phase": "Discard",
///       "actor": 2,
///       "action": "tile-discarded",
///       "tilesJson": "[47]",
///       "timestampUtc": "2026-05-23T04:21:18.402Z"
///     }
///     // …one entry per ChangshaGameState.EventLog entry, ordered by
///     // sequence (insertion order on the runtime — chronological).
///   ]
/// }
/// </code>
///
/// <para><b>Rate limit.</b> The endpoint is large by nature (an end-game
/// replay can run into hundreds of KB) and read-only, so it explicitly
/// opts into the <c>token-bucket-api</c> policy (Phase J Wave 6) which
/// caps per-IP bursts at the same level as the rest of the
/// <c>/api/**</c> surface.</para>
/// </summary>
[ApiController]
[Route("api/games")]
public sealed class ChangshaReplayController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ChangshaReplayController> _logger;

    public ChangshaReplayController(AppDbContext db, ILogger<ChangshaReplayController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns the persisted replay snapshot for <paramref name="gameId"/>,
    /// or <c>404</c> when no row exists. <c>gameId</c> must parse as a
    /// <see cref="Guid"/>; malformed ids return <c>400</c>.
    ///
    /// <para>The body's <c>events</c> field is materialised from the
    /// stored <c>EventsJson</c> string so the wire shape is JSON-native
    /// (consumers see a structured array, not an encoded string).
    /// <c>tilesJson</c> inside each event remains a string per the
    /// agreed wire contract.</para>
    /// </summary>
    [HttpGet("{gameId}/replay")]
    [EnableRateLimiting(RateLimiting.RateLimitingExtensions.ApiPolicy)]
    public async Task<IActionResult> Get(string gameId, CancellationToken ct = default)
    {
        if (!Guid.TryParse(gameId, out var gameGuid))
        {
            return BadRequest(new { error = "gameId must be a GUID." });
        }

        var row = await _db.ChangshaGameReplays
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.GameId == gameGuid, ct);

        if (row is null)
        {
            return NotFound(new { error = "Replay not found.", gameId });
        }

        // Materialise EventsJson back into a JSON node so the response is
        // structured, then normalise the order: the wire contract guarantees
        // events arrive sorted by `turn` ascending (stable on ties — the
        // serialisation sequence is the tiebreaker). Vasquez's
        // GameReplayEndpointTests.GameReplay_Events_AreOrderedByTurnAscending
        // pins this contract: even if a row is seeded with an out-of-order
        // `EventsJson` (admin import, partial replay merge), the endpoint
        // must hand the frontend scrubber a monotonic turn sequence.
        //
        // If the stored payload is malformed (it shouldn't be — we own the
        // writer) we fall back to surfacing the raw string so the client at
        // least gets the data.
        object events;
        try
        {
            using var doc = JsonDocument.Parse(row.EventsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var sorted = doc.RootElement.EnumerateArray()
                    .Select((el, idx) => (
                        Element: el.Clone(),
                        Turn: el.TryGetProperty("turn", out var t) && t.ValueKind == JsonValueKind.Number
                            ? t.GetInt32()
                            : int.MaxValue,
                        Order: idx))
                    .OrderBy(x => x.Turn)
                    .ThenBy(x => x.Order)
                    .Select(x => x.Element)
                    .ToArray();
                events = sorted;
            }
            else
            {
                events = doc.RootElement.Clone();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Replay row {ReplayId} for game {GameId} has malformed EventsJson.", row.Id, gameId);
            events = row.EventsJson;
        }

        return Ok(new
        {
            gameId = gameGuid,
            createdAt = row.CreatedAt,
            events,
        });
    }
}
