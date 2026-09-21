using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;
using static Mahjong.Autotable.Api.Tests.Changsha.Acceptance.AcceptanceFixture;
using static Mahjong.Autotable.Api.Tests.Changsha._TestHarness.ChangshaTestHelpers;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

public sealed class CanonicalRuleQualificationTests
{
    public static IEnumerable<object[]> DealerDiceAndHandCases()
    {
        for (var dealer = 0; dealer < 4; dealer++)
        for (var sum = 2; sum <= 12; sum++)
        foreach (var hand in new[] { 1, 2, 16 })
            yield return new object[] { dealer, sum, hand };
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "DealEquivalence")]
    [MemberData(nameof(DealerDiceAndHandCases))]
    public void ManualAndAuto_ConvergeOnExactTiles_ForEveryDealerDiceAndHand(
        int dealer, int diceSum, int handNumber)
    {
        var automatic = NewDealPosition(dealer, handNumber);
        var manual = NewDealPosition(dealer, handNumber);
        var firstDie = Math.Min(6, diceSum - 1);
        var roll = new DiceRoll(firstDie, diceSum - firstDie);

        ChangshaGameStateMachine.RollDice(automatic, new FixedDiceService(roll));
        ChangshaGameStateMachine.Deal(automatic);
        ChangshaGameStateMachine.BeginManualDeal(manual, roll);
        var pickups = 0;
        while (ChangshaGameStateMachine.IsPickupPhase(manual.Phase))
        {
            Assert.True(pickups < 17, "The canonical ceremony must finish in 17 pickups.");
            var picker = manual.PickupSeatIndex
                ?? throw new InvalidOperationException("A pickup phase must identify its actor.");
            ChangshaGameStateMachine.TakeTilesFromWall(
                manual, picker,
                ChangshaGameStateMachine.ExpectedPickupCount(manual.Phase));
            pickups++;
        }

        Assert.Equal(17, pickups);
        Assert.Equal(automatic.BreakPoint, manual.BreakPoint);
        Assert.Equal(automatic.LastDiceRoll, manual.LastDiceRoll);
        Assert.Equal(automatic.Wall, manual.Wall);
        Assert.Equal(55, manual.Wall.Count);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, manual.Phase);
        Assert.Equal(dealer, manual.ActiveSeatIndex);
        Assert.Null(manual.PickupSeatIndex);
        Assert.Equal(0, manual.WallBackDrawn);
        for (var seat = 0; seat < 4; seat++)
        {
            Assert.Equal(automatic.Hands[seat].ConcealedTiles, manual.Hands[seat].ConcealedTiles);
            Assert.Equal(seat == dealer ? 14 : 13, manual.Hands[seat].ConcealedTiles.Count);
        }
        AssertCompleteDeck(automatic);
        AssertCompleteDeck(manual);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "RobbingKongResume")]
    public void AddedKong_AllHuPass_RestoresDiscardPhaseAndPermitsDiscard()
    {
        var state = NewKongPosition();
        var replacement = state.Wall[^1];
        ChangshaGameStateMachine.DeclareAddedKong(state, 0, 19);
        Assert.Equal(ChangshaPhase.AwaitingClaim, state.Phase);
        Assert.True(state.ClaimWindow!.IsKongRobbing);

        ChangshaGameStateMachine.PassClaim(state);

        Assert.Null(state.ClaimWindow);
        Assert.Equal(MeldKind.AddedKong, Assert.Single(state.Hands[0].Melds).Kind);
        Assert.Contains(replacement, state.Hands[0].ConcealedTiles);
        Assert.Equal(1, state.WallBackDrawn);
        AssertCompleteDeck(state);
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        ChangshaGameStateMachine.Discard(state, 0, replacement);
        Assert.DoesNotContain(replacement, state.Hands[0].ConcealedTiles);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "RobbingKongConservation")]
    public void RobbedAddedKong_TransfersFourthTileOnce_AndPreserves108Tiles()
    {
        var state = NewKongPosition();
        ChangshaGameStateMachine.DeclareAddedKong(state, 0, 19);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            opportunity => opportunity.SeatIndex == 1 && opportunity.ClaimType == TableClaimType.Hu);

        ChangshaGameStateMachine.ResolveClaim(state, 1, TableClaimType.Hu);

        Assert.Equal(WinMethod.RobbingKong, state.CurrentWin!.Method);
        Assert.Equal(0, state.CurrentWin.SourceSeatIndex);
        Assert.Equal(MeldKind.Pung, Assert.Single(state.Hands[0].Melds).Kind);
        Assert.Contains(19, state.Hands[1].ConcealedTiles);
        Assert.Equal(0, state.WallBackDrawn);
        Assert.DoesNotContain(19, state.Hands[0].ConcealedTiles);
        AssertCompleteDeck(state);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "PungChoice")]
    public void ThreeMatchingTiles_OfferPungAsWellAsExposedKong()
    {
        var state = NewPosition(new Dictionary<int, int[]>
        {
            [0] = [33],
            [1] = [32, 34, 35]
        });
        ChangshaGameStateMachine.Discard(state, 0, 33);
        var opportunities = state.ClaimWindow!.Opportunities;

        Assert.Contains(opportunities, o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Kong);
        Assert.Contains(opportunities, o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Pung);
        ChangshaGameStateMachine.ResolveClaim(state, 1, TableClaimType.Pung);
        Assert.Single(state.Hands[1].ConcealedTiles, t => t / 4 == 8);
        AssertCompleteDeck(state);
    }

    [Theory, Trait("Category", "RulesQualification"), Trait("Rule", "RejectedChowAtomicity")]
    [InlineData("not-held")]
    [InlineData("different-suit")]
    [InlineData("not-sequential")]
    [InlineData("duplicate")]
    [InlineData("wrong-count")]
    public void RejectedExplicitChow_DoesNotRemoveDiscardOrChangeState(string invalidChoice)
    {
        var state = NewPosition(new Dictionary<int, int[]>
        {
            [0] = [9],
            [1] = [4, 12, 16, 28, 48]
        });
        ChangshaGameStateMachine.Discard(state, 0, 9);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Chow);
        int[] choice = invalidChoice switch
        {
            "not-held" => [state.Hands[2].ConcealedTiles[0], 4],
            "different-suit" => [12, 48],
            "not-sequential" => [12, 28],
            "duplicate" => [12, 12],
            "wrong-count" => [12],
            _ => throw new ArgumentOutOfRangeException(nameof(invalidChoice))
        };
        var before = JsonSerializer.Serialize(state);

        var error = Assert.Throws<TableRuleException>(() =>
            ChangshaGameStateMachine.ResolveClaim(state, 1, TableClaimType.Chow, choice));

        Assert.Equal(TableActionErrorCodes.ChowTilesInvalid, error.Code);
        Assert.Equal(before, JsonSerializer.Serialize(state));
        AssertCompleteDeck(state);
    }

    [Fact, Trait("Category", "RulesQualification"), Trait("Rule", "EarthlyHandHistory")]
    public void DealerDiscardAfterTwoPungClaims_IsNotEarthlyHand()
    {
        var state = NewPosition(new Dictionary<int, int[]>
        {
            [0] = [33, 57, 58, 19],
            [1] = WanFiveWait(),
            [2] = [34, 35, 56]
        });

        ChangshaGameStateMachine.Discard(state, 0, 33);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 2 && o.ClaimType == TableClaimType.Pung);
        ChangshaGameStateMachine.ResolveClaim(state, 2, TableClaimType.Pung);
        ChangshaGameStateMachine.Discard(state, 2, 56);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 0 && o.ClaimType == TableClaimType.Pung);
        ChangshaGameStateMachine.ResolveClaim(state, 0, TableClaimType.Pung);
        ChangshaGameStateMachine.Discard(state, 0, 19);

        Assert.Equal(3, state.EventLog.Count(e => e.EventType == "tile-discarded"));
        Assert.Single(state.DiscardPile);
        Assert.Contains(state.ClaimWindow!.Opportunities,
            o => o.SeatIndex == 1 && o.ClaimType == TableClaimType.Hu);
        AssertCompleteDeck(state);
        ChangshaGameStateMachine.ResolveClaim(state, 1, TableClaimType.Hu);
        ChangshaGameStateMachine.Score(state);

        Assert.Equal(2, state.CurrentScore!.BasePoints);
        Assert.DoesNotContain(WinPattern.EarthlyHand, state.CurrentWin!.AllPatterns);
        AssertCompleteDeck(state);
    }

    [Theory, Trait("Category", "RulesQualificationGap"), Trait("Rule", "OwnTurnActionAvailability")]
    [InlineData("self-draw")]
    [InlineData("concealed-kong")]
    [InlineData("added-kong")]
    public void CurrentClaimChannel_OmitsOtherwiseLegalOwnTurnActions(string action)
    {
        ChangshaGameState state;
        if (action == "self-draw")
        {
            var winning = ThirteenTileWaitingForWan1();
            var drawnTile = Tid(Suit.Wan, 1);
            winning.Add(drawnTile);
            state = NewPosition(new Dictionary<int, int[]> { [0] = winning.ToArray() });
            Assert.True(state.Hands[0].ConcealedTiles.Remove(drawnTile));
            state.Wall.Insert(0, drawnTile);
            state.WallBackIndex = state.Wall.Count - 1;
            Assert.Equal(13, state.Hands[0].ConcealedTiles.Count);
            Assert.Null(state.LastDrawSeatIndex);
            Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(state, 0));
            AssertCompleteDeck(state);

            // The legacy claim-channel check below is separate from the new ownTurn route.
            var draws = ChangshaGameStateMachine.DrawTile(state);
            var draw = Assert.Single(draws, entry => entry.EventType == "tile-drawn");
            Assert.Equal(0, draw.SeatIndex);
            Assert.Equal(drawnTile, draw.TileId);
            Assert.Equal(14, state.Hands[0].ConcealedTiles.Count);
            Assert.Equal(drawnTile, state.Hands[0].ConcealedTiles[^1]);
            Assert.Equal(0, state.LastDrawSeatIndex);
            AssertCompleteDeck(state);
            Assert.True(new ChangshaWinDetector().Detect(state.Hands[0]).IsWin);
        }
        else
        {
            state = NewKongPosition(concealed: action == "concealed-kong");
        }

        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var ownClaim = Assert.Single(entries,
            entry => entry.Kind == "claim" && Convert.ToString(entry.Key) == "0");
        Assert.Null(ownClaim.Value);

        // These are gap characterizations, not assertions that missing UI actions conform.
        if (action == "self-draw")
        {
            ChangshaGameStateMachine.DeclareSelfDrawWin(state, 0);
            Assert.Equal(WinMethod.SelfDraw, state.CurrentWin!.Method);
        }
        else if (action == "concealed-kong")
        {
            ChangshaGameStateMachine.DeclareConcealedKong(state, 0, 4);
            Assert.Equal(MeldKind.ConcealedKong, Assert.Single(state.Hands[0].Melds).Kind);
        }
        else
        {
            ChangshaGameStateMachine.DeclareAddedKong(state, 0, 19);
            Assert.True(state.ClaimWindow!.IsKongRobbing);
        }
    }

    [Fact, Trait("Category", "RulesQualificationGap"), Trait("Rule", "ConcealedKongPresentation")]
    public void CurrentConcealedKongTranslation_EmitsFourFaceDownTiles()
    {
        var state = NewKongPosition(concealed: true);
        ChangshaGameStateMachine.DeclareConcealedKong(state, 0, 4);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var meldTiles = entries.Where(e => e.Kind == "things")
            .Select(e => Assert.IsType<ThingInfo>(e.Value))
            .Where(t => t.SlotName.StartsWith("meld.0.", StringComparison.Ordinal)
                && t.SlotName.EndsWith("@0", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(4, meldTiles.Count);
        Assert.All(meldTiles, tile => Assert.Equal(2, tile.RotationIndex));
    }

    private static ChangshaGameState NewDealPosition(int dealer, int handNumber)
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(4242, []);
        state.DealerSeatIndex = dealer;
        state.HandNumber = handNumber;
        state.MaxHands = 16;
        state.HandInRound = (handNumber - 1) % 4 + 1;
        state.RoundNumber = (handNumber - 1) / 4 + 1;
        state.RoundWind = (Wind)(state.RoundNumber - 1);
        foreach (var seat in state.Seats)
            seat.IsDealer = seat.SeatIndex == dealer;
        ChangshaGameStateMachine.StartGame(state);
        return state;
    }

    private static int[] WanFiveWait() =>
        [0, 4, 8, 12, 20, 24, 28, 32, 72, 73, 74, 88, 89];

    private static ChangshaGameState NewKongPosition(bool concealed = false)
    {
        int[] filler = [40, 41, 42, 44, 45, 46, 48, 49, 50, 52];
        var ownTiles = concealed
            ? new[] { 16, 17, 18, 19 }.Concat(filler).ToArray()
            : filler.Append(19).ToArray();
        var melds = concealed
            ? new Dictionary<int, Meld[]>()
            : new Dictionary<int, Meld[]>
            {
                [0] =
                [
                    new Meld
                    {
                        Kind = MeldKind.Pung,
                        TileIds = [16, 17, 18],
                        ClaimedFromSeatIndex = 3
                    }
                ]
            };
        return NewPosition(new Dictionary<int, int[]> { [0] = ownTiles, [1] = WanFiveWait() }, melds);
    }

    private static ChangshaGameState NewPosition(
        IReadOnlyDictionary<int, int[]> concealed,
        IReadOnlyDictionary<int, Meld[]>? melds = null)
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(42, []);
        state.Phase = ChangshaPhase.AwaitingDiscard;
        state.ActiveSeatIndex = 0;
        state.LastDiceRoll = new DiceRoll(1, 4);
        state.BreakPoint = new BreakPointService().ComputeBreakPoint(5, 0);
        foreach (var (seat, tiles) in concealed)
            state.Hands[seat].ConcealedTiles.AddRange(tiles);
        if (melds is not null)
            foreach (var (seat, sets) in melds)
                state.Hands[seat].Melds.AddRange(sets);

        var used = state.Hands.SelectMany(h =>
            h.ConcealedTiles.Concat(h.Melds.SelectMany(m => m.TileIds))).ToList();
        Assert.Equal(used.Count, used.Distinct().Count());
        Assert.All(used, tile => Assert.InRange(tile, 0, 107));
        var unassigned = new Queue<int>(Enumerable.Range(0, 108).Except(used));
        foreach (var hand in state.Hands)
        {
            var expected = 13 - 3 * hand.Melds.Count + (hand.SeatIndex == 0 ? 1 : 0);
            Assert.True(hand.ConcealedTiles.Count <= expected);
            while (hand.ConcealedTiles.Count < expected)
                hand.ConcealedTiles.Add(unassigned.Dequeue());
        }
        state.Wall = unassigned.ToList();
        state.WallBackIndex = state.Wall.Count - 1;
        AssertCompleteDeck(state);
        return state;
    }

    private static void AssertCompleteDeck(ChangshaGameState state)
    {
        var tiles = state.Wall
            .Concat(state.DiscardPile.Select(d => d.TileId))
            .Concat(state.Hands.SelectMany(h =>
                h.ConcealedTiles.Concat(h.Melds.SelectMany(m => m.TileIds))))
            .OrderBy(tile => tile)
            .ToArray();
        Assert.Equal(Enumerable.Range(0, 108), tiles);
    }

    private sealed class FixedDiceService(DiceRoll roll) : IDiceService
    {
        public DiceRoll Roll() => roll;
    }
}
