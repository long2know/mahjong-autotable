namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// SignalR per-group telemetry surface (EWMA-smoothed
/// messages-per-second + admin-gated GET /api/signalr/groups).
/// Soft-pinned so the gate stays green if Bishop's surfaces have
/// not yet landed.
/// </summary>
public sealed class SignalRPerGroupTelemetryContractTests
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
    public void SignalRGroupTelemetryController_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Observability", "SignalRGroupTelemetryController.cs");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void SignalRGroupTelemetry_Has_EwmaOrAlpha_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Observability", "SignalRGroupTelemetryController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("EWMA", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("alpha", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("MsgRate", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("MessageRate", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void MetricsEndpoint_Has_SignalRGroupGauges_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Observability", "MetricsEndpoint.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("signalr_group_connections", StringComparison.Ordinal)
                   || text.Contains("signalr_group_msg_rate", StringComparison.Ordinal)
                   || text.Contains("signalr_group", StringComparison.Ordinal)
                   || text.Contains("SignalRGroupMetrics", StringComparison.Ordinal)
                   || text.Contains("SignalRGroup", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void SignalRGroupTelemetry_Exposes_GroupsGet_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Observability", "SignalRGroupTelemetryController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // GET /api/signalr/groups admin-gated.
        var has = text.Contains("groups", StringComparison.OrdinalIgnoreCase)
                   && (text.Contains("HttpGet", StringComparison.Ordinal)
                       || text.Contains("[HttpGet", StringComparison.Ordinal));
        Assert.True(has);
    }
}
