using FluentAssertions;
using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public class BotClaimAdvisorTests
{
    [Fact]
    public void ShouldClaimDiscardWin_ThrowsForNullHandTiles()
    {
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);

        var act = () => BotClaimAdvisor.ShouldClaimDiscardWin(null!, meldTiles, discardedTile);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ThrowsForNullMeldTiles()
    {
        var handTiles = new List<Tile> { new(TileSuit.Dots, 1) };
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var act = () => BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, null!, discardedTile);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ThrowsForNullDiscardedTile()
    {
        var handTiles = new List<Tile> { new(TileSuit.Dots, 1) };
        var meldTiles = new List<Tile>();

        var act = () => BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ThrowsWhenTileCollectionsContainNull()
    {
        var handTiles = new List<Tile> { new(TileSuit.Dots, 1), null! };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var act = () => BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Tiles cannot contain null values.*");
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsFalse_WhenPlayerOwnsWrongTileShapeForDrawTurn()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 9);

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile);

        shouldClaim.Should().BeFalse();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsTrue_ForChineseOfficialBigThreeDragonsDiscardWin()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.White);

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile, MahjongRuleset.ChineseOfficial);

        shouldClaim.Should().BeTrue();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsFalse_WhenDiscardedTileDoesNotCompleteWinningHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Bamboos, 9);

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile, MahjongRuleset.ChineseOfficial);

        shouldClaim.Should().BeFalse();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsFalse_WhenMissedWinLockBlocksDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.White);
        var missedWinLock = new MissedWinLock(new HashSet<Tile> { discardedTile });

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            missedWinLock);

        shouldClaim.Should().BeFalse();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsTrue_WhenMissedWinLockDoesNotBlockDiscard()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.White);
        var missedWinLock = new MissedWinLock(new HashSet<Tile> { new Tile(TileSuit.Dots, 9) });

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            missedWinLock);

        shouldClaim.Should().BeTrue();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsFalse_WhenChineseOfficialMinimumFanNotMet()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 5);

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile, MahjongRuleset.ChineseOfficial);

        shouldClaim.Should().BeFalse();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenHandHasThreeMatchingTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.Red);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenPotentialOutsDropWithoutMinimumFan()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4), new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 9)
        };
        var discardedTile = new Tile(TileSuit.Dots, 7);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var preClaimPotentialOuts = CountWinningDrawOutsAfterDraw(
            handTiles,
            Array.Empty<Tile>(),
            drawTileTypes,
            ruleset,
            requireMinimumFan: false);
        var postClaimPotentialOuts = CountWinningDrawOutsAfterExposedKong(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            drawTileTypes,
            ruleset,
            requireMinimumFan: false);

        preClaimOuts.Should().Be(0);
        preClaimPotentialOuts.Should().BeGreaterThan(0);
        postClaimPotentialOuts.Should().BeLessThan(preClaimPotentialOuts);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out _);

        shouldClaim.Should().BeFalse();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenOnlyLowFanOutsExist()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 7);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenClaimWouldRemoveOnlyWinningOut()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 7);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenKongCutsWinningOutsBeyondTolerance()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, Array.Empty<Tile>(), discardedTile, drawTileTypes, ruleset);

        preClaimOuts.Should().BeGreaterThan(postClaimOuts);
        postClaimOuts.Should().BeGreaterThan(0);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out _);

        shouldClaim.Should().BeFalse();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenOnlyWinningOutIsFullyVisible()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 7);
        var visibleTiles = new List<Tile> { discardedTile };

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaOnlySmallWinPairWouldBeConsumed()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaHasAlternateSmallWinPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaHasAlternateSmallWinSingletonCandidate()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 5), new(TileSuit.Characters, 6),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaAlternateSmallWinSingletonIsNearDead()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaBigWinFlexibleEyeAllowsKong()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsTrue_ForChangsha258RequiredPairDiscardWin()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 2)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 2);

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile, MahjongRuleset.Changsha258);

        shouldClaim.Should().BeTrue();
    }

    [Fact]
    public void ShouldClaimDiscardWin_ReturnsFalse_ForChangsha258WhenDiscardedTileIsHonor()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 2)
        };

        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Winds, 0, Wind.East);

        var shouldClaim = BotClaimAdvisor.ShouldClaimDiscardWin(handTiles, meldTiles, discardedTile, MahjongRuleset.Changsha258);

        shouldClaim.Should().BeFalse();
    }

    private static int CountWinningDrawOutsAfterDraw(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        IReadOnlyList<Tile> drawTileTypes,
        MahjongRuleset ruleset,
        bool requireMinimumFan = true)
    {
        var ownedTiles = handTiles
            .Concat(meldTiles)
            .ToList();
        var ownedCounts = ownedTiles
            .GroupBy(static tile => tile)
            .ToDictionary(group => group.Key, group => group.Count());

        var winningOuts = 0;

        foreach (var drawTileType in drawTileTypes)
        {
            ownedCounts.TryGetValue(drawTileType, out var drawTileCount);
            var remainingCopies = 4 - drawTileCount;
            if (remainingCopies <= 0)
            {
                continue;
            }

            var candidateTiles = ownedTiles
                .Append(drawTileType)
                .ToList();

            WinningHand? winningHand;
            if (!requireMinimumFan)
            {
                if (ruleset == MahjongRuleset.Changsha258)
                {
                    if (!Changsha258Ruleset.TryGetWinningHand(candidateTiles, out winningHand))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!HandAnalyzer.TryGetWinningHand(candidateTiles, out winningHand))
                    {
                        continue;
                    }
                }
            }
            else
            {
                if (!RulesetHandAnalyzer.TryGetWinningHand(candidateTiles, ruleset, out winningHand))
                {
                    continue;
                }
            }

            if (winningHand is null)
            {
                continue;
            }

            if (requireMinimumFan && !ScoreCalculator.MeetsRulesetMinimumFan(winningHand, ruleset))
            {
                continue;
            }

            winningOuts++;
        }

        return winningOuts;
    }

    private static int CountWinningDrawOutsAfterExposedKong(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        IReadOnlyList<Tile> drawTileTypes,
        MahjongRuleset ruleset,
        bool requireMinimumFan = true)
    {
        var handAfterKong = new List<Tile>(handTiles);

        for (var i = 0; i < 3; i++)
        {
            var index = handAfterKong.FindIndex(tile => tile.Equals(discardedTile));
            if (index < 0)
            {
                return 0;
            }

            handAfterKong.RemoveAt(index);
        }

        var meldAfterKong = meldTiles
            .Concat(Enumerable.Repeat(discardedTile, 4))
            .ToList();

        return CountWinningDrawOutsAfterDraw(handAfterKong, meldAfterKong, drawTileTypes, ruleset, requireMinimumFan);
    }

    private static IReadOnlyList<Tile> BuildDrawTileTypes(MahjongRuleset ruleset)
        => ruleset == MahjongRuleset.Changsha258
            ? TileFactory.CreateChangsha258Set().Distinct().ToList()
            : TileFactory.CreateStandardSet().Distinct().ToList();
}
