namespace MahjongClaw.Domain.Gameplay;

public enum TurnPhase
{
    NotStarted,
    Draw,
    Discard,
    Claim,
    Completed,
    Invalid
}

public enum TurnAction
{
    Draw,
    Discard,
    Meld,
    Win
}

public sealed record TurnActionState(TurnPhase Phase, IReadOnlyList<TurnAction> LegalActions)
{
    public bool HasAction(TurnAction action) => LegalActions.Contains(action);
}

public static class TurnActionStateResolver
{
    private static readonly IReadOnlyList<TurnAction> NoActions = Array.Empty<TurnAction>();

    public static TurnActionState Resolve(
        string? gameStatus,
        int handTileCount,
        int meldTileCount,
        int declaredKongCount = 0,
        bool hasPendingClaimWindow = false)
    {
        if (string.Equals(gameStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return new TurnActionState(TurnPhase.Completed, NoActions);
        }

        if (!string.Equals(gameStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return new TurnActionState(TurnPhase.NotStarted, NoActions);
        }

        if (hasPendingClaimWindow)
        {
            return new TurnActionState(TurnPhase.Claim, NoActions);
        }

        if (TurnFlowValidator.CanDraw(handTileCount, meldTileCount, declaredKongCount))
        {
            return new TurnActionState(TurnPhase.Draw, new[] { TurnAction.Draw });
        }

        if (!TurnFlowValidator.CanDiscard(handTileCount, meldTileCount, declaredKongCount))
        {
            return new TurnActionState(TurnPhase.Invalid, NoActions);
        }

        return new TurnActionState(
            TurnPhase.Discard,
            new[]
            {
                TurnAction.Discard,
                TurnAction.Meld,
                TurnAction.Win
            });
    }
}
