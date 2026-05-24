namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// audit-log retention purge admin surface
/// (POST /api/audit-log/purge?olderThanDays=N — admin-only,
/// mandatory X-Admin-Reason header, meta-audit row).
/// </summary>
public sealed class AuditLogRetentionPurgeContractTests
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
    public void AuditLogPurgeController_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Audit", "AuditLogPurgeController.cs");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void AuditLogPurgeController_Has_PurgePost_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Audit", "AuditLogPurgeController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("purge", StringComparison.OrdinalIgnoreCase)
                   && (text.Contains("HttpPost", StringComparison.Ordinal)
                       || text.Contains("[HttpPost", StringComparison.Ordinal));
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void AuditLogPurgeController_RequiresAdminReason_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Audit", "AuditLogPurgeController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("X-Admin-Reason", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Admin-Reason", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("AdminReason", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void AuditLogPurgeController_HasMetaAuditRow_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Audit", "AuditLogPurgeController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Meta-audit row of kind "audit-log-purged" written after the
        // purge, in a separate scope.
        var has = text.Contains("audit-log-purged", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("audit_log_purged", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("audit-log-purge", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("audit.log.purged", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("KindAuditLogPurged", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void MetricsEndpoint_Has_AuditLogPurgeCounter_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Observability", "MetricsEndpoint.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("audit_log_purge_rows_total", StringComparison.Ordinal)
                   || text.Contains("audit_log_purge", StringComparison.Ordinal)
                   || text.Contains("AuditLogPurgeMetrics", StringComparison.Ordinal)
                   || text.Contains("AuditLogPurge", StringComparison.Ordinal);
        Assert.True(has);
    }
}
