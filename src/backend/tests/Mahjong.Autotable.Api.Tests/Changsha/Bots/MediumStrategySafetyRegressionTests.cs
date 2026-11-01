using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Changsha.Bot.Heuristics;
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

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W56")]
    public void Medium_PrefersLowerShantenFullDangerousGenbutsu_WhenScoreIsNotWorse()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                Wan(1, 0),
                Wan(2, 0),
                Wan(4, 0),
                Wan(7, 0),
                Tong(4, 0), Tong(4, 1), Tong(4, 2),
                Tong(9, 0),
                Tiao(4, 0), Tiao(5, 0), Tiao(6, 0),
                Tiao(8, 0), Tiao(8, 1),
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
                new() { SeatIndex = 1, TileId = Wan(2, 1), TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = Wan(2, 2), TurnNumber = 2 },
                new() { SeatIndex = 1, TileId = Tiao(9, 1), TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = Tiao(9, 2), TurnNumber = 4 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tiao(9, 0), action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W57")]
    public void Medium_DeadSmallWinSingletonChoice_PreservesBestShantenTier()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                50, 66, 56, 91, 69, 75, 95, 18, 54, 21, 76, 41, 70, 106
            }
        };

        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingDiscard,
            ActiveSeatIndex = 0,
            Hands = new List<ChangshaHandState>
            {
                hand,
                new() { SeatIndex = 1 },
                new() { SeatIndex = 2 },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 73, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 89, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 25, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 99, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 38, TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = 20, TurnNumber = 6 },
                new() { SeatIndex = 1, TileId = 6, TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = 23, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 19, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 9, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 7, TurnNumber = 11 },
                new() { SeatIndex = 3, TileId = 84, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 29, TurnNumber = 13 },
                new() { SeatIndex = 2, TileId = 102, TurnNumber = 14 },
                new() { SeatIndex = 3, TileId = 63, TurnNumber = 15 },
                new() { SeatIndex = 1, TileId = 65, TurnNumber = 16 },
                new() { SeatIndex = 2, TileId = 96, TurnNumber = 17 },
                new() { SeatIndex = 3, TileId = 72, TurnNumber = 18 },
                new() { SeatIndex = 1, TileId = 43, TurnNumber = 19 },
                new() { SeatIndex = 2, TileId = 103, TurnNumber = 20 },
                new() { SeatIndex = 3, TileId = 88, TurnNumber = 21 },
                new() { SeatIndex = 1, TileId = 64, TurnNumber = 22 },
                new() { SeatIndex = 2, TileId = 48, TurnNumber = 23 },
                new() { SeatIndex = 3, TileId = 3, TurnNumber = 24 },
                new() { SeatIndex = 1, TileId = 59, TurnNumber = 25 },
                new() { SeatIndex = 2, TileId = 100, TurnNumber = 26 },
                new() { SeatIndex = 3, TileId = 28, TurnNumber = 27 },
                new() { SeatIndex = 1, TileId = 107, TurnNumber = 28 },
                new() { SeatIndex = 2, TileId = 46, TurnNumber = 29 },
                new() { SeatIndex = 3, TileId = 90, TurnNumber = 30 },
                new() { SeatIndex = 1, TileId = 45, TurnNumber = 31 },
                new() { SeatIndex = 2, TileId = 35, TurnNumber = 32 },
                new() { SeatIndex = 3, TileId = 16, TurnNumber = 33 },
                new() { SeatIndex = 1, TileId = 82, TurnNumber = 34 },
                new() { SeatIndex = 2, TileId = 85, TurnNumber = 35 },
                new() { SeatIndex = 3, TileId = 67, TurnNumber = 36 }
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

        var deadSmallWinSingletons = new[] { 66, 91 };
        var minDeadSingletonShanten = deadSmallWinSingletons.Min(tileId => ShantenAfterDiscard(hand, tileId));

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId!.Value);
        Assert.Equal(minDeadSingletonShanten, chosenShanten);
        Assert.Equal(66, action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W58")]
    public void Medium_DeadSingletonSafetySwap_DoesNotLeaveBestShantenTierNearTenpai()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                97, 84, 81, 100, 39, 90, 87, 24, 82, 6, 46, 104, 63, 4
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
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 44, 45, 46 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 88, 89, 90 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 16, 17, 18 } }
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 104, 105, 106 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 80, 81, 82 } },
                        new() { Kind = MeldKind.Pung, TileIds = new List<int> { 96, 97, 98 } }
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 1, TileId = 79, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 78, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 42, TurnNumber = 3 },
                new() { SeatIndex = 3, TileId = 37, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 22, TurnNumber = 5 },
                new() { SeatIndex = 2, TileId = 93, TurnNumber = 6 },
                new() { SeatIndex = 3, TileId = 2, TurnNumber = 7 },
                new() { SeatIndex = 2, TileId = 71, TurnNumber = 8 },
                new() { SeatIndex = 2, TileId = 40, TurnNumber = 9 },
                new() { SeatIndex = 2, TileId = 33, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 80, TurnNumber = 11 }
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

        var minShanten = hand.ConcealedTiles.Min(tileId => ShantenAfterDiscard(hand, tileId));
        Assert.Equal(1, minShanten);

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId!.Value);
        Assert.Equal(minShanten, chosenShanten);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W60")]
    public void Medium_KeepsBestPracticalShantenTier_WhenDeadSingletonSafetyBiasCompetesAtTwoShanten()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>
            {
                0, 4, 9, 26, 41, 45, 62, 69, 76, 79, 80, 83, 90, 92
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
                        FakePung(3),
                        FakePung(8),
                        FakePung(15)
                    }
                },
                new()
                {
                    SeatIndex = 2,
                    Melds = new List<Meld>
                    {
                        FakePung(2),
                        FakePung(13),
                        FakePung(20)
                    }
                },
                new() { SeatIndex = 3 }
            },
            DiscardPile = new List<ChangshaDiscard>
            {
                new() { SeatIndex = 2, TileId = 42, TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = 4, TurnNumber = 2 },
                new() { SeatIndex = 2, TileId = 4, TurnNumber = 3 },
                new() { SeatIndex = 1, TileId = 18, TurnNumber = 4 },
                new() { SeatIndex = 1, TileId = 106, TurnNumber = 5 },
                new() { SeatIndex = 1, TileId = 22, TurnNumber = 6 },
                new() { SeatIndex = 2, TileId = 52, TurnNumber = 7 },
                new() { SeatIndex = 1, TileId = 63, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 35, TurnNumber = 9 },
                new() { SeatIndex = 2, TileId = 22, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 54, TurnNumber = 11 },
                new() { SeatIndex = 3, TileId = 102, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 61, TurnNumber = 13 },
                new() { SeatIndex = 3, TileId = 16, TurnNumber = 14 },
                new() { SeatIndex = 2, TileId = 52, TurnNumber = 15 },
                new() { SeatIndex = 3, TileId = 40, TurnNumber = 16 },
                new() { SeatIndex = 2, TileId = 73, TurnNumber = 17 },
                new() { SeatIndex = 3, TileId = 32, TurnNumber = 18 },
                new() { SeatIndex = 1, TileId = 4, TurnNumber = 19 },
                new() { SeatIndex = 1, TileId = 1, TurnNumber = 20 },
                new() { SeatIndex = 1, TileId = 84, TurnNumber = 21 },
                new() { SeatIndex = 2, TileId = 106, TurnNumber = 22 },
                new() { SeatIndex = 3, TileId = 35, TurnNumber = 23 },
                new() { SeatIndex = 1, TileId = 81, TurnNumber = 24 }
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

        var minShanten = hand.ConcealedTiles.Min(tileId => ShantenAfterDiscard(hand, tileId));
        Assert.Equal(2, minShanten);

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId!.Value);
        Assert.Equal(minShanten, chosenShanten);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W61")]
    public void Medium_PrefersLowerShantenFullGenbutsuAlternate_WhenPreservingLiveSmallWinSingleton()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId!.Value);
        var bestFullSafetyShanten = hand.ConcealedTiles
            .Where(tileId => TenpaiDetector.SafetyBias(tileId, state, 0) <= -2)
            .Distinct()
            .Min(tileId => ShantenAfterDiscard(hand, tileId));

        Assert.Equal(bestFullSafetyShanten, chosenShanten);
        Assert.Equal(77, action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W63")]
    public void Medium_PrefersLowerShantenFullGenbutsuUpgrade_EvenWhenKeepScoreIsSlightlyWorse()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 28, 59, 11, 44, 39, 62, 7, 99, 17, 21, 56, 77, 2, 101 }
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
                new() { SeatIndex = 1, TileId = 37, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 107, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 35, TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = 18, TurnNumber = 4 },
                new() { SeatIndex = 2, TileId = 75, TurnNumber = 5 },
                new() { SeatIndex = 2, TileId = 82, TurnNumber = 6 },
                new() { SeatIndex = 2, TileId = 28, TurnNumber = 7 },
                new() { SeatIndex = 1, TileId = 51, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 4, TurnNumber = 9 },
                new() { SeatIndex = 3, TileId = 80, TurnNumber = 10 },
                new() { SeatIndex = 2, TileId = 57, TurnNumber = 11 },
                new() { SeatIndex = 2, TileId = 38, TurnNumber = 12 },
                new() { SeatIndex = 3, TileId = 53, TurnNumber = 13 },
                new() { SeatIndex = 1, TileId = 62, TurnNumber = 14 },
                new() { SeatIndex = 3, TileId = 57, TurnNumber = 15 },
                new() { SeatIndex = 3, TileId = 49, TurnNumber = 16 },
                new() { SeatIndex = 1, TileId = 20, TurnNumber = 17 },
                new() { SeatIndex = 1, TileId = 92, TurnNumber = 18 },
                new() { SeatIndex = 2, TileId = 88, TurnNumber = 19 },
                new() { SeatIndex = 1, TileId = 63, TurnNumber = 20 },
                new() { SeatIndex = 1, TileId = 79, TurnNumber = 21 },
                new() { SeatIndex = 2, TileId = 78, TurnNumber = 22 },
                new() { SeatIndex = 1, TileId = 57, TurnNumber = 23 },
                new() { SeatIndex = 2, TileId = 56, TurnNumber = 24 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(77, action.TileId);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W64")]
    public void Medium_PrefersLowerShantenSingleGenbutsu_WhenAlreadySingleSafeFarFromTenpai()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);
        Assert.True(TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0) <= -1);

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId.Value);
        var bestDangerousShanten = hand.ConcealedTiles
            .Where(tileId => TenpaiDetector.SafetyBias(tileId, state, 0) <= -1)
            .Distinct()
            .Min(tileId => ShantenAfterDiscard(hand, tileId));

        Assert.Equal(bestDangerousShanten, chosenShanten);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W65")]
    public void Medium_PrefersLowerShantenUnsafeFallback_WhenPreservingLiveSmallWinSingletonUnderFullDangerousPressure()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 88, 76, 93, 74, 46, 103, 67, 102, 38, 60, 107, 86, 78, 65 }
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
                new() { SeatIndex = 1, TileId = 89, TurnNumber = 1 },
                new() { SeatIndex = 2, TileId = 90, TurnNumber = 2 }
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);

        var chosenLogical = ChangshaDeckBuilder.GetLogicalTile(action.TileId.Value);
        var chosenRank = chosenLogical % 9 + 1;
        Assert.DoesNotContain(chosenRank, new[] { 2, 5, 8 });

        var chosenShanten = ShantenAfterDiscard(hand, action.TileId.Value);
        var bestNonSmallWinShanten = hand.ConcealedTiles
            .Where(tileId =>
            {
                var logical = ChangshaDeckBuilder.GetLogicalTile(tileId);
                var rank = logical % 9 + 1;
                return rank is not (2 or 5 or 8);
            })
            .Distinct()
            .Min(tileId => ShantenAfterDiscard(hand, tileId));

        Assert.Equal(bestNonSmallWinShanten, chosenShanten);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W66")]
    public void Medium_KeepsSingleSafeLiveSmallWinSingleton_WhenFallbackIsNotSafer()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(53, action.TileId);
        Assert.True(TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W67")]
    public void Medium_PromotesSaferSmallWinSingleton_WhenSingleDangerousPressureExists()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Wan(5, 0), action.TileId);
        Assert.True(TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W68")]
    public void Medium_PromotesSaferSmallWinSingleton_WhenSaferSingletonHasSlightlyHigherKeepScore()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(Tong(5, 0), action.TileId);
        Assert.True(TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W70")]
    public void Medium_PrefersLowerShantenSmallWinSingleton_WhenSingleDangerousSafetyTies()
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(7, action.TileId);
        Assert.Equal(-1, TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0));
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W75")]
    public void Medium_PromotesSaferLiveSmallWinSingleton_WhenBackupSmallWinPairExistsUnderSingleDanger()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 93, 7, 91, 6, 30, 81, 46, 67, 18, 95, 10, 48, 17, 97 }
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
                new() { SeatIndex = 1, TileId = 91, TurnNumber = 1 }
            }
        };

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(91, action.TileId);
        Assert.True(TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0) <= -1);
    }

    [Fact, Trait("Category", "Bot"), Trait("Wave", "W76")]
    public void Medium_PrefersLowerShantenSingleGenbutsu_WhenAlreadySingleSafeAndScoreGapIsTwo()
    {
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int> { 21, 3, 32, 45, 67, 70, 22, 18, 35, 68, 20, 52, 43, 4 }
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
                new() { SeatIndex = 1, TileId = 47, TurnNumber = 1 },
                new() { SeatIndex = 1, TileId = 28, TurnNumber = 2 },
                new() { SeatIndex = 3, TileId = 90, TurnNumber = 3 },
                new() { SeatIndex = 2, TileId = 61, TurnNumber = 4 },
                new() { SeatIndex = 1, TileId = 2, TurnNumber = 5 },
                new() { SeatIndex = 3, TileId = 24, TurnNumber = 6 },
                new() { SeatIndex = 2, TileId = 12, TurnNumber = 7 },
                new() { SeatIndex = 1, TileId = 56, TurnNumber = 8 },
                new() { SeatIndex = 3, TileId = 0, TurnNumber = 9 },
                new() { SeatIndex = 1, TileId = 31, TurnNumber = 10 },
                new() { SeatIndex = 3, TileId = 65, TurnNumber = 11 },
                new() { SeatIndex = 1, TileId = 57, TurnNumber = 12 },
                new() { SeatIndex = 1, TileId = 38, TurnNumber = 13 },
                new() { SeatIndex = 2, TileId = 66, TurnNumber = 14 },
                new() { SeatIndex = 2, TileId = 101, TurnNumber = 15 },
                new() { SeatIndex = 1, TileId = 26, TurnNumber = 16 },
                new() { SeatIndex = 1, TileId = 7, TurnNumber = 17 },
                new() { SeatIndex = 1, TileId = 17, TurnNumber = 18 }
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

        var medium = ChangshaBotEngine.Resolve("medium");
        var action = medium.DecideAction(state, botSeatIndex: 0);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.Equal(18, action.TileId);
        Assert.True(TenpaiDetector.SafetyBias(action.TileId!.Value, state, 0) <= -1);
        Assert.True(ShantenAfterDiscard(hand, action.TileId.Value) < ShantenAfterDiscard(hand, 3));
    }
}
