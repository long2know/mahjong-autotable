using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mahjong.Autotable.Api.Tables;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Mahjong.Autotable.Api.Tests;

public sealed class ClaimResolutionApiTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task ResolveClaim_WhenWindowNotOpen_ReturnsStructuredError()
    {
        using var client = factory.CreateClient();
        var tableId = await CreateTableAsync(client, 8100);

        var response = await client.PostAsJsonAsync($"/api/tables/{tableId}/claims/resolve", new
        {
            decision = TableClaimResolutionDecisionValues.Pass
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(TableActionErrorCodes.ClaimWindowNotOpen, root.GetProperty("code").GetString());
        Assert.True(root.GetProperty("stateVersion").GetInt32() > 0);
        Assert.True(root.GetProperty("actionSequence").GetInt64() >= 0);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task ResolveClaim_WhenDecisionInvalid_ReturnsStructuredError()
    {
        using var client = factory.CreateClient();
        var opened = await OpenClaimWindowAsync(client);

        var response = await client.PostAsJsonAsync($"/api/tables/{opened.TableId}/claims/resolve", new
        {
            decision = "invalid-decision",
            expectedStateVersion = opened.ExpectedStateVersion
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(TableActionErrorCodes.InvalidClaimDecision, root.GetProperty("code").GetString());
        Assert.Equal(opened.ExpectedStateVersion, root.GetProperty("stateVersion").GetInt32());
        Assert.True(root.GetProperty("actionSequence").GetInt64() > 0);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task ResolveClaim_WithPass_ReturnsAppliedDecisionAndClearsWindow()
    {
        using var client = factory.CreateClient();
        var opened = await OpenClaimWindowAsync(client);

        var response = await client.PostAsJsonAsync($"/api/tables/{opened.TableId}/claims/resolve", new
        {
            decision = TableClaimResolutionDecisionValues.Pass,
            expectedStateVersion = opened.ExpectedStateVersion
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(TableClaimResolutionDecisionValues.Pass, root.GetProperty("appliedDecision").GetString());
        Assert.Equal("claim-resolve-pass", root.GetProperty("resolutionAction").GetProperty("actionType").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("table").GetProperty("state").GetProperty("claimWindow").ValueKind);
    }

    [Fact]
    public async Task NextHand_FromExistingTable_CreatesNewTableWithIncrementedSeed()
    {
        using var client = factory.CreateClient();
        var sourceTableId = await CreateTableAsync(client, 9100);

        var sourceResponse = await client.GetAsync($"/api/tables/{sourceTableId}");
        sourceResponse.EnsureSuccessStatusCode();
        using var sourceDocument = JsonDocument.Parse(await sourceResponse.Content.ReadAsStringAsync());
        var sourceRuleSet = sourceDocument.RootElement.GetProperty("ruleSet").GetString();
        var sourceSeed = sourceDocument.RootElement.GetProperty("state").GetProperty("metadata").GetProperty("seed").GetInt32();

        var nextHandResponse = await client.PostAsJsonAsync($"/api/tables/{sourceTableId}/next-hand", new { });

        Assert.Equal(HttpStatusCode.Created, nextHandResponse.StatusCode);
        using var nextDocument = JsonDocument.Parse(await nextHandResponse.Content.ReadAsStringAsync());
        var nextRoot = nextDocument.RootElement;
        Assert.NotEqual(sourceTableId, nextRoot.GetProperty("id").GetGuid());
        Assert.Equal(sourceRuleSet, nextRoot.GetProperty("ruleSet").GetString());
        Assert.Equal(sourceSeed + 1, nextRoot.GetProperty("state").GetProperty("metadata").GetProperty("seed").GetInt32());
        Assert.Equal(1, nextRoot.GetProperty("stateVersion").GetInt32());
        Assert.Equal(0, nextRoot.GetProperty("state").GetProperty("actionSequence").GetInt64());
    }

    private static async Task<Guid> CreateTableAsync(HttpClient client, int seed)
    {
        var response = await client.PostAsJsonAsync("/api/tables", new { seed });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid TableId, int ExpectedStateVersion)> OpenClaimWindowAsync(HttpClient client)
    {
        for (var seed = 8200; seed < 8600; seed++)
        {
            var tableId = await CreateTableAsync(client, seed);
            var state = await GetTableStateAsync(client, tableId);
            if (!TryFindClaimOpeningDiscard(state.TableState, out var tileId))
            {
                continue;
            }

            var discardResponse = await client.PostAsJsonAsync($"/api/tables/{tableId}/actions/discard", new
            {
                seatIndex = 0,
                tileId,
                expectedStateVersion = state.StateVersion
            });

            if (!discardResponse.IsSuccessStatusCode)
            {
                continue;
            }

            using var discardDocument = JsonDocument.Parse(await discardResponse.Content.ReadAsStringAsync());
            var table = discardDocument.RootElement.GetProperty("table");
            var claimWindow = table.GetProperty("state").GetProperty("claimWindow");
            if (claimWindow.ValueKind != JsonValueKind.Null)
            {
                return (tableId, table.GetProperty("stateVersion").GetInt32());
            }
        }

        throw new InvalidOperationException("Unable to find a deterministic discard that opens a claim window.");
    }

    private static async Task<(int StateVersion, JsonElement TableState)> GetTableStateAsync(HttpClient client, Guid tableId)
    {
        var response = await client.GetAsync($"/api/tables/{tableId}");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var state = document.RootElement.GetProperty("state");
        return (document.RootElement.GetProperty("stateVersion").GetInt32(), state.Clone());
    }

    private static bool TryFindClaimOpeningDiscard(JsonElement tableState, out int tileId)
    {
        var hands = tableState.GetProperty("hands")
            .EnumerateArray()
            .ToDictionary(
                hand => hand.GetProperty("seatIndex").GetInt32(),
                hand => hand.GetProperty("tiles").EnumerateArray().Select(tile => tile.GetInt32()).ToArray());

        foreach (var candidateTile in hands[0].OrderBy(tile => tile))
        {
            var discardLogicalTile = candidateTile / 4;
            for (var seatIndex = 1; seatIndex < TableStateEngine.SeatCount; seatIndex++)
            {
                var matchingCount = hands[seatIndex].Count(tile => tile / 4 == discardLogicalTile);
                if (matchingCount >= 2)
                {
                    tileId = candidateTile;
                    return true;
                }
            }

            if (IsChowCandidate(hands[1], discardLogicalTile))
            {
                tileId = candidateTile;
                return true;
            }
        }

        tileId = default;
        return false;
    }

    private static bool IsChowCandidate(IReadOnlyCollection<int> seatTiles, int discardLogical)
    {
        if (discardLogical is < 0 or >= 27)
        {
            return false;
        }

        var suitOffset = discardLogical / 9;
        var rank = discardLogical % 9;
        var suitedTiles = seatTiles
            .Select(tile => tile / 4)
            .Where(tile => tile / 9 == suitOffset)
            .Select(tile => tile % 9)
            .ToHashSet();

        var hasLowerRun = rank >= 2 && suitedTiles.Contains(rank - 2) && suitedTiles.Contains(rank - 1);
        var hasMiddleRun = rank >= 1 && rank <= 7 && suitedTiles.Contains(rank - 1) && suitedTiles.Contains(rank + 1);
        var hasUpperRun = rank <= 6 && suitedTiles.Contains(rank + 1) && suitedTiles.Contains(rank + 2);
        return hasLowerRun || hasMiddleRun || hasUpperRun;
    }
}

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        AppContext.BaseDirectory,
        $"claim-resolution-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
