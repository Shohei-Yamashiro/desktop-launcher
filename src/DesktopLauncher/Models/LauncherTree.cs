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

    /// <summary>
    /// Moves several nodes so that they end up next to each other, in the given order, starting at
    /// <paramref name="index"/> of the target list (-1 = end). Nothing moves if any of them cannot.
    /// </summary>
    public bool MoveMany(IReadOnlyList<LauncherItem> nodes, LauncherItem? newParent, int index = -1)
    {
        if (nodes.Count == 0 || !nodes.All(n => CanMove(n, newParent))) return false;

        var target = newParent?.Children ?? Roots;
        foreach (var node in nodes)
        {
            if (index >= 0 && SiblingsOf(node) == target && target.IndexOf(node) < index) index--;
            Move(node, newParent, index);
            if (index >= 0) index++;
        }
        return true;
    }

    /// <summary>Drops nodes that sit inside another of the given groups, keeping tree order.</summary>
    public IReadOnlyList<LauncherItem> TopMost(IEnumerable<LauncherItem> nodes)
    {
        var set = nodes.ToHashSet();
        return All().Where(n => set.Contains(n) && !HasAncestorIn(n, set)).ToList();
    }

    /// <summary>Nodes shown in the tree (inside expanded groups only), top to bottom.</summary>
    public IEnumerable<LauncherItem> Visible() => FlattenExpanded(Roots);

    public IEnumerable<LauncherItem> All() => Flatten(Roots);

    private static bool HasAncestorIn(LauncherItem node, HashSet<LauncherItem> set)
    {
        for (var group = node.Parent; group is not null; group = group.Parent)
        {
            if (set.Contains(group)) return true;
        }
        return false;
    }

    private static IEnumerable<LauncherItem> FlattenExpanded(IEnumerable<LauncherItem> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            if (!node.IsExpanded) continue;
            foreach (var child in FlattenExpanded(node.Children)) yield return child;
        }
    }

    public IEnumerable<LauncherItem> Groups() => All().Where(n => n.IsGroup);

    /// <summary>Every tag used by at least one item, sorted.</summary>
    public IReadOnlyList<string> AllTags() => LauncherItem.NormalizeTags(All().SelectMany(n => n.Tags));

    /// <summary>
    /// Items (not groups) that match the search text (see <see cref="SearchQuery"/>) and carry the tag
    /// themselves or through an enclosing group; either condition may be empty.
    /// </summary>
    public IEnumerable<LauncherItem> FindItems(string text, string? tag) => FindItems(SearchQuery.Parse(text, AllTags()), tag);

    public IEnumerable<LauncherItem> FindItems(SearchQuery query, string? tag) => All().Where(n =>
        !n.IsGroup
        && query.Matches(n)
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
