using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mahjong.Autotable.Api.Tables;

public static class TableStateHasher
{
    private static readonly JsonSerializerOptions CanonicalSerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Compute(TableGameState state)
    {
        var canonical = new
        {
            state.StateVersion,
            state.ActionSequence,
            state.ActiveSeat,
            state.TurnNumber,
            state.DrawNumber,
            Phase = state.Phase.ToString(),
            Rng = new
            {
                state.Metadata.Seed,
                state.Metadata.AlgorithmId
            },
            Wall = state.Wall,
            Seats = state.Seats
                .OrderBy(seat => seat.SeatIndex)
                .Select(seat => new
                {
                    seat.SeatIndex,
                    SeatType = seat.SeatType.ToString(),
                    seat.PlayerId
                }),
            Hands = state.Hands
                .OrderBy(hand => hand.SeatIndex)
                .Select(hand => new
                {
                    hand.SeatIndex,
                    hand.Tiles
                }),
            ExposedMelds = (state.ExposedMelds ?? [])
                .OrderBy(seatMelds => seatMelds.SeatIndex)
                .Select(seatMelds => new
                {
                    seatMelds.SeatIndex,
                    Melds = seatMelds.Melds
                        .OrderBy(meld => meld.SourceActionSequence)
                        .ThenBy(meld => meld.SourceTurnNumber)
                        .ThenBy(meld => meld.ClaimType)
                        .Select(meld => new
                        {
                            ClaimType = meld.ClaimType.ToString(),
                            meld.ClaimedFromSeatIndex,
                            meld.SourceTurnNumber,
                            meld.SourceActionSequence,
                            TileIds = meld.TileIds.OrderBy(tileId => tileId)
                        })
                }),
            DiscardPile = state.DiscardPile.Select(discard => new
            {
                discard.SeatIndex,
                discard.TileId,
                discard.TurnNumber
            }),
            ClaimWindow = state.ClaimWindow is null
                ? null
                : new
                {
                    state.ClaimWindow.SourceActionSequence,
                    state.ClaimWindow.DiscardSeatIndex,
                    state.ClaimWindow.DiscardTileId,
                    state.ClaimWindow.DiscardTurnNumber,
                    state.ClaimWindow.PrecedencePolicy,
                    Opportunities = state.ClaimWindow.Opportunities
                        .OrderBy(opportunity => opportunity.SeatIndex)
                        .ThenByDescending(opportunity => opportunity.Priority)
                        .ThenBy(opportunity => opportunity.ClaimType)
                        .Select(opportunity => new
                        {
                            opportunity.SeatIndex,
                            ClaimType = opportunity.ClaimType.ToString(),
                            opportunity.Priority
                        }),
                    SelectedOpportunity = state.ClaimWindow.SelectedOpportunity is null
                        ? null
                        : new
                        {
                            state.ClaimWindow.SelectedOpportunity.SeatIndex,
                            ClaimType = state.ClaimWindow.SelectedOpportunity.ClaimType.ToString(),
                            state.ClaimWindow.SelectedOpportunity.Priority
                        }
                },
            ActionLog = state.ActionLog.Select(action => new
            {
                action.Sequence,
                action.ActionType,
                action.SeatIndex,
                action.TurnNumber,
                action.TileId,
                action.Detail
            }),
            LastAction = state.LastAction is null
                ? null
                : new
                {
                    state.LastAction.Sequence,
                    state.LastAction.SeatIndex,
                    state.LastAction.ActionType,
                    state.LastAction.TileId,
                    state.LastAction.Detail
                }
        };

        var payload = JsonSerializer.Serialize(canonical, CanonicalSerializerOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes);
    }
}
