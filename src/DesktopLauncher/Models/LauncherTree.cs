using System.Collections.ObjectModel;

namespace DesktopLauncher.Models;

/// <summary>
/// Tree operations that keep each node's Parent in sync with the collection that holds it.
/// </summary>
public sealed class LauncherTree
{
    public ObservableCollection<LauncherItem> Roots { get; } = [];

    public ObservableCollection<LauncherItem> SiblingsOf(LauncherItem node) => node.Parent?.Children ?? Roots;

    public void Insert(LauncherItem node, LauncherItem? parent, int index = -1)
    {
        if (parent is { IsGroup: false }) throw new ArgumentException("Only groups can hold items.", nameof(parent));

        var list = parent?.Children ?? Roots;
        node.Parent = parent;
        if (index < 0 || index > list.Count) list.Add(node);
        else list.Insert(index, node);
    }

    public void Remove(LauncherItem node)
    {
        SiblingsOf(node).Remove(node);
        node.Parent = null;
    }

    /// <summary>A group cannot move into itself or into one of its own descendants.</summary>
    public static bool CanMove(LauncherItem node, LauncherItem? newParent)
    {
        if (newParent is null) return true;
        if (!newParent.IsGroup) return false;
        for (var group = newParent; group is not null; group = group.Parent)
        {
            if (group == node) return false;
        }
        return true;
    }

    /// <summary>Moves the node so that it ends up at <paramref name="index"/> of the target list (-1 = end).</summary>
    public bool Move(LauncherItem node, LauncherItem? newParent, int index = -1)
    {
        if (!CanMove(node, newParent)) return false;

        var source = SiblingsOf(node);
        var target = newParent?.Children ?? Roots;
        var oldIndex = source.IndexOf(node);

        if (source == target)
        {
            if (index < 0 || index >= target.Count) index = target.Count - 1;
            if (index != oldIndex) target.Move(oldIndex, index);
            return true;
        }

        source.RemoveAt(oldIndex);
        Insert(node, newParent, index);
        return true;
    }

    public bool MoveUp(LauncherItem node)
    {
        var siblings = SiblingsOf(node);
        var index = siblings.IndexOf(node);
        if (index <= 0) return false;
        siblings.Move(index, index - 1);
        return true;
    }

    public bool MoveDown(LauncherItem node)
    {
        var siblings = SiblingsOf(node);
        var index = siblings.IndexOf(node);
        if (index < 0 || index >= siblings.Count - 1) return false;
        siblings.Move(index, index + 1);
        return true;
    }

    public IEnumerable<LauncherItem> All() => Flatten(Roots);

    public IEnumerable<LauncherItem> Groups() => All().Where(n => n.IsGroup);

    /// <summary>Every tag used by at least one item, sorted.</summary>
    public IReadOnlyList<string> AllTags() => LauncherItem.NormalizeTags(All().SelectMany(n => n.Tags));

    /// <summary>
    /// Items (not groups) whose name contains the query and that carry the tag themselves or through
    /// an enclosing group; either condition may be empty.
    /// </summary>
    public IEnumerable<LauncherItem> FindItems(string query, string? tag) => All().Where(n =>
        !n.IsGroup
        && (query.Length == 0 || n.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
        && (tag is null || n.HasTagIncludingGroups(tag)));

    public int Depth(LauncherItem node)
    {
        var depth = 0;
        for (var group = node.Parent; group is not null; group = group.Parent) depth++;
        return depth;
    }

    private static IEnumerable<LauncherItem> Flatten(IEnumerable<LauncherItem> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children)) yield return child;
        }
    }
}
