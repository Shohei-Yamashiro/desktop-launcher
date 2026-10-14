using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class MultiSelectTests
{
    private static LauncherItem Item(string name) => LauncherItem.CreateItem(name, $@"C:\{name}.exe");

    private static string[] Names(IEnumerable<LauncherItem> nodes) => nodes.Select(n => n.Name).ToArray();

    private static (LauncherTree Tree, LauncherItem[] Items) Flat(params string[] names)
    {
        var tree = new LauncherTree();
        var items = names.Select(Item).ToArray();
        foreach (var item in items) tree.Insert(item, null);
        return (tree, items);
    }

    [Fact]
    public void MoveMany_DownwardInSameList_KeepsOrderBeforeTarget()
    {
        var (tree, i) = Flat("A", "B", "C", "D");

        Assert.True(tree.MoveMany([i[0], i[1]], null, 3));

        Assert.Equal(["C", "A", "B", "D"], Names(tree.Roots));
    }

    [Fact]
    public void MoveMany_UpwardInSameList_KeepsOrder()
    {
        var (tree, i) = Flat("A", "B", "C", "D");

        Assert.True(tree.MoveMany([i[2], i[3]], null, 1));

        Assert.Equal(["A", "C", "D", "B"], Names(tree.Roots));
    }

    [Fact]
    public void MoveMany_IntoGroup_AppendsAll()
    {
        var (tree, i) = Flat("A", "B");
        var group = LauncherItem.CreateGroup("G");
        tree.Insert(group, null);

        Assert.True(tree.MoveMany([i[0], i[1]], group));

        Assert.Equal(["G"], Names(tree.Roots));
        Assert.Equal(["A", "B"], Names(group.Children));
        Assert.All(group.Children, c => Assert.Same(group, c.Parent));
    }

    [Fact]
    public void MoveMany_RejectsAllWhenOneCannotMove()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("G");
        var a = Item("A");
        tree.Insert(group, null);
        tree.Insert(a, null);

        Assert.False(tree.MoveMany([a, group], group));

        Assert.Equal(["G", "A"], Names(tree.Roots));
    }

    [Fact]
    public void TopMost_DropsNodesInsideSelectedGroups_InTreeOrder()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("G");
        var inner = Item("Inner");
        var a = Item("A");
        tree.Insert(a, null);
        tree.Insert(group, null);
        tree.Insert(inner, group);

        Assert.Equal(["A", "G"], Names(tree.TopMost([inner, group, a])));
    }

    [Fact]
    public void Visible_SkipsChildrenOfCollapsedGroups()
    {
        var tree = new LauncherTree();
        var open = LauncherItem.CreateGroup("Open");
        var closed = LauncherItem.CreateGroup("Closed");
        open.IsExpanded = true;
        tree.Insert(open, null);
        tree.Insert(Item("A"), open);
        tree.Insert(closed, null);
        tree.Insert(Item("B"), closed);

        Assert.Equal(["Open", "A", "Closed"], Names(tree.Visible()));
    }

    [Fact]
    public void ApplyTagChanges_AddsAndRemovesButKeepsOthers()
    {
        var item = Item("A");
        item.SetTags(["keep", "old"]);

        item.ApplyTagChanges(["new", "Keep"], ["OLD"]);

        Assert.Equal(["keep", "new"], item.Tags);
    }
}
