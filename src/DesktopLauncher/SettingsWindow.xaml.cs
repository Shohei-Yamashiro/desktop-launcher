using System.Windows;
using System.Windows.Input;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher;

public partial class SettingsWindow : Window
{
    private const string HotKeyHint = "欄を選んで、使いたいキーの組み合わせを押します。\nCtrl・Alt・Win のどれかを含めてください。";

    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly HotKey? _hotKey;
    private HotKeyGesture _gesture;

    public SettingsWindow(SettingsStore store, AppSettings settings, HotKey? hotKey)
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        _store = store;
        _settings = settings;
        _hotKey = hotKey;
        _gesture = hotKey?.Gesture ?? HotKeyGesture.Parse(settings.HotKey) ?? HotKeyGesture.Default;
        HotKeyBox.Text = _gesture.DisplayText;
        HotKeyNote.Text = HotKeyHint;

        // While this window is open the current hotkey is released, so pressing it here types it into the box.
        if (hotKey is not null)
        {
            Loaded += (_, _) => hotKey.Suspend();
            Closed += (_, _) => hotKey.Register();
        }

        MoveShortcutsBox.IsChecked = settings.MoveShortcutsToLauncher;
        (settings.RowSpacing switch
        {
            RowSpacing.Compact => CompactRadio,
            RowSpacing.Relaxed => RelaxedRadio,
            _ => NormalRadio,
        }).IsChecked = true;
        StartupBox.IsChecked = StartupRegistration.IsEnabled;
    }

    private void HotKeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var modifiers = Keyboard.Modifiers;

        // Plain Tab, Enter and Esc keep moving focus, pressing OK and closing the window.
        if (modifiers is ModifierKeys.None or ModifierKeys.Shift && key is Key.Tab or Key.Enter or Key.Escape) return;
        e.Handled = true;

        if (HotKeyGesture.IsModifierKey(key))
        {
            HotKeyBox.Text = HotKeyGesture.PartialText(modifiers);
            return;
        }

        var gesture = new HotKeyGesture(modifiers, key);
        if (!gesture.IsValid)
        {
            HotKeyBox.Text = _gesture.DisplayText;
            HotKeyNote.Text = "Ctrl・Alt・Win のどれかと一緒に押してください。";
            return;
        }

        _gesture = gesture;
        HotKeyBox.Text = gesture.DisplayText;
        HotKeyNote.Text = HotKeyHint;
    }

    /// <summary>Shows the chosen combination again when only modifier keys were pressed and released.</summary>
    private void HotKeyBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None) HotKeyBox.Text = _gesture.DisplayText;
    }

    private void ResetHotKey_Click(object sender, RoutedEventArgs e)
    {
        _gesture = HotKeyGesture.Default;
        HotKeyBox.Text = _gesture.DisplayText;
        HotKeyNote.Text = HotKeyHint;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_hotKey is not null && _gesture != _hotKey.Gesture && !_hotKey.Change(_gesture))
        {
            MessageBox.Show(this, $"{_gesture.DisplayText} は他のアプリで使われているため登録できませんでした。\n別の組み合わせを選んでください。",
                Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            HotKeyBox.Focus();
            return;
        }
        _settings.HotKey = _gesture == HotKeyGesture.Default ? null : _gesture.DisplayText;

        _settings.MoveShortcutsToLauncher = MoveShortcutsBox.IsChecked == true;
        _settings.RowSpacing = CompactRadio.IsChecked == true ? RowSpacing.Compact
            : RelaxedRadio.IsChecked == true ? RowSpacing.Relaxed
            : RowSpacing.Normal;
        _store.Save(_settings);

        try
        {
            if (StartupBox.IsChecked == true && !StartupRegistration.IsEnabled) StartupRegistration.Enable();
            else if (StartupBox.IsChecked != true && StartupRegistration.IsEnabled) StartupRegistration.Disable();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            MessageBox.Show(this, $"スタートアップの設定を変更できませんでした。\n\n{ex.Message}", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
