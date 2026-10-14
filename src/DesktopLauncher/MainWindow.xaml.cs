using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopLauncher.Models;
using DesktopLauncher.Services;
using Microsoft.Win32;

namespace DesktopLauncher;

public partial class MainWindow : Window
{
    private const string NodeFormat = "DesktopLauncher.Node";

    private readonly ItemStore _store = new();
    private readonly LauncherTree _tree = new();
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private int _modalDepth;
    private bool _exiting;

    private Point _pressPoint;
    private LauncherItem? _pressedNode;
    private bool _dragging;
    private LauncherItem? _dropTarget;

    public MainWindow(SettingsStore settingsStore, AppSettings settings)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _settings = settings;

        Tree.ItemsSource = _tree.Roots;
        _tree.Roots.CollectionChanged += (_, _) => UpdateEmptyText();

        LoadItems();
    }

    public event Action? ClosedToTray;

    private bool IsSearching => SearchBox.Text.Trim().Length > 0;

    /// <summary>Screen rectangle the window opens from (the mini icon).</summary>
    public Func<Rect>? Anchor { get; set; }

    public void ShowLauncher()
    {
        if (!IsVisible && Anchor is not null) PlaceNearAnchor(Anchor());
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void PlaceNearAnchor(Rect anchor)
    {
        var center = new Point(anchor.X + anchor.Width / 2, anchor.Y + anchor.Height / 2);
        var placed = WindowPlacement.PlaceNear(anchor, new Size(Width, Height), WindowPlacement.WorkAreaAt(this, center));
        Left = placed.Left;
        Top = placed.Top;
        Width = placed.Width;
        Height = placed.Height;
    }

    public void HideLauncher()
    {
        SearchBox.Clear();
        SetDropTarget(null, DropPosition.None);
        Save();
        Hide();
    }

    public void ToggleLauncher()
    {
        if (IsVisible && IsActive) HideLauncher();
        else ShowLauncher();
    }

    public void PrepareExit()
    {
        _exiting = true;
        Save();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting)
        {
            e.Cancel = true;
            HideLauncher();
            ClosedToTray?.Invoke();
        }
        base.OnClosing(e);
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
        if (e.Key == Key.Escape)
        {
            if (SearchBox.Text.Length > 0) SearchBox.Clear();
            else HideLauncher();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_modalDepth == 0 && !_dragging && PinButton.IsChecked != true && !_exiting) HideLauncher();
    }

    /// <summary>Opening a dialog deactivates this window, which must not hide it.</summary>
    private T WithModal<T>(Func<T> showDialog)
    {
        _modalDepth++;
        try
        {
            return showDialog();
        }
        finally
        {
            _modalDepth--;
        }
    }

    private void LoadItems()
    {
        try
        {
            _store.Load(_tree);
            foreach (var node in _tree.All()) node.Icon = LoadIcon(node);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }

        UpdateEmptyText();
    }

    // ---- Search ----

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateSearch();

    private void UpdateSearch()
    {
        if (!IsSearching)
        {
            SearchList.ItemsSource = null;
            SearchList.Visibility = Visibility.Collapsed;
            Tree.Visibility = Visibility.Visible;
        }
        else
        {
            var query = SearchBox.Text.Trim();
            var results = _tree.All()
                .Where(n => !n.IsGroup && n.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            SearchList.ItemsSource = results;
            SearchList.SelectedIndex = results.Count > 0 ? 0 : -1;
            SearchList.Visibility = Visibility.Visible;
            Tree.Visibility = Visibility.Collapsed;
        }
        UpdateEmptyText();
    }

    private void UpdateEmptyText()
    {
        if (_tree.Roots.Count == 0)
        {
            EmptyText.Text = "ここにファイルやフォルダー、URL をドラッグして追加できます。\n「＋グループ」で階層を作れます。";
            EmptyText.Visibility = Visibility.Visible;
        }
        else if (IsSearching && SearchList.Items.Count == 0)
        {
            EmptyText.Text = "一致する項目がありません。";
            EmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (IsSearching)
                {
                    if (SearchList.SelectedItem is LauncherItem hit) Launch(hit);
                }
                else if (Tree.SelectedItem is LauncherItem { IsGroup: false } selected)
                {
                    Launch(selected);
                }
                e.Handled = true;
                break;
            case Key.Down:
                if (IsSearching) FocusSearchResult();
                else FocusTree();
                e.Handled = true;
                break;
        }
    }

    private void FocusSearchResult()
    {
        if (SearchList.Items.Count == 0) return;
        if (SearchList.SelectedIndex < 0) SearchList.SelectedIndex = 0;
        (SearchList.ItemContainerGenerator.ContainerFromIndex(SearchList.SelectedIndex) as ListBoxItem)?.Focus();
    }

    private void FocusTree()
    {
        if (_tree.Roots.Count == 0) return;
        var node = Tree.SelectedItem as LauncherItem ?? _tree.Roots[0];
        node.IsSelected = true;
        (Tree.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem)?.Focus();
    }

    private void SearchList_KeyDown(object sender, KeyEventArgs e)
    {
        if (SearchList.SelectedItem is LauncherItem node) HandleNodeKey(node, e);
    }

    private void SearchItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: LauncherItem node }) Launch(node);
    }

    // ---- Tree: click, keys ----

    private void Tree_KeyDown(object sender, KeyEventArgs e)
    {
        if (Tree.SelectedItem is LauncherItem node) HandleNodeKey(node, e);
    }

    private void HandleNodeKey(LauncherItem node, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            var moved = key == Key.Up ? _tree.MoveUp(node) : _tree.MoveDown(node);
            if (moved) Reselect(node);
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.Enter:
                if (node.IsGroup) node.IsExpanded = !node.IsExpanded;
                else Launch(node);
                e.Handled = true;
                break;
            case Key.Delete:
                Remove(node);
                e.Handled = true;
                break;
            case Key.F2:
                Rename(node);
                e.Handled = true;
                break;
        }
    }

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(Tree);
        _pressedNode = RowFrom(e.OriginalSource)?.Node;
    }

    private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedNode is null || e.LeftButton != MouseButtonState.Pressed || _dragging) return;

        var delta = e.GetPosition(Tree) - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var node = _pressedNode;
        _pressedNode = null;
        _dragging = true;
        try
        {
            DragDrop.DoDragDrop(Tree, new DataObject(NodeFormat, node), DragDropEffects.Move);
        }
        finally
        {
            _dragging = false;
            SetDropTarget(null, DropPosition.None);
        }
    }

    private void Tree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pressed = _pressedNode;
        _pressedNode = null;
        if (pressed is null || RowFrom(e.OriginalSource)?.Node != pressed) return;

        if (pressed.IsGroup) pressed.IsExpanded = !pressed.IsExpanded;
        else Launch(pressed);
    }

    // ---- Tree: drag & drop ----

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        var (target, position) = DropSpot(e);
        var allowed = false;

        if (e.Data.GetData(NodeFormat) is LauncherItem dragged)
        {
            var newParent = position == DropPosition.Into ? target : target?.Parent;
            allowed = target != dragged && LauncherTree.CanMove(dragged, newParent);
            e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop) || TryGetDroppedUrl(e.Data) is not null)
        {
            allowed = true;
            e.Effects = DragDropEffects.Link;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        SetDropTarget(allowed ? target : null, position);
        e.Handled = true;
    }

    private void Tree_DragLeave(object sender, DragEventArgs e) => SetDropTarget(null, DropPosition.None);

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        var (target, position) = DropSpot(e);
        SetDropTarget(null, DropPosition.None);
        e.Handled = true;

        var (parent, index) = InsertionPoint(target, position);

        if (e.Data.GetData(NodeFormat) is LauncherItem dragged)
        {
            if (target == dragged) return;
            if (index >= 0 && _tree.SiblingsOf(dragged) == (parent?.Children ?? _tree.Roots)
                && _tree.SiblingsOf(dragged).IndexOf(dragged) < index)
            {
                index--;
            }
            if (!_tree.Move(dragged, parent, index)) return;
            Reselect(dragged);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddDroppedPaths(paths, parent, index);
        }
        else if (TryGetDroppedUrl(e.Data) is { } url)
        {
            AddPaths([url], parent, index);
        }
    }

    private (LauncherItem? Target, DropPosition Position) DropSpot(DragEventArgs e)
    {
        if (RowFrom(e.OriginalSource) is not { } row) return (null, DropPosition.None);

        var y = e.GetPosition(row.Element).Y;
        var height = Math.Max(1, row.Element.ActualHeight);
        if (row.Node.IsGroup)
        {
            if (y < height * 0.25) return (row.Node, DropPosition.Before);
            if (y > height * 0.75) return (row.Node, DropPosition.After);
            return (row.Node, DropPosition.Into);
        }
        return (row.Node, y < height / 2 ? DropPosition.Before : DropPosition.After);
    }

    /// <summary>Where a drop lands: the end of the top level when not over a row.</summary>
    private (LauncherItem? Parent, int Index) InsertionPoint(LauncherItem? target, DropPosition position)
    {
        if (target is null) return (null, -1);
        if (position == DropPosition.Into) return (target, -1);

        var index = _tree.SiblingsOf(target).IndexOf(target);
        return (target.Parent, position == DropPosition.After ? index + 1 : index);
    }

    private void SetDropTarget(LauncherItem? node, DropPosition position)
    {
        if (_dropTarget is not null && _dropTarget != node) _dropTarget.DropPosition = DropPosition.None;
        _dropTarget = node;
        if (node is not null) node.DropPosition = position;
    }

    private static (LauncherItem Node, FrameworkElement Element)? RowFrom(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            if (current is TreeViewItem) return null;
            if (current is FrameworkElement { Tag: "row", DataContext: LauncherItem node } row) return (node, row);
        }
        return null;
    }

    private static LauncherItem? NodeFrom(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            if (current is FrameworkElement { DataContext: LauncherItem node }) return node;
            if (current is TreeView or ListBox) return null;
        }
        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    // ---- Window-level drop (outside the list) adds to the top level ----

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || TryGetDroppedUrl(e.Data) is not null
            ? DragDropEffects.Link
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddDroppedPaths(paths, null, -1);
        else if (TryGetDroppedUrl(e.Data) is { } url) AddPaths([url], null, -1);
    }

    /// <summary>Runs after the drop returns, so Explorer has released the files before originals are recycled.</summary>
    private void AddDroppedPaths(string[] paths, LauncherItem? parent, int index) =>
        Dispatcher.BeginInvoke(() => AddPaths(paths, parent, index));

    private static string? TryGetDroppedUrl(IDataObject data)
    {
        if (data.GetDataPresent(NodeFormat) || data.GetData(DataFormats.UnicodeText) is not string text) return null;
        text = text.Trim();
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? text
            : null;
    }

    // ---- Context menu ----

    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        var node = NodeFrom(e.OriginalSource);
        if (node is not null && sender == Tree) node.IsSelected = true;

        var menu = BuildMenu(node);
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private ContextMenu BuildMenu(LauncherItem? node)
    {
        var menu = new ContextMenu();
        if (node is null)
        {
            menu.Items.Add(MenuItem("新しいグループ", () => AddGroup(null)));
            return menu;
        }

        if (node.IsGroup)
        {
            menu.Items.Add(MenuItem(node.IsExpanded ? "閉じる" : "開く", () => node.IsExpanded = !node.IsExpanded, bold: true));
            menu.Items.Add(MenuItem("この中に新しいグループ", () => AddGroup(node)));
        }
        else
        {
            menu.Items.Add(MenuItem("開く", () => Launch(node), bold: true));
            menu.Items.Add(MenuItem("ファイルの場所を開く", () => OpenLocation(node)));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("名前を変更", () => Rename(node), gesture: "F2"));
        menu.Items.Add(BuildMoveMenu(node));

        var siblings = _tree.SiblingsOf(node);
        var index = siblings.IndexOf(node);
        menu.Items.Add(MenuItem("上へ", () => { if (_tree.MoveUp(node)) Reselect(node); }, gesture: "Alt+↑", enabled: index > 0));
        menu.Items.Add(MenuItem("下へ", () => { if (_tree.MoveDown(node)) Reselect(node); }, gesture: "Alt+↓", enabled: index < siblings.Count - 1));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("ランチャーから削除", () => Remove(node), gesture: "Delete"));
        return menu;
    }

    private MenuItem BuildMoveMenu(LauncherItem node)
    {
        var move = new MenuItem { Header = "移動" };
        if (node.Parent is not null) move.Items.Add(MenuItem("（いちばん上の階層）", () => MoveInto(node, null)));

        foreach (var group in _tree.Groups())
        {
            if (group == node.Parent || !LauncherTree.CanMove(node, group)) continue;
            var indent = new string('　', _tree.Depth(group));
            move.Items.Add(MenuItem(indent + group.Name, () => MoveInto(node, group)));
        }

        move.IsEnabled = move.Items.Count > 0;
        return move;
    }

    private static MenuItem MenuItem(string header, Action action, bool bold = false, string? gesture = null, bool enabled = true)
    {
        var item = new MenuItem
        {
            Header = header.Replace("_", "__"),
            InputGestureText = gesture ?? "",
            IsEnabled = enabled,
        };
        if (bold) item.FontWeight = FontWeights.Bold;
        item.Click += (_, _) => action();
        return item;
    }

    // ---- Commands ----

    private LauncherItem? TargetGroupForAdd()
    {
        var selected = IsSearching ? SearchList.SelectedItem as LauncherItem : Tree.SelectedItem as LauncherItem;
        return selected is null ? null : selected.IsGroup ? selected : selected.Parent;
    }

    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var parent = TargetGroupForAdd();
        var dialog = new OpenFileDialog
        {
            Title = "追加するファイルを選択",
            Multiselect = true,
            DereferenceLinks = false,
        };
        if (WithModal(() => dialog.ShowDialog(this)) == true) AddPaths(dialog.FileNames, parent, -1);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var parent = TargetGroupForAdd();
        var dialog = new OpenFolderDialog
        {
            Title = "追加するフォルダーを選択",
            Multiselect = true,
        };
        if (WithModal(() => dialog.ShowDialog(this)) == true) AddPaths(dialog.FolderNames, parent, -1);
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e) => AddGroup(TargetGroupForAdd());

    private void AddGroup(LauncherItem? parent)
    {
        var dialog = new RenameWindow("新しいグループ", "新しいグループ") { Owner = this };
        if (WithModal(dialog.ShowDialog) != true) return;

        var group = LauncherItem.CreateGroup(dialog.NewName);
        group.IsExpanded = true;
        _tree.Insert(group, parent);
        SearchBox.Clear();
        Reselect(group);
    }

    private void AddPaths(IEnumerable<string> paths, LauncherItem? parent, int index)
    {
        var existing = _tree.All().Where(n => !n.IsGroup).Select(n => n.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notRecycled = new List<string>();
        LauncherItem? last = null;
        foreach (var path in paths)
        {
            if (!existing.Add(path)) continue;

            var item = LauncherItem.CreateItem(DefaultName(path), CaptureShortcut(path, notRecycled));
            item.Icon = LoadIcon(item);
            _tree.Insert(item, parent, index);
            if (index >= 0) index++;
            last = item;
        }

        if (last is not null) Reselect(last);
        if (notRecycled.Count > 0)
        {
            ShowError("ランチャーには登録しましたが、元のショートカットをごみ箱に移せませんでした。\n"
                + "（管理者の権限が必要な場所にある可能性があります）\n\n" + string.Join("\n", notRecycled));
        }
    }

    /// <summary>
    /// When enabled, copies a shortcut into the app folder and recycles the original.
    /// Returns the path the item should point to.
    /// </summary>
    private string CaptureShortcut(string path, List<string> notRecycled)
    {
        if (!_settings.MoveShortcutsToLauncher || !ShortcutFiles.IsShortcut(path) || _store.IsInLinksDirectory(path)) return path;

        string copy;
        try
        {
            copy = ShortcutFiles.CopyInto(path, _store.LinksDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            notRecycled.Add(path);
            return path;
        }

        if (!ShortcutFiles.MoveToRecycleBin(path)) notRecycled.Add(path);
        return copy;
    }

    private void MoveInto(LauncherItem node, LauncherItem? group)
    {
        if (_tree.Move(node, group)) Reselect(node);
    }

    /// <summary>Expands the path to the node, selects it, and saves.</summary>
    private void Reselect(LauncherItem node)
    {
        for (var group = node.Parent; group is not null; group = group.Parent) group.IsExpanded = true;
        node.IsSelected = true;
        if (IsSearching) UpdateSearch();
        Save();
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

    private static ImageSource? LoadIcon(LauncherItem item)
    {
        if (item.IsGroup) return null;
        if (item.IsUrl) return ShellIcon.ForUrl();
        return File.Exists(item.Path) || Directory.Exists(item.Path) ? ShellIcon.ForPath(item.Path) : null;
    }

    private void Launch(LauncherItem item)
    {
        if (item.IsGroup) return;
        if (!item.IsUrl && !File.Exists(item.Path) && !Directory.Exists(item.Path))
        {
            ShowError($"見つかりません。移動または削除された可能性があります。\n{item.Path}");
            return;
        }

        var startInfo = new ProcessStartInfo(item.Path) { UseShellExecute = true };
        if (File.Exists(item.Path) && !ShortcutFiles.IsShortcut(item.Path))
        {
            startInfo.WorkingDirectory = Path.GetDirectoryName(item.Path) ?? "";
        }

        try
        {
            Process.Start(startInfo);
            HideLauncher();
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

    private void OpenLocation(LauncherItem item)
    {
        if (item.IsUrl)
        {
            ShowError("URL には保存場所がありません。");
            return;
        }
        var path = item.Path;
        if (_store.IsInLinksDirectory(path))
        {
            path = ShortcutFiles.ResolveTarget(path) ?? "";
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                ShowError($"このショートカットのリンク先は URL です。\n{path}");
                return;
            }
        }
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            ShowError($"見つかりません。\n{(path.Length > 0 ? path : item.Path)}");
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private void Rename(LauncherItem node)
    {
        var dialog = new RenameWindow(node.Name) { Owner = this };
        if (WithModal(dialog.ShowDialog) != true) return;

        node.Name = dialog.NewName;
        if (IsSearching) UpdateSearch();
        Save();
    }

    private void Remove(LauncherItem node)
    {
        if (node.IsGroup && node.Children.Count > 0)
        {
            var count = _tree.All().Count(n => IsDescendant(n, node));
            var answer = WithModal(() => MessageBox.Show(this,
                $"グループ「{node.Name}」と、中の {count} 件をランチャーから削除しますか？\n"
                + "（ファイルやフォルダー自体は削除されません。取り込んだショートカットはごみ箱に移ります）",
                Title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel));
            if (answer != MessageBoxResult.OK) return;
        }

        var captured = _tree.All()
            .Where(n => (n == node || IsDescendant(n, node)) && !n.IsGroup && _store.IsInLinksDirectory(n.Path))
            .Select(n => n.Path)
            .ToList();

        _tree.Remove(node);
        if (IsSearching) UpdateSearch();
        Save();

        foreach (var path in captured.Where(File.Exists)) ShortcutFiles.MoveToRecycleBin(path);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settingsStore, _settings) { Owner = this };
        WithModal(dialog.ShowDialog);
    }

    private static bool IsDescendant(LauncherItem node, LauncherItem ancestor)
    {
        for (var group = node.Parent; group is not null; group = group.Parent)
        {
            if (group == ancestor) return true;
        }
        return false;
    }

    private void Save()
    {
        try
        {
            _store.Save(_tree);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"保存できませんでした。\n{_store.FilePath}\n\n{ex.Message}");
        }
    }

    private void ShowError(string message) => WithModal(() => IsVisible
        ? MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Warning)
        : MessageBox.Show(message, Title, MessageBoxButton.OK, MessageBoxImage.Warning));
}
