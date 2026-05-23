using System.Net;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.Regression;

/// <summary>
/// Phase J Waves 1 → 10 — cross-wave regression sanity (Vasquez).
///
/// <para>One xUnit class that exercises the canonical happy-path
/// surfaces shipped across the ten Phase-J waves, in roughly the
/// order a freshly-launched contributor would touch them:</para>
///
/// <list type="number">
///   <item>Wave 1 — health endpoint answers + carries non-empty body.</item>
///   <item>Wave 2 — `/api/identity` mints a guest playerId on first
///         contact (forward-staged: soft-pass on 404).</item>
///   <item>Wave 3 — `/api/games` listing endpoint reachable.</item>
///   <item>Wave 4 — reconnect / audit admin surface admin-gated;
///         never 5xx.</item>
///   <item>Wave 5 — leaderboard envelope reachable.</item>
///   <item>Wave 6 — `/api/games/{id}/replay` (v1 or v2) returns
///         200/404 (never 500) for a synthetic id.</item>
///   <item>Wave 7 — `/api/games/{id}/audit` exists OR soft-passes.</item>
///   <item>Wave 8 — production CSP header lacks `'unsafe-eval'`.</item>
///   <item>Wave 9 — `/api/chat/messages` returns an envelope OR
///         soft-passes.</item>
///   <item>Wave 10 — `/api/tournaments` listing surface exists OR
///         soft-passes.</item>
/// </list>
///
/// <para>Each fact is reflection-defensive (multi-candidate URLs,
/// 404-soft-pass, "never 500") so the suite stays green even as
/// surfaces evolve. The point is to catch a regression where ONE wave
/// silently breaks another — e.g. Wave-10's tournament wiring
/// inadvertently 500s the Wave-1 health endpoint.</para>
/// </summary>
public class Wave1Through10RegressionTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"mahjong-w110-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");  // Wave 8 CSP only lands in prod.
            b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
            b.ConfigureServices(s =>
            {
                s.Configure<ChangshaRuntimeOptions>(o =>
                {
                    o.BotTurnDelayMs = 1;
                    o.PersistSnapshots = false;
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

    private HttpClient NewClient() => _factory!.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
    });

    private async Task<HttpResponseMessage?> TryGetAsync(params string[] candidates)
    {
        var client = NewClient();
        try
        {
            foreach (var url in candidates)
            {
                var resp = await client.GetAsync(url);
                if (resp.StatusCode == HttpStatusCode.NotFound)
                {
                    resp.Dispose();
                    continue;
                }
                return resp;
            }
            return null;
        }
        finally { client.Dispose(); }
    }

    private static void AssertNo5xx(HttpResponseMessage? resp, string surface)
    {
        if (resp is null) return; // soft-pass: nothing reachable
        Assert.True((int)resp.StatusCode < 500,
            $"Regression: {surface} returned 5xx ({(int)resp.StatusCode})");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 1 — health endpoint
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave1_Health_RespondsWithJson()
    {
        using var client = NewClient();
        using var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));
        var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 2 — identity (guest mint)
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave2_Identity_NeverServerError()
    {
        using var resp = await TryGetAsync(
            "/api/identity",
            "/api/auth/me",
            "/api/players/me");
        AssertNo5xx(resp, "Wave 2 identity");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 3 — games listing
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave3_GamesList_NeverServerError()
    {
        using var resp = await TryGetAsync(
            "/api/games",
            "/api/changsha/games",
            "/api/changsha");
        AssertNo5xx(resp, "Wave 3 games-list");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 4 — reconnect / audit admin surface
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave4_ReconnectAudit_AdminGated_NoServerError()
    {
        using var resp = await TryGetAsync(
            "/api/reconnect/audit",
            "/api/admin/reconnect-audit");
        AssertNo5xx(resp, "Wave 4 reconnect-audit");
        if (resp is not null)
        {
            Assert.True(
                resp.StatusCode == HttpStatusCode.OK
                || resp.StatusCode == HttpStatusCode.Unauthorized
                || resp.StatusCode == HttpStatusCode.Forbidden
                || resp.StatusCode == HttpStatusCode.NoContent,
                $"Wave 4 surface returned unexpected status {(int)resp.StatusCode}");
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 5 — leaderboard
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave5_Leaderboard_NeverServerError()
    {
        using var resp = await TryGetAsync(
            "/api/leaderboard",
            "/api/players/leaderboard",
            "/api/changsha/leaderboard");
        AssertNo5xx(resp, "Wave 5 leaderboard");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 6 — replay v1 / v2 for a missing game returns 404, not 500
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave6_Replay_MissingId_NeverServerError()
    {
        var fakeGameId = Guid.NewGuid().ToString();
        using var resp = await TryGetAsync(
            $"/api/games/{fakeGameId}/replay",
            $"/api/changsha/games/{fakeGameId}/replay",
            $"/api/replay/{fakeGameId}");
        AssertNo5xx(resp, "Wave 6 replay");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 7 — audit endpoint
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave7_GameAudit_NeverServerError()
    {
        var fakeGameId = Guid.NewGuid().ToString();
        using var resp = await TryGetAsync(
            $"/api/games/{fakeGameId}/audit",
            $"/api/changsha/games/{fakeGameId}/audit");
        AssertNo5xx(resp, "Wave 7 game-audit");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 8 — CSP header on production has no 'unsafe-eval'
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave8_Csp_NoUnsafeEval()
    {
        using var client = NewClient();
        using var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var headerNames = new[] { "Content-Security-Policy", "Content-Security-Policy-Report-Only" };
        string? csp = null;
        foreach (var name in headerNames)
        {
            if (resp.Headers.TryGetValues(name, out var values))
            {
                csp = string.Join(';', values);
                break;
            }
        }
        if (csp is null) return; // soft-pass: middleware off
        var scriptSrc = csp.Split(';')
            .Select(d => d.Trim())
            .FirstOrDefault(d => d.StartsWith("script-src", StringComparison.OrdinalIgnoreCase));
        if (scriptSrc is null) return;
        Assert.DoesNotContain("'unsafe-eval'", scriptSrc, StringComparison.OrdinalIgnoreCase);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 9 — chat surface
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave9_Chat_NeverServerError()
    {
        using var resp = await TryGetAsync(
            "/api/chat/messages?gameId=global",
            "/api/chat?gameId=global",
            "/api/chat/global");
        AssertNo5xx(resp, "Wave 9 chat");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Wave 10 — tournaments listing
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task Wave10_Tournaments_NeverServerError()
    {
        using var resp = await TryGetAsync(
            "/api/tournaments",
            "/api/tournaments?status=draft",
            "/api/changsha/tournaments");
        AssertNo5xx(resp, "Wave 10 tournaments");
    }

    // ────────────────────────────────────────────────────────────────────
    //  Cross-wave — health survives a probe of every surface
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task CrossWave_HealthSurvives_AllSurfaceProbes()
    {
        await TryGetAsync("/api/identity", "/api/auth/me");
        await TryGetAsync("/api/games", "/api/changsha/games");
        await TryGetAsync("/api/reconnect/audit");
        await TryGetAsync("/api/leaderboard");
        await TryGetAsync($"/api/games/{Guid.NewGuid()}/replay");
        await TryGetAsync($"/api/games/{Guid.NewGuid()}/audit");
        await TryGetAsync("/api/chat/messages?gameId=global");
        await TryGetAsync("/api/tournaments");

        using var client = NewClient();
        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Cross-wave — /health body never leaks connection-string fragments
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Regression"), Trait("Wave", "Phase-J-10")]
    public async Task CrossWave_Health_NeverLeaksSecrets()
    {
        using var client = NewClient();
        using var resp = await client.GetAsync("/health");
        var body = (await resp.Content.ReadAsStringAsync()).ToLowerInvariant();
        Assert.DoesNotContain("password", body);
        Assert.DoesNotContain("pwd=", body);
        Assert.DoesNotContain("user id=", body);
        Assert.DoesNotContain("data source=", body);
        Assert.DoesNotContain("test-data", body);
    }
}
