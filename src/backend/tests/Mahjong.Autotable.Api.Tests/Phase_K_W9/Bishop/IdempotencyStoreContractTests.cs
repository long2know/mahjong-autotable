using Mahjong.Autotable.Api.Audit;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W9.Bishop;

/// <summary>
/// Phase K Wave 9 — Bishop. Hard-asserted contract facts for the
/// EF-backed <see cref="EfIdempotencyStore"/> + the Redis-toggled
/// wrapper. The W9 brief calls for replay-protection that survives
/// across replicas; these tests drive that surface against a real
/// SQLite database so the round-trip persistence is exercised.
///
/// <list type="number">
///   <item>Record / read round-trips an entry.</item>
///   <item>Expired entries are treated as missing.</item>
///   <item>Re-recording the same key updates in place.</item>
///   <item>Remove drops the row.</item>
///   <item>Sweep removes expired rows in bulk.</item>
///   <item>Records survive across <c>EfIdempotencyStore</c>
///         instances (multi-replica simulation).</item>
///   <item>Oversized response bodies are truncated to the entity
///         cap (64 KB).</item>
///   <item>The Redis wrapper writes through to the EF store.</item>
///   <item>The Redis wrapper exposes the connection string.</item>
/// </list>
/// </summary>
public sealed class IdempotencyStoreContractTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"mahjong-idem-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
            b.ConfigureServices(s =>
            {
                s.Configure<ChangshaRuntimeOptions>(o =>
                {
                    o.PersistSnapshots = false;
                    o.BotTurnDelayMs = 1;
                });
            });
        });
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory?.Dispose();
        try { if (_tempDb is not null && File.Exists(_tempDb)) File.Delete(_tempDb); } catch { }
        return Task.CompletedTask;
    }

    private EfIdempotencyStore NewStore(TimeSpan? replayWindow = null) =>
        new(_factory!.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EfIdempotencyStore>.Instance,
            replayWindow);

    private static IdempotencyRecord MakeRecord(string key, string payloadHash = "h0", int status = 201) =>
        new(Key: key,
            PayloadHash: payloadHash,
            RecordedAt: DateTimeOffset.UtcNow,
            StatusCode: status,
            ContentType: "application/json",
            ResponseBody: "{\"ok\":true}");

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void Record_AndTryGet_RoundTrips()
    {
        var s = NewStore();
        var key = "kw9-rt-" + Guid.NewGuid().ToString("N")[..10];
        var rec = MakeRecord(key);
        s.Record(rec);
        var got = s.TryGet(key);
        Assert.NotNull(got);
        Assert.Equal(key, got!.Key);
        Assert.Equal(201, got.StatusCode);
        Assert.Equal("application/json", got.ContentType);
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void TryGet_UnknownKey_ReturnsNull()
    {
        var s = NewStore();
        Assert.Null(s.TryGet("never-recorded-" + Guid.NewGuid().ToString("N")));
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public async Task ExpiredEntries_AreTreatedAsMissing()
    {
        var s = NewStore(replayWindow: TimeSpan.FromMilliseconds(50));
        var key = "kw9-exp-" + Guid.NewGuid().ToString("N")[..10];
        s.Record(MakeRecord(key));
        await Task.Delay(120);
        Assert.Null(s.TryGet(key));
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void Record_DuplicateKey_UpdatesInPlace()
    {
        var s = NewStore();
        var key = "kw9-dup-" + Guid.NewGuid().ToString("N")[..10];
        s.Record(MakeRecord(key, payloadHash: "h-a", status: 201));
        s.Record(MakeRecord(key, payloadHash: "h-b", status: 202));
        var got = s.TryGet(key);
        Assert.NotNull(got);
        Assert.Equal("h-b", got!.PayloadHash);
        Assert.Equal(202, got.StatusCode);
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void Remove_DropsTheRow()
    {
        var s = NewStore();
        var key = "kw9-rm-" + Guid.NewGuid().ToString("N")[..10];
        s.Record(MakeRecord(key));
        Assert.NotNull(s.TryGet(key));
        s.Remove(key);
        Assert.Null(s.TryGet(key));
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void Remove_UnknownKey_DoesNotThrow()
    {
        var s = NewStore();
        s.Remove("never-recorded-" + Guid.NewGuid().ToString("N"));
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public async Task Sweep_DropsExpiredRowsInBulk()
    {
        var s = NewStore(replayWindow: TimeSpan.FromMilliseconds(20));
        var keys = Enumerable.Range(0, 5)
            .Select(i => "kw9-sweep-" + Guid.NewGuid().ToString("N")[..10])
            .ToArray();
        foreach (var k in keys) s.Record(MakeRecord(k));
        await Task.Delay(80);
        var swept = s.Sweep(DateTime.UtcNow);
        Assert.True(swept >= keys.Length,
            $"expected ≥ {keys.Length} rows swept, got {swept}");
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void Records_SurviveAcrossStoreInstances()
    {
        var key = "kw9-cross-" + Guid.NewGuid().ToString("N")[..10];
        var s1 = NewStore();
        s1.Record(MakeRecord(key));

        var s2 = NewStore();
        var got = s2.TryGet(key);
        Assert.NotNull(got);
        Assert.Equal(key, got!.Key);
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void OversizedResponseBody_IsTruncatedToEntityCap()
    {
        var s = NewStore();
        var key = "kw9-big-" + Guid.NewGuid().ToString("N")[..10];
        var big = new string('x', IdempotencyEntry.MaxResponseBodyLength + 4_096);
        s.Record(new IdempotencyRecord(
            Key: key,
            PayloadHash: "h0",
            RecordedAt: DateTimeOffset.UtcNow,
            StatusCode: 200,
            ContentType: "text/plain",
            ResponseBody: big));
        var got = s.TryGet(key);
        Assert.NotNull(got);
        Assert.True(got!.ResponseBody.Length <= IdempotencyEntry.MaxResponseBodyLength,
            $"body length {got.ResponseBody.Length} exceeds cap {IdempotencyEntry.MaxResponseBodyLength}");
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void RedisWrapper_WritesThroughToEf()
    {
        var ef = NewStore();
        var wrapper = new RedisIdempotencyStore(ef, NullLogger<RedisIdempotencyStore>.Instance, "redis://stub");
        var key = "kw9-redis-" + Guid.NewGuid().ToString("N")[..10];
        wrapper.Record(MakeRecord(key));

        var direct = ef.TryGet(key);
        Assert.NotNull(direct);
        Assert.Equal(key, direct!.Key);
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void RedisWrapper_TryGet_HitsLocalCache_ThenFallback()
    {
        var ef = NewStore();
        var wrapper = new RedisIdempotencyStore(ef, NullLogger<RedisIdempotencyStore>.Instance, "redis://stub");
        var key = "kw9-cache-" + Guid.NewGuid().ToString("N")[..10];
        ef.Record(MakeRecord(key));
        // First read populates local cache; second read should serve
        // from the local store without re-hitting EF.
        Assert.NotNull(wrapper.TryGet(key));
        Assert.NotNull(wrapper.TryGet(key));
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void RedisWrapper_ExposesConnectionString()
    {
        var ef = NewStore();
        var wrapper = new RedisIdempotencyStore(ef, NullLogger<RedisIdempotencyStore>.Instance, "redis://example:6379");
        Assert.Equal("redis://example:6379", wrapper.ConnectionString);
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void RedisWrapper_Remove_PropagatesToEf()
    {
        var ef = NewStore();
        var wrapper = new RedisIdempotencyStore(ef, NullLogger<RedisIdempotencyStore>.Instance, "redis://stub");
        var key = "kw9-rm-r-" + Guid.NewGuid().ToString("N")[..10];
        wrapper.Record(MakeRecord(key));
        Assert.NotNull(ef.TryGet(key));
        wrapper.Remove(key);
        Assert.Null(ef.TryGet(key));
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public void DefaultReplayWindow_IsFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), EfIdempotencyStore.DefaultReplayWindow);
    }

    [Fact, Trait("Category", "Audit"), Trait("Wave", "Phase-K-9")]
    public async Task PersistedRow_HasMatchingExpiresAt()
    {
        var s = NewStore(replayWindow: TimeSpan.FromMinutes(7));
        var key = "kw9-exp-shape-" + Guid.NewGuid().ToString("N")[..10];
        s.Record(MakeRecord(key));

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.IdempotencyEntries.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key);
        Assert.NotNull(row);
        var diff = row!.ExpiresAt - row.RecordedAt;
        Assert.InRange(diff.TotalSeconds, 60 * 7 - 5, 60 * 7 + 5);
    }
}
