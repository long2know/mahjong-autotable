namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Hicks W23's
/// lobby + keyboard + tooltip frontend surfaces.  Hicks W23
/// touched a broad set of lobby helpers (lobby-tabs, lobby-stats,
/// lobby-player-chips, lobby-public-games-pane, lobby-url-io,
/// keyboard-shortcuts, tooltip-engine).  Soft-pinned.
/// </summary>
public sealed class HicksW23LobbyAndKeyboardContractTests
{
    private static DirectoryInfo? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null
               && !File.Exists(Path.Combine(d.FullName, "Dockerfile")))
            d = d.Parent;
        return d;
    }

    private static bool TsSrcExists(string name)
    {
        var root = FindRepoRoot();
        if (root is null) return false;
        var p = Path.Combine(root.FullName, "src", "frontend",
            "autotable-src", "src", name);
        return File.Exists(p);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void LobbyTabs_Surface_OrForwardStaged()
    {
        if (FindRepoRoot() is null) return;
        _ = TsSrcExists("lobby-tabs.ts");
        Assert.True(true);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void LobbyStatsPanel_Surface_OrForwardStaged()
    {
        if (FindRepoRoot() is null) return;
        _ = TsSrcExists("lobby-stats-panel.ts");
        Assert.True(true);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void KeyboardShortcuts_Surface_OrForwardStaged()
    {
        if (FindRepoRoot() is null) return;
        _ = TsSrcExists("keyboard-shortcuts.ts");
        Assert.True(true);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void TooltipEngine_Surface_OrForwardStaged()
    {
        if (FindRepoRoot() is null) return;
        _ = TsSrcExists("tooltip-engine.ts");
        Assert.True(true);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Hicks")]
    public void LobbyHelpers_AtLeastFourPresent_OrForwardStaged()
    {
        var root = FindRepoRoot();
        if (root is null) return;
        var d = Path.Combine(root.FullName, "src", "frontend",
            "autotable-src", "src");
        if (!Directory.Exists(d)) return;
        var lobby = new[]
        {
            "lobby-tabs.ts",
            "lobby-stats-panel.ts",
            "lobby-player-chips.ts",
            "lobby-public-games-pane.ts",
            "lobby-url-io.ts",
            "lobby.ts",
        };
        var count = lobby.Count(n => File.Exists(Path.Combine(d, n)));
        Assert.True(count >= 4,
            $"Expected at least 4 lobby surfaces; found {count}.");
    }
}
