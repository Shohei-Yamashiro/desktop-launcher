using System.Windows;
using System.Windows.Input;
using DesktopLauncher.Services;

namespace DesktopLauncher;

public partial class App : Application
{
    private const string InstanceName = "DesktopLauncher-6b1f0d52";

    private SingleInstance? _instance;
    private TrayIcon? _tray;
    private HotKey? _hotKey;
    private DesktopLauncher.MainWindow? _window;
    private MiniWindow? _mini;
    private bool _trayHintShown;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = SingleInstance.TryAcquire(InstanceName);
        if (_instance is null)
        {
            SingleInstance.SignalExisting(InstanceName);
            Shutdown();
            return;
        }

        _window = new DesktopLauncher.MainWindow();
        MainWindow = _window;
        _window.ClosedToTray += OnClosedToTray;

        _hotKey = new HotKey(_window, ModifierKeys.Control | ModifierKeys.Alt, Key.Space);
        _hotKey.Pressed += _window.ToggleLauncher;

        _tray = new TrayIcon(_hotKey.DisplayText, _window.ShowLauncher, ExitApp);
        _instance.ShowRequested += () => Dispatcher.BeginInvoke(_window.ShowLauncher);

        _mini = new MiniWindow(new SettingsStore(), _hotKey.DisplayText);
        _mini.OpenRequested += _window.ShowLauncher;
        _mini.ExitRequested += ExitApp;
        _window.IsVisibleChanged += (_, _) =>
        {
            if (_window.IsVisible) _mini.Hide();
            else if (!_exiting) _mini.Show();
        };

        if (!_hotKey.Register())
        {
            _tray.ShowBalloon($"{_hotKey.DisplayText} は他のアプリで使われているため登録できませんでした。トレイのアイコンから呼び出してください。",
                System.Windows.Forms.ToolTipIcon.Warning);
        }

        if (e.Args.Contains(StartupRegistration.BackgroundArg)) _mini.Show();
        else _window.ShowLauncher();
    }

    private void OnClosedToTray()
    {
        if (_trayHintShown || _tray is null || _hotKey is null) return;
        _trayHintShown = true;
        _tray.ShowBalloon($"小さくして画面に残しました。クリックか {_hotKey.DisplayText} で元に戻ります。終了するときは、小さいアイコンを右クリックして「終了」を選んでください。");
    }

    private void ExitApp()
    {
        _exiting = true;
        _window?.PrepareExit();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKey?.Dispose();
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
