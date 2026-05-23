using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mahjong.Autotable.Api.Replays;

/// <summary>
/// Phase K Wave 12 — Bishop. REST surface for the replay-by-id
/// store. Hicks's W12 <c>?action=replay</c> client URL fetches a
/// replay envelope from <c>GET /api/replays/{replayId}</c>; the
/// game-over runtime hook (W13 forward) will <c>POST</c> the
/// payload at completion time. For W12 the POST is a public
/// endpoint accepting raw payloads — production deployments
/// gate it via the API gateway / network policy.
///
/// <list type="bullet">
///   <item><c>GET /api/replays/{replayId}</c> — returns the
///         metadata envelope + the decompressed JSON
///         play-by-play. 404 when no row exists.</item>
///   <item><c>POST /api/replays</c> — accepts the JSON envelope
///         <c>{ gameId, completedAt, variant, turnCount, payload }</c>
///         and returns <c>{ replayId, ingestedAt, expiresAt }</c>.
///         The server gzip-compresses the payload at ingest.</item>
/// </list>
///
/// <para>Toggle: <see cref="ReplayOptions.StorageImpl"/>
/// (<c>"InMemory"</c> default for tests / <c>"Ef"</c> for prod).
/// See <c>docs/replay-by-id.md</c>.</para>
/// </summary>
[ApiController]
[Route("api/replays")]
public sealed class ReplayController : ControllerBase
{
    private readonly IReplayStore _store;
    private readonly ReplayOptions _options;
    private readonly ILogger<ReplayController> _logger;

    public ReplayController(
        IReplayStore store,
        ReplayOptions options,
        ILogger<ReplayController> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetch a single replay by id. Returns the metadata
    /// envelope plus the decompressed JSON play-by-play. 404
    /// when no row exists.
    /// </summary>
    [HttpGet("{replayId}")]
    [EnableRateLimiting(RateLimiting.RateLimitingExtensions.ApiPolicy)]
    public async Task<IActionResult> Get(string replayId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(replayId))
        {
            return BadRequest(new { error = "replayId is required." });
        }
        var row = await _store.GetAsync(replayId, ct);
        if (row is null)
        {
            return NotFound(new { error = "replay-not-found", replayId });
        }
        var payloadJson = row.DecompressPayload();
        // Phase K Wave 12 — Bishop. Wire shape mirrors the
        // existing ChangshaReplayController contract: metadata
        // fields camelCased + the JSON payload reified as a
        // nested object (not an opaque string) so consumers see
        // a structured shape directly.
        object payloadNode;
        try
        {
            payloadNode = string.IsNullOrWhiteSpace(payloadJson)
                ? new { }
                : JsonSerializer.Deserialize<JsonElement>(payloadJson);
        }
        catch (JsonException)
        {
            payloadNode = new { raw = payloadJson };
        }
        return Ok(new
        {
            replayId = row.ReplayId,
            gameId = row.GameId,
            completedAt = row.CompletedAt,
            variant = row.Variant,
            turnCount = row.TurnCount,
            ingestedAt = row.IngestedAt,
            expiresAt = row.ExpiresAt,
            payload = payloadNode,
        });
    }

    /// <summary>
    /// Insert a new replay record. The server mints
    /// <c>replayId</c> + <c>ingestedAt</c> + <c>expiresAt</c>
    /// and gzip-compresses the payload before persistence.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimiting.RateLimitingExtensions.ApiPolicy)]
    public async Task<IActionResult> Post([FromBody] PostReplayBody? body, CancellationToken ct = default)
    {
        if (body is null)
        {
            return BadRequest(new { error = "body is required." });
        }
        if (body.GameId == Guid.Empty)
        {
            return BadRequest(new { error = "gameId is required." });
        }
        var payloadJson = body.Payload is null
            ? "{}"
            : body.Payload.Value.GetRawText();
        if (payloadJson.Length > _options.MaxCompressedBytes * 8) // soft upper bound on uncompressed
        {
            return BadRequest(new { error = "payload-too-large" });
        }
        var compressed = ReplayRecord.CompressPayload(payloadJson);
        if (compressed.Length > _options.MaxCompressedBytes)
        {
            return BadRequest(new { error = "payload-too-large" });
        }
        var completedAt = body.CompletedAt == default ? DateTime.UtcNow : body.CompletedAt.ToUniversalTime();
        var record = new ReplayRecord
        {
            ReplayId = string.IsNullOrWhiteSpace(body.ReplayId) ? string.Empty : body.ReplayId!.Trim(),
            GameId = body.GameId,
            CompletedAt = completedAt,
            Variant = string.IsNullOrWhiteSpace(body.Variant) ? "changsha-v1" : body.Variant!.Trim(),
            TurnCount = Math.Max(0, body.TurnCount),
            CompressedPayload = compressed,
        };
        var stored = await _store.InsertAsync(record, ct);
        return CreatedAtAction(nameof(Get), new { replayId = stored.ReplayId }, new
        {
            replayId = stored.ReplayId,
            gameId = stored.GameId,
            ingestedAt = stored.IngestedAt,
            expiresAt = stored.ExpiresAt,
        });
    }

    /// <summary>
    /// Phase K Wave 12 — Bishop. POST body envelope. The
    /// payload field is a free-form JSON object — typically the
    /// canonical Changsha play-by-play emitted by
    /// <c>ChangshaGameRuntime.EmitGameCompletedAsync</c>.
    /// </summary>
    public sealed class PostReplayBody
    {
        /// <summary>Optional client-supplied replay id. Empty
        /// → server-minted. Mostly useful for replay tests that
        /// need a deterministic id.</summary>
        public string? ReplayId { get; set; }

        /// <summary>Originating game id. Required.</summary>
        public Guid GameId { get; set; }

        /// <summary>UTC timestamp when the game completed.
        /// Empty → server stamps <c>DateTime.UtcNow</c>.</summary>
        public DateTime CompletedAt { get; set; }

        /// <summary>Rule variant (e.g. <c>"changsha-v1"</c>).
        /// Empty → defaults to <c>"changsha-v1"</c>.</summary>
        public string? Variant { get; set; }

        /// <summary>Turn count for the encoded play-by-play.
        /// Surfaced in the GET response so listing endpoints
        /// can show a digest without pulling the
        /// payload.</summary>
        public int TurnCount { get; set; }

        /// <summary>The play-by-play JSON object. Compressed
        /// at ingest.</summary>
        public JsonElement? Payload { get; set; }
    }
}
