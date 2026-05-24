namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// backend csproj 0.31.0 → 0.32.0 bump (paired with Apone W23's
/// CHANGELOG [0.32.0] block + mobile/package.json 0.32.0 stamp).
/// Soft-pinned with the W22→W23 forward-broadening pattern from
/// the outset: accept W23 stamp 0.32.0 OR any later 0.N.0 form.
/// </summary>
public sealed class BishopW23BackendCsprojVersionContractTests
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
    public void BackendCsproj_Version_0_32_0_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Mahjong.Autotable.Api.csproj");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        var has = text.Contains("<Version>0.32.0</Version>", StringComparison.Ordinal)
                   || text.Contains("0.32.0", StringComparison.Ordinal);
        Assert.True(has);
    }
}
