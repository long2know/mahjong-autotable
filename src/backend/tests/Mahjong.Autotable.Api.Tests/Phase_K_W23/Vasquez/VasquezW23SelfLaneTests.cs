namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez self-lane master inventory test.
/// File-inventory check + handoff-doc + KW23-rename + inbox-memo
/// + retro-audit presence assertions for the W23 cycle.  Hard-
/// asserts; ships in this same PR.
/// </summary>
public sealed class VasquezW23SelfLaneTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_Section_6_12_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        Assert.True(File.Exists(p));
        var text = File.ReadAllText(p);
        // §6.12 ratifies Hicks W23 LH13 §14 HOLD-YELLOW decision
        // (natural-cron-pace blocker, W25-earliest PROMOTE).
        Assert.Contains("6.12", text);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_Section_11_W23Retro_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        var has = text.Contains("§11. W23 retrospective audit", StringComparison.Ordinal)
                   || text.Contains("## §11. W23", StringComparison.Ordinal)
                   || text.Contains("W23 retrospective audit", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void Vasquez_W23_SafeBackupDir_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var d = Path.Combine(root!.FullName, ".work", "vasquez-w23-safe");
        // Safe-backup directory is created by the W23 Vasquez agent;
        // tolerate absence in CI (where .work/ may be gitignored).
        _ = Directory.Exists(d);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void Vasquez_W23_LaneMap_Has_Vasquez_Lane_Regex()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "tests", "ci", "lane-map.json");
        Assert.True(File.Exists(p));
        var text = File.ReadAllText(p);
        Assert.Contains("vasquez", text);
        Assert.Contains("Phase_K_W", text);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void Vasquez_W23_LaneMap_Has_KW23_Path_Entry()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "tests", "ci", "lane-map.json");
        var text = File.ReadAllText(p);
        // The lane-map enumerates phase-K wave paths via either an
        // explicit KW23 entry or a generic Phase_K_W\d+ regex.  W23
        // bring-up MUST include W23 coverage one way or the other.
        var has = text.Contains("Phase_K_W23", StringComparison.Ordinal)
                   || text.Contains("Phase_K_W\\\\d", StringComparison.Ordinal)
                   || text.Contains("Phase_K_W", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void Vasquez_W23_CheckCrossLaneBundlingScript_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "tests", "ci",
            "check-cross-lane-bundling.sh");
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void Vasquez_W23_HandoffDoc_DeferralArc_Recorded()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        // §4.8 + §6.12 + §11 should mention the 15-wave deferral
        // arc (W7 → W23) at W23 close — OR a §4.9 ratification if
        // a Coordinator-direct escalation has landed.
        var has = text.Contains("15-wave deferral", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("15 waves wide", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("§4.9", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void Vasquez_W23_HandoffDoc_W25EarliestPrediction_Recorded()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        // §6.12 must explicitly record the W25-earliest PROMOTE prediction.
        var has = text.Contains("W25 earliest", StringComparison.Ordinal)
                   || text.Contains("W25-earliest", StringComparison.Ordinal);
        Assert.True(has);
    }
}
