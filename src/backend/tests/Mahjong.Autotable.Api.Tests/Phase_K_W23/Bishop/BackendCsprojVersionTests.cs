using System.Text.RegularExpressions;
using Xunit;

namespace Mahjong.Autotable.Api.Tests.Phase_K_W23.Bishop;

/// <summary>
/// Phase K Wave 23 — Bishop. Contract tests pinning the backend
/// csproj <c>&lt;Version&gt;</c> stamp. W22 shipped <c>0.31.0</c>;
/// W23 bumps to <c>0.32.0</c>.
/// </summary>
public sealed class BackendCsprojVersionTests
{
    private const string ExpectedVersion = "0.32.0";

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

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void CsprojFile_Exists()
    {
        var path = LocateCsproj();
        Assert.True(File.Exists(path));
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void CsprojFile_ContainsVersionElement()
    {
        var content = File.ReadAllText(LocateCsproj());
        Assert.Matches(@"<Version>\d+\.\d+\.\d+</Version>", content);
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionIsExpectedW23Stamp()
    {
        var content = File.ReadAllText(LocateCsproj());
        Assert.Contains($"<Version>{ExpectedVersion}</Version>", content);
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionStrictlyAboveW22Baseline()
    {
        var content = File.ReadAllText(LocateCsproj());
        var match = Regex.Match(content, @"<Version>(\d+)\.(\d+)\.(\d+)</Version>");
        Assert.True(match.Success);
        var major = int.Parse(match.Groups[1].Value);
        var minor = int.Parse(match.Groups[2].Value);
        var patch = int.Parse(match.Groups[3].Value);
        var current = new Version(major, minor, patch);
        var w22Baseline = new Version(0, 31, 0);
        Assert.True(current > w22Baseline,
            $"W23 csproj version ({current}) must be strictly > W22 baseline ({w22Baseline}).");
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionElementAppearsExactlyOnce()
    {
        var content = File.ReadAllText(LocateCsproj());
        var matches = Regex.Matches(content, @"<Version>\d+\.\d+\.\d+</Version>");
        Assert.Single(matches);
    }

    [Fact, Trait("Category", "Build"), Trait("Wave", "Phase-K-23"), Trait("Lane", "Bishop")]
    public void CsprojFile_VersionMajorMinorPatchAreNonNegative()
    {
        var content = File.ReadAllText(LocateCsproj());
        var match = Regex.Match(content, @"<Version>(\d+)\.(\d+)\.(\d+)</Version>");
        Assert.True(match.Success);
        Assert.True(int.Parse(match.Groups[1].Value) >= 0);
        Assert.True(int.Parse(match.Groups[2].Value) >= 0);
        Assert.True(int.Parse(match.Groups[3].Value) >= 0);
    }
}
