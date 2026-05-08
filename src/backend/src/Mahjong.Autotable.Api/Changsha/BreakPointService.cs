namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// Given dice sum + roller (dealer) seat, computes which wall to break and where.
/// Per Vasquez §2:
///   1. Dice sum selects wall counting counterclockwise from dealer: (sum-1) % 4 maps to
///      dealer(0)/right(1)/opposite(2)/left(3).
///   2. From the RIGHT end of that wall, count stacks equal to dice sum.
///      Break occurs AFTER that stack. Drawing proceeds counterclockwise from the break.
///
/// Wall layout: 54 stacks total. Two walls have 14 stacks, two have 13 stacks.
/// Physical layout: [dealer:14, right:13, opposite:14, left:13] — but for digital
/// implementation we treat the wall as a flat ordered list of 108 tiles and compute
/// the absolute tile index for the break point.
/// </summary>
public interface IBreakPointService
{
    BreakPointResult ComputeBreakPoint(int diceSum, int dealerSeatIndex);
}

public sealed class BreakPointService : IBreakPointService
{
    // Wall stacks per player: dealer and opposite get 14, right and left get 13
    // Ordered counterclockwise from dealer: dealer(0), right(1), opposite(2), left(3)
    private static readonly int[] StacksPerWall = [14, 13, 14, 13];

    public BreakPointResult ComputeBreakPoint(int diceSum, int dealerSeatIndex)
    {
        if (diceSum < 2 || diceSum > 12)
            throw new ArgumentOutOfRangeException(nameof(diceSum), "Dice sum must be 2–12.");

        // Step 1: Determine which wall to break (counterclockwise from dealer)
        // Count: 1=dealer, 2=right, 3=opposite, 4=left, 5=dealer, ...
        var wallOffset = (diceSum - 1) % 4;
        var wallIndex = (dealerSeatIndex + wallOffset) % 4;

        // Step 2: Count stacks from the RIGHT end of that wall
        var wallStacks = StacksPerWall[GetRelativeWallIndex(dealerSeatIndex, wallIndex)];
        var stacksFromRight = diceSum;

        // If dice sum exceeds stacks in this wall, we wrap (but sum max is 12, wall min is 13, so no wrap)
        var stackIndex = wallStacks - stacksFromRight;

        // Step 3: Compute absolute tile index
        // Tiles are ordered: wall0 stacks left-to-right (each stack = 2 tiles), then wall1, etc.
        // Drawing starts AFTER the break point, proceeding counterclockwise (left-to-right from break)
        var tilesBeforeWall = GetTilesBeforeWall(dealerSeatIndex, wallIndex);
        var tileIndex = tilesBeforeWall + stackIndex * 2;

        return new BreakPointResult(wallIndex, stackIndex, tileIndex);
    }

    private static int GetRelativeWallIndex(int dealerSeatIndex, int wallIndex)
    {
        return (wallIndex - dealerSeatIndex + 4) % 4;
    }

    private static int GetTilesBeforeWall(int dealerSeatIndex, int wallIndex)
    {
        var tiles = 0;
        for (var i = 0; i < 4; i++)
        {
            var currentWall = (dealerSeatIndex + i) % 4;
            if (currentWall == wallIndex)
                break;
            tiles += StacksPerWall[i] * 2;
        }
        return tiles;
    }
}
