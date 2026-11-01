using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Changsha.Bot.Heuristics;
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

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W54")]
    public void HardAndMaster_KeepSingleDangerousSmallWinPreserve_ShantenSafeNearTenpai()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 0, 6, 17, 21, 40, 50, 55, 59, 68, 69, 75, 79, 83, 88 }
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                // Single dangerous opponent genbutsu coverage only.
                new() { SeatIndex = 1, TileId = 85, TurnNumber = 1 }
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

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W58")]
    public void HardAndMaster_KeepBestShantenTier_WhenFullDangerousSingletonSwapWouldRegressPracticalShape()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 0), Wan(1, 2),
                Wan(3, 0),
                Wan(4, 0), Wan(4, 2),
                Wan(6, 2), Wan(6, 3),
                Wan(7, 3),
                Tong(2, 3),
                Tong(6, 2), Tong(7, 0),
                Tiao(2, 3),
                Tiao(8, 0), Tiao(8, 1)
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(1),
                        FakePung(10),
                        FakePung(19)
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 54, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 85, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 96, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 15, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 31, TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = 87, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 20, TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = 98, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 67, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 62, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 46, TurnNumber = 11 },
                new() { SeatIndex = 3, TileId = 82, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 63, TurnNumber = 13 },
                new() { SeatIndex = 2, TileId = 16, TurnNumber = 14 },
                new() { SeatIndex = 3, TileId = 17, TurnNumber = 15 },
                new() { SeatIndex = 1, TileId = 50, TurnNumber = 16 },
                new() { SeatIndex = 2, TileId = 53, TurnNumber = 17 },
                new() { SeatIndex = 3, TileId = 69, TurnNumber = 18 },
                new() { SeatIndex = 1, TileId = 36, TurnNumber = 19 },
                new() { SeatIndex = 2, TileId = 21, TurnNumber = 20 },
                new() { SeatIndex = 3, TileId = 91, TurnNumber = 21 },
                new() { SeatIndex = 1, TileId = 66, TurnNumber = 22 },
                new() { SeatIndex = 2, TileId = 78, TurnNumber = 23 },
                new() { SeatIndex = 3, TileId = 93, TurnNumber = 24 }
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

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W59")]
    public void HardAndMaster_KeepBestPracticalShantenTier_WhenSingleDangerousPairSwapCompetes()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 104, 106, 17, 96, 28, 79, 47, 64, 74, 46, 5, 97, 2, 45 }
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 104, TurnNumber = 1 }
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

        var minShanten = hand.ConcealedTiles.Select(tileId => ShantenAfterDiscard(hand, tileId)).Min();

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(minShanten, ShantenAfterDiscard(hand, hardAction.TileId!.Value));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(minShanten, ShantenAfterDiscard(hand, masterAction.TileId!.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W62")]
    public void HardAndMaster_PreferLowerShantenFullGenbutsuAlternate_WhenPreservingLiveSmallWinSingleton()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                35, 103, 55, 29, 96, 105, 54, 5, 93, 62, 40, 87, 77, 68
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
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 92, 93, 94 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 68, 69, 70 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 88, 89, 90 } }
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 72, 73, 74 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 16, 17, 18 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 44, 45, 46 } }
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 31, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 41, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 25, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 16, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 88, TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = 81, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 76, TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = 33, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 82, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 69, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 90, TurnNumber = 11 },
                new() { SeatIndex = 3, TileId = 46, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 14, TurnNumber = 13 },
                new() { SeatIndex = 2, TileId = 60, TurnNumber = 14 },
                new() { SeatIndex = 3, TileId = 48, TurnNumber = 15 },
                new() { SeatIndex = 1, TileId = 72, TurnNumber = 16 },
                new() { SeatIndex = 2, TileId = 95, TurnNumber = 17 },
                new() { SeatIndex = 3, TileId = 6, TurnNumber = 18 },
                new() { SeatIndex = 1, TileId = 11, TurnNumber = 19 },
                new() { SeatIndex = 2, TileId = 1, TurnNumber = 20 },
                new() { SeatIndex = 3, TileId = 42, TurnNumber = 21 },
                new() { SeatIndex = 1, TileId = 44, TurnNumber = 22 },
                new() { SeatIndex = 2, TileId = 86, TurnNumber = 23 },
                new() { SeatIndex = 3, TileId = 75, TurnNumber = 24 },
                new() { SeatIndex = 1, TileId = 84, TurnNumber = 25 },
                new() { SeatIndex = 2, TileId = 79, TurnNumber = 26 },
                new() { SeatIndex = 3, TileId = 28, TurnNumber = 27 },
                new() { SeatIndex = 1, TileId = 89, TurnNumber = 28 },
                new() { SeatIndex = 2, TileId = 94, TurnNumber = 29 },
                new() { SeatIndex = 3, TileId = 7, TurnNumber = 30 }
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

        var bestFullSafetyShanten = hand.ConcealedTiles
            .Where(tileId => TenpaiDetector.SafetyBias(tileId, state, 0) <= -2)
            .Distinct()
            .Min(tileId => ShantenAfterDiscard(hand, tileId));

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.NotNull(hardAction.TileId);
        Assert.Equal(bestFullSafetyShanten, ShantenAfterDiscard(hand, hardAction.TileId!.Value));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.NotNull(masterAction.TileId);
        Assert.Equal(bestFullSafetyShanten, ShantenAfterDiscard(hand, masterAction.TileId!.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W63")]
    public void HardAndMaster_PreferLowerShantenFullGenbutsu_WhenFarFromTenpai()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 43, 34, 70, 60, 25, 61, 3, 63, 107, 20, 79, 52, 99, 7 }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1, Melds = new List<Meld> { FakePung(0), FakePung(9), FakePung(18) } },
                new() { SeatIndex = 2, Melds = new List<Meld> { FakePung(1), FakePung(10), FakePung(19) } },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 88, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 18, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 81, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 66, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 30, TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = 48, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 100, TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = 24, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 9, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 80, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 104, TurnNumber = 11 },
                new() { SeatIndex = 3, TileId = 11, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 13, TurnNumber = 13 },
                new() { SeatIndex = 2, TileId = 49, TurnNumber = 14 },
                new() { SeatIndex = 3, TileId = 33, TurnNumber = 15 },
                new() { SeatIndex = 1, TileId = 75, TurnNumber = 16 },
                new() { SeatIndex = 2, TileId = 93, TurnNumber = 17 },
                new() { SeatIndex = 3, TileId = 97, TurnNumber = 18 },
                new() { SeatIndex = 1, TileId = 21, TurnNumber = 19 },
                new() { SeatIndex = 2, TileId = 12, TurnNumber = 20 },
                new() { SeatIndex = 3, TileId = 27, TurnNumber = 21 },
                new() { SeatIndex = 1, TileId = 56, TurnNumber = 22 },
                new() { SeatIndex = 2, TileId = 85, TurnNumber = 23 },
                new() { SeatIndex = 3, TileId = 65, TurnNumber = 24 },
                new() { SeatIndex = 1, TileId = 47, TurnNumber = 25 },
                new() { SeatIndex = 2, TileId = 51, TurnNumber = 26 },
                new() { SeatIndex = 3, TileId = 28, TurnNumber = 27 },
                new() { SeatIndex = 1, TileId = 15, TurnNumber = 28 },
                new() { SeatIndex = 2, TileId = 106, TurnNumber = 29 },
                new() { SeatIndex = 3, TileId = 83, TurnNumber = 30 },
                new() { SeatIndex = 1, TileId = 105, TurnNumber = 31 },
                new() { SeatIndex = 2, TileId = 96, TurnNumber = 32 },
                new() { SeatIndex = 3, TileId = 16, TurnNumber = 33 },
                new() { SeatIndex = 1, TileId = 54, TurnNumber = 34 },
                new() { SeatIndex = 2, TileId = 17, TurnNumber = 35 },
                new() { SeatIndex = 3, TileId = 84, TurnNumber = 36 },
                new() { SeatIndex = 1, TileId = 103, TurnNumber = 37 },
                new() { SeatIndex = 2, TileId = 53, TurnNumber = 38 },
                new() { SeatIndex = 3, TileId = 10, TurnNumber = 39 },
                new() { SeatIndex = 1, TileId = 35, TurnNumber = 40 }
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

        var bestFullSafetyShanten = hand.ConcealedTiles
            .Where(tileId => TenpaiDetector.SafetyBias(tileId, state, 0) <= -2)
            .Distinct()
            .Min(tileId => ShantenAfterDiscard(hand, tileId));

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.NotNull(hardAction.TileId);
        Assert.Equal(bestFullSafetyShanten, ShantenAfterDiscard(hand, hardAction.TileId!.Value));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.NotNull(masterAction.TileId);
        Assert.Equal(bestFullSafetyShanten, ShantenAfterDiscard(hand, masterAction.TileId!.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W64")]
    public void HardAndMaster_PreferLowerShantenSingleGenbutsu_WhenAlreadySingleSafeFarFromTenpai()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 43, 58, 28, 51, 80, 34, 101, 75, 32, 38, 67, 19, 4, 68 }
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 29, TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = 106, TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = 92, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 58, TurnNumber = 4 },
                new() { SeatIndex = 1, TileId = 12, TurnNumber = 5 },
                new() { SeatIndex = 1, TileId = 28, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 18, TurnNumber = 7 },
                new() { SeatIndex = 1, TileId = 102, TurnNumber = 8 },
                new() { SeatIndex = 1, TileId = 90, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 2, TurnNumber = 10 },
                new() { SeatIndex = 1, TileId = 37, TurnNumber = 11 },
                new() { SeatIndex = 1, TileId = 9, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 97, TurnNumber = 13 },
                new() { SeatIndex = 1, TileId = 68, TurnNumber = 14 },
                new() { SeatIndex = 1, TileId = 4, TurnNumber = 15 },
                new() { SeatIndex = 1, TileId = 84, TurnNumber = 16 },
                new() { SeatIndex = 1, TileId = 27, TurnNumber = 17 },
                new() { SeatIndex = 1, TileId = 80, TurnNumber = 18 },
                new() { SeatIndex = 1, TileId = 71, TurnNumber = 19 },
                new() { SeatIndex = 1, TileId = 55, TurnNumber = 20 },
                new() { SeatIndex = 1, TileId = 23, TurnNumber = 21 },
                new() { SeatIndex = 1, TileId = 32, TurnNumber = 22 },
                new() { SeatIndex = 1, TileId = 24, TurnNumber = 23 },
                new() { SeatIndex = 1, TileId = 74, TurnNumber = 24 },
                new() { SeatIndex = 1, TileId = 39, TurnNumber = 25 },
                new() { SeatIndex = 1, TileId = 47, TurnNumber = 26 },
                new() { SeatIndex = 1, TileId = 48, TurnNumber = 27 },
                new() { SeatIndex = 1, TileId = 3, TurnNumber = 28 },
                new() { SeatIndex = 1, TileId = 20, TurnNumber = 29 },
                new() { SeatIndex = 2, TileId = 32, TurnNumber = 30 },
                new() { SeatIndex = 2, TileId = 52, TurnNumber = 31 },
                new() { SeatIndex = 3, TileId = 54, TurnNumber = 32 },
                new() { SeatIndex = 3, TileId = 6, TurnNumber = 33 },
                new() { SeatIndex = 3, TileId = 105, TurnNumber = 34 },
                new() { SeatIndex = 3, TileId = 95, TurnNumber = 35 },
                new() { SeatIndex = 3, TileId = 99, TurnNumber = 36 },
                new() { SeatIndex = 3, TileId = 98, TurnNumber = 37 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(4, hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(4, masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W65")]
    public void HardAndMaster_KeepLowerShantenSingleSafeSmallWinDiscard_WhenFallbackIsNotSafer()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 5, 61, 12, 2, 16, 83, 65, 62, 53, 100, 89, 78, 30, 25 }
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 54, TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = 30, TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = 1, TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = 76, TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = 66, TurnNumber = 5 },
                new() { SeatIndex = 2, TileId = 29, TurnNumber = 6 },
                new() { SeatIndex = 3, TileId = 16, TurnNumber = 7 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(53, hardAction.TileId);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(53, masterAction.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W68")]
    public void HardAndMaster_PromoteSaferSmallWinSingleton_WhenSingleDangerousPressureExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // lower keep score but not dangerous-opponent genbutsu
                Wan(5, 0),  // dangerous-opponent genbutsu small-win singleton

                Wan(1, 0), Wan(1, 1),
                Wan(4, 0), Wan(4, 1),
                Wan(7, 0), Wan(7, 1),
                Tong(6, 0), Tong(6, 1),
                Tong(7, 0), Tong(7, 1),
                Tiao(9, 0), Tiao(9, 1)
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Wan(5, 1), TurnNumber = 1 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Wan(5, 0), hardAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(hardAction.TileId!.Value, state, 0) <= -1);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Wan(5, 0), masterAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(masterAction.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W69")]
    public void HardAndMaster_PromoteSaferSmallWinSingleton_WhenSaferSingletonHasSlightlyHigherKeepScore()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0),
                Tong(3, 0),
                Tong(4, 0),
                Tong(5, 0), // safer 2/5/8 singleton (dangerous-opponent genbutsu)
                Tong(6, 0), Tong(6, 1), Tong(6, 2),
                Tong(8, 0), // lower keep-score 2/5/8 singleton that used to win
                Tiao(3, 0),
                Tiao(5, 0), Tiao(5, 1),
                Tiao(6, 0),
                Tiao(9, 0), Tiao(9, 1)
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Tong(5, 1), TurnNumber = 1 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(Tong(5, 0), hardAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(hardAction.TileId!.Value, state, 0) <= -1);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(Tong(5, 0), masterAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(masterAction.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W71")]
    public void HardAndMaster_PreferLowerShantenSmallWinSingleton_WhenSingleDangerousSafetyTies()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                86, 91, 35, 18, 39, 45, 73, 94, 78, 59, 103, 71, 7, 34
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
                        FakePung(22),
                        FakePung(3),
                        FakePung(5)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 88, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 39, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 94, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 91, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 0, TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = 65, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 4, TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = 62, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 34, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 72, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 76, TurnNumber = 11 },
                new() { SeatIndex = 3, TileId = 9, TurnNumber = 12 }
            }
        };

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(7, hardAction.TileId);
        Assert.Equal(-1, TenpaiDetector.SafetyBias(hardAction.TileId!.Value, state, 0));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(7, masterAction.TileId);
        Assert.Equal(-1, TenpaiDetector.SafetyBias(masterAction.TileId!.Value, state, 0));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W72")]
    public void HardAndMaster_AllowSaferLiveSmallWinSingletonDiscard_WhenBackupLiveRoutesExist()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 93, 7, 91, 6, 30, 81, 46, 67, 18, 95, 10, 48, 17, 97 }
        };

        var state = BuildSingleDangerousState(hand, 91);

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(91, hardAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(hardAction.TileId!.Value, state, 0) <= -1);

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(91, masterAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(masterAction.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W73")]
    public void HardAndMaster_KeepFullDangerousSmallWinDiscard_WhenOnlyUnsafeFallbackKeepsSameShanten()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 70, 83, 74, 55, 33, 21, 63, 20, 93, 23, 40, 14, 12, 25 }
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
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 40, 41, 42 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 56, 57, 58 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 0, 1, 2 } }
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 0, 1, 2 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 8, 9, 10 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 36, 37, 38 } }
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 2, TileId = 103, TurnNumber = 1 },
                new() { SeatIndex = 3, TileId = 22, TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = 107, TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = 2, TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = 69, TurnNumber = 5 },
                new() { SeatIndex = 1, TileId = 50, TurnNumber = 6 },
                new() { SeatIndex = 2, TileId = 75, TurnNumber = 7 },
                new() { SeatIndex = 3, TileId = 58, TurnNumber = 8 },
                new() { SeatIndex = 1, TileId = 45, TurnNumber = 9 },
                new() { SeatIndex = 2, TileId = 27, TurnNumber = 10 },
                new() { SeatIndex = 3, TileId = 44, TurnNumber = 11 },
                new() { SeatIndex = 1, TileId = 7, TurnNumber = 12 },
                new() { SeatIndex = 2, TileId = 52, TurnNumber = 13 },
                new() { SeatIndex = 3, TileId = 82, TurnNumber = 14 },
                new() { SeatIndex = 1, TileId = 43, TurnNumber = 15 },
                new() { SeatIndex = 2, TileId = 72, TurnNumber = 16 },
                new() { SeatIndex = 3, TileId = 73, TurnNumber = 17 },
                new() { SeatIndex = 1, TileId = 53, TurnNumber = 18 },
                new() { SeatIndex = 2, TileId = 64, TurnNumber = 19 },
                new() { SeatIndex = 3, TileId = 38, TurnNumber = 20 },
                new() { SeatIndex = 1, TileId = 30, TurnNumber = 21 },
                new() { SeatIndex = 2, TileId = 56, TurnNumber = 22 },
                new() { SeatIndex = 3, TileId = 13, TurnNumber = 23 },
                new() { SeatIndex = 1, TileId = 96, TurnNumber = 24 },
                new() { SeatIndex = 2, TileId = 10, TurnNumber = 25 },
                new() { SeatIndex = 3, TileId = 90, TurnNumber = 26 },
                new() { SeatIndex = 1, TileId = 9, TurnNumber = 27 },
                new() { SeatIndex = 2, TileId = 24, TurnNumber = 28 },
                new() { SeatIndex = 3, TileId = 46, TurnNumber = 29 },
                new() { SeatIndex = 1, TileId = 88, TurnNumber = 30 }
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

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(55, hardAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(hardAction.TileId!.Value, state, 0) <= -2);
        Assert.Equal(2, ShantenAfterDiscard(hand, hardAction.TileId.Value));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(55, masterAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(masterAction.TileId!.Value, state, 0) <= -2);
        Assert.Equal(2, ShantenAfterDiscard(hand, masterAction.TileId.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W78")]
    public void HardAndMaster_PreferLowerShantenSingleGenbutsu_WhenUpgradingFromUnsafeCandidate()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 96, 53, 95, 90, 81, 106, 103, 91, 65, 72, 21, 49, 11, 14 }
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
                        FakePung(0),
                        FakePung(9),
                        FakePung(18)
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 40, TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = 3, TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = 35, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 47, TurnNumber = 4 },
                new() { SeatIndex = 1, TileId = 44, TurnNumber = 5 },
                new() { SeatIndex = 1, TileId = 34, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 58, TurnNumber = 7 },
                new() { SeatIndex = 1, TileId = 82, TurnNumber = 8 },
                new() { SeatIndex = 1, TileId = 80, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 29, TurnNumber = 10 },
                new() { SeatIndex = 1, TileId = 8, TurnNumber = 11 },
                new() { SeatIndex = 1, TileId = 61, TurnNumber = 12 },
                new() { SeatIndex = 2, TileId = 42, TurnNumber = 13 },
                new() { SeatIndex = 2, TileId = 57, TurnNumber = 14 },
                new() { SeatIndex = 2, TileId = 85, TurnNumber = 15 },
                new() { SeatIndex = 2, TileId = 31, TurnNumber = 16 }
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

        var hard = ChangshaBotEngine.Resolve("hard");
        var hardAction = hard.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, hardAction.Type);
        Assert.Equal(11, hardAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(hardAction.TileId!.Value, state, 0) <= -1);
        Assert.True(ShantenAfterDiscard(hand, hardAction.TileId.Value) < ShantenAfterDiscard(hand, 81));

        var master = ChangshaBotEngine.Resolve("master");
        var masterAction = master.DecideAction(state, botSeatIndex: 0);
        Assert.Equal(BotActionType.Discard, masterAction.Type);
        Assert.Equal(11, masterAction.TileId);
        Assert.True(TenpaiDetector.SafetyBias(masterAction.TileId!.Value, state, 0) <= -1);
        Assert.True(ShantenAfterDiscard(hand, masterAction.TileId.Value) < ShantenAfterDiscard(hand, 81));
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
