using System.Reflection;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Bot;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Tables;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
    public void Bot_ClaimWindow_NonHuFallbackPrefersChowShape_OverPung_WhenChowHasStrongerOuts()
    {
        var (state, _) = ChangshaGameStateMachine.CreateGame(seed: 1717, botSeatIndexes: new[] { 0, 1, 2, 3 });
        ChangshaGameStateMachine.StartGame(state);
        ChangshaGameStateMachine.RollDice(state, new DiceService(1717));
        ChangshaGameStateMachine.Deal(state);

        var discardTong8 = ChangshaTestHelpers.Tid(Suit.Tong, 8, 1);
        var chowTong6 = ChangshaTestHelpers.Tid(Suit.Tong, 6, 3);
        var chowTong7 = ChangshaTestHelpers.Tid(Suit.Tong, 7, 2);

        state.Hands[1].ConcealedTiles.Clear();
        state.Hands[1].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 4, 2), ChangshaTestHelpers.Tid(Suit.Wan, 4, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 3, 0), ChangshaTestHelpers.Tid(Suit.Tong, 4, 2), ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            chowTong6, chowTong7,
            ChangshaTestHelpers.Tid(Suit.Tong, 8, 2), ChangshaTestHelpers.Tid(Suit.Tong, 8, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 3, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 4, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 5, 0)
        });
        state.Hands[1].Melds.Clear();

        state.ClaimWindow = new ChangshaClaimWindow
        {
            DiscardSeatIndex = 0,
            DiscardTileId = discardTong8,
            Opportunities = new()
            {
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Pung, Priority = 2 },
                new ChangshaClaimOpportunity { SeatIndex = 1, ClaimType = TableClaimType.Chow, Priority = 1 }
            }
        };
        state.Phase = ChangshaPhase.AwaitingClaim;

        var action = new ChangshaBotPolicy().DecideAction(state, 1);

        Assert.Equal(BotActionType.Claim, action.Type);
        Assert.Equal(TableClaimType.Chow, action.ClaimType);
        Assert.NotNull(action.ClaimTileIds);
        Assert.Contains(chowTong6, action.ClaimTileIds!);
        Assert.Contains(chowTong7, action.ClaimTileIds!);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task Bot_ClaimWindow_UsesExplicitChowTileIds_WhenStrategyProvidesBestShape()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 5_000;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var gameId = await runtime.CreateGameAsync(
            seed: 1226,
            botSeatIndexes: new[] { 1 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        var discardTong6 = ChangshaTestHelpers.Tid(Suit.Tong, 6, 1);
        var chosenTong7 = ChangshaTestHelpers.Tid(Suit.Tong, 7, 3);
        var chosenTong8 = ChangshaTestHelpers.Tid(Suit.Tong, 8, 0);

        // Seat 1 can chow Tong-6 either as 5-6-7 (legacy lowest-pattern fallback)
        // or 6-7-8 (stronger shape). The injected bot strategy requests 6-7-8.
        live.Hands[1].ConcealedTiles.Clear();
        live.Hands[1].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 1), ChangshaTestHelpers.Tid(Suit.Wan, 6, 0), ChangshaTestHelpers.Tid(Suit.Wan, 7, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 3, 0), ChangshaTestHelpers.Tid(Suit.Tong, 5, 2),
            ChangshaTestHelpers.Tid(Suit.Tong, 6, 0), ChangshaTestHelpers.Tid(Suit.Tong, 6, 3),
            chosenTong7, chosenTong8,
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 4, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0)
        });
        live.Hands[1].Melds.Clear();

        // Seat 0 holds the discard tile and no one else is set up to claim.
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Tong, 9, 0), ChangshaTestHelpers.Tid(Suit.Tong, 9, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 8, 1), ChangshaTestHelpers.Tid(Suit.Tong, 7, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 5, 0), ChangshaTestHelpers.Tid(Suit.Tong, 4, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0), ChangshaTestHelpers.Tid(Suit.Wan, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 7, 0), ChangshaTestHelpers.Tid(Suit.Wan, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0),
            discardTong6
        });
        live.Hands[0].Melds.Clear();

        var scripted = new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingClaim
                ? BotAction.Claim(TableClaimType.Chow, new[] { chosenTong7, chosenTong8 })
                : BotAction.Pass());
        InjectStrategyOrFail(runtime, scripted);

        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: discardTong6, cts.Token);

        await WaitForAsync(
            () => Snapshot(runtime, gameId).ClaimWindow is null
               && Snapshot(runtime, gameId).Hands[1].Melds.Any(),
            TimeSpan.FromSeconds(3),
            "bot chow claim was not resolved");

        var meld = Snapshot(runtime, gameId).Hands[1].Melds.Single();
        Assert.Equal(MeldKind.Chow, meld.Kind);
        Assert.Contains(discardTong6, meld.TileIds);
        Assert.Contains(chosenTong7, meld.TileIds);
        Assert.Contains(chosenTong8, meld.TileIds);
        Assert.DoesNotContain(ChangshaTestHelpers.Tid(Suit.Tong, 5, 2), meld.TileIds);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W93")]
    public async Task Bot_InvalidMatchingClaimTileIds_FallBackToCanonicalClaimTiles_WithoutStallingClaimResolution()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 5_000;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var gameId = await runtime.CreateGameAsync(
            seed: 8891,
            botSeatIndexes: new[] { 1 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        var discardWan5 = ChangshaTestHelpers.Tid(Suit.Wan, 5, 3);

        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            discardWan5,
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0), ChangshaTestHelpers.Tid(Suit.Wan, 2, 0), ChangshaTestHelpers.Tid(Suit.Wan, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 0), ChangshaTestHelpers.Tid(Suit.Tong, 2, 0), ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 0), ChangshaTestHelpers.Tid(Suit.Tong, 6, 0), ChangshaTestHelpers.Tid(Suit.Tong, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 4, 0)
        });
        live.Hands[0].Melds.Clear();

        live.Hands[1].ConcealedTiles.Clear();
        live.Hands[1].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 7, 1), ChangshaTestHelpers.Tid(Suit.Wan, 8, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 1), ChangshaTestHelpers.Tid(Suit.Tong, 2, 1), ChangshaTestHelpers.Tid(Suit.Tong, 3, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 1), ChangshaTestHelpers.Tid(Suit.Tong, 6, 1),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 1),
            ChangshaTestHelpers.Tid(Suit.Tiao, 3, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 4, 1)
        });
        live.Hands[1].Melds.Clear();

        live.Hands[2].ConcealedTiles.Clear();
        live.Hands[2].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 2), ChangshaTestHelpers.Tid(Suit.Wan, 2, 2), ChangshaTestHelpers.Tid(Suit.Wan, 3, 2),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 2), ChangshaTestHelpers.Tid(Suit.Tong, 2, 2), ChangshaTestHelpers.Tid(Suit.Tong, 3, 2),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 2), ChangshaTestHelpers.Tid(Suit.Tong, 6, 2),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 2), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 2), ChangshaTestHelpers.Tid(Suit.Tiao, 3, 2),
            ChangshaTestHelpers.Tid(Suit.Tiao, 4, 2), ChangshaTestHelpers.Tid(Suit.Tiao, 5, 2)
        });
        live.Hands[2].Melds.Clear();

        live.Hands[3].ConcealedTiles.Clear();
        live.Hands[3].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 3), ChangshaTestHelpers.Tid(Suit.Wan, 2, 3), ChangshaTestHelpers.Tid(Suit.Wan, 3, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 3), ChangshaTestHelpers.Tid(Suit.Tong, 2, 3), ChangshaTestHelpers.Tid(Suit.Tong, 3, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 3), ChangshaTestHelpers.Tid(Suit.Tong, 6, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 3, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 4, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 5, 3)
        });
        live.Hands[3].Melds.Clear();

        live.ClaimWindow = null;
        live.MissedWinSeats.Clear();
        live.ActiveSeatIndex = 0;
        live.Phase = ChangshaPhase.AwaitingDiscard;

        var scripted = new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingClaim
                ? BotAction.Claim(TableClaimType.Pung, new[]
                {
                    ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0),
                    ChangshaTestHelpers.Tid(Suit.Tiao, 9, 1)
                })
                : BotAction.Pass());
        InjectStrategyOrFail(runtime, scripted);

        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: discardWan5, cts.Token);

        await WaitForAsync(
            () =>
            {
                var snap = Snapshot(runtime, gameId);
                return snap.ClaimWindow is null
                    && snap.Phase == ChangshaPhase.AwaitingDiscard
                    && snap.ActiveSeatIndex == 1
                    && snap.Hands[1].Melds.Any();
            },
            TimeSpan.FromSeconds(3),
            "invalid bot pung selection did not fall back to canonical claim tile selection");

        var resolved = Snapshot(runtime, gameId);
        Assert.Null(resolved.ClaimWindow);
        var meld = Assert.Single(resolved.Hands[1].Melds);
        Assert.Equal(MeldKind.Pung, meld.Kind);
        Assert.Contains(discardWan5, meld.TileIds);
        Assert.Equal(3, meld.TileIds.Count);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W90")]
    public async Task Bot_InvalidOwnTurnDiscard_UsesFallbackDiscard_WithoutStallingTurnLoop()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 25;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Discard(tileId: 99_999)
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9043,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var initialHandTiles = Snapshot(runtime, gameId).Hands[0].ConcealedTiles.ToHashSet();

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "invalid bot discard did not fall back to a legal discard");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.NotEqual(99_999, discarded);
        Assert.Contains(discarded, initialHandTiles);
        Assert.DoesNotContain(discarded, resolved.Hands[0].ConcealedTiles);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W100")]
    public async Task Bot_InvalidOwnTurnDiscard_UsesMediumFallback_ToTakeSelfDrawHu()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Discard(tileId: 99_999)
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9052,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 5), (Suit.Tong, 5)));
        live.Hands[0].Melds.Clear();
        live.LastDrawSeatIndex = 0;
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.EventLog.Any(e => e.EventType == "win-declared" && e.SeatIndex == 0),
            TimeSpan.FromSeconds(2),
            "invalid own-turn discard did not use medium fallback to convert self-draw Hu",
            cts.Token);

        var resolved = await SnapshotCopyAsync(runtime, gameId, cts.Token);
        Assert.Contains(resolved.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 0);
        Assert.Contains(resolved.EventLog, e => e.EventType == "scoring-complete" && e.SeatIndex == 0);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "discard" && e.SeatIndex == 0);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W102")]
    public async Task Bot_SelfDrawHuOverflow_UsesMediumFallbackDiscard_InsteadOfStallingTurnLoop()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.DeclareWin()
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9053,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 5), (Suit.Tong, 5)));
        live.Hands[0].Melds.Clear();
        live.LastDrawSeatIndex = 0;

        // Force any self-draw settlement attempt to overflow checked cumulative-score arithmetic.
        live.CumulativeScores[0] = int.MaxValue;
        live.CumulativeScores[1] = 0;
        live.CumulativeScores[2] = 0;
        live.CumulativeScores[3] = 0;

        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));

        var expectedSafeDiscard = MediumStrategy.SelectDiscardTile(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>(live.Hands[0].ConcealedTiles),
        });

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "overflowing self-draw Hu proposal did not fall back to deterministic discard");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.Equal(expectedSafeDiscard, discarded);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 0);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "scoring-complete" && e.SeatIndex == 0);
        Assert.Null(resolved.CurrentWin);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W105")]
    public async Task Bot_SelfDrawHuOverflow_UsesMediumNonHuFallback_ToTakeConcealedKong_WhenLegal()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.DeclareWin()
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9054,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 2),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 3),
            ChangshaTestHelpers.Tid(Suit.Wan, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 8, 1),
        });
        live.Hands[0].Melds.Clear();
        live.Hands[0].Melds.Add(new Meld
        {
            Kind = MeldKind.Chow,
            TileIds = new List<int>
            {
                ChangshaTestHelpers.Tid(Suit.Tong, 1, 0),
                ChangshaTestHelpers.Tid(Suit.Tong, 2, 0),
                ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            }
        });
        live.LastDrawSeatIndex = 0;

        // Force any self-draw settlement attempt to overflow checked cumulative-score arithmetic.
        live.CumulativeScores[0] = int.MaxValue;
        live.CumulativeScores[1] = 0;
        live.CumulativeScores[2] = 0;
        live.CumulativeScores[3] = 0;

        var concealedKongLogical = ChangshaTestHelpers.Logical(Suit.Wan, 5);
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));
        Assert.True(ChangshaGameStateMachine.CanDeclareConcealedKong(live, 0, concealedKongLogical));

        var baselineEventCount = live.EventLog.Count;

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.EventLog
                .Skip(baselineEventCount)
                .Any(e => e.EventType == "concealed-kong" && e.SeatIndex == 0),
            TimeSpan.FromSeconds(2),
            "overflowed self-draw Hu did not use medium non-hu fallback to take concealed kong",
            cts.Token);

        var resolved = await SnapshotCopyAsync(runtime, gameId, cts.Token);
        Assert.Contains(resolved.EventLog, e => e.EventType == "concealed-kong" && e.SeatIndex == 0);
        Assert.Contains(resolved.EventLog, e => e.EventType == "kong-replacement-drawn" && e.SeatIndex == 0);
        Assert.Contains(resolved.Hands[0].Melds, m => m.Kind == MeldKind.ConcealedKong);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 0);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "scoring-complete" && e.SeatIndex == 0);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W91")]
    public async Task Bot_InvalidOwnTurnConcealedKong_UsesFallbackDiscard_WithoutStallingTurnLoop()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 25;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.DeclareConcealedKong(logicalTile: 999_999)
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9044,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var initialHandTiles = Snapshot(runtime, gameId).Hands[0].ConcealedTiles.ToHashSet();

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "invalid concealed-kong proposal did not fall back to a legal discard");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.Contains(discarded, initialHandTiles);
        Assert.DoesNotContain(discarded, resolved.Hands[0].ConcealedTiles);
        Assert.Empty(resolved.Hands[0].Melds);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W92")]
    public async Task Bot_InvalidOwnTurnAddedKong_UsesFallbackDiscard_WithoutStallingTurnLoop()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 25;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.DeclareAddedKong(tileId: 999_999)
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9045,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var initialHandTiles = Snapshot(runtime, gameId).Hands[0].ConcealedTiles.ToHashSet();

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "invalid added-kong proposal did not fall back to a legal discard");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.Contains(discarded, initialHandTiles);
        Assert.DoesNotContain(discarded, resolved.Hands[0].ConcealedTiles);
        Assert.Empty(resolved.Hands[0].Melds);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W93")]
    public async Task Bot_InvalidOwnTurnSelfDrawWin_UsesFallbackDiscard_WithoutDeclaringWin()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.DeclareWin()
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9046,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        // Ensure the active bot hand is definitely non-winning so DeclareWin is invalid
        // and must degrade to the deterministic discard fallback.
        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0), ChangshaTestHelpers.Tid(Suit.Wan, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 7, 0), ChangshaTestHelpers.Tid(Suit.Wan, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 0), ChangshaTestHelpers.Tid(Suit.Tong, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 0), ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 7, 0), ChangshaTestHelpers.Tid(Suit.Tong, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0)
        });
        live.Hands[0].Melds.Clear();
        var initialHandTiles = live.Hands[0].ConcealedTiles.ToHashSet();

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "invalid self-draw win proposal did not fall back to a legal discard");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.Contains(discarded, initialHandTiles);
        Assert.DoesNotContain(discarded, resolved.Hands[0].ConcealedTiles);
        Assert.Null(resolved.CurrentWin);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "win-declared");
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W94")]
    public async Task Bot_InvalidOwnTurnPass_UsesDeterministicDiscardFallback_InsteadOfHandOrderTail()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Pass()
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9047,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        // Put a deterministic hand where the medium fallback discard differs from the hand-order tail.
        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 0), ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 0), ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 1, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 2)
        });
        live.Hands[0].Melds.Clear();

        var expectedSafeDiscard = MediumStrategy.SelectDiscardTile(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>(live.Hands[0].ConcealedTiles),
        });
        var handTailTile = live.Hands[0].ConcealedTiles[^1];
        Assert.NotEqual(handTailTile, expectedSafeDiscard);

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "invalid own-turn pass did not degrade to deterministic discard fallback");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.Equal(expectedSafeDiscard, discarded);
        Assert.DoesNotContain(discarded, resolved.Hands[0].ConcealedTiles);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W95")]
    public async Task Bot_InvalidOwnTurnClaim_UsesDeterministicDiscardFallback_InsteadOfStalling()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Claim(TableClaimType.Pung, new[]
                {
                    ChangshaTestHelpers.Tid(Suit.Wan, 5, 0),
                    ChangshaTestHelpers.Tid(Suit.Wan, 5, 1)
                })
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9048,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        // Use a deterministic hand where medium fallback differs from the hand tail,
        // proving runtime used the policy fallback instead of a random/order artifact.
        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 0), ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 0), ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 1, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 2)
        });
        live.Hands[0].Melds.Clear();

        var expectedSafeDiscard = MediumStrategy.SelectDiscardTile(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>(live.Hands[0].ConcealedTiles),
        });

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromSeconds(2),
            "invalid own-turn claim did not degrade to deterministic discard fallback");

        var resolved = Snapshot(runtime, gameId);
        var discarded = resolved.DiscardPile.Last().TileId;

        Assert.Equal(expectedSafeDiscard, discarded);
        Assert.DoesNotContain(discarded, resolved.Hands[0].ConcealedTiles);
        Assert.Empty(resolved.Hands[0].Melds);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W98")]
    public async Task Bot_InvalidOwnTurnClaim_UsesMediumFallback_ToTakeSelfDrawHu()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Claim(TableClaimType.Pung, new[]
                {
                    ChangshaTestHelpers.Tid(Suit.Wan, 5, 0),
                    ChangshaTestHelpers.Tid(Suit.Wan, 5, 1)
                })
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9050,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        // Give seat 0 a deterministic self-draw winning hand. If runtime downgrades
        // unsupported own-turn actions to medium own-turn fallback, bot should Hu.
        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 5), (Suit.Tong, 5)));
        live.Hands[0].Melds.Clear();
        live.LastDrawSeatIndex = 0;
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.EventLog.Any(e => e.EventType == "win-declared" && e.SeatIndex == 0),
            TimeSpan.FromSeconds(2),
            "invalid own-turn claim did not use medium fallback to convert self-draw Hu",
            cts.Token);

        var resolved = await SnapshotCopyAsync(runtime, gameId, cts.Token);
        Assert.Contains(resolved.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 0);
        Assert.Contains(resolved.EventLog, e => e.EventType == "scoring-complete" && e.SeatIndex == 0);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "discard" && e.SeatIndex == 0);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W100")]
    public async Task Bot_UnsupportedOwnTurnAction_UsesMediumFallback_ToTakeConcealedKong()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Claim(TableClaimType.Pung, new[]
                {
                    ChangshaTestHelpers.Tid(Suit.Wan, 1, 0),
                    ChangshaTestHelpers.Tid(Suit.Wan, 1, 1)
                })
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9052,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 2),
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 9, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 7, 0),
        });
        live.Hands[0].Melds.Clear();
        live.LastDrawSeatIndex = 0;

        var concealedKongLogical = ChangshaTestHelpers.Logical(Suit.Wan, 1);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));
        Assert.True(ChangshaGameStateMachine.CanDeclareConcealedKong(live, 0, concealedKongLogical));

        var baselineEventCount = live.EventLog.Count;

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.EventLog
                .Skip(baselineEventCount)
                .Any(e => e.EventType == "concealed-kong" && e.SeatIndex == 0),
            TimeSpan.FromSeconds(2),
            "unsupported own-turn action did not use medium fallback to take concealed kong",
            cts.Token);

        var resolved = await SnapshotCopyAsync(runtime, gameId, cts.Token);
        Assert.Contains(resolved.EventLog, e => e.EventType == "concealed-kong" && e.SeatIndex == 0);
        Assert.Contains(resolved.EventLog, e => e.EventType == "kong-replacement-drawn" && e.SeatIndex == 0);
        Assert.Contains(resolved.Hands[0].Melds, m => m.Kind == MeldKind.ConcealedKong);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W101")]
    public async Task Bot_UnsupportedOwnTurnAction_UsesMediumFallback_ToTakeAddedKong()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingDiscard
                ? BotAction.Claim(TableClaimType.Pung, new[]
                {
                    ChangshaTestHelpers.Tid(Suit.Wan, 2, 0),
                    ChangshaTestHelpers.Tid(Suit.Wan, 2, 1)
                })
                : BotAction.Pass()));

        var gameId = await runtime.CreateGameAsync(
            seed: 9053,
            botSeatIndexes: new[] { 0 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 2, 3),
            ChangshaTestHelpers.Tid(Suit.Wan, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 4, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0),
        });
        live.Hands[0].Melds.Clear();
        live.Hands[0].Melds.Add(new Meld
        {
            Kind = MeldKind.Pung,
            TileIds = new List<int>
            {
                ChangshaTestHelpers.Tid(Suit.Wan, 2, 0),
                ChangshaTestHelpers.Tid(Suit.Wan, 2, 1),
                ChangshaTestHelpers.Tid(Suit.Wan, 2, 2)
            }
        });
        live.LastDrawSeatIndex = 0;

        var addedKongTile = ChangshaTestHelpers.Tid(Suit.Wan, 2, 3);
        Assert.False(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));
        Assert.True(ChangshaGameStateMachine.CanDeclareAddedKong(live, 0, addedKongTile));

        var baselineEventCount = live.EventLog.Count;

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.EventLog
                .Skip(baselineEventCount)
                .Any(e => e.EventType == "added-kong" && e.SeatIndex == 0),
            TimeSpan.FromSeconds(2),
            "unsupported own-turn action did not use medium fallback to take added kong",
            cts.Token);

        var resolved = await SnapshotCopyAsync(runtime, gameId, cts.Token);
        Assert.Contains(resolved.EventLog, e => e.EventType == "added-kong" && e.SeatIndex == 0 && e.TileId == addedKongTile);
        Assert.Contains(resolved.EventLog, e => e.EventType == "kong-replacement-drawn" && e.SeatIndex == 0);
        Assert.Contains(resolved.Hands[0].Melds, m => m.Kind == MeldKind.AddedKong && m.TileIds.Count == 4 && m.TileIds.Contains(addedKongTile));
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "discard" && e.SeatIndex == 0);
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

    [Fact, Trait("Category", "Changsha")]
    public async Task Bot_TimeoutFallback_FallsBackToSafeAction()
    {
        // Bishop's Phase H Wave 1 contract: when a bot strategy's DecideAction exceeds
        // ChangshaRuntimeOptions.BotDecisionTimeoutMs the runtime substitutes a safe-default
        // action — Discard(HandEvaluator.SelectDiscardTile(hand)) on its own turn — and the
        // background strategy task is allowed to complete out-of-band (its result is discarded).
        AssertBotDecisionTimeoutOptionExists();

        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 100;
            o.BotTurnDelayMs = 5;
            o.BotClaimDelayMs = 5;
            o.ClaimWindowTimeoutMs = 2000;
        });
        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var gameId = await runtime.CreateGameAsync(seed: 13579,
            botSeatIndexes: new[] { 0, 1, 2, 3 }, hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        // Inject a strategy that blocks for 5x the timeout. The Discard-fallback would-return
        // value is irrelevant — Bishop's wrapper must discard whatever the slow task eventually
        // produces and substitute the safe default.
        var slow = new SlowBotStrategy(delayMs: 600);
        InjectStrategyOrFail(runtime, slow);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await runtime.StartGameAsync(gameId, cts.Token);

        // Auto-deal lands us in AwaitingDiscard with dealer (seat 0, a bot) active. The bot
        // turn loop will fire RunBotTurnAsync(seat 0) → strategy times out → safe-default
        // discard fires. Discard advances the phase (or opens a claim window).
        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1
               || Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingClaim,
            TimeSpan.FromMilliseconds(2_000),
            "bot dealer never discarded — timeout fallback never fired");
        sw.Stop();

        // The runtime call should return within ~timeout + generous buffer; the background
        // strategy's 600ms sleep must NOT block the runtime past timeout+200ms (the spec).
        Assert.True(sw.ElapsedMilliseconds < 1_500,
            $"Runtime did not return within BotDecisionTimeoutMs + buffer. " +
            $"Took {sw.ElapsedMilliseconds}ms; spec is timeout(100) + ~200ms buffer. " +
            $"Bishop's timeout wrapper must not await the slow strategy task.");

        Assert.True(slow.InvocationCount >= 1,
            "Slow strategy was never invoked — Bishop's runtime is not consulting the injected " +
            "IChangshaBotStrategy. Verify the injection seam is wired to the decision path.");
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task Bot_Timeout_Discard_PicksLowestRankSafe()
    {
        // The safe-default discard is deterministic. Bishop's spec names it
        // HandEvaluator.SelectDiscardTile(hand); for parity with the existing Medium
        // implementation that ChangshaBotPolicy already exposes, the result must equal
        // MediumStrategy.SelectDiscardTile(hand) computed on the pre-timeout hand snapshot.
        AssertBotDecisionTimeoutOptionExists();

        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 100;
            o.BotTurnDelayMs = 5;
            o.BotClaimDelayMs = 5;
            o.ClaimWindowTimeoutMs = 2000;
        });
        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var gameId = await runtime.CreateGameAsync(seed: 24680,
            botSeatIndexes: new[] { 0, 1, 2, 3 }, hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        var slow = new SlowBotStrategy(delayMs: 600);
        InjectStrategyOrFail(runtime, slow);

        await runtime.StartGameAsync(gameId, cts.Token);

        // Compute the expected safe-default discard from the dealer's hand BEFORE any
        // discard fires. The hand is deterministic by seed; the SelectDiscardTile output
        // is the canonical fallback.
        var dealerHandSnapshot = new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>(state.Hands[0].ConcealedTiles),
        };
        var expectedSafeDiscard = MediumStrategy.SelectDiscardTile(dealerHandSnapshot);

        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1,
            TimeSpan.FromMilliseconds(2_000),
            "bot dealer never discarded after slow-strategy timeout");

        var actualDiscard = Snapshot(runtime, gameId).DiscardPile[0].TileId;
        Assert.Equal(expectedSafeDiscard, actualDiscard);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W97")]
    public async Task Bot_Timeout_OnOwnTurn_UsesMediumFallback_ToTakeSelfDrawHu()
    {
        // Own-turn timeout fallback should preserve bot quality too: if strategy hangs,
        // runtime should apply deterministic Medium-policy own-turn evaluation and
        // convert obvious self-draw Hu opportunities instead of forcing a discard.
        AssertBotDecisionTimeoutOptionExists();

        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 100;
            o.BotTurnDelayMs = 5;
            o.BotClaimDelayMs = 5;
            o.ClaimWindowTimeoutMs = 2_000;
        });
        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var gameId = await runtime.CreateGameAsync(seed: 778899,
            botSeatIndexes: new[] { 1, 2, 3 }, hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () =>
            {
                var snap = Snapshot(runtime, gameId);
                return snap.Phase == ChangshaPhase.AwaitingDiscard && snap.ActiveSeatIndex == 0;
            },
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with dealer seat active");

        // Dealer starts human in this test to avoid implicit scheduling races; we
        // then flip to bot and explicitly trigger ScheduleBotIfNeeded after shaping
        // a known self-draw win hand.
        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(ChangshaTestHelpers.Tiles(
            (Suit.Wan, 1), (Suit.Wan, 2), (Suit.Wan, 3),
            (Suit.Wan, 4), (Suit.Wan, 5), (Suit.Wan, 6),
            (Suit.Tong, 1), (Suit.Tong, 2), (Suit.Tong, 3),
            (Suit.Tiao, 4), (Suit.Tiao, 5), (Suit.Tiao, 6),
            (Suit.Tong, 5), (Suit.Tong, 5)));
        live.Hands[0].Melds.Clear();
        live.LastDrawSeatIndex = 0;
        live.Seats[0].IsBot = true;
        Assert.True(ChangshaGameStateMachine.CanDeclareSelfDrawWin(live, 0));

        var slow = new SlowBotStrategy(delayMs: 600, decision: (_, _) => BotAction.Pass());
        InjectStrategyOrFail(runtime, slow);

        var baselineEventCount = live.EventLog.Count;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await ScheduleBotIfNeededOrFail(runtime, gameId, cts.Token);
        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.EventLog
                .Skip(baselineEventCount)
                .Any(e => e.EventType == "win-declared" && e.SeatIndex == 0),
            TimeSpan.FromMilliseconds(4_000),
            "own-turn timeout medium fallback did not declare self-draw Hu",
            cts.Token);
        sw.Stop();

        var final = await runtime.TryGetSnapshotCopyAsync(gameId, cts.Token);
        Assert.NotNull(final);
        Assert.Contains(final.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 0);
        Assert.DoesNotContain(final.EventLog, e => e.EventType == "discard" && e.SeatIndex == 0);
        Assert.True(sw.ElapsedMilliseconds < 2_500,
            $"Own-turn timeout fallback took too long to convert immediate self-draw Hu. Took {sw.ElapsedMilliseconds}ms.");
        Assert.True(slow.InvocationCount >= 1,
            "Slow strategy was never invoked; timeout fallback path was not exercised.");
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W96")]
    public async Task Bot_Timeout_DuringClaim_UsesMediumFallback_ToTakeHuOpportunity()
    {
        // Claim-window timeout fallback should preserve bot quality: if the strategy hangs,
        // runtime should still apply deterministic Medium-policy claim evaluation (not forced
        // Pass), so obvious Hu opportunities are converted instead of being dropped.
        AssertBotDecisionTimeoutOptionExists();

        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 100;
            o.BotTurnDelayMs = 5;
            o.BotClaimDelayMs = 5;
            // Keep claim-window timeout far above decision timeout so we verify decision-timeout
            // fallback behavior rather than window-timeout auto-pass resolution.
            o.ClaimWindowTimeoutMs = 20_000;
        });
        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var gameId = await runtime.CreateGameAsync(seed: 11,
            botSeatIndexes: new[] { 1, 2, 3 }, hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard");

        // Prepare a Hu-only claim window for bot seat 1 on a Wan-1 discard.
        var live = Snapshot(runtime, gameId);
        var wan1Logical = ChangshaTestHelpers.Logical(Suit.Wan, 1);
        var wan1Copy0 = ChangshaTestHelpers.Tid(Suit.Wan, 1, 0);

        live.Hands[1].ConcealedTiles.Clear();
        live.Hands[1].ConcealedTiles.AddRange(Acceptance.AcceptanceFixture.ThirteenTileWaitingForWan1());
        live.Hands[1].Melds.Clear();

        foreach (var h in live.Hands.Where(h => h.SeatIndex is 2 or 3))
            h.ConcealedTiles.RemoveAll(t => ChangshaDeckBuilder.GetLogicalTile(t) == wan1Logical);

        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Tong, 9, 0), ChangshaTestHelpers.Tid(Suit.Tong, 9, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 8, 0), ChangshaTestHelpers.Tid(Suit.Tong, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 6, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0), ChangshaTestHelpers.Tid(Suit.Wan, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 7, 0),
            wan1Copy0,
        });

        // Deliberately return Pass after timeout-sized sleep. If runtime uses the slow strategy
        // result directly, Hu is missed; medium fallback should still claim Hu.
        var slow = new SlowBotStrategy(delayMs: 600, decision: (_, _) => BotAction.Pass());
        InjectStrategyOrFail(runtime, slow);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: wan1Copy0, cts.Token);

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.ClaimWindow is null
                    && snap.EventLog.Any(e => e.EventType == "win-declared" && e.SeatIndex == 1)
                    && snap.EventLog.Any(e => e.EventType == "scoring-complete" && e.SeatIndex == 1),
            TimeSpan.FromMilliseconds(12_000),
            "claim-timeout medium fallback did not convert available Hu opportunity",
            cts.Token);
        sw.Stop();

        var final = await runtime.TryGetSnapshotCopyAsync(gameId, cts.Token);
        Assert.NotNull(final);
        Assert.Contains(final!.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 1);
        Assert.Contains(final.EventLog, e => e.EventType == "scoring-complete" && e.SeatIndex == 1);

        var strategyState = await slow.FirstState.Task.WaitAsync(cts.Token);
        Assert.NotSame(live, strategyState);
        Assert.NotSame(live.EventLog, strategyState.EventLog);
        Assert.NotSame(live.Hands[1].ConcealedTiles, strategyState.Hands[1].ConcealedTiles);
        Assert.True(sw.ElapsedMilliseconds < 13_000,
            $"Claim-timeout medium fallback did not fire within BotDecisionTimeoutMs + buffer. " +
            $"Took {sw.ElapsedMilliseconds}ms; decision-timeout fallback (100ms) must resolve long " +
            $"before the 20s ClaimWindowTimeoutMs path.");
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W99")]
    public async Task Bot_UnsupportedClaimWindowAction_UsesMediumFallback_ToTakeHuOpportunity()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 5_000;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 20_000;
        });
        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var gameId = await runtime.CreateGameAsync(seed: 9050,
            botSeatIndexes: new[] { 1 }, hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard");

        var live = Snapshot(runtime, gameId);
        var wan1Logical = ChangshaTestHelpers.Logical(Suit.Wan, 1);
        var wan1Copy0 = ChangshaTestHelpers.Tid(Suit.Wan, 1, 0);

        live.Hands[1].ConcealedTiles.Clear();
        live.Hands[1].ConcealedTiles.AddRange(Acceptance.AcceptanceFixture.ThirteenTileWaitingForWan1());
        live.Hands[1].Melds.Clear();

        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Tong, 9, 0), ChangshaTestHelpers.Tid(Suit.Tong, 9, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 8, 0), ChangshaTestHelpers.Tid(Suit.Tong, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 6, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0), ChangshaTestHelpers.Tid(Suit.Wan, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 7, 0),
            wan1Copy0,
        });

        foreach (var h in live.Hands.Where(h => h.SeatIndex != 1))
            h.ConcealedTiles.RemoveAll(t => ChangshaDeckBuilder.GetLogicalTile(t) == wan1Logical && t != wan1Copy0);

        InjectStrategyOrFail(runtime, new SlowBotStrategy(delayMs: 0, decision: (s, _) =>
            s.Phase == ChangshaPhase.AwaitingClaim
                ? BotAction.Discard(ChangshaTestHelpers.Tid(Suit.Tong, 9, 0))
                : BotAction.Pass()));

        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: wan1Copy0, cts.Token);

        await WaitForSnapshotAsync(
            runtime,
            gameId,
            snap => snap.ClaimWindow is null
                    && snap.EventLog.Any(e => e.EventType == "win-declared" && e.SeatIndex == 1)
                    && snap.EventLog.Any(e => e.EventType == "scoring-complete" && e.SeatIndex == 1),
            TimeSpan.FromSeconds(10),
            "unsupported claim-window action did not degrade to medium claim fallback",
            cts.Token);

        var final = await runtime.TryGetSnapshotCopyAsync(gameId, cts.Token);
        Assert.NotNull(final);
        Assert.Contains(final!.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 1);
        Assert.Contains(final.EventLog, e => e.EventType == "scoring-complete" && e.SeatIndex == 1);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W97")]
    public async Task Bot_HuOverflow_FallsBackToBestNonHuClaim_InsteadOfPassing()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 5_000;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

        var gameId = await runtime.CreateGameAsync(
            seed: 9051,
            botSeatIndexes: new[] { 1 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        var discardWan1 = ChangshaTestHelpers.Tid(Suit.Wan, 1, 0);

        // Seat 0: legal 14-tile hand containing the discard tile.
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            discardWan1,
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0), ChangshaTestHelpers.Tid(Suit.Wan, 8, 0), ChangshaTestHelpers.Tid(Suit.Wan, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 9, 0), ChangshaTestHelpers.Tid(Suit.Tong, 8, 0), ChangshaTestHelpers.Tid(Suit.Tong, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 0), ChangshaTestHelpers.Tid(Suit.Wan, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 6, 0)
        });
        live.Hands[0].Melds.Clear();

        // Seat 1 (bot): discard Wan-1 gives both Hu and Pung opportunities.
        live.Hands[1].ConcealedTiles.Clear();
        live.Hands[1].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 1), ChangshaTestHelpers.Tid(Suit.Wan, 1, 2),
            ChangshaTestHelpers.Tid(Suit.Wan, 2, 1), ChangshaTestHelpers.Tid(Suit.Wan, 3, 1), ChangshaTestHelpers.Tid(Suit.Wan, 4, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 1), ChangshaTestHelpers.Tid(Suit.Wan, 6, 1), ChangshaTestHelpers.Tid(Suit.Wan, 7, 1),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 1), ChangshaTestHelpers.Tid(Suit.Tong, 3, 1), ChangshaTestHelpers.Tid(Suit.Tong, 4, 1),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 9, 2)
        });
        live.Hands[1].Melds.Clear();

        live.Hands[2].ConcealedTiles.Clear();
        live.Hands[2].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 2, 2), ChangshaTestHelpers.Tid(Suit.Wan, 3, 2), ChangshaTestHelpers.Tid(Suit.Wan, 4, 2),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 2), ChangshaTestHelpers.Tid(Suit.Wan, 6, 2), ChangshaTestHelpers.Tid(Suit.Wan, 7, 2),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 2), ChangshaTestHelpers.Tid(Suit.Tong, 3, 2), ChangshaTestHelpers.Tid(Suit.Tong, 4, 2),
            ChangshaTestHelpers.Tid(Suit.Tiao, 2, 2), ChangshaTestHelpers.Tid(Suit.Tiao, 3, 2), ChangshaTestHelpers.Tid(Suit.Tiao, 4, 2),
            ChangshaTestHelpers.Tid(Suit.Tiao, 5, 2)
        });
        live.Hands[2].Melds.Clear();

        live.Hands[3].ConcealedTiles.Clear();
        live.Hands[3].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 2, 3), ChangshaTestHelpers.Tid(Suit.Wan, 3, 3), ChangshaTestHelpers.Tid(Suit.Wan, 4, 3),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 3), ChangshaTestHelpers.Tid(Suit.Wan, 6, 3), ChangshaTestHelpers.Tid(Suit.Wan, 7, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 3), ChangshaTestHelpers.Tid(Suit.Tong, 3, 3), ChangshaTestHelpers.Tid(Suit.Tong, 4, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 2, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 3, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 4, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 5, 3)
        });
        live.Hands[3].Melds.Clear();

        // Force Hu preflight overflow so runtime must downgrade to a legal non-Hu claim.
        live.CumulativeScores[0] = 0;
        live.CumulativeScores[1] = int.MaxValue;
        live.CumulativeScores[2] = 0;
        live.CumulativeScores[3] = 0;

        live.ClaimWindow = null;
        live.MissedWinSeats.Clear();
        live.ActiveSeatIndex = 0;
        live.Phase = ChangshaPhase.AwaitingDiscard;

        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: discardWan1, cts.Token);

        await WaitForAsync(
            () =>
            {
                var snap = Snapshot(runtime, gameId);
                return snap.ClaimWindow is null
                    && snap.Hands[1].Melds.Any(m => m.Kind == MeldKind.Pung && m.TileIds.Contains(discardWan1));
            },
            TimeSpan.FromSeconds(3),
            "overflowed hu did not downgrade to a legal non-hu claim");

        var resolved = Snapshot(runtime, gameId);
        var pung = Assert.Single(resolved.Hands[1].Melds);
        Assert.Equal(MeldKind.Pung, pung.Kind);
        Assert.Contains(discardWan1, pung.TileIds);
        Assert.Null(resolved.CurrentWin);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 1);

        var lastDecision = GetLastBotDecisionOrFail(runtime, gameId, seatIndex: 1);
        Assert.Equal(BotActionType.Claim, lastDecision.Action.Type);
        Assert.Equal(TableClaimType.Pung, lastDecision.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha"), Trait("Wave", "W103")]
    public async Task Bot_HuOverflow_PrefersMediumNonHuChowShape_OverRankOnlyPungFallback()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 2_000;
            o.BotTurnDelayMs = 5_000;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 2_000;
        });

        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));

        var gameId = await runtime.CreateGameAsync(
            seed: 9053,
            botSeatIndexes: new[] { 1 },
            hostPlayerId: null,
            hostConnectionId: null,
            cts.Token);

        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var seat in state.Seats) seat.IsDealer = seat.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with seat 0 active");

        var live = Snapshot(runtime, gameId);
        var discardTong8 = ChangshaTestHelpers.Tid(Suit.Tong, 8, 1);
        var chowTong6 = ChangshaTestHelpers.Tid(Suit.Tong, 6, 3);
        var chowTong7 = ChangshaTestHelpers.Tid(Suit.Tong, 7, 2);

        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            discardTong8,
            ChangshaTestHelpers.Tid(Suit.Wan, 9, 0), ChangshaTestHelpers.Tid(Suit.Wan, 8, 0), ChangshaTestHelpers.Tid(Suit.Wan, 7, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 6, 0), ChangshaTestHelpers.Tid(Suit.Wan, 2, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 9, 0), ChangshaTestHelpers.Tid(Suit.Tong, 7, 0), ChangshaTestHelpers.Tid(Suit.Tong, 6, 0), ChangshaTestHelpers.Tid(Suit.Tong, 5, 1),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 6, 0)
        });
        live.Hands[0].Melds.Clear();

        // Seat 1: discard Tong-8 yields Hu + Pung + Chow, but overflowed Hu should
        // fall back to the Medium non-Hu shape (Chow 6-7-8), not rank-only Pung.
        live.Hands[1].ConcealedTiles.Clear();
        live.Hands[1].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 4, 2), ChangshaTestHelpers.Tid(Suit.Wan, 4, 3),
            ChangshaTestHelpers.Tid(Suit.Tong, 3, 0), ChangshaTestHelpers.Tid(Suit.Tong, 4, 2), ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            chowTong6, chowTong7,
            ChangshaTestHelpers.Tid(Suit.Tong, 8, 2), ChangshaTestHelpers.Tid(Suit.Tong, 8, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 3, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 4, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 5, 0)
        });
        live.Hands[1].Melds.Clear();

        // Keep other seats unable to claim the discard.
        live.Hands[2].ConcealedTiles.Clear();
        live.Hands[2].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0), ChangshaTestHelpers.Tid(Suit.Wan, 1, 1), ChangshaTestHelpers.Tid(Suit.Wan, 2, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 2, 2), ChangshaTestHelpers.Tid(Suit.Wan, 3, 0), ChangshaTestHelpers.Tid(Suit.Wan, 3, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 1), ChangshaTestHelpers.Tid(Suit.Wan, 5, 2),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 0), ChangshaTestHelpers.Tid(Suit.Tong, 1, 1),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 1, 1), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 1)
        });
        live.Hands[2].Melds.Clear();

        live.Hands[3].ConcealedTiles.Clear();
        live.Hands[3].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 2), ChangshaTestHelpers.Tid(Suit.Wan, 1, 3), ChangshaTestHelpers.Tid(Suit.Wan, 2, 3),
            ChangshaTestHelpers.Tid(Suit.Wan, 3, 2), ChangshaTestHelpers.Tid(Suit.Wan, 3, 3), ChangshaTestHelpers.Tid(Suit.Wan, 5, 3),
            ChangshaTestHelpers.Tid(Suit.Wan, 6, 1), ChangshaTestHelpers.Tid(Suit.Wan, 6, 2),
            ChangshaTestHelpers.Tid(Suit.Tong, 1, 2), ChangshaTestHelpers.Tid(Suit.Tong, 1, 3),
            ChangshaTestHelpers.Tid(Suit.Tiao, 1, 2), ChangshaTestHelpers.Tid(Suit.Tiao, 1, 3), ChangshaTestHelpers.Tid(Suit.Tiao, 2, 2)
        });
        live.Hands[3].Melds.Clear();

        // Force Hu preflight overflow so runtime must downgrade to non-Hu claim quality path.
        live.CumulativeScores[0] = 0;
        live.CumulativeScores[1] = int.MaxValue;
        live.CumulativeScores[2] = 0;
        live.CumulativeScores[3] = 0;

        live.ClaimWindow = null;
        live.MissedWinSeats.Clear();
        live.ActiveSeatIndex = 0;
        live.Phase = ChangshaPhase.AwaitingDiscard;

        await runtime.DiscardAsync(gameId, seatIndex: 0, tileId: discardTong8, cts.Token);

        await WaitForAsync(
            () =>
            {
                var snap = Snapshot(runtime, gameId);
                return snap.ClaimWindow is null
                    && snap.Hands[1].Melds.Any(m => m.TileIds.Contains(discardTong8));
            },
            TimeSpan.FromSeconds(3),
            "overflowed hu did not resolve to a non-hu claim");

        var resolved = Snapshot(runtime, gameId);
        var meld = Assert.Single(resolved.Hands[1].Melds);
        Assert.Equal(MeldKind.Chow, meld.Kind);
        Assert.Contains(discardTong8, meld.TileIds);
        Assert.Contains(chowTong6, meld.TileIds);
        Assert.Contains(chowTong7, meld.TileIds);
        Assert.Null(resolved.CurrentWin);
        Assert.DoesNotContain(resolved.EventLog, e => e.EventType == "win-declared" && e.SeatIndex == 1);

        var lastDecision = GetLastBotDecisionOrFail(runtime, gameId, seatIndex: 1);
        Assert.Equal(BotActionType.Claim, lastDecision.Action.Type);
        Assert.Equal(TableClaimType.Chow, lastDecision.Action.ClaimType);
        Assert.NotEqual(TableClaimType.Hu, lastDecision.Action.ClaimType);
    }

    [Fact, Trait("Category", "Changsha")]
    public async Task Bot_Decision_Within_Timeout_ProceedsNormally()
    {
        // Control test: with BotDecisionTimeoutMs comfortably above the strategy's runtime,
        // the strategy's NATURAL action is what lands — not the safe-default. We pin a
        // deterministic dealer hand and have the fast strategy return a specific tile that
        // is provably NOT the MediumStrategy.SelectDiscardTile safe-default for that hand.
        AssertBotDecisionTimeoutOptionExists();

        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 5_000;
            // Give the test a 300ms window after observing AwaitingDiscard to override
            // the dealer's hand and swap in the scripted strategy before the bot turn fires.
            o.BotTurnDelayMs = 300;
            o.BotClaimDelayMs = 5;
            o.ClaimWindowTimeoutMs = 2000;
        });
        var runtime = harness.Runtime;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var gameId = await runtime.CreateGameAsync(seed: 99,
            botSeatIndexes: new[] { 0, 1, 2, 3 }, hostPlayerId: null, hostConnectionId: null, cts.Token);
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        state!.DealerSeatIndex = 0;
        foreach (var s in state.Seats) s.IsDealer = s.SeatIndex == 0;
        state.DealMode = DealMode.Auto;

        await runtime.StartGameAsync(gameId, cts.Token);
        await WaitForAsync(
            () => Snapshot(runtime, gameId).Phase == ChangshaPhase.AwaitingDiscard
               && Snapshot(runtime, gameId).ActiveSeatIndex == 0,
            TimeSpan.FromSeconds(2),
            "auto-deal never reached AwaitingDiscard with bot dealer active");

        // Pin a deterministic dealer hand. Pick a 14-tile hand whose safe-default discard
        // (lowest keep-score) is provably different from our scripted "preferred" tile.
        var live = Snapshot(runtime, gameId);
        live.Hands[0].ConcealedTiles.Clear();
        live.Hands[0].ConcealedTiles.AddRange(new[]
        {
            ChangshaTestHelpers.Tid(Suit.Wan, 5, 0), ChangshaTestHelpers.Tid(Suit.Wan, 5, 1),
            ChangshaTestHelpers.Tid(Suit.Wan, 4, 0), ChangshaTestHelpers.Tid(Suit.Wan, 6, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 2, 0), ChangshaTestHelpers.Tid(Suit.Tong, 3, 0),
            ChangshaTestHelpers.Tid(Suit.Tong, 4, 0), ChangshaTestHelpers.Tid(Suit.Tong, 5, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 7, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 8, 0),
            ChangshaTestHelpers.Tid(Suit.Tiao, 9, 0), ChangshaTestHelpers.Tid(Suit.Tiao, 1, 0),
            ChangshaTestHelpers.Tid(Suit.Wan, 1, 0), ChangshaTestHelpers.Tid(Suit.Tong, 9, 0),
        });
        live.Hands[0].Melds.Clear();

        var safeDefault = MediumStrategy.SelectDiscardTile(new ChangshaHandState
        {
            SeatIndex = 0,
            ConcealedTiles = new List<int>(live.Hands[0].ConcealedTiles),
        });
        // Pick a "scripted" tile that is provably NOT the safe-default: the highest-id
        // tile in the hand (different ordering rule than SelectDiscardTile).
        var scripted = live.Hands[0].ConcealedTiles.OrderByDescending(t => t).First(t => t != safeDefault);

        var fast = new SlowBotStrategy(delayMs: 0,
            decision: (_, _) => BotAction.Discard(scripted));
        InjectStrategyOrFail(runtime, fast);

        // Re-trigger seat 0's turn loop so the injected fast strategy fires for the dealer.
        // The runtime is already in AwaitingDiscard from StartGame; an explicit no-op
        // mutation isn't available, so we rely on the in-flight RunBotTurnAsync to have
        // captured the injected strategy via field read on every DecideAction call.
        await WaitForAsync(
            () => Snapshot(runtime, gameId).DiscardPile.Count >= 1
               || Snapshot(runtime, gameId).Phase == ChangshaPhase.EndHand,
            TimeSpan.FromSeconds(3),
            "fast strategy never produced a discard within control-test window");

        Assert.True(fast.InvocationCount >= 1,
            "Fast strategy was not invoked — runtime is not consulting the injected IChangshaBotStrategy.");
        // First discard MUST be the scripted tile (not the safe-default). This proves the
        // timeout wrapper did NOT fire spuriously on a fast strategy.
        var firstDiscard = Snapshot(runtime, gameId).DiscardPile[0].TileId;
        Assert.NotEqual(safeDefault, firstDiscard);
        Assert.Equal(scripted, firstDiscard);
    }

    // ── Phase H Wave 1 — slow-strategy harness & reflection helpers ─────────────

    [Fact, Trait("Category", "Changsha")]
    public async Task TimedOutTurnStrategy_RetainsAnIsolatedState_WhileTheRuntimeAdvances()
    {
        await using var harness = new RuntimeHarness(o =>
        {
            o.BotDecisionTimeoutMs = 100;
            o.BotTurnDelayMs = 1;
            o.BotClaimDelayMs = 1;
            o.ClaimWindowTimeoutMs = 100;
        });
        var runtime = harness.Runtime;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource<ChangshaGameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new SlowBotStrategy(0, (state, _) =>
        {
            started.TrySetResult(state);
            release.Wait(timeout.Token);
            completed.TrySetResult();
            return BotAction.Pass();
        });
        InjectStrategyOrFail(runtime, slow);
        var gameId = await runtime.CreateGameAsync(seed: 11, botSeatIndexes: new[] { 0 },
            hostPlayerId: null, hostConnectionId: null, timeout.Token);
        var live = Snapshot(runtime, gameId);
        live.DealerSeatIndex = 0;
        foreach (var seat in live.Seats) seat.IsDealer = seat.SeatIndex == 0;
        live.DealMode = DealMode.Auto;

        try
        {
            await runtime.StartGameAsync(gameId, timeout.Token);
            var captured = await started.Task.WaitAsync(timeout.Token);
            var capturedEvents = captured.EventLog.Count;
            var capturedTiles = captured.Hands[0].ConcealedTiles.ToArray();
            await WaitForAsync(() => Snapshot(runtime, gameId).DiscardPile.Count > 0,
                TimeSpan.FromSeconds(5), "timed-out turn did not apply its discard fallback");

            Assert.NotSame(live, captured);
            Assert.Equal(capturedEvents, captured.EventLog.Count);
            Assert.Equal(capturedTiles, captured.Hands[0].ConcealedTiles);
            Assert.Equal(14, captured.Hands[0].ConcealedTiles.Count);
            Assert.Empty(captured.DiscardPile);
            var after = await runtime.TryGetSnapshotCopyAsync(gameId, timeout.Token);
            Assert.NotNull(after);
            Assert.Single(after.DiscardPile);
            Assert.Equal(13, after.Hands[0].ConcealedTiles.Count);
        }
        finally
        {
            release.Set();
            await completed.Task.WaitAsync(timeout.Token);
        }
    }

    /// <summary>Test-double IChangshaBotStrategy that blocks <see cref="DecideAction"/>
    /// for <paramref name="delayMs"/> via <see cref="Thread.Sleep"/> (deliberate: a hung
    /// strategy is what Bishop's timeout wrapper must defeat). The result the strategy
    /// would have returned after the sleep is supplied by <paramref name="decision"/>;
    /// when Bishop's wrapper substitutes the safe-default, the slow task's result is
    /// expected to be discarded entirely.</summary>
    private sealed class SlowBotStrategy : IChangshaBotStrategy
    {
        private readonly int _delayMs;
        private readonly Func<ChangshaGameState, int, BotAction> _decision;
        public int InvocationCount;
        public TaskCompletionSource<ChangshaGameState> FirstState { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Difficulty => "slow-test";

        public SlowBotStrategy(int delayMs, Func<ChangshaGameState, int, BotAction>? decision = null)
        {
            _delayMs = delayMs;
            _decision = decision ?? ((_, _) => BotAction.Pass());
        }

        public BotAction OnTurnStart(ChangshaGameState state, int seat) => DecideAction(state, seat);
        public BotAction OnOtherDiscard(ChangshaGameState state, int seat, int discarder, int tile) => DecideAction(state, seat);
        public BotAction OnSelfDraw(ChangshaGameState state, int seat) => DecideAction(state, seat);
        public BotAction OnPickupCue(ChangshaGameState state, int seat) => BotAction.Wait();

        public BotAction DecideAction(ChangshaGameState state, int seat)
        {
            Interlocked.Increment(ref InvocationCount);
            FirstState.TrySetResult(state);
            if (_delayMs > 0) Thread.Sleep(_delayMs);
            return _decision(state, seat);
        }
    }

    /// <summary>Per-test factory harness — mirrors the shape used in
    /// <c>BotPickupSchedulerAcceptanceTests</c> so options can be tuned per test
    /// (BotDecisionTimeoutMs especially).</summary>
    private sealed class RuntimeHarness : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Factory { get; }
        public IChangshaGameRuntime Runtime { get; }
        private readonly string _tempDb;

        public RuntimeHarness(Action<ChangshaRuntimeOptions> configureOptions)
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "test-data");
            Directory.CreateDirectory(dataDir);
            _tempDb = Path.Combine(dataDir, $"phase-h-bot-timeout-{Guid.NewGuid():N}.db");
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_tempDb}");
                b.ConfigureServices(s =>
                {
                    s.Configure<ChangshaRuntimeOptions>(o =>
                    {
                        o.BotTurnDelayMs = 1;
                        o.BotClaimDelayMs = 1;
                        o.BotPickupDelayMs = 50;
                        o.ClaimWindowTimeoutMs = 200;
                        o.DealBatchDelayMs = 0;
                        o.PersistSnapshots = false;
                        configureOptions(o);
                    });
                });
            });
            _ = Factory.Server;
            Runtime = Factory.Services.GetRequiredService<IChangshaGameRuntime>();
        }

        public ValueTask DisposeAsync()
        {
            Factory.Dispose();
            try { if (File.Exists(_tempDb)) File.Delete(_tempDb); } catch { }
            return ValueTask.CompletedTask;
        }
    }

    private static ChangshaGameState Snapshot(IChangshaGameRuntime runtime, string gameId)
    {
        Assert.True(runtime.TryGetSnapshot(gameId, out var state));
        return state!;
    }

    private static async Task<ChangshaGameState> SnapshotCopyAsync(
        IChangshaGameRuntime runtime,
        string gameId,
        CancellationToken ct = default)
    {
        var snapshot = await runtime.TryGetSnapshotCopyAsync(gameId, ct);
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static async Task WaitForSnapshotAsync(
        IChangshaGameRuntime runtime,
        string gameId,
        Func<ChangshaGameState, bool> predicate,
        TimeSpan timeout,
        string description,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var snapshot = await runtime.TryGetSnapshotCopyAsync(gameId, ct);
            if (snapshot is not null && predicate(snapshot)) return;
            await Task.Delay(15, ct);
        }

        throw new TimeoutException(
            $"Bot decision-timeout contract violated: {description}. " +
            $"Bishop owes ChangshaRuntimeOptions.BotDecisionTimeoutMs + safe-default fallback wiring " +
            $"in BotClaimAsync / RunBotTurnAsync — see Phase H Wave 1 spec.");
    }

    private static async Task WaitForAsync(Func<bool> predicate, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(15);
        }
        throw new TimeoutException(
            $"Bot decision-timeout contract violated: {description}. " +
            $"Bishop owes ChangshaRuntimeOptions.BotDecisionTimeoutMs + safe-default fallback wiring " +
            $"in BotClaimAsync / RunBotTurnAsync — see Phase H Wave 1 spec.");
    }

    private static BotDecision GetLastBotDecisionOrFail(
        IChangshaGameRuntime runtime,
        string gameId,
        int seatIndex)
    {
        var runtimeType = runtime.GetType();
        var gamesField = runtimeType.GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(gamesField);

        var games = gamesField!.GetValue(runtime);
        Assert.NotNull(games);

        var tryGetGame = games!.GetType().GetMethod("TryGetValue", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(tryGetGame);

        var gameArgs = new object?[] { gameId, null };
        var foundGame = (bool)(tryGetGame!.Invoke(games, gameArgs) ?? false);
        Assert.True(foundGame, $"Failed to locate runtime game instance for gameId={gameId}.");
        Assert.NotNull(gameArgs[1]);

        var instance = gameArgs[1]!;
        var lastBotDecisionsProperty = instance.GetType().GetProperty("LastBotDecisions", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(lastBotDecisionsProperty);

        var lastBotDecisions = lastBotDecisionsProperty!.GetValue(instance);
        Assert.NotNull(lastBotDecisions);

        var tryGetDecision = lastBotDecisions!.GetType().GetMethod("TryGetValue", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(tryGetDecision);

        var decisionArgs = new object?[] { seatIndex, null };
        var foundDecision = (bool)(tryGetDecision!.Invoke(lastBotDecisions, decisionArgs) ?? false);
        Assert.True(foundDecision, $"No LastBotDecision recorded for seat {seatIndex} in game {gameId}.");

        var boxed = decisionArgs[1];
        Assert.NotNull(boxed);
        Assert.IsType<BotDecision>(boxed);
        return (BotDecision)boxed!;
    }

    private static void AssertBotDecisionTimeoutOptionExists()
    {
        var prop = typeof(ChangshaRuntimeOptions).GetProperty("BotDecisionTimeoutMs",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.True(prop is not null,
            "ChangshaRuntimeOptions.BotDecisionTimeoutMs missing. Bishop owns Phase H Wave 1 — add the new " +
            "option (int, default 2000ms) and wrap _botPolicy.DecideAction with a Task.WhenAny race.");
        Assert.Equal(typeof(int), prop!.PropertyType);
    }

    private static async Task ScheduleBotIfNeededOrFail(IChangshaGameRuntime runtime, string gameId, CancellationToken ct)
    {
        var runtimeType = runtime.GetType();
        var gamesField = runtimeType.GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(gamesField);

        var games = gamesField!.GetValue(runtime);
        Assert.NotNull(games);

        var tryGetValue = games!.GetType().GetMethod("TryGetValue", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(tryGetValue);

        var args = new object?[] { gameId, null };
        var found = (bool)(tryGetValue!.Invoke(games, args) ?? false);
        Assert.True(found, $"Failed to locate runtime game instance for gameId={gameId}.");
        Assert.NotNull(args[1]);

        var schedule = runtimeType.GetMethod("ScheduleBotIfNeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(schedule);

        var task = schedule!.Invoke(runtime, new[] { args[1], (object)ct }) as Task;
        Assert.NotNull(task);
        await task!;
    }

    /// <summary>Reflectively swap the runtime's bot-decision strategy. Bishop's Phase H
    /// refactor MUST expose an injection seam so tests can supply a slow strategy. We
    /// probe (in order) for: any private IChangshaBotStrategy field on the runtime, a
    /// retyped <c>_botPolicy</c> assignable from the interface, or a static override
    /// hook on <see cref="ChangshaBotEngine"/>. Fails red with a descriptive message
    /// (naming the missing seam) until Bishop ships.</summary>
    private static void InjectStrategyOrFail(IChangshaGameRuntime runtime, IChangshaBotStrategy strategy)
    {
        var runtimeType = runtime.GetType();
        var fields = runtimeType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        // 1) Any field whose type is assignable to IChangshaBotStrategy (Bishop refactored
        //    the legacy _botPolicy : ChangshaBotPolicy to _strategy : IChangshaBotStrategy).
        var ifaceField = fields.FirstOrDefault(f => typeof(IChangshaBotStrategy).IsAssignableFrom(f.FieldType));
        if (ifaceField is not null)
        {
            ifaceField.SetValue(runtime, strategy);
            return;
        }

        // 2) A static override on ChangshaBotEngine ("TestOverride" / "Override").
        foreach (var memberName in new[] { "TestOverride", "Override", "StrategyOverride" })
        {
            var staticField = typeof(ChangshaBotEngine).GetField(memberName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (staticField is not null && typeof(IChangshaBotStrategy).IsAssignableFrom(staticField.FieldType))
            {
                staticField.SetValue(null, strategy);
                return;
            }
            var staticProp = typeof(ChangshaBotEngine).GetProperty(memberName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (staticProp is not null && typeof(IChangshaBotStrategy).IsAssignableFrom(staticProp.PropertyType))
            {
                staticProp.SetValue(null, strategy);
                return;
            }
        }

        Assert.Fail(
            "ChangshaGameRuntime exposes no IChangshaBotStrategy injection seam. " +
            "Bishop's Phase H Wave 1 timeout-fallback contract is untestable without one. " +
            "Either retype the existing _botPolicy field to IChangshaBotStrategy (or rename to _strategy) " +
            "or add a ChangshaBotEngine.TestOverride static hook. Probe scanned: " +
            string.Join(", ", fields.Select(f => $"{f.Name}:{f.FieldType.Name}")));
    }
}
