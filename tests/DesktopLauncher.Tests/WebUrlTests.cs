using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class WebUrlTests
{
    [Theory]
    [InlineData("https://example.com/docs?q=1", "https://example.com/docs?q=1")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("  https://example.com/  ", "https://example.com/")]
    [InlineData("example.com/path", "https://example.com/path")]
    [InlineData("www.example.co.jp", "https://www.example.co.jp")]
    [InlineData("localhost:3000", "https://localhost:3000")]
    [InlineData("http://localhost:8080/app", "http://localhost:8080/app")]
    [InlineData("例え.jp", "https://例え.jp")]
    public void TryNormalize_AcceptsWebAddresses(string text, string expected)
    {
        Assert.True(WebUrl.TryNormalize(text, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("memo")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///C:/Windows")]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("example .com")]
    [InlineData(".example.com")]
    public void TryNormalize_RejectsOthers(string? text)
    {
        Assert.False(WebUrl.TryNormalize(text, out var url));
        Assert.Equal("", url);
    }

    [Theory]
    [InlineData("https://example.com/docs", "https://example.com/docs")]
    [InlineData("  http://localhost:8080/app  ", "http://localhost:8080/app")]
    public void TryParseCopied_AcceptsUrlsWithScheme(string text, string expected)
    {
        Assert.True(WebUrl.TryParseCopied(text, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("imap.gmail.com")]
    [InlineData("example.com/docs")]
    [InlineData("ftp://example.com")]
    [InlineData("https://memo")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseCopied_NeedsHttpOrHttps(string? text)
    {
        Assert.False(WebUrl.TryParseCopied(text, out var url));
        Assert.Equal("", url);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("example.com", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData(@"C:\Windows", false)]
    public void IsWebUrl_OnlyHttpAndHttps(string text, bool expected) =>
        Assert.Equal(expected, WebUrl.IsWebUrl(text));

    [Theory]
    [InlineData("https://github.com/dotnet/wpf", "github.com")]
    [InlineData("https://www.example.co.jp/", "www.example.co.jp")]
    [InlineData("http://localhost:8080/app", "localhost")]
    [InlineData("https://例え.jp/", "例え.jp")]
    public void DefaultName_IsHost(string url, string expected) =>
        Assert.Equal(expected, WebUrl.DefaultName(url));
}
