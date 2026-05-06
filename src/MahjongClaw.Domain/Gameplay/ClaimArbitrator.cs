using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public static class ClaimArbitrator
{
    public static ClaimRequest? Resolve(
        IReadOnlyList<ClaimRequest> submissions,
        Guid discarderId,
        IReadOnlyDictionary<Guid, Wind> playerSeats)
    {
        ArgumentNullException.ThrowIfNull(submissions);
        ArgumentNullException.ThrowIfNull(playerSeats);

        if (!playerSeats.ContainsKey(discarderId))
        {
            throw new ArgumentException($"Discarder {discarderId} not found in player seats.", nameof(discarderId));
        }

        var nonPassClaims = submissions
            .Where(c => c.ClaimType != ClaimType.Pass)
            .ToList();

        if (nonPassClaims.Count == 0)
        {
            return null;
        }

        var maxPriority = nonPassClaims.Max(c => c.Priority);
        var topPriorityClaims = nonPassClaims
            .Where(c => c.Priority == maxPriority)
            .ToList();

        if (topPriorityClaims.Count == 1)
        {
            return topPriorityClaims[0];
        }

        return ResolveByProximity(topPriorityClaims, discarderId, playerSeats);
    }

    private static ClaimRequest ResolveByProximity(
        IReadOnlyList<ClaimRequest> claims,
        Guid discarderId,
        IReadOnlyDictionary<Guid, Wind> playerSeats)
    {
        var discarderSeat = playerSeats[discarderId];
        var seatOrder = new[] { Wind.East, Wind.South, Wind.West, Wind.North };
        var discarderIndex = Array.IndexOf(seatOrder, discarderSeat);

        for (var offset = 1; offset <= 3; offset++)
        {
            var targetSeat = seatOrder[(discarderIndex + offset) % 4];
            var claimForSeat = claims.FirstOrDefault(c =>
                playerSeats.TryGetValue(c.PlayerId, out var seat) && seat == targetSeat);

            if (claimForSeat is not null)
            {
                return claimForSeat;
            }
        }

        return claims[0];
    }
}
