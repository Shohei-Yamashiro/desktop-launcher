using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Screenshots;

/// <summary>
/// Captures this process's own windows with PrintWindow, cropped to the visible frame. Nothing else on
/// the screen is read.
/// </summary>
public static class WindowCapture
{
    private const uint PW_RENDERFULLCONTENT = 2;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    /// <summary>The window as shown on screen, in device pixels, and its top-left corner on the screen.</summary>
    public static (BitmapSource Image, Point Origin) Capture(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        GetWindowRect(hwnd, out var outer);
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, Marshal.SizeOf<RECT>()) != 0) frame = outer;

        var width = outer.Right - outer.Left;
        var height = outer.Bottom - outer.Top;
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var old = SelectObject(memory, bitmap);
        try
        {
            PrintWindow(hwnd, memory, PW_RENDERFULLCONTENT);
            var full = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var opaque = new FormatConvertedBitmap(full, PixelFormats.Bgr32, null, 0);
            var crop = new Int32Rect(frame.Left - outer.Left, frame.Top - outer.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
            var image = new CroppedBitmap(opaque, crop);
            image.Freeze();
            return (image, new Point(frame.Left, frame.Top));
        }
        finally
        {
            SelectObject(memory, old);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>Renders an element (such as a popup's content) at the screen's scale.</summary>
    public static (BitmapSource Image, Point Origin) Render(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(element);
        bitmap.Freeze();
        return (bitmap, element.PointToScreen(new Point(0, 0)));
    }

    /// <summary>Draws layers over the base capture at their screen positions and saves a PNG.</summary>
    public static void Save(string path, (BitmapSource Image, Point Origin) baseLayer, params (BitmapSource Image, Point Origin)[] layers)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(baseLayer.Image, new Rect(0, 0, baseLayer.Image.PixelWidth, baseLayer.Image.PixelHeight));
            foreach (var (image, origin) in layers)
            {
                dc.DrawImage(image, new Rect(origin.X - baseLayer.Origin.X, origin.Y - baseLayer.Origin.Y, image.PixelWidth, image.PixelHeight));
            }
        }

        var target = new RenderTargetBitmap(baseLayer.Image.PixelWidth, baseLayer.Image.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
