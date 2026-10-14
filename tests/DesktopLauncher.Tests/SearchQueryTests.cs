using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class SearchQueryTests
{
    private static (LauncherTree Tree, LauncherItem Nobunaga, LauncherItem Sangoku, LauncherItem Memo) Sample()
    {
        var tree = new LauncherTree();
        var games = LauncherItem.CreateGroup("ゲーム");
        games.SetTags(["ゲーム"]);
        var nobunaga = LauncherItem.CreateItem("信長の野望", @"C:\n.exe");
        nobunaga.SetTags(["戦国"]);
        var sangoku = LauncherItem.CreateItem("三國志", @"C:\s.exe");
        sangoku.SetTags(["三国志", "戦略"]);
        var memo = LauncherItem.CreateItem("メモ帳", @"C:\memo.exe");
        tree.Insert(games, null);
        tree.Insert(nobunaga, games);
        tree.Insert(sangoku, games);
        tree.Insert(memo, null);
        return (tree, nobunaga, sangoku, memo);
    }

    [Fact]
    public void PlainWord_MatchesNameOrTag()
    {
        var (tree, nobunaga, sangoku, _) = Sample();

        Assert.Equal([nobunaga], tree.FindItems("信長", null));
        Assert.Equal([nobunaga], tree.FindItems("戦国", null));
        Assert.Equal([sangoku], tree.FindItems("戦略", null));
    }

    [Fact]
    public void PlainWord_MatchesInheritedTag()
    {
        var (tree, nobunaga, sangoku, _) = Sample();

        Assert.Equal([nobunaga, sangoku], tree.FindItems("ゲーム", null));
    }

    [Fact]
    public void HashTag_MatchesOnlyTags_AndInheritedOnes()
    {
        var (tree, nobunaga, sangoku, _) = Sample();

        Assert.Equal([nobunaga, sangoku], tree.FindItems("#ゲーム", null));
        Assert.Empty(tree.FindItems("#信長", null));
    }

    [Fact]
    public void HashTag_KnownTagIsExact_UnknownIsPrefix()
    {
        var (tree, nobunaga, sangoku, _) = Sample();
        var extra = LauncherItem.CreateItem("ゲーム機", @"C:\g.exe");
        extra.SetTags(["戦国時代"]);
        tree.Insert(extra, null);

        Assert.Equal([nobunaga], tree.FindItems("#戦国", null));
        Assert.Equal([nobunaga, sangoku, extra], tree.FindItems("#戦", null));
    }

    [Fact]
    public void FullWidthHash_WorksLikeHash()
    {
        var (tree, nobunaga, _, _) = Sample();

        Assert.Equal([nobunaga], tree.FindItems("＃戦国", null));
    }

    [Fact]
    public void Parts_AreCombinedWithAnd()
    {
        var (tree, _, sangoku, _) = Sample();

        Assert.Equal([sangoku], tree.FindItems("#ゲーム 三", null));
        Assert.Equal([sangoku], tree.FindItems("#ゲーム #戦略", null));
        Assert.Empty(tree.FindItems("#戦国 #戦略", null));
    }

    [Fact]
    public void Parse_IgnoresLoneHashAndExtraSpaces()
    {
        var query = SearchQuery.Parse("  #  メモ  ", []);

        Assert.Equal(["メモ"], query.Words);
        Assert.Empty(query.Tags);
    }
}
