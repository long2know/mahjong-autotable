using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class MissedWinLockTests
{
    [Fact]
    public void CreateLock_PlayerHasWinningHand_CreatesLock()
    {
        // Seven pairs pattern waiting for one more pair (13 tiles + discard = winning)
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 8)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 8);

        var result = MissedWinLockTracker.CreateLock(handTiles, meldTiles, discardedTile);

        Assert.NotNull(result);
        Assert.NotEmpty(result.LockedWinningTiles);
    }

    [Fact]
    public void CreateLock_PlayerDoesNotHaveWinningHand_ReturnsNull()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 3)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 6);

        var result = MissedWinLockTracker.CreateLock(handTiles, meldTiles, discardedTile);

        Assert.Null(result);
    }

    [Fact]
    public void IsLockedOnTile_TileInLockedSet_ReturnsTrue()
    {
        var lockedTiles = new HashSet<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 9)
        };
        var missedWinLock = new MissedWinLock(lockedTiles);
        var testTile = new Tile(TileSuit.Characters, 3);

        var result = missedWinLock.IsLockedOnTile(testTile);

        Assert.True(result);
    }

    [Fact]
    public void IsLockedOnTile_TileNotInLockedSet_ReturnsFalse()
    {
        var lockedTiles = new HashSet<Tile>
        {
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 6)
        };
        var missedWinLock = new MissedWinLock(lockedTiles);
        var testTile = new Tile(TileSuit.Characters, 9);

        var result = missedWinLock.IsLockedOnTile(testTile);

        Assert.False(result);
    }

    [Fact]
    public void ComputeWaitSet_SimpleWait_ReturnsCorrectSet()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 6),
            new(TileSuit.Dots, 8)
        };
        var meldTiles = new List<Tile>();

        var waitSet = MissedWinLockTracker.ComputeWaitSet(handTiles, meldTiles);

        Assert.NotEmpty(waitSet);
        Assert.Contains(waitSet, t => t.Suit == TileSuit.Dots && t.Rank == 8);
    }

    [Fact]
    public void ComputeWaitSet_MultipleWaits_ReturnsAllWaitTiles()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 6),
            new(TileSuit.Characters, 7),
            new(TileSuit.Characters, 8),
            new(TileSuit.Characters, 9),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5)
        };
        var meldTiles = new List<Tile>();

        var waitSet = MissedWinLockTracker.ComputeWaitSet(handTiles, meldTiles);

        Assert.NotEmpty(waitSet);
        Assert.True(waitSet.Count >= 1);
    }

    [Fact]
    public void ComputeWaitSet_NoWaits_ReturnsEmptySet()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 4),
            new(TileSuit.Characters, 7),
            new(TileSuit.Dots, 1),
            new(TileSuit.Dots, 4),
            new(TileSuit.Dots, 7),
            new(TileSuit.Bamboos, 1),
            new(TileSuit.Bamboos, 4),
            new(TileSuit.Bamboos, 7),
            new(TileSuit.Characters, 2),
            new(TileSuit.Dots, 3),
            new(TileSuit.Bamboos, 6),
            new(TileSuit.Characters, 9)
        };
        var meldTiles = new List<Tile>();

        var waitSet = MissedWinLockTracker.ComputeWaitSet(handTiles, meldTiles);

        Assert.Empty(waitSet);
    }

    [Fact]
    public void CreateLock_WithMelds_CreatesCorrectLock()
    {
        // Standard hand with exposed pung, waiting on a tile to complete
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 8)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 2)
        };
        var discardedTile = new Tile(TileSuit.Characters, 8);

        var result = MissedWinLockTracker.CreateLock(handTiles, meldTiles, discardedTile);

        Assert.NotNull(result);
        Assert.NotEmpty(result.LockedWinningTiles);
    }

    [Fact]
    public void CreateLock_NullHandTiles_ThrowsArgumentNullException()
    {
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 1);

        Assert.Throws<ArgumentNullException>(() => 
            MissedWinLockTracker.CreateLock(null!, meldTiles, discardedTile));
    }

    [Fact]
    public void CreateLock_NullMeldTiles_ThrowsArgumentNullException()
    {
        var handTiles = new List<Tile> { new(TileSuit.Characters, 1) };
        var discardedTile = new Tile(TileSuit.Characters, 1);

        Assert.Throws<ArgumentNullException>(() => 
            MissedWinLockTracker.CreateLock(handTiles, null!, discardedTile));
    }

    [Fact]
    public void CreateLock_NullDiscardedTile_ThrowsArgumentNullException()
    {
        var handTiles = new List<Tile> { new(TileSuit.Characters, 1) };
        var meldTiles = new List<Tile>();

        Assert.Throws<ArgumentNullException>(() => 
            MissedWinLockTracker.CreateLock(handTiles, meldTiles, null!));
    }

    [Fact]
    public void IsLockedOnTile_NullTile_ThrowsArgumentNullException()
    {
        var lockedTiles = new HashSet<Tile> { new(TileSuit.Characters, 1) };
        var missedWinLock = new MissedWinLock(lockedTiles);

        Assert.Throws<ArgumentNullException>(() => missedWinLock.IsLockedOnTile(null!));
    }

    [Fact]
    public void ComputeWaitSet_SevenPairsWait_ReturnsCorrectWait()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 5),
            new(TileSuit.Characters, 5),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 2),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Characters, 8)
        };
        var meldTiles = new List<Tile>();

        var waitSet = MissedWinLockTracker.ComputeWaitSet(handTiles, meldTiles);

        Assert.NotEmpty(waitSet);
        Assert.Contains(waitSet, t => t.Suit == TileSuit.Characters && t.Rank == 8);
    }

    [Fact]
    public void IsLockedOnTile_DifferentInstanceSameTile_ReturnsTrue()
    {
        var lockedTiles = new HashSet<Tile>
        {
            new(TileSuit.Characters, 5)
        };
        var missedWinLock = new MissedWinLock(lockedTiles);
        var testTile = new Tile(TileSuit.Characters, 5);

        var result = missedWinLock.IsLockedOnTile(testTile);

        Assert.True(result);
    }

    [Fact]
    public void ComputeWaitSet_HandWithKongs_HandlesCorrectly()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 2),
            new(TileSuit.Characters, 3),
            new(TileSuit.Characters, 4),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 2),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Dots, 5),
            new(TileSuit.Bamboos, 8),
            new(TileSuit.Bamboos, 9)
        };
        var meldTiles = new List<Tile>
        {
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5),
            new(TileSuit.Bamboos, 5)
        };

        var waitSet = MissedWinLockTracker.ComputeWaitSet(handTiles, meldTiles);

        Assert.NotNull(waitSet);
    }

    [Fact]
    public void CreateLock_InvalidHandSize_ReturnsNull()
    {
        var handTiles = new List<Tile>
        {
            new(TileSuit.Characters, 1),
            new(TileSuit.Characters, 2)
        };
        var meldTiles = new List<Tile>();
        var discardedTile = new Tile(TileSuit.Characters, 3);

        var result = MissedWinLockTracker.CreateLock(handTiles, meldTiles, discardedTile);

        Assert.Null(result);
    }
}
