using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class ChowEligibilityRule
{
    public static bool IsEligibleForChow(
        Guid claimerId,
        Guid discarderId,
        IReadOnlyDictionary<Guid, Wind> playerSeats)
    {
        ArgumentNullException.ThrowIfNull(playerSeats);

        if (!playerSeats.TryGetValue(claimerId, out var claimerSeat))
        {
            return false;
        }

        if (!playerSeats.TryGetValue(discarderId, out var discarderSeat))
        {
            return false;
        }

        var downstreamSeat = GetDownstreamSeat(discarderSeat);
        return claimerSeat == downstreamSeat;
    }

    private static Wind GetDownstreamSeat(Wind currentSeat)
    {
        return currentSeat switch
        {
            Wind.East => Wind.South,
            Wind.South => Wind.West,
            Wind.West => Wind.North,
            Wind.North => Wind.East,
            _ => throw new ArgumentException($"Invalid seat: {currentSeat}", nameof(currentSeat))
        };
    }
}
