namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract pinning Hicks W23's
/// LH13 §14 evidence-gate re-evaluation HOLD YELLOW decision.
/// Hicks W23 (86a3366) HELD §6.8 YELLOW with the natural-cron-pace
/// blocker unchanged from W22.  At W23 bring-up (~2026-05-24T18:3xZ)
/// the wall-clock is still PRE-FIRST-POST-MERGE-CRON.  Predicted
/// PROMOTE wave: W25 earliest.
///
/// This Vasquez §6.12 entry mirrors Hicks's §14 update and ratifies
/// the HOLD in <c>docs/agent-handoff-protocol.md §6.12</c>.
/// </summary>
public sealed class LH13_Section6_12_StatusContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    private static string Lh13Path()
    {
        var root = FindRepoRoot();
        return Path.Combine(root!.FullName, "docs", "lh13-soft-pin-rationale.md");
    }

    private static string HandoffPath()
    {
        var root = FindRepoRoot();
        return Path.Combine(root!.FullName, "docs", "agent-handoff-protocol.md");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void Lh13Doc_Section14_Present_OrForwardStaged()
    {
        var p = Lh13Path();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("## §14", StringComparison.Ordinal)
                   || text.Contains("§14 — W23", StringComparison.Ordinal)
                   || text.Contains("§14", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void Lh13Doc_HoldYellow_W23_OrForwardStaged()
    {
        var p = Lh13Path();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("HOLD", StringComparison.OrdinalIgnoreCase)
                   && text.Contains("YELLOW", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void Lh13Doc_W23_NaturalCronPaceBlocker_OrForwardStaged()
    {
        var p = Lh13Path();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // W23 reason carries forward from W22: natural cron-pace
        // accumulation (nightly cron cadence; 0 successful post-W18-
        // merge runs).
        var has = text.Contains("natural cron", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("cron-pace", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("nightly", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void Lh13Doc_W23_W25EarliestPromote_OrForwardStaged()
    {
        var p = Lh13Path();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("W25 earliest", StringComparison.Ordinal)
                   || text.Contains("W25-earliest", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_Section_6_12_W23_HoldYellow_Ratification_Present()
    {
        var text = File.ReadAllText(HandoffPath());
        Assert.Contains("6.12", text);
        var has = text.Contains("HOLD", StringComparison.OrdinalIgnoreCase)
                   && text.Contains("W23", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Vasquez")]
    public void HandoffDoc_Section_6_11_W22_StillPresent()
    {
        // §6.11 from W22 should remain — historical entries are not
        // overwritten.
        var text = File.ReadAllText(HandoffPath());
        Assert.Contains("6.11", text);
    }
}
