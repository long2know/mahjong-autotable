namespace Mahjong.Autotable.Api.Tables;

public interface ITableStateEngine
{
    TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null, int? seed = null);
    void NormalizePersistedState(TableGameState state, int persistedStateVersion);
    TableGameState ReplayFromSeed(TableGameState snapshot);
    ReplayVerificationResult VerifyReplayIntegrity(TableGameState snapshot);
    DiscardActionResult ApplyHumanDiscard(TableGameState state, int seatIndex, int tileId);
    BotAdvanceResult AdvanceBots(TableGameState state, int maxActions);
}

public sealed class DiscardActionResult
{
    public required TableAction DiscardAction { get; init; }
    public required TableAction? DrawAction { get; init; }
}

public sealed class ReplayVerificationResult
{
    public required bool IntegrityMatch { get; init; }
    public required string ExpectedStateHash { get; init; }
    public required string ReplayedStateHash { get; init; }
    public required int ReplayedStateVersion { get; init; }
    public required long ReplayedActionSequence { get; init; }
}

public sealed class TableStateEngine : ITableStateEngine
{
    public const int SeatCount = 4;
    public const int TotalTiles = 136;
    public const string RngAlgorithmId = "fisher-yates-v1";

    public TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null, int? seed = null)
    {
        var botSet = NormalizeBots(botSeatIndexes);
        var resolvedSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);

        var seats = Enumerable.Range(0, SeatCount)
            .Select(index => new TableSeatState
            {
                SeatIndex = index,
                SeatType = botSet.Contains(index) ? TableSeatType.Bot : TableSeatType.Human,
                PlayerId = botSet.Contains(index) ? $"bot-{index}" : $"human-{index}"
            })
            .ToList();

        var wall = CreateShuffledWall(resolvedSeed);
        var hands = Enumerable.Range(0, SeatCount)
            .Select(index => new TableSeatHandState { SeatIndex = index })
            .ToList();

        for (var round = 0; round < 13; round++)
        {
            for (var seatIndex = 0; seatIndex < SeatCount; seatIndex++)
            {
                hands[seatIndex].Tiles.Add(DrawFromWall(wall));
            }
        }

        hands[0].Tiles.Add(DrawFromWall(wall));

        var state = new TableGameState
        {
            StateVersion = 1,
            ActionSequence = 0,
            ActiveSeat = 0,
            TurnNumber = 1,
            DrawNumber = 1,
            Metadata = new TableStateMetadata
            {
                Seed = resolvedSeed,
                AlgorithmId = RngAlgorithmId
            },
            Seats = seats,
            Hands = hands,
            Wall = wall
        };

        RefreshIntegrity(state);
        return state;
    }

    public void NormalizePersistedState(TableGameState state, int persistedStateVersion)
    {
        ValidateState(state);
        state.StateVersion = Math.Max(state.StateVersion, persistedStateVersion);
        state.ActionSequence = Math.Max(state.ActionSequence, GetMaxActionSequence(state));
        state.Metadata ??= new TableStateMetadata();
        if (string.IsNullOrWhiteSpace(state.Metadata.AlgorithmId))
        {
            state.Metadata.AlgorithmId = RngAlgorithmId;
        }

        if (state.LastAction is null && state.ActionLog.Count > 0)
        {
            state.LastAction = ToLastAction(state.ActionLog[^1]);
        }

        RefreshIntegrity(state);
    }

    public TableGameState ReplayFromSeed(TableGameState snapshot)
    {
        ValidateState(snapshot);
        snapshot.Metadata ??= new TableStateMetadata();

        var botSeatIndexes = snapshot.Seats
            .Where(seat => seat.SeatType == TableSeatType.Bot)
            .Select(seat => seat.SeatIndex)
            .ToArray();

        var replay = CreateInitialState(botSeatIndexes, snapshot.Metadata.Seed);
        var discardActions = snapshot.ActionLog
            .Where(action => action.ActionType.Equals("discard", StringComparison.OrdinalIgnoreCase))
            .OrderBy(action => action.Sequence);

        foreach (var action in discardActions)
        {
            var tileId = action.TileId;
            if (!tileId.HasValue)
            {
                ThrowInvariant(replay, "Discard action is missing tile id for replay.");
            }

            var replaySeat = GetSeat(replay, action.SeatIndex);
            var requiredSeatType = replaySeat.SeatType == TableSeatType.Human
                ? TableSeatType.Human
                : TableSeatType.Bot;
            _ = ApplyDiscard(replay, action.SeatIndex, tileId.GetValueOrDefault(), requiredSeatType);
        }

        return replay;
    }

    public ReplayVerificationResult VerifyReplayIntegrity(TableGameState snapshot)
    {
        var replay = ReplayFromSeed(snapshot);
        return new ReplayVerificationResult
        {
            IntegrityMatch = string.Equals(
                snapshot.Integrity.StateHash,
                replay.Integrity.StateHash,
                StringComparison.Ordinal),
            ExpectedStateHash = snapshot.Integrity.StateHash,
            ReplayedStateHash = replay.Integrity.StateHash,
            ReplayedStateVersion = replay.StateVersion,
            ReplayedActionSequence = replay.ActionSequence
        };
    }

    public DiscardActionResult ApplyHumanDiscard(TableGameState state, int seatIndex, int tileId)
        => ApplyDiscard(state, seatIndex, tileId, TableSeatType.Human);

    public BotAdvanceResult AdvanceBots(TableGameState state, int maxActions)
    {
        ValidateState(state);
        var boundedActions = Math.Max(1, maxActions);
        var actions = new List<TableAction>();

        while (actions.Count < boundedActions)
        {
            if (state.Phase == TableTurnPhase.WallExhausted)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.WallExhausted
                };
            }

            var seat = GetSeat(state, state.ActiveSeat);
            if (seat.SeatType == TableSeatType.Human)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.HumanTurn
                };
            }

            var requiredActions = state.Wall.Count > 0 ? 2 : 1;
            if (actions.Count + requiredActions > boundedActions)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.MaxActionsReached
                };
            }

            var hand = GetHandForSeat(state, seat.SeatIndex);
            var tileId = hand.Tiles.Min();
            var result = ApplyDiscard(state, seat.SeatIndex, tileId, TableSeatType.Bot);
            actions.Add(result.DiscardAction);
            if (result.DrawAction is not null)
            {
                actions.Add(result.DrawAction);
            }

            if (state.Phase == TableTurnPhase.WallExhausted)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.WallExhausted
                };
            }
        }

        return new BotAdvanceResult
        {
            Actions = actions,
            StopReason = BotAdvanceStopReason.MaxActionsReached
        };
    }

    private static HashSet<int> NormalizeBots(IReadOnlyCollection<int>? botSeatIndexes)
    {
        if (botSeatIndexes is null || botSeatIndexes.Count == 0)
        {
            return [1, 2, 3];
        }

        if (botSeatIndexes.Count >= SeatCount)
        {
            throw new ArgumentException("At least one human seat is required.", nameof(botSeatIndexes));
        }

        var set = new HashSet<int>(botSeatIndexes);
        if (set.Count != botSeatIndexes.Count)
        {
            throw new ArgumentException("Bot seat indexes must be unique.", nameof(botSeatIndexes));
        }

        if (set.Any(index => index is < 0 or >= SeatCount))
        {
            throw new ArgumentException("Bot seat indexes must be between 0 and 3.", nameof(botSeatIndexes));
        }

        return set;
    }

    private static void ValidateState(TableGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Seats.Count != SeatCount)
        {
            ThrowInvariant(state, "Table state must contain exactly four seats.");
        }

        if (state.ActiveSeat is < 0 or >= SeatCount)
        {
            ThrowInvariant(state, "Active seat must be between 0 and 3.");
        }

        var expectedSeatIndexes = Enumerable.Range(0, SeatCount);
        var seatIndexes = state.Seats.Select(seat => seat.SeatIndex).OrderBy(index => index).ToArray();
        if (!seatIndexes.SequenceEqual(expectedSeatIndexes))
        {
            ThrowInvariant(state, "Seat indexes must map to 0-3 exactly once.");
        }

        if (state.Hands.Count != SeatCount)
        {
            ThrowInvariant(state, "Table state must contain exactly four hands.");
        }

        var handSeatIndexes = state.Hands.Select(hand => hand.SeatIndex).OrderBy(index => index).ToArray();
        if (!handSeatIndexes.SequenceEqual(expectedSeatIndexes))
        {
            ThrowInvariant(state, "Hand seat indexes must map to 0-3 exactly once.");
        }

        var trackedTiles = state.Wall.Count + state.Hands.Sum(hand => hand.Tiles.Count) + state.DiscardPile.Count;
        if (trackedTiles != TotalTiles)
        {
            ThrowInvariant(state, "Tile conservation invariant failed.");
        }
    }

    private static DiscardActionResult ApplyDiscard(
        TableGameState state,
        int seatIndex,
        int tileId,
        TableSeatType requiredSeatType)
    {
        ValidateState(state);

        if (state.Phase == TableTurnPhase.WallExhausted)
        {
            ThrowRule(state, TableActionErrorCodes.RoundNotActive, "Round is no longer active.");
        }

        if (state.Phase != TableTurnPhase.AwaitingDiscard)
        {
            ThrowRule(state, TableActionErrorCodes.InvalidPhase, "Discards are only allowed while awaiting discard.");
        }

        if (seatIndex != state.ActiveSeat)
        {
            ThrowRule(state, TableActionErrorCodes.NotActiveSeat, "Only the active seat can discard.");
        }

        var seat = GetSeat(state, seatIndex);
        if (seat.SeatType != requiredSeatType)
        {
            ThrowRule(state, TableActionErrorCodes.NotActiveSeat, $"Seat {seatIndex} is not a {requiredSeatType} seat.");
        }

        var hand = GetHandForSeat(state, seatIndex);
        if (!hand.Tiles.Remove(tileId))
        {
            ThrowRule(state, TableActionErrorCodes.TileNotInHand, "Tile is not in the active seat hand.");
        }

        var discardAction = AppendAction(
            state,
            seatIndex,
            state.TurnNumber,
            "discard",
            tileId,
            $"tile-{tileId}");

        state.DiscardPile.Add(new TableDiscard
        {
            SeatIndex = seatIndex,
            TileId = tileId,
            TurnNumber = state.TurnNumber,
            OccurredUtc = discardAction.OccurredUtc
        });

        state.ActiveSeat = (state.ActiveSeat + 1) % SeatCount;
        state.TurnNumber++;
        RefreshIntegrity(state);
        discardAction.StateHash = state.Integrity.StateHash;

        if (state.Wall.Count == 0)
        {
            state.Phase = TableTurnPhase.WallExhausted;
            RefreshIntegrity(state);
            discardAction.StateHash = state.Integrity.StateHash;
            return new DiscardActionResult
            {
                DiscardAction = discardAction,
                DrawAction = null
            };
        }

        var drawSeat = state.ActiveSeat;
        var drawTileId = DrawFromWall(state.Wall);
        var nextHand = GetHandForSeat(state, drawSeat);
        nextHand.Tiles.Add(drawTileId);
        state.DrawNumber++;
        state.Phase = TableTurnPhase.AwaitingDiscard;

        var drawAction = AppendAction(
            state,
            drawSeat,
            state.TurnNumber,
            "draw",
            drawTileId,
            $"tile-{drawTileId}");

        RefreshIntegrity(state);
        drawAction.StateHash = state.Integrity.StateHash;

        return new DiscardActionResult
        {
            DiscardAction = discardAction,
            DrawAction = drawAction
        };
    }

    private static TableSeatState GetSeat(TableGameState state, int seatIndex)
    {
        var seat = state.Seats.SingleOrDefault(currentSeat => currentSeat.SeatIndex == seatIndex);
        if (seat is null)
        {
            throw new TableRuleException(
                TableActionErrorCodes.SeatNotFound,
                $"Seat {seatIndex} does not exist.",
                state.StateVersion,
                state.ActionSequence);
        }

        return seat;
    }

    private static TableSeatHandState GetHandForSeat(TableGameState state, int seatIndex)
    {
        var hand = state.Hands.SingleOrDefault(currentHand => currentHand.SeatIndex == seatIndex);
        if (hand is null)
        {
            throw new TableRuleException(
                TableActionErrorCodes.StateInvariantBroken,
                $"Seat {seatIndex} hand is missing.",
                state.StateVersion,
                state.ActionSequence);
        }

        return hand;
    }

    private static List<int> CreateShuffledWall(int seed)
    {
        var wall = Enumerable.Range(0, TotalTiles).ToList();
        var random = new Random(seed);

        for (var index = wall.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (wall[index], wall[swapIndex]) = (wall[swapIndex], wall[index]);
        }

        return wall;
    }

    private static int DrawFromWall(List<int> wall)
    {
        if (wall.Count == 0)
        {
            throw new InvalidOperationException("Cannot draw from an empty wall.");
        }

        var lastIndex = wall.Count - 1;
        var tileId = wall[lastIndex];
        wall.RemoveAt(lastIndex);
        return tileId;
    }

    private static TableAction AppendAction(
        TableGameState state,
        int seatIndex,
        int turnNumber,
        string actionType,
        int? tileId,
        string detail)
    {
        var action = new TableAction
        {
            Sequence = ++state.ActionSequence,
            SeatIndex = seatIndex,
            TurnNumber = turnNumber,
            ActionType = actionType,
            TileId = tileId,
            Detail = detail,
            OccurredUtc = DateTime.UtcNow
        };

        state.ActionLog.Add(action);
        state.LastAction = ToLastAction(action);
        state.StateVersion++;
        return action;
    }

    private static TableLastActionState ToLastAction(TableAction action) =>
        new()
        {
            Sequence = action.Sequence,
            SeatIndex = action.SeatIndex,
            ActionType = action.ActionType,
            TileId = action.TileId,
            Detail = action.Detail
        };

    private static long GetMaxActionSequence(TableGameState state) =>
        state.ActionLog.Count == 0 ? 0 : state.ActionLog.Max(action => action.Sequence);

    private static void RefreshIntegrity(TableGameState state)
    {
        state.Integrity ??= new TableIntegrityState();
        state.Integrity.StateHash = TableStateHasher.Compute(state);
    }

    private static void ThrowInvariant(TableGameState state, string message) =>
        ThrowRule(state, TableActionErrorCodes.StateInvariantBroken, message);

    private static void ThrowRule(TableGameState state, string code, string message) =>
        throw new TableRuleException(code, message, state.StateVersion, state.ActionSequence);
}
