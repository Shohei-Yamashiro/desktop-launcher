namespace DesktopLauncher.Models;

public sealed record BookmarkImportOptions
{
    /// <summary>
    /// Folders whose own links are brought in; null brings in every folder. Folders inside are decided on
    /// their own, so a subfolder can be taken without its parent's links.
    /// </summary>
    public Func<BookmarkNode, bool>? IncludeFolder { get; init; }

    /// <summary>Leave out URLs that are already registered (or appear twice in the bookmarks).</summary>
    public bool SkipExisting { get; init; } = true;

    /// <summary>Mark the links of the bookmarks bar (and the folders inside it) as favorites.</summary>
    public bool FavoriteToolbar { get; init; }
}

public sealed record BookmarkImportResult(LauncherItem? Group, int Added, int Skipped);

/// <summary>
/// Copies browser bookmarks into the tree as one new top-level group that mirrors the bookmark folders.
/// Only http / https links are taken; folders left empty are not created.
/// </summary>
public static class BookmarkImporter
{
    public static BookmarkImportResult Import(LauncherTree tree, string groupName, IReadOnlyList<BookmarkNode> roots,
        BookmarkImportOptions options)
    {
        var known = new HashSet<string>(
            options.SkipExisting ? tree.All().Where(n => !n.IsGroup).Select(n => n.Path) : [],
            StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var skipped = 0;

        var group = LauncherItem.CreateGroup(groupName);
        AddLinks(group, roots.Where(r => !r.IsFolder), favorite: false);
        foreach (var folder in roots.Where(r => r.IsFolder)) AddFolder(group, folder, options.FavoriteToolbar && folder.IsToolbar);

        if (group.Children.Count == 0) return new BookmarkImportResult(null, 0, skipped);
        tree.Insert(group, null);
        return new BookmarkImportResult(group, added, skipped);

        void AddFolder(LauncherItem parent, BookmarkNode folder, bool favorite)
        {
            var child = LauncherItem.CreateGroup(folder.Name.Length > 0 ? folder.Name : "（名前なし）");
            if (options.IncludeFolder?.Invoke(folder) ?? true) AddLinks(child, folder.Children.Where(c => !c.IsFolder), favorite);
            foreach (var sub in folder.Children.Where(c => c.IsFolder)) AddFolder(child, sub, favorite);
            if (child.Children.Count > 0) Attach(parent, child);
        }

        void AddLinks(LauncherItem parent, IEnumerable<BookmarkNode> links, bool favorite)
        {
            foreach (var link in links)
            {
                if (link.Url is not { } url || !WebUrl.IsWebUrl(url)) continue;
                if (options.SkipExisting && !known.Add(url))
                {
                    skipped++;
                    continue;
                }

                var item = LauncherItem.CreateItem(link.Name.Length > 0 ? link.Name : WebUrl.DefaultName(url), url);
                item.IsFavorite = favorite;
                Attach(parent, item);
                added++;
            }
        }
    }

    /// <summary>Builds the subtree off-tree; Parent is set so that tags and paths work once it is inserted.</summary>
    private static void Attach(LauncherItem parent, LauncherItem child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
    }
}
