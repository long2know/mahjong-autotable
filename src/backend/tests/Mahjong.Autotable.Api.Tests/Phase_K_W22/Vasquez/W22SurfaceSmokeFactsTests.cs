namespace Mahjong.Autotable.Api.Tests.Phase_K_W22.Vasquez;

/// <summary>
/// Phase K Wave 22 — Vasquez self-lane W22 surface smoke facts.
/// A small set of "the things this Vasquez W22 commit promised"
/// hard-pins that the inbox memo / handoff doc reference.  All
/// hard-assert (they ship in this same PR).
/// </summary>
public sealed class W22SurfaceSmokeFactsTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Vasquez")]
    public void Phase_K_W22_Vasquez_Directory_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var d = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Phase_K_W22", "Vasquez");
        Assert.True(Directory.Exists(d));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Vasquez")]
    public void Phase_K_W22_Vasquez_HasAtLeastTwentyForwardStages()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var d = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Phase_K_W22", "Vasquez");
        Assert.True(Directory.Exists(d));
        var n = Directory.EnumerateFiles(d, "*.cs", SearchOption.TopDirectoryOnly).Count();
        // Vasquez W22 brief: 20-30 forward-stage contract files.
        Assert.True(n >= 20, $"Expected ≥ 20 W22 Vasquez forward-stage files; found {n}.");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Vasquez")]
    public void KW21_To_KW22_Regression_Class_Rename_Landed()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var regDir = Path.Combine(root!.FullName, "src", "backend", "tests",
            "Mahjong.Autotable.Api.Tests", "Regression");
        // W23 forward-broadening (Vasquez): accept either the W22
        // rename target (Wave1ThroughKW22RegressionTests.cs) OR the
        // W23 rename target (Wave1ThroughKW23RegressionTests.cs) so
        // this historical W22-rename-landed pin keeps passing across
        // each subsequent wave's KW(N) → KW(N+1) rename.  Mirrors the
        // W22 forward-broadening of the W21SurfaceSmokeFactsTests
        // KW20_To_KW21_Regression_Class_Rename_Landed pin.
        var w22Path = Path.Combine(regDir, "Wave1ThroughKW22RegressionTests.cs");
        var w23Path = Path.Combine(regDir, "Wave1ThroughKW23RegressionTests.cs");
        var oldPath = Path.Combine(regDir, "Wave1ThroughKW21RegressionTests.cs");
        Assert.True(File.Exists(w22Path) || File.Exists(w23Path),
            "Neither the W22 nor the W23 regression class is present.");
        Assert.False(File.Exists(oldPath), "Old W21 regression class still present.");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Vasquez")]
    public void Vasquez_W22_InboxMemo_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, ".squad", "decisions", "inbox",
            "vasquez-phase-k-wave-22.md");
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Vasquez")]
    public void Wave1ThroughKW22RegressionTests_Class_Present()
    {
        var asm = typeof(W22SurfaceSmokeFactsTests).Assembly;
        // W23 forward-broadening (Vasquez): accept either the W22
        // rename target (Wave1ThroughKW22RegressionTests) OR the W23
        // rename target (Wave1ThroughKW23RegressionTests).  Mirrors
        // the W22 forward-broadening of the historical W21 pin and
        // the regression-class rename-pin forward-broadening codified
        // at §10 of docs/agent-handoff-protocol.md.
        var t = asm.GetTypes().FirstOrDefault(x =>
            x.Name.Equals("Wave1ThroughKW22RegressionTests", StringComparison.Ordinal)
            || x.Name.Equals("Wave1ThroughKW23RegressionTests", StringComparison.Ordinal));
        Assert.NotNull(t);
    }
}
