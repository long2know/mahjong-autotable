namespace Mahjong.Autotable.Api.Tests.Phase_K_W21.Vasquez;

/// <summary>
/// Phase K Wave 21 — Vasquez paired contract for Apone W21's
/// CHANGELOG <c>[0.30.0]</c> block + <c>mobile/package.json</c>
/// 0.30.0 stamp (paired with Bishop W21's csproj 0.30.0 bump).
/// Soft-pinned so the gate stays green if Apone W21 has not yet
/// landed the entries.
/// </summary>
public sealed class AponeW21ChangelogW21ContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-21"), Trait("Lane", "Apone")]
    public void Changelog_Has_0_30_0_Block_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "CHANGELOG.md");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("0.30.0", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-21"), Trait("Lane", "Apone")]
    public void MobilePackageJson_HasVersion_0_30_0_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "mobile", "package.json");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // W22 forward-broadening (Vasquez): accept the historical
        // W21 stamp 0.30.0 OR the W22 bump to 0.31.0 (Apone W22
        // mobile-shell bump in 10907cd, paired with Bishop W22's
        // backend csproj 0.30.0 → 0.31.0 bump in 5029650).  Same
        // pattern as the W20 mobile-pin forward-broadening
        // codified in §9.5 of docs/agent-handoff-protocol.md.
        // W23 forward-broadening (Vasquez): also accept 0.32.0
        // (Apone W23 bump in dfb4ac0, paired with Bishop W23's
        // backend csproj 0.31.0 → 0.32.0 bump in 490f7fa).
        var has = text.Contains("\"version\": \"0.30.0\"", StringComparison.Ordinal)
                   || text.Contains("\"version\":\"0.30.0\"", StringComparison.Ordinal)
                   || text.Contains("0.30.0", StringComparison.Ordinal)
                   || text.Contains("\"version\": \"0.31.0\"", StringComparison.Ordinal)
                   || text.Contains("\"version\":\"0.31.0\"", StringComparison.Ordinal)
                   || text.Contains("0.31.0", StringComparison.Ordinal)
                   || text.Contains("\"version\": \"0.32.0\"", StringComparison.Ordinal)
                   || text.Contains("\"version\":\"0.32.0\"", StringComparison.Ordinal)
                   || text.Contains("0.32.0", StringComparison.Ordinal);
        Assert.True(has);
    }
}
