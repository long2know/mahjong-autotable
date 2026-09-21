using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Scoring;
using Mahjong.Autotable.Api.Tables;
using Mahjong.Autotable.Api.Tests.Changsha.Scoring;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class CanonicalEngineRepairTests
{
    private static readonly int[] StandardWin =
        [0, 4, 8, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53];
    private static readonly int[] SevenPairs =
        [0, 1, 12, 13, 28, 29, 36, 37, 52, 53, 72, 73, 104, 105];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialDealerExtra_IsOwnDraw_AfterAutoAndManualDeal(bool manual)
    {
        var state = Dealt(manual: manual);
        PlaceHand(state, 0, StandardWin);
        AssertDeck(state);
        Assert.Equal(0, state.LastDrawSeatIndex);
        Assert.Equal(0, state.DiscardsThisHand);
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 0));
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 1));

        ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);

        Assert.Contains(WinPattern.HeavenlyHand, state.CurrentWin!.AllPatterns);
        AssertDeck(state);
    }

    [Fact]
    public void WinningFourteenTiles_WithoutOwnDraw_DoNotGrantSelfDraw()
    {
        var state = Dealt();
        PlaceHand(state, 0, StandardWin);
        state.LastDrawSeatIndex = null;
        var before = JsonSerializer.Serialize(state);

        Assert.True(new ChangshaWinDetector().Detect(state.Hands[0]).IsWin);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 0));
        Assert.Throws<InvalidOperationException>(() => ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0));
        Assert.True(before == JsonSerializer.Serialize(state));
        AssertDeck(state);
    }

    [Theory]
    [InlineData(TableClaimType.Pung)]
    [InlineData(TableClaimType.Chow)]
    public void MeldWithoutDraw_DoesNotGrantSelfDraw_AndPreservesPassHu(TableClaimType claim)
    {
        var state = Dealt();
        var waiting = claim == TableClaimType.Pung
            ? new[] { 0, 1, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53 }
            : new[] { 4, 8, 12, 16, 20, 36, 40, 44, 84, 88, 92, 52, 53 };
        var discarded = claim == TableClaimType.Pung ? 2 : 0;
        PlaceHand(state, 1, waiting);
        SwapInto(state, state.Hands[0].ConcealedTiles, 0, discarded);
        state.MissedWinSeats.Add(1);
        ChangshaGameStateMachine.Discard(state, 0, discarded);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            opportunity => opportunity.SeatIndex == 1 && opportunity.ClaimType == claim);
        var wall = state.Wall.ToArray();

        ChangshaGameStateMachine.ResolveClaim(state, 1, claim);

        Assert.Equal(wall, state.Wall);
        Assert.Contains(1, state.MissedWinSeats);
        Assert.Null(state.LastDrawSeatIndex);
        Assert.True(new ChangshaWinDetector().Detect(state.Hands[1]).IsWin);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 1));
        var before = JsonSerializer.Serialize(state);
        Assert.Throws<InvalidOperationException>(() => ChangshaGameStateMachine.DeclareSelfDrawWin(state, 1));
        Assert.True(before == JsonSerializer.Serialize(state));
        AssertDeck(state);
    }

    [Fact]
    public void NormalOwnDraw_GrantsSelfDraw_AndOnlyClearsDrawingSeatsPassHu()
    {
        var state = Dealt();
        PlaceHand(state, 1, StandardWin[..^1]);
        SwapInto(state, state.Wall, 0, StandardWin[^1]);
        var discarded = state.Hands[0].ConcealedTiles[0];
        ChangshaGameStateMachine.Discard(state, 0, discarded);
        if (state.ClaimWindow is not null)
            ChangshaGameStateMachine.PassClaim(state);
        Assert.Equal(1, state.ActiveSeatIndex);
        state.MissedWinSeats.UnionWith([1, 2]);
        Assert.Null(state.LastDrawSeatIndex);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 1));

        ChangshaGameStateMachine.DrawTile(state);

        Assert.Equal(1, state.LastDrawSeatIndex);
        Assert.DoesNotContain(1, state.MissedWinSeats);
        Assert.Contains(2, state.MissedWinSeats);
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 1));
        ChangshaGameStateMachine.DeclareSelfDrawWin(state, 1);
        Assert.DoesNotContain(WinPattern.HeavenlyHand, state.CurrentWin!.AllPatterns);
        AssertDeck(state);
    }

    [Fact]
    public void KongReplacement_GrantsRealOwnDraw_WithoutChangingPassHuClearingPolicy()
    {
        var state = Dealt();
        PlaceHand(state, 0, [16, 17, 18, 19, 0, 4, 8, 36, 40, 44, 84, 88, 92, 52]);
        SwapInto(state, state.Wall, state.Wall.Count - 1, 53);
        state.MissedWinSeats.Add(0);
        Assert.Equal(new[] { 4 }, ChangshaGameStateMachine.GetConcealedKongCandidates(state, 0));

        ChangshaGameStateMachine.DeclareConcealedKong(state, 0, 4);

        Assert.Equal(0, state.LastDrawSeatIndex);
        Assert.Contains(0, state.MissedWinSeats);
        Assert.True(state.LastDrawWasKongReplacement);
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 0));
        state = RoundTrip(state);
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 0));
        ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
        Assert.Contains(WinPattern.KongReplacementWin, state.CurrentWin!.AllPatterns);
        Assert.DoesNotContain(WinPattern.HeavenlyHand, state.CurrentWin.AllPatterns);
        AssertDeck(state);
    }

    [Fact]
    public async Task RuntimeAddedKong_AllHuPass_DrawsOneReplacementAndAcceptsDeclarersDiscard()
    {
        await using var host = new HudsonOwnTurnWsFixture();
        await using var table = await host.OpenHumanTableAsync(ephemeralKinds: ["ownTurn"]);
        await using var robber = await host.ConnectAsync(table.RoomId, ephemeralKinds: ["ownTurn"]);
        await robber.UpdateAsync([new object[] { "seats", robber.PlayerId, new { seat = 1 } }]);
        await robber.BarrierAsync();
        var draw = HudsonOwnTurnWsFixture.ArrangeBeforeDraw(table, "added-kong-rob");
        await host.AdvanceToDrawAsync(table, draw);
        var before = await host.SnapshotAsync(table);

        await host.Runtime.DeclareKongAsync(table.GameId, 0, [19],
            expectedVersion: before.StateVersion, expectedPlayerId: table.Peer.PlayerId,
            requestedKind: MeldKind.AddedKong);
        Assert.Equal(ChangshaPhase.AwaitingClaim, table.State.Phase);
        Assert.Equal(1, Assert.Single(table.State.ClaimWindow!.Opportunities.Select(o => o.SeatIndex).Distinct()));
        await robber.UpdateAsync([new object[] { "claim", 1, new { action = "pass", type = (string?)null } }]);
        await robber.BarrierAsync();

        var settled = await host.SnapshotAsync(table);
        Assert.Null(settled.ClaimWindow);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, settled.Phase);
        Assert.Equal(0, settled.ActiveSeatIndex);
        Assert.Equal(0, settled.LastDrawSeatIndex);
        Assert.Equal(before.Wall.Count - 1, settled.Wall.Count);
        Assert.Equal(before.WallBackDrawn + 1, settled.WallBackDrawn);
        Assert.Equal(draw.BackTile, settled.Hands[0].ConcealedTiles[^1]);
        Assert.Equal(11, settled.Hands[0].ConcealedTiles.Count);
        Assert.Equal(MeldKind.AddedKong, Assert.Single(settled.Hands[0].Melds).Kind);
        AssertDeck(settled);

        await host.Runtime.DiscardAsync(table.GameId, 0, draw.BackTile);
        var after = await host.SnapshotAsync(table);
        Assert.True(after.StateVersion > settled.StateVersion);
        Assert.DoesNotContain(draw.BackTile, after.Hands[0].ConcealedTiles);
        Assert.Contains(after.DiscardPile, discard => discard.SeatIndex == 0 && discard.TileId == draw.BackTile);
        AssertDeck(after);
    }

    [Fact]
    public async Task RuntimeInvalidChow_RejectsBeforeQueuing_AndValidClaimCanStillComplete()
    {
        await using var host = new HudsonOwnTurnWsFixture();
        await using var table = await host.OpenHumanTableAsync();
        await using var claimant = await host.ConnectAsync(table.RoomId);
        await claimant.UpdateAsync([new object[] { "seats", claimant.PlayerId, new { seat = 1 } }]);
        await claimant.BarrierAsync();
        await using var huOwner = await host.ConnectAsync(table.RoomId);
        await huOwner.UpdateAsync([new object[] { "seats", huOwner.PlayerId, new { seat = 2 } }]);
        await huOwner.BarrierAsync();
        await using var other = await host.ConnectAsync(table.RoomId);
        await other.UpdateAsync([new object[] { "seats", other.PlayerId, new { seat = 3 } }]);
        await other.BarrierAsync();

        var state = table.State;
        Assert.Equal(ChangshaPhase.RollingDice, state.Phase);
        ChangshaGameStateMachine.RollDice(state, new DiceService(42));
        ChangshaGameStateMachine.Deal(state);
        PlaceHand(state, 1, [4, 8, 12, 16, 20, 32, 36, 40, 44, 52, 56, 84, 92]);
        PlaceHand(state, 2, [5, 9, 13, 17, 21, 37, 41, 45, 85, 89, 93, 54, 55]);
        SwapInto(state, state.Hands[0].ConcealedTiles, 0, 0);
        await host.Runtime.DiscardAsync(table.GameId, 0, 0);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            opportunity => opportunity.SeatIndex == 1 && opportunity.ClaimType == TableClaimType.Chow);
        Assert.Contains(state.ClaimWindow.Opportunities,
            opportunity => opportunity.SeatIndex == 2 && opportunity.ClaimType == TableClaimType.Hu);
        var remainingResponders = state.ClaimWindow.Opportunities
            .Select(opportunity => opportunity.SeatIndex).Where(seat => seat != 1).Distinct().ToArray();
        var before = await host.SnapshotAsync(table);

        var error = await Assert.ThrowsAsync<TableRuleException>(() =>
            host.Runtime.ClaimAsync(table.GameId, 1, "chow", [4, 4], expectedVersion: before.StateVersion));

        Assert.Equal(TableActionErrorCodes.ChowTilesInvalid, error.Code);
        Assert.True(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(await host.SnapshotAsync(table)));
        await host.Runtime.ClaimAsync(table.GameId, 1, "chow", [4, 8], expectedVersion: before.StateVersion);
        foreach (var seat in remainingResponders)
            await host.Runtime.PassAsync(table.GameId, seat);
        var after = await host.SnapshotAsync(table);
        Assert.Null(after.ClaimWindow);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, after.Phase);
        Assert.Equal(1, after.ActiveSeatIndex);
        Assert.Equal(MeldKind.Chow, Assert.Single(after.Hands[1].Melds).Kind);
        Assert.True(after.StateVersion > before.StateVersion);
        Assert.Equal(before.Wall, after.Wall);
        AssertDeck(after);
    }

    [Fact]
    public void CanonicalOwnTurnQueries_ExposeEveryKongChoice_WithoutMutation()
    {
        var state = Dealt();
        PlaceHand(state, 0, [0, 1, 2, 3, 16, 17, 18, 19, 36, 40, 44, 52, 53, 56]);
        var before = JsonSerializer.Serialize(state);
        Assert.Equal(new[] { 0, 4 }, ChangshaGameStateMachine.GetConcealedKongCandidates(state, 0));
        Assert.Empty(ChangshaGameStateMachine.GetConcealedKongCandidates(state, 1));
        Assert.Empty(ChangshaGameStateMachine.GetAddedKongCandidates(state, 0));
        Assert.True(before == JsonSerializer.Serialize(state));

        foreach (var tiles in new[] { new[] { 0, 1, 2 }, new[] { 16, 17, 18 } })
        {
            foreach (var tile in tiles)
                state.Hands[0].ConcealedTiles.Remove(tile);
            state.Hands[0].Melds.Add(new Meld
            {
                Kind = MeldKind.Pung,
                TileIds = tiles.ToList(),
                ClaimedFromSeatIndex = 3
            });
        }
        before = JsonSerializer.Serialize(state);
        Assert.Equal(new[] { 3, 19 }, ChangshaGameStateMachine.GetAddedKongCandidates(state, 0));
        Assert.True(ChangshaGameStateMachine.CanDeclareAddedKong(state, 0, 3));
        Assert.False(ChangshaGameStateMachine.CanDeclareAddedKong(state, 1, 3));
        Assert.False(ChangshaGameStateMachine.CanDeclareAddedKong(state, 0, 2));
        Assert.True(before == JsonSerializer.Serialize(state));
        AssertDeck(state);
    }

    [Theory]
    [InlineData(TableClaimType.Hu)]
    [InlineData(TableClaimType.Pung)]
    [InlineData(TableClaimType.Kong)]
    public void InvalidClaim_ValidatesBeforeRiverOrHandMutation(TableClaimType claim)
    {
        var state = Dealt();
        PlaceHand(state, 1, [4, 8, 12, 16, 20, 32, 36, 40, 44, 52, 56, 84, 92]);
        SwapInto(state, state.Hands[0].ConcealedTiles, 0, 0);
        ChangshaGameStateMachine.Discard(state, 0, 0);
        Assert.NotNull(state.ClaimWindow);
        var before = JsonSerializer.Serialize(state);

        Assert.Throws<InvalidOperationException>(() => ChangshaGameStateMachine.ResolveClaim(state, 1, claim));

        Assert.True(before == JsonSerializer.Serialize(state));
        AssertDeck(state);
    }

    [Theory]
    [MemberData(nameof(Section51GoldenTests.Examples), MemberType = typeof(Section51GoldenTests))]
    public void BaseUnit_ScalesEveryCanonicalPaymentAndDeltaOnce(Section51GoldenTests.GoldenCase example)
    {
        foreach (var unit in new[] { 1, 10, 100, ChangshaBaseUnit.MaxValue })
        {
            var state = Dealt(baseUnit: unit);
            state.DealerSeatIndex = example.DealerSeat;
            state.CurrentWin = new WinResult
            {
                WinningSeatIndex = example.WinnerSeat,
                SourceSeatIndex = example.SourceSeat,
                Method = example.Method,
                Pattern = example.Pattern,
                WinningTileId = 0,
                IsFullFlush = example.Pattern == WinPattern.FullFlush
            };
            state.Phase = ChangshaPhase.Scoring;
            var raw = new ScoringService().CalculateScore(state.CurrentWin, example.DealerSeat, state.CurrentWin.IsFullFlush);
            var scaled = new ScoringService().CalculateScore(
                state.CurrentWin, example.DealerSeat, state.CurrentWin.IsFullFlush, 1, unit);

            ChangshaGameStateMachine.Score(state);

            Assert.Equal(example.ExpectedCategory, state.CurrentScore!.Category);
            Assert.Equal(raw.Payments.Select(payment => checked(payment.Amount * unit)),
                state.CurrentScore.Payments.Select(payment => payment.Amount));
            Assert.Equal(scaled.Payments.Select(payment => payment.Amount),
                state.CurrentScore.Payments.Select(payment => payment.Amount));
            for (var seat = 0; seat < 4; seat++)
                Assert.Equal(checked(example.ExpectedDeltas[seat] * unit), state.CumulativeScores[seat]);
            Assert.Equal(state.CurrentScore.Payments.Sum(payment => payment.Amount), state.CurrentScore.BasePoints);
            Assert.Equal(0L, state.CumulativeScores.Values.Sum(value => (long)value));
            var restored = RoundTrip(state);
            Assert.Equal(unit, restored.BaseUnit);
            Assert.Equal(state.CumulativeScores, restored.CumulativeScores);
            AssertDeck(restored);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(ChangshaBaseUnit.MaxValue)]
    public void BaseUnit_MaximumCanonicalSixteenHandMatch_RemainsExactThroughPersistence(int unit)
    {
        var state = Dealt(baseUnit: unit);
        state.MaxHands = 16;
        for (var hand = 1; hand <= 16; hand++)
        {
            if (hand > 1)
            {
                Assert.Null(state.LastDrawSeatIndex);
                ChangshaGameStateMachine.RollDice(state, new DiceService(hand));
                ChangshaGameStateMachine.Deal(state);
            }
            Assert.Equal(0, state.DiscardsThisHand);
            PlaceHand(state, 0, SevenPairs);
            ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
            ChangshaGameStateMachine.Score(state);
            Assert.Equal(checked(12 * unit), state.CurrentScore!.BasePoints);
            Assert.Equal(checked(hand * 12 * unit), state.CumulativeScores[0]);
            Assert.Equal(4, state.CumulativeScores.Count);
            Assert.Equal(0L, state.CumulativeScores.Values.Sum(value => (long)value));
            AssertDeck(state);
            state = RoundTrip(state);
            ChangshaGameStateMachine.RotateBanker(state);
            Assert.Equal(unit, state.BaseUnit);
        }
        Assert.True(state.IsGameComplete);
        Assert.Equal(ChangshaPhase.GameComplete, state.Phase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BaseUnit_ScalesOnlyMoney_OnceAfterExistingOptionalFanFolding(bool houseRules)
    {
        var options = houseRules ? ChangshaScoringOptions.HouseRules : ChangshaScoringOptions.SpecPure;
        var baseline = Dealt();
        var scaled = Dealt(baseUnit: 100);
        foreach (var state in new[] { baseline, scaled })
        {
            PlaceHand(state, 0, SevenPairs);
            ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
            ChangshaGameStateMachine.Score(state, options);
        }
        Assert.Equal(baseline.CurrentScore!.FanPoints, scaled.CurrentScore!.FanPoints);
        Assert.Equal(baseline.CurrentScore.Payments.Select(payment => payment.Amount * 100),
            scaled.CurrentScore.Payments.Select(payment => payment.Amount));
        Assert.Equal(baseline.CurrentScore.Payments.Select(payment => payment.Reason),
            scaled.CurrentScore.Payments.Select(payment => payment.Reason));
        Assert.Equal(baseline.CurrentScore.BasePoints * 100, scaled.CurrentScore.BasePoints);
        Assert.Equal(0L, scaled.CumulativeScores.Values.Sum(value => (long)value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ChangshaBaseUnit.MaxValue + 1)]
    [InlineData(int.MaxValue)]
    public void BaseUnit_InvalidCreationAndScoringValues_AreRejected(int unit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChangshaGameStateMachine.CreateGame(42, baseUnit: unit));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScoringService().CalculateFalseHuPenalty(0, unit));
    }

    [Fact]
    public void ScoreOverflow_LeavesEntireStateUnchanged()
    {
        var state = Dealt();
        PlaceHand(state, 0, SevenPairs);
        ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
        state.CumulativeScores[0] = int.MaxValue - 5;
        state.CumulativeScores[1] = -(int.MaxValue - 5);
        var before = JsonSerializer.Serialize(state);

        Assert.Throws<OverflowException>(() => ChangshaGameStateMachine.Score(state));

        Assert.True(before == JsonSerializer.Serialize(state));
        Assert.Null(state.CurrentScore);
    }

    [Fact]
    public void FalseHu_BaseUnitScalesAllThreePayments_AndOverflowIsAtomic()
    {
        var state = Dealt(baseUnit: 10);
        var penalty = ChangshaGameStateMachine.RecordFalseHu(state, 0);
        Assert.Equal(60, penalty.PenaltyPerOpponent);
        Assert.Equal(-180, state.CumulativeScores[0]);
        Assert.Equal(0L, state.CumulativeScores.Values.Sum(value => (long)value));

        state.CumulativeScores[0] = int.MinValue + 17;
        state.CumulativeScores[1] = -(int.MinValue + 17);
        state.CumulativeScores[2] = state.CumulativeScores[3] = 0;
        var before = JsonSerializer.Serialize(state);
        Assert.Throws<OverflowException>(() => ChangshaGameStateMachine.RecordFalseHu(state, 0));
        Assert.True(before == JsonSerializer.Serialize(state));
    }

    private static ChangshaGameState Dealt(bool manual = false, int baseUnit = 1)
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(42, [], baseUnit);
        ChangshaGameStateMachine.StartGame(state);
        if (manual)
        {
            ChangshaGameStateMachine.BeginManualDeal(state, new DiceRoll(1, 4));
            while (ChangshaGameStateMachine.IsPickupPhase(state.Phase))
                ChangshaGameStateMachine.TakeTilesFromWall(state, state.PickupSeatIndex!.Value,
                    ChangshaGameStateMachine.ExpectedPickupCount(state.Phase));
        }
        else
        {
            ChangshaGameStateMachine.RollDice(state, new DiceService(42));
            ChangshaGameStateMachine.Deal(state);
        }
        AssertDeck(state);
        return state;
    }

    private static void PlaceHand(ChangshaGameState state, int seat, int[] tiles)
    {
        var hand = state.Hands[seat].ConcealedTiles;
        Assert.Equal(hand.Count, tiles.Length);
        for (var index = 0; index < tiles.Length; index++)
            SwapInto(state, hand, index, tiles[index]);
        AssertDeck(state);
    }

    private static void SwapInto(ChangshaGameState state, List<int> target, int index, int tile)
    {
        var source = state.Hands.Select(hand => hand.ConcealedTiles).Append(state.Wall)
            .Single(tiles => tiles.Contains(tile));
        var sourceIndex = source.IndexOf(tile);
        (source[sourceIndex], target[index]) = (target[index], source[sourceIndex]);
    }

    private static ChangshaGameState RoundTrip(ChangshaGameState state) =>
        JsonSerializer.Deserialize<ChangshaGameState>(JsonSerializer.Serialize(state))
        ?? throw new InvalidOperationException("State round-trip returned null.");

    private static void AssertDeck(ChangshaGameState state) =>
        Assert.True(Enumerable.Range(0, 108).SequenceEqual(state.Wall
            .Concat(state.DiscardPile.Select(discard => discard.TileId))
            .Concat(state.Hands.SelectMany(hand =>
                hand.ConcealedTiles.Concat(hand.Melds.SelectMany(meld => meld.TileIds))))
            .Order()), "Every physical tile must have exactly one owner.");
}
