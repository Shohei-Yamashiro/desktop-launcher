using System.IO;
using DesktopLauncher.Models;
using static DesktopLauncher.EditWindow;

namespace DesktopLauncher.Tests;

public class EditTests
{
    [Fact]
    public void KindOf_FollowsWhatThePathIs()
    {
        var folder = Directory.CreateTempSubdirectory("dl-edit-").FullName;
        try
        {
            var file = Path.Combine(folder, "a.txt");
            File.WriteAllText(file, "");

            Assert.Equal(EditKind.Group, KindOf(LauncherItem.CreateGroup("g")));
            Assert.Equal(EditKind.Url, KindOf(LauncherItem.CreateItem("u", "https://example.com")));
            Assert.Equal(EditKind.Folder, KindOf(LauncherItem.CreateItem("d", folder)));
            Assert.Equal(EditKind.File, KindOf(LauncherItem.CreateItem("f", file)));
            Assert.Equal(EditKind.File, KindOf(LauncherItem.CreateItem("m", Path.Combine(folder, "missing.exe"))));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void SettingPath_NotifiesPathAndIsUrl()
    {
        var item = LauncherItem.CreateItem("x", @"C:\x.exe");
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.Path = "https://example.com";

        Assert.True(item.IsUrl);
        Assert.Contains(nameof(LauncherItem.Path), changed);
        Assert.Contains(nameof(LauncherItem.IsUrl), changed);
    }

    [Fact]
    public void SettingPath_OnGroup_Throws()
    {
        var group = LauncherItem.CreateGroup("g");

        Assert.Throws<InvalidOperationException>(() => group.Path = @"C:\x");
    }
}
