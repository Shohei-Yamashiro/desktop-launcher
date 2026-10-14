using System.IO;
using DesktopLauncher.Services;

namespace DesktopLauncher.Tests;

public sealed class ShortcutFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopLauncher.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string WriteUrlFile(string folder, string name, string url)
    {
        var path = Path.Combine(_dir, folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"[InternetShortcut]\r\nURL={url}\r\n");
        return path;
    }

    [Fact]
    public void IsShortcut_OnlyForExistingLnkAndUrlFiles()
    {
        var url = WriteUrlFile("src", "site.url", "https://example.com/");

        Assert.True(ShortcutFiles.IsShortcut(url));
        Assert.False(ShortcutFiles.IsShortcut(Path.Combine(_dir, "missing.lnk")));
        Assert.False(ShortcutFiles.IsShortcut(@"C:\Windows\notepad.exe"));
    }

    [Fact]
    public void CopyInto_AddsNumberWhenNameIsTaken()
    {
        var first = WriteUrlFile("a", "site.url", "https://example.com/a");
        var second = WriteUrlFile("b", "site.url", "https://example.com/b");
        var links = Path.Combine(_dir, "links");

        var copy1 = ShortcutFiles.CopyInto(first, links);
        var copy2 = ShortcutFiles.CopyInto(second, links);

        Assert.Equal(Path.Combine(links, "site.url"), copy1);
        Assert.Equal(Path.Combine(links, "site (2).url"), copy2);
        Assert.True(File.Exists(first));
        Assert.Equal("https://example.com/b", ShortcutFiles.ResolveTarget(copy2));
    }
}
