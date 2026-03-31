using FluentAssertions;
using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Tests;

public class RulesetResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ReturnsChineseOfficial_WhenRulesetEmpty(string? ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }

    [Theory]
    [InlineData("Changsha")]
    [InlineData("Changsha258")]
    [InlineData("Changsha 258")]
    [InlineData("Changsha 2/5/8")]
    [InlineData("changsha-258")]
    [InlineData("258")]
    public void Resolve_ReturnsChangsha258_WhenRulesetMatches(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsFalse_WhenRulesetDoesNotMatch()
    {
        RulesetResolver.IsChangsha258("ChineseOfficial").Should().BeFalse();
    }
}
