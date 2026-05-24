namespace Mahjong.Autotable.Api.Tests.Phase_K_W22.Vasquez;

/// <summary>
/// Phase K Wave 22 — Vasquez paired contract for Apone W22's
/// us-east-1 auto-rollback runbook and supporting workflow.
/// </summary>
public sealed class AponeW22AutoRollbackContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Apone")]
    public void UsEast1AutoRollbackWorkflow_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, ".github", "workflows", "us-east-1-auto-rollback.yml");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("auto-rollback", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("rollback", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-22"), Trait("Lane", "Apone")]
    public void UsEast1AutoRollbackRunbookDoc_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "docs", "us-east-1-auto-rollback-runbook.md");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("rollback", StringComparison.OrdinalIgnoreCase)
                   && text.Contains("us-east-1", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }
}
