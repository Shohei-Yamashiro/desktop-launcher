using System.Windows;
using DesktopLauncher.Services;

namespace DesktopLauncher;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly AppSettings _settings;

    public SettingsWindow(SettingsStore store, AppSettings settings)
    {
        InitializeComponent();
        _store = store;
        _settings = settings;
        MoveShortcutsBox.IsChecked = settings.MoveShortcutsToLauncher;
        StartupBox.IsChecked = StartupRegistration.IsEnabled;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _settings.MoveShortcutsToLauncher = MoveShortcutsBox.IsChecked == true;
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
