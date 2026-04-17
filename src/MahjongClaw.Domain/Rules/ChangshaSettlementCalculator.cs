using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Rules;

public sealed record ChangshaSettlementSeat(Guid PlayerId, Wind Seat, int CurrentScore);

public sealed record ChangshaSeatDelta(Guid PlayerId, int Delta, int NewScore);

public sealed record ChangshaSettlementRequest(
    IReadOnlyList<ChangshaSettlementSeat> Seats,
    Guid WinnerId,
    Guid DealerId,
    bool IsSelfDraw,
    bool IsBigWin,
    Guid? DiscarderId = null,
    int? BirdRank = null,
    bool IsMultipleWin = false);

public sealed record ChangshaSettlementResult(
    IReadOnlyList<ChangshaSeatDelta> ScoreDeltas,
    Guid NextDealerId,
    int NextRoundIncrement,
    Wind? BirdSeat);

public static class ChangshaSettlementCalculator
{
    private static readonly IReadOnlyList<Wind> CounterClockwiseSeatOrder =
    [
        Wind.East,
        Wind.South,
        Wind.West,
        Wind.North
    ];

    public static ChangshaSettlementResult Calculate(ChangshaSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Seats);

        if (request.Seats.Count != 4)
        {
            throw new ArgumentException("Changsha settlement requires exactly four seats.", nameof(request));
        }

        var seatsByPlayer = request.Seats.ToDictionary(seat => seat.PlayerId);
        if (!seatsByPlayer.ContainsKey(request.WinnerId))
        {
            throw new ArgumentException("Winner must belong to one of the four seats.", nameof(request));
        }

        if (!seatsByPlayer.ContainsKey(request.DealerId))
        {
            throw new ArgumentException("Dealer must belong to one of the four seats.", nameof(request));
        }

        if (!request.IsSelfDraw)
        {
            if (request.DiscarderId is null)
            {
                throw new ArgumentException("Discarder is required for discard wins.", nameof(request));
            }

            if (!seatsByPlayer.ContainsKey(request.DiscarderId.Value))
            {
                throw new ArgumentException("Discarder must belong to one of the four seats.", nameof(request));
            }

            if (request.DiscarderId == request.WinnerId)
            {
                throw new ArgumentException("Winner and discarder cannot be the same player for discard wins.", nameof(request));
            }
        }

        if (request.BirdRank is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(request.BirdRank), "Bird rank must be between 1 and 9.");
        }

        var deltas = request.Seats.ToDictionary(seat => seat.PlayerId, static _ => 0);
        var loserBasePayments = new Dictionary<Guid, int>();
        var winnerIsDealer = request.WinnerId == request.DealerId;

        if (request.IsSelfDraw)
        {
            foreach (var seat in request.Seats)
            {
                if (seat.PlayerId == request.WinnerId)
                {
                    continue;
                }

                var payment = request.IsBigWin
                    ? winnerIsDealer
                        ? 70
                        : seat.PlayerId == request.DealerId ? 70 : 60
                    : winnerIsDealer
                        ? 20
                        : seat.PlayerId == request.DealerId ? 20 : 10;

                loserBasePayments[seat.PlayerId] = payment;
                deltas[seat.PlayerId] -= payment;
                deltas[request.WinnerId] += payment;
            }
        }
        else
        {
            var discarderPlayerId = request.DiscarderId!.Value;
            var payment = request.IsBigWin ? 60 : 10;

            if (discarderPlayerId == request.DealerId)
            {
                payment += 10;
            }

            if (winnerIsDealer)
            {
                payment += 10;
            }

            loserBasePayments[discarderPlayerId] = payment;
            deltas[discarderPlayerId] -= payment;
            deltas[request.WinnerId] += payment;
        }

        Wind? birdSeat = null;
        if (request.BirdRank is { } birdRank)
        {
            birdSeat = ResolveBirdSeat(request.Seats, request.DealerId, birdRank);
            ApplyBirdCatchAdjustments(request, birdSeat.Value, deltas, loserBasePayments);
        }

        var scoreDeltas = request.Seats
            .Select(seat => new ChangshaSeatDelta(
                seat.PlayerId,
                deltas[seat.PlayerId],
                seat.CurrentScore + deltas[seat.PlayerId]))
            .ToList();

        var nextDealerId = request.IsMultipleWin && !request.IsSelfDraw && request.DiscarderId is { } discarderId
            ? discarderId
            : request.WinnerId;

        return new ChangshaSettlementResult(
            scoreDeltas,
            nextDealerId,
            NextRoundIncrement: 1,
            birdSeat);
    }

    private static void ApplyBirdCatchAdjustments(
        ChangshaSettlementRequest request,
        Wind birdSeat,
        IDictionary<Guid, int> deltas,
        IReadOnlyDictionary<Guid, int> loserBasePayments)
    {
        var winnerSeat = request.Seats.Single(seat => seat.PlayerId == request.WinnerId);

        if (request.IsSelfDraw)
        {
            if (winnerSeat.Seat == birdSeat)
            {
                foreach (var (loserId, payment) in loserBasePayments)
                {
                    deltas[loserId] -= payment;
                    deltas[request.WinnerId] += payment;
                }

                return;
            }

            var birdPlayer = request.Seats.FirstOrDefault(seat => seat.Seat == birdSeat);
            if (birdPlayer is null || birdPlayer.PlayerId == request.WinnerId)
            {
                return;
            }

            if (!loserBasePayments.TryGetValue(birdPlayer.PlayerId, out var loserPayment))
            {
                return;
            }

            deltas[birdPlayer.PlayerId] -= loserPayment;
            deltas[request.WinnerId] += loserPayment;
            return;
        }

        if (request.DiscarderId is not { } discarderId)
        {
            return;
        }

        var discarderSeat = request.Seats.Single(seat => seat.PlayerId == discarderId);
        if (birdSeat != winnerSeat.Seat && birdSeat != discarderSeat.Seat)
        {
            return;
        }

        if (!loserBasePayments.TryGetValue(discarderId, out var discardPayment))
        {
            return;
        }

        deltas[discarderId] -= discardPayment;
        deltas[request.WinnerId] += discardPayment;
    }

    private static Wind ResolveBirdSeat(IReadOnlyList<ChangshaSettlementSeat> seats, Guid dealerId, int birdRank)
    {
        var dealerSeat = seats.Single(seat => seat.PlayerId == dealerId).Seat;
        var dealerIndex = CounterClockwiseSeatOrder
            .Select(static (seat, index) => new { seat, index })
            .Where(item => item.seat == dealerSeat)
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        if (dealerIndex < 0)
        {
            throw new InvalidOperationException($"Dealer seat '{dealerSeat}' is not in the supported seat order.");
        }

        var relativeOffset = (birdRank - 1) % CounterClockwiseSeatOrder.Count;
        return CounterClockwiseSeatOrder[(dealerIndex + relativeOffset) % CounterClockwiseSeatOrder.Count];
    }
}
