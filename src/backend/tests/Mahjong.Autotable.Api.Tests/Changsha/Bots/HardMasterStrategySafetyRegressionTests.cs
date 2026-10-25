using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Changsha.Bots;

public class HardMasterStrategySafetyRegressionTests
{
    private static int Wan(int rank, int copy = 0) => ((int)Suit.Wan * 9 + (rank - 1)) * 4 + copy;
    private static int Tong(int rank, int copy = 0) => ((int)Suit.Tong * 9 + (rank - 1)) * 4 + copy;
    private static int Tiao(int rank, int copy = 0) => ((int)Suit.Tiao * 9 + (rank - 1)) * 4 + copy;

    private static Meld FakePung(int logicalTile) => new()
    {
        Kind = MeldKind.Pung,
        TileIds = new List<int>
        {
            logicalTile * 4,
            logicalTile * 4 + 1,
            logicalTile * 4 + 2
        }
    };

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W28")]
    public void Hard_PrefersSingleDangerousOpponentGenbutsu_OnSameShantenWithinTolerance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(9, 0),  // unsafe best-score tile absent single-genbutsu promotion
                Tong(2, 0), // dangerous-opponent genbutsu
                Tiao(7, 0)
            }
        };

        var state = BuildSingleDangerousState(hand, Tong(2, 1));

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W28")]
    public void Master_PrefersSingleDangerousOpponentGenbutsu_OnSameShantenWithinTolerance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(9, 0),  // unsafe best-score tile absent single-genbutsu promotion
                Tong(2, 0), // dangerous-opponent genbutsu
                Tiao(7, 0)
            }
        };

        var state = BuildSingleDangerousState(hand, Tong(2, 1));

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W28")]
    public void Master_PromotesSingleDangerousOpponentGenbutsu_AfterShantenScoreSelection()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 2),
                Wan(4, 2),
                Wan(6, 0),
                Tong(2, 2),
                Tong(5, 2),
                Tong(7, 3),
                Tong(8, 2),
                Tong(9, 0),
                Tong(9, 1),
                Tiao(4, 3),
                Tiao(6, 1),
                Tiao(7, 1), // dangerous-opponent genbutsu
                Tiao(8, 1),
                Tiao(8, 3)
            }
        };

        var state = BuildSingleDangerousState(hand, Tiao(7, 3));

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tiao(7, 1), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Hard_KeepsFullDangerousGenbutsu_WhenDeadSmallWinFallbackIsOnlyEqualSafety()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead Changsha singleton (all copies visible)
                Tong(9, 0), // full dangerous-opponent genbutsu with better keep score
                Wan(4, 0)
            }
        };

        var state = BuildState(hand);

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(9, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Master_KeepsFullDangerousGenbutsu_WhenDeadSmallWinFallbackIsOnlyEqualSafety()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead Changsha singleton (all copies visible)
                Tong(9, 0), // full dangerous-opponent genbutsu with better keep score
                Wan(4, 0)
            }
        };

        var state = BuildState(hand);

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(9, 0), action.TileId);
    }

    private static ChangshaGameState BuildSingleDangerousState(ChangshaHandState hand, int dangerousGenbutsuTileId)
    {
        return new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new()
                {
                    SeatIndex = 1,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(2, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(6, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0)))
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = dangerousGenbutsuTileId, TurnNumber = 1 }
            }
        };
    }

    private static ChangshaGameState BuildState(ChangshaHandState hand)
    {
        return new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new()
                {
                    SeatIndex = 1,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(2, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(6, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // Full dangerous-opponent genbutsu tile (seat 1 + seat 2 both discarded)
                new() { SeatIndex = 1, TileId = Tong(9, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(9, 2), TurnNumber = 2 },

                // Dead Changsha singleton (all four copies visible)
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Tiao(2, 2), TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = Tiao(2, 3), TurnNumber = 5 }
            }
        };
    }
}
