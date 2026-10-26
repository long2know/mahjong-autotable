using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Changsha.Bots;

public class MediumStrategySafetyRegressionTests
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

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Medium_DoesNotForceDeadSmallWinSingleton_WhenSaferDangerousGenbutsuAlternativeExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton (all other copies visible)
                Wan(1, 0),  // dangerous-opponent genbutsu
                Tong(4, 0),
                Tong(7, 0)
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
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tiao(2, 1), TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = Tiao(2, 2), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Tiao(2, 3), TurnNumber = 4 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(1, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Medium_KeepsSingleDangerousGenbutsu_WhenDeadSmallWinFallbackIsOnlyEqualSafety()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton (all copies visible)
                Wan(1, 0),  // dangerous-opponent genbutsu with better discard score
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
                // same dangerous-opponent genbutsu coverage for both candidates
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 2 },

                // complete dead visibility for the 2/5/8 singleton
                new() { SeatIndex = 2, TileId = Tiao(2, 2), TurnNumber = 3 },
                new() { SeatIndex = 3, TileId = Tiao(2, 3), TurnNumber = 4 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(1, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Medium_PrefersFullDangerousGenbutsu_OverDeadSmallWinSingleton_WhenSafetyUpgradeWithinTolerance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton
                Wan(1, 0),  // lowest keep-score but unsafe
                Tong(9, 0), Tong(9, 1) // higher keep-score but full dangerous-opponent genbutsu
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
                new() { SeatIndex = 1, TileId = Tong(9, 2), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(9, 3), TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 3 },
                new() { SeatIndex = 3, TileId = Tiao(2, 2), TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = Tiao(2, 3), TurnNumber = 5 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(
            ChangshaDeckBuilder.GetLogicalTile(Tong(9, 0)),
            ChangshaDeckBuilder.GetLogicalTile(action.TileId!.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Medium_PrefersDangerousOpponentGenbutsu_WhenSingleDangerousSafetyUpgradeWithinTolerance()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 0),  // lowest keep-score but unsafe
                Tong(4, 0), // dangerous-opponent genbutsu with +1 keep-score
                Tong(6, 0)
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(6, 1))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(9, 0)))
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Tong(4, 1), TurnNumber = 1 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(4, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W24")]
    public void Medium_PrefersFullDangerousGenbutsu_OverPartialSafeOffRankPair_WhenDeadSmallWinSingletonExists()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tiao(2, 0), // dead 2/5/8 singleton
                Wan(4, 0), Wan(4, 1), // off-rank pair (safe vs one dangerous opponent only)
                Tong(9, 0) // full dangerous-opponent genbutsu singleton
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
                // dead 2 singleton (all four copies visible)
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tiao(2, 2), TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = Tiao(2, 3), TurnNumber = 3 },

                // partial-safe off-rank pair candidate (seat 1 only)
                new() { SeatIndex = 1, TileId = Wan(4, 2), TurnNumber = 4 },

                // full dangerous-opponent genbutsu candidate (both seat 1 and 2)
                new() { SeatIndex = 1, TileId = Tong(9, 1), TurnNumber = 5 },
                new() { SeatIndex = 2, TileId = Tong(9, 2), TurnNumber = 6 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(9, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W31")]
    public void Medium_DoesNotPromoteLiveSmallWinSingleton_ForFullDangerousGenbutsuTie()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(8, 0), Wan(8, 1), // best keep-shape candidate (unsafe)
                Tong(2, 0), // live lone 2/5/8 singleton with full dangerous-opponent genbutsu
                Tiao(7, 0)
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
                // full dangerous-opponent genbutsu for the live 2 tile
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W34")]
    public void Medium_DoesNotPromoteLiveSmallWinSingleton_ForSingleDangerousGenbutsuTie()
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
                // single dangerous-opponent genbutsu coverage for the live 2 tile
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W32")]
    public void Medium_KeepsFullDangerousGenbutsu_WhenAnotherAlternateHasDangerousCoverage()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live 2/5/8 singleton, full dangerous-opponent genbutsu
                Wan(5, 0),  // extra small-win tile so Tong(2) can still be promoted on score tolerance
                Wan(1, 0),  // partial dangerous-opponent genbutsu (seat 1 only)
                Tiao(9, 0)  // fully unsafe fallback candidate
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W34")]
    public void Medium_KeepsFullDangerousGenbutsu_WhenBackupSmallWinSingletonExistsAndAlternatesAreUnsafe()
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
                // full dangerous-opponent genbutsu for Tong(2) only
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W35")]
    public void Medium_DoesNotSpendOnlyLiveSmallWinSingleton_WhenBackupSingletonIsDead()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // live lone 2/5/8 singleton with full dangerous-opponent genbutsu
                Wan(4, 0),
                Wan(5, 0),  // backup 2/5/8 singleton but dead (all copies visible)
                Wan(6, 0),
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W36")]
    public void Medium_DoesNotSpendLiveSmallWinSingleton_WhenBackupSingletonIsDeadViaMeldVisibilityOnly()
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
                new()
                {
                    SeatIndex = 3,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(5, 1)))
                    }
                }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Tong(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotEqual(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W37")]
    public void Medium_PrefersCoveredAlternateOverFullyUnsafeFallback_WhenPreservingLiveSmallWinSingleton()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // lone live 2/5/8 singleton (full dangerous-opponent genbutsu)
                Wan(5, 0), Wan(5, 1), // avoid "single small-win tile" lock while keeping no backup singleton
                Wan(1, 0), // covered by one dangerous opponent
                Tiao(9, 0) // fully unsafe fallback
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

                // partial dangerous-opponent coverage on Wan(1)
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(1, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W41")]
    public void Medium_KeepsFullDangerousGenbutsu_WhenBackupSmallWinPairExists_AndAlternatesAreUnsafe()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // full dangerous-opponent genbutsu singleton
                Wan(5, 0), Wan(5, 1), // backup 2/5/8 pair already secures eye route
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
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W42")]
    public void Medium_KeepsFullDangerousGenbutsu_WhenBackupSmallWinPairExists_AndOnlyUnsafeAlternateRemains()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // full dangerous-opponent genbutsu singleton
                Wan(5, 0), Wan(5, 1), // backup 2/5/8 pair already secures eye route
                Wan(1, 0) // lone non-small-win fallback and fully unsafe
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
                new() { SeatIndex = 2, TileId = Tong(2, 2), TurnNumber = 2 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W43")]
    public void Medium_PrefersFullDangerousCoveredAlternate_OverPartialCoveredAlternate_WhenPreservingLiveSmallWinSingleton()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(2, 0), // lone live 2/5/8 singleton (full dangerous-opponent genbutsu)
                Wan(5, 0), Wan(5, 1), // keep alternate small-win route as a pair, not singleton
                Wan(1, 0), // partial dangerous-opponent genbutsu (seat 1 only)
                Tong(9, 0), Tong(9, 1) // full dangerous-opponent genbutsu but shape-worse than Wan(1)
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

                // partial dangerous-opponent coverage on Wan(1)
                new() { SeatIndex = 1, TileId = Wan(1, 1), TurnNumber = 3 },

                // full dangerous-opponent coverage on Tong(9)
                new() { SeatIndex = 1, TileId = Tong(9, 2), TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = Tong(9, 3), TurnNumber = 5 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(ChangshaDeckBuilder.GetLogicalTile(Tong(9, 0)), ChangshaDeckBuilder.GetLogicalTile(action.TileId!.Value));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W45")]
    public void Medium_DoesNotTradeShantenForSingleDangerousGenbutsuPromotion()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(3, 0),
                Wan(6, 2), // single-dangerous-opponent genbutsu candidate
                Wan(8, 2),
                Wan(9, 1),
                Wan(9, 2),
                Tong(3, 2),
                Tong(5, 1),
                Tong(5, 2),
                Tiao(3, 3),
                Tiao(4, 3),
                Tiao(5, 2),
                Tiao(6, 0),
                Tiao(7, 1),
                Tiao(8, 0)
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(1, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(1, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(1, 0)))
                    }
                },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Wan(6, 3), TurnNumber = 1 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(3, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W46")]
    public void Medium_DoesNotTradeShantenForFullDangerousGenbutsuPromotion()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(3, 0),
                Wan(6, 2), // full-dangerous-opponent genbutsu candidate
                Wan(8, 2),
                Wan(9, 1),
                Wan(9, 2),
                Tong(3, 2),
                Tong(5, 1),
                Tong(5, 2),
                Tiao(3, 3),
                Tiao(4, 3),
                Tiao(5, 2),
                Tiao(6, 0),
                Tiao(7, 1),
                Tiao(8, 0)
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(1, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(1, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(1, 0)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(2, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(2, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(2, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Wan(6, 3), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Wan(6, 1), TurnNumber = 2 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(3, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W47")]
    public void Medium_DoesNotTradeShantenForDeadSmallWinSafetySwap()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tiao(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W48")]
    public void Medium_KeepsFullDangerousLiveSmallWinSingleton_WhenDeadBackupSwapWouldRegressShanten()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(2, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W49")]
    public void Medium_KeepsNearTenpaiShanten_WhenDeadSmallWinSingletonIsFullSafeButShapeWorse()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Tong(7, 3),
                Tiao(7, 3), Tiao(7, 1),
                Wan(8, 0), Wan(7, 3),
                Wan(5, 2),
                Tiao(3, 1),
                Wan(2, 2), Wan(3, 1),
                Tiao(2, 0),
                Wan(9, 3),
                Wan(4, 1), Wan(4, 3),
                Tong(9, 1)
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
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(2, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(2, 0)))
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Wan(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tong(3, 0))),
                        FakePung(ChangshaDeckBuilder.GetLogicalTile(Tiao(3, 0)))
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Wan(7, 0), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Wan(7, 1), TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = Tiao(3, 0), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Tong(8, 0), TurnNumber = 4 },
                new() { SeatIndex = 3, TileId = Wan(9, 0), TurnNumber = 5 },
                new() { SeatIndex = 1, TileId = Wan(4, 0), TurnNumber = 6 },
                new() { SeatIndex = 2, TileId = Tong(9, 0), TurnNumber = 7 },
                new() { SeatIndex = 3, TileId = Wan(8, 1), TurnNumber = 8 },
                new() { SeatIndex = 1, TileId = Wan(8, 2), TurnNumber = 9 },
                new() { SeatIndex = 2, TileId = Wan(8, 3), TurnNumber = 10 },
                new() { SeatIndex = 3, TileId = Tiao(5, 0), TurnNumber = 11 },
                new() { SeatIndex = 1, TileId = Wan(1, 0), TurnNumber = 12 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var shantenByLogical = hand.ConcealedTiles
            .GroupBy(ChangshaDeckBuilder.GetLogicalTile)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var probeTiles = new List<int>(hand.ConcealedTiles);
                    var idx = probeTiles.FindIndex(tileId => ChangshaDeckBuilder.GetLogicalTile(tileId) == g.Key);
                    probeTiles.RemoveAt(idx);
                    var probeHand = new ChangshaHandState
                    {
                        SeatIndex = hand.SeatIndex,
                        ConcealedTiles = probeTiles,
                        Melds = hand.Melds
                    };
                    return HandEvaluator.MinShantenToHu(probeHand, Array.Empty<int>());
                });

        var chosenLogical = ChangshaDeckBuilder.GetLogicalTile(action.TileId.Value);
        var chosenShanten = shantenByLogical[chosenLogical];
        var bestShanten = shantenByLogical.Values.Min();

        Assert.Equal(bestShanten, chosenShanten);
        Assert.Equal(1, bestShanten);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W51")]
    public void Medium_DoesNotTradeShantenForLiveSmallWinPreservationSwap_UnderFullDangerousPressure()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);

        var singletonDiscardHand = new List<int>(hand.ConcealedTiles);
        singletonDiscardHand.Remove(41);
        var singletonDiscardShanten = HandEvaluator.MinShantenToHu(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = singletonDiscardHand,
            Melds = hand.Melds
        }, Array.Empty<int>());

        var chosenDiscardHand = new List<int>(hand.ConcealedTiles);
        chosenDiscardHand.Remove(action.TileId!.Value);
        var chosenDiscardShanten = HandEvaluator.MinShantenToHu(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = chosenDiscardHand,
            Melds = hand.Melds
        }, Array.Empty<int>());

        Assert.True(chosenDiscardShanten <= singletonDiscardShanten,
            $"expected shanten-safe swap (chosen={action.TileId}, chosenShanten={chosenDiscardShanten}, singletonShanten={singletonDiscardShanten})");
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W55")]
    public void Medium_KeepsNearTenpaiShantenPrimary_WhenDangerousSafetyBiasCompetes()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(4, 0),
                Wan(5, 0), Wan(5, 1), Wan(5, 2),
                Wan(9, 0), Wan(9, 1), Wan(9, 2),
                Tong(1, 0), Tong(3, 0), Tong(8, 0),
                Tiao(2, 0), Tiao(2, 1),
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
                        FakePung(5),
                        FakePung(10)
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(1),
                        FakePung(6),
                        FakePung(11)
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = Wan(9, 0), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Tiao(1, 2), TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = Wan(5, 2), TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = Tiao(2, 1), TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = Tiao(4, 1), TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = Wan(7, 2), TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = Wan(4, 2), TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = Wan(3, 2), TurnNumber = 8 }
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
            .Distinct()
            .Select(tileId => ShantenAfterDiscard(hand, tileId))
            .Min();

        Assert.Equal(1, minShanten);

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId!.Value);
        Assert.Equal(minShanten, chosenShanten);
    }
}
