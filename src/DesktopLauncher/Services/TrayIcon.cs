using System.Drawing;
using System.Windows.Forms;

namespace DesktopLauncher.Services;

public sealed class TrayIcon : IDisposable
{
    private const string AppName = "#Hashelf";

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _showItem;

    public TrayIcon(string hotKeyText, Action show, Action exit)
    {
        _showItem = new ToolStripMenuItem("表示", null, (_, _) => show())
        {
            ShortcutKeyDisplayString = hotKeyText,
            Font = new Font(SystemFonts.MenuFont ?? Control.DefaultFont, System.Drawing.FontStyle.Bold),
        };
        var startupItem = new ToolStripMenuItem("Windows 起動時に開始", null, (_, _) => ToggleStartup());
        var exitItem = new ToolStripMenuItem("終了", null, (_, _) => exit());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_showItem, startupItem, new ToolStripSeparator(), exitItem]);
        menu.Opening += (_, _) => startupItem.Checked = StartupRegistration.IsEnabled;

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = $"{AppName}（{hotKeyText}）",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) show();
        };
    }

    public void SetHotKeyText(string hotKeyText)
    {
        _showItem.ShortcutKeyDisplayString = hotKeyText;
        _icon.Text = $"{AppName}（{hotKeyText}）";
    }

    public void ShowBalloon(string text, ToolTipIcon icon = ToolTipIcon.Info) =>
        _icon.ShowBalloonTip(5000, AppName, text, icon);

    private static void ToggleStartup()
    {
        try
        {
            if (StartupRegistration.IsEnabled) StartupRegistration.Disable();
            else StartupRegistration.Enable();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            System.Windows.MessageBox.Show($"スタートアップの設定を変更できませんでした。\n\n{ex.Message}", AppName,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private static Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (resource is null) return SystemIcons.Application;
        using var stream = resource.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
