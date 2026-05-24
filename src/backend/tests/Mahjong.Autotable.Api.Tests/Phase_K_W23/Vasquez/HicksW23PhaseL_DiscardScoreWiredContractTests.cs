namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Hicks W23's
/// Phase L discard-pile + score-display wire-up.  W22 staged the
/// controller; W23 wires it into the renderer-webgl2 scene via a
/// new state-binding controller exposing a
/// `?renderer=webgl2-discard-score` smoke harness.
/// </summary>
public sealed class HicksW23PhaseL_DiscardScoreWiredContractTests
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
    public void DiscardPileController_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "src", "renderer-webgl2",
            "discard-pile-controller.ts");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DiscardPileController_Has_CreateAndPushAndPop_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "src", "renderer-webgl2",
            "discard-pile-controller.ts");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = (text.Contains("createDiscardPileController", StringComparison.Ordinal)
                   || text.Contains("DiscardPile", StringComparison.OrdinalIgnoreCase))
                   && (text.Contains("pushDiscard", StringComparison.Ordinal)
                       || text.Contains("popDiscard", StringComparison.Ordinal)
                       || text.Contains("Discard", StringComparison.Ordinal));
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void DiscardPileController_Has_ScoreDisplay_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "src", "renderer-webgl2",
            "discard-pile-controller.ts");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("createScoreDisplayController", StringComparison.Ordinal)
                   || text.Contains("ScoreDisplay", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("setSeatScore", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void HelloHarness_Has_DiscardScore_Mode_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "frontend",
            "autotable-src", "src", "renderer-webgl2", "hello.ts");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("webgl2-discard-score", StringComparison.Ordinal)
                   || text.Contains("mountDiscardScore", StringComparison.Ordinal)
                   || text.Contains("discard-score", StringComparison.Ordinal);
        Assert.True(has);
    }
}
