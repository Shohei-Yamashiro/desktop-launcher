using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class LetterBadgeTests
{
    [Theory]
    [InlineData("kintai.example.com", "K")]
    [InlineData("www.example.com", "E")]
    [InlineData("WWW.Github.com", "G")]
    [InlineData("例え.jp", "例")]
    [InlineData("localhost", "L")]
    [InlineData("", "?")]
    [InlineData("www.", "?")]
    public void Letter_IsTheFirstLetterWithoutWww(string host, string expected) =>
        Assert.Equal(expected, LetterBadge.Letter(host));

    [Fact]
    public void ColorIndex_IsStableAndSpread()
    {
        // A fixed value: the same host must keep its color in every run and on every PC.
        Assert.Equal(2, LetterBadge.ColorIndex("kintai.example.com", 8));
        Assert.Equal(2, LetterBadge.ColorIndex("KINTAI.example.com", 8));
        var colors = Enumerable.Range(0, 20).Select(i => LetterBadge.ColorIndex($"site{i}.example.com", 8)).Distinct().Count();
        Assert.True(colors > 1, "Different sites should not all get the same color.");
    }
}
