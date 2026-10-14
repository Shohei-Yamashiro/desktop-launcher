using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DesktopLauncher.Models;
using DesktopLauncher.Services;
using Microsoft.Win32;

namespace DesktopLauncher;

public partial class MainWindow : Window
{
    private readonly ItemStore _store = new();
    private readonly ObservableCollection<LauncherItem> _items = [];
    private readonly ICollectionView _view;

    public MainWindow()
    {
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.Filter = MatchesSearch;
        ItemsList.ItemsSource = _view;
        _items.CollectionChanged += (_, _) => UpdateEmptyText();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var item in _store.Load())
            {
                item.Icon = LoadIcon(item);
                _items.Add(item);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }

        UpdateEmptyText();
    }

    private bool MatchesSearch(object obj)
    {
        var query = SearchBox.Text.Trim();
        return query.Length == 0
            || (obj is LauncherItem item && item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private void UpdateEmptyText()
    {
        if (_items.Count == 0)
        {
            EmptyText.Text = "ここにファイルやフォルダー、URL をドラッグして追加できます。\n右上のボタンからも追加できます。";
            EmptyText.Visibility = Visibility.Visible;
        }
        else if (_view.IsEmpty)
        {
            EmptyText.Text = "一致する項目がありません。";
            EmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _view.Refresh();
        UpdateEmptyText();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                SearchBox.Clear();
                e.Handled = true;
                break;
            case Key.Enter:
                if (_view.Cast<LauncherItem>().FirstOrDefault() is { } first) Launch(first);
                e.Handled = true;
                break;
            case Key.Down:
                if (!_view.IsEmpty)
                {
                    ItemsList.SelectedIndex = 0;
                    (ItemsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                }
                e.Handled = true;
                break;
        }
    }

    private void ItemsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (ItemsList.SelectedItem is not LauncherItem item) return;

        switch (e.Key)
        {
            case Key.Enter:
                Launch(item);
                e.Handled = true;
                break;
            case Key.Delete:
                Remove(item);
                e.Handled = true;
                break;
            case Key.F2:
                Rename(item);
                e.Handled = true;
                break;
        }
    }

    private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: LauncherItem item }) Launch(item);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is { } item) Launch(item);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is not { } item) return;

        if (item.IsUrl)
        {
            ShowError("URL には保存場所がありません。");
            return;
        }

        if (!File.Exists(item.Path) && !Directory.Exists(item.Path))
        {
            ShowError($"見つかりません。\n{item.Path}");
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is { } item) Rename(item);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ItemFrom(sender) is { } item) Remove(item);
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "追加するファイルを選択",
            Multiselect = true,
            DereferenceLinks = false,
        };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "追加するフォルダーを選択",
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FolderNames);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || TryGetDroppedUrl(e.Data) is not null
            ? DragDropEffects.Link
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddPaths(paths);
        }
        else if (TryGetDroppedUrl(e.Data) is { } url)
        {
            AddPaths([url]);
        }
    }

    private static string? TryGetDroppedUrl(IDataObject data)
    {
        if (data.GetData(DataFormats.UnicodeText) is not string text) return null;
        text = text.Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? text
            : null;
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var added = false;
        foreach (var path in paths)
        {
            if (_items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;

            var item = new LauncherItem { Name = DefaultName(path), Path = path };
            item.Icon = LoadIcon(item);
            _items.Add(item);
            added = true;
        }

        if (added) Save();
    }

    private static string DefaultName(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile) return uri.Host;
        if (Directory.Exists(path))
        {
            var name = new DirectoryInfo(path).Name;
            return name.Length > 0 ? name : path;
        }
        return Path.GetFileNameWithoutExtension(path);
    }

    private static System.Windows.Media.ImageSource? LoadIcon(LauncherItem item)
    {
        if (item.IsUrl) return ShellIcon.ForUrl();
        return File.Exists(item.Path) || Directory.Exists(item.Path) ? ShellIcon.ForPath(item.Path) : null;
    }

    private void Launch(LauncherItem item)
    {
        if (!item.IsUrl && !File.Exists(item.Path) && !Directory.Exists(item.Path))
        {
            ShowError($"見つかりません。移動または削除された可能性があります。\n{item.Path}");
            return;
        }

        var startInfo = new ProcessStartInfo(item.Path) { UseShellExecute = true };
        if (File.Exists(item.Path)) startInfo.WorkingDirectory = Path.GetDirectoryName(item.Path) ?? "";

        try
        {
            Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED: the user declined the UAC prompt.
        }
        catch (Win32Exception ex)
        {
            ShowError($"起動できませんでした。\n{item.Path}\n\n{ex.Message}");
        }
    }

    private void Rename(LauncherItem item)
    {
        var dialog = new RenameWindow(item.Name) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        item.Name = dialog.NewName;
        _view.Refresh();
        UpdateEmptyText();
        Save();
    }

    private void Remove(LauncherItem item)
    {
        _items.Remove(item);
        Save();
    }

    private void Save()
    {
        try
        {
            _store.Save(_items);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"保存できませんでした。\n{_store.FilePath}\n\n{ex.Message}");
        }
    }

    private static LauncherItem? ItemFrom(object sender) =>
        (sender as FrameworkElement)?.DataContext as LauncherItem;

    private void ShowError(string message) =>
        MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
