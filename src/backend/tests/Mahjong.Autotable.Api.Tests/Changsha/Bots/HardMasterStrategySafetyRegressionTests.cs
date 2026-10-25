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
                Tong(3, 0), // dangerous-opponent genbutsu (non-2/5/8)
                Tiao(7, 0)
            }
        };

        var state = BuildSingleDangerousState(hand, Tong(3, 1));

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(3, 0), action.TileId);
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
                Tong(3, 0), // dangerous-opponent genbutsu (non-2/5/8)
                Tiao(7, 0)
            }
        };

        var state = BuildSingleDangerousState(hand, Tong(3, 1));

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(3, 0), action.TileId);
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
    public void Hard_KeepsSingleDangerousGenbutsu_WhenDeadSmallWinFallbackIsOnlyEqualSafety()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton (all copies visible)
                Wan(1, 0),  // dangerous-opponent genbutsu with better keep score
                Tong(9, 0)
            }
        };

        var state = new ChangshaGameState
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
                // same-dangerous-opponent genbutsu for both candidates
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 2 },

                // complete dead visibility for the 2/5/8 singleton
                new() { SeatIndex = 2, TileId = Tiao(2, 2), TurnNumber = 3 },
                new() { SeatIndex = 3, TileId = Tiao(2, 3), TurnNumber = 4 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(1, 0), action.TileId);
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
    public void Master_KeepsSingleDangerousGenbutsu_WhenDeadSmallWinFallbackIsOnlyEqualSafety()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton (all copies visible)
                Wan(1, 0),  // dangerous-opponent genbutsu with better keep score
                Tong(9, 0)
            }
        };

        var state = new ChangshaGameState
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
                // same-dangerous-opponent genbutsu for both candidates
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 2 },

                // complete dead visibility for the 2/5/8 singleton
                new() { SeatIndex = 2, TileId = Tiao(2, 2), TurnNumber = 3 },
                new() { SeatIndex = 3, TileId = Tiao(2, 3), TurnNumber = 4 }
            }
        };

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(1, 0), action.TileId);
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

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W29")]
    public void Hard_PromotesDeadSmallWinSingleton_WhenDangerousPressureExistsButNoGenbutsuSafetyGain()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton (all copies visible)
                Wan(1, 0),
                Tong(9, 0)
            }
        };

        var state = BuildDangerousWithoutGenbutsuState(hand);

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tiao(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W29")]
    public void Master_PromotesDeadSmallWinSingleton_WhenDangerousPressureExistsButNoGenbutsuSafetyGain()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton (all copies visible)
                Wan(1, 0),
                Tong(9, 0)
            }
        };

        var state = BuildDangerousWithoutGenbutsuState(hand);

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tiao(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W30")]
    public void Hard_DoesNotPromoteLiveSmallWinSingleton_ForSingleDangerousGenbutsuTie()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(9, 0),
                Tong(2, 0), // live lone 2/5/8 singleton
                Tiao(7, 0)
            }
        };

        var state = BuildSingleDangerousState(hand, Tong(2, 1));

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W30")]
    public void Master_DoesNotPromoteLiveSmallWinSingleton_ForSingleDangerousGenbutsuTie()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(9, 0),
                Tong(2, 0), // live lone 2/5/8 singleton
                Tiao(7, 0)
            }
        };

        var state = BuildSingleDangerousState(hand, Tong(2, 1));

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W31")]
    public void Hard_DoesNotPromoteLiveSmallWinSingleton_ForFullDangerousGenbutsuTie()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(9, 0),
                Tong(2, 0), // live lone 2/5/8 singleton (only one copy remains live)
                Tiao(7, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W31")]
    public void Master_DoesNotPromoteLiveSmallWinSingleton_ForFullDangerousGenbutsuTie()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(9, 0),
                Tong(2, 0), // live lone 2/5/8 singleton (only one copy remains live)
                Tiao(7, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W33")]
    public void Hard_KeepsFullDangerousGenbutsu_WhenAnotherAlternateHasDangerousCoverage()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(5, 0),  // extra small-win tile keeps Tong(2) eligible for full-safety promotion
                Wan(1, 0),  // partial dangerous-opponent genbutsu alternate
                Tiao(9, 0)  // fully unsafe fallback with better shape score
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 1)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu coverage for Tong(2)
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // partial dangerous-opponent coverage exists on Wan(1)
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W33")]
    public void Master_KeepsFullDangerousGenbutsu_WhenAnotherAlternateHasDangerousCoverage()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(5, 0),  // extra small-win tile keeps Tong(2) eligible for full-safety promotion
                Wan(1, 0),  // partial dangerous-opponent genbutsu alternate
                Tiao(9, 0)  // fully unsafe fallback with better shape score
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 1)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu coverage for Tong(2)
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // partial dangerous-opponent coverage exists on Wan(1)
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 }
            }
        };

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W33")]
    public void Hard_KeepsFullDangerousGenbutsu_WhenCoveredAlternateIsShapeWorseThanUnsafeFallback()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(5, 0),  // extra small-win tile so Tong(2) can still be promoted on score tolerance
                Wan(1, 0), Wan(1, 1), // covered alternate that is structurally expensive to break
                Tiao(9, 0) // unsafe fallback with better shape score
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 1)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu coverage for Tong(2)
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // partial dangerous-opponent coverage on one alternate
                new() { SeatIndex = 1, TileId = Wan(1, 2), TurnNumber = 3 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W33")]
    public void Master_KeepsFullDangerousGenbutsu_WhenCoveredAlternateIsShapeWorseThanUnsafeFallback()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(5, 0),  // extra small-win tile so Tong(2) can still be promoted on score tolerance
                Wan(1, 0), Wan(1, 1), // covered alternate that is structurally expensive to break
                Tiao(9, 0) // unsafe fallback with better shape score
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 1)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu coverage for Tong(2)
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // partial dangerous-opponent coverage on one alternate
                new() { SeatIndex = 1, TileId = Wan(1, 2), TurnNumber = 3 }
            }
        };

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W34")]
    public void Hard_KeepsFullDangerousGenbutsu_WhenBackupSmallWinSingletonExistsAndAlternatesAreUnsafe()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // full dangerous-opponent genbutsu singleton
                Wan(5, 0),  // backup 2/5/8 singleton keeps small-win route alive
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W34")]
    public void Master_KeepsFullDangerousGenbutsu_WhenBackupSmallWinSingletonExistsAndAlternatesAreUnsafe()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // full dangerous-opponent genbutsu singleton
                Wan(5, 0),  // backup 2/5/8 singleton keeps small-win route alive
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W35")]
    public void Hard_DoesNotSpendOnlyLiveSmallWinSingleton_WhenBackupSingletonIsDead()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live lone 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(5, 0),  // backup 2/5/8 singleton but dead (all copies visible)
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 1)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu for Tong(2)
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // backup small-win singleton is dead (all four 5-wan copies visible)
                new() { SeatIndex = 1, TileId = Wan(5, 1), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Wan(5, 2), TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = Wan(5, 3), TurnNumber = 5 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var action = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W35")]
    public void Master_DoesNotSpendOnlyLiveSmallWinSingleton_WhenBackupSingletonIsDead()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live lone 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(5, 0),  // backup 2/5/8 singleton but dead (all copies visible)
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 1)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(5, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu for Tong(2)
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // backup small-win singleton is dead (all four 5-wan copies visible)
                new() { SeatIndex = 1, TileId = Wan(5, 1), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Wan(5, 2), TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = Wan(5, 3), TurnNumber = 5 }
            }
        };

        var master = ChangshaBotEngine.Resolve("master");
        var action = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W36")]
    public void HardAndMaster_DoNotSpendLiveSmallWinSingleton_WhenBackupSingletonIsDeadViaMeldVisibilityOnly()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0),
                Wan(5, 0),
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));
        state.Hands[3].Melds = new List<Meld>
        {
            FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(5, 1)))
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.NotEqual(Tong(2, 0), hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.NotEqual(Tong(2, 0), masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W39")]
    public void HardAndMaster_KeepFullDangerousGenbutsu_WhenBackupSmallWinPairExistsAndAlternatesAreUnsafe()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0),
                Wan(5, 0), Wan(5, 1), // backup small-win pair already secures 2/5/8 eye route
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Tong(2, 0), hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Tong(2, 0), masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W38")]
    public void HardAndMaster_KeepFullDangerousGenbutsu_WhenBackupSmallWinPairExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0),
                Wan(5, 0), Wan(5, 1), // backup small-win pair (already viable eye)
                Wan(1, 0),
                Tiao(9, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 });

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Tong(2, 0), hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Tong(2, 0), masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W40")]
    public void HardAndMaster_PreserveLiveSmallWinSingleton_WhenEqualFullSafetyOffRankExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // lone live 2/5/8 singleton
                Wan(1, 0),  // non-small-win alternate with full dangerous-opponent genbutsu
                Wan(2, 0),  // dead backup small-win singleton (all copies visible)
                Tiao(9, 0)
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        // Make the off-rank alternate full dangerous-opponent genbutsu.
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 });
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 2, TileId = Wan(1, 2), TurnNumber = 4 });

        // Dead backup singleton should not count as a live alternate 2/5/8 route.
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 3, TileId = Wan(2, 1), TurnNumber = 5 });
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 3, TileId = Wan(2, 2), TurnNumber = 6 });
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 3, TileId = Wan(2, 3), TurnNumber = 7 });

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Wan(1, 0), hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Wan(1, 0), masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W44")]
    public void HardAndMaster_PreferFullDangerousCoveredAlternate_OverPartialCoveredAlternate_WhenPreservingLiveSmallWinSingleton()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // lone live 2/5/8 singleton (full dangerous-opponent genbutsu)
                Wan(2, 0),  // dead backup 2/5/8 singleton (all copies visible)
                Wan(1, 0),  // partial dangerous-opponent covered alternate (seat 1 only)
                Tong(9, 0), // full dangerous-opponent covered alternate (seat 1 + seat 2)
                Tiao(9, 0)  // unsafe fallback
            }
        };

        var state = BuildFullDangerousState(hand, Tong(2, 1), Tong(2, 2));

        // Partial coverage on one non-small-win alternate.
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 });

        // Full coverage on a second non-small-win alternate.
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 1, TileId = Tong(9, 1), TurnNumber = 4 });
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 2, TileId = Tong(9, 2), TurnNumber = 5 });

        // Dead backup singleton should not count as a live alternate 2/5/8 route.
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 3, TileId = Wan(2, 1), TurnNumber = 6 });
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 3, TileId = Wan(2, 2), TurnNumber = 7 });
        state.DiscardPile.Add(new ChangshaDiscard { SeatIndex = 3, TileId = Wan(2, 3), TurnNumber = 8 });

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Tong(9, 0)), ChangshaDeckBuilder.GetLogicalTile(hardAction.TileId!.Value));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Tong(9, 0)), ChangshaDeckBuilder.GetLogicalTile(masterAction.TileId!.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W49")]
    public void HardAndMaster_DoNotTradeShantenForTenpaiSafeOffRankPairSwap_NearTenpai()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 0), Wan(2, 0), Wan(3, 0),
                Wan(4, 0), Wan(5, 0), Wan(6, 0),
                Tong(1, 0), Tong(2, 0), Tong(3, 0),
                Tiao(4, 0), Tiao(5, 0),
                Tong(7, 0), Tong(7, 1),
                Tiao(2, 0) // dead 2/5/8 singleton
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(9, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(9, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0)))
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Tong(7, 2), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tiao(2, 1), TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = Tiao(2, 2), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Tiao(2, 3), TurnNumber = 4 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Tiao(2, 0), hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Tiao(2, 0), masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W50")]
    public void HardAndMaster_KeepFullDangerousLiveSmallWinSingleton_WhenDeadBackupSwapWouldRegressShanten()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0),
                Wan(5, 0),
                Wan(1, 0), Wan(1, 1),
                Wan(3, 0), Wan(4, 0), Wan(6, 0),
                Tong(3, 0), Tong(3, 3), Tong(4, 0), Tong(6, 0),
                Tiao(3, 0), Tiao(4, 0), Tiao(6, 0)
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(7, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(7, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(8, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(1, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // full dangerous-opponent genbutsu coverage for the live Tong 2 singleton
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 },

                // backup Wan 5 singleton is dead
                new() { SeatIndex = 1, TileId = Wan(5, 1), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Wan(5, 2), TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = Wan(5, 3), TurnNumber = 5 },

                new() { SeatIndex = 1, TileId = Wan(1, 2), TurnNumber = 6 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Tong(2, 0), hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Tong(2, 0), masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W52")]
    public void HardAndMaster_KeepFullDangerousLiveSmallWinSingletonSwap_ShantenSafe()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                93, 81, 37, 61, 80, 38, 56, 60, 39, 13, 23, 86, 41, 96
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(4, 0)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(6, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(7, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(8, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 41, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 42, TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = 56, TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = 101, TurnNumber = 4 },
                new() { SeatIndex = 1, TileId = 2, TurnNumber = 5 },
                new() { SeatIndex = 2, TileId = 63, TurnNumber = 6 }
            }
        };

        var singletonDiscardHand = new List<int>(hand.ConcealedTiles);
        singletonDiscardHand.Remove(41);
        var singletonDiscardShanten = HandEvaluator.MinShantenToHu(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = singletonDiscardHand,
            Melds = hand.Melds
        }, Array.Empty<int>());

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);

        var hardDiscardHand = new List<int>(hand.ConcealedTiles);
        hardDiscardHand.Remove(hardAction.TileId!.Value);
        var hardChosenShanten = HandEvaluator.MinShantenToHu(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = hardDiscardHand,
            Melds = hand.Melds
        }, Array.Empty<int>());
        Assert.True(hardChosenShanten <= singletonDiscardShanten,
            $"expected hard shanten-safe swap (chosen={hardAction.TileId}, chosenShanten={hardChosenShanten}, singletonShanten={singletonDiscardShanten})");

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);

        var masterDiscardHand = new List<int>(hand.ConcealedTiles);
        masterDiscardHand.Remove(masterAction.TileId!.Value);
        var masterChosenShanten = HandEvaluator.MinShantenToHu(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = masterDiscardHand,
            Melds = hand.Melds
        }, Array.Empty<int>());
        Assert.True(masterChosenShanten <= singletonDiscardShanten,
            $"expected master shanten-safe swap (chosen={masterAction.TileId}, chosenShanten={masterChosenShanten}, singletonShanten={singletonDiscardShanten})");
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W53")]
    public void HardAndMaster_PreserveShantenPrimary_WhenSafetyOverridesCompete()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                6, 17, 21, 31, 32, 40, 54, 66, 68, 83, 85, 97, 98, 101
            }
        };

        var state = new ChangshaGameState
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
                        FakePung(1),
                        FakePung(5),
                        FakePung(9)
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(2),
                        FakePung(6),
                        FakePung(10)
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 3, TileId = 50, TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = 107, TurnNumber = 2 },
                new() { SeatIndex = 2, TileId = 38, TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = 60, TurnNumber = 4 },
                new() { SeatIndex = 1, TileId = 10, TurnNumber = 5 },
                new() { SeatIndex = 2, TileId = 96, TurnNumber = 6 }
            }
        };

        static int ShantenAfterDiscard(ChangshaHandState handState, int tileId)
        {
            var concealedAfter = new List<int>(handState.ConcealedTiles);
            concealedAfter.Remove(tileId);
            return HandEvaluator.MinShantenToHu(new ChangshaHandState
            {
                SeatIndex = handState.SeatIndex,
                ConcealedTiles = concealedAfter,
                Melds = handState.Melds
            }, Array.Empty<int>());
        }

        var minShanten = hand.ConcealedTiles
            .Select(tileId => ShantenAfterDiscard(hand, tileId))
            .Min();

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.NotNull(hardAction.TileId);
        Assert.Equal(minShanten, ShantenAfterDiscard(hand, hardAction.TileId!.Value));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.NotNull(masterAction.TileId);
        Assert.Equal(minShanten, ShantenAfterDiscard(hand, masterAction.TileId!.Value));
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

    private static ChangshaGameState BuildDangerousWithoutGenbutsuState(ChangshaHandState hand)
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
                // Dead visibility for the 2 singleton, but no dangerous-opponent
                // genbutsu for either competing discard candidate.
                new() { SeatIndex = 2, TileId = Tiao(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 3, TileId = Tiao(2, 2), TurnNumber = 2 },
                new() { SeatIndex = 2, TileId = Tiao(2, 3), TurnNumber = 3 }
            }
        };
    }

    private static ChangshaGameState BuildFullDangerousState(ChangshaHandState hand, int seatOneGenbutsuTileId, int seatTwoGenbutsuTileId)
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
                // Both dangerous opponents have discarded the same logical tile.
                new() { SeatIndex = 1, TileId = seatOneGenbutsuTileId, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = seatTwoGenbutsuTileId, TurnNumber = 2 }
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
