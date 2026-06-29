using FluentAssertions;
using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Tests;

public sealed class ChangshaMeldSourceResolverTests
{
    [Theory]
    [InlineData("Kong", "ConcealedKong", TileSource.SelfDrawn)]
    [InlineData("Kong", "PromotedKong", TileSource.SelfDrawn)]
    [InlineData("Kong", "ExposedKong", TileSource.ClaimedDiscard)]
    [InlineData("Chow", null, TileSource.ClaimedDiscard)]
    [InlineData("Pung", null, TileSource.ClaimedDiscard)]
    public void Resolve_ReturnsExpectedTileSource(string meldType, string? meldKind, TileSource expected)
    {
        var result = ChangshaMeldSourceResolver.Resolve(meldType, meldKind);

        result.Should().Be(expected);
    }

    [Fact]
    public void Resolve_IgnoresCase_ForMeldTypeAndKind()
    {
        var result = ChangshaMeldSourceResolver.Resolve("kOnG", "pRoMoTeDkOnG");

        result.Should().Be(TileSource.SelfDrawn);
    }
}
