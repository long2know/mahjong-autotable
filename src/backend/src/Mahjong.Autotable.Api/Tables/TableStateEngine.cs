namespace Mahjong.Autotable.Api.Tables;

public interface ITableStateEngine
{
    TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null, int? seed = null);
    DiscardActionResult ApplyHumanDiscard(TableGameState state, int seatIndex, int tileId);
    BotAdvanceResult AdvanceBots(TableGameState state, int maxActions);
}

public sealed class DiscardActionResult
{
    public required TableAction DiscardAction { get; init; }
    public required TableAction? DrawAction { get; init; }
}

public sealed class TableStateEngine : ITableStateEngine
{
    public const int SeatCount = 4;
    public const int TotalTiles = 136;

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

        return new TableGameState
        {
            ActiveSeat = 0,
            TurnNumber = 1,
            DrawNumber = 1,
            Metadata = new TableStateMetadata { Seed = resolvedSeed },
            Seats = seats,
            Hands = hands,
            Wall = wall
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

            var seat = state.Seats[state.ActiveSeat];
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
        if (state.Seats.Count != SeatCount)
        {
            throw new InvalidOperationException("Table state must contain exactly four seats.");
        }

        if (state.ActiveSeat is < 0 or >= SeatCount)
        {
            throw new InvalidOperationException("Active seat must be between 0 and 3.");
        }

        if (state.Seats.Select(seat => seat.SeatIndex).Distinct().Count() != SeatCount)
        {
            throw new InvalidOperationException("Seat indexes must be unique.");
        }

        if (state.Hands.Count != SeatCount)
        {
            throw new InvalidOperationException("Table state must contain exactly four hands.");
        }

        if (state.Hands.Select(hand => hand.SeatIndex).Distinct().Count() != SeatCount)
        {
            throw new InvalidOperationException("Hand seat indexes must be unique.");
        }
    }

    private static DiscardActionResult ApplyDiscard(
        TableGameState state,
        int seatIndex,
        int tileId,
        TableSeatType requiredSeatType)
    {
        ValidateState(state);

        if (state.Phase != TableTurnPhase.AwaitingDiscard)
        {
            throw new InvalidOperationException("Discards are only allowed while awaiting discard.");
        }

        if (seatIndex != state.ActiveSeat)
        {
            throw new InvalidOperationException("Only the active seat can discard.");
        }

        var seat = state.Seats.Single(currentSeat => currentSeat.SeatIndex == seatIndex);
        if (seat.SeatType != requiredSeatType)
        {
            throw new InvalidOperationException($"Seat {seatIndex} is not a {requiredSeatType} seat.");
        }

        var hand = GetHandForSeat(state, seatIndex);
        if (!hand.Tiles.Remove(tileId))
        {
            throw new InvalidOperationException("Tile is not in the active seat hand.");
        }

        var discardAction = CreateAction(
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
        state.ActionLog.Add(discardAction);

        state.ActiveSeat = (state.ActiveSeat + 1) % SeatCount;
        state.TurnNumber++;

        if (state.Wall.Count == 0)
        {
            state.Phase = TableTurnPhase.WallExhausted;
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

        var drawAction = CreateAction(
            drawSeat,
            state.TurnNumber,
            "draw",
            drawTileId,
            $"tile-{drawTileId}");
        state.ActionLog.Add(drawAction);

        return new DiscardActionResult
        {
            DiscardAction = discardAction,
            DrawAction = drawAction
        };
    }

    private static TableSeatHandState GetHandForSeat(TableGameState state, int seatIndex) =>
        state.Hands.Single(hand => hand.SeatIndex == seatIndex);

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

    private static TableAction CreateAction(
        int seatIndex,
        int turnNumber,
        string actionType,
        int? tileId,
        string detail) =>
        new()
        {
            SeatIndex = seatIndex,
            TurnNumber = turnNumber,
            ActionType = actionType,
            TileId = tileId,
            Detail = detail,
            OccurredUtc = DateTime.UtcNow
        };
}
