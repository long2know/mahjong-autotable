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
    [InlineData("Changsha default")]
    [InlineData("Changsha default ruleset")]
    [InlineData("Changsha default rules")]
    [InlineData("ChangshaRules")]
    [InlineData("ChangshaRuleset")]
    [InlineData("ChangshaDefault")]
    [InlineData("Changsha258")]
    [InlineData("Changsha 258")]
    [InlineData("Changsha 2/5/8")]
    [InlineData("Changsha 2-5-8")]
    [InlineData("Changsha 2 5 8")]
    [InlineData("Changsha 2.5.8")]
    [InlineData("Changsha 2/5/8 pair")]
    [InlineData("Changsha 2/5/8 pair required")]
    [InlineData("Changsha 2-5-8 pair required")]
    [InlineData("Changsha 2/5/8 pair, 108 tiles")]
    [InlineData("Changsha 2-5-8 pair")]
    [InlineData("Changsha 2 5 8 pair")]
    [InlineData("Changsha 258 (2/5/8 pair)")]
    [InlineData("Changsha 258 (2/5/8 pair, 108 tiles)")]
    [InlineData("Changsha 258 (108 tiles, 2/5/8 pair, default)")]
    [InlineData("Changsha 258 (108 tiles)")]
    [InlineData("Changsha 258 minimal")]
    [InlineData("Changsha258Minimal")]
    [InlineData("Changsha minimal")]
    [InlineData("Changsha mini")]
    [InlineData("Changsha 258 (no honors)")]
    [InlineData("Changsha no honor")]
    [InlineData("Changsha no honors")]
    [InlineData("Changsha no honour")]
    [InlineData("Changsha no honours")]
    [InlineData("Changsha no flower")]
    [InlineData("Changsha no flowers")]
    [InlineData("Changsha no season")]
    [InlineData("Changsha no seasons")]
    [InlineData("Changsha no bonus")]
    [InlineData("Changsha no bonuses")]
    [InlineData("Changsha without honor")]
    [InlineData("Changsha without honors")]
    [InlineData("Changsha without honour")]
    [InlineData("Changsha without honours")]
    [InlineData("Changsha without flower")]
    [InlineData("Changsha without flowers")]
    [InlineData("Changsha without season")]
    [InlineData("Changsha without seasons")]
    [InlineData("Changsha without bonus")]
    [InlineData("Changsha without bonuses")]
    [InlineData("Changsha suited")]
    [InlineData("Changsha suited only")]
    [InlineData("changsha-258")]
    [InlineData("CS258")]
    [InlineData("CS-258")]
    [InlineData("CS 258")]
    [InlineData("CS 258 pair")]
    [InlineData("CS rules")]
    [InlineData("CS ruleset")]
    [InlineData("CS no honor")]
    [InlineData("CS no honors")]
    [InlineData("CS no honour")]
    [InlineData("CS no honours")]
    [InlineData("CS no flower")]
    [InlineData("CS no flowers")]
    [InlineData("CS no season")]
    [InlineData("CS no seasons")]
    [InlineData("CS no bonus")]
    [InlineData("CS no bonuses")]
    [InlineData("CS without honor")]
    [InlineData("CS without honors")]
    [InlineData("CS without honour")]
    [InlineData("CS without honours")]
    [InlineData("CS without flower")]
    [InlineData("CS without flowers")]
    [InlineData("CS without season")]
    [InlineData("CS without seasons")]
    [InlineData("CS without bonus")]
    [InlineData("CS without bonuses")]
    [InlineData("CS suited")]
    [InlineData("CS suited only")]
    [InlineData("CS default")]
    [InlineData("CS default rules")]
    [InlineData("CS default ruleset")]
    [InlineData("CSDefault")]
    [InlineData("CS minimal")]
    [InlineData("CS258Minimal")]
    [InlineData("CS mini")]
    [InlineData("CS 2/5/8")]
    [InlineData("CS 2 5 8")]
    [InlineData("CS 2.5.8")]
    [InlineData("CS 2/5/8 pair")]
    [InlineData("CS 2/5/8 pair required")]
    [InlineData("CS 2-5-8 pair required")]
    [InlineData("CS 2 5 8 pair")]
    [InlineData("CS 2-5-8")]
    [InlineData("CS 2-5-8 pair")]
    [InlineData("258")]
    [InlineData("258 pair")]
    [InlineData("2 5 8")]
    [InlineData("2.5.8")]
    [InlineData("2/5/8")]
    [InlineData("2-5-8")]
    [InlineData("2/5/8 pair")]
    [InlineData("2 5 8 pair")]
    [InlineData("2/5/8 pair required")]
    [InlineData("2-5-8 pair required")]
    [InlineData("2-5-8 pair")]
    [InlineData("Changsha 108 tiles")]
    [InlineData("Changsha-108")]
    [InlineData("Changsha 108/258")]
    [InlineData("Changsha 108 2/5/8 pair")]
    [InlineData("Changsha 258/108/258")]
    [InlineData("CS 258/108/258")]
    [InlineData("Changsha108")]
    [InlineData("CS 108")]
    [InlineData("CS-108")]
    [InlineData("CS 108 tiles")]
    [InlineData("CS 108/258")]
    [InlineData("CS 108 2-5-8 pair")]
    [InlineData("CS108")]
    [InlineData("108")]
    [InlineData("108 tiles")]
    [InlineData("108-tile")]
    [InlineData("108/258")]
    [InlineData("Changsha 258 rules")]
    [InlineData("Changsha 258 ruleset")]
    [InlineData("Changsha 258 default")]
    [InlineData("Changsha 258 default ruleset")]
    [InlineData("CS 258 default")]
    [InlineData("Changsha 2/5/8 rules")]
    [InlineData("Changsha 2-5-8 rules")]
    [InlineData("CS 258 ruleset")]
    [InlineData("CS 258 default ruleset")]
    [InlineData("ChangshaMinimal")]
    [InlineData("CSRules")]
    [InlineData("CSRuleset")]
    [InlineData("CSMinimal")]
    [InlineData("Changsha rules default")]
    [InlineData("CS rules default")]
    public void Resolve_ReturnsChangsha258_WhenRulesetMatches(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("Changsha")]
    [InlineData("Changsha rules")]
    [InlineData("Changsha ruleset")]
    [InlineData("Changsha default")]
    [InlineData("Changsha default ruleset")]
    [InlineData("Changsha default rules")]
    [InlineData("ChangshaRules")]
    [InlineData("ChangshaRuleset")]
    [InlineData("ChangshaDefault")]
    [InlineData("Changsha 2/5/8")]
    [InlineData("Changsha 2-5-8")]
    [InlineData("Changsha 2 5 8")]
    [InlineData("Changsha 2.5.8")]
    [InlineData("Changsha 2/5/8 pair")]
    [InlineData("Changsha 2/5/8 pair required")]
    [InlineData("Changsha 2-5-8 pair required")]
    [InlineData("Changsha 2/5/8 pair, 108 tiles")]
    [InlineData("Changsha 2-5-8 pair")]
    [InlineData("Changsha 2 5 8 pair")]
    [InlineData("Changsha 258 (2/5/8 pair)")]
    [InlineData("Changsha 258 (2/5/8 pair, 108 tiles)")]
    [InlineData("Changsha 258 (108 tiles, 2/5/8 pair, default)")]
    [InlineData("Changsha 258 (108 tiles)")]
    [InlineData("Changsha 258 minimal")]
    [InlineData("Changsha258Minimal")]
    [InlineData("Changsha minimal")]
    [InlineData("Changsha mini")]
    [InlineData("Changsha 258 (no honors)")]
    [InlineData("Changsha no honor")]
    [InlineData("Changsha no honors")]
    [InlineData("Changsha no honour")]
    [InlineData("Changsha no honours")]
    [InlineData("Changsha no flower")]
    [InlineData("Changsha no flowers")]
    [InlineData("Changsha no season")]
    [InlineData("Changsha no seasons")]
    [InlineData("Changsha no bonus")]
    [InlineData("Changsha no bonuses")]
    [InlineData("Changsha without honor")]
    [InlineData("Changsha without honors")]
    [InlineData("Changsha without honour")]
    [InlineData("Changsha without honours")]
    [InlineData("Changsha without flower")]
    [InlineData("Changsha without flowers")]
    [InlineData("Changsha without season")]
    [InlineData("Changsha without seasons")]
    [InlineData("Changsha without bonus")]
    [InlineData("Changsha without bonuses")]
    [InlineData("Changsha suited")]
    [InlineData("Changsha suited only")]
    [InlineData("changsha-258")]
    [InlineData("CS258")]
    [InlineData("CS-258")]
    [InlineData("CS 258")]
    [InlineData("CS 258 pair")]
    [InlineData("CS rules")]
    [InlineData("CS ruleset")]
    [InlineData("CS no honor")]
    [InlineData("CS no honors")]
    [InlineData("CS no honour")]
    [InlineData("CS no honours")]
    [InlineData("CS no flower")]
    [InlineData("CS no flowers")]
    [InlineData("CS no season")]
    [InlineData("CS no seasons")]
    [InlineData("CS no bonus")]
    [InlineData("CS no bonuses")]
    [InlineData("CS without honor")]
    [InlineData("CS without honors")]
    [InlineData("CS without honour")]
    [InlineData("CS without honours")]
    [InlineData("CS without flower")]
    [InlineData("CS without flowers")]
    [InlineData("CS without season")]
    [InlineData("CS without seasons")]
    [InlineData("CS without bonus")]
    [InlineData("CS without bonuses")]
    [InlineData("CS suited")]
    [InlineData("CS suited only")]
    [InlineData("CS default")]
    [InlineData("CS default rules")]
    [InlineData("CS default ruleset")]
    [InlineData("CSDefault")]
    [InlineData("CS minimal")]
    [InlineData("CS258Minimal")]
    [InlineData("CS mini")]
    [InlineData("CS 2/5/8")]
    [InlineData("CS 2 5 8")]
    [InlineData("CS 2.5.8")]
    [InlineData("CS 2/5/8 pair")]
    [InlineData("CS 2/5/8 pair required")]
    [InlineData("CS 2-5-8 pair required")]
    [InlineData("CS 2 5 8 pair")]
    [InlineData("CS 2-5-8")]
    [InlineData("CS 2-5-8 pair")]
    [InlineData("258")]
    [InlineData("258 pair")]
    [InlineData("2 5 8")]
    [InlineData("2.5.8")]
    [InlineData("2/5/8")]
    [InlineData("2-5-8")]
    [InlineData("2/5/8 pair")]
    [InlineData("2 5 8 pair")]
    [InlineData("2/5/8 pair required")]
    [InlineData("2-5-8 pair required")]
    [InlineData("2-5-8 pair")]
    [InlineData("Changsha 108 tiles")]
    [InlineData("Changsha-108")]
    [InlineData("Changsha 108/258")]
    [InlineData("Changsha 108 2/5/8 pair")]
    [InlineData("Changsha 258/108/258")]
    [InlineData("CS 258/108/258")]
    [InlineData("Changsha108")]
    [InlineData("CS 108")]
    [InlineData("CS-108")]
    [InlineData("CS 108 tiles")]
    [InlineData("CS 108/258")]
    [InlineData("CS 108 2-5-8 pair")]
    [InlineData("CS108")]
    [InlineData("108")]
    [InlineData("108 tiles")]
    [InlineData("108-tile")]
    [InlineData("108/258")]
    [InlineData("Changsha 258 rules")]
    [InlineData("Changsha 258 ruleset")]
    [InlineData("Changsha 258 default")]
    [InlineData("Changsha 258 default ruleset")]
    [InlineData("CS 258 default")]
    [InlineData("Changsha 2/5/8 rules")]
    [InlineData("Changsha 2-5-8 rules")]
    [InlineData("CS 258 ruleset")]
    [InlineData("CS 258 default ruleset")]
    [InlineData("ChangshaMinimal")]
    [InlineData("CSRules")]
    [InlineData("CSRuleset")]
    [InlineData("CSMinimal")]
    [InlineData("Changsha rules default")]
    [InlineData("CS rules default")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetMatches(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetEmpty(string? ruleset)
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
    [InlineData("Changsha suited 2589")]
    [InlineData("Changsha no honors 2580")]
    [InlineData("Changsha 258 999")]
    [InlineData("CS 108 258 777")]
    [InlineData("minimal 258 ruleset variant 999")]
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
    [InlineData("Changsha suited 2589")]
    [InlineData("Changsha no honors 2580")]
    [InlineData("Changsha 258 999")]
    [InlineData("CS 108 258 777")]
    [InlineData("minimal 258 ruleset variant 999")]
    [InlineData("CS2589")]
    [InlineData("2589")]
    public void Resolve_ReturnsChineseOfficial_WhenDigitSequenceIsNot258(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }

    [Theory]
    [InlineData("Changsha two five eight")]
    [InlineData("Changsha two five eight pair")]
    [InlineData("Changsha two five eight pair required")]
    [InlineData("Changsha two five eight small win")]
    [InlineData("Changsha two five eight small win pair")]
    [InlineData("Changsha two five eight small win pair required")]
    [InlineData("CS two five eight")]
    [InlineData("CS two five eight pair required")]
    [InlineData("CS two five eight small win")]
    [InlineData("CS two five eight small win pair required")]
    [InlineData("Two five eight")]
    [InlineData("Two five eight pair required")]
    [InlineData("Two five eight small win")]
    [InlineData("Two five eight small win pair required")]
    [InlineData("er wu ba")]
    [InlineData("er wu ba pair")]
    [InlineData("er wu ba pair required")]
    [InlineData("er wu ba small win")]
    [InlineData("er wu ba small win pair required")]
    [InlineData("xiao hu")]
    [InlineData("xiao hu pair required")]
    [InlineData("small hu")]
    [InlineData("small hu pair required")]
    [InlineData("small win")]
    [InlineData("small win pair required")]
    [InlineData("Changsha small win")]
    [InlineData("CS small win pair required")]
    [InlineData("Changsha xiao hu")]
    [InlineData("CS small hu pair required")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesWorded258Alias(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("Changsha two five eight")]
    [InlineData("Changsha two five eight pair")]
    [InlineData("Changsha two five eight pair required")]
    [InlineData("Changsha two five eight small win")]
    [InlineData("Changsha two five eight small win pair")]
    [InlineData("Changsha two five eight small win pair required")]
    [InlineData("CS two five eight")]
    [InlineData("CS two five eight pair required")]
    [InlineData("CS two five eight small win")]
    [InlineData("CS two five eight small win pair required")]
    [InlineData("Two five eight")]
    [InlineData("Two five eight pair required")]
    [InlineData("Two five eight small win")]
    [InlineData("Two five eight small win pair required")]
    [InlineData("er wu ba")]
    [InlineData("er wu ba pair")]
    [InlineData("er wu ba pair required")]
    [InlineData("er wu ba small win")]
    [InlineData("er wu ba small win pair required")]
    [InlineData("xiao hu")]
    [InlineData("xiao hu pair required")]
    [InlineData("small hu")]
    [InlineData("small hu pair required")]
    [InlineData("small win")]
    [InlineData("small win pair required")]
    [InlineData("Changsha small win")]
    [InlineData("CS small win pair required")]
    [InlineData("Changsha xiao hu")]
    [InlineData("CS small hu pair required")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesWorded258Alias(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("长沙")]
    [InlineData("长沙 258")]
    [InlineData("二五八")]
    [InlineData("二五八将")]
    [InlineData("二五八将对")]
    [InlineData("小胡")]
    [InlineData("小胡将")]
    [InlineData("小胡将对")]
    [InlineData("长沙 二五八")]
    [InlineData("长沙 二五八 规则")]
    [InlineData("二五八 规则")]
    [InlineData("二五八 规则集")]
    [InlineData("长沙 小胡")]
    [InlineData("长沙 小胡将对")]
    [InlineData("长沙 小胡 规则")]
    [InlineData("小胡 规则")]
    [InlineData("长沙 258 规则")]
    [InlineData("长沙 258 规则集")]
    [InlineData("长沙 108 牌")]
    [InlineData("Changsha 二五八")]
    [InlineData("Changsha 小胡将对")]
    [InlineData("CS 二五八 规则")]
    [InlineData("CS 小胡")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesChineseAliases(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("长沙")]
    [InlineData("长沙 258")]
    [InlineData("二五八")]
    [InlineData("二五八将")]
    [InlineData("二五八将对")]
    [InlineData("小胡")]
    [InlineData("小胡将")]
    [InlineData("小胡将对")]
    [InlineData("长沙 二五八")]
    [InlineData("长沙 二五八 规则")]
    [InlineData("二五八 规则")]
    [InlineData("二五八 规则集")]
    [InlineData("长沙 小胡")]
    [InlineData("长沙 小胡将对")]
    [InlineData("长沙 小胡 规则")]
    [InlineData("小胡 规则")]
    [InlineData("长沙 258 规则")]
    [InlineData("长沙 258 规则集")]
    [InlineData("长沙 108 牌")]
    [InlineData("Changsha 二五八")]
    [InlineData("Changsha 小胡将对")]
    [InlineData("CS 二五八 规则")]
    [InlineData("CS 小胡")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesChineseAliases(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("長沙")]
    [InlineData("長沙 258")]
    [InlineData("長沙 二五八")]
    [InlineData("長沙 小胡")]
    [InlineData("長沙 108 牌")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesTraditionalChineseChangshaAliases(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("長沙")]
    [InlineData("長沙 258")]
    [InlineData("長沙 二五八")]
    [InlineData("長沙 小胡")]
    [InlineData("長沙 108 牌")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesTraditionalChineseChangshaAliases(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("Changsha two five eight nine")]
    [InlineData("CS two five eight nine")]
    [InlineData("Two five eight nine")]
    [InlineData("Changsha two five eight one")]
    [InlineData("CS two five eight ten")]
    [InlineData("er wu ba jiu")]
    [InlineData("Changsha er wu ba yi")]
    [InlineData("CS er wu ba shi")]
    [InlineData("Changsha rules two five eight nine")]
    [InlineData("CS default er wu ba jiu")]
    [InlineData("xiao hu nine gates")]
    [InlineData("small hu nine gates")]
    [InlineData("small win nine gates")]
    [InlineData("二五八九")]
    [InlineData("长沙二五八九")]
    [InlineData("長沙二五八九")]
    [InlineData("小胡九莲")]
    [InlineData("Changsha 二五八九")]
    [InlineData("CS 小胡九莲")]
    public void Resolve_ReturnsChineseOfficial_WhenWordedDigitSequenceIsNot258(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }

    [Theory]
    [InlineData("Changsha two five eight nine")]
    [InlineData("CS two five eight nine")]
    [InlineData("Two five eight nine")]
    [InlineData("Changsha two five eight one")]
    [InlineData("CS two five eight ten")]
    [InlineData("er wu ba jiu")]
    [InlineData("Changsha er wu ba yi")]
    [InlineData("CS er wu ba shi")]
    [InlineData("Changsha rules two five eight nine")]
    [InlineData("CS default er wu ba jiu")]
    [InlineData("xiao hu nine gates")]
    [InlineData("small hu nine gates")]
    [InlineData("small win nine gates")]
    [InlineData("minimal two five eight nine")]
    [InlineData("minimal two five eight zero")]
    [InlineData("minimal er wu ba jiu")]
    [InlineData("minimal er wu ba yi")]
    [InlineData("二五八九")]
    [InlineData("长沙二五八九")]
    [InlineData("長沙二五八九")]
    [InlineData("小胡九莲")]
    [InlineData("Changsha 二五八九")]
    [InlineData("CS 小胡九莲")]
    public void IsChangsha258_ReturnsFalse_WhenWordedDigitSequenceIsNot258(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeFalse();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant")]
    [InlineData("minimal changsha 258 ruleset")]
    [InlineData("mini changsha 258")]
    [InlineData("minimal cs 258 ruleset")]
    [InlineData("mini cs 258")]
    [InlineData("minimal 258 ruleset variant")]
    [InlineData("mini 258")]
    [InlineData("minimal two five eight ruleset variant")]
    [InlineData("mini two five eight")]
    [InlineData("minimal er wu ba ruleset variant")]
    [InlineData("mini er wu ba")]
    [InlineData("minimal small win")]
    [InlineData("mini small win pair required")]
    [InlineData("minimal xiao hu")]
    [InlineData("mini small hu pair required")]
    [InlineData("minimal 108 tiles variant")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesMinimalPrefixWithChangshaOr258Signals(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant")]
    [InlineData("minimal changsha 258 ruleset")]
    [InlineData("mini changsha 258")]
    [InlineData("minimal cs 258 ruleset")]
    [InlineData("mini cs 258")]
    [InlineData("minimal 258 ruleset variant")]
    [InlineData("mini 258")]
    [InlineData("minimal two five eight ruleset variant")]
    [InlineData("mini two five eight")]
    [InlineData("minimal er wu ba ruleset variant")]
    [InlineData("mini er wu ba")]
    [InlineData("minimal small win")]
    [InlineData("mini small win pair required")]
    [InlineData("minimal xiao hu")]
    [InlineData("mini small hu pair required")]
    [InlineData("minimal 108 tiles variant")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesMinimalPrefixWithChangshaOr258Signals(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal #258 ruleset variant")]
    [InlineData("mini #108 tiles variant")]
    [InlineData("minimal ＃２５８ ruleset variant")]
    [InlineData("mini ＃１０８ tiles variant")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesSemanticHashtagDigits(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal #258 ruleset variant")]
    [InlineData("mini #108 tiles variant")]
    [InlineData("minimal ＃２５８ ruleset variant")]
    [InlineData("mini ＃１０８ tiles variant")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesSemanticHashtagDigits(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (per TASKS.md #1)")]
    [InlineData("minimal 258 (changsha) ruleset variant (follow-up PR #329)")]
    [InlineData("minimal 258 (changsha) ruleset variant (per TASKS.md ＃1)")]
    [InlineData("minimal 258 (changsha) ruleset variant（follow-up PR ＃３２９）")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsHashtagNumberReference(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (per TASKS.md #1)")]
    [InlineData("minimal 258 (changsha) ruleset variant (follow-up PR #329)")]
    [InlineData("minimal 258 (changsha) ruleset variant (per TASKS.md ＃1)")]
    [InlineData("minimal 258 (changsha) ruleset variant（follow-up PR ＃３２９）")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsHashtagNumberReference(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (per TASKS.md 1)")]
    [InlineData("minimal 258 (changsha) ruleset variant (follow-up PR 329)")]
    [InlineData("minimal 258 (changsha) ruleset variant（follow-up PR：３２９）")]
    [InlineData("minimal 258 (changsha) ruleset variant (follow-up pull-request 329)")]
    [InlineData("minimal 258 (changsha) ruleset variant (task 1)")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsNamedNumberReferenceWithoutHashtag(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (per TASKS.md 1)")]
    [InlineData("minimal 258 (changsha) ruleset variant (follow-up PR 329)")]
    [InlineData("minimal 258 (changsha) ruleset variant（follow-up PR：３２９）")]
    [InlineData("minimal 258 (changsha) ruleset variant (follow-up pull-request 329)")]
    [InlineData("minimal 258 (changsha) ruleset variant (task 1)")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsNamedNumberReferenceWithoutHashtag(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (<=5 commits, single theme)")]
    [InlineData("minimal 258 (changsha) ruleset variant <=5 commits")]
    [InlineData("minimal 258 (changsha) ruleset variant （≤５ commits）")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsCommitScopeMetadata(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (<=5 commits, single theme)")]
    [InlineData("minimal 258 (changsha) ruleset variant <=5 commits")]
    [InlineData("minimal 258 (changsha) ruleset variant （≤５ commits）")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsCommitScopeMetadata(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (2026-04-04 08:21 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (2026/04/04 01:21 PT)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 01:21 AM)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 04:21)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 16:21)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 08：21 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Saturday, April 4th, 2026 — 2:01 AM (America/Los_Angeles) / 2026-04-04 09:01 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Sat, Apr 4, 2026 2:01 AM PT)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Saturday, 4 April 2026 — 2:01 AM (America/Los_Angeles) / 2026-04-04 09:01 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Sat, 4 Apr 2026 2:01 AM PT)")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsTemporalMetadata(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (2026-04-04 08:21 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (2026/04/04 01:21 PT)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 01:21 AM)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 04:21)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 16:21)")]
    [InlineData("minimal 258 (changsha) ruleset variant (check-in 08：21 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Saturday, April 4th, 2026 — 2:01 AM (America/Los_Angeles) / 2026-04-04 09:01 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Sat, Apr 4, 2026 2:01 AM PT)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Saturday, 4 April 2026 — 2:01 AM (America/Los_Angeles) / 2026-04-04 09:01 UTC)")]
    [InlineData("minimal 258 (changsha) ruleset variant (Current time: Sat, 4 Apr 2026 2:01 AM PT)")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsTemporalMetadata(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("[cron:aac88361-c047-4527-8bef-737045ae8702] minimal 258 (changsha) ruleset variant")]
    [InlineData("[cron aac88361-c047-4527-8bef-737045ae8702] minimal 258 (changsha) ruleset variant")]
    [InlineData("[cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] minimal 258 (changsha) ruleset variant for channel 1474176085838004255")]
    [InlineData("[cron aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] minimal 258 (changsha) ruleset variant for channel 1474176085838004255")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsCronAndChannelMetadata(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("[cron:aac88361-c047-4527-8bef-737045ae8702] minimal 258 (changsha) ruleset variant")]
    [InlineData("[cron aac88361-c047-4527-8bef-737045ae8702] minimal 258 (changsha) ruleset variant")]
    [InlineData("[cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] minimal 258 (changsha) ruleset variant for channel 1474176085838004255")]
    [InlineData("[cron aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] minimal 258 (changsha) ruleset variant for channel 1474176085838004255")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsCronAndChannelMetadata(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant for channel-id 1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channel_id=1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channelid:1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channelid＝1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channel-id＝１４７４１７６０８５８３８００４２５５")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsChannelIdMetadataVariants(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant for channel-id 1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channel_id=1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channelid:1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channelid＝1474176085838004255")]
    [InlineData("minimal 258 (changsha) ruleset variant for channel-id＝１４７４１７６０８５８３８００４２５５")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsChannelIdMetadataVariants(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("[cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.")]
    [InlineData("[cron aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.")]
    [InlineData("Autonomous Mahjong execution — TOP PRIORITY implement minimal 258 (Changsha) ruleset variant <=5 commits")]
    public void Resolve_ReturnsChangsha258_WhenSingleLineInstructionPrefixPrecedesRulesetHint(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant <@1474176085838004255>")]
    [InlineData("minimal 258 (changsha) ruleset variant <@!1474176085838004255>")]
    [InlineData("minimal 258 (changsha) ruleset variant <@&1474176085838004255>")]
    [InlineData("minimal 258 (changsha) ruleset variant </mahjong:1474176085838004255>")]
    public void Resolve_ReturnsChangsha258_WhenRulesetContainsDiscordMentionMetadata(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("[cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.")]
    [InlineData("[cron aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.")]
    [InlineData("Autonomous Mahjong execution — TOP PRIORITY implement minimal 258 (Changsha) ruleset variant <=5 commits")]
    public void IsChangsha258_ReturnsTrue_WhenSingleLineInstructionPrefixPrecedesRulesetHint(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant <@1474176085838004255>")]
    [InlineData("minimal 258 (changsha) ruleset variant <@!1474176085838004255>")]
    [InlineData("minimal 258 (changsha) ruleset variant <@&1474176085838004255>")]
    [InlineData("minimal 258 (changsha) ruleset variant </mahjong:1474176085838004255>")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetContainsDiscordMentionMetadata(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetIsEmbeddedInCronReminderBody()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.

            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.

            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            Current time: Saturday, April 4th, 2026 — 2:21 AM (America/Los_Angeles) / 2026-04-04 09:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetIsEmbeddedInCronReminderBody()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.

            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.

            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            Current time: Saturday, April 4th, 2026 — 2:21 AM (America/Los_Angeles) / 2026-04-04 09:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetIsEmbeddedInCronReminderBodyWithStatusTemplateInstructions()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.

            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.

            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            Current time: Saturday, April 4th, 2026 — 5:41 AM (America/Los_Angeles) / 2026-04-04 12:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetIsEmbeddedInCronReminderBodyWithStatusTemplateInstructions()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.

            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.

            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            Current time: Saturday, April 4th, 2026 — 5:41 AM (America/Los_Angeles) / 2026-04-04 12:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyIsRepeatedWithCurrentTimeMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.

            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            Current time: Saturday, April 4th, 2026 — 6:01 AM (America/Los_Angeles) / 2026-04-04 13:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyIsRepeatedWithCurrentTimeMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.

            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            Current time: Saturday, April 4th, 2026 — 6:01 AM (America/Los_Angeles) / 2026-04-04 13:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyRepeatsWithPostMergeAndVisualParityInstructions()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".

            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:41 AM (America/Los_Angeles) / 2026-04-04 13:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyRepeatsWithPostMergeAndVisualParityInstructions()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".

            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:41 AM (America/Los_Angeles) / 2026-04-04 13:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:01 AM (America/Los_Angeles) / 2026-04-04 14:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:01 AM (America/Los_Angeles) / 2026-04-04 14:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:21 AM (America/Los_Angeles) / 2026-04-04 14:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:21 AM (America/Los_Angeles) / 2026-04-04 14:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:41 AM (America/Los_Angeles) / 2026-04-04 14:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:41 AM (America/Los_Angeles) / 2026-04-04 14:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:01 AM (America/Los_Angeles) / 2026-04-04 15:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:01 AM (America/Los_Angeles) / 2026-04-04 15:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:21 AM (America/Los_Angeles) / 2026-04-04 15:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:21 AM (America/Los_Angeles) / 2026-04-04 15:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:41 AM (America/Los_Angeles) / 2026-04-04 15:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:41 AM (America/Los_Angeles) / 2026-04-04 15:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:01 AM (America/Los_Angeles) / 2026-04-04 16:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:01 AM (America/Los_Angeles) / 2026-04-04 16:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:21 AM (America/Los_Angeles) / 2026-04-04 16:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:21 AM (America/Los_Angeles) / 2026-04-04 16:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:41 AM (America/Los_Angeles) / 2026-04-04 16:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:41 AM (America/Los_Angeles) / 2026-04-04 16:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:01 AM (America/Los_Angeles) / 2026-04-04 17:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:01 AM (America/Los_Angeles) / 2026-04-04 17:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:21 AM (America/Los_Angeles) / 2026-04-04 17:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:21 AM (America/Los_Angeles) / 2026-04-04 17:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:41 AM (America/Los_Angeles) / 2026-04-04 17:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:41 AM (America/Los_Angeles) / 2026-04-04 17:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:01 AM (America/Los_Angeles) / 2026-04-04 18:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:01 AM (America/Los_Angeles) / 2026-04-04 18:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:21 AM (America/Los_Angeles) / 2026-04-04 18:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:21 AM (America/Los_Angeles) / 2026-04-04 18:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:41 AM (America/Los_Angeles) / 2026-04-04 18:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:41 AM (America/Los_Angeles) / 2026-04-04 18:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 12:01 PM (America/Los_Angeles) / 2026-04-04 19:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 12:01 PM (America/Los_Angeles) / 2026-04-04 19:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 12:21 PM (America/Los_Angeles) / 2026-04-04 19:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 12:21 PM (America/Los_Angeles) / 2026-04-04 19:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 12:41 PM (America/Los_Angeles) / 2026-04-04 19:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 12:41 PM (America/Los_Angeles) / 2026-04-04 19:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 1:01 PM (America/Los_Angeles) / 2026-04-04 20:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 1:01 PM (America/Los_Angeles) / 2026-04-04 20:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 1:21 PM (America/Los_Angeles) / 2026-04-04 20:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 1:21 PM (America/Los_Angeles) / 2026-04-04 20:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 1:41 PM (America/Los_Angeles) / 2026-04-04 20:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 1:41 PM (America/Los_Angeles) / 2026-04-04 20:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 2:01 PM (America/Los_Angeles) / 2026-04-04 21:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 2:01 PM (America/Los_Angeles) / 2026-04-04 21:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 2:21 PM (America/Los_Angeles) / 2026-04-04 21:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 2:21 PM (America/Los_Angeles) / 2026-04-04 21:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 2:41 PM (America/Los_Angeles) / 2026-04-04 21:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 2:41 PM (America/Los_Angeles) / 2026-04-04 21:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 3:01 PM (America/Los_Angeles) / 2026-04-04 22:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 3:01 PM (America/Los_Angeles) / 2026-04-04 22:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 3:21 PM (America/Los_Angeles) / 2026-04-04 22:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 3:21 PM (America/Los_Angeles) / 2026-04-04 22:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 3:41 PM (America/Los_Angeles) / 2026-04-04 22:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 3:41 PM (America/Los_Angeles) / 2026-04-04 22:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 4:01 PM (America/Los_Angeles) / 2026-04-04 23:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 4:01 PM (America/Los_Angeles) / 2026-04-04 23:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 4:21 PM (America/Los_Angeles) / 2026-04-04 23:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 4:21 PM (America/Los_Angeles) / 2026-04-04 23:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 4:41 PM (America/Los_Angeles) / 2026-04-04 23:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 4:41 PM (America/Los_Angeles) / 2026-04-04 23:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 5:01 PM (America/Los_Angeles) / 2026-04-05 00:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 5:01 PM (America/Los_Angeles) / 2026-04-05 00:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 5:21 PM (America/Los_Angeles) / 2026-04-05 00:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 5:21 PM (America/Los_Angeles) / 2026-04-05 00:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 5:41 PM (America/Los_Angeles) / 2026-04-05 00:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 5:41 PM (America/Los_Angeles) / 2026-04-05 00:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:01 PM (America/Los_Angeles) / 2026-04-05 01:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixOhOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:01 PM (America/Los_Angeles) / 2026-04-05 01:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:21 PM (America/Los_Angeles) / 2026-04-05 01:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixTwentyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:21 PM (America/Los_Angeles) / 2026-04-05 01:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:41 PM (America/Los_Angeles) / 2026-04-05 01:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixFortyOneTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 6:41 PM (America/Los_Angeles) / 2026-04-05 01:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:01 PM (America/Los_Angeles) / 2026-04-05 02:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:01 PM (America/Los_Angeles) / 2026-04-05 02:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:21 PM (America/Los_Angeles) / 2026-04-05 02:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:21 PM (America/Los_Angeles) / 2026-04-05 02:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:41 PM (America/Los_Angeles) / 2026-04-05 02:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 7:41 PM (America/Los_Angeles) / 2026-04-05 02:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:01 PM (America/Los_Angeles) / 2026-04-05 03:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:01 PM (America/Los_Angeles) / 2026-04-05 03:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:21 PM (America/Los_Angeles) / 2026-04-05 03:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:21 PM (America/Los_Angeles) / 2026-04-05 03:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:41 PM (America/Los_Angeles) / 2026-04-05 03:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 8:41 PM (America/Los_Angeles) / 2026-04-05 03:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:01 PM (America/Los_Angeles) / 2026-04-05 04:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:01 PM (America/Los_Angeles) / 2026-04-05 04:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:21 PM (America/Los_Angeles) / 2026-04-05 04:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:21 PM (America/Los_Angeles) / 2026-04-05 04:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:41 PM (America/Los_Angeles) / 2026-04-05 04:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 9:41 PM (America/Los_Angeles) / 2026-04-05 04:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:01 PM (America/Los_Angeles) / 2026-04-05 05:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:01 PM (America/Los_Angeles) / 2026-04-05 05:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:21 PM (America/Los_Angeles) / 2026-04-05 05:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:21 PM (America/Los_Angeles) / 2026-04-05 05:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:41 PM (America/Los_Angeles) / 2026-04-05 05:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 10:41 PM (America/Los_Angeles) / 2026-04-05 05:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:01 PM (America/Los_Angeles) / 2026-04-05 06:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:01 PM (America/Los_Angeles) / 2026-04-05 06:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:21 PM (America/Los_Angeles) / 2026-04-05 06:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:21 PM (America/Los_Angeles) / 2026-04-05 06:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:41 PM (America/Los_Angeles) / 2026-04-05 06:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Saturday, April 4th, 2026 — 11:41 PM (America/Los_Angeles) / 2026-04-05 06:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:01 AM (America/Los_Angeles) / 2026-04-05 07:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:01 AM (America/Los_Angeles) / 2026-04-05 07:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:21 AM (America/Los_Angeles) / 2026-04-05 07:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:21 AM (America/Los_Angeles) / 2026-04-05 07:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:41 AM (America/Los_Angeles) / 2026-04-05 07:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:41 AM (America/Los_Angeles) / 2026-04-05 07:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:01 AM (America/Los_Angeles) / 2026-04-05 08:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:01 AM (America/Los_Angeles) / 2026-04-05 08:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:21 AM (America/Los_Angeles) / 2026-04-05 08:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:21 AM (America/Los_Angeles) / 2026-04-05 08:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:41 AM (America/Los_Angeles) / 2026-04-05 08:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:41 AM (America/Los_Angeles) / 2026-04-05 08:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:01 AM (America/Los_Angeles) / 2026-04-05 09:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:01 AM (America/Los_Angeles) / 2026-04-05 09:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:21 AM (America/Los_Angeles) / 2026-04-05 09:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:21 AM (America/Los_Angeles) / 2026-04-05 09:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:41 AM (America/Los_Angeles) / 2026-04-05 09:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:41 AM (America/Los_Angeles) / 2026-04-05 09:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:01 AM (America/Los_Angeles) / 2026-04-05 10:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:01 AM (America/Los_Angeles) / 2026-04-05 10:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:21 AM (America/Los_Angeles) / 2026-04-05 10:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:21 AM (America/Los_Angeles) / 2026-04-05 10:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:41 AM (America/Los_Angeles) / 2026-04-05 10:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:41 AM (America/Los_Angeles) / 2026-04-05 10:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:01 AM (America/Los_Angeles) / 2026-04-05 11:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:01 AM (America/Los_Angeles) / 2026-04-05 11:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:21 AM (America/Los_Angeles) / 2026-04-05 11:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:21 AM (America/Los_Angeles) / 2026-04-05 11:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:41 AM (America/Los_Angeles) / 2026-04-05 11:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:41 AM (America/Los_Angeles) / 2026-04-05 11:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:01 AM (America/Los_Angeles) / 2026-04-05 12:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:01 AM (America/Los_Angeles) / 2026-04-05 12:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:21 AM (America/Los_Angeles) / 2026-04-05 12:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:21 AM (America/Los_Angeles) / 2026-04-05 12:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:41 AM (America/Los_Angeles) / 2026-04-05 12:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:41 AM (America/Los_Angeles) / 2026-04-05 12:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:01 AM (America/Los_Angeles) / 2026-04-05 13:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:01 AM (America/Los_Angeles) / 2026-04-05 13:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:21 AM (America/Los_Angeles) / 2026-04-05 13:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:21 AM (America/Los_Angeles) / 2026-04-05 13:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:41 AM (America/Los_Angeles) / 2026-04-05 13:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:41 AM (America/Los_Angeles) / 2026-04-05 13:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:01 AM (America/Los_Angeles) / 2026-04-05 14:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:01 AM (America/Los_Angeles) / 2026-04-05 14:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:21 AM (America/Los_Angeles) / 2026-04-05 14:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:21 AM (America/Los_Angeles) / 2026-04-05 14:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:41 AM (America/Los_Angeles) / 2026-04-05 14:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:41 AM (America/Los_Angeles) / 2026-04-05 14:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:01 AM (America/Los_Angeles) / 2026-04-05 15:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:01 AM (America/Los_Angeles) / 2026-04-05 15:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:21 AM (America/Los_Angeles) / 2026-04-05 15:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:21 AM (America/Los_Angeles) / 2026-04-05 15:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:41 AM (America/Los_Angeles) / 2026-04-05 15:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:41 AM (America/Los_Angeles) / 2026-04-05 15:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:01 AM (America/Los_Angeles) / 2026-04-05 16:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:01 AM (America/Los_Angeles) / 2026-04-05 16:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:21 AM (America/Los_Angeles) / 2026-04-05 16:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:21 AM (America/Los_Angeles) / 2026-04-05 16:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:41 AM (America/Los_Angeles) / 2026-04-05 16:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:41 AM (America/Los_Angeles) / 2026-04-05 16:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 10:01 AM (America/Los_Angeles) / 2026-04-05 17:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenOhOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 10:01 AM (America/Los_Angeles) / 2026-04-05 17:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 10:21 AM (America/Los_Angeles) / 2026-04-05 17:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 10:21 AM (America/Los_Angeles) / 2026-04-05 17:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTenFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 10:41 AM (America/Los_Angeles) / 2026-04-05 17:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTenFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 10:41 AM (America/Los_Angeles) / 2026-04-05 17:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 11:01 AM (America/Los_Angeles) / 2026-04-05 18:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 11:01 AM (America/Los_Angeles) / 2026-04-05 18:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 11:21 AM (America/Los_Angeles) / 2026-04-05 18:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenTwentyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 11:21 AM (America/Los_Angeles) / 2026-04-05 18:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsElevenFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 11:41 AM (America/Los_Angeles) / 2026-04-05 18:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsElevenFortyOneAmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 11:41 AM (America/Los_Angeles) / 2026-04-05 18:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:01 PM (America/Los_Angeles) / 2026-04-05 19:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveOhOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:01 PM (America/Los_Angeles) / 2026-04-05 19:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:21 PM (America/Los_Angeles) / 2026-04-05 19:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveTwentyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:21 PM (America/Los_Angeles) / 2026-04-05 19:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwelveFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:41 PM (America/Los_Angeles) / 2026-04-05 19:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwelveFortyOnePmTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 12:41 PM (America/Los_Angeles) / 2026-04-05 19:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:01 PM (America/Los_Angeles) / 2026-04-05 20:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:01 PM (America/Los_Angeles) / 2026-04-05 20:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:21 PM (America/Los_Angeles) / 2026-04-05 20:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:21 PM (America/Los_Angeles) / 2026-04-05 20:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsOneFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:41 PM (America/Los_Angeles) / 2026-04-05 20:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsOneFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 1:41 PM (America/Los_Angeles) / 2026-04-05 20:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:01 PM (America/Los_Angeles) / 2026-04-05 21:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:01 PM (America/Los_Angeles) / 2026-04-05 21:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:21 PM (America/Los_Angeles) / 2026-04-05 21:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:21 PM (America/Los_Angeles) / 2026-04-05 21:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsTwoFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:41 PM (America/Los_Angeles) / 2026-04-05 21:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsTwoFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 2:41 PM (America/Los_Angeles) / 2026-04-05 21:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:01 PM (America/Los_Angeles) / 2026-04-05 22:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:01 PM (America/Los_Angeles) / 2026-04-05 22:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:21 PM (America/Los_Angeles) / 2026-04-05 22:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:21 PM (America/Los_Angeles) / 2026-04-05 22:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsThreeFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:41 PM (America/Los_Angeles) / 2026-04-05 22:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsThreeFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 3:41 PM (America/Los_Angeles) / 2026-04-05 22:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:01 PM (America/Los_Angeles) / 2026-04-05 23:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:01 PM (America/Los_Angeles) / 2026-04-05 23:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:21 PM (America/Los_Angeles) / 2026-04-05 23:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:21 PM (America/Los_Angeles) / 2026-04-05 23:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFourFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:41 PM (America/Los_Angeles) / 2026-04-05 23:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFourFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 4:41 PM (America/Los_Angeles) / 2026-04-05 23:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:01 PM (America/Los_Angeles) / 2026-04-06 00:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:01 PM (America/Los_Angeles) / 2026-04-06 00:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:21 PM (America/Los_Angeles) / 2026-04-06 00:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:21 PM (America/Los_Angeles) / 2026-04-06 00:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsFiveFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:41 PM (America/Los_Angeles) / 2026-04-06 00:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsFiveFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 5:41 PM (America/Los_Angeles) / 2026-04-06 00:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:01 PM (America/Los_Angeles) / 2026-04-06 01:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:01 PM (America/Los_Angeles) / 2026-04-06 01:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:21 PM (America/Los_Angeles) / 2026-04-06 01:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:21 PM (America/Los_Angeles) / 2026-04-06 01:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSixFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:41 PM (America/Los_Angeles) / 2026-04-06 01:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSixFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 6:41 PM (America/Los_Angeles) / 2026-04-06 01:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:01 PM (America/Los_Angeles) / 2026-04-06 02:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:01 PM (America/Los_Angeles) / 2026-04-06 02:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:21 PM (America/Los_Angeles) / 2026-04-06 02:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:21 PM (America/Los_Angeles) / 2026-04-06 02:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsSevenFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:41 PM (America/Los_Angeles) / 2026-04-06 02:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsSevenFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 7:41 PM (America/Los_Angeles) / 2026-04-06 02:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:01 PM (America/Los_Angeles) / 2026-04-06 03:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:01 PM (America/Los_Angeles) / 2026-04-06 03:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:21 PM (America/Los_Angeles) / 2026-04-06 03:21 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightTwentyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:21 PM (America/Los_Angeles) / 2026-04-06 03:21 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsEightFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:41 PM (America/Los_Angeles) / 2026-04-06 03:41 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsEightFortyOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 8:41 PM (America/Los_Angeles) / 2026-04-06 03:41 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Fact]
    public void Resolve_ReturnsChangsha258_WhenRulesetCronReminderBodyContainsNineOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:01 PM (America/Los_Angeles) / 2026-04-06 04:01 UTC
            """;

        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Fact]
    public void IsChangsha258_ReturnsTrue_WhenRulesetCronReminderBodyContainsNineOhOnePmSundayTimestampMetadata()
    {
        const string ruleset = """
            [cron:aac88361-c047-4527-8bef-737045ae8702 Reminder to #mahjong] Autonomous Mahjong execution for channel 1474176085838004255.
            Use ONLY these channel-scoped files as source of truth:
            - /home/node/.openclaw/workspaces/main/channel-mahjong/AGENTS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/TASKS.md
            - /home/node/.openclaw/workspaces/main/channel-mahjong/MEMORY.md
            Do NOT use /home/node/.openclaw/workspaces/main/TASKS.md for this run.
            TOP PRIORITY: implement the minimal 258 (Changsha) ruleset variant (per TASKS.md #1) before anything else.
            REQUIREMENT: Each run MUST create and merge exactly one small PR (or explain why it cannot). Keep scope tight (<=5 commits, single theme).
            If tests fail or PR cannot be opened/merged, send:
            "Status update: NO PROGRESS SINCE LAST CHECK-IN (<blocker>) | Next: <immediate corrective action>" and stop.
            After PR merge, send a Discord message to channel 1474176085838004255 with exact format:
            "Status update: <PR URL> | Next: <single immediate step>".
            If the task is visual parity and a screenshot is required, capture and send the screenshot via message tool after the PR, then post the status update line.
            Current time: Sunday, April 5th, 2026 — 9:01 PM (America/Los_Angeles) / 2026-04-06 04:01 UTC
            """;

        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("minimal changsha 2589")]
    [InlineData("minimal cs 2589")]
    [InlineData("minimal two five eight nine")]
    [InlineData("minimal er wu ba jiu")]
    public void Resolve_ReturnsChineseOfficial_WhenMinimalPrefixInputIsNotValidChangsha258(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }

    [Theory]
    [InlineData("minimal changsha seven pairs ruleset")]
    [InlineData("minimal changsha seven pair ruleset")]
    [InlineData("mini cs thirteen orphans rules")]
    [InlineData("mini cs thirteen orphan rules")]
    [InlineData("minimal small win nine gates")]
    [InlineData("mini xiao hu nine gates")]
    [InlineData("changsha minimal nine gates")]
    [InlineData("changsha minimal nine gate")]
    [InlineData("cs default big three dragons")]
    [InlineData("cs default big three dragon")]
    [InlineData("changsha rules little four winds")]
    [InlineData("changsha rules little four wind")]
    [InlineData("changsha 258 big win")]
    [InlineData("cs 258 big wins")]
    [InlineData("changsha da hu")]
    [InlineData("cs dahu")]
    [InlineData("长沙 258 大胡")]
    public void Resolve_ReturnsChineseOfficial_WhenRulesetIncludesUnsupportedSpecialPatternKeywords(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }

    [Theory]
    [InlineData("minimal changsha seven pairs ruleset")]
    [InlineData("minimal changsha seven pair ruleset")]
    [InlineData("mini cs thirteen orphans rules")]
    [InlineData("mini cs thirteen orphan rules")]
    [InlineData("minimal small win nine gates")]
    [InlineData("mini xiao hu nine gates")]
    [InlineData("changsha minimal nine gates")]
    [InlineData("changsha minimal nine gate")]
    [InlineData("cs default big three dragons")]
    [InlineData("cs default big three dragon")]
    [InlineData("changsha rules little four winds")]
    [InlineData("changsha rules little four wind")]
    [InlineData("changsha 258 big win")]
    [InlineData("cs 258 big wins")]
    [InlineData("changsha da hu")]
    [InlineData("cs dahu")]
    [InlineData("长沙 258 大胡")]
    public void IsChangsha258_ReturnsFalse_WhenRulesetIncludesUnsupportedSpecialPatternKeywords(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeFalse();
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (big wins optional later)")]
    [InlineData("minimal 258 (changsha) ruleset variant (big wins are optional later)")]
    [InlineData("minimal 258 (changsha) ruleset variant (big wins optional)")]
    [InlineData("changsha 258 no big wins")]
    [InlineData("changsha 258 optional big win")]
    [InlineData("changsha 258 optional big wins later")]
    [InlineData("cs 258 without big win")]
    [InlineData("cs 258 big win optional")]
    [InlineData("cs 258 optional big win later")]
    [InlineData("长沙 258 无大胡")]
    [InlineData("长沙 258 不要大胡")]
    [InlineData("长沙 258 可选大胡")]
    public void Resolve_ReturnsChangsha258_WhenRulesetExplicitlyOptsOutBigWinPatterns(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("minimal 258 (changsha) ruleset variant (big wins optional later)")]
    [InlineData("minimal 258 (changsha) ruleset variant (big wins are optional later)")]
    [InlineData("minimal 258 (changsha) ruleset variant (big wins optional)")]
    [InlineData("changsha 258 no big wins")]
    [InlineData("changsha 258 optional big win")]
    [InlineData("changsha 258 optional big wins later")]
    [InlineData("cs 258 without big win")]
    [InlineData("cs 258 big win optional")]
    [InlineData("cs 258 optional big win later")]
    [InlineData("长沙 258 无大胡")]
    [InlineData("长沙 258 不要大胡")]
    [InlineData("长沙 258 可选大胡")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetExplicitlyOptsOutBigWinPatterns(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("Changsha ２５８")]
    [InlineData("CS ２-５-８ pair")]
    [InlineData("２５８")]
    [InlineData("minimal ２５８ ruleset variant")]
    [InlineData("Changsha １０８ tiles")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesFullWidthDigits(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("Changsha ２５８")]
    [InlineData("CS ２-５-８ pair")]
    [InlineData("２５８")]
    [InlineData("minimal ２５８ ruleset variant")]
    [InlineData("Changsha １０８ tiles")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesFullWidthDigits(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("ＣＳ ２５８")]
    [InlineData("Ｃｈａｎｇｓｈａ ２５８")]
    [InlineData("ｍｉｎｉｍａｌ ２５８ (ｃｈａｎｇｓｈａ) ruleset variant")]
    public void Resolve_ReturnsChangsha258_WhenRulesetUsesFullWidthLatinLetters(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.Changsha258);
    }

    [Theory]
    [InlineData("ＣＳ ２５８")]
    [InlineData("Ｃｈａｎｇｓｈａ ２５８")]
    [InlineData("ｍｉｎｉｍａｌ ２５８ (ｃｈａｎｇｓｈａ) ruleset variant")]
    public void IsChangsha258_ReturnsTrue_WhenRulesetUsesFullWidthLatinLetters(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeTrue();
    }

    [Theory]
    [InlineData("Changsha ２５８９")]
    [InlineData("minimal ２５８９ ruleset variant")]
    [InlineData("ＣＳ ２５８９")]
    [InlineData("ｍｉｎｉｍａｌ ２５８９ ruleset variant")]
    public void Resolve_ReturnsChineseOfficial_WhenFullWidthDigitSequenceIsNot258(string ruleset)
    {
        RulesetResolver.Resolve(ruleset).Should().Be(MahjongRuleset.ChineseOfficial);
    }

    [Theory]
    [InlineData("Changsha ２５８９")]
    [InlineData("minimal ２５８９ ruleset variant")]
    [InlineData("ＣＳ ２５８９")]
    [InlineData("ｍｉｎｉｍａｌ ２５８９ ruleset variant")]
    public void IsChangsha258_ReturnsFalse_WhenFullWidthDigitSequenceIsNot258(string ruleset)
    {
        RulesetResolver.IsChangsha258(ruleset).Should().BeFalse();
    }
}
