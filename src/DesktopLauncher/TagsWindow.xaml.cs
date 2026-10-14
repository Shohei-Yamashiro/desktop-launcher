using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher;

public partial class TagsWindow : Window
{
    private readonly ObservableCollection<TagOption> _options;
    private readonly Func<string, Brush> _brushFor;

    public TagsWindow(string itemName, IEnumerable<string> allTags, IReadOnlyList<string> currentTags, Func<string, Brush> brushFor)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        _brushFor = brushFor;
        ItemNameText.Text = $"「{itemName}」に付けるタグ";
        _options = new(LauncherItem.NormalizeTags(allTags.Concat(currentTags))
            .Select(t => new TagOption(t, brushFor(t), currentTags.Contains(t, StringComparer.CurrentCultureIgnoreCase))));
        TagList.ItemsSource = _options;
        UpdateNoTagsText();
        Loaded += (_, _) => NewTagBox.Focus();
    }

    public IReadOnlyList<string> SelectedTags { get; private set; } = [];

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
        SelectedTags = LauncherItem.NormalizeTags(_options.Where(o => o.IsChecked).Select(o => o.Name));
        DialogResult = true;
    }

    public sealed class TagOption(string name, Brush brush, bool isChecked)
    {
        public string Name { get; } = name;

        public Brush Brush { get; } = brush;

        public bool IsChecked { get; set; } = isChecked;
    }
}
