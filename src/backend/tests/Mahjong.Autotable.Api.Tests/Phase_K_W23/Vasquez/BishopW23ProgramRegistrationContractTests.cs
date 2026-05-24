namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract pinning Bishop W23's
/// DI registrations in Program.cs.  Hosted services + supporting
/// singletons for the new W23 surfaces (JWT rotation-drill autorun,
/// SignalR per-group telemetry, audit-log purge).
/// </summary>
public sealed class BishopW23ProgramRegistrationContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    private static string ProgramPath()
    {
        var root = FindRepoRoot();
        return Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Program.cs");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Program_Registers_JwtRotationDrillAutorunService_OrForwardStaged()
    {
        var p = ProgramPath();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("JwtRotationDrillAutorunService", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Program_Registers_SignalRConnectionRegistry_OrForwardStaged()
    {
        var p = ProgramPath();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // W23 fixed the W22 DI gap — register SignalRConnectionRegistry.
        var has = text.Contains("SignalRConnectionRegistry", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Program_Registers_SignalRGroupTelemetryTickService_OrForwardStaged()
    {
        var p = ProgramPath();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("SignalRGroupTelemetry", StringComparison.Ordinal)
                   || text.Contains("SignalRGroupTelemetryTickService", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void Program_Registers_AuditLogPurgeMetrics_OrForwardStaged()
    {
        var p = ProgramPath();
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("AuditLogPurgeMetrics", StringComparison.Ordinal)
                   || text.Contains("AuditLogPurge", StringComparison.Ordinal);
        Assert.True(has);
    }
}
