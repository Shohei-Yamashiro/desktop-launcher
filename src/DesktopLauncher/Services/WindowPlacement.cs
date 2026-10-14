using System.Windows;
using System.Windows.Media;

namespace DesktopLauncher.Services;

public static class WindowPlacement
{
    /// <summary>
    /// Places a window of <paramref name="size"/> so that it opens from the anchor toward the
    /// center of the work area, then keeps it fully inside the work area.
    /// </summary>
    public static Rect PlaceNear(Rect anchor, Size size, Rect workArea)
    {
        var width = Math.Min(size.Width, workArea.Width);
        var height = Math.Min(size.Height, workArea.Height);
        var centerX = anchor.X + anchor.Width / 2;
        var centerY = anchor.Y + anchor.Height / 2;

        var left = centerX > workArea.X + workArea.Width / 2 ? anchor.Right - width : anchor.Left;
        var top = centerY > workArea.Y + workArea.Height / 2 ? anchor.Bottom - height : anchor.Top;

        left = Math.Clamp(left, workArea.Left, workArea.Right - width);
        top = Math.Clamp(top, workArea.Top, workArea.Bottom - height);
        return new Rect(left, top, width, height);
    }

    /// <summary>Work area (in device-independent units) of the monitor that contains the point.</summary>
    public static Rect WorkAreaAt(Visual visual, Point point)
    {
        var dpi = VisualTreeHelper.GetDpi(visual);
        var screen = System.Windows.Forms.Screen.FromPoint(
            new System.Drawing.Point((int)(point.X * dpi.DpiScaleX), (int)(point.Y * dpi.DpiScaleY)));
        var area = screen.WorkingArea;
        return new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY,
            area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
    }
}
