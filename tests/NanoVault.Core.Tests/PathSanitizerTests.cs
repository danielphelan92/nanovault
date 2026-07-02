using NanoVault.Core.Policies;
using Xunit;

namespace NanoVault.Core.Tests;

public class PathSanitizerTests
{
    [Theory]
    [InlineData("AC/DC", "AC-DC")]
    [InlineData("What?", "What")]
    [InlineData("Track: One", "Track- One")]
    [InlineData("A<B>C", "A(B)C")]
    [InlineData("He said \"hi\"", "He said 'hi'")]
    [InlineData("a|b\\c", "a-b-c")]
    [InlineData("Stars ****", "Stars")]
    public void Replaces_invalid_characters_readably(string input, string expected) =>
        Assert.Equal(expected, PathSanitizer.SanitizeComponent(input, "Fallback"));

    [Theory]
    [InlineData("Trailing dot.")]
    [InlineData("Trailing space ")]
    [InlineData("Both. . ")]
    public void Trims_trailing_periods_and_spaces(string input)
    {
        var result = PathSanitizer.SanitizeComponent(input, "Fallback");
        Assert.False(result.EndsWith('.'));
        Assert.False(result.EndsWith(' '));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    [InlineData("lpt9")]
    public void Reserved_windows_names_are_made_safe(string reserved)
    {
        var result = PathSanitizer.SanitizeComponent(reserved, "Fallback");
        Assert.False(PathSanitizer.IsReservedName(result));
        Assert.NotEqual(reserved, result);
    }

    [Fact]
    public void Reserved_name_with_extension_is_detected()
    {
        Assert.True(PathSanitizer.IsReservedName("CON.mp3"));
        var fileName = PathSanitizer.SanitizeFileName("CON", ".mp3", "Track");
        Assert.False(PathSanitizer.IsReservedName(fileName));
        Assert.EndsWith(".mp3", fileName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("???")]
    [InlineData("***")]
    public void Empty_or_hopeless_input_returns_fallback(string? input) =>
        Assert.Equal("Fallback", PathSanitizer.SanitizeComponent(input, "Fallback"));

    [Fact]
    public void Very_long_components_are_truncated()
    {
        var longName = new string('x', 500);
        var result = PathSanitizer.SanitizeComponent(longName, "Fallback");
        Assert.True(result.Length <= PathSanitizer.DefaultMaxComponentLength);
    }

    [Fact]
    public void Ordinary_punctuation_is_preserved()
    {
        Assert.Equal("Don't Stop Me Now!", PathSanitizer.SanitizeComponent("Don't Stop Me Now!", "F"));
        Assert.Equal("Track (Live) [2004] #1 & more…", PathSanitizer.SanitizeComponent("Track (Live) [2004] #1 & more…", "F"));
    }

    [Fact]
    public void Control_characters_are_removed()
    {
        var result = PathSanitizer.SanitizeComponent("badname\ttab", "Fallback");
        Assert.DoesNotContain(result, c => char.IsControl(c));
    }

    [Fact]
    public void Whitespace_is_collapsed()
    {
        Assert.Equal("a b", PathSanitizer.SanitizeComponent("a      b", "Fallback"));
    }

    [Theory]
    [InlineData("MP3", ".mp3")]
    [InlineData(".M4A", ".m4a")]
    [InlineData(" .mp3 ", ".mp3")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Extensions_are_normalised(string? input, string expected) =>
        Assert.Equal(expected, PathSanitizer.NormalizeExtension(input));

    [Fact]
    public void File_name_length_budget_accounts_for_extension()
    {
        var result = PathSanitizer.SanitizeFileName(new string('a', 300), ".mp3", "Track", 50);
        Assert.True(result.Length <= 50);
        Assert.EndsWith(".mp3", result);
    }
}
