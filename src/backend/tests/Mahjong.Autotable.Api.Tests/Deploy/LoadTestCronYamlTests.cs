using System.Text.RegularExpressions;

namespace Mahjong.Autotable.Api.Tests.Deploy;

/// <summary>
/// Phase K Wave 1 — nightly load-test cron workflow contract (Vasquez).
///
/// <para>Apone's Phase K Wave 1 brief ships
/// <c>.github/workflows/load-test-nightly.yml</c> — a scheduled cron
/// workflow that runs <c>tests/load/</c> against a deployed staging
/// preview every night. Contract:
/// <list type="bullet">
///   <item>Triggered by <c>schedule.cron</c> at <b>02:00 UTC</b>
///         (i.e. <c>"0 2 * * *"</c>).</item>
///   <item>Has a <c>workflow_dispatch</c> manual override.</item>
///   <item>Invokes the load-test harness (e.g. <c>k6 run</c>,
///         <c>artillery</c>, or a shell wrapper under
///         <c>scripts/run-load-test.sh</c>).</item>
///   <item>Uploads results as an artifact for forensics.</item>
/// </list></para>
///
/// <para><b>Forward-staged.</b> Soft-pass when the workflow file isn't
/// yet present.</para>
/// </summary>
public class LoadTestCronYamlTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))
                && File.Exists(Path.Combine(dir.FullName, "Dockerfile")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate repo root from {AppContext.BaseDirectory}");
    }

    private static string? LoadWorkflow()
    {
        var root = LocateRepoRoot();
        foreach (var name in new[]
            {
                "load-test-nightly.yml",
                "load-test-nightly.yaml",
                "load-test.yml",
                "nightly-load.yml",
            })
        {
            var p = Path.Combine(root, ".github", "workflows", name);
            if (File.Exists(p)) return File.ReadAllText(p);
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────────────
    //  1. File present OR forward-staged
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Deploy"), Trait("Wave", "Phase-K-1")]
    public void LoadTest_Workflow_ExistsOrNotYetShipped()
    {
        var content = LoadWorkflow();
        if (content is null) return;
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    // ────────────────────────────────────────────────────────────────────
    //  2. Has the canonical workflow shape
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Deploy"), Trait("Wave", "Phase-K-1")]
    public void LoadTest_Workflow_HasCanonicalShape()
    {
        var content = LoadWorkflow();
        if (content is null) return;
        Assert.Matches(@"(?m)^name:\s*\S", content);
        Assert.Matches(@"(?m)^on:\s*", content);
        Assert.Matches(@"(?m)^jobs:\s*", content);
    }

    // ────────────────────────────────────────────────────────────────────
    //  3. Cron schedule fires at 02:00 UTC
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Deploy"), Trait("Wave", "Phase-K-1")]
    public void LoadTest_Workflow_CronAt02UTC()
    {
        var content = LoadWorkflow();
        if (content is null) return;
        var match = Regex.Match(content,
            @"cron:\s*['""](?<expr>[^'""]+)['""]", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "load-test-nightly.yml must carry a schedule.cron entry.");
        var expr = match.Groups["expr"].Value.Trim();
        var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(parts.Length == 5, $"cron expression must be 5 fields: '{expr}'");
        // parts[1] is the hour. Must be "2" (02:00).
        Assert.Equal("2", parts[1]);
        // parts[0] is minute. Should be "0" or "00".
        Assert.True(parts[0] == "0" || parts[0] == "00",
            $"Minute field must be 0 (02:00 UTC), got '{parts[0]}'.");
    }

    // ────────────────────────────────────────────────────────────────────
    //  4. workflow_dispatch manual trigger present
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Deploy"), Trait("Wave", "Phase-K-1")]
    public void LoadTest_Workflow_HasManualDispatch()
    {
        var content = LoadWorkflow();
        if (content is null) return;
        Assert.Contains("workflow_dispatch", content, StringComparison.OrdinalIgnoreCase);
    }

    // ────────────────────────────────────────────────────────────────────
    //  5. Load harness invoked (k6 / artillery / locust / wrapper script)
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Deploy"), Trait("Wave", "Phase-K-1")]
    public void LoadTest_Workflow_InvokesLoadHarness()
    {
        var content = LoadWorkflow();
        if (content is null) return;
        var hasHarness =
            Regex.IsMatch(content, @"\bk6\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(content, @"\bartillery\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(content, @"\blocust\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(content, @"run-load-test\.sh", RegexOptions.IgnoreCase)
            || content.Contains("tests/load/", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasHarness, "load-test-nightly.yml must invoke a load-test harness.");
    }

    // ────────────────────────────────────────────────────────────────────
    //  6. Uploads results as a workflow artifact
    // ────────────────────────────────────────────────────────────────────

    [Fact, Trait("Category", "Deploy"), Trait("Wave", "Phase-K-1")]
    public void LoadTest_Workflow_UploadsArtifact()
    {
        var content = LoadWorkflow();
        if (content is null) return;
        // Either upload-artifact action or aws s3 cp / gh artifact upload.
        var uploads =
            Regex.IsMatch(content, @"actions/upload-artifact@", RegexOptions.IgnoreCase)
            || Regex.IsMatch(content, @"\baws\s+s3\s+cp\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(content, @"\bgh\s+artifact\s+upload\b", RegexOptions.IgnoreCase);
        Assert.True(uploads,
            "load-test-nightly.yml must upload results (upload-artifact / s3 / gh artifact).");
    }
}
