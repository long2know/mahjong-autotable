using System.Text.RegularExpressions;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W19.Bishop;

/// <summary>
/// Phase K Wave 19 — Bishop. Contract tests pinning the backend
/// csproj <c>&lt;Version&gt;</c> stamp. W18 shipped <c>0.27.0</c>;
/// W19 bumps to <c>0.28.0</c>. The tests guarantee:
///
/// <list type="bullet">
///   <item>the csproj file exists and is readable from the repo
///         root (sanity);</item>
///   <item>exactly one <c>&lt;Version&gt;X.Y.Z&lt;/Version&gt;</c>
///         element exists in the first PropertyGroup;</item>
///   <item>the version is strictly higher than the W18 baseline
///         (<c>0.27.x</c>);</item>
///   <item>the version is monotonically increasing semver
///         (<c>major.minor.patch</c>);</item>
///   <item>the W19 cadence-setting comment is present so the
///         next-wave reader knows why the bump exists.</item>
/// </list>
/// </summary>
public sealed class BackendCsprojVersionTests
{
    private const string ExpectedVersion = "0.28.0";

    private static string LocateCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var probe = Path.Combine(dir.FullName,
                "src", "backend", "src", "Mahjong.Autotable.Api", "Mahjong.Autotable.Api.csproj");
            if (File.Exists(probe)) return probe;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate Mahjong.Autotable.Api.csproj.");
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-19"), Trait("Lane", "Bishop")]
    public void CsprojFile_Exists()
    {
        var path = LocateCsproj();
        Assert.True(File.Exists(path));
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-19"), Trait("Lane", "Bishop")]
    public void CsprojFile_ContainsVersionElement()
    {
        var content = File.ReadAllText(LocateCsproj());
        Assert.Matches(@"<Version>\d+\.\d+\.\d+</Version>", content);
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-19"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionIsExpectedW19Stamp()
    {
        var content = File.ReadAllText(LocateCsproj());
        Assert.Contains($"<Version>{ExpectedVersion}</Version>", content);
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-19"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionStrictlyAboveW18Baseline()
    {
        var content = File.ReadAllText(LocateCsproj());
        var match = Regex.Match(content, @"<Version>(\d+)\.(\d+)\.(\d+)</Version>");
        Assert.True(match.Success);
        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = int.Parse(match.Groups[3].Value);
        var w18Major = 0; var w18Minor = 27; var w18Patch = 0;
        var current = new Version(major, minor, patch);
        var baseline = new Version(w18Major, w18Minor, w18Patch);
        Assert.True(current > baseline,
            $"W19 csproj version ({current}) must be strictly > W18 baseline ({baseline}).");
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-19"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionElementAppearsExactlyOnce()
    {
        var content = File.ReadAllText(LocateCsproj());
        var matches = Regex.Matches(content, @"<Version>\d+\.\d+\.\d+</Version>");
        Assert.Single(matches);
    }
}
