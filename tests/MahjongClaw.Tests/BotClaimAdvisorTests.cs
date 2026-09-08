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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenFourCopiesAlreadyInHand()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 2), new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenPlayerOwnsWrongTileShape()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8)
        };
        var discardedTile = new Tile(TileSuit.Dots, 1);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenNoWinningOrPotentialOutsExist()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 6),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South)
        };
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.Red);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var preClaimPotentialOuts = CountWinningDrawOutsAfterDraw(
            handTiles,
            Array.Empty<Tile>(),
            drawTileTypes,
            ruleset,
            requireMinimumFan: false);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            drawTileTypes,
            ruleset);
        var postClaimPotentialOuts = CountWinningDrawOutsAfterExposedKong(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            drawTileTypes,
            ruleset,
            requireMinimumFan: false);

        preClaimOuts.Should().Be(0);
        preClaimPotentialOuts.Should().Be(0);
        postClaimOuts.Should().Be(0);
        postClaimPotentialOuts.Should().Be(0);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChineseOfficialSevenPairsLeanWouldBreakForKong()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West)
        };
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.Red);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            drawTileTypes,
            ruleset);

        preClaimOuts.Should().BeGreaterThan(0);
        postClaimOuts.Should().BeLessOrEqualTo(preClaimOuts);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChineseOfficialThirteenOrphansLeanWouldBreakForKong()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red)
        };
        var discardedTile = new Tile(TileSuit.Dots, 1);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaKongHasNoWinningOuts()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7)
        };
        var discardedTile = new Tile(TileSuit.Dots, 3);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            drawTileTypes,
            ruleset);

        preClaimOuts.Should().Be(0);
        postClaimOuts.Should().Be(0);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenImmediateOutsAppearDespitePotentialOutsDrop()
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
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, Array.Empty<Tile>(), discardedTile, drawTileTypes, ruleset);
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
        postClaimOuts.Should().BeGreaterThan(0);
        preClaimPotentialOuts.Should().BeGreaterThan(0);
        postClaimPotentialOuts.Should().BeLessThan(preClaimPotentialOuts);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
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
    public void TrySelectExposedKongClaim_TreatsDiscardAsVisible_WhenVisibleTilesOmitDiscard()
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

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
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
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenKongCreatesImmediateWinningOuts()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 3), new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9), new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 5)
        };
        var discardedTile = new Tile(TileSuit.Dots, 5);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, Array.Empty<Tile>(), discardedTile, drawTileTypes, ruleset);

        preClaimOuts.Should().Be(0);
        postClaimOuts.Should().BeGreaterThan(0);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            Array.Empty<Tile>(),
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaFlexibleEyeKongCutsWinningOuts()
    {
        var ruleset = MahjongRuleset.Changsha258;
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

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();
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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaFlexibleEyeSmallWinKongCutsWinningOuts()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };
        var discardedTile = new Tile(TileSuit.Dots, 2);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, Array.Empty<Tile>(), discardedTile, drawTileTypes, ruleset);

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();
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
        var visibleTiles = new List<Tile>();

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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaOffRankKongHasThinSmallWinSingletonCandidate()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2)
        };

        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, Array.Empty<Tile>(), discardedTile, drawTileTypes, ruleset);

        preClaimOuts.Should().Be(0);
        postClaimOuts.Should().Be(0);

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaOffRankKongHasStrongSmallWinSingletonCandidate()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectExposedKongClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(3);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaOffRankKongHasNoLiveSmallWinPairCandidatesAndNoPairOrChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = BuildExhaustedChangshaSmallWinVisibleTiles();

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeFalse();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

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
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaOffRankKongHasNoLiveSmallWinPairsButKeepsNonSmallWinPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = BuildFullyExhaustedChangshaSmallWinVisibleTiles();

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeFalse();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

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
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaOffRankKongHasLiveSmallWinPairCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 6)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 7);

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeFalse();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaSmallWinKongLeavesNoPairOrChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeTrue();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaSevenPairsLeanWouldLoseOnlySmallWinPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7)
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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaSevenPairsLeanWouldBreakForKong()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 4);

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
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 5)
        };

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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaAlternateSmallWinSingletonsAreThin()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8)
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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaAlternateSmallWinSingletonIsUnconfirmed()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeTrue();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaAlternateSmallWinSingletonIsDead()
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
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaAlternateSmallWinSingletonHasSingleLiveCopy()
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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaAlternateSmallWinSingletonHasSingleLiveCopyEvenWithPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3)
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
    public void TrySelectExposedKongClaim_ReturnsTrue_WhenChangshaAlternateSmallWinSingletonsCombineForThreeLiveCopies()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 1)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 8), new(TileSuit.Bamboos, 8),
            new(TileSuit.Characters, 5), new(TileSuit.Characters, 5)
        };

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
    public void TrySelectExposedKongClaim_ReturnsFalse_WhenChangshaBigWinFlexibleEyeKongCutsWinningOuts()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };
        var discardedTile = new Tile(TileSuit.Dots, 2);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, Array.Empty<Tile>(), drawTileTypes, ruleset);
        var postClaimOuts = CountWinningDrawOutsAfterExposedKong(handTiles, Array.Empty<Tile>(), discardedTile, drawTileTypes, ruleset);

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, Array.Empty<Tile>()).Should().BeTrue();
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
    public void TrySelectPungClaim_ReturnsTrue_WhenChangshaClaimCreatesWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Bamboos, 7);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsTrue_WhenHandAlreadyHasThreeOfDiscardedTile()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 7),
            new(TileSuit.Dots, 8),
            new(TileSuit.Dots, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Bamboos, 7);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsTrue_WhenChangshaOffRankPungHasNoLiveSmallWinPairCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Characters, 3), new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 7);
        var visibleTiles = BuildExhaustedChangshaSmallWinVisibleTiles();

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeFalse();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsTrue_WhenChangshaSmallWinPairsAreExhausted()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 4)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 7);
        var visibleTiles = BuildFullyExhaustedChangshaSmallWinVisibleTiles();

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeFalse();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsTrue_WhenChangshaOffRankPungHasLiveSmallWinPairCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 6), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 6)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 7);

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeFalse();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsTrue_WhenChangshaBigWinFlexibleEyeAllowsPung()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9), new(TileSuit.Dots, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeTrue();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeTrue();

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Equals(discardedTile));
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChangshaOffRankPungHasThinSmallWinSingletonCandidate()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChangshaOffRankPungHasTwoThinSmallWinSingletons()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Bamboos, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 5)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenAlternateSmallWinSingletonsAreExhaustedWithoutVisibility()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 8), new(TileSuit.Dots, 8),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 3),
            new(TileSuit.Bamboos, 4)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5)
        };
        var discardedTile = new Tile(TileSuit.Dots, 8);

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChangshaOnlySmallWinPairWouldBeConsumed()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        Changsha258Ruleset.IsSmallWinPair(discardedTile).Should().BeTrue();
        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChangshaPungLeavesNoPairOrChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChangshaSevenPairsLeanWouldBreakForPung()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 3), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 2),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 4),
            new(TileSuit.Bamboos, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, meldTiles, drawTileTypes, ruleset);
        var postClaimOuts = CountBestWinningOutsAfterPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            drawTileTypes,
            ruleset);

        preClaimOuts.Should().BeGreaterThan(0);
        postClaimOuts.Should().BeLessOrEqualTo(preClaimOuts);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChangshaSevenPairsLeanHasNoWinningOuts()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 9), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 1),
            new(TileSuit.Dots, 2)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            visibleTiles,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChineseOfficialSevenPairsLeanWouldBreakForPung()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.Green),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Dragons, 0, null, Dragon.White),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dragons, 0, null, Dragon.Red);
        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preClaimOuts = CountWinningDrawOutsAfterDraw(handTiles, meldTiles, drawTileTypes, ruleset);
        var postClaimOuts = CountBestWinningOutsAfterPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            drawTileTypes,
            ruleset);

        preClaimOuts.Should().BeGreaterThan(0);
        postClaimOuts.Should().BeLessOrEqualTo(preClaimOuts);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChineseOfficialThirteenOrphansLeanWouldBreakForPung()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dragons, 0, null, Dragon.Green)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectPungClaim_ReturnsFalse_WhenChineseOfficialThirteenOrphansLeanHasFourNonOrphans()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 5)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);

        var shouldClaim = BotClaimAdvisor.TrySelectPungClaim(
            handTiles,
            meldTiles,
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
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

    [Fact]
    public void TrySelectChowClaim_ReturnsTrue_WhenChangshaClaimCreatesWinningOuts()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 5), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 2), new(TileSuit.Characters, 2),
            new(TileSuit.Bamboos, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Bamboos, 4);

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().HaveCount(2);
        claimTiles.Should().OnlyContain(tile => tile.Suit == TileSuit.Bamboos);

        var chowRanks = claimTiles
            .Select(tile => tile.Rank)
            .Append(discardedTile.Rank)
            .OrderBy(rank => rank)
            .ToArray();

        chowRanks.Distinct().Should().HaveCount(3);
        (chowRanks[2] - chowRanks[0]).Should().Be(2);
    }

    [Fact]
    public void TrySelectChowClaim_PrefersPreservingChangshaSmallWinPair_WhenOutsTie()
    {
        var ruleset = MahjongRuleset.Changsha258;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 5), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 4), new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 4);
        var preserveCandidate = new List<Tile> { new(TileSuit.Dots, 2), new(TileSuit.Dots, 3) };
        var breakCandidate = new List<Tile> { new(TileSuit.Dots, 3), new(TileSuit.Dots, 5) };

        var drawTileTypes = BuildDrawTileTypes(ruleset);
        var preserveOuts = CountBestWinningOutsAfterChowClaimSummary(
            handTiles,
            meldTiles,
            discardedTile,
            preserveCandidate,
            drawTileTypes,
            ruleset);
        var breakOuts = CountBestWinningOutsAfterChowClaimSummary(
            handTiles,
            meldTiles,
            discardedTile,
            breakCandidate,
            drawTileTypes,
            ruleset);

        preserveOuts.Should().Be(breakOuts);

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
            handTiles,
            meldTiles,
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeTrue();
        claimTiles.Should().BeEquivalentTo(preserveCandidate);
    }

    [Fact]
    public void TrySelectChowClaim_ReturnsFalse_WhenChangshaClaimLeavesNoPairOrChow()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2),
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 4), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 4), new(TileSuit.Characters, 7), new(TileSuit.Characters, 9)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Bamboos, 3);

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectChowClaim_ReturnsFalse_WhenChangshaClaimConsumesOnlyThinSmallWinSingleton()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 4)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };
        var discardedTile = new Tile(TileSuit.Dots, 3);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
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
    public void TrySelectChowClaim_ReturnsFalse_WhenChangshaClaimConsumesLastLiveSmallWinCandidate()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Characters, 7), new(TileSuit.Characters, 7)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 5), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 6), new(TileSuit.Bamboos, 7), new(TileSuit.Bamboos, 8)
        };
        var discardedTile = new Tile(TileSuit.Bamboos, 1);
        var visibleTiles = BuildExhaustedChangshaSmallWinVisibleTiles();
        visibleTiles.Remove(new Tile(TileSuit.Bamboos, 2));

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
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
    public void TrySelectChowClaim_ReturnsFalse_WhenChangshaClaimBreaksSoleNearDeadSmallWinPair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);
        var visibleTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2)
        };

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
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
    public void TrySelectChowClaim_ReturnsFalse_WhenChangshaClaimBreaksSoleSmallWinPairWithoutAlternatePair()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 2), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 6), new(TileSuit.Characters, 7), new(TileSuit.Characters, 8),
            new(TileSuit.Winds, 0, Wind.East)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 1);

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.Changsha258,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectChowClaim_ReturnsFalse_WhenChangshaClaimLeavesNoLiveSmallWinPairCandidates()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 3), new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 6), new(TileSuit.Dots, 6), new(TileSuit.Dots, 7), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 3), new(TileSuit.Bamboos, 4), new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 1), new(TileSuit.Characters, 3)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 2);
        var visibleTiles = BuildFullyExhaustedChangshaSmallWinVisibleTiles();

        Changsha258Ruleset.IsBigWinEyeFlexibleCandidate(handTiles, meldTiles).Should().BeFalse();

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
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
    public void TrySelectChowClaim_ReturnsFalse_WhenChineseOfficialThirteenOrphansLeanWouldBreakForChow()
    {
        var ruleset = MahjongRuleset.ChineseOfficial;
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 9),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 9),
            new(TileSuit.Winds, 0, Wind.East),
            new(TileSuit.Winds, 0, Wind.South),
            new(TileSuit.Winds, 0, Wind.West),
            new(TileSuit.Winds, 0, Wind.North),
            new(TileSuit.Dragons, 0, null, Dragon.Red),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Dots, 4);

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
            handTiles,
            meldTiles,
            discardedTile,
            ruleset,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
    }

    [Fact]
    public void TrySelectChowClaim_ReturnsFalse_WhenDiscardIsHonor()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Dots, 1), new(TileSuit.Dots, 2), new(TileSuit.Dots, 3),
            new(TileSuit.Dots, 4), new(TileSuit.Dots, 5), new(TileSuit.Dots, 6),
            new(TileSuit.Dots, 7), new(TileSuit.Dots, 8), new(TileSuit.Dots, 9),
            new(TileSuit.Bamboos, 1), new(TileSuit.Bamboos, 2), new(TileSuit.Bamboos, 3),
            new(TileSuit.Bamboos, 4)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Winds, 0, Wind.East);

        var shouldClaim = BotClaimAdvisor.TrySelectChowClaim(
            handTiles,
            meldTiles,
            discardedTile,
            MahjongRuleset.ChineseOfficial,
            out var claimTiles);

        shouldClaim.Should().BeFalse();
        claimTiles.Should().BeEmpty();
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

    private static int CountBestWinningOutsAfterPungClaim(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        IReadOnlyList<Tile> drawTileTypes,
        MahjongRuleset ruleset,
        bool requireMinimumFan = true)
    {
        var handAfterClaim = new List<Tile>(handTiles);

        for (var i = 0; i < 2; i++)
        {
            var index = handAfterClaim.FindIndex(tile => tile.Equals(discardedTile));
            if (index < 0)
            {
                return 0;
            }

            handAfterClaim.RemoveAt(index);
        }

        var meldAfterClaim = meldTiles
            .Concat(Enumerable.Repeat(discardedTile, 3))
            .ToList();

        var bestOuts = 0;

        for (var discardIndex = 0; discardIndex < handAfterClaim.Count; discardIndex++)
        {
            var reducedHand = handAfterClaim
                .Where((_, index) => index != discardIndex)
                .ToList();

            var outs = CountWinningDrawOutsAfterDraw(reducedHand, meldAfterClaim, drawTileTypes, ruleset, requireMinimumFan);
            if (outs > bestOuts)
            {
                bestOuts = outs;
            }
        }

        return bestOuts;
    }

    private readonly record struct WinningOutSummary(int TileTypes, int RemainingCopies);

    private static WinningOutSummary CountWinningDrawOutsAfterDrawSummary(
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
        var winningOutCopies = 0;

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
            winningOutCopies += remainingCopies;
        }

        return new WinningOutSummary(winningOuts, winningOutCopies);
    }

    private static WinningOutSummary CountBestWinningOutsAfterChowClaimSummary(
        IReadOnlyList<Tile> handTiles,
        IReadOnlyList<Tile> meldTiles,
        Tile discardedTile,
        IReadOnlyList<Tile> chowTiles,
        IReadOnlyList<Tile> drawTileTypes,
        MahjongRuleset ruleset,
        bool requireMinimumFan = true)
    {
        var handAfterClaim = new List<Tile>(handTiles);

        foreach (var chowTile in chowTiles)
        {
            var index = handAfterClaim.FindIndex(tile => tile.Equals(chowTile));
            if (index < 0)
            {
                return default;
            }

            handAfterClaim.RemoveAt(index);
        }

        var meldAfterClaim = meldTiles
            .Concat(chowTiles)
            .Append(discardedTile)
            .ToList();

        var bestOuts = default(WinningOutSummary);

        for (var discardIndex = 0; discardIndex < handAfterClaim.Count; discardIndex++)
        {
            var reducedHand = handAfterClaim
                .Where((_, index) => index != discardIndex)
                .ToList();

            var outs = CountWinningDrawOutsAfterDrawSummary(reducedHand, meldAfterClaim, drawTileTypes, ruleset, requireMinimumFan);
            if (outs.TileTypes > bestOuts.TileTypes
                || (outs.TileTypes == bestOuts.TileTypes && outs.RemainingCopies > bestOuts.RemainingCopies))
            {
                bestOuts = outs;
            }
        }

        return bestOuts;
    }

    private static List<Tile> BuildExhaustedChangshaSmallWinVisibleTiles()
    {
        var tiles = new List<Tile>();

        foreach (var suit in Changsha258Ruleset.AllowedSuits)
        {
            foreach (var rank in Changsha258Ruleset.SmallWinPairRanks)
            {
                for (var i = 0; i < 3; i++)
                {
                    tiles.Add(new Tile(suit, rank));
                }
            }
        }

        return tiles;
    }

    private static List<Tile> BuildFullyExhaustedChangshaSmallWinVisibleTiles()
    {
        var tiles = new List<Tile>();

        foreach (var suit in Changsha258Ruleset.AllowedSuits)
        {
            foreach (var rank in Changsha258Ruleset.SmallWinPairRanks)
            {
                for (var i = 0; i < 4; i++)
                {
                    tiles.Add(new Tile(suit, rank));
                }
            }
        }

        return tiles;
    }

    private static IReadOnlyList<Tile> BuildDrawTileTypes(MahjongRuleset ruleset)
        => ruleset == MahjongRuleset.Changsha258
            ? TileFactory.CreateChangsha258Set().Distinct().ToList()
            : TileFactory.CreateStandardSet().Distinct().ToList();
}
