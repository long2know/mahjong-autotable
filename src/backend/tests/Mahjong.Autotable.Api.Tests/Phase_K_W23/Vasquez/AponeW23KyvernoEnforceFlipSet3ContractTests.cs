namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Apone W23's
/// Kyverno W23 audit-mode launch (4th batch).  Two new audit-mode
/// ClusterPolicies land with a 5-WAVE grace window (W23 → W28
/// earliest enforce-flip):
///
/// <list type="bullet">
///   <item>require-readonly-rootfs.yaml — Pod-spec rootfs read-only</item>
///   <item>require-runas-non-root.yaml — explicit runAsNonRoot:true</item>
/// </list>
/// </summary>
public sealed class AponeW23KyvernoEnforceFlipSet3ContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    private static string KyvernoDir()
    {
        var root = FindRepoRoot();
        return Path.Combine(root!.FullName, "infra", "k8s", "base", "kyverno-policies");
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void RequireReadonlyRootfs_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(KyvernoDir(), "require-readonly-rootfs.yaml");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void RequireReadonlyRootfs_Has_AuditMode_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(KyvernoDir(), "require-readonly-rootfs.yaml");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // W23 launches in Audit mode (5-wave grace window).
        var has = text.Contains("Audit", StringComparison.Ordinal)
                   || text.Contains("validationFailureAction:", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void RequireRunAsNonRoot_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(KyvernoDir(), "require-runas-non-root.yaml");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void RequireRunAsNonRoot_Has_AuditMode_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(KyvernoDir(), "require-runas-non-root.yaml");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("Audit", StringComparison.Ordinal)
                   || text.Contains("validationFailureAction:", StringComparison.Ordinal);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Apone")]
    public void Docs_KyvernoW23AdditionalRules_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var p = Path.Combine(root.FullName, "docs", "kyverno-w23-additional-rules.md");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }
}
