using System.IO;
using DesktopLauncher.Services;

namespace DesktopLauncher.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopLauncher.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void RowSpacing_IsSavedByName()
    {
        var store = new SettingsStore(_dir);
        store.Save(new AppSettings { RowSpacing = RowSpacing.Relaxed });

        Assert.Contains("\"Relaxed\"", File.ReadAllText(store.FilePath));
        Assert.Equal(RowSpacing.Relaxed, store.Load().RowSpacing);
    }

    [Fact]
    public void OlderFileWithoutNewSettings_UsesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "MiniLeft": 10, "MiniTop": 20 }""");

        var settings = new SettingsStore(_dir).Load();

        Assert.Equal(10, settings.MiniLeft);
        Assert.Equal(RowSpacing.Normal, settings.RowSpacing);
        Assert.True(settings.MoveShortcutsToLauncher);
    }

    [Theory]
    [InlineData(RowSpacing.Compact, 1)]
    [InlineData(RowSpacing.Normal, 3)]
    [InlineData(RowSpacing.Relaxed, 6)]
    public void VerticalPadding_GrowsWithSpacing(RowSpacing spacing, double expected) =>
        Assert.Equal(expected, spacing.VerticalPadding());
}
