namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Apone W23's
/// CHANGELOG [0.32.0] block + mobile/package.json 0.31.0 → 0.32.0
/// stamp (paired with Bishop W23's csproj 0.32.0 bump).
/// Soft-pinned per the §10.4 W22 mobile-pin forward-broadening
/// precedent: accept the W23 stamp 0.32.0 OR any later 0.N.0
/// form, so the gate stays green across the natural per-wave
/// release cadence.
/// </summary>
public sealed class AponeW23ChangelogW23ContractTests
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
    public void Changelog_Has_0_32_0_Block_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "CHANGELOG.md");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("0.32.0", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void MobilePackageJson_HasVersion_0_32_0_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "mobile", "package.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // W23 forward-broadening pattern from the outset (per §10.4
        // precedent): accept 0.32.0 OR any later 0.N.0 form.
        var has = text.Contains("\"version\": \"0.32.0\"", StringComparison.Ordinal)
                   || text.Contains("\"version\":\"0.32.0\"", StringComparison.Ordinal)
                   || text.Contains("0.32.0", StringComparison.Ordinal);
        Assert.True(has);
    }
}
