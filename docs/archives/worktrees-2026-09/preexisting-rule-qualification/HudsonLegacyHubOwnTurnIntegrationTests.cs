using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit.Abstractions;
using static Mahjong.Autotable.Api.Tests.RulesQualification.HudsonOwnTurnWsFixture;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class HudsonLegacyHubOwnTurnIntegrationTests(ITestOutputHelper output)
{
    [Theory, Trait("Category", "RulesQualificationLegacyHub")]
    [InlineData("hu-14")]
    [InlineData("hu-11")]
    [InlineData("hu-8")]
    [InlineData("concealed-kong")]
    [InlineData("added-kong")]
    public async Task LegacyDeclaration_UsesActualCallerAndUnchangedArguments_WithValidOwnerControl(string scenario)
    {
        await using var host = new HudsonOwnTurnWsFixture(output: output);
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var otherSeat = await host.ConnectAsync(table.RoomId);
        await otherSeat.UpdateAsync([new object[] { "seats", otherSeat.PlayerId, new { seat = 1 } }]);
        await otherSeat.BarrierAsync();
        Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(table.GameId, otherSeat.PlayerId));
        var prepared = ArrangeBeforeDraw(table, scenario);
        await host.AdvanceToDrawAsync(table, prepared);
        var ready = await host.SnapshotAsync(table);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, ready.Phase);
        Assert.Equal(0, ready.ActiveSeatIndex);
        Assert.Equal(0, ready.LastDrawSeatIndex);
        Assert.Equal(14, ready.Hands[0].ConcealedTiles.Count + 3 * ready.Hands[0].Melds.Count);
        AssertInventory(ready);
        var hu = scenario.StartsWith("hu-", StringComparison.Ordinal);
        if (hu)
            Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(ready, 0));
        else if (scenario == "concealed-kong")
            Assert.True(ChangshaGameStateMachine.CanDeclareConcealedKong(ready, 0, 4));
        else
            Assert.True(ChangshaGameStateMachine.CanDeclareAddedKong(ready, 0, 19));

        await using var owner = await host.ConnectHubAsync(table.Peer.PlayerId);
        await using var wrongOwner = await host.ConnectHubAsync(otherSeat.PlayerId);
        var spectatorId = $"hudson-legacy-spectator-{Guid.NewGuid():N}";
        await using var spectator = await host.ConnectHubAsync(spectatorId);
        await JoinAsync(owner, table.GameId);
        await JoinAsync(wrongOwner, table.GameId);
        await JoinAsync(spectator, table.GameId);
        Assert.Equal(0, host.Runtime.TryGetSeatForPlayer(table.GameId, table.Peer.PlayerId));
        Assert.Equal(1, host.Runtime.TryGetSeatForPlayer(table.GameId, otherSeat.PlayerId));
        Assert.Null(host.Runtime.TryGetSeatForPlayer(table.GameId, spectatorId));

        var before = await host.SnapshotAsync(table);
        foreach (var (connection, actor) in new[] { (wrongOwner, "other-seated-owner"), (spectator, "spectator") })
        {
            var failure = await Assert.ThrowsAsync<HubException>(() =>
                DeclareAsync(connection, table.GameId, scenario));
            Assert.False(string.IsNullOrWhiteSpace(failure.Message));
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await host.SnapshotAsync(table)));
            Assert.Equal(HubConnectionState.Connected, connection.State);
            await JoinAsync(connection, table.GameId);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await host.SnapshotAsync(table)));
            output.WriteLine($"Legacy {scenario}, actual caller={actor}: Hub error, full state/version/events unchanged, same Hub socket remains usable.");
        }

        await DeclareAsync(owner, table.GameId, scenario);
        var after = await host.SnapshotAsync(table);
        Assert.True(after.StateVersion > before.StateVersion);
        AssertInventory(after);
        if (hu)
        {
            Assert.Equal(ChangshaPhase.GameComplete, after.Phase);
            Assert.True(after.IsGameComplete);
            Assert.NotNull(after.CurrentWin);
            Assert.Equal(0, after.CurrentWin.WinningSeatIndex);
            Assert.Equal(WinMethod.SelfDraw, after.CurrentWin.Method);
            Assert.Equal(prepared.DrawTile, after.CurrentWin.WinningTileId);
            Assert.True(after.CurrentWin.IsSelfDraw);
            Assert.False(after.CurrentWin.IsKongReplacement);
            Assert.NotNull(after.CurrentScore);
            Assert.True(after.CumulativeScores[0] > before.CumulativeScores[0]);
            Assert.Equal(0, after.CumulativeScores.Values.Sum());
            Assert.Equal(before.Wall, after.Wall);
            Assert.Equal(before.WallBackDrawn, after.WallBackDrawn);
            output.WriteLine($"Legacy DeclareWin(gameId,seatIndex) actual owner succeeded after real {before.Hands[0].ConcealedTiles.Count - 1}->{before.Hands[0].ConcealedTiles.Count} draw; tile={prepared.DrawTile}, GameComplete, version={after.StateVersion}, inventory108.");
        }
        else
        {
            Assert.Equal(ChangshaPhase.AwaitingDiscard, after.Phase);
            Assert.Equal(0, after.ActiveSeatIndex);
            Assert.Equal(0, after.LastDrawSeatIndex);
            Assert.True(after.LastDrawWasKongReplacement);
            Assert.Null(after.ClaimWindow);
            Assert.Null(after.CurrentWin);
            Assert.Null(after.CurrentScore);
            var kind = scenario == "concealed-kong" ? MeldKind.ConcealedKong : MeldKind.AddedKong;
            var meld = Assert.Single(after.Hands[0].Melds, candidate => candidate.Kind == kind);
            Assert.Equal(new[] { 16, 17, 18, 19 }, meld.TileIds.OrderBy(tile => tile));
            Assert.Equal(before.Wall.Take(before.Wall.Count - 1), after.Wall);
            Assert.Equal(before.Wall[^1], after.Hands[0].ConcealedTiles[^1]);
            Assert.Equal(before.WallBackDrawn + 1, after.WallBackDrawn);
            Assert.Equal(before.WallDrawIndex, after.WallDrawIndex);
            Assert.Equal(before.Hands[0].ConcealedTiles.Count - (scenario == "concealed-kong" ? 3 : 0),
                after.Hands[0].ConcealedTiles.Count);
            Assert.Equal(before.CumulativeScores.OrderBy(pair => pair.Key),
                after.CumulativeScores.OrderBy(pair => pair.Key));
            output.WriteLine($"Legacy DeclareKong(gameId,seatIndex,tileIds) owner succeeded with one held representative for {scenario}; exact quartet/back replacement={before.Wall[^1]}, version={after.StateVersion}, inventory108.");
        }
    }

    private static async Task JoinAsync(HubConnection connection, string game)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await connection.InvokeAsync("JoinTable", game, timeout.Token);
    }

    private static async Task DeclareAsync(HubConnection connection, string game, string scenario)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (scenario.StartsWith("hu-", StringComparison.Ordinal))
        {
            await connection.InvokeAsync("DeclareWin", game, 0, timeout.Token);
        }
        else
        {
            var representative = scenario switch
            {
                "concealed-kong" => 16,
                "added-kong" => 19,
                _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown legacy declaration fixture.")
            };
            await connection.InvokeAsync("DeclareKong", game, 0, new[] { representative }, timeout.Token);
        }
    }
}
