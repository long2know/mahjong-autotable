using MahjongClaw.Domain.Gameplay;
using MahjongClaw.Domain.Tiles;

namespace MahjongClaw.Tests;

public sealed class ClaimArbitratorTests
{
    private static readonly Guid Player1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Player2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Player3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Player4 = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Dictionary<Guid, Wind> FourPlayerSeats = new()
    {
        { Player1, Wind.East },
        { Player2, Wind.South },
        { Player3, Wind.West },
        { Player4, Wind.North }
    };

    [Fact]
    public void Resolve_NoClaims_ReturnsNull()
    {
        var claims = Array.Empty<ClaimRequest>();
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_OnlyPassClaims_ReturnsNull()
    {
        var claims = new[]
        {
            new ClaimRequest(Player2, ClaimType.Pass),
            new ClaimRequest(Player3, ClaimType.Pass),
            new ClaimRequest(Player4, ClaimType.Pass)
        };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_SingleWinClaim_ReturnsWinClaim()
    {
        var winClaim = new ClaimRequest(Player2, ClaimType.Win);
        var claims = new[] { winClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player2, result.PlayerId);
        Assert.Equal(ClaimType.Win, result.ClaimType);
    }

    [Fact]
    public void Resolve_WinVsPung_WinTakesPriority()
    {
        var winClaim = new ClaimRequest(Player2, ClaimType.Win);
        var pungClaim = new ClaimRequest(Player3, ClaimType.Pung);
        var claims = new[] { pungClaim, winClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(ClaimType.Win, result.ClaimType);
    }

    [Fact]
    public void Resolve_WinVsKong_WinTakesPriority()
    {
        var winClaim = new ClaimRequest(Player3, ClaimType.Win);
        var kongClaim = new ClaimRequest(Player2, ClaimType.ExposedKong);
        var claims = new[] { kongClaim, winClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(ClaimType.Win, result.ClaimType);
    }

    [Fact]
    public void Resolve_PungVsChow_PungTakesPriority()
    {
        var pungClaim = new ClaimRequest(Player4, ClaimType.Pung);
        var chowClaim = new ClaimRequest(Player2, ClaimType.Chow);
        var claims = new[] { chowClaim, pungClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(ClaimType.Pung, result.ClaimType);
    }

    [Fact]
    public void Resolve_KongVsChow_KongTakesPriority()
    {
        var kongClaim = new ClaimRequest(Player3, ClaimType.ExposedKong);
        var chowClaim = new ClaimRequest(Player2, ClaimType.Chow);
        var claims = new[] { chowClaim, kongClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(ClaimType.ExposedKong, result.ClaimType);
    }

    [Fact]
    public void Resolve_TwoWinClaims_NextClockwiseWins()
    {
        // Player1 (East) discards. Player2 (South, next clockwise) and Player4 (North) both claim win.
        var winClaim1 = new ClaimRequest(Player2, ClaimType.Win);
        var winClaim2 = new ClaimRequest(Player4, ClaimType.Win);
        var claims = new[] { winClaim2, winClaim1 };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player2, result.PlayerId); // South is closer
    }

    [Fact]
    public void Resolve_TwoWinClaimsOppositeSeats_CloserWins()
    {
        // Player1 (East) discards. Player3 (West, opposite) and Player4 (North) claim win.
        var winClaim1 = new ClaimRequest(Player3, ClaimType.Win);
        var winClaim2 = new ClaimRequest(Player4, ClaimType.Win);
        var claims = new[] { winClaim1, winClaim2 };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        // East -> South -> West -> North, so West (Player3) is closer than North (Player4)
        Assert.Equal(Player3, result.PlayerId);
    }

    [Fact]
    public void Resolve_TwoPungClaims_NextClockwiseWins()
    {
        var pungClaim1 = new ClaimRequest(Player2, ClaimType.Pung);
        var pungClaim2 = new ClaimRequest(Player3, ClaimType.Pung);
        var claims = new[] { pungClaim2, pungClaim1 };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player2, result.PlayerId);
    }

    [Fact]
    public void Resolve_WinAndPassAndPung_WinTakesPriority()
    {
        var winClaim = new ClaimRequest(Player4, ClaimType.Win);
        var passClaim = new ClaimRequest(Player2, ClaimType.Pass);
        var pungClaim = new ClaimRequest(Player3, ClaimType.Pung);
        var claims = new[] { passClaim, pungClaim, winClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(ClaimType.Win, result.ClaimType);
    }

    [Fact]
    public void Resolve_SingleChowClaim_ReturnsChowClaim()
    {
        var chowClaim = new ClaimRequest(Player2, ClaimType.Chow);
        var claims = new[] { chowClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(ClaimType.Chow, result.ClaimType);
    }

    [Fact]
    public void Resolve_ThreeWinClaims_EarliestClockwiseWins()
    {
        // Player1 (East) discards. All three opponents claim win.
        var winClaim1 = new ClaimRequest(Player2, ClaimType.Win); // South
        var winClaim2 = new ClaimRequest(Player3, ClaimType.Win); // West
        var winClaim3 = new ClaimRequest(Player4, ClaimType.Win); // North
        var claims = new[] { winClaim3, winClaim1, winClaim2 };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player2, result.PlayerId); // South is next clockwise from East
    }

    [Fact]
    public void Resolve_DiscarderNotInSeats_ThrowsArgumentException()
    {
        var unknownPlayer = Guid.NewGuid();
        var claims = new[] { new ClaimRequest(Player2, ClaimType.Win) };
        Assert.Throws<ArgumentException>(() =>
            ClaimArbitrator.Resolve(claims, unknownPlayer, FourPlayerSeats));
    }

    [Fact]
    public void Resolve_NullSubmissions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ClaimArbitrator.Resolve(null!, Player1, FourPlayerSeats));
    }

    [Fact]
    public void Resolve_NullPlayerSeats_ThrowsArgumentNullException()
    {
        var claims = new[] { new ClaimRequest(Player2, ClaimType.Win) };
        Assert.Throws<ArgumentNullException>(() =>
            ClaimArbitrator.Resolve(claims, Player1, null!));
    }

    [Fact]
    public void Resolve_SouthDiscardsWestAndNorthClaimWin_WestWins()
    {
        // South (Player2) discards, West (Player3) and North (Player4) claim win
        var winClaim1 = new ClaimRequest(Player3, ClaimType.Win);
        var winClaim2 = new ClaimRequest(Player4, ClaimType.Win);
        var claims = new[] { winClaim2, winClaim1 };
        var result = ClaimArbitrator.Resolve(claims, Player2, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player3, result.PlayerId); // West is next from South
    }

    [Fact]
    public void Resolve_NorthDiscardsEastClaimsWin_EastWins()
    {
        // North (Player4) discards, East (Player1) claims win
        var winClaim = new ClaimRequest(Player1, ClaimType.Win);
        var claims = new[] { winClaim };
        var result = ClaimArbitrator.Resolve(claims, Player4, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player1, result.PlayerId);
    }

    [Fact]
    public void Resolve_PungAndKongClaims_PungWinsByProximity()
    {
        // Both have same priority (50), so proximity wins
        var pungClaim = new ClaimRequest(Player2, ClaimType.Pung);
        var kongClaim = new ClaimRequest(Player3, ClaimType.ExposedKong);
        var claims = new[] { kongClaim, pungClaim };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        Assert.Equal(Player2, result.PlayerId); // South is closer than West
    }

    [Fact]
    public void Resolve_TwoChowClaims_NextClockwiseWins()
    {
        var chowClaim1 = new ClaimRequest(Player3, ClaimType.Chow);
        var chowClaim2 = new ClaimRequest(Player4, ClaimType.Chow);
        var claims = new[] { chowClaim2, chowClaim1 };
        var result = ClaimArbitrator.Resolve(claims, Player1, FourPlayerSeats);
        Assert.NotNull(result);
        // East -> South -> West -> North, but only West and North claiming
        // So West (Player3) is closer
        Assert.Equal(Player3, result.PlayerId);
    }
}
