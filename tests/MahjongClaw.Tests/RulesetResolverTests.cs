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
    [InlineData("Changsha 2-5-8")]
    [InlineData("Changsha 2/5/8 pair")]
    [InlineData("Changsha 2-5-8 pair")]
    [InlineData("Changsha 258 (2/5/8 pair)")]
    [InlineData("changsha-258")]
    [InlineData("CS258")]
    [InlineData("CS-258")]
    [InlineData("CS 2/5/8 pair")]
    [InlineData("CS 2-5-8")]
    [InlineData("258")]
    [InlineData("258 pair")]
    [InlineData("2/5/8 pair")]
    [InlineData("2-5-8 pair")]
    public void Resolve_ReturnsChangsha258_WhenRulesetMatches(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("Changsha")]
    [InlineData("Changsha 2/5/8")]
    [InlineData("Changsha 2-5-8")]
    [InlineData("Changsha 2/5/8 pair")]
    [InlineData("Changsha 2-5-8 pair")]
    [InlineData("Changsha 258 (2/5/8 pair)")]
    [InlineData("changsha-258")]
    [InlineData("CS258")]
    [InlineData("CS-258")]
    [InlineData("CS 2/5/8 pair")]
    [InlineData("CS 2-5-8")]
    [InlineData("258")]
    [InlineData("258 pair")]
    [InlineData("2/5/8 pair")]
    [InlineData("2-5-8 pair")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetMatches(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void IsChangsha258_ReturnsFalse_WhenRulesetDoesNotMatch()
    {
        RulesetResolver.IsChangsha258("ChineseOfficial").Should().BeFalse();
    }

    [Theory]
    [InlineData("NotChangsha258")]
    [InlineData("NoChangsha258Rules")]
    [InlineData("XChangsha258")]
    public void IsChangsha258_ReturnsFalse_WhenRulesetDoesNotStartWithChangsha(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeFalse();
    }

    [Theory]
    [InlineData("NotChangsha258")]
    [InlineData("NoChangsha258Rules")]
    [InlineData("XChangsha258")]
    public void Resolve_ReturnsChineseOfficial_WhenRulesetDoesNotStartWithChangsha(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }
}
