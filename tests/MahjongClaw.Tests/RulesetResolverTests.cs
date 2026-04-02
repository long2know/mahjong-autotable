using FluentAssertions;
using MahjongClaw.Domain.Rules;

namespace MahjongClaw.Tests;

public class RulesetResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ReturnsChangsha258_WhenRulesetEmpty(string? ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("Changsha")]
    [InlineData("Changsha rules")]
    [InlineData("Changsha ruleset")]
    [InlineData("ChangshaRules")]
    [InlineData("ChangshaRuleset")]
    [InlineData("Changsha258")]
    [InlineData("Changsha 258")]
    [InlineData("Changsha 2/5/8")]
    [InlineData("Changsha 2-5-8")]
    [InlineData("Changsha 2 5 8")]
    [InlineData("Changsha 2/5/8 pair")]
    [InlineData("Changsha 2/5/8 pair, 108 tiles")]
    [InlineData("Changsha 2-5-8 pair")]
    [InlineData("Changsha 2 5 8 pair")]
    [InlineData("Changsha 258 (2/5/8 pair)")]
    [InlineData("Changsha 258 (2/5/8 pair, 108 tiles)")]
    [InlineData("Changsha 258 (108 tiles)")]
    [InlineData("Changsha 258 minimal")]
    [InlineData("Changsha 258 (no honors)")]
    [InlineData("Changsha no honors")]
    [InlineData("Changsha suited only")]
    [InlineData("changsha-258")]
    [InlineData("CS258")]
    [InlineData("CS-258")]
    [InlineData("CS 258")]
    [InlineData("CS 258 pair")]
    [InlineData("CS rules")]
    [InlineData("CS ruleset")]
    [InlineData("CS 2/5/8")]
    [InlineData("CS 2 5 8")]
    [InlineData("CS 2/5/8 pair")]
    [InlineData("CS 2 5 8 pair")]
    [InlineData("CS 2-5-8")]
    [InlineData("CS 2-5-8 pair")]
    [InlineData("258")]
    [InlineData("258 pair")]
    [InlineData("2 5 8")]
    [InlineData("2/5/8")]
    [InlineData("2-5-8")]
    [InlineData("2/5/8 pair")]
    [InlineData("2-5-8 pair")]
    [InlineData("Changsha 108 tiles")]
    [InlineData("Changsha108")]
    [InlineData("CS 108")]
    [InlineData("CS 108 tiles")]
    [InlineData("CS108")]
    [InlineData("108")]
    [InlineData("108 tiles")]
    [InlineData("Changsha 258 rules")]
    [InlineData("Changsha 2/5/8 rules")]
    [InlineData("Changsha 2-5-8 rules")]
    public void Resolve_ReturnsChangsha258_WhenRulesetMatches(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("Changsha")]
    [InlineData("Changsha rules")]
    [InlineData("Changsha ruleset")]
    [InlineData("ChangshaRules")]
    [InlineData("ChangshaRuleset")]
    [InlineData("Changsha 2/5/8")]
    [InlineData("Changsha 2-5-8")]
    [InlineData("Changsha 2 5 8")]
    [InlineData("Changsha 2/5/8 pair")]
    [InlineData("Changsha 2/5/8 pair, 108 tiles")]
    [InlineData("Changsha 2-5-8 pair")]
    [InlineData("Changsha 2 5 8 pair")]
    [InlineData("Changsha 258 (2/5/8 pair)")]
    [InlineData("Changsha 258 (2/5/8 pair, 108 tiles)")]
    [InlineData("Changsha 258 (108 tiles)")]
    [InlineData("Changsha 258 minimal")]
    [InlineData("Changsha 258 (no honors)")]
    [InlineData("Changsha no honors")]
    [InlineData("Changsha suited only")]
    [InlineData("changsha-258")]
    [InlineData("CS258")]
    [InlineData("CS-258")]
    [InlineData("CS 258")]
    [InlineData("CS 258 pair")]
    [InlineData("CS rules")]
    [InlineData("CS ruleset")]
    [InlineData("CS 2/5/8")]
    [InlineData("CS 2 5 8")]
    [InlineData("CS 2/5/8 pair")]
    [InlineData("CS 2 5 8 pair")]
    [InlineData("CS 2-5-8")]
    [InlineData("CS 2-5-8 pair")]
    [InlineData("258")]
    [InlineData("258 pair")]
    [InlineData("2 5 8")]
    [InlineData("2/5/8")]
    [InlineData("2-5-8")]
    [InlineData("2/5/8 pair")]
    [InlineData("2-5-8 pair")]
    [InlineData("Changsha 108 tiles")]
    [InlineData("Changsha108")]
    [InlineData("CS 108")]
    [InlineData("CS 108 tiles")]
    [InlineData("CS108")]
    [InlineData("108")]
    [InlineData("108 tiles")]
    [InlineData("Changsha 258 rules")]
    [InlineData("Changsha 2/5/8 rules")]
    [InlineData("Changsha 2-5-8 rules")]
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
    [InlineData("Changsha 2589")]
    [InlineData("Changsha 2580 pair")]
    [InlineData("CS2589")]
    [InlineData("2589")]
    public void IsChangsha258_ReturnsFalse_WhenDigitSequenceIsNot258(string ruleset)
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

    [Theory]
    [InlineData("Changsha 2589")]
    [InlineData("Changsha 2580 pair")]
    [InlineData("CS2589")]
    [InlineData("2589")]
    public void Resolve_ReturnsChineseOfficial_WhenDigitSequenceIsNot258(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }
}
