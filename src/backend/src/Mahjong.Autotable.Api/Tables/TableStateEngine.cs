namespace Mahjong.Autotable.Api.Tables;

public interface ITableStateEngine
{
    TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null);
    BotAdvanceResult AdvanceBots(TableGameState state, int maxActions);
}

public sealed class TableStateEngine : ITableStateEngine
{
    public const int SeatCount = 4;

    public TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null)
    {
        var botSet = NormalizeBots(botSeatIndexes);

        var seats = Enumerable.Range(0, SeatCount)
            .Select(index => new TableSeatState
            {
                SeatIndex = index,
                SeatType = botSet.Contains(index) ? TableSeatType.Bot : TableSeatType.Human,
                PlayerId = botSet.Contains(index) ? $"bot-{index}" : $"human-{index}"
            })
            .ToList();

        return new TableGameState
        {
            ActiveSeat = 0,
            TurnNumber = 1,
            DrawNumber = 0,
            Seats = seats
        };
    }

    public BotAdvanceResult AdvanceBots(TableGameState state, int maxActions)
    {
        ValidateState(state);
        var boundedActions = Math.Max(1, maxActions);
        var actions = new List<TableAction>();

        while (actions.Count < boundedActions)
        {
            var seat = state.Seats[state.ActiveSeat];
            if (seat.SeatType == TableSeatType.Human)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.HumanTurn
                };
            }

            if (actions.Count + 2 > boundedActions)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.MaxActionsReached
                };
            }

            state.DrawNumber++;
            var drawAction = CreateAction(
                state.ActiveSeat,
                state.TurnNumber,
                "draw",
                $"draw-{state.ActiveSeat}-{state.DrawNumber}");
            AddAction(state, actions, drawAction);

            var tileNumber = ((state.DrawNumber + state.ActiveSeat) % 9) + 1;
            var discardAction = CreateAction(
                state.ActiveSeat,
                state.TurnNumber,
                "discard",
                $"tile-{tileNumber}");
            AddAction(state, actions, discardAction);

            state.ActiveSeat = (state.ActiveSeat + 1) % SeatCount;
            state.TurnNumber++;
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
    }

    private static TableAction CreateAction(int seatIndex, int turnNumber, string actionType, string detail) =>
        new()
        {
            SeatIndex = seatIndex,
            TurnNumber = turnNumber,
            ActionType = actionType,
            Detail = detail,
            OccurredUtc = DateTime.UtcNow
        };

    private static void AddAction(TableGameState state, List<TableAction> actions, TableAction action)
    {
        actions.Add(action);
        state.ActionLog.Add(action);
    }
}
