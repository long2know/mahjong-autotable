using System.Text.Json;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.RulesQualification;

[Trait("Category", "RulesQualification")]
[Trait("Rule", "KongEffectiveHandCount")]
public sealed class DrakeKongEffectiveHandEligibilityTests
{
    private const string ChowPrefix =
        "D:0:5 T:1 D:1:2 C:2:Chow D:2:13 T:3 D:3:1 T:0 D:0:10 C:1:Chow";
    private const string ConcealedOnePreDraw =
        ChowPrefix + " D:1:23 T:2 D:2:19 T:3 D:3:9 T:0 D:0:28";
    private const string AddedOnePreDraw =
        ConcealedOnePreDraw + " T:1 D:1:25 C:0:Pung D:0:44 T:1 D:1:45 T:2 D:2:21 T:3 D:3:11 T:0"
        + " D:0:60 C:3:Pung D:3:20";
    private const string ConcealedTwoPreDraw =
        "D:0:23 T:1 D:1:5 C:2:Chow D:2:16 C:0:Pung D:0:32 T:1 D:1:11 C:0:Pung"
        + " D:0:43 C:1:Chow D:1:25 T:2 D:2:44 C:1:Pung D:1:40 T:2 D:2:68 T:3 D:3:4 T:0"
        + " D:0:56 C:2:Pung D:2:79 T:3 D:3:12 T:0 D:0:62 C:2:Pung D:2:83 T:3 D:3:19";
    private const string AddedTwoPreDraw =
        "D:0:0 C:3:Pung D:3:14 C:0:Chow D:0:27 C:1:Chow D:1:29 C:2:Chow D:2:18 T:3"
        + " D:3:25 T:0 D:0:30 T:1 D:1:48 C:3:Pung D:3:34 T:0 D:0:50 T:1 D:1:74 T:2 D:2:11 T:3"
        + " D:3:57 T:0 D:0:10 T:1 D:1:8 T:2 D:2:46";
    private const string BothKongsAfterPung =
        "D:0:7 T:1 D:1:12 C:2:Chow D:2:14 C:3:Chow D:3:0 C:1:Pung";
    private const string ConcealedAfterTwoClaims =
        "D:0:6 C:1:Pung D:1:22 T:2 D:2:4 T:3 D:3:1 T:0 D:0:17 T:1 D:1:61 C:2:Chow"
        + " D:2:33 T:3 D:3:16 T:0 D:0:46 C:1:Pung";
    private const string AddedAfterChow =
        "D:0:11 T:1 D:1:8 T:2 D:2:19 C:3:Chow D:3:3 T:0 D:0:22 T:1 D:1:26 T:2"
        + " D:2:27 T:3 D:3:17 T:0 D:0:30 C:3:Pung D:3:48 C:1:Pung D:1:42 T:2 D:2:34"
        + " T:3 D:3:6 C:0:Pung D:0:32 T:1 D:1:2 T:2 D:2:45 T:3 D:3:58 C:0:Chow";

    [Theory]
    [InlineData("concealed-0")]
    [InlineData("concealed-1")]
    [InlineData("concealed-2")]
    [InlineData("added-1")]
    [InlineData("added-2")]
    public void NaturalPreDraw_QueriesAreEmptyAndPure(string position)
    {
        var fixture = PreDrawPosition(position);
        var state = Replay(fixture.Seed, fixture.Steps);
        AssertPreDraw(state, fixture);
        AssertPhysicalCandidate(state, fixture);
        var before = JsonSerializer.Serialize(state);

        Assert.Empty(ChangshaGameStateMachine.GetConcealedKongCandidates(state, fixture.Seat));
        Assert.Empty(ChangshaGameStateMachine.GetAddedKongCandidates(state, fixture.Seat));
        Assert.False(CanDeclare(state, fixture));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        AssertCompleteDeck(state);
    }

