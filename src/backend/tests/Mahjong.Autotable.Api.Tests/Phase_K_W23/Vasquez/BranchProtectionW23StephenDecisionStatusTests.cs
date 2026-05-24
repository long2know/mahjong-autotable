namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez §4.8 Stephen-decision tree
/// 15-wave deferral arc status pin.  Branch protection on `main`
/// is STILL not configured for the lane-discipline check at W23
/// close.  At W22 close the 14-wave deferral arc-trigger language
/// from W21 was SATISFIED ("consider Coordinator-direct escalation
/// at 14-wave").  W23 either ratifies a Coord-direct escalation
/// (if one has landed) OR extends to a 15-wave deferral with an
/// explicit Stephen-decision-requirement note.
/// </summary>
public sealed class BranchProtectionW23StephenDecisionStatusTests
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
    public void HandoffDoc_Section_4_8_StephenDecision_Present()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        Assert.Contains("4.8", text);
        Assert.Contains("Stephen", text);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_W23_DeferralArc_15Wave_Recorded()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        // §11.4 forward-stage carry-over should mention the 15-wave
        // deferral arc (W7 → W23) at W23 close.
        var has = text.Contains("15-wave deferral", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("15 waves wide", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_W23_Section_4_9_Status_Recorded()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        // Either "No §4.9 opened" (still deferred) OR a §4.9 entry
        // (escalation invoked).  Both forms are recorded explicitly.
        var has = text.Contains("No §4.9", StringComparison.Ordinal)
                   || text.Contains("no §4.9", StringComparison.Ordinal)
                   || text.Contains("No 4.9", StringComparison.Ordinal)
                   || text.Contains("§4.9 —", StringComparison.Ordinal)
                   || text.Contains("§4.9 — ", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_W23_CoordDirectEscalation_Trigger_Recorded()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
        var text = File.ReadAllText(p);
        // W23 §11 must explicitly record that the W21-handoff
        // "consider Coordinator-direct escalation at 14-wave"
        // trigger was satisfied at W22 close and the W23
        // disposition (ratify OR extend) is captured.
        var has = text.Contains("Coordinator-direct escalation", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Coord-direct escalation", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("14-wave", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }
}
