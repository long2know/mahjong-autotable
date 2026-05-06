using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Domain.Gameplay;

public sealed class PendingClaimWindow
{
    public required Guid DiscardedTileId { get; init; }
    public required Tile DiscardedTile { get; init; }
    public required Guid DiscarderId { get; init; }
    public required IReadOnlyList<Guid> EligiblePlayerIds { get; init; }
    public required DateTime OpenedAtUtc { get; init; }
    public required int TimeoutMs { get; init; }
    public List<ClaimRequest> SubmittedClaims { get; init; } = new();

    public bool IsExpired(DateTime nowUtc) =>
        (nowUtc - OpenedAtUtc).TotalMilliseconds >= TimeoutMs;

    public bool HasAllResponses() =>
        SubmittedClaims.Count >= EligiblePlayerIds.Count;
}
