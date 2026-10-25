using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DesktopLauncher.Models;
using DesktopLauncher.Services;
using Microsoft.Win32;

namespace DesktopLauncher;

/// <summary>Chooses a browser profile (or an exported HTML file) and the bookmark folders to bring in.</summary>
public partial class ImportBookmarksWindow : Window
{
    private const string HtmlChoice = "HTML ファイルから…";

    private readonly IReadOnlyList<BookmarkSource> _sources;
    private IReadOnlyList<BookmarkNode> _roots = [];
    private List<FolderOption> _options = [];
    private FolderOption? _looseLinks;
    private int _lastSourceIndex = -1;
    private string _sourceName = "";

    public ImportBookmarksWindow(IReadOnlyList<BookmarkSource> sources)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        _sources = sources;
        foreach (var source in sources) SourceBox.Items.Add(source.DisplayName);
        SourceBox.Items.Add(HtmlChoice);
        if (sources.Count > 0) SourceBox.SelectedIndex = 0;
        else ShowFolders([], "ブラウザのブックマークが見つかりませんでした。「HTML ファイルから…」を選んでください。");
    }

    public IReadOnlyList<BookmarkNode> Roots => _roots;

    /// <summary>Name of the group the bookmarks go into.</summary>
    public string GroupName => $"{_sourceName}のブックマーク";

    public BookmarkImportOptions Options => new()
    {
        IncludeFolder = folder => _options.SelectMany(Flatten).FirstOrDefault(o => ReferenceEquals(o.Node, folder))?.IsChecked ?? true,
        SkipExisting = SkipExistingBox.IsChecked == true,
        FavoriteToolbar = FavoriteBarBox.IsChecked == true,
    };

    private void SourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = SourceBox.SelectedIndex;
        if (index < 0) return;
        try
        {
            if (index < _sources.Count)
            {
                ShowFolders(BrowserBookmarks.Read(_sources[index]), "このプロファイルにはブックマークがありません。");
                _sourceName = _sources[index].DisplayName;
                _lastSourceIndex = index;
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "ブックマークの HTML ファイルを選択",
                Filter = "ブックマークの HTML ファイル (*.html;*.htm)|*.html;*.htm|すべてのファイル (*.*)|*.*",
            };
            if (dialog.ShowDialog(this) != true)
            {
                SourceBox.SelectedIndex = _lastSourceIndex;
                return;
            }
            ShowFolders(BrowserBookmarks.ReadHtmlFile(dialog.FileName), "このファイルにはブックマークが見つかりませんでした。");
            _sourceName = Path.GetFileNameWithoutExtension(dialog.FileName);
            _lastSourceIndex = index;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            ShowFolders([], $"ブックマークを読み込めませんでした。\n{ex.Message}");
        }
    }

    private void ShowFolders(IReadOnlyList<BookmarkNode> roots, string emptyMessage)
    {
        _roots = roots;
        _options = [];
        var looseLinks = roots.Where(r => !r.IsFolder).Sum(r => WebUrl.IsWebUrl(r.Url!) ? 1 : 0);
        _looseLinks = looseLinks > 0 ? new FolderOption(BookmarkNode.Folder("（フォルダーに入っていない項目）", []), looseLinks) : null;
        if (_looseLinks is not null) _options.Add(_looseLinks);
        _options.AddRange(roots.Where(r => r.IsFolder && r.LinkCount > 0).Select(FolderOption.From));

        FolderTree.ItemsSource = _options;
        var total = roots.Sum(r => r.IsFolder ? r.LinkCount : WebUrl.IsWebUrl(r.Url!) ? 1 : 0);
        NoFoldersText.Text = emptyMessage;
        NoFoldersText.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        ImportButton.IsEnabled = total > 0;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (!_options.SelectMany(Flatten).Any(o => o.IsChecked))
        {
            MessageBox.Show(this, "取り込むフォルダーを選んでください。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Links outside every folder come along only when their row is checked.
        if (_looseLinks is { IsChecked: false }) _roots = [.. _roots.Where(r => r.IsFolder)];
        DialogResult = true;
    }

    private static IEnumerable<FolderOption> Flatten(FolderOption option) => [option, .. option.Children.SelectMany(Flatten)];

    /// <summary>A folder row with a check box; checking or clearing it does the same to the folders inside.</summary>
    public sealed class FolderOption(BookmarkNode node, int count) : INotifyPropertyChanged
    {
        private bool _isChecked = true;

        public BookmarkNode Node { get; } = node;

        public string Name => Node.Name.Length > 0 ? Node.Name : "（名前なし）";

        public string CountText => $"　{count} 件";

        public List<FolderOption> Children { get; } = [];

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                foreach (var child in Children) child.IsChecked = value;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static FolderOption From(BookmarkNode folder)
        {
            var option = new FolderOption(folder, folder.LinkCount);
            option.Children.AddRange(folder.Children.Where(c => c.IsFolder && c.LinkCount > 0).Select(From));
            return option;
        }
    }
}
