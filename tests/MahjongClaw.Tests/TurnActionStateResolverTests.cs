using FluentAssertions;
using MahjongClaw.Domain.Gameplay;

namespace MahjongClaw.Tests;

public sealed class TurnActionStateResolverTests
{
    [Fact]
    public void Resolve_ReturnsNotStarted_WhenGameIsNotActive()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Waiting",
            handTileCount: 13,
            meldTileCount: 0);

        result.Phase.Should().Be(TurnPhase.NotStarted);
        result.LegalActions.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ReturnsCompleted_WhenGameIsCompleted()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Completed",
            handTileCount: 14,
            meldTileCount: 0);

        result.Phase.Should().Be(TurnPhase.Completed);
        result.LegalActions.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ReturnsClaim_WhenClaimWindowIsOpen()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Active",
            handTileCount: 14,
            meldTileCount: 0,
            hasPendingClaimWindow: true);

        result.Phase.Should().Be(TurnPhase.Claim);
        result.LegalActions.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_ReturnsDrawPhase_WhenHandShapeAllowsDraw()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Active",
            handTileCount: 13,
            meldTileCount: 0);

        result.Phase.Should().Be(TurnPhase.Draw);
        result.LegalActions.Should().Equal(TurnAction.Draw);
    }

    [Fact]
    public void Resolve_ReturnsDiscardPhase_WhenHandShapeAllowsDiscard()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Active",
            handTileCount: 14,
            meldTileCount: 0);

        result.Phase.Should().Be(TurnPhase.Discard);
        result.LegalActions.Should().Contain(new[]
        {
            TurnAction.Discard,
            TurnAction.Meld,
            TurnAction.Win
        });
        result.LegalActions.Should().NotContain(TurnAction.Draw);
    }

    [Fact]
    public void Resolve_ReturnsDiscardPhase_WhenDeclaredKongAdjustsTileShape()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Active",
            handTileCount: 11,
            meldTileCount: 4,
            declaredKongCount: 1);

        result.Phase.Should().Be(TurnPhase.Discard);
        result.LegalActions.Should().Contain(TurnAction.Discard);
    }

    [Fact]
    public void Resolve_ReturnsInvalid_WhenActiveHandShapeIsInvalid()
    {
        var result = TurnActionStateResolver.Resolve(
            gameStatus: "Active",
            handTileCount: 12,
            meldTileCount: 0);

        result.Phase.Should().Be(TurnPhase.Invalid);
        result.LegalActions.Should().BeEmpty();
    }
}