    [Theory]
    [InlineData("concealed-0")]
    [InlineData("concealed-1")]
    [InlineData("concealed-2")]
    [InlineData("added-1")]
    [InlineData("added-2")]
    public void NaturalPreDraw_DeclarationRejectsAtomically(string position)
    {
        var fixture = PreDrawPosition(position);
        var state = Replay(fixture.Seed, fixture.Steps);
        AssertPreDraw(state, fixture);
        AssertPhysicalCandidate(state, fixture);
        var before = JsonSerializer.Serialize(state);

        Assert.Throws<InvalidOperationException>(() => Declare(state, fixture));

        Assert.Equal(before, JsonSerializer.Serialize(state));
        AssertHandSize(state, fixture.Seat, fixture.Concealed, fixture.Groups, 13);
        AssertCompleteDeck(state);
    }

    [Theory]
    [InlineData("concealed-0")]
    [InlineData("concealed-1")]
    [InlineData("concealed-2")]
    [InlineData("added-1")]
    [InlineData("added-2")]
    public void NaturalFrontDraw_EnablesKongAndOneReplacementThenLegalDiscard(string position)
    {
        var fixture = PreDrawPosition(position);
        var state = Replay(fixture.Seed, fixture.Steps);
        AssertPreDraw(state, fixture);
        var front = state.Wall[0];

        ChangshaGameStateMachine.DrawTile(state);

        Assert.Equal(front, state.Hands[fixture.Seat].ConcealedTiles[^1]);
        Assert.Equal(fixture.Seat, state.LastDrawSeatIndex);
        AssertHandSize(state, fixture.Seat, fixture.Concealed + 1, fixture.Groups, 14);
        var replacement = DeclareAndAssertReplacement(state, fixture);
        AssertLegalDiscard(state, fixture.Seat, replacement);
    }

    [Theory]
    [InlineData("concealed-0")]
    [InlineData("concealed-1")]
    [InlineData("concealed-2")]
    [InlineData("added-1")]
    [InlineData("added-2")]
    public void ExtraPublicDraw_DoesNotMakeAnOverfullHandEligible(string position)
    {
        var fixture = PreDrawPosition(position);
        var state = Replay(fixture.Seed, fixture.Steps);
        ChangshaGameStateMachine.DrawTile(state);
        ChangshaGameStateMachine.DrawTile(state);
        Assert.Equal(fixture.Seat, state.LastDrawSeatIndex);
        AssertHandSize(state, fixture.Seat, fixture.Concealed + 2, fixture.Groups, 15);
        AssertPhysicalCandidate(state, fixture);
        var before = JsonSerializer.Serialize(state);

        Assert.Empty(ChangshaGameStateMachine.GetConcealedKongCandidates(state, fixture.Seat));
        Assert.Empty(ChangshaGameStateMachine.GetAddedKongCandidates(state, fixture.Seat));
        Assert.False(CanDeclare(state, fixture));
        Assert.Throws<InvalidOperationException>(() => Declare(state, fixture));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        AssertCompleteDeck(state);
    }

    [Theory]
    [InlineData("concealed-pung")]
    [InlineData("concealed-chow")]
    [InlineData("concealed-two-pungs")]
    [InlineData("added-pung")]
    [InlineData("added-after-chow")]
    public void NaturalPostClaim_EffectiveFourteenPermitsKongWithoutAnOwnDraw(string position)
    {
        var fixture = PostClaimPosition(position);
        var state = Replay(fixture.Seed, fixture.Steps);
        Assert.Equal(fixture.Seat, state.ActiveSeatIndex);
        Assert.Null(state.LastDrawSeatIndex);
        AssertHandSize(state, fixture.Seat, fixture.Concealed, fixture.Groups, 14);

        var replacement = DeclareAndAssertReplacement(state, fixture);

        AssertLegalDiscard(state, fixture.Seat, replacement);
    }

    [Fact]
    public void FourPhysicalKongTiles_CountAsOneThreeTileMeldGroup()
    {
        var fixture = PostClaimPosition("concealed-pung");
        var state = Replay(fixture.Seed, fixture.Steps);
        Assert.Null(state.LastDrawSeatIndex);
        AssertHandSize(state, 1, 11, 1, 14);
        Assert.True(ChangshaGameStateMachine.CanDeclareAddedKong(state, 1, 3));

        DeclareAndAssertReplacement(state, fixture);

        AssertHandSize(state, 1, 8, 2, 14);
        Assert.Equal(7, state.Hands[1].Melds.Sum(m => m.TileIds.Count));
        var added = fixture with { Kind = MeldKind.AddedKong, Candidate = 3 };
        var replacement = DeclareAndAssertReplacement(state, added);
        AssertHandSize(state, 1, 8, 2, 14);
        Assert.All(state.Hands[1].Melds, meld => Assert.Equal(4, meld.TileIds.Count));
        AssertLegalDiscard(state, 1, replacement);
    }

