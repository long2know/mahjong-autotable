using FluentAssertions;
using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Tests;

public sealed class ChangshaFullyReliantDetectorTests
{
    [Fact]
    public void IsFullyReliant_ReturnsFalse_WhenWinningTileIsSelfDrawn()
    {
        var melds = new List<MeldInfo>
        {
            new MeldInfo(TileSource.ClaimedDiscard),
            new MeldInfo(TileSource.ClaimedDiscard),
            new MeldInfo(TileSource.ClaimedDiscard)
        };

        var result = ChangshaFullyReliantDetector.IsFullyReliant(melds, TileSource.SelfDrawn);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsFullyReliant_ReturnsTrue_WhenAllMeldsAndWinFromDiscards()
    {
        var melds = new List<MeldInfo>
        {
            new MeldInfo(TileSource.ClaimedDiscard),
            new MeldInfo(TileSource.ClaimedDiscard),
            new MeldInfo(TileSource.ClaimedDiscard)
        };

        var result = ChangshaFullyReliantDetector.IsFullyReliant(melds, TileSource.ClaimedDiscard);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsFullyReliant_ReturnsFalse_WhenAnyMeldIsSelfDrawn()
    {
        var melds = new List<MeldInfo>
        {
            new MeldInfo(TileSource.ClaimedDiscard),
            new MeldInfo(TileSource.SelfDrawn),
            new MeldInfo(TileSource.ClaimedDiscard)
        };

        var result = ChangshaFullyReliantDetector.IsFullyReliant(melds, TileSource.ClaimedDiscard);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsFullyReliant_ReturnsTrue_WhenNoMeldsButWinFromDiscard()
    {
        var melds = new List<MeldInfo>();

        var result = ChangshaFullyReliantDetector.IsFullyReliant(melds, TileSource.ClaimedDiscard);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsFullyReliant_ReturnsFalse_WhenNoMeldsAndSelfDrawnWin()
    {
        var melds = new List<MeldInfo>();

        var result = ChangshaFullyReliantDetector.IsFullyReliant(melds, TileSource.SelfDrawn);

        result.Should().BeFalse();
    }
}
