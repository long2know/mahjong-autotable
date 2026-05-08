using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.Changsha;

/// <summary>
/// CAT-F: Win Pattern Tests
/// Tests P0 scenarios for the 4 core Changsha hand patterns in v1 scope.
/// V1 supports: Standard 4+1 with 258 pair, Seven Pairs, All Pungs, Full Flush.
/// Deferred to v2: Special wins (Heaven, Earth, Last Tile, Kong-related, Robbing Kong, instant wins).
/// </summary>
public class WinPatternTests
{
    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void StandardWin_FourMeldsAndPairWith258_ValidatesAsSmallWin()
    {
        // F-01: Win with 4 melds (sets/sequences) + 1 pair of 2, 5, or 8 (Small Win)
        
        // Arrange: Hand with 4 melds + pair of 5s
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     // Pung of Bamboo 1s
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     // Chow of Dots 3-4-5
        //     new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 5),
        //     // Chow of Characters 6-7-8
        //     new Tile(TileSuit.Characters, 6), new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8),
        //     // Chow of Bamboo 7-8-9
        //     new Tile(TileSuit.Bamboo, 7), new Tile(TileSuit.Bamboo, 8), new Tile(TileSuit.Bamboo, 9),
        //     // Pair of 5s (258 General)
        //     new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5)
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Valid Small Win with 258 pair
        // Assert.True(result.IsWin);
        // Assert.Equal(WinType.SmallWin, result.WinType);
        // Assert.Contains(WinPattern.Standard_4_Plus_1, result.Patterns);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void StandardWin_FourMeldsWithNon258Pair_RejectedForSmallWin()
    {
        // F-01 negative case: 4 melds + pair of 3 (not 2/5/8) is invalid for Standard Small Win
        
        // Arrange: Hand with 4 melds + pair of 3s
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 4), new Tile(TileSuit.Dots, 5),
        //     new Tile(TileSuit.Characters, 6), new Tile(TileSuit.Characters, 7), new Tile(TileSuit.Characters, 8),
        //     new Tile(TileSuit.Bamboo, 7), new Tile(TileSuit.Bamboo, 8), new Tile(TileSuit.Bamboo, 9),
        //     new Tile(TileSuit.Characters, 3), new Tile(TileSuit.Characters, 3) // Pair of 3s
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Not a valid Standard Small Win (pair must be 2/5/8)
        // Assert.False(result.IsWin);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void SevenPairs_ExactlySevenDistinctPairs_ValidatesAsBigWin()
    {
        // F-02: Win with exactly seven distinct pairs of tiles (Big Win)
        
        // Arrange: Hand with 7 pairs
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3),
        //     new Tile(TileSuit.Characters, 5), new Tile(TileSuit.Characters, 5),
        //     new Tile(TileSuit.Bamboo, 7), new Tile(TileSuit.Bamboo, 7),
        //     new Tile(TileSuit.Dots, 9), new Tile(TileSuit.Dots, 9),
        //     new Tile(TileSuit.Characters, 2), new Tile(TileSuit.Characters, 2),
        //     new Tile(TileSuit.Bamboo, 4), new Tile(TileSuit.Bamboo, 4)
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Valid Big Win, Seven Pairs pattern
        // Assert.True(result.IsWin);
        // Assert.Equal(WinType.BigWin, result.WinType);
        // Assert.Contains(WinPattern.SevenPairs, result.Patterns);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void AllPungs_FourPungsAndAnyPair_ValidatesAsBigWin()
    {
        // F-04: Win with 4 pungs/kongs + any pair (Big Win, no 258 requirement)
        
        // Arrange: Hand with 4 pungs + pair of 7s (not 258)
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     // Pung of Bamboo 1s
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     // Pung of Dots 3s
        //     new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3), new Tile(TileSuit.Dots, 3),
        //     // Pung of Characters 6s
        //     new Tile(TileSuit.Characters, 6), new Tile(TileSuit.Characters, 6), new Tile(TileSuit.Characters, 6),
        //     // Pung of Bamboo 9s
        //     new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9),
        //     // Pair of 7s (ANY pair allowed for All Pungs)
        //     new Tile(TileSuit.Dots, 7), new Tile(TileSuit.Dots, 7)
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Valid Big Win, All Pungs pattern
        // Assert.True(result.IsWin);
        // Assert.Equal(WinType.BigWin, result.WinType);
        // Assert.Contains(WinPattern.AllPungs, result.Patterns);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void FullFlush_AllTilesFromSingleSuit_ValidatesAsBigWin()
    {
        // F-05: Win with all tiles from single suit (Dots, Bamboo, or Characters), any pattern (Big Win, no 258 requirement)
        
        // Arrange: Hand with all Bamboo tiles
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     // All Bamboo suit
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     new Tile(TileSuit.Bamboo, 3), new Tile(TileSuit.Bamboo, 4), new Tile(TileSuit.Bamboo, 5),
        //     new Tile(TileSuit.Bamboo, 6), new Tile(TileSuit.Bamboo, 7), new Tile(TileSuit.Bamboo, 8),
        //     new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9),
        //     new Tile(TileSuit.Bamboo, 4), new Tile(TileSuit.Bamboo, 4) // Pair of 4s (non-258 allowed)
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Valid Big Win, Full Flush pattern
        // Assert.True(result.IsWin);
        // Assert.Equal(WinType.BigWin, result.WinType);
        // Assert.Contains(WinPattern.FullFlush, result.Patterns);
    }

    [Fact(Skip = "Awaiting Bishop's IWinDetector")]
    [Trait("Category", "Changsha")]
    public void FullFlush_MixedSuits_RejectedForFullFlush()
    {
        // F-05 negative case: Mixed suits do not qualify as Full Flush
        
        // Arrange: Hand with Bamboo and Dots mixed
        // var detector = new ChangshaWinDetector();
        // var hand = new[] {
        //     new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1), new Tile(TileSuit.Bamboo, 1),
        //     new Tile(TileSuit.Bamboo, 3), new Tile(TileSuit.Bamboo, 4), new Tile(TileSuit.Bamboo, 5),
        //     new Tile(TileSuit.Dots, 6), new Tile(TileSuit.Dots, 7), new Tile(TileSuit.Dots, 8), // Mixed!
        //     new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9), new Tile(TileSuit.Bamboo, 9),
        //     new Tile(TileSuit.Bamboo, 2), new Tile(TileSuit.Bamboo, 2)
        // };
        
        // Act: Validate win
        // var result = detector.IsWinningHand(hand);
        
        // Assert: Not a Full Flush (contains multiple suits)
        // Assert.False(result.Patterns.Contains(WinPattern.FullFlush));
    }

    // DEFERRED TO V2: Special wins (Heaven, Earth, Last Tile, Kong-related, Robbing Kong, instant wins)
    
    [Fact(Skip = "Deferred to v2")]
    [Trait("Category", "Changsha")]
    public void BlessingOfHeaven_DealerWinsOnInitialDeal_ValidatesAsBigWin()
    {
        // F-08: Dealer wins on their initial 14-tile deal without any action (Big Win, 258 pair required)
        // DEFERRED: Instant wins not in v1 scope
    }

    [Fact(Skip = "Deferred to v2")]
    [Trait("Category", "Changsha")]
    public void RobbingTheKong_WinByClaimingAddedKongTile_ValidatesAsBigWin()
    {
        // F-14: Win by claiming the tile another player adds to existing pung to form exposed kong
        // DEFERRED: Kong robbing not in v1 scope
    }

    [Fact(Skip = "Deferred to v2")]
    [Trait("Category", "Changsha")]
    public void StartingHandInstantWins_FourJoys_VoidedSuit_SixSixStraight()
    {
        // F-15, F-16, F-17, F-18, F-19: Starting hand instant win conditions
        // DEFERRED: Instant wins not in v1 scope
    }
}
