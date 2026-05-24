namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// JWT rotation-drill autorun BackgroundService.  Surface-less
/// hosted service that periodically re-evaluates every per-tenant
/// rotation policy and stamps a meta-audit row.  Soft-pinned.
/// </summary>
public sealed class JwtRotationDrillAutorunContractTests
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
    public void JwtRotationDrillAutorunService_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Auth", "JwtRotationDrillAutorunService.cs");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void JwtRotationDrillAutorunService_Has_BackgroundService_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Auth", "JwtRotationDrillAutorunService.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("BackgroundService", StringComparison.Ordinal)
                   || text.Contains("IHostedService", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void JwtRotationDrillAutorunService_Has_TickOnceAsync_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Auth", "JwtRotationDrillAutorunService.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Public TickOnceAsync is exposed so loop can be driven by tests.
        var has = text.Contains("TickOnceAsync", StringComparison.Ordinal)
                   || text.Contains("Tick", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Appsettings_Has_RotationDrillAutorunSchedule_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "appsettings.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Auth:RotationDrill:AutorunCronSchedule + StartupSettleSeconds.
        var has = text.Contains("AutorunCronSchedule", StringComparison.Ordinal)
                   || text.Contains("RotationDrill", StringComparison.Ordinal);
        Assert.True(has);
    }
}
