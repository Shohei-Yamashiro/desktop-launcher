using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopLauncher.Models;

/// <summary>A browser bookmark: a folder (Url is null) or a link.</summary>
public sealed record BookmarkNode(string Name, string? Url, IReadOnlyList<BookmarkNode> Children, bool IsToolbar = false)
{
    public bool IsFolder => Url is null;

    public static BookmarkNode Folder(string name, IReadOnlyList<BookmarkNode> children, bool isToolbar = false) =>
        new(name, null, children, isToolbar);

    public static BookmarkNode Link(string name, string url) => new(name, url, []);

    /// <summary>Links that can be registered (http / https) in this folder and all folders inside it.</summary>
    public int LinkCount => Children.Sum(c => c.IsFolder ? c.LinkCount : WebUrl.IsWebUrl(c.Url!) ? 1 : 0);
}

/// <summary>Reads the bookmark files of Chromium browsers (Edge, Chrome, ...) and exported HTML files.</summary>
public static partial class BookmarkReader
{
    /// <summary>
    /// The "Bookmarks" JSON file in a Chromium profile. Returns its top folders (bookmarks bar, other, mobile)
    /// that have anything in them.
    /// </summary>
    public static IReadOnlyList<BookmarkNode> ReadChromium(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Object) return [];

        var folders = new List<BookmarkNode>();
        foreach (var root in roots.EnumerateObject())
        {
            if (root.Value.ValueKind != JsonValueKind.Object) continue;
            var folder = ReadChromiumNode(root.Value, isToolbar: root.Name == "bookmark_bar");
            if (folder is { IsFolder: true, Children.Count: > 0 }) folders.Add(folder);
        }
        return folders;
    }

    private static BookmarkNode? ReadChromiumNode(JsonElement node, bool isToolbar = false)
    {
        var name = node.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var type = node.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type == "url") return node.TryGetProperty("url", out var url) && url.GetString() is { } u ? BookmarkNode.Link(name, u) : null;
        if (type != "folder") return null;

        var children = new List<BookmarkNode>();
        if (node.TryGetProperty("children", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in list.EnumerateArray())
            {
                if (ReadChromiumNode(child) is { } read) children.Add(read);
            }
        }
        return BookmarkNode.Folder(name, children, isToolbar);
    }

    /// <summary>
    /// A bookmarks HTML file as exported by any browser (the Netscape bookmark format). Returns the folders
    /// and links at its top level; the toolbar folder is marked by PERSONAL_TOOLBAR_FOLDER.
    /// </summary>
    public static IReadOnlyList<BookmarkNode> ReadHtml(string html)
    {
        var top = new List<BookmarkNode>();
        var stack = new Stack<(string Name, bool IsToolbar, List<BookmarkNode> Children)>();
        (string Name, bool IsToolbar)? pendingFolder = null;
        var depth = 0;

        foreach (Match m in HtmlToken().Matches(html))
        {
            var current = stack.Count > 0 ? stack.Peek().Children : top;
            if (m.Groups["folder"].Success)
            {
                var attributes = m.Groups["fattrs"].Value;
                pendingFolder = (Decode(m.Groups["fname"].Value),
                    attributes.Contains("PERSONAL_TOOLBAR_FOLDER=\"true\"", StringComparison.OrdinalIgnoreCase));
            }
            else if (m.Groups["link"].Success)
            {
                var href = HrefAttribute().Match(m.Groups["lattrs"].Value);
                if (href.Success)
                {
                    var url = href.Groups[1].Success ? href.Groups[1].Value : href.Groups[2].Value;
                    current.Add(BookmarkNode.Link(Decode(m.Groups["lname"].Value), Decode(url)));
                }
            }
            else if (m.Groups["open"].Success)
            {
                depth++;
                // The outermost <DL> is the file itself; any other one holds the folder named just before it.
                if (pendingFolder is { } folder) stack.Push((folder.Name, folder.IsToolbar, []));
                else if (depth > 1) stack.Push(("", false, []));
                pendingFolder = null;
            }
            else if (m.Groups["close"].Success)
            {
                depth--;
                if (stack.Count == 0) continue;
                var (name, isToolbar, children) = stack.Pop();
                (stack.Count > 0 ? stack.Peek().Children : top).Add(BookmarkNode.Folder(name, children, isToolbar));
            }
        }
        return top;
    }

    private static string Decode(string text) => WebUtility.HtmlDecode(text).Trim();

    [GeneratedRegex(
        """<DT>\s*<H3(?<fattrs>[^>]*)>(?<fname>.*?)</H3>(?<folder>)|<DT>\s*<A(?<lattrs>[^>]*)>(?<lname>.*?)</A>(?<link>)|<DL\b[^>]*>(?<open>)|</DL\s*>(?<close>)""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlToken();

    [GeneratedRegex("""\bHREF\s*=\s*"([^"]*)"|\bHREF\s*=\s*'([^']*)'""", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttribute();
}
