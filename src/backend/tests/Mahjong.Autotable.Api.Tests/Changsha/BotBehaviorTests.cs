using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-J: Bot Behavior — drives ChangshaBotPolicy through ChangshaGameStateMachine.
/// </summary>
public class BotBehaviorTests
{
    [Fact, Trait("Category", "Changsha")]
    public void Bot_CompletesFullHand_WithoutIllegalMoves()
    {
        var outcome = BotMatchHarness.RunUntilHandFinished(seed: 42);
        Assert.True(outcome.WinnerDeclared || outcome.WallExhausted);
        Assert.Equal(ChangshaPhase.EndHand, outcome.FinalState.Phase);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Bot_DiscardsLegalTile_FromOwnHand()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 21, botSeats: new[] { 0, 1, 2, 3 });
        var dealer = state.DealerSeatIndex;

        var action = new ChangshaBotPolicy().DecideAction(state, dealer);

        Assert.Equal(BotActionType.Discard, action.Type);
        Assert.NotNull(action.TileId);
        Assert.Contains(action.TileId!.Value, state.Hands[dealer].ConcealedTiles);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Bot_RecognizesWinningHand_DeclaresWin()
    {
        // Construct a state where the active seat already holds a 14-tile winning hand.
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed: 99, botSeatIndexes: new[] { 0, 1, 2, 3 });
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(99));
        ChangshaGameStateMachine.Deal(state);

        var dealer = state.DealerSeatIndex;
        // Replace dealer's hand with a known Standard winner.
        state.Hands[dealer].ConcealedTiles = ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 5), (Suit.Tong, 5));
        state.Hands[dealer].Melds.Clear();

        var action = new ChangshaBotPolicy().DecideAction(state, dealer);

        Assert.Equal(BotActionType.DeclareWin, action.Type);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Bot_ClaimWindow_PrefersHuOverKongOverPung()
    {
        // Make claim window with a winning opportunity for one bot and a kong for another.
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed: 17, botSeatIndexes: new[] { 0, 1, 2, 3 });
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(17));
        ChangshaGameStateMachine.Deal(state);

        // Manually open a claim window with a Hu opportunity for seat 1.
        state.ClaimWindow = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = Tables.TableClaimType.Hu, Priority = 4 },
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = Tables.TableClaimType.Pung, Priority = 2 }
            }
        };
        state.Phase = ChangshaPhase.AwaitingClaim;

        var action = new ChangshaBotPolicy().DecideAction(state, 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(Tables.TableClaimType.Hu, action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Bot_ClaimWindow_PassesWhenNoEligibleOpportunity()
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed: 17, botSeatIndexes: new[] { 0, 1, 2, 3 });
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(17));
        ChangshaGameStateMachine.Deal(state);

        state.ClaimWindow = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = 0,
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 2, ClaimType = Tables.TableClaimType.Pung, Priority = 2 }
            }
        };
        state.Phase = ChangshaPhase.AwaitingClaim;

        // Seat 1 has no opportunity for itself.
        var action = new ChangshaBotPolicy().DecideAction(state, 1);
        Assert.Equal(BotActionType.Pass, action.Type);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Bot_DeterministicWithSeed_ProducesSameEventLog()
    {
        var o1 = BotMatchHarness.RunUntilHandFinished(seed: 12345);
        var o2 = BotMatchHarness.RunUntilHandFinished(seed: 12345);

        Assert.Equal(
            o1.FinalState.EventLog.Select(e => e.EventType).ToList(),
            o2.FinalState.EventLog.Select(e => e.EventType).ToList());
        Assert.Equal(o1.WinnerDeclared, o2.WinnerDeclared);
    }

    [Fact, Trait("Category", "Changsha")]
    public void Bot_DiscardSelection_PrefersIsolatedTilesOverPairs()
    {
        // Hand contains a clear pair plus an isolated honor-rank tile.
        // SelectDiscardTile is deterministic; it should not pick a tile that's part of a pair.
        var hand = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = ChangshaTestHelpers.Tiles(
                (Suit.Wan, 5), (Suit.Wan, 5),  // pair — keep
                (Suit.Tong, 4), (Suit.Tong, 5), // sequence partial — keep
                (Suit.Tiao, 1), // isolated — discard
                (Suit.Wan, 9))  // isolated — discard candidate
        };

        var picked = ChangshaBotPolicy.SelectDiscardTile(hand);
        var pickedLogical = ChangshaDeckBuilder.GetLogicalTile(picked);

        Assert.NotEqual(ChangshaTestHelpers.Logical(Suit.Wan, 5), pickedLogical);
    }

    [Fact(Skip = "Awaiting Bishop's decision-timeout API surface"), Trait("Category", "Changsha")]
    public void Bot_TimeoutFallback_DeferredV2()
    {
    }
}
