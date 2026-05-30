using Music.Service.Services;
using Xunit;

namespace BuildingBlocks.Tests;

public class PathContainmentTests
{
    private static readonly string BaseDir =
        Path.Combine(Path.GetTempPath(), "attrition-music-temp");

    [Theory]
    [InlineData("temp_abc123.mp3")]
    [InlineData("temp-cover-xyz.jpg")]
    public void ResolveContainedPath_AcceptsPlainFileNames(string candidate)
    {
        var resolved = MusicHelpers.ResolveContainedPath(BaseDir, candidate);
        Assert.NotNull(resolved);
        Assert.EndsWith(candidate, resolved);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\appsettings.json")]
    [InlineData("/etc/shadow")]
    [InlineData("subdir/../../escape.txt")]
    public void ResolveContainedPath_CollapsesTraversalToFileName(string candidate)
    {
        // GetFileName strips directory components, so a traversal attempt either resolves
        // to a bare name inside BaseDir or yields null — never a path outside BaseDir.
        var resolved = MusicHelpers.ResolveContainedPath(BaseDir, candidate);
        if (resolved != null)
        {
            var baseFull = Path.GetFullPath(BaseDir);
            Assert.StartsWith(baseFull, resolved);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveContainedPath_RejectsEmpty(string? candidate)
    {
        Assert.Null(MusicHelpers.ResolveContainedPath(BaseDir, candidate));
    }

    [Theory]
    [InlineData("song.mp3", true)]
    [InlineData("song.FLAC", true)]
    [InlineData("payload.svg", false)]
    [InlineData("payload.html", false)]
    [InlineData("noext", false)]
    public void IsAllowedAudioExtension(string name, bool expected)
    {
        Assert.Equal(expected, MusicHelpers.IsAllowedAudioExtension(name));
    }

    [Theory]
    [InlineData("cover.jpg", true)]
    [InlineData("cover.PNG", true)]
    [InlineData("evil.svg", false)]
    [InlineData("evil.html", false)]
    public void IsAllowedImageExtension(string name, bool expected)
    {
        Assert.Equal(expected, MusicHelpers.IsAllowedImageExtension(name));
    }
}
