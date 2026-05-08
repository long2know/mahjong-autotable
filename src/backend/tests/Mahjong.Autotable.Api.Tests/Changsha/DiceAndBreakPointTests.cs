using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-B: Dice Roll & Break Point Tests
/// Tests P0 scenarios for dice-driven wall break point determination.
/// </summary>
public class DiceAndBreakPointTests
{
    [Fact(Skip = "Awaiting Bishop's IDiceService")]
    [Trait("Category", "Changsha")]
    public void DiceRoll_Range_BothDiceReturn1To6()
    {
        // B-01: Two dice rolled, each returning value 1-6, sum is 2-12
        
        // Arrange: Initialize dice service with fixed seed for determinism
        // var diceService = new DiceService(seed: 42);
        
        // Act: Roll two dice 100 times
        // var rolls = new List<(int die1, int die2, int sum)>();
        // for (int i = 0; i < 100; i++)
        // {
        //     var (die1, die2) = diceService.RollTwoDice();
        //     rolls.Add((die1, die2, die1 + die2));
        // }
        
        // Assert: Each die always 1-6
        // Assert.All(rolls, r => Assert.InRange(r.die1, 1, 6));
        // Assert.All(rolls, r => Assert.InRange(r.die2, 1, 6));
        
        // Assert: Sum always 2-12
        // Assert.All(rolls, r => Assert.InRange(r.sum, 2, 12));
    }

    [Fact(Skip = "Awaiting Bishop's IDiceService")]
    [Trait("Category", "Changsha")]
    public void StartingWallDetermination_DiceSum7_SelectsCorrectWall()
    {
        // B-02: Dice sum determines which player's wall, counting counterclockwise from dealer
        // Example: sum=7, dealer=seat0 → (7 mod 4 = 3) or count: 1=dealer, 2=right, 3=opposite, 4=left, 5=dealer...
        
        // Arrange: Dealer at seat 0, dice service returns sum 7
        // var diceService = new MockDiceService(fixedSum: 7);
        // var wallSelector = new ChangshaWallSelector();
        
        // Act: Determine starting wall
        // var startingSeat = wallSelector.DetermineStartingWall(dealerSeat: 0, diceSum: 7);
        
        // Assert: Sum 7 → count CCW: dealer=1, right(3)=2, opposite(2)=3, left(1)=4, dealer=5, right=6, opposite=7
        // Seat 2 (opposite) is the 7th position
        // Assert.Equal(2, startingSeat);
    }

    [Fact(Skip = "Awaiting Bishop's IBreakPointService")]
    [Trait("Category", "Changsha")]
    public void BreakPointCalculation_DiceSum7_CountsFromRightEnd()
    {
        // B-03: Using dice sum, count that many stacks from the right end of starting wall
        
        // Arrange: Starting wall identified, dice sum = 7
        // var breakPointService = new ChangshaBreakPointService();
        // var wall = CreateMockWall(stackCount: 14); // Dealer wall has 14 stacks
        
        // Act: Calculate break point with sum 7
        // var breakPoint = breakPointService.CalculateBreakPoint(wall, diceSum: 7);
        
        // Assert: Break occurs after the 7th stack from right
        // Count from right: stack positions 13, 12, 11, 10, 9, 8, 7 → break after position 7
        // Assert.Equal(7, breakPoint.StackIndexFromRight);
    }

    [Fact(Skip = "Awaiting Bishop's IBreakPointService")]
    [Trait("Category", "Changsha")]
    public void DrawWall_BeginsAfterBreakPoint_WrapsClockwise()
    {
        // B-04: Draw wall is sequence of tiles immediately following break point, wrapping clockwise
        
        // Arrange: Break point determined at position X
        // var breakPointService = new ChangshaBreakPointService();
        // var walls = CreateMockWalls(); // 4 walls in square
        // var breakPoint = new BreakPoint(wallIndex: 0, stackIndex: 7);
        
        // Act: Build draw wall sequence
        // var drawWall = breakPointService.BuildDrawWall(walls, breakPoint);
        
        // Assert: Draw wall starts after break point and continues clockwise
        // Assert.Equal(108 - 53, drawWall.TileCount); // 53 tiles dealt initially, 55 remain
        // Assert.Equal(walls[0].Tiles[8], drawWall.Tiles[0]); // First tile after break
    }

    [Fact(Skip = "Awaiting Bishop's IDiceService")]
    [Trait("Category", "Changsha")]
    public void DiceRoll_WithFixedSeed_IsDeterministic()
    {
        // Determinism test: Same seed produces same rolls
        
        // Arrange: Two dice services with same seed
        // var dice1 = new DiceService(seed: 123);
        // var dice2 = new DiceService(seed: 123);
        
        // Act: Roll each 10 times
        // var rolls1 = Enumerable.Range(0, 10).Select(_ => dice1.RollTwoDice()).ToList();
        // var rolls2 = Enumerable.Range(0, 10).Select(_ => dice2.RollTwoDice()).ToList();
        
        // Assert: Identical sequences
        // Assert.Equal(rolls1, rolls2);
    }
}
