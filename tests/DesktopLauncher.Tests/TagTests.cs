using System.IO;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher.Tests;

public sealed class TagTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "DesktopLauncher.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void SetTags_TrimsDropsBlanksAndDuplicates_AndSorts()
    {
        var item = LauncherItem.CreateItem("メモ帳", @"C:\Windows\notepad.exe");

        item.SetTags([" 仕事 ", "Dev", "", "dev", "仕事", "  "]);

        Assert.Equal(["Dev", "仕事"], item.Tags);
        Assert.True(item.HasTag("DEV"));
    }

    [Fact]
    public void Groups_CannotHaveTags()
    {
        var group = LauncherItem.CreateGroup("仕事");

        Assert.Throws<InvalidOperationException>(() => group.SetTags(["a"]));
    }

    [Fact]
    public void AllTags_AndFindItems_UseTagsFromEveryLevel()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("ゲーム");
        var stellaris = LauncherItem.CreateItem("Stellaris", @"C:\a.lnk");
        var memo = LauncherItem.CreateItem("メモ帳", @"C:\Windows\notepad.exe");
        var calc = LauncherItem.CreateItem("電卓", @"C:\Windows\System32\calc.exe");
        tree.Insert(group, null);
        tree.Insert(stellaris, group);
        tree.Insert(memo, null);
        tree.Insert(calc, null);
        stellaris.SetTags(["戦略"]);
        memo.SetTags(["ツール", "仕事"]);
        calc.SetTags(["ツール"]);

        Assert.Equal(["ツール", "仕事", "戦略"], tree.AllTags());
        Assert.Equal([memo, calc], tree.FindItems("", "ツール"));
        Assert.Equal([calc], tree.FindItems("電", "ツール"));
        Assert.Equal([stellaris], tree.FindItems("", "戦略"));
        Assert.Equal([stellaris, memo, calc], tree.FindItems("", null));
    }

    [Fact]
    public void Tags_SurviveSaveAndLoad_AndAreOmittedWhenEmpty()
    {
        var store = new ItemStore(_dir);
        var tree = new LauncherTree();
        var tagged = LauncherItem.CreateItem("メモ帳", @"C:\Windows\notepad.exe");
        tagged.SetTags(["ツール", "仕事"]);
        tree.Insert(tagged, null);
        tree.Insert(LauncherItem.CreateItem("電卓", @"C:\Windows\System32\calc.exe"), null);
        store.Save(tree);

        var loaded = new LauncherTree();
        store.Load(loaded);

        Assert.Equal(["ツール", "仕事"], loaded.Roots[0].Tags);
        Assert.Empty(loaded.Roots[1].Tags);
        Assert.Equal(1, File.ReadAllText(store.FilePath).Split("\"Tags\"").Length - 1);
    }

    [Fact]
    public void TagColors_DifferUntilThePaletteRunsOut()
    {
        var colors = new TagColors();
        var indexes = Enumerable.Range(1, TagColors.Count).Select(i => colors.IndexFor($"tag{i}")).ToList();

        Assert.Equal(TagColors.Count, indexes.Distinct().Count());
        Assert.Equal(colors.IndexFor("tag1"), colors.IndexFor("TAG1"));
    }

    [Fact]
    public void TagColors_KeepTheirColor_AndFreedColorsAreReused()
    {
        var colors = new TagColors(new Dictionary<string, int> { ["仕事"] = 3, ["ゲーム"] = 0 });

        Assert.Equal(3, colors.IndexFor("仕事"));
        Assert.False(colors.Changed);

        colors.KeepOnly(["ゲーム"]);
        Assert.True(colors.Changed);
        Assert.Equal(1, colors.IndexFor("ツール"));
        Assert.Equal(2, colors.IndexFor("戦略"));
        Assert.Equal(3, colors.IndexFor("Dev"));
    }
}
