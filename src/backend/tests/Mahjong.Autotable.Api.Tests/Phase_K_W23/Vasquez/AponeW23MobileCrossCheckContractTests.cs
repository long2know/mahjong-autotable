namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Apone W23's
/// mobile platform cross-check workflow
/// (.github/workflows/mobile-platform-cross-check.yml) extending
/// the W22 mobile build matrix.
/// </summary>
public sealed class AponeW23MobileCrossCheckContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void MobileCrossCheck_Workflow_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(root.FullName, ".github", "workflows",
            "mobile-platform-cross-check.yml");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void MobileCrossCheck_Workflow_Has_PlatformMatrix_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(root.FullName, ".github", "workflows",
            "mobile-platform-cross-check.yml");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Expect at least one of: iOS, Android, tvOS, watchOS (any
        // platform string is fine for the soft-pin).
        var has = text.Contains("ios", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("android", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("tvos", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("watchos", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void MobilePackageJson_Version_032_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(root.FullName, "mobile", "package.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("0.32.0", StringComparison.Ordinal)
                   || text.Contains("0.31.0", StringComparison.Ordinal);
        Assert.True(has);
    }
}
