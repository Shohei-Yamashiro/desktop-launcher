using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DesktopLauncher.Services;

namespace DesktopLauncher;

/// <summary>
/// Small always-on-top icon shown while the launcher is collapsed. Click to expand, drag to move.
/// </summary>
public partial class MiniWindow : Window
{
    private const double EdgeMargin = 24;
    private const double ClickTolerance = 4;

    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;

    public MiniWindow(SettingsStore settingsStore, AppSettings settings, string hotKeyText)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
        _settings = settings;
        IconImage.Source = LoadLargestIconFrame();
        ToolTip = $"Desktop Launcher（{hotKeyText}）\nクリックで開く・ドラッグで移動";
        PlaceInitially();
    }

    public Rect Bounds => new(Left, Top, Width, Height);

    public event Action? OpenRequested;

    public event Action? ExitRequested;

    private void PlaceInitially()
    {
        var area = SystemParameters.WorkArea;
        var left = _settings.MiniLeft ?? area.Right - Width - EdgeMargin;
        var top = _settings.MiniTop ?? area.Bottom - Height - EdgeMargin;

        var onScreen = left >= SystemParameters.VirtualScreenLeft
            && top >= SystemParameters.VirtualScreenTop
            && left + Width <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && top + Height <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (!onScreen)
        {
            left = area.Right - Width - EdgeMargin;
            top = area.Bottom - Height - EdgeMargin;
        }

        Left = left;
        Top = top;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var startLeft = Left;
        var startTop = Top;
        DragMove();

        if (Math.Abs(Left - startLeft) < ClickTolerance && Math.Abs(Top - startTop) < ClickTolerance)
        {
            Left = startLeft;
            Top = startTop;
            OpenRequested?.Invoke();
            return;
        }

        _settings.MiniLeft = Left;
        _settings.MiniTop = Top;
        _settingsStore.Save(_settings);
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenRequested?.Invoke();

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

    private static BitmapSource? LoadLargestIconFrame()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (resource is null) return null;
        using var stream = resource.Stream;
        var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        return decoder.Frames.MaxBy(f => f.PixelWidth);
    }
}
