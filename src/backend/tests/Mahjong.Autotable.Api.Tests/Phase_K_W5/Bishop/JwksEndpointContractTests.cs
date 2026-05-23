using System.Net;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W5.Bishop;

/// <summary>
/// Phase K Wave 5 — Bishop. JWKS endpoint contract pin.
///
/// <para>The HMAC-signed Wave-5 surface cannot publish a real JWKS
/// document (the shared secret would defeat the purpose of having
/// one), so the endpoint exists ONLY to reserve the route + force
/// cache-bypass: any intermediate proxy/CDN that pins the 404 with
/// a long TTL would prevent the Phase L RS256 flip from rolling out
/// cleanly. The <c>Cache-Control: no-store</c> header + the
/// structured 404 body together pin the contract.</para>
/// </summary>
public sealed class JwksEndpointContractTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"mahjong-bishop-jwks-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
            b.UseSetting("Auth:JwtSigningKeys:0", "phase-k-w5-bishop-jwks-32-bytes!!");
            b.ConfigureServices(s =>
                s.Configure<ChangshaRuntimeOptions>(o => { o.PersistSnapshots = false; }));
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

    [Fact, Trait("Category", "Auth"), Trait("Wave", "Phase-K-5"), Trait("Lane", "Bishop")]
    public async Task Jwks_Returns404WithCacheControlNoStore()
    {
        Assert.NotNull(_factory);
        using var client = _factory!.CreateClient();
        using var resp = await client.GetAsync("/api/auth/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(
            resp.Headers.CacheControl?.NoStore == true
            || (resp.Headers.TryGetValues("Cache-Control", out var vals)
                && vals.Any(v => v.Contains("no-store", StringComparison.OrdinalIgnoreCase))),
            "JWKS 404 MUST carry Cache-Control: no-store so the negative isn't cached by an intermediate proxy/CDN ahead of the Phase L RS256 flip.");
    }

    [Fact, Trait("Category", "Auth"), Trait("Wave", "Phase-K-5"), Trait("Lane", "Bishop")]
    public async Task Jwks_BodyCarriesAlgorithmAndNote()
    {
        Assert.NotNull(_factory);
        using var client = _factory!.CreateClient();
        using var resp = await client.GetAsync("/api/auth/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("algorithm", out var algEl));
        Assert.Equal("HS256", algEl.GetString());
        Assert.True(root.TryGetProperty("note", out _));
        Assert.True(root.TryGetProperty("error", out _));
    }
}
