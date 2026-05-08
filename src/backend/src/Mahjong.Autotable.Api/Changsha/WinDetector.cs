namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// STUB: Win pattern detection service for Changsha Mahjong.
/// TODO: Implement full win detection logic for 4 core patterns:
/// - Standard 4+1 with 258 pair (Small Win)
/// - Seven Pairs (Big Win)
/// - All Pungs (Big Win)
/// - Full Flush (Big Win)
/// </summary>
public static class ChangshaWinDetector
{
    /// <summary>
    /// STUB: Check if adding the specified tile to the hand creates a winning configuration.
    /// </summary>
    public static bool IsWinningWith(ChangshaHandState hand, int tileId)
    {
        // STUB: Always return false until full implementation
        return false;
    }
}
