using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Tables;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Changsha.Bots;

public class MediumClaimGuardTests
{
    private static int Wan(int rank, int copy = 0) => ((int)Suit.Wan * 9 + (rank - 1)) * 4 + copy;
    private static int Tong(int rank, int copy = 0) => ((int)Suit.Tong * 9 + (rank - 1)) * 4 + copy;
    private static int Tiao(int rank, int copy = 0) => ((int)Suit.Tiao * 9 + (rank - 1)) * 4 + copy;

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PassesPung_WhenClaimWorsensSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(6, 0), Wan(8, 0),
                Tong(2, 0), Tong(3, 0), Tong(9, 0), Tong(9, 1),
                Tiao(3, 0), Tiao(4, 0), Tiao(5, 0), Tiao(5, 1), Tiao(5, 2), Tiao(8, 0), Tiao(9, 0)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Tiao(5, 3), claimType: TableClaimType.Pung);

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PassesPung_WhenClaimWorsensShantenEvenIfSmallWinPairDistanceKeeps()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(5, 3), Tong(7, 2), Wan(4, 2),
                Tiao(4, 1), Tiao(7, 1),
                Tong(5, 0), Tong(6, 2), Tong(4, 1), Tong(8, 2),
                Wan(9, 3), Wan(2, 2), Tong(6, 3), Tiao(3, 3)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Tong(6, 0), claimType: TableClaimType.Pung);

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_StillClaimsPung_WhenClaimKeepsShantenAndSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(6, 0), Wan(8, 0),
                Tong(2, 0), Tong(3, 0), Tong(9, 0), Tong(9, 1),
                Tiao(3, 0), Tiao(4, 0), Tiao(5, 0), Tiao(5, 1), Tiao(5, 2), Tiao(8, 0), Tiao(9, 0)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Tong(9, 3), claimType: TableClaimType.Pung);

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PassesPung_WhenClaimKeepsShapeButDropsLiveWinningOuts()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(2, 0),
                Wan(3, 1), Wan(3, 2),
                Wan(4, 0), Wan(4, 3),
                Wan(6, 0),
                Tong(2, 0), Tong(4, 1), Tong(9, 1),
                Tiao(2, 2), Tiao(5, 1), Tiao(6, 0), Tiao(6, 3)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Wan(4, 1), claimType: TableClaimType.Pung);

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PrefersChowOverPung_WhenPungWorsensSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(5, 0), Wan(5, 1),
                Wan(4, 0), Wan(6, 0),
                Tong(1, 0), Tong(2, 0), Tong(3, 0),
                Tong(7, 0), Tong(8, 0), Tong(9, 0),
                Tiao(1, 0), Tiao(3, 0), Tiao(4, 0)
            }
        };

        var state = BuildClaimState(
            hand,
            discardTileId: Wan(5, 2),
            opportunities: new[] { TableClaimType.Pung, TableClaimType.Chow });

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Chow, action.ClaimType);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_StillPrioritizesPungOverChow_WhenBothKeepSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Tong(2, 0), Tong(2, 1), // stable 2/5/8 eye survives either claim
                Wan(9, 0), Wan(9, 1),
                Wan(8, 0), Wan(7, 0),
                Tong(4, 0), Tong(5, 0), Tong(6, 0),
                Tiao(1, 0), Tiao(3, 0), Tiao(4, 0), Tiao(6, 0)
            }
        };

        var state = BuildClaimState(
            hand,
            discardTileId: Wan(9, 2),
            opportunities: new[] { TableClaimType.Pung, TableClaimType.Chow });

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PrefersShantenReducingChowOverFlatPung_WhenBothKeepSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(4, 0),
                Wan(5, 1), Wan(5, 2),
                Wan(6, 2), Wan(6, 3),
                Wan(7, 0),
                Wan(8, 0), Wan(8, 3),
                Wan(9, 3),
                Tong(4, 2), Tong(9, 2),
                Tiao(2, 0), Tiao(4, 3)
            }
        };

        var state = BuildClaimState(
            hand,
            discardTileId: Wan(5, 3),
            opportunities: new[] { TableClaimType.Pung, TableClaimType.Chow });

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Chow, action.ClaimType);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PrefersBestChowPatternOverPung_WhenOnlyLaterChowShapeImprovesShanten()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(1, 1), Wan(6, 0), Wan(7, 3),
                Tong(3, 0), Tong(5, 2),
                Tong(6, 0), Tong(6, 3),
                Tong(7, 3), Tong(8, 0),
                Tiao(1, 3), Tiao(2, 1), Tiao(4, 3), Tiao(7, 0)
            }
        };

        var state = BuildClaimState(
            hand,
            discardTileId: Tong(6, 1),
            opportunities: new[] { TableClaimType.Pung, TableClaimType.Chow });

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Chow, action.ClaimType);
        Assert.NotNull(action.ClaimTileIds);
        Assert.Equal(2, action.ClaimTileIds!.Length);
        Assert.Equal(
            new[]
            {
                ChangshaDeckBuilder.GetLogicalTile(Tong(7, 0)),
                ChangshaDeckBuilder.GetLogicalTile(Tong(8, 0))
            },
            action.ClaimTileIds
                .Select(ChangshaDeckBuilder.GetLogicalTile)
                .OrderBy(logical => logical)
                .ToArray());
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W26")]
    public void Medium_PrefersChowWithMoreLiveOuts_WhenClaimShapeTiesWithPung()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(3, 0), Wan(7, 2), Wan(9, 2),
                Tong(2, 3), Tong(4, 3), Tong(5, 3),
                Tong(6, 1), Tong(6, 2), Tong(7, 3), Tong(8, 1), Tong(9, 0),
                Tiao(1, 1), Tiao(5, 1)
            }
        };

        var state = BuildClaimState(
            hand,
            discardTileId: Tong(6, 0),
            opportunities: new[] { TableClaimType.Pung, TableClaimType.Chow });

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Chow, action.ClaimType);
        Assert.NotNull(action.ClaimTileIds);
        Assert.Equal(2, action.ClaimTileIds!.Length);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W27")]
    public void Medium_PassesChow_WhenLiveOutsDropAfterCountingClaimedTilesInMeld()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(1, 0),
                Wan(3, 0), Wan(3, 2),
                Wan(6, 0),
                Wan(9, 3),
                Tong(5, 3), Tong(6, 0),
                Tiao(2, 3), Tiao(3, 3), Tiao(4, 0), Tiao(5, 2), Tiao(7, 1), Tiao(9, 0)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Tiao(2, 2), claimType: TableClaimType.Chow);

        var action = new MediumStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    private static ChangshaGameState BuildSingleClaimState(
        ChangshaHandState hand,
        int discardTileId,
        TableClaimType claimType)
    {
        return BuildClaimState(hand, discardTileId, new[] { claimType });
    }

    private static ChangshaGameState BuildClaimState(
        ChangshaHandState hand,
        int discardTileId,
        IReadOnlyList<TableClaimType> opportunities)
    {
        return new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingClaim,
            ActiveSeatIndex = 0,
            Hands = new()
            {
                new() { SeatIndex = 0 },
                hand,
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            ClaimWindow = new ChangshaClaimWindow
            {
                DiscardSeatIndex = 0,
                DiscardTileId = discardTileId,
                Opportunities = opportunities.Select(claimType => new ChangshaClaimOpportunity
                {
                    SeatIndex = 1,
                    ClaimType = claimType,
                    Priority = ChangshaClaimPriority.TierOf(claimType)
                }).ToList()
            }
        };
    }
}
