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
}
