namespace Mahjong.Autotable.Api.Tests.Phase_K_W21.Vasquez;

/// <summary>
/// Phase K Wave 21 — Vasquez self-lane W21 surface smoke facts.
/// A small set of "the things this Vasquez W21 commit promised"
/// hard-pins that the inbox memo / handoff doc reference.  All
/// hard-assert (they ship in this same PR).
/// </summary>
public sealed class W21SurfaceSmokeFactsTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-21"), Trait("Lane", "Vasquez")]
    public void Phase_K_W21_Vasquez_Directory_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var d = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Phase_K_W21", "Vasquez");
        Assert.True(Directory.Exists(d));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-21"), Trait("Lane", "Vasquez")]
    public void Phase_K_W21_Vasquez_HasAtLeastTwentyForwardStages()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var d = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Phase_K_W21", "Vasquez");
        Assert.True(Directory.Exists(d));
        var n = Directory.EnumerateFiles(d, "*.cs", SearchOption.TopDirectoryOnly).Count();
        // Vasquez W21 brief: 20-30 forward-stage contract files.
        Assert.True(n >= 20, $"Expected ≥ 20 W21 Vasquez forward-stage files; found {n}.");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-21"), Trait("Lane", "Vasquez")]
    public void KW20_To_KW21_Regression_Class_Rename_Landed()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var newPath = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Regression",
            "Wave1ThroughKW21RegressionTests.cs");
        var oldPath = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Regression",
            "Wave1ThroughKW20RegressionTests.cs");
        Assert.True(File.Exists(newPath), "New W21 regression class missing.");
        Assert.False(File.Exists(oldPath), "Old W20 regression class still present.");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-21"), Trait("Lane", "Vasquez")]
    public void Vasquez_W21_InboxMemo_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, ".squad", "decisions", "inbox",
            "vasquez-phase-k-wave-21.md");
        Assert.True(File.Exists(p));
    }
}
