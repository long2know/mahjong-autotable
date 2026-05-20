namespace Mahjong.Autotable.Api.Changsha.Bot;

/// <summary>
/// Phase F resolver for <see cref="IChangshaBotStrategy"/>. The runtime asks
/// <c>ChangshaBotEngine.Resolve("easy"|"medium"|"hard")</c> on every decision point
/// and gets back a singleton strategy instance. Unknown values fall back to Medium —
/// matches Stephen's UX rule that an unrecognised <c>?bots=hard</c> shouldn't break
/// the game.
/// </summary>
/// <remarks>
/// Strategies are stateless across hands, so a single instance per difficulty is
/// reused for the lifetime of the process. This keeps allocations zero on the hot
/// path during a bot's turn.
/// </remarks>
public static class ChangshaBotEngine
{
    private static readonly IChangshaBotStrategy EasyInstance = new EasyStrategy();
    private static readonly IChangshaBotStrategy MediumInstance = new MediumStrategy();
    private static readonly IChangshaBotStrategy HardInstance = new HardStrategy();

    /// <summary>
    /// Resolves a difficulty string to its strategy. Case-insensitive; whitespace
    /// trimmed. Empty / null / unrecognised strings → Medium (the default).
    /// </summary>
    public static IChangshaBotStrategy Resolve(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
            return MediumInstance;

        return difficulty.Trim().ToLowerInvariant() switch
        {
            "easy" => EasyInstance,
            "medium" => MediumInstance,
            "hard" => HardInstance,
            _ => MediumInstance
        };
    }

    /// <summary>The default strategy (Medium) — exposed for unit testing.</summary>
    public static IChangshaBotStrategy Default => MediumInstance;
}
