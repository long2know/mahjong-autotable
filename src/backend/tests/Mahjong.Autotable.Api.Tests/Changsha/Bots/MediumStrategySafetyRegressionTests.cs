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
}
