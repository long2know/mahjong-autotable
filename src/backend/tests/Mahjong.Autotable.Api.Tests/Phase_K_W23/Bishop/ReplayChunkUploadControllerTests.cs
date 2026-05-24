using System.Security.Cryptography;
using System.Text;
using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Replays;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Tests for the W23 admin-gated
/// chunked-upload endpoint: auth gate, per-chunk write
/// semantics, gap detection, checksum verification,
/// finalize-into-store flow.
/// </summary>
public sealed class ReplayChunkUploadControllerTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly string _dbPath;
    private readonly InMemoryReplayStore _store = new();
    private readonly ReplayChunkUploadBuffer _buffer = new();

    public ReplayChunkUploadControllerTests()
    {
        var scratch = Path.Combine(AppContext.BaseDirectory, "bishop-w23-upload-sqlite");
        Directory.CreateDirectory(scratch);
        _dbPath = Path.Combine(scratch, $"upload-{Guid.NewGuid():N}.sqlite");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _sp = services.BuildServiceProvider();
        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _sp.Dispose();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<(AuthCookieService, HttpContext)> MakeSessionAsync(string role = "admin")
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var issue = new DefaultHttpContext();
        var s = await cookies.IssueAsync(issue, $"p-{Guid.NewGuid():N}", Guid.NewGuid(), role);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Cookie"] = $"{AuthCookieService.CookieName}={s.Token}";
        return (cookies, ctx);
    }

    private ReplayChunkUploadController MakeController(HttpContext ctx, AuthCookieService cookies)
    {
        var c = new ReplayChunkUploadController(cookies, _buffer, _store);
        c.ControllerContext = new ControllerContext { HttpContext = ctx };
        return c;
    }

    private static void SetBody(HttpContext ctx, byte[] payload)
    {
        ctx.Request.Body = new MemoryStream(payload);
        ctx.Request.ContentLength = payload.Length;
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_NoSession_Returns401()
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var c = MakeController(new DefaultHttpContext(), cookies);
        SetBody(c.HttpContext, new byte[] { 1, 2, 3 });
        var r = await c.UploadChunk("r1", 1, CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_NonAdmin_Returns403()
    {
        var (cookies, ctx) = await MakeSessionAsync(role: "player");
        SetBody(ctx, new byte[] { 1, 2 });
        var r = await MakeController(ctx, cookies).UploadChunk("r1", 1, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(r).StatusCode);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_EmptyReplayId_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        SetBody(ctx, new byte[] { 1 });
        var r = await MakeController(ctx, cookies).UploadChunk(" ", 1, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_NegativeSeq_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        SetBody(ctx, new byte[] { 1 });
        var r = await MakeController(ctx, cookies).UploadChunk("r1", 0, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_EmptyBody_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        SetBody(ctx, Array.Empty<byte>());
        var r = await MakeController(ctx, cookies).UploadChunk("r1", 1, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_Valid_Returns201AndStages()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        SetBody(ctx, Encoding.UTF8.GetBytes("hello"));
        var r = await MakeController(ctx, cookies).UploadChunk("r-stage1", 1, CancellationToken.None);
        var obj = Assert.IsType<ObjectResult>(r);
        Assert.Equal(StatusCodes.Status201Created, obj.StatusCode);
        Assert.NotNull(_buffer.Inspect("r-stage1"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Upload_MultipleChunks_AggregateInOrder()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        for (int i = 1; i <= 3; i++)
        {
            ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($"chunk{i};"));
            ctx.Request.ContentLength = ctx.Request.Body.Length;
            var r = await MakeController(ctx, cookies).UploadChunk("r-multi", i, CancellationToken.None);
            Assert.IsType<ObjectResult>(r);
        }
        var inspect = _buffer.Inspect("r-multi");
        Assert.NotNull(inspect);
        Assert.Equal(3, inspect!.ChunkCount);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_NoSession_Returns401()
    {
        var cookies = new AuthCookieService(_sp.GetRequiredService<IServiceScopeFactory>(), new AuthOptions());
        var c = MakeController(new DefaultHttpContext(), cookies);
        var r = await c.Finalize("r1", null, null, null, CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_NonAdmin_Returns403()
    {
        var (cookies, ctx) = await MakeSessionAsync(role: "player");
        var r = await MakeController(ctx, cookies).Finalize("r1", null, null, null, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(r).StatusCode);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_NoStagedChunks_Returns404()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var r = await MakeController(ctx, cookies).Finalize("nope", null, null, null, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_GapBetweenChunks_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        // Stage seq=1 + seq=3 (no seq=2).
        ctx.Request.Body = new MemoryStream(new byte[] { 1, 2 }); ctx.Request.ContentLength = 2;
        await MakeController(ctx, cookies).UploadChunk("rgap", 1, CancellationToken.None);
        ctx.Request.Body = new MemoryStream(new byte[] { 3, 4 }); ctx.Request.ContentLength = 2;
        await MakeController(ctx, cookies).UploadChunk("rgap", 3, CancellationToken.None);
        var r = await MakeController(ctx, cookies).Finalize("rgap", null, null, null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_ValidUpload_StoresRecord()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var payload = Encoding.UTF8.GetBytes("{\"hand\":1}");
        ctx.Request.Body = new MemoryStream(payload);
        ctx.Request.ContentLength = payload.Length;
        await MakeController(ctx, cookies).UploadChunk("r-store", 1, CancellationToken.None);

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["Cookie"] = ctx.Request.Headers["Cookie"]!;
        var r = await MakeController(ctx2, cookies).Finalize("r-store", Guid.NewGuid(), "changsha-v1", 5, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(r);
        Assert.NotNull(ok.Value);
        var stored = await _store.GetAsync("r-store");
        Assert.NotNull(stored);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_ChecksumHeaderMatches_Succeeds()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var payload = Encoding.UTF8.GetBytes("{\"foo\":42}");
        var hex = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        ctx.Request.Body = new MemoryStream(payload);
        ctx.Request.ContentLength = payload.Length;
        await MakeController(ctx, cookies).UploadChunk("r-ck", 1, CancellationToken.None);

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["Cookie"] = ctx.Request.Headers["Cookie"]!;
        ctx2.Request.Headers[ReplayChunkUploadController.ChecksumHeader] = hex;
        var r = await MakeController(ctx2, cookies).Finalize("r-ck", null, null, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_ChecksumMismatch_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        var payload = Encoding.UTF8.GetBytes("real-data");
        ctx.Request.Body = new MemoryStream(payload);
        ctx.Request.ContentLength = payload.Length;
        await MakeController(ctx, cookies).UploadChunk("r-cmm", 1, CancellationToken.None);

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["Cookie"] = ctx.Request.Headers["Cookie"]!;
        ctx2.Request.Headers[ReplayChunkUploadController.ChecksumHeader] =
            new string('0', 64);
        var r = await MakeController(ctx2, cookies).Finalize("r-cmm", null, null, null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public async Task Finalize_InvalidChecksumFormat_Returns400()
    {
        var (cookies, ctx) = await MakeSessionAsync();
        ctx.Request.Body = new MemoryStream(new byte[] { 9 }); ctx.Request.ContentLength = 1;
        await MakeController(ctx, cookies).UploadChunk("r-cif", 1, CancellationToken.None);

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["Cookie"] = ctx.Request.Headers["Cookie"]!;
        ctx2.Request.Headers[ReplayChunkUploadController.ChecksumHeader] = "not-hex-and-not-64";
        var r = await MakeController(ctx2, cookies).Finalize("r-cif", null, null, null, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(r);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buffer_Write_AllowsSeqReplacement()
    {
        var buf = new ReplayChunkUploadBuffer();
        buf.Write("rid", 1, new byte[] { 1, 2 });
        buf.Write("rid", 1, new byte[] { 3, 4, 5 });
        var s = buf.Inspect("rid");
        Assert.NotNull(s);
        Assert.Equal(1, s!.ChunkCount);
        Assert.Equal(3L, s.TotalBytes);
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buffer_Assemble_NoChunks_Throws()
    {
        var buf = new ReplayChunkUploadBuffer();
        Assert.Throws<InvalidOperationException>(() => buf.Assemble("missing"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buffer_Assemble_WithGap_Throws()
    {
        var buf = new ReplayChunkUploadBuffer();
        buf.Write("rid", 1, new byte[] { 1 });
        buf.Write("rid", 3, new byte[] { 3 });
        Assert.Throws<InvalidOperationException>(() => buf.Assemble("rid"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buffer_Assemble_Success_ClearsSession()
    {
        var buf = new ReplayChunkUploadBuffer();
        buf.Write("rid", 1, new byte[] { 1 });
        buf.Write("rid", 2, new byte[] { 2, 3 });
        var assembled = buf.Assemble("rid");
        Assert.Equal(3, assembled.Length);
        Assert.Null(buf.Inspect("rid"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buffer_Abort_RemovesSession()
    {
        var buf = new ReplayChunkUploadBuffer();
        buf.Write("rid", 1, new byte[] { 1 });
        Assert.True(buf.Abort("rid"));
        Assert.False(buf.Abort("rid"));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Buffer_NullArgs_Throw()
    {
        var buf = new ReplayChunkUploadBuffer();
        Assert.Throws<ArgumentException>(() => buf.Write("", 1, new byte[] { 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => buf.Write("rid", 0, new byte[] { 1 }));
        Assert.Throws<ArgumentNullException>(() => buf.Write("rid", 1, null!));
    }

    [Fact, Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void IsHex_RejectsNonHex()
    {
        Assert.True(ReplayChunkUploadController.IsHex("abc123"));
        Assert.True(ReplayChunkUploadController.IsHex("ABCDEF"));
        Assert.False(ReplayChunkUploadController.IsHex("xyz"));
        Assert.False(ReplayChunkUploadController.IsHex(""));
    }
}
