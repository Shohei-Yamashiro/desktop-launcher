using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopLauncher.Controls;
using DesktopLauncher.Models;
using DesktopLauncher.Services;
using Microsoft.Win32;

namespace DesktopLauncher;

/// <summary>The tabs above the search box.</summary>
public enum LauncherView
{
    All,
    Favorites,
    Frequent,
}

public partial class MainWindow : Window
{
    private const string NodeFormat = "DesktopLauncher.Node";

    private readonly ItemStore _store = new();
    private readonly LauncherTree _tree = new();
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly TagColors _tagColors;
    private int _modalDepth;
    private bool _exiting;

    private Point _pressPoint;
    private LauncherItem? _pressedNode;
    private bool _dragging;
    private LauncherItem? _dropTarget;
    private string? _activeTag;

    private LauncherView _view = LauncherView.All;

    private LauncherItem? _anchor;
    private bool _collapseOnUp;
    private bool _settingCurrent;

    public MainWindow(SettingsStore settingsStore, AppSettings settings)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _settings = settings;
        _tagColors = new TagColors(settings.TagColors);
        ((TagBrushConverter)Resources["TagBrush"]).Colors = _tagColors;
        DarkFrame.Apply(this);
        ApplyRowSpacing();

        Tree.ItemsSource = _tree.Roots;
        _tree.Roots.CollectionChanged += (_, _) => UpdateEmptyText();