    private sealed record Position(int Seed, string Steps, int Seat, MeldKind Kind, int Candidate, int Concealed, int Groups);

    private static Position PreDrawPosition(string position) => position switch
    {
        "concealed-0" => new(0, "D:0:56", 1, MeldKind.ConcealedKong, 16, 13, 0),
        "concealed-1" => new(0, ConcealedOnePreDraw, 1, MeldKind.ConcealedKong, 16, 10, 1),
        "concealed-2" => new(86, ConcealedTwoPreDraw, 0, MeldKind.ConcealedKong, 18, 7, 2),
        "added-1" => new(0, AddedOnePreDraw, 0, MeldKind.AddedKong, 26, 10, 1),
        "added-2" => new(17, AddedTwoPreDraw, 3, MeldKind.AddedKong, 3, 7, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };

    private static Position PostClaimPosition(string position) => position switch
    {
        "concealed-pung" => new(4886, BothKongsAfterPung, 1, MeldKind.ConcealedKong, 8, 11, 1),
        "concealed-chow" => new(0, ChowPrefix, 1, MeldKind.ConcealedKong, 16, 11, 1),
        "concealed-two-pungs" => new(451, ConcealedAfterTwoClaims, 1, MeldKind.ConcealedKong, 20, 8, 2),
        "added-pung" => new(4886, BothKongsAfterPung, 1, MeldKind.AddedKong, 3, 11, 1),
        "added-after-chow" => new(6, AddedAfterChow, 0, MeldKind.AddedKong, 7, 8, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };

    private static ChangshaGameState Replay(int seed, string steps)
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed);
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(42));
        ChangshaGameStateMachine.Deal(state);
        foreach (var step in steps.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = step.Split(':');
            switch (parts[0])
            {
                case "D":
                    var discarder = int.Parse(parts[1]);
                    Assert.Equal(discarder, state.ActiveSeatIndex);
                    Assert.Equal(14, EffectiveCount(state.Hands[discarder]));
                    ChangshaGameStateMachine.Discard(state, discarder, int.Parse(parts[2]));
                    break;
                case "T":
                    Assert.Equal(int.Parse(parts[1]), state.ActiveSeatIndex);
                    Assert.Equal(13, EffectiveCount(state.Hands[state.ActiveSeatIndex]));
                    ChangshaGameStateMachine.DrawTile(state);
                    break;
                case "C":
                    var claimant = int.Parse(parts[1]);
                    var claim = Enum.Parse<TableClaimType>(parts[2]);
                    Assert.Contains(state.ClaimWindow!.Opportunities,
                        opportunity => opportunity.SeatIndex == claimant && opportunity.ClaimType == claim);
                    ChangshaGameStateMachine.ResolveClaim(state, claimant, claim);
                    break;
                case "P":
                    ChangshaGameStateMachine.PassClaim(state);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown natural fixture step {step}");
            }
            AssertCompleteDeck(state);
        }
        if (state.Phase == ChangshaPhase.AwaitingClaim)
            ChangshaGameStateMachine.PassClaim(state);
        AssertCompleteDeck(state);
        return state;
    }

    private static void AssertPreDraw(ChangshaGameState state, Position fixture)
    {
        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(fixture.Seat, state.ActiveSeatIndex);
        Assert.Null(state.ClaimWindow);
        Assert.Null(state.LastDrawSeatIndex);
        AssertHandSize(state, fixture.Seat, fixture.Concealed, fixture.Groups, 13);
    }

    private static void AssertPhysicalCandidate(ChangshaGameState state, Position fixture)
    {
        var hand = state.Hands[fixture.Seat];
        if (fixture.Kind == MeldKind.ConcealedKong)
            Assert.Equal(4, hand.ConcealedTiles.Count(tile => tile / 4 == fixture.Candidate));
        else
        {
            Assert.Contains(fixture.Candidate, hand.ConcealedTiles);
            Assert.Contains(hand.Melds, meld => meld.Kind == MeldKind.Pung
                && meld.TileIds.Count == 3 && meld.TileIds.All(tile => tile / 4 == fixture.Candidate / 4));
        }
    }

    private static bool CanDeclare(ChangshaGameState state, Position fixture) =>
        fixture.Kind == MeldKind.ConcealedKong
            ? ChangshaGameStateMachine.CanDeclareConcealedKong(state, fixture.Seat, fixture.Candidate)
            : ChangshaGameStateMachine.CanDeclareAddedKong(state, fixture.Seat, fixture.Candidate);

    private static void Declare(ChangshaGameState state, Position fixture)
    {
        if (fixture.Kind == MeldKind.ConcealedKong)
            ChangshaGameStateMachine.DeclareConcealedKong(state, fixture.Seat, fixture.Candidate);
        else
            ChangshaGameStateMachine.DeclareAddedKong(state, fixture.Seat, fixture.Candidate);
    }

    private static int DeclareAndAssertReplacement(ChangshaGameState state, Position fixture)
    {
        var hand = state.Hands[fixture.Seat];
        var concealed = hand.ConcealedTiles.Count;
        var groups = hand.Melds.Count;
        var wall = state.Wall.ToArray();
        var backDrawn = state.WallBackDrawn;
        var before = JsonSerializer.Serialize(state);
        AssertPhysicalCandidate(state, fixture);
        Assert.True(CanDeclare(state, fixture));
        Assert.Equal(before, JsonSerializer.Serialize(state));

        Declare(state, fixture);
        if (state.ClaimWindow is not null)
        {
            Assert.True(state.ClaimWindow.IsKongRobbing);
            ChangshaGameStateMachine.PassClaim(state);
        }

        Assert.Equal(ChangshaPhase.AwaitingDiscard, state.Phase);
        Assert.Equal(fixture.Seat, state.ActiveSeatIndex);
        Assert.Null(state.ClaimWindow);
        Assert.Equal(fixture.Seat, state.LastDrawSeatIndex);
        Assert.True(state.LastDrawWasKongReplacement);
        Assert.Equal(backDrawn + 1, state.WallBackDrawn);
        Assert.Equal(wall[..^1], state.Wall);
        Assert.Equal(wall[^1], hand.ConcealedTiles[^1]);
        var concealedKong = fixture.Kind == MeldKind.ConcealedKong;
        AssertHandSize(state, fixture.Seat, concealed - (concealedKong ? 3 : 0), groups + (concealedKong ? 1 : 0), 14);
        Assert.Contains(hand.Melds, meld => meld.Kind == fixture.Kind && meld.TileIds.Count == 4);
        AssertCompleteDeck(state);
        return wall[^1];
    }

    private static void AssertLegalDiscard(ChangshaGameState state, int seat, int tile)
    {
        ChangshaGameStateMachine.Discard(state, seat, tile);
        Assert.DoesNotContain(tile, state.Hands[seat].ConcealedTiles);
        Assert.Contains(state.DiscardPile, discard => discard.SeatIndex == seat && discard.TileId == tile);
        Assert.Equal(13, EffectiveCount(state.Hands[seat]));
        AssertCompleteDeck(state);
    }

    private static int EffectiveCount(ChangshaHandState hand) => hand.ConcealedTiles.Count + 3 * hand.Melds.Count;

    private static void AssertHandSize(ChangshaGameState state, int seat, int concealed, int groups, int effective)
    {
        Assert.Equal(concealed, state.Hands[seat].ConcealedTiles.Count);
        Assert.Equal(groups, state.Hands[seat].Melds.Count);
        Assert.Equal(effective, EffectiveCount(state.Hands[seat]));
    }

    private static void AssertCompleteDeck(ChangshaGameState state)
    {
        var tiles = state.Wall
            .Concat(state.Hands.SelectMany(hand => hand.ConcealedTiles))
            .Concat(state.Hands.SelectMany(hand => hand.Melds).SelectMany(meld => meld.TileIds))
            .Concat(state.DiscardPile.Select(discard => discard.TileId))
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(0, 108), tiles);
    }
}
