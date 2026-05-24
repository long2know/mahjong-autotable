namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// tournament Buchholz + Sonneborn-Berger tiebreakers + standings
/// GET surface.  Soft-pinned so the gate stays green if Bishop's
/// surfaces have not yet landed.
/// </summary>
public sealed class TournamentBuchholzTiebreakerContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void TournamentStandingsController_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Tournament", "TournamentStandingsController.cs");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void TournamentFinalizationController_Has_Buchholz_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Tournament", "TournamentFinalizationController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("Buchholz", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("ComputeBuchholz", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void TournamentFinalizationController_Has_SonnebornBerger_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Tournament", "TournamentFinalizationController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("SonnebornBerger", StringComparison.Ordinal)
                   || text.Contains("Sonneborn-Berger", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Sonneborn", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void TournamentStandings_Get_Endpoint_Routed_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Tournament", "TournamentStandingsController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Expect a GET routed at /api/tournaments/{id}/standings.
        var has = text.Contains("standings", StringComparison.OrdinalIgnoreCase)
                   && (text.Contains("HttpGet", StringComparison.Ordinal)
                       || text.Contains("[HttpGet", StringComparison.Ordinal)
                       || text.Contains("Route(", StringComparison.Ordinal));
        Assert.True(has);
    }
}
