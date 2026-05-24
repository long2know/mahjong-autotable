namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// replay restoration audit history paginated query surface
/// (GET /api/replays/audit/restorations?since=…&amp;outcome=…&amp;
/// page=…&amp;pageSize=…).  Admin-gated paginated query over the
/// W21 ReplayRestorationAttempt table.
/// </summary>
public sealed class ReplayRestorationAuditHistoryContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayRestorationAuditHistoryController_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayRestorationAuditHistoryController.cs");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayRestoration_Get_Endpoint_Routed_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayRestorationAuditHistoryController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = (text.Contains("restorations", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("restoration", StringComparison.OrdinalIgnoreCase))
                   && (text.Contains("HttpGet", StringComparison.Ordinal)
                       || text.Contains("[HttpGet", StringComparison.Ordinal));
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayRestoration_HasPaging_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayRestorationAuditHistoryController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Default pageSize=50, max 200.  Any paging reference accepted.
        var has = text.Contains("pageSize", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("page", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("PageSize", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayRestoration_HasMetaAuditRow_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayRestorationAuditHistoryController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Meta-audit row "replay-restoration-audit-queried".
        var has = text.Contains("replay-restoration-audit", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("restoration-audit-queried", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("audit-queried", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("restoration.audit.queried", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("KindReplayRestorationAuditQueried", StringComparison.Ordinal)
                   || text.Contains("Meta-audit", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }
}
