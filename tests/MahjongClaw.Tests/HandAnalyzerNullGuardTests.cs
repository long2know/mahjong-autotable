using FluentAssertions;
using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Tests;

public class HandAnalyzerNullGuardTests
{
    [Fact]
    public void IsWinningHand_ThrowsForNullTiles()
    {
        var act = () => HandAnalyzer.IsWinningHand(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryGetWinningHand_ThrowsForNullTiles()
    {
        var act = () => HandAnalyzer.TryGetWinningHand(null!, out _);

        act.Should().Throw<ArgumentNullException>();
    }
}
