using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher;

/// <summary>
/// Edits the tags of one or more nodes. A tag that only some of the nodes have starts out
/// indeterminate and is left alone unless the user checks or clears it. Tags inherited from
/// enclosing groups are listed first and marked, so they can also be set on the nodes themselves.
/// </summary>
public partial class TagsWindow : Window
{
    private readonly ObservableCollection<TagOption> _options;
    private readonly Func<string, Brush> _brushFor;

    public TagsWindow(string heading, string? note, IEnumerable<string> allTags, IReadOnlyList<LauncherItem> nodes,
        Func<string, Brush> brushFor)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        _brushFor = brushFor;

        var used = nodes.SelectMany(n => n.Tags);
        _options = new(LauncherItem.NormalizeTags(allTags.Concat(used))
            .Select(tag =>
            {
                var count = nodes.Count(n => n.HasTag(tag));
                bool? state = count == 0 ? false : count == nodes.Count ? true : null;
                return new TagOption(tag, brushFor(tag), state, InheritedText(tag, nodes),
                    inheritsAll: nodes.All(n => n.InheritedFrom(tag) is not null));
            })
            .OrderBy(o => o.InheritedText is null ? 1 : 0));

        var notes = new List<string>();
        if (note is not null) notes.Add(note);
        if (_options.Any(o => o.IsMixed)) notes.Add("「－」は一部に付いているタグ。そのままなら変更しません。");
        if (_options.Any(o => o.InheritedText is not null))
        {
            notes.Add("薄いチェックは上位のグループから継承しているタグで、ここでは外せません。"
                + "チェックすると項目自身にも付き、グループから外しても残ります。");
        }
        ItemNameText.Text = heading;
        NoteText.Text = string.Join("\n", notes);
        NoteText.Visibility = notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TagList.ItemsSource = _options;
        UpdateNoTagsText();
        Loaded += (_, _) => NewTagBox.Focus();
    }

    /// <summary>Tags to add to every node.</summary>
    public IReadOnlyList<string> AddedTags { get; private set; } = [];

    /// <summary>Tags to remove from every node.</summary>
    public IReadOnlyList<string> RemovedTags { get; private set; } = [];

    private void NewTagBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddPendingTag();
        e.Handled = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        AddPendingTag();
        NewTagBox.Focus();
    }

    /// <summary>Checks the typed tag, creating it when it is new.</summary>
    private void AddPendingTag()
    {
        var name = NewTagBox.Text.Trim();
        NewTagBox.Clear();
        if (name.Length == 0) return;

        var existing = _options.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is not null)
        {
            existing.IsChecked = true;
            TagList.Items.Refresh();
            return;
        }

        // New tags cannot be inherited, so they go among the plain tags after the inherited ones.
        var index = _options.Count(o => o.InheritedText is not null);
        while (index < _options.Count && StringComparer.CurrentCultureIgnoreCase.Compare(_options[index].Name, name) < 0) index++;
        _options.Insert(index, new TagOption(name, _brushFor(name), true));
        UpdateNoTagsText();
    }

    private void UpdateNoTagsText() =>
        NoTagsText.Visibility = _options.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        AddPendingTag();
        AddedTags = _options.Where(o => o.IsChecked == true).Select(o => o.Name).ToList();
        RemovedTags = _options.Where(o => o.IsChecked == false).Select(o => o.Name).ToList();
        DialogResult = true;
    }

    /// <summary>
    /// "「グループ」から継承" for one node; for several, how many of them inherit the tag. Null when none does.
    /// </summary>
    private static string? InheritedText(string tag, IReadOnlyList<LauncherItem> nodes)
    {
        if (nodes is [var single]) return single.InheritedFrom(tag) is { } group ? $"「{group.Name}」から継承" : null;

        var count = nodes.Count(n => n.InheritedFrom(tag) is not null);
        return count == 0 ? null : count == nodes.Count ? "すべて継承" : $"{count} 件が継承";
    }

    public sealed class TagOption(string name, Brush brush, bool? isChecked, string? inheritedText = null, bool inheritsAll = false)
    {
        public string Name { get; } = name;

        public Brush Brush { get; } = brush;

        public string? InheritedText { get; } = inheritedText;

        /// <summary>
        /// "inherited" when every node inherits the tag, which makes the theme's check box show a faint
        /// check while unchecked: clearing it cannot take the tag away.
        /// </summary>
        public string? CheckHint { get; } = inheritsAll ? "inherited" : null;

        /// <summary>Only some of the nodes have the tag, so "unchanged" is a valid choice.</summary>
        public bool IsMixed { get; } = isChecked is null;

        public bool? IsChecked { get; set; } = isChecked;
    }
}
