using System.Collections.Concurrent;
using System.Security.Cryptography;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mahjong.Autotable.Api.Replays;

/// <summary>
/// Phase K Wave 23 — Bishop. In-memory chunk-upload staging
/// buffer. Counterpart to the W22 chunked-DOWNLOAD surface
/// (<see cref="ReplayChunksController"/>) — operators uploading
/// a large historical replay can stream the gzip payload in
/// fixed-size slices and assemble at the end so a single
/// flaky disconnect doesn't force a full re-upload.
///
/// <para>The staging area is a per-replay <c>SortedDictionary&lt;int, byte[]&gt;</c>
/// keyed by the upload <c>seq</c> integer (1-based; gaps are
/// allowed mid-upload but the finalize call refuses a payload
/// that has any gap in [1..MaxSeq]). Each chunk write replaces
/// any prior buffer at the same <c>seq</c>, so resume-from-N
/// works without an explicit DELETE.</para>
///
/// <para>The buffer is process-local — multi-replica
/// deployments need session-affinity for an upload session, or
/// an alternative shared-store implementation (a future wave
/// can swap in a Redis-backed buffer behind the same interface
/// without changing the controller surface).</para>
/// </summary>
public sealed class ReplayChunkUploadBuffer
{
    public const int DefaultMaxChunkBytes = 4 * 1024 * 1024;
    public const int DefaultMaxTotalChunks = 1024;

    private sealed class Session
    {
        public SortedDictionary<int, byte[]> Chunks { get; } = new();
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
        public object Lock { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Session> _sessions =
        new(StringComparer.Ordinal);

    /// <summary>Writes a single chunk slice. Replaces any
    /// existing buffer at the same <paramref name="seq"/>.
    /// Returns the per-session aggregate state for the
    /// caller's response.</summary>
    public ChunkWriteState Write(string replayId, int seq, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayId);
        if (seq < 1) throw new ArgumentOutOfRangeException(nameof(seq));
        ArgumentNullException.ThrowIfNull(payload);

        var session = _sessions.GetOrAdd(replayId, _ => new Session());
        lock (session.Lock)
        {
            session.Chunks[seq] = payload;
            session.LastUpdatedUtc = DateTime.UtcNow;
            return new ChunkWriteState(
                replayId,
                Seq: seq,
                ChunkCount: session.Chunks.Count,
                MaxSeqObserved: session.Chunks.Keys.Max(),
                TotalBytes: session.Chunks.Values.Sum(b => (long)b.Length));
        }
    }

    /// <summary>Assembles the buffered chunks in sequence
    /// order. Throws if any chunk is missing in [1..maxSeq].
    /// Removes the session on success.</summary>
    public byte[] Assemble(string replayId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replayId);
        if (!_sessions.TryGetValue(replayId, out var session))
        {
            throw new InvalidOperationException(
                $"No upload session staged for replay '{replayId}'.");
        }
        lock (session.Lock)
        {
            if (session.Chunks.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Upload session for '{replayId}' has zero chunks.");
            }
            var maxSeq = session.Chunks.Keys.Max();
            for (var i = 1; i <= maxSeq; i++)
            {
                if (!session.Chunks.ContainsKey(i))
                {
                    throw new InvalidOperationException(
                        $"Upload session for '{replayId}' is missing chunk {i} (max={maxSeq}).");
                }
            }
            var totalLength = session.Chunks.Values.Sum(b => (long)b.Length);
            var buffer = new byte[totalLength];
            var offset = 0;
            foreach (var kv in session.Chunks)
            {
                Buffer.BlockCopy(kv.Value, 0, buffer, offset, kv.Value.Length);
                offset += kv.Value.Length;
            }
            _sessions.TryRemove(replayId, out _);
            return buffer;
        }
    }

    /// <summary>Inspect the per-session state without
    /// mutating it (used by the finalize controller's "this
    /// upload doesn't exist" branch and by tests). Returns null
    /// when no session is staged.</summary>
    public ChunkWriteState? Inspect(string replayId)
    {
        if (string.IsNullOrWhiteSpace(replayId)) return null;
        if (!_sessions.TryGetValue(replayId, out var session)) return null;
        lock (session.Lock)
        {
            if (session.Chunks.Count == 0) return null;
            return new ChunkWriteState(
                replayId,
                Seq: session.Chunks.Keys.Max(),
                ChunkCount: session.Chunks.Count,
                MaxSeqObserved: session.Chunks.Keys.Max(),
                TotalBytes: session.Chunks.Values.Sum(b => (long)b.Length));
        }
    }

