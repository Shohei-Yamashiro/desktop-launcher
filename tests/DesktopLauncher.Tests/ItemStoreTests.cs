using System.IO;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher.Tests;

public sealed class ItemStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopLauncher.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void SaveThenLoad_KeepsHierarchyOrderAndExpansion()
    {
        var store = new ItemStore(_dir);
        var tree = new LauncherTree();
        var work = LauncherItem.CreateGroup("仕事");
        var docs = LauncherItem.CreateGroup("資料");
        work.IsExpanded = true;
        tree.Insert(work, null);
        tree.Insert(docs, work);
        tree.Insert(LauncherItem.CreateItem("メモ帳", @"C:\Windows\notepad.exe"), docs);
        tree.Insert(LauncherItem.CreateItem("example", "https://example.com/"), null);

        store.Save(tree);
        var loaded = new LauncherTree();
        store.Load(loaded);

        Assert.Equal(["仕事", "資料", "メモ帳", "example"], loaded.All().Select(n => n.Name).ToArray());
        var loadedWork = loaded.Roots[0];
        Assert.True(loadedWork.IsGroup);
        Assert.True(loadedWork.IsExpanded);
        Assert.False(loadedWork.Children[0].IsExpanded);
        var notepad = loadedWork.Children[0].Children[0];
        Assert.Equal(@"C:\Windows\notepad.exe", notepad.Path);
        Assert.Equal("仕事 / 資料", notepad.Location);
    }

    [Fact]
    public void Load_ReadsOldFlatFormat()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "items.json"), """
            [
              { "Name": "メモ帳", "Path": "C:\\Windows\\notepad.exe" },
              { "Name": "Windows", "Path": "C:\\Windows" }
            ]
            """);

        var tree = new LauncherTree();
        new ItemStore(_dir).Load(tree);

        Assert.Equal(["メモ帳", "Windows"], tree.Roots.Select(n => n.Name).ToArray());
        Assert.All(tree.Roots, n => Assert.False(n.IsGroup));
    }

    [Fact]
    public void Load_EmptyGroupStaysAGroup()
    {
        var store = new ItemStore(_dir);
        var tree = new LauncherTree();
        tree.Insert(LauncherItem.CreateGroup("空"), null);
        store.Save(tree);

        var loaded = new LauncherTree();
        store.Load(loaded);

        Assert.True(loaded.Roots.Single().IsGroup);
    }

    [Fact]
    public void PathsInsideAppFolder_AreSavedRelative_AndFollowTheFolderWhenMoved()
    {
        var oldApp = Path.Combine(_dir, "old");
        var store = new ItemStore(Path.Combine(oldApp, "data"));
        var tree = new LauncherTree();
        tree.Insert(LauncherItem.CreateItem("メモ", Path.Combine(store.LinksDirectory, "メモ.lnk")), null);
        tree.Insert(LauncherItem.CreateItem("メモ帳", @"C:\Windows\notepad.exe"), null);
        store.Save(tree);

        var json = File.ReadAllText(store.FilePath);
        Assert.Contains(@"data\\links\\メモ.lnk", json);
        Assert.DoesNotContain(oldApp.Replace(@"\", @"\\"), json);

        var newApp = Path.Combine(_dir, "new");
        Directory.Move(oldApp, newApp);
        var moved = new ItemStore(Path.Combine(newApp, "data"));
        var loaded = new LauncherTree();
        moved.Load(loaded);

        Assert.Equal(Path.Combine(newApp, "data", "links", "メモ.lnk"), loaded.Roots[0].Path);
        Assert.True(moved.IsInLinksDirectory(loaded.Roots[0].Path));
        Assert.Equal(@"C:\Windows\notepad.exe", loaded.Roots[1].Path);
    }

    [Fact]
    public void Load_BrokenFile_IsMovedAsideAndReported()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "items.json"), "{ not json");

        Assert.Throws<InvalidDataException>(() => new ItemStore(_dir).Load(new LauncherTree()));
        Assert.False(File.Exists(Path.Combine(_dir, "items.json")));
        Assert.Single(Directory.GetFiles(_dir, "items.broken-*.json"));
    }
}
