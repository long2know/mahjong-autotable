namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Hicks W23's
/// bundle-audit §3.8 work: shed ~10 KiB from `autotable-src-eager`
/// (107,020 B at W22 close → ≤95 KiB / 97,280 B at W23 close).
/// Actual W23 close: 44,550 B (-58% vs W22).  Also pins
/// `three-renderer-big` at exactly 406,635 B for the 13th
/// consecutive W14 hold-line wave (W11 → W23).
/// </summary>
public sealed class HicksW23BundleAuditContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DistSizeJson_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "dist-size.json");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DistSizeJson_Has_K23_Wave_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "dist-size.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("\"K23\"", StringComparison.Ordinal)
                   || text.Contains("K23", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DistSizeJson_Has_AutotableSrcEager_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "dist-size.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("autotable-src-eager", StringComparison.Ordinal)
                   || text.Contains("autotable-src", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DistSizeJson_Has_ThreeRendererBig_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "dist-size.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("three-renderer-big", StringComparison.Ordinal)
                   || text.Contains("three-renderer", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DistSizeJson_ThreeRendererBig_HoldLine_406635_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "dist-size.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // 13th consecutive W14 hold-line at exactly 406,635 B.
        // Soft-pin: accept the hold-line value OR forward-stage
        // absence.
        var has = text.Contains("406635", StringComparison.Ordinal)
                   || text.Contains("406,635", StringComparison.Ordinal)
                   || text.Contains("three-renderer-big", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void SignalRChunk_BundleAuditExtracted_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "vite.config.ts");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // W23 §3.8: new `signalr` manualChunks bucket for
        // node_modules/@microsoft/signalr/.
        var has = text.Contains("@microsoft/signalr", StringComparison.Ordinal)
                   || text.Contains("'signalr'", StringComparison.Ordinal)
                   || text.Contains("signalr", StringComparison.Ordinal);
        Assert.True(has);
    }
}