    /// <summary>Drop a staged session (called by the
    /// finalize-cleanup path on failure or by an admin
    /// abort).</summary>
    public bool Abort(string replayId)
    {
        if (string.IsNullOrWhiteSpace(replayId)) return false;
        return _sessions.TryRemove(replayId, out _);
    }

    /// <summary>Currently-tracked session count — surfaced
    /// for the admin diagnostic view + tests.</summary>
    public int ActiveSessionCount => _sessions.Count;

    /// <summary>Reset all sessions (tests).</summary>
    public void Clear() => _sessions.Clear();
}

/// <summary>
/// Phase K Wave 23 — Bishop. Per-write response payload.
/// Wire-stable so the operator dashboard can render the
/// upload progress.
/// </summary>
public sealed record ChunkWriteState(
    string ReplayId,
    int Seq,
    int ChunkCount,
    int MaxSeqObserved,
    long TotalBytes);

/// <summary>
/// Phase K Wave 23 — Bishop. Admin-gated chunked-UPLOAD
/// surface — the W23 counterpart to W22's chunked-DOWNLOAD.
///
/// <para><b>Endpoints.</b>
/// <list type="bullet">
///   <item><c>POST /api/replays/{replayId}/chunks/{seq}</c> —
///         appends a single chunk. Body is binary
///         (<c>application/octet-stream</c>). The optional
///         <c>Content-Range</c> header is recorded but the
///         server treats <c>seq</c> as authoritative; the
///         header is surfaced in the response for
///         observability.</item>
///   <item><c>POST /api/replays/{replayId}/finalize</c> —
///         assembles the staged chunks, verifies the
///         supplied <c>X-Replay-Checksum</c> SHA-256
///         (when present) against the decompressed payload,
///         persists via <see cref="IReplayStore.InsertAsync"/>,
///         and returns the canonical <c>ETag</c>.</item>
/// </list>
/// </para>
///
/// <para><b>Auth.</b> 401 / 403 / 400 / 404 / 200 / 201.
/// Mandatory admin role.</para>
/// </summary>
[ApiController]
[Route("api/replays/{replayId}")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class ReplayChunkUploadController : ControllerBase
{
    public const int MinChunkBytes = 1;
    public const int MaxChunkBytes = 4 * 1024 * 1024;
    public const int MaxChunksPerSession = 1024;
    public const int MaxAggregateBytes = 64 * 1024 * 1024;

    public const string ChecksumHeader = "X-Replay-Checksum";
    public const string ContentRangeHeader = "Content-Range";

    public const string ErrorReplayIdRequired = "replay-id-required";
    public const string ErrorInvalidSeq = "invalid-seq";
    public const string ErrorEmptyBody = "empty-body";
    public const string ErrorChunkTooLarge = "chunk-too-large";
    public const string ErrorTooManyChunks = "too-many-chunks";
    public const string ErrorTooLarge = "aggregate-too-large";
    public const string ErrorNoStagedChunks = "no-staged-chunks";
    public const string ErrorChunkGap = "chunk-gap";
    public const string ErrorChecksumMismatch = "checksum-mismatch";
    public const string ErrorInvalidChecksum = "invalid-checksum";

    private readonly AuthCookieService _cookies;
    private readonly ReplayChunkUploadBuffer _buffer;
    private readonly IReplayStore _store;

    public ReplayChunkUploadController(
        AuthCookieService cookies,
        ReplayChunkUploadBuffer buffer,
        IReplayStore store)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    [HttpPost("chunks/{seq:int}")]
    public async Task<IActionResult> UploadChunk(
        [FromRoute] string replayId,
        [FromRoute] int seq,
        CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "session-required" });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "admin-required" });
        }
        if (string.IsNullOrWhiteSpace(replayId))
        {
            return BadRequest(new { error = ErrorReplayIdRequired });
        }
        if (seq < 1 || seq > MaxChunksPerSession)
        {
            return BadRequest(new { error = ErrorInvalidSeq, seq, maximum = MaxChunksPerSession });
        }

        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var payload = ms.ToArray();
        if (payload.Length < MinChunkBytes)
        {
            return BadRequest(new { error = ErrorEmptyBody });
        }
        if (payload.Length > MaxChunkBytes)
        {
            return BadRequest(new { error = ErrorChunkTooLarge, length = payload.Length, maximum = MaxChunkBytes });
        }

        var state = _buffer.Write(replayId, seq, payload);
        if (state.ChunkCount > MaxChunksPerSession)
        {
            _buffer.Abort(replayId);
            return BadRequest(new { error = ErrorTooManyChunks, maximum = MaxChunksPerSession });
        }
        if (state.TotalBytes > MaxAggregateBytes)
        {
            _buffer.Abort(replayId);
            return BadRequest(new { error = ErrorTooLarge, totalBytes = state.TotalBytes, maximum = MaxAggregateBytes });
        }

        var contentRange = Request.Headers.TryGetValue(ContentRangeHeader, out var crv)
            ? crv.ToString()
            : null;
        return StatusCode(StatusCodes.Status201Created, new
        {
            replayId,
            seq,
            chunkCount = state.ChunkCount,
            maxSeqObserved = state.MaxSeqObserved,
            totalBytes = state.TotalBytes,
            contentRange,
        });
    }

    [HttpPost("finalize")]
    public async Task<IActionResult> Finalize(
        [FromRoute] string replayId,
        [FromQuery] Guid? gameId,
        [FromQuery] string? variant,
        [FromQuery] int? turnCount,
        CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "session-required" });
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "admin-required" });
        }
        if (string.IsNullOrWhiteSpace(replayId))
        {
            return BadRequest(new { error = ErrorReplayIdRequired });
        }

        var inspect = _buffer.Inspect(replayId);
        if (inspect is null)
        {
            return NotFound(new { error = ErrorNoStagedChunks, replayId });
        }

        byte[] assembled;
        try
        {
            assembled = _buffer.Assemble(replayId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ErrorChunkGap, message = ex.Message, replayId });
        }

        var hash = SHA256.HashData(assembled);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        var headerHex = Request.Headers.TryGetValue(ChecksumHeader, out var checksumValues)
            ? checksumValues.ToString().Trim().ToLowerInvariant()
            : null;
        if (!string.IsNullOrEmpty(headerHex))
        {
            // Accept the bare hex or a quoted "sha256-<hex>" envelope
            // (the W22 download surface emits the strong form).
            var normalised = headerHex.Trim('"');
            if (normalised.StartsWith("sha256-", StringComparison.OrdinalIgnoreCase))
            {
                normalised = normalised.Substring("sha256-".Length);
            }
            if (normalised.Length != 64 || !IsHex(normalised))
            {
                return BadRequest(new { error = ErrorInvalidChecksum, header = ChecksumHeader });
            }
            if (!string.Equals(normalised, hex, StringComparison.Ordinal))
            {
                _buffer.Abort(replayId);
                return BadRequest(new
                {
                    error = ErrorChecksumMismatch,
                    expected = normalised,
                    actual = hex,
                });
            }
        }

        // Re-encode the assembled bytes through the canonical
        // compressor so the stored payload uses the same gzip
        // envelope downstream surfaces expect.
        var json = System.Text.Encoding.UTF8.GetString(assembled);
        var record = new ReplayRecord
        {
            ReplayId = replayId,
            GameId = gameId ?? Guid.Empty,
            Variant = string.IsNullOrWhiteSpace(variant) ? "changsha-v1" : variant!,
            TurnCount = turnCount ?? 0,
            CompletedAt = DateTime.UtcNow,
            IngestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            CompressedPayload = ReplayRecord.CompressPayload(json),
        };
        var stored = await _store.InsertAsync(record, ct);
        var etag = $"\"sha256-{hex}\"";
        Response.Headers.ETag = etag;
        return Ok(new
        {
            replayId = stored.ReplayId,
            gameId = stored.GameId,
            variant = stored.Variant,
            turnCount = stored.TurnCount,
            totalBytes = assembled.Length,
            sha256 = hex,
            etag,
            completedAt = stored.CompletedAt,
        });
    }

    internal static bool IsHex(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var ch in s)
        {
            var ok = (ch >= '0' && ch <= '9')
                || (ch >= 'a' && ch <= 'f')
                || (ch >= 'A' && ch <= 'F');
            if (!ok) return false;
        }
        return true;
    }
}
