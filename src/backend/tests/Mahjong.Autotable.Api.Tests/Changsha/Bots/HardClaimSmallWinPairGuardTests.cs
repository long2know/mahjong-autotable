using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Tables;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Changsha.Bots;

public class HardClaimSmallWinPairGuardTests
{
    private static int Wan(int rank, int copy = 0) => ((int)Suit.Wan * 9 + (rank - 1)) * 4 + copy;
    private static int Tong(int rank, int copy = 0) => ((int)Suit.Tong * 9 + (rank - 1)) * 4 + copy;
    private static int Tiao(int rank, int copy = 0) => ((int)Suit.Tiao * 9 + (rank - 1)) * 4 + copy;

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W25")]
    public void Hard_PassesChow_WhenClaimWorsensSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(2, 0),
                Wan(4, 0),
                Wan(6, 0), Wan(7, 0),
                Tong(1, 0), Tong(3, 0), Tong(4, 0),
                Tong(6, 0), Tong(7, 0),
                Tiao(1, 0), Tiao(3, 0), Tiao(4, 0), Tiao(6, 0)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Wan(3, 1), claimType: TableClaimType.Chow);

        var action = new HardStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W25")]
    public void Hard_StillClaimsChow_WhenClaimKeepsSmallWinPairDistance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 1,
            ConcealedTiles = new()
            {
                Wan(2, 0),
                Wan(4, 0),
                Wan(6, 0), Wan(7, 0),
                Tong(1, 0), Tong(2, 0), Tong(3, 0), Tong(4, 0), Tong(6, 0), Tong(7, 0),
                Tiao(1, 0), Tiao(3, 0), Tiao(4, 0)
            }
        };

        var state = BuildSingleClaimState(hand, discardTileId: Wan(3, 1), claimType: TableClaimType.Chow);

        var action = new HardStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Chow, action.ClaimType);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W25")]
    public void Hard_PassesPung_WhenClaimWorsensSmallWinPairDistance()
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

        var action = new HardStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W25")]
    public void Hard_StillClaimsPung_WhenClaimKeepsSmallWinPairDistance()
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

        var action = new HardStrategy().DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Pung, action.ClaimType);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W25")]
    public void Master_DelegatesPungGuard_AndPassesWhenClaimWorsensSmallWinPairDistance()
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

        var action = ChangshaBotEngine.Resolve("master").DecideAction(state, botSeatIndex: 1);

        Assert.Equal(BotActionType.Pass, action.Type);
    }

    private static ChangshaGameState BuildSingleClaimState(
        ChangshaHandState hand,
        int discardTileId,
        TableClaimType claimType)
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
                Opportunities = new()
                {
                    new()
                    {
                        SeatIndex = 1,
                        ClaimType = claimType,
                        Priority = ChangshaClaimPriority.TierOf(claimType)
                    }
                }
            }
        };
    }
}
