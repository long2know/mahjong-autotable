using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tables;

namespace Mahjong.Autotable.Api.Tests.ChangshaServices;

public class ClaimAdjudicatorTests
{
    private readonly ClaimAdjudicator _svc = new();

    [Fact]
    public void Hu_TakesPriorityOverKong()
    {
        var hands = new List<ChangshaHandState>
        {
            new() { SeatIndex = 0, ConcealedTiles = [] }, // discarder
            new() { SeatIndex = 1, ConcealedTiles = CreateHuHand(0) }, // has Hu
            new() { SeatIndex = 2, ConcealedTiles = [0, 1, 2] }, // has Kong (3 matching)
            new() { SeatIndex = 3, ConcealedTiles = [] }
        };

        // Discard tile 3 (logical 0 = Wan 1)
        var winner = _svc.Adjudicate(0, 3, hands, []);
        Assert.NotNull(winner);
        Assert.Equal(TableClaimType.Hu, winner.ClaimType);
    }

    [Fact]
    public void Kong_TakesPriorityOverPung()
    {
        var hands = new List<ChangshaHandState>
        {
            new() { SeatIndex = 0, ConcealedTiles = [] },
            new() { SeatIndex = 1, ConcealedTiles = [0, 1, 2] }, // Kong
            new() { SeatIndex = 2, ConcealedTiles = [0, 1] }, // Pung
            new() { SeatIndex = 3, ConcealedTiles = [] }
        };

        var winner = _svc.Adjudicate(0, 3, hands, []);
        Assert.NotNull(winner);
        Assert.Equal(TableClaimType.Kong, winner.ClaimType);
        Assert.Equal(1, winner.SeatIndex);
    }

    [Fact]
    public void Pung_TakesPriorityOverChow()
    {
        var hands = new List<ChangshaHandState>
        {
            new() { SeatIndex = 0, ConcealedTiles = [] },
            new() { SeatIndex = 1, ConcealedTiles = [4, 8] }, // Chow (next seat): Wan2, Wan3 for Wan1 discard
            new() { SeatIndex = 2, ConcealedTiles = [0, 1] }, // Pung
            new() { SeatIndex = 3, ConcealedTiles = [] }
        };

        // Discard tile 3 (logical 0 = Wan 1)
        var winner = _svc.Adjudicate(0, 3, hands, []);
        Assert.NotNull(winner);
        Assert.Equal(TableClaimType.Pung, winner.ClaimType);
        Assert.Equal(2, winner.SeatIndex);
    }

    [Fact]
    public void Chow_OnlyFromNextSeat()
    {
        var hands = new List<ChangshaHandState>
        {
            new() { SeatIndex = 0, ConcealedTiles = [] },
            new() { SeatIndex = 1, ConcealedTiles = [4, 8] }, // Next seat — can chow
            new() { SeatIndex = 2, ConcealedTiles = [4, 8] }, // Not next seat — cannot chow
            new() { SeatIndex = 3, ConcealedTiles = [4, 8] }  // Not next seat — cannot chow
        };

        var opportunities = _svc.GetOpportunities(0, 3, hands);
        var chows = opportunities.Where(o => o.ClaimType == TableClaimType.Chow).ToList();

        Assert.Single(chows);
        Assert.Equal(1, chows[0].SeatIndex);
    }

    [Fact]
    public void NoOpportunities_ReturnsNull()
    {
        var hands = new List<ChangshaHandState>
        {
            new() { SeatIndex = 0, ConcealedTiles = [] },
            new() { SeatIndex = 1, ConcealedTiles = [40, 44] }, // unrelated tiles
            new() { SeatIndex = 2, ConcealedTiles = [48, 52] },
            new() { SeatIndex = 3, ConcealedTiles = [56, 60] }
        };

        var winner = _svc.Adjudicate(0, 3, hands, []);
        Assert.Null(winner);
    }

    // Helper: create a 13-tile hand that wins with tile logical=0 (Wan 1)
    // Standard win: Wan 2-3-4, Wan 5-6-7, Wan 8-8 pair, Tong 1-2-3 = needs Wan 1 to complete Wan 1-2-3
    // Actually, let's make it simpler: hand has tiles that win when Wan1 (tile 3) is added
    private static List<int> CreateHuHand(int discardLogical)
    {
        // Build a hand that's 1 tile away from winning
        // 3 melds + pair + need one more tile for the 4th meld
        // Wan 2-3 (need Wan 1 to complete), Wan 4-5-6, Wan 7-8-9, Tong 5-5 pair
        return [
            4, 8,      // Wan 2 (copy 0), Wan 3 (copy 0) — need Wan 1
            12, 16, 20, // Wan 4, 5, 6
            24, 28, 32, // Wan 7, 8, 9
            52, 53,     // Tong 5 (copy 0, 1) — pair (rank 5 = 258)
            36, 40, 44  // Tong 1, 2, 3
        ];
    }
}
