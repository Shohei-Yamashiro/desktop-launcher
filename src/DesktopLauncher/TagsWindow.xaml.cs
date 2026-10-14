using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher;

/// <summary>
/// Edits the tags of one or more nodes. A tag that only some of the nodes have starts out
/// indeterminate and is left alone unless the user checks or clears it.
/// </summary>
public partial class TagsWindow : Window
{
    private readonly ObservableCollection<TagOption> _options;
    private readonly Func<string, Brush> _brushFor;

    public TagsWindow(string heading, string? note, IEnumerable<string> allTags, IReadOnlyList<IReadOnlyList<string>> currentTagSets,
        Func<string, Brush> brushFor)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        _brushFor = brushFor;

        var used = currentTagSets.SelectMany(t => t);
        _options = new(LauncherItem.NormalizeTags(allTags.Concat(used)).Select(tag =>
        {
            var count = currentTagSets.Count(tags => tags.Contains(tag, StringComparer.CurrentCultureIgnoreCase));
            bool? state = count == 0 ? false : count == currentTagSets.Count ? true : null;
            return new TagOption(tag, brushFor(tag), state);
        }));

        var notes = new List<string>();
        if (note is not null) notes.Add(note);
        if (_options.Any(o => o.IsMixed)) notes.Add("「－」は一部に付いているタグ。そのままなら変更しません。");
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

        var index = 0;
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

    public sealed class TagOption(string name, Brush brush, bool? isChecked)
    {
        public string Name { get; } = name;

        public Brush Brush { get; } = brush;

        /// <summary>Only some of the nodes have the tag, so "unchanged" is a valid choice.</summary>
        public bool IsMixed { get; } = isChecked is null;

        public bool? IsChecked { get; set; } = isChecked;
    }
}
