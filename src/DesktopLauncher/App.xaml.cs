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
    private bool _trayHintShown;

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

        if (!_hotKey.Register())
        {
            _tray.ShowBalloon($"{_hotKey.DisplayText} は他のアプリで使われているため登録できませんでした。トレイのアイコンから呼び出してください。",
                System.Windows.Forms.ToolTipIcon.Warning);
        }

        if (!e.Args.Contains(StartupRegistration.BackgroundArg)) _window.ShowLauncher();
    }

    private void OnClosedToTray()
    {
        if (_trayHintShown || _tray is null || _hotKey is null) return;
        _trayHintShown = true;
        _tray.ShowBalloon($"トレイで動作中です。{_hotKey.DisplayText} で呼び出せます。終了はトレイのアイコンを右クリックしてください。");
    }

    private void ExitApp()
    {
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
