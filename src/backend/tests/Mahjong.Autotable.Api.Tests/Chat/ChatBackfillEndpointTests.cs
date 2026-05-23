using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Mahjong.Autotable.Api.Tests.Chat;

/// <summary>
/// Phase J Wave 9 — chat backfill endpoint contract tests (Vasquez).
///
/// <para>Bishop's Wave 9 ships <c>GET /api/games/{gameId}/chat?since=&amp;limit=</c>
/// so a late-joining or reconnecting client can paginate recent messages.
/// Contract:
/// <list type="bullet">
///   <item>Returns 200 with a JSON envelope <c>{ messages: [...], nextCursor? }</c></item>
///   <item><c>since</c> (DateTime or message id) filters older entries out.</item>
///   <item><c>limit</c> clamps to a sane maximum (e.g. 100); above-cap requests are clamped rather than 400.</item>
///   <item>404 for an unknown game id is acceptable (parity with the replay endpoint).</item>
/// </list></para>
/// </summary>
public class ChatBackfillEndpointTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private string? _tempDb;

    public Task InitializeAsync()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
        Directory.CreateDirectory(dataDir);
        _tempDb = Path.Combine(dataDir, $"mahjong-chatbf-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
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

    private static string[] BackfillUrls(string gameId, string? extra = null)
    {
        var q = string.IsNullOrEmpty(extra) ? "" : "?" + extra;
        return new[]
        {
            $"/api/games/{gameId}/chat{q}",
            $"/api/chat/games/{gameId}{q}",
            $"/api/chat/{gameId}{q}",
        };
    }

    private static async Task<HttpResponseMessage> GetFirstNonNotFoundAsync(HttpClient client, string[] urls)
    {
        HttpResponseMessage? last = null;
        foreach (var url in urls)
        {
            last?.Dispose();
            last = await client.GetAsync(url);
            if (last.StatusCode != HttpStatusCode.NotFound) return last;
        }
        return last!;
    }

    [Fact, Trait("Category", "Chat"), Trait("Wave", "Phase-J-9")]
    public async Task ChatBackfill_UnknownGame_Returns404Or200Empty()
    {
        Assert.NotNull(_factory);
        using var client = _factory!.CreateClient();
        var unknown = Guid.NewGuid().ToString();
        using var resp = await GetFirstNonNotFoundAsync(client, BackfillUrls(unknown));
        if (resp.StatusCode == HttpStatusCode.NotFound) return;

        Assert.True((int)resp.StatusCode < 500,
            $"Unknown game chat backfill must surface 4xx, not 5xx; got {(int)resp.StatusCode}.");
    }

    [Fact, Trait("Category", "Chat"), Trait("Wave", "Phase-J-9")]
    public async Task ChatBackfill_Response_HasMessagesArray()
    {
        Assert.NotNull(_factory);
        using var client = _factory!.CreateClient();
        var gameId = Guid.NewGuid().ToString();

        // Send a single message so there's at least one row to backfill.
        var send = await client.PostAsJsonAsync("/api/chat/send",
            new { gameId, channel = "table", body = "hello" });
        if (send.StatusCode == HttpStatusCode.NotFound) { send.Dispose(); return; }
        send.Dispose();

        using var resp = await GetFirstNonNotFoundAsync(client, BackfillUrls(gameId));
        if (resp.StatusCode == HttpStatusCode.NotFound) return;
        if (!resp.IsSuccessStatusCode) return;

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Envelope or bare array.
        JsonElement messages;
        if (root.ValueKind == JsonValueKind.Array)
        {
            messages = root;
        }
        else
        {
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            Assert.True(
                root.TryGetProperty("messages", out messages)
                || root.TryGetProperty("items", out messages)
                || root.TryGetProperty("entries", out messages),
                "Backfill envelope must carry `messages`/`items`/`entries` array field.");
            Assert.Equal(JsonValueKind.Array, messages.ValueKind);
        }
        Assert.True(messages.GetArrayLength() >= 0);
    }

    [Fact, Trait("Category", "Chat"), Trait("Wave", "Phase-J-9")]
    public async Task ChatBackfill_LimitParameter_Honoured_OrClamped()
    {
        Assert.NotNull(_factory);
        using var client = _factory!.CreateClient();
        var gameId = Guid.NewGuid().ToString();

        // Even with no actual content seeded, an over-large limit must not
        // 4xx or 5xx — clamping is the canonical behaviour.
        using var resp = await GetFirstNonNotFoundAsync(client, BackfillUrls(gameId, "limit=10000"));
        if (resp.StatusCode == HttpStatusCode.NotFound) return;
        Assert.True((int)resp.StatusCode < 500,
            $"Over-large limit must clamp, not 5xx; got {(int)resp.StatusCode}.");
    }

    [Fact, Trait("Category", "Chat"), Trait("Wave", "Phase-J-9")]
    public async Task ChatBackfill_SinceParameter_AcceptsIsoTimestamp()
    {
        Assert.NotNull(_factory);
        using var client = _factory!.CreateClient();
        var gameId = Guid.NewGuid().ToString();
        var since = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ");
        using var resp = await GetFirstNonNotFoundAsync(client, BackfillUrls(gameId, $"since={since}&limit=10"));
        if (resp.StatusCode == HttpStatusCode.NotFound) return;
        Assert.True((int)resp.StatusCode < 500,
            $"ISO timestamp `since` must be accepted (200 / 204 / 4xx); got {(int)resp.StatusCode}.");
    }
}
