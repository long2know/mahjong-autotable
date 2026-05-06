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

    /// <summary>
    /// Resolves multi-winner claims. Returns all Win claims when multiple players claim Win.
    /// For non-Win claims, returns single highest-priority claim resolved by proximity.
    /// </summary>
    public static IReadOnlyList<ClaimRequest> ResolveMultiWin(
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
            return Array.Empty<ClaimRequest>();
        }

        var maxPriority = nonPassClaims.Max(c => c.Priority);
        var topPriorityClaims = nonPassClaims
            .Where(c => c.Priority == maxPriority)
            .ToList();

        // If top priority is Win and multiple players claimed Win, return all Win claims
        if (topPriorityClaims.Count > 1 && topPriorityClaims[0].ClaimType == ClaimType.Win)
        {
            return topPriorityClaims;
        }

        // Otherwise, single claim resolution (Pung/Kong/Chow tie-breaking, or single Win)
        var singleClaim = topPriorityClaims.Count == 1
            ? topPriorityClaims[0]
            : ResolveByProximity(topPriorityClaims, discarderId, playerSeats);

        return new[] { singleClaim };
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
