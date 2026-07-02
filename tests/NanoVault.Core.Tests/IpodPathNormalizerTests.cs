using NanoVault.Core.Policies;
using Xunit;

namespace NanoVault.Core.Tests;

public class IpodPathNormalizerTests
{
    [Fact]
    public void Colon_separated_ipod_path_becomes_local_relative_path()
    {
        var result = IpodPathNormalizer.ToRelativePath(":iPod_Control:Music:F00:ABCD.mp3");
        var expected = string.Join(Path.DirectorySeparatorChar, "iPod_Control", "Music", "F00", "ABCD.mp3");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":::")]
    public void Empty_or_invalid_paths_return_null(string? input) =>
        Assert.Null(IpodPathNormalizer.ToRelativePath(input));

    [Fact]
    public void Traversal_segments_are_rejected()
    {
        Assert.Null(IpodPathNormalizer.ToRelativePath(":..:secret:file.mp3"));
        Assert.Null(IpodPathNormalizer.ToRelativePath(":iPod_Control:.:x.mp3"));
    }

    [Fact]
    public void Comparable_key_matches_across_separator_styles()
    {
        var fromDb = IpodPathNormalizer.ToComparableKey(":iPod_Control:Music:F00:ABCD.MP3");
        var fromWindows = IpodPathNormalizer.ToComparableKey(@"iPod_Control\Music\F00\abcd.mp3");
        var fromUnix = IpodPathNormalizer.ToComparableKey("iPod_Control/Music/F00/Abcd.Mp3");

        Assert.Equal(fromDb, fromWindows);
        Assert.Equal(fromWindows, fromUnix);
    }

    [Fact]
    public void Comparable_key_collapses_duplicate_separators()
    {
        Assert.Equal("a/b", IpodPathNormalizer.ToComparableKey("a//b"));
        Assert.Equal("a/b", IpodPathNormalizer.ToComparableKey("/a/b/"));
    }
}
