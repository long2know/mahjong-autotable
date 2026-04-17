using FluentAssertions;
using MahjongClaw.Domain.Gameplay;

namespace MahjongClaw.Tests;

public sealed class KongKindTests
{
    [Theory]
    [InlineData("ConcealedKong", KongKind.Concealed)]
    [InlineData("ExposedKong", KongKind.Exposed)]
    [InlineData("PromotedKong", KongKind.Promoted)]
    public void Parse_ReturnsCorrectKind_ForValidString(string input, KongKind expected)
    {
        var result = KongKindParser.Parse(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Parse_ThrowsException_ForInvalidString()
    {
        var action = () => KongKindParser.Parse("InvalidKong");
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(KongKind.Concealed, "ConcealedKong")]
    [InlineData(KongKind.Exposed, "ExposedKong")]
    [InlineData(KongKind.Promoted, "PromotedKong")]
    public void ToMeldTypeString_ReturnsCorrectString_ForKind(KongKind kind, string expected)
    {
        var result = KongKindParser.ToMeldTypeString(kind);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ConcealedKong", KongKind.Concealed, true)]
    [InlineData("ExposedKong", KongKind.Exposed, true)]
    [InlineData("PromotedKong", KongKind.Promoted, true)]
    [InlineData("InvalidKong", default(KongKind), false)]
    [InlineData("Kong", default(KongKind), false)]
    public void TryParse_HandlesValidAndInvalidStrings(string input, KongKind expectedKind, bool expectedSuccess)
    {
        var success = KongKindParser.TryParse(input, out var kind);
        
        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            kind.Should().Be(expectedKind);
        }
    }
}
