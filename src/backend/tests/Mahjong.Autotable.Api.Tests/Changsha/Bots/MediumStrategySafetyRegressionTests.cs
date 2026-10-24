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
}