        LoadItems();
    }

    public event Action? ClosedToTray;

    /// <summary>Text is typed or a tag is chosen.</summary>
    private bool IsFiltering => SearchBox.Text.Trim().Length > 0 || _activeTag is not null;

    /// <summary>The flat result list replaces the tree while filtering and on every tab but すべて.</summary>
    private bool ShowsList => _view != LauncherView.All || IsFiltering;

    /// <summary>Screen rectangle the window opens from (the mini icon).</summary>
    public Func<Rect>? Anchor { get; set; }

    /// <summary>The hotkey that toggles this window, changed from the settings.</summary>
    public HotKey? HotKey { get; set; }

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
        TagSuggest.IsOpen = false;
        ClearFilter();
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
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.FocusedElement is not TextBox
            && PasteFromClipboard())
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            SwitchTab(backward: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            if (TagSuggest.IsOpen) TagSuggest.IsOpen = false;
            else if (SearchBox.Text.Length > 0) SearchBox.Clear();
            else if (_activeTag is not null) SetActiveTag(null);
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

        UpdateTagBar();
        UpdateEmptyText();
    }

    // ---- Tags ----

    public sealed record TagChip(string Name, Brush Brush, bool IsActive);

    private void UpdateTagBar()
    {
        var tags = _tree.AllTags();
        if (_activeTag is not null && !tags.Contains(_activeTag, StringComparer.CurrentCultureIgnoreCase)) _activeTag = null;

        _tagColors.KeepOnly(tags);
        TagBar.ItemsSource = tags
            .Select(t => new TagChip(t, _tagColors.BrushFor(t), string.Equals(t, _activeTag, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
        TagBar.Visibility = tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_tagColors.Changed)
        {
            _settings.TagColors = new(_tagColors.Assigned);
            _settingsStore.Save(_settings);
            _tagColors.Changed = false;
        }
    }

    private void TagChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TagChip chip }) SetActiveTag(chip.IsActive ? null : chip.Name);
    }

    private void SetActiveTag(string? tag)
    {
        _activeTag = tag;
        UpdateTagBar();
        UpdateSearch();
    }

    private void ClearFilter()
    {
        SearchBox.Clear();
        if (_activeTag is not null) SetActiveTag(null);
    }

    /// <summary>Back to the tree with no filter, so that something just added is in view.</summary>
    private void ShowAll()
    {
        AllTab.IsChecked = true;
        ClearFilter();
    }

    private void ViewTab_Checked(object sender, RoutedEventArgs e)
    {
        // Raised while the XAML is still loading, before the lists exist.
        if (SearchList is null) return;
        _view = FavoritesTab.IsChecked == true ? LauncherView.Favorites
            : FrequentTab.IsChecked == true ? LauncherView.Frequent
            : LauncherView.All;
        UpdateSearch();
    }

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab move between the tabs.</summary>
    private void SwitchTab(bool backward)
    {
        RadioButton[] tabs = [AllTab, FavoritesTab, FrequentTab];
        var index = Array.FindIndex(tabs, t => t.IsChecked == true);
        tabs[(index + (backward ? tabs.Length - 1 : 1)) % tabs.Length].IsChecked = true;
    }

    /// <summary>Forgets how often the items were opened, which takes them off the よく使う tab.</summary>
    private void ClearLaunches(IReadOnlyList<LauncherItem> nodes)
    {
        foreach (var item in nodes.Where(n => !n.IsGroup)) item.ClearLaunches();
        if (_view == LauncherView.Frequent) UpdateSearch();
        Save();
    }

    /// <summary>Adds the items to the favorites, or removes them when all of them already are.</summary>
    private void ToggleFavorite(IReadOnlyList<LauncherItem> nodes)
    {
        var items = nodes.Where(n => !n.IsGroup).ToList();
        if (items.Count == 0) return;

        var favorite = !items.All(n => n.IsFavorite);
        foreach (var item in items) item.IsFavorite = favorite;
        if (_view == LauncherView.Favorites) UpdateSearch();
        Save();
    }

    /// <summary>One node: name, path or URL, and tags. Several nodes: their tags.</summary>
    private void Edit(IReadOnlyList<LauncherItem> nodes)
    {
        var heading = $"選んだ {nodes.Count} 件に付けるタグ";
        var note = nodes.Any(n => n.IsGroup) ? "グループのタグで絞り込むと、中の項目もすべて表示されます。" : null;

        var dialog = new EditWindow(heading, note, _tree.AllTags(), nodes, _tagColors.BrushFor, DefaultIcon) { Owner = this };
        if (WithModal(dialog.ShowDialog) != true) return;

        if (nodes is [var single])
        {
            single.Name = dialog.NewName;
            single.IsFavorite = dialog.IsFavorite;
            if (dialog.NewPath is { } path)
            {
                var old = single.Path;
                single.Path = path;
                single.Icon = LoadIcon(single);
                if (_store.IsInLinksDirectory(old) && File.Exists(old)) ShortcutFiles.MoveToRecycleBin(old);
            }
            ApplyIconChoice(single, dialog.IconChoice, dialog.IconFile);
        }
        foreach (var node in nodes) node.ApplyTagChanges(dialog.AddedTags, dialog.RemovedTags);
        UpdateTagBar();
        if (ShowsList) UpdateSearch();
        Save();
    }

    /// <summary>Copies a picked image into data\icons (so it stays when the original moves), or goes back to the default.</summary>
    private void ApplyIconChoice(LauncherItem node, EditWindow.IconChange choice, string? file)
    {
        if (choice == EditWindow.IconChange.None) return;

        var old = node.CustomIcon;
        if (choice == EditWindow.IconChange.File && file is not null)
        {
            try
            {
                node.CustomIcon = _store.ImportIcon(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowError($"アイコンの画像をコピーできませんでした。\n{file}\n\n{ex.Message}");
                return;
            }
        }
        else
        {
            node.CustomIcon = null;
        }
        _store.DeleteIcon(old);
        node.Icon = LoadIcon(node);
    }

    // ---- Search ----

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearch();
        UpdateSuggestions();
    }

    // ---- Search: tag suggestions ----

    public sealed record TagSuggestion(string Name, Brush Brush, int Count)
    {
        public string CountText => $"{Count} 件";
    }

    private const int MaxSuggestions = 8;

    /// <summary>The space-separated word around the caret.</summary>
    private (int Start, int Length) CurrentWord()
    {
        var text = SearchBox.Text;
        var caret = Math.Min(SearchBox.CaretIndex, text.Length);
        var start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        var end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        return (start, end - start);
    }

    /// <summary>
    /// Lists existing tags that contain the word being typed (those starting with it first).
    /// After "#" a tag is clearly meant, so the first one is preselected for Tab/Enter;
    /// for a plain word Enter keeps launching the first result.
    /// </summary>
    private void UpdateSuggestions()
    {
        var (start, length) = CurrentWord();
        var word = SearchBox.Text.Substring(start, length);
        var isTag = SearchQuery.IsTagWord(word);
        var key = isTag ? word[1..] : word;
        if (key.Length == 0 && !isTag)
        {
            TagSuggest.IsOpen = false;
            return;
        }

        var allTags = _tree.AllTags();
        var used = SearchQuery.Parse(SearchBox.Text, allTags).Tags.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var candidates = allTags
            .Where(t => t.Contains(key, StringComparison.CurrentCultureIgnoreCase))
            .Where(t => !used.Contains(t) || string.Equals(t, key, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(t => t.StartsWith(key, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1)
            .Take(MaxSuggestions)
            .ToList();

        // Nothing left to complete once the word already is the only matching tag.
        if (candidates.Count == 0
            || (isTag && candidates is [var only] && string.Equals(only, key, StringComparison.CurrentCultureIgnoreCase)))
        {
            TagSuggest.IsOpen = false;
            return;
        }

        SuggestList.ItemsSource = candidates
            .Select(t => new TagSuggestion(t, _tagColors.BrushFor(t), _tree.All().Count(n => !n.IsGroup && n.HasTagIncludingGroups(t))))
            .ToList();
        SuggestList.SelectedIndex = isTag ? 0 : -1;
        TagSuggest.IsOpen = SearchBox.IsKeyboardFocused;
    }

    /// <summary>Replaces the word being typed with "#tag ".</summary>
    private void AcceptSuggestion(TagSuggestion suggestion)
    {
        var (start, length) = CurrentWord();
        var text = SearchBox.Text;
        var insert = SearchQuery.TagPrefix + suggestion.Name + " ";
        var rest = text[(start + length)..].TrimStart();
        SearchBox.Text = text[..start] + insert + rest;
        SearchBox.CaretIndex = start + insert.Length;
        TagSuggest.IsOpen = false;
        SearchBox.Focus();
    }

    /// <summary>Handles the suggestion keys; returns true when the key was used.</summary>
    private bool HandleSuggestionKey(Key key)
    {
        if (!TagSuggest.IsOpen || SuggestList.Items.Count == 0) return false;
        switch (key)
        {
            case Key.Down:
                SuggestList.SelectedIndex = Math.Min(SuggestList.SelectedIndex + 1, SuggestList.Items.Count - 1);
                SuggestList.ScrollIntoView(SuggestList.SelectedItem);
                return true;
            case Key.Up:
                SuggestList.SelectedIndex = Math.Max(SuggestList.SelectedIndex - 1, -1);
                return true;
            case Key.Tab:
                AcceptSuggestion((TagSuggestion)(SuggestList.SelectedItem ?? SuggestList.Items[0]));
                return true;
            case Key.Enter when SuggestList.SelectedItem is TagSuggestion selected:
                AcceptSuggestion(selected);
                return true;
            default:
                return false;
        }
    }

    private void SuggestList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject) is { } source
            && ItemsControl.ContainerFromElement(SuggestList, source) is ListBoxItem { DataContext: TagSuggestion suggestion })
        {
            AcceptSuggestion(suggestion);
            e.Handled = true;
        }
    }

    private void SearchBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => TagSuggest.IsOpen = false;

    private void UpdateSearch()
    {
        // Hidden rows must not stay selected, or commands would act on them.
        SelectOnly(null);
        if (!ShowsList)
        {
            SearchList.ItemsSource = null;
            SearchList.Visibility = Visibility.Collapsed;
            Tree.Visibility = Visibility.Visible;
        }
        else
        {
            var matches = _tree.FindItems(SearchBox.Text, _activeTag);
            var results = _view switch
            {
                LauncherView.Favorites => matches.Where(n => n.IsFavorite).ToList(),
                LauncherView.Frequent => FrequentItems.Rank(matches, DateTime.Now).ToList(),
                _ => matches.ToList(),
            };
            SearchList.ItemsSource = results;
            SearchList.SelectedIndex = -1;
            SearchList.Visibility = Visibility.Visible;
            Tree.Visibility = Visibility.Collapsed;
        }
        UpdateEmptyText();
    }

    private void UpdateEmptyText()
    {
        if (_tree.Roots.Count == 0)
        {
            EmptyText.Text = "ここにファイルやフォルダー、URL をドラッグして追加できます。\nコピーしたものは Ctrl+V で貼り付けられます。「＋グループ」で階層を作れます。";
            EmptyText.Visibility = Visibility.Visible;
        }
        else if (ShowsList && SearchList.Items.Count == 0)
        {
            EmptyText.Text = (_view, IsFiltering) switch
            {
                (LauncherView.Favorites, false) =>
                    "お気に入りはまだありません。\n行の先頭の ☆ をクリックするか、Ctrl+D を押すと、ここに表示されます。",
                (LauncherView.Frequent, false) =>
                    "よく使う項目はまだありません。\n項目を開くと、開いた回数と新しさの順にここに並びます。",
                _ => "一致する項目がありません。",
            };
            EmptyText.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleSuggestionKey(e.Key))
        {
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            // Enter moves to the first result; a second Enter there launches it.
            case Key.Enter:
            case Key.Down:
                if (ShowsList) FocusSearchResult();
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
        var node = Tree.SelectedItem as LauncherItem ?? SelectedNodes().FirstOrDefault() ?? _tree.Roots[0];
        SelectOnly(node);
        SetCurrent(node);
        (Tree.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem)?.Focus();
    }

    private void SearchList_KeyDown(object sender, KeyEventArgs e)
    {
        if (SearchList.SelectedItem is LauncherItem node) HandleNodeKey(node, e);
    }

    /// <summary>Covers rows that are not realized, which the IsSelected binding cannot reach.</summary>
    private void SearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var node in e.RemovedItems.OfType<LauncherItem>()) node.IsSelected = false;
        foreach (var node in e.AddedItems.OfType<LauncherItem>()) node.IsSelected = true;
    }

    private void SearchItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Keyboard.Modifiers != ModifierKeys.None || IsOnStar(e.OriginalSource)) return;
        if (sender is ListBoxItem { DataContext: LauncherItem node }) Launch(node);
    }

    // ---- Selection ----

    /// <summary>Selected nodes in tree order.</summary>
    private List<LauncherItem> SelectedNodes() => _tree.All().Where(n => n.IsSelected).ToList();

    private void SelectOnly(LauncherItem? node)
    {
        foreach (var n in _tree.All()) n.IsSelected = n == node;
        if (node is not null) _anchor = node;
    }

    /// <summary>Selects the visible rows between the anchor and the node.</summary>
    private void SelectRange(LauncherItem node, bool keepOthers)
    {
        var visible = _tree.Visible().ToList();
        var to = visible.IndexOf(node);
        var from = _anchor is null ? -1 : visible.IndexOf(_anchor);
        if (from < 0) from = to;
        var (low, high) = from <= to ? (from, to) : (to, from);

        if (!keepOthers) foreach (var n in _tree.All()) n.IsSelected = false;
        for (var i = low; i <= high; i++) visible[i].IsSelected = true;
    }

    /// <summary>Moves the TreeView's cursor without letting it reset the multi-selection.</summary>
    private void SetCurrent(LauncherItem node)
    {
        _settingCurrent = true;
        try
        {
            // A node that was moved keeps IsCurrent from its old container; toggle so the new one selects.
            foreach (var n in _tree.All()) n.IsCurrent = false;
            node.IsCurrent = true;
        }
        finally
        {
            _settingCurrent = false;
        }
    }

    /// <summary>Arrow keys move the TreeView's own selection; follow it, extending with Shift.</summary>
    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_settingCurrent || Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed) return;
        if (e.NewValue is not LauncherItem node) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) SelectRange(node, keepOthers: false);
        else SelectOnly(node);
    }

    // ---- Tree: click, keys ----

    private void Tree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            foreach (var node in _tree.Visible()) node.IsSelected = true;
            e.Handled = true;
            return;
        }
        if ((Tree.SelectedItem as LauncherItem ?? SelectedNodes().FirstOrDefault()) is { } current) HandleNodeKey(current, e);
    }

    private void HandleNodeKey(LauncherItem node, KeyEventArgs e)
    {
        var selection = SelectedNodes();
        if (selection.Count == 0) selection = [node];
        var single = selection.Count == 1 ? selection[0] : null;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            if (single is not null && (key == Key.Up ? _tree.MoveUp(single) : _tree.MoveDown(single))) Reselect(single);
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.Enter when single is not null:
                if (single.IsGroup) single.IsExpanded = !single.IsExpanded;
                else Launch(single);
                e.Handled = true;
                break;
            case Key.Delete:
                Remove(selection);
                e.Handled = true;
                break;
            case Key.D when Keyboard.Modifiers == ModifierKeys.Control:
                ToggleFavorite(selection);
                e.Handled = true;
                break;
            case Key.F2:
                Edit(selection);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Click selects, Ctrl+click toggles, Shift+click selects a range, double-click launches an item
    /// (a group opens or closes by the TreeView's own double-click handling).
    /// </summary>
    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(Tree);
        _pressedNode = IsOnStar(e.OriginalSource) ? null : RowFrom(e.OriginalSource)?.Node;
        _collapseOnUp = false;
        if (_pressedNode is not { } node) return;

        var modifiers = Keyboard.Modifiers;
        if (e.ClickCount == 2)
        {
            if (!node.IsGroup && modifiers == ModifierKeys.None)
            {
                _pressedNode = null;
                e.Handled = true;
                Launch(node);
            }
            return;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            SelectRange(node, keepOthers: modifiers.HasFlag(ModifierKeys.Control));
        }
        else if (modifiers.HasFlag(ModifierKeys.Control))
        {
            node.IsSelected = !node.IsSelected;
            _anchor = node;
        }
        else if (node.IsSelected && SelectedNodes().Count > 1)
        {
            // Keep the selection so that it can be dragged; a plain click narrows it on release.
            _collapseOnUp = true;
        }
        else
        {
            SelectOnly(node);
        }
    }

    private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedNode is null || e.LeftButton != MouseButtonState.Pressed || _dragging) return;

        var delta = e.GetPosition(Tree) - _pressPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var node = _pressedNode;
        _pressedNode = null;
        _collapseOnUp = false;
        var nodes = node.IsSelected ? _tree.TopMost(SelectedNodes()) : [node];

        _dragging = true;
        try
        {
            DragDrop.DoDragDrop(Tree, new DataObject(NodeFormat, nodes.ToArray()), DragDropEffects.Move);
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
        if (_collapseOnUp && pressed is not null && RowFrom(e.OriginalSource)?.Node == pressed) SelectOnly(pressed);
        _collapseOnUp = false;
    }

    // ---- Tree: drag & drop ----

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        var (target, position) = DropSpot(e);
        var allowed = false;

        if (e.Data.GetData(NodeFormat) is LauncherItem[] dragged)
        {
            var newParent = position == DropPosition.Into ? target : target?.Parent;
            allowed = (target is null || !dragged.Contains(target)) && dragged.All(n => LauncherTree.CanMove(n, newParent));
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

        if (e.Data.GetData(NodeFormat) is LauncherItem[] dragged)
        {
            if (target is not null && dragged.Contains(target)) return;
            if (!_tree.MoveMany(dragged, parent, index)) return;
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

    /// <summary>The star at the start of a row toggles the favorite; it must not select, drag or launch the row.</summary>
    private static bool IsOnStar(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            if (current is FrameworkElement { Tag: "star" }) return true;
            if (current is FrameworkElement { Tag: "row" } or ListBoxItem or TreeViewItem) return false;
        }
        return false;
    }

    private void FavoriteStar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LauncherItem node }) ToggleFavorite([node]);
        e.Handled = true;
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
        return WebUrl.IsWebUrl(text) ? text : null;
    }

    // ---- Context menu ----

    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        var node = NodeFrom(e.OriginalSource);
        if (node is not null && !node.IsSelected) SelectOnly(node);

        var selection = node is null ? [] : _tree.TopMost(SelectedNodes());
        var menu = selection.Count > 1 ? BuildMultiMenu(selection) : BuildMenu(node);
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    private ContextMenu BuildMenu(LauncherItem? node)
    {
        var menu = new ContextMenu();
        if (node is null)
        {
            menu.Items.Add(MenuItem("新しいグループ", () => AddGroup(null)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("すべてのグループを開く", () => SetExpanded(true, null)));
            menu.Items.Add(MenuItem("すべてのグループを閉じる", () => SetExpanded(false, null)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("ブラウザのブックマークを取り込む…", () => ImportBookmarks(this)));
            return menu;
        }

        if (node.IsGroup)
        {
            menu.Items.Add(MenuItem(node.IsExpanded ? "閉じる" : "開く", () => node.IsExpanded = !node.IsExpanded, bold: true));
            if (node.Children.Any(c => c.IsGroup))
            {
                menu.Items.Add(MenuItem("中のグループもすべて開く", () => SetExpanded(true, node)));
                menu.Items.Add(MenuItem("中のグループもすべて閉じる", () => SetExpanded(false, node)));
            }
            menu.Items.Add(MenuItem("この中に新しいグループ", () => AddGroup(node)));
        }
        else
        {
            menu.Items.Add(MenuItem("開く", () => Launch(node), bold: true));
            menu.Items.Add(MenuItem("ファイルの場所を開く", () => OpenLocation(node)));
            menu.Items.Add(MenuItem(node.IsFavorite ? "お気に入りから外す" : "お気に入りに追加", () => ToggleFavorite([node]), gesture: "Ctrl+D"));
            if (node.LaunchCount > 0) menu.Items.Add(MenuItem("よく使うから外す", () => ClearLaunches([node])));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("編集…", () => Edit([node]), gesture: "F2"));
        menu.Items.Add(BuildMoveMenu([node]));

        var siblings = _tree.SiblingsOf(node);
        var index = siblings.IndexOf(node);
        menu.Items.Add(MenuItem("上へ", () => { if (_tree.MoveUp(node)) Reselect(node); }, gesture: "Alt+↑", enabled: index > 0));
        menu.Items.Add(MenuItem("下へ", () => { if (_tree.MoveDown(node)) Reselect(node); }, gesture: "Alt+↓", enabled: index < siblings.Count - 1));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("ランチャーから削除", () => Remove([node]), gesture: "Delete"));
        return menu;
    }

    private ContextMenu BuildMultiMenu(IReadOnlyList<LauncherItem> nodes)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItem($"{nodes.Count} 件を選択中", () => { }, enabled: false));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("タグを編集…", () => Edit(nodes), gesture: "F2"));
        var items = nodes.Where(n => !n.IsGroup).ToList();
        if (items.Count > 0)
        {
            menu.Items.Add(MenuItem(items.All(n => n.IsFavorite) ? "お気に入りから外す" : "お気に入りに追加",
                () => ToggleFavorite(nodes), gesture: "Ctrl+D"));
            if (items.Any(n => n.LaunchCount > 0)) menu.Items.Add(MenuItem("よく使うから外す", () => ClearLaunches(nodes)));
        }
        menu.Items.Add(BuildMoveMenu(nodes));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("ランチャーから削除", () => Remove(nodes), gesture: "Delete"));
        return menu;
    }

    private MenuItem BuildMoveMenu(IReadOnlyList<LauncherItem> nodes)
    {
        var move = new MenuItem { Header = "移動" };
        if (nodes.Any(n => n.Parent is not null)) move.Items.Add(MenuItem("（いちばん上の階層）", () => MoveInto(nodes, null)));

        foreach (var group in _tree.Groups())
        {
            if (nodes.All(n => n.Parent == group) || !nodes.All(n => LauncherTree.CanMove(n, group))) continue;
            var indent = new string('　', _tree.Depth(group));
            move.Items.Add(MenuItem(indent + group.Name, () => MoveInto(nodes, group)));
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
        var selected = SelectedNodes().FirstOrDefault();
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

    private void ExpandAll_Click(object sender, RoutedEventArgs e) => SetExpanded(true, null);

    private void CollapseAll_Click(object sender, RoutedEventArgs e) => SetExpanded(false, null);

    /// <summary>Opens or closes all groups (or a group and the groups inside it). Selection hidden by closing moves to the group that hides it.</summary>
    private void SetExpanded(bool expanded, LauncherItem? group)
    {
        if (!_tree.SetExpanded(expanded, group)) return;

        if (!expanded)
        {
            var visible = _tree.Visible().ToHashSet();
            var hidden = SelectedNodes().Where(n => !visible.Contains(n)).ToList();
            if (hidden.Count > 0)
            {
                var shown = hidden[0].Parent;
                while (shown is not null && !visible.Contains(shown)) shown = shown.Parent;
                foreach (var n in hidden) n.IsSelected = false;
                if (shown is not null)
                {
                    shown.IsSelected = true;
                    _anchor = shown;
                    SetCurrent(shown);
                }
            }
        }
        Save();
    }

    private void AddGroup(LauncherItem? parent)
    {
        var dialog = new RenameWindow("新しいグループ", "新しいグループ") { Owner = this };
        if (WithModal(dialog.ShowDialog) != true) return;

        var group = LauncherItem.CreateGroup(dialog.NewName);
        group.IsExpanded = true;
        _tree.Insert(group, parent);
        ShowAll();
        Reselect(group);
    }

    private void AddUrl_Click(object sender, RoutedEventArgs e)
    {
        var parent = TargetGroupForAdd();
        var dialog = new UrlWindow { Owner = this };
        if (WithModal(dialog.ShowDialog) != true) return;

        ShowAll();
        AddPaths([dialog.Url], parent, -1, dialog.ItemName);
    }

    /// <summary>Registers files copied in Explorer or a URL copied as text. Returns false when there is neither.</summary>
    private bool PasteFromClipboard()
    {
        var parent = TargetGroupForAdd();
        if (ClipboardContent.Files() is { Length: > 0 } files)
        {
            ShowAll();
            AddDroppedPaths(files, parent, -1);
            return true;
        }
        if (ClipboardContent.Url() is { } url)
        {
            ShowAll();
            AddPaths([url], parent, -1);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Files and folders already registered are not added again; their items get selected instead.
    /// URLs may be registered more than once.
    /// </summary>
    private void AddPaths(IEnumerable<string> paths, LauncherItem? parent, int index, string? name = null)
    {
        var existing = new Dictionary<string, LauncherItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in _tree.All().Where(n => !n.IsGroup)) existing.TryAdd(node.Path, node);

        var notRecycled = new List<string>();
        var touched = new List<LauncherItem>();
        foreach (var path in paths)
        {
            if (!WebUrl.IsWebUrl(path) && existing.TryGetValue(path, out var registered))
            {
                if (!touched.Contains(registered)) touched.Add(registered);
                continue;
            }

            var item = LauncherItem.CreateItem(name ?? DefaultName(path), CaptureShortcut(path, notRecycled));
            item.Icon = LoadIcon(item);
            _tree.Insert(item, parent, index);
            if (index >= 0) index++;
            existing.TryAdd(path, item);
            touched.Add(item);
        }

        if (touched.Count > 0) Reselect(touched);
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

    private void MoveInto(IReadOnlyList<LauncherItem> nodes, LauncherItem? group)
    {
        if (_tree.MoveMany(nodes, group)) Reselect(nodes);
    }

    private void Reselect(LauncherItem node) => Reselect([node]);

    /// <summary>Expands the paths to the nodes, selects them, and saves.</summary>
    private void Reselect(IReadOnlyList<LauncherItem> nodes)
    {
        if (ShowsList) UpdateSearch();
        var visible = ShowsList ? SearchList.Items.OfType<LauncherItem>().ToHashSet() : null;

        foreach (var n in _tree.All()) n.IsSelected = false;
        foreach (var node in nodes)
        {
            if (visible is not null && !visible.Contains(node)) continue;
            for (var group = node.Parent; group is not null; group = group.Parent) group.IsExpanded = true;
            node.IsSelected = true;
        }
        if (nodes.Count > 0)
        {
            _anchor = nodes[0];
            if (!ShowsList) SetCurrent(nodes[0]);
        }
        Save();
    }

    private static string DefaultName(string path)
    {
        if (WebUrl.IsWebUrl(path)) return WebUrl.DefaultName(path);
        if (Directory.Exists(path))
        {
            var name = new DirectoryInfo(path).Name;
            return name.Length > 0 ? name : path;
        }
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>The image the user chose, or else the default icon.</summary>
    private static ImageSource? LoadIcon(LauncherItem item) =>
        item.CustomIcon is { } custom && ItemIcons.LoadImage(custom) is { } image ? image : DefaultIcon(item);

    /// <summary>Shell icon for files and folders, a letter badge for URLs, none for groups (they show a folder glyph).</summary>
    private static ImageSource? DefaultIcon(LauncherItem item)
    {
        if (item.IsGroup) return null;
        if (item.IsUrl) return ItemIcons.LetterBadgeFor(item.Path);
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
            item.RecordLaunch(DateTime.Now);
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

    private void Remove(IReadOnlyList<LauncherItem> selection)
    {
        var nodes = _tree.TopMost(selection);
        if (nodes.Count == 0) return;

        var inside = _tree.All().Count(n => nodes.Any(top => IsDescendant(n, top)));
        string? question = null;
        if (nodes.Count > 1)
        {
            question = $"選んだ {nodes.Count} 件" + (inside > 0 ? $"と、グループの中の {inside} 件" : "") + "をランチャーから削除しますか？";
        }
        else if (inside > 0)
        {
            question = $"グループ「{nodes[0].Name}」と、中の {inside} 件をランチャーから削除しますか？";
        }
        if (question is not null)
        {
            var answer = WithModal(() => MessageBox.Show(this,
                question + "\n（ファイルやフォルダー自体は削除されません。取り込んだショートカットはごみ箱に移ります）",
                Title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel));
            if (answer != MessageBoxResult.OK) return;
        }

        var removed = _tree.All().Where(n => nodes.Any(top => n == top || IsDescendant(n, top))).ToList();
        var captured = removed.Where(n => !n.IsGroup && _store.IsInLinksDirectory(n.Path)).Select(n => n.Path).ToList();
        var icons = removed.Select(n => n.CustomIcon).OfType<string>().ToList();

        foreach (var node in nodes) _tree.Remove(node);
        _anchor = null;
        UpdateTagBar();
        if (ShowsList) UpdateSearch();
        Save();

        foreach (var path in captured.Where(File.Exists)) ShortcutFiles.MoveToRecycleBin(path);
        foreach (var icon in icons) _store.DeleteIcon(icon);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => HideLauncher();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settingsStore, _settings, HotKey, ImportBookmarks) { Owner = this };
        if (WithModal(dialog.ShowDialog) == true) ApplyRowSpacing();
    }

    /// <summary>Brings browser bookmarks in as one new group, opened from the list's menu or the settings.</summary>
    private void ImportBookmarks(Window owner)
    {
        var dialog = new ImportBookmarksWindow(BrowserBookmarks.Find()) { Owner = owner };
        if (WithModal(dialog.ShowDialog) != true) return;

        var result = BookmarkImporter.Import(_tree, dialog.GroupName, dialog.Roots, dialog.Options);
        var skipped = result.Skipped > 0 ? $"\n（登録済みの {result.Skipped} 件は取り込みませんでした）" : "";
        if (result.Group is not { } group)
        {
            ShowInfo("取り込める URL がありませんでした。" + skipped);
            return;
        }

        foreach (var node in _tree.All().Where(n => IsDescendant(n, group))) node.Icon = LoadIcon(node);
        group.IsExpanded = true;
        ShowAll();
        Reselect(group);
        ShowInfo($"{result.Added} 件のブックマークを「{group.Name}」グループに取り込みました。" + skipped);
    }

    private void ShowInfo(string message) => WithModal(() => MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information));

    private void ApplyRowSpacing()
    {
        var vertical = _settings.RowSpacing.VerticalPadding();
        Resources["RowPadding"] = new Thickness(4, vertical, 8, vertical);
        Resources["SearchRowPadding"] = new Thickness(8, vertical, 8, vertical);
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
