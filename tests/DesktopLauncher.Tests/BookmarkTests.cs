using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class BookmarkTests
{
    private const string ChromiumJson = """
        {
          "checksum": "x",
          "roots": {
            "bookmark_bar": {
              "name": "お気に入りバー", "type": "folder",
              "children": [
                { "name": "勤怠", "type": "url", "url": "https://kintai.example.com/" },
                { "name": "資料", "type": "folder", "children": [
                  { "name": "社内ポータル", "type": "url", "url": "https://portal.example.com/" },
                  { "name": "設定画面", "type": "url", "url": "edge://settings" }
                ] }
              ]
            },
            "other": {
              "name": "その他のお気に入り", "type": "folder",
              "children": [ { "name": "", "type": "url", "url": "https://news.example.com/top" } ]
            },
            "synced": { "name": "モバイルのお気に入り", "type": "folder", "children": [] }
          },
          "version": 1
        }
        """;

    private const string NetscapeHtml = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
        <TITLE>Bookmarks</TITLE>
        <H1>Bookmarks</H1>
        <DL><p>
            <DT><H3 ADD_DATE="1" PERSONAL_TOOLBAR_FOLDER="true">ブックマーク ツールバー</H3>
            <DL><p>
                <DT><A HREF="https://kintai.example.com/" ADD_DATE="1">勤怠 &amp; 経費</A>
                <DT><H3>資料</H3>
                <DL><p>
                    <DT><A HREF='https://portal.example.com/'>社内ポータル</A>
                </DL><p>
            </DL><p>
            <DT><A HREF="https://news.example.com/">ニュース</A>
            <DT><A HREF="javascript:alert(1)">ブックマークレット</A>
        </DL><p>
        """;

    [Fact]
    public void ReadChromium_KeepsFoldersAndSkipsEmptyRoots()
    {
        var roots = BookmarkReader.ReadChromium(ChromiumJson);

        Assert.Equal(["お気に入りバー", "その他のお気に入り"], roots.Select(r => r.Name).ToArray());
        Assert.True(roots[0].IsToolbar);
        Assert.False(roots[1].IsToolbar);
        Assert.Equal("資料", roots[0].Children[1].Name);
        Assert.Equal("https://portal.example.com/", roots[0].Children[1].Children[0].Url);
        Assert.Equal(2, roots[0].LinkCount);
    }

    [Fact]
    public void ReadHtml_ReadsNestedFoldersEntitiesAndToolbar()
    {
        var top = BookmarkReader.ReadHtml(NetscapeHtml);

        var toolbar = Assert.Single(top, n => n.IsFolder);
        Assert.Equal("ブックマーク ツールバー", toolbar.Name);
        Assert.True(toolbar.IsToolbar);
        Assert.Equal("勤怠 & 経費", toolbar.Children[0].Name);
        Assert.Equal("資料", toolbar.Children[1].Name);
        Assert.Equal("https://portal.example.com/", toolbar.Children[1].Children[0].Url);
        Assert.Equal(["ニュース", "ブックマークレット"], top.Where(n => !n.IsFolder).Select(n => n.Name).ToArray());
    }

    [Fact]
    public void Import_MirrorsFoldersUnderOneGroupAndTakesOnlyWebLinks()
    {
        var tree = new LauncherTree();

        var result = BookmarkImporter.Import(tree, "Edge のブックマーク", BookmarkReader.ReadChromium(ChromiumJson), new());

        var group = Assert.Single(tree.Roots);
        Assert.Same(group, result.Group);
        Assert.Equal("Edge のブックマーク", group.Name);
        Assert.Equal(["お気に入りバー", "その他のお気に入り"], group.Children.Select(c => c.Name).ToArray());
        Assert.Equal(3, result.Added);
        Assert.Equal("Edge のブックマーク / お気に入りバー / 資料", tree.All().First(n => n.Name == "社内ポータル").Location);
        Assert.Equal("news.example.com", tree.All().Single(n => n.Path == "https://news.example.com/top").Name);
        Assert.DoesNotContain(tree.All(), n => n.Path.StartsWith("edge:", StringComparison.Ordinal));
        Assert.DoesNotContain(tree.All(), n => n.IsFavorite);
    }

    [Fact]
    public void Import_SkipsRegisteredUrlsAndCanMarkTheToolbar()
    {
        var tree = new LauncherTree();
        tree.Insert(LauncherItem.CreateItem("勤怠", "https://kintai.example.com/"), null);

        var result = BookmarkImporter.Import(tree, "取り込み", BookmarkReader.ReadHtml(NetscapeHtml),
            new BookmarkImportOptions { FavoriteToolbar = true });

        Assert.Equal(1, result.Skipped);
        Assert.Equal(2, result.Added);
        Assert.True(tree.All().Single(n => n.Name == "社内ポータル").IsFavorite);
        Assert.False(tree.All().Single(n => n.Name == "ニュース").IsFavorite);
    }

    [Fact]
    public void Import_LeavesOutUnselectedFoldersAndEmptyResults()
    {
        var tree = new LauncherTree();
        var roots = BookmarkReader.ReadChromium(ChromiumJson);

        var result = BookmarkImporter.Import(tree, "取り込み", roots,
            new BookmarkImportOptions { IncludeFolder = f => f.Name != "資料" && f.Name != "その他のお気に入り" });

        Assert.Equal(1, result.Added);
        Assert.Equal(["取り込み", "お気に入りバー", "勤怠"], tree.All().Select(n => n.Name).ToArray());

        // A subfolder can be taken without the links of the folder around it.
        var only = new LauncherTree();
        BookmarkImporter.Import(only, "取り込み", roots, new BookmarkImportOptions { IncludeFolder = f => f.Name == "資料" });
        Assert.Equal(["取り込み", "お気に入りバー", "資料", "社内ポータル"], only.All().Select(n => n.Name).ToArray());

        var none = BookmarkImporter.Import(new LauncherTree(), "空", roots, new BookmarkImportOptions { IncludeFolder = _ => false });
        Assert.Null(none.Group);
    }
}
