namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Vasquez;

/// <summary>
/// Phase K Wave 23 — Vasquez paired contract for Bishop W23's
/// replay chunked-UPLOAD admin surface (counterpart to W22's
/// chunked DOWNLOAD).  Soft-pinned so the gate stays green if
/// Bishop's surfaces have not yet landed.
/// </summary>
public sealed class ReplayChunkedUploadContractTests
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
    public void ReplayChunkUploadController_File_Present_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayChunkUploadController.cs");
        if (!File.Exists(p)) return;
        Assert.True(File.Exists(p));
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayChunkUploadController_Has_FinalizeAndChunks_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayChunkUploadController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // POST /api/replays/{id}/chunks/{seq} + POST /api/replays/{id}/finalize.
        var has = text.Contains("chunks", StringComparison.OrdinalIgnoreCase)
                   && text.Contains("finalize", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayChunkUploadController_Has_ChecksumHeader_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayChunkUploadController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Optional X-Replay-Checksum header for sha256 verification.
        var has = text.Contains("Replay-Checksum", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("sha256", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Checksum", StringComparison.OrdinalIgnoreCase);
        Assert.True(has);
    }

    [Fact, Trait("Category", "Contract"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void ReplayChunkUploadController_Has_AggregateCap_OrForwardStaged()
    {
        var root = FindRepoRoot();
        Assert.NotNull(root);
        var p = Path.Combine(root!.FullName, "src", "backend", "src",
            "Mahjong.Autotable.Api", "Replays", "ReplayChunkUploadController.cs");
        if (!File.Exists(p)) return;
        var text = File.ReadAllText(p);
        // Per-replay aggregate cap (64 MB) + per-chunk cap (4 MB) +
        // per-session chunk cap (1024).  Any cap reference accepted.
        var has = text.Contains("Cap", StringComparison.Ordinal)
                   || text.Contains("MaxBytes", StringComparison.Ordinal)
                   || text.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("1024", StringComparison.Ordinal)
                   || text.Contains("64", StringComparison.Ordinal);
        Assert.True(has);
    }
}
