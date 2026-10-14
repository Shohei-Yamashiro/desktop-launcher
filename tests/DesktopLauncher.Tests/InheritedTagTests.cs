using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class InheritedTagTests
{
    private static string[] Shown(LauncherItem node) =>
        node.DisplayTags.Select(t => t.IsInherited ? $"({t.Name})" : t.Name).ToArray();

    [Fact]
    public void DisplayTags_ListOwnTagsFirst_ThenTagsOfEveryEnclosingGroup()
    {
        var tree = new LauncherTree();
        var outer = LauncherItem.CreateGroup("Outer");
        var inner = LauncherItem.CreateGroup("Inner");
        var item = LauncherItem.CreateItem("A", @"C:\a.exe");
        outer.SetTags(["outer"]);
        inner.SetTags(["inner"]);
        item.SetTags(["mine"]);
        tree.Insert(outer, null);
        tree.Insert(inner, outer);
        tree.Insert(item, inner);

        Assert.Equal(["mine", "(inner)", "(outer)"], Shown(item));
        Assert.Equal(["inner", "(outer)"], Shown(inner));
    }

    [Fact]
    public void DisplayTags_TagSetOnItemAndGroup_ShowsOnlyAsOwn()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("G");
        var item = LauncherItem.CreateItem("A", @"C:\a.exe");
        group.SetTags(["shared"]);
        item.SetTags(["Shared"]);
        tree.Insert(group, null);
        tree.Insert(item, group);

        Assert.Equal(["Shared"], Shown(item));
    }

    [Fact]
    public void DisplayTags_FollowGroupTagChanges()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("G");
        var item = LauncherItem.CreateItem("A", @"C:\a.exe");
        tree.Insert(group, null);
        tree.Insert(item, group);

        group.SetTags(["new"]);
        Assert.Equal(["(new)"], Shown(item));

        group.SetTags([]);
        Assert.Empty(item.DisplayTags);
    }

    [Fact]
    public void DisplayTags_FollowMoves()
    {
        var tree = new LauncherTree();
        var tagged = LauncherItem.CreateGroup("Tagged");
        var plain = LauncherItem.CreateGroup("Plain");
        var item = LauncherItem.CreateItem("A", @"C:\a.exe");
        tagged.SetTags(["t"]);
        tree.Insert(tagged, null);
        tree.Insert(plain, null);
        tree.Insert(item, tagged);

        tree.Move(item, plain);
        Assert.Empty(item.DisplayTags);

        tree.Move(plain, tagged);
        Assert.Equal(["(t)"], Shown(item));
    }
}
