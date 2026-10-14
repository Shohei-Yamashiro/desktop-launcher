using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace DesktopLauncher.Models;

public enum DropPosition
{
    None,
    Before,
    Into,
    After,
}

/// <summary>
/// A launchable entry, or a group (IsGroup) that holds other entries and groups.
/// </summary>
public sealed class LauncherItem : INotifyPropertyChanged
{
    private string _name = "";
    private ImageSource? _icon;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isCurrent;
    private DropPosition _dropPosition;
    private IReadOnlyList<string> _tags = [];

    private LauncherItem(bool isGroup)
    {
        IsGroup = isGroup;
        Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ChildCountText));
    }

    public static LauncherItem CreateItem(string name, string path) => new(false) { Name = name, Path = path };

    public static LauncherItem CreateGroup(string name) => new(true) { Name = name };

    public bool IsGroup { get; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Path { get; private init; } = "";

    public ObservableCollection<LauncherItem> Children { get; } = [];

    public LauncherItem? Parent { get; set; }

    public ImageSource? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    /// <summary>Part of the (multi-)selection that commands act on.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>The TreeView's own single selection, used as the keyboard cursor.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetField(ref _isCurrent, value);
    }

    public DropPosition DropPosition
    {
        get => _dropPosition;
        set => SetField(ref _dropPosition, value);
    }

    /// <summary>Tag names in display order. A group's tags also apply to everything inside it.</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => SetField(ref _tags, value);
    }

    public void SetTags(IEnumerable<string> tags)
    {
        var normalized = NormalizeTags(tags);
        if (!normalized.SequenceEqual(_tags)) Tags = normalized;
    }

    public bool HasTag(string tag) => _tags.Contains(tag, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>Adds and removes tags while leaving the node's other tags alone.</summary>
    public void ApplyTagChanges(IEnumerable<string> added, IEnumerable<string> removed)
    {
        var remove = new HashSet<string>(removed, StringComparer.CurrentCultureIgnoreCase);
        SetTags(_tags.Where(t => !remove.Contains(t)).Concat(added));
    }

    /// <summary>True when this node or any enclosing group carries the tag.</summary>
    public bool HasTagIncludingGroups(string tag)
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            if (node.HasTag(tag)) return true;
        }
        return false;
    }

    /// <summary>Trims, drops blanks and case-insensitive duplicates, and sorts.</summary>
    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags) => tags
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .Order(StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public string ChildCountText => IsGroup ? $"{Children.Count}" : "";

    /// <summary>Names of the enclosing groups, e.g. "仕事 / 資料".</summary>
    public string Location
    {
        get
        {
            var names = new List<string>();
            for (var group = Parent; group is not null; group = group.Parent) names.Insert(0, group.Name);
            return string.Join(" / ", names);
        }
    }

    public bool IsUrl => !IsGroup
        && Uri.TryCreate(Path, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
