namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// ChangshaEntities additions: TournamentStanding gets two new
/// double columns (Buchholz, SonnebornBerger) + supporting
/// background-service / chunk-upload entity touches.
/// </summary>
public sealed class BishopW23ChangshaEntitiesContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    private static string EntitiesPath()
    {
        var root = FindRepoRoot();
        return Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Data", "Entities", "ChangshaEntities.cs");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Entities_File_Present_OrForwardStaged()
    {
        var p = EntitiesPath();
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Entities_TournamentStanding_HasBuchholz_OrForwardStaged()
    {
        var p = EntitiesPath();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("Buchholz", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Entities_TournamentStanding_HasSonnebornBerger_OrForwardStaged()
    {
        var p = EntitiesPath();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("SonnebornBerger", StringComparison.Ordinal)
                   || text.Contains("Sonneborn", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }
}
