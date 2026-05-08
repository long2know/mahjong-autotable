using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-I: State Machine Tests
/// Tests for deterministic state transitions, event logging, and integrity.
/// </summary>
public class StateMachineTests
{
    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_CreateInitialState_HasValidIntegrity()
    {
        // Verify initial state has proper integrity hash and version
        
        // Arrange & Act: Create initial state
        // var stateMachine = new ChangshaGameStateMachine(seed: 999);
        // var state = stateMachine.CreateInitialState();
        
        // Assert: State has integrity hash
        // Assert.False(string.IsNullOrWhiteSpace(state.Integrity.StateHash));
        
        // Assert: State version is 1
        // Assert.Equal(1, state.StateVersion);
        
        // Assert: Metadata contains seed
        // Assert.Equal(999, state.Metadata.Seed);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_ApplyAction_IncreasesStateVersion()
    {
        // Each action increments state version for optimistic concurrency
        
        // Arrange: Initial state at version 1
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = stateMachine.CreateInitialState();
        // Assert.Equal(1, state.StateVersion);
        
        // Act: Apply discard action
        // var newState = stateMachine.ApplyDiscard(state, seatIndex: 0, tileIndex: 0);
        
        // Assert: Version incremented
        // Assert.Equal(2, newState.StateVersion);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_WithSameSeed_ProducesDeterministicSequence()
    {
        // Same seed + same actions = identical state hashes
        
        // Arrange: Two machines with same seed
        // var machine1 = new ChangshaGameStateMachine(seed: 111);
        // var machine2 = new ChangshaGameStateMachine(seed: 111);
        
        // Act: Create initial states
        // var state1 = machine1.CreateInitialState();
        // var state2 = machine2.CreateInitialState();
        
        // Assert: Identical state hashes
        // Assert.Equal(state1.Integrity.StateHash, state2.Integrity.StateHash);
        
        // Act: Apply same discard action
        // var next1 = machine1.ApplyDiscard(state1, seatIndex: 0, tileIndex: 5);
        // var next2 = machine2.ApplyDiscard(state2, seatIndex: 0, tileIndex: 5);
        
        // Assert: Still identical
        // Assert.Equal(next1.Integrity.StateHash, next2.Integrity.StateHash);
    }

    [Fact(Skip = "Awaiting Bishop's TableSessionEventStore")]
    [Trait("Category", "Changsha")]
    public void EventLog_AppendOnly_RecordsAllActions()
    {
        // Event log must capture all actions for replay
        
        // Arrange: State machine with event logging
        // var stateMachine = new ChangshaGameStateMachine();
        // var eventStore = new TableSessionEventStore();
        // var state = stateMachine.CreateInitialState();
        
        // Act: Apply sequence of actions
        // var state2 = stateMachine.ApplyDiscard(state, seatIndex: 0, tileIndex: 0);
        // eventStore.AppendEvent(new DiscardEvent(seatIndex: 0, tileIndex: 0, turnNumber: 1));
        
        // var state3 = stateMachine.ApplyDraw(state2, seatIndex: 1);
        // eventStore.AppendEvent(new DrawEvent(seatIndex: 1, turnNumber: 2));
        
        // Assert: Event log has 2 events
        // var events = eventStore.GetAllEvents();
        // Assert.Equal(2, events.Count);
        // Assert.IsType<DiscardEvent>(events[0]);
        // Assert.IsType<DrawEvent>(events[1]);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_InvalidAction_ThrowsRuleException()
    {
        // Illegal actions must throw TableRuleException
        
        // Arrange: Player with 13 tiles tries to discard (should have 14)
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(activeSeat: 2, seatHandCounts: new[] { 13, 13, 13, 13 });
        
        // Act & Assert: Discard with 13 tiles throws
        // var ex = Assert.Throws<TableRuleException>(() =>
        //     stateMachine.ApplyDiscard(state, seatIndex: 2, tileIndex: 0));
        
        // Assert.Equal(TableActionErrorCodes.InvalidHandSize, ex.ErrorCode);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_PhaseGating_EnforcesAwaitingDiscardBeforeDraw()
    {
        // Phase-based turn gating: AwaitingDiscard → Draw → AwaitingDiscard
        
        // Arrange: State in AwaitingDiscard phase
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = CreateGameState(phase: TableTurnPhase.AwaitingDiscard, activeSeat: 1);
        
        // Act: Apply discard
        // var state2 = stateMachine.ApplyDiscard(state, seatIndex: 1, tileIndex: 3);
        
        // Assert: Phase advances, next player draws
        // Assert.Equal(2, state2.ActiveSeat);
        // Assert.Equal(TableTurnPhase.AwaitingDraw, state2.Phase); // or auto-transitions
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_TileConservation_AllTilesAccountedFor()
    {
        // At any point, 108 tiles must be accounted for across wall + hands + discards + melds
        
        // Arrange: Initial state
        // var stateMachine = new ChangshaGameStateMachine();
        // var state = stateMachine.CreateInitialState();
        
        // Act: Count tiles
        // var wallTiles = state.Wall.RemainingTiles;
        // var handTiles = state.Hands.Sum(h => h.TileCount);
        // var discardTiles = state.DiscardPile.TileCount;
        // var meldTiles = state.ExposedMelds.Sum(m => m.Melds.Sum(meld => meld.Tiles.Count));
        
        // Assert: Total is always 108
        // Assert.Equal(108, wallTiles + handTiles + discardTiles + meldTiles);
    }

    [Fact(Skip = "Awaiting Bishop's ChangshaGameStateMachine")]
    [Trait("Category", "Changsha")]
    public void StateMachine_Replay_ReconstructsIdenticalState()
    {
        // Event replay from seed must produce identical state
        
        // Arrange: Original game sequence
        // var machine = new ChangshaGameStateMachine(seed: 555);
        // var state1 = machine.CreateInitialState();
        // var state2 = machine.ApplyDiscard(state1, seatIndex: 0, tileIndex: 7);
        // var state3 = machine.ApplyDraw(state2, seatIndex: 1);
        
        // Act: Replay from same seed and same actions
        // var replayMachine = new ChangshaGameStateMachine(seed: 555);
        // var replayState1 = replayMachine.CreateInitialState();
        // var replayState2 = replayMachine.ApplyDiscard(replayState1, seatIndex: 0, tileIndex: 7);
        // var replayState3 = replayMachine.ApplyDraw(replayState2, seatIndex: 1);
        
        // Assert: Final states identical
        // Assert.Equal(state3.Integrity.StateHash, replayState3.Integrity.StateHash);
    }
}
