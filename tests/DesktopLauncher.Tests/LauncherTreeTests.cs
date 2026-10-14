using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class LauncherTreeTests
{
    private static LauncherItem Item(string name) => LauncherItem.CreateItem(name, $@"C:\{name}.exe");

    private static string[] Names(IEnumerable<LauncherItem> nodes) => nodes.Select(n => n.Name).ToArray();

    [Fact]
    public void Insert_SetsParentAndPosition()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("仕事");
        tree.Insert(group, null);
        tree.Insert(Item("a"), group);
        tree.Insert(Item("b"), group, 0);

        Assert.Equal(["b", "a"], Names(group.Children));
        Assert.All(group.Children, n => Assert.Same(group, n.Parent));
    }

    [Fact]
    public void Insert_IntoNonGroup_Throws()
    {
        var tree = new LauncherTree();
        var item = Item("a");
        tree.Insert(item, null);

        Assert.Throws<ArgumentException>(() => tree.Insert(Item("b"), item));
    }

    [Fact]
    public void Move_IntoAnotherGroup_UpdatesParent()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("g");
        var item = Item("a");
        tree.Insert(group, null);
        tree.Insert(item, null);

        Assert.True(tree.Move(item, group));

        Assert.Equal(["g"], Names(tree.Roots));
        Assert.Same(group, item.Parent);
        Assert.Equal("g", item.Location);
    }

    [Fact]
    public void Move_WithinSameList_PlacesAtIndex()
    {
        var tree = new LauncherTree();
        var a = Item("a");
        tree.Insert(a, null);
        tree.Insert(Item("b"), null);
        tree.Insert(Item("c"), null);

        tree.Move(a, null, 2);

        Assert.Equal(["b", "c", "a"], Names(tree.Roots));
    }

    [Fact]
    public void Move_ToTopLevel_ClearsParent()
    {
        var tree = new LauncherTree();
        var group = LauncherItem.CreateGroup("g");
        var item = Item("a");
        tree.Insert(group, null);
        tree.Insert(item, group);

        tree.Move(item, null, 0);

        Assert.Equal(["a", "g"], Names(tree.Roots));
        Assert.Null(item.Parent);
        Assert.Empty(group.Children);
    }

    [Fact]
    public void Move_GroupIntoItsOwnDescendant_IsRejected()
    {
        var tree = new LauncherTree();
        var outer = LauncherItem.CreateGroup("outer");
        var inner = LauncherItem.CreateGroup("inner");
        tree.Insert(outer, null);
        tree.Insert(inner, outer);

        Assert.False(tree.Move(outer, inner));
        Assert.False(tree.Move(outer, outer));
        Assert.Same(outer, inner.Parent);
        Assert.Equal(["outer"], Names(tree.Roots));
    }

    [Fact]
    public void MoveUpAndDown_StopAtEdges()
    {
        var tree = new LauncherTree();
        var a = Item("a");
        var b = Item("b");
        tree.Insert(a, null);
        tree.Insert(b, null);

        Assert.False(tree.MoveUp(a));
        Assert.True(tree.MoveUp(b));
        Assert.Equal(["b", "a"], Names(tree.Roots));
        Assert.False(tree.MoveDown(a));
    }

    [Fact]
    public void All_ReturnsDepthFirstOrder_AndDepthCountsAncestors()
    {
        var tree = new LauncherTree();
        var g1 = LauncherItem.CreateGroup("g1");
        var g2 = LauncherItem.CreateGroup("g2");
        var leaf = Item("leaf");
        tree.Insert(g1, null);
        tree.Insert(g2, g1);
        tree.Insert(leaf, g2);
        tree.Insert(Item("top"), null);

        Assert.Equal(["g1", "g2", "leaf", "top"], Names(tree.All()));
        Assert.Equal(2, tree.Depth(leaf));
        Assert.Equal("g1 / g2", leaf.Location);
    }
}
