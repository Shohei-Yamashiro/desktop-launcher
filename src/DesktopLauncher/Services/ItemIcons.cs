using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopLauncher.Models;

namespace DesktopLauncher.Services;

/// <summary>Icons drawn without the shell: letter badges for URLs and images the user picked.</summary>
public static class ItemIcons
{
    private static readonly Dictionary<string, ImageSource> Badges = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Typeface BadgeFont = new(new FontFamily("Segoe UI Variable Text, Segoe UI, Yu Gothic UI"),
        FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush BadgeText = Frozen(new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)));

    /// <summary>A round badge with the site's first letter, in a color that is always the same for the site.</summary>
    public static ImageSource LetterBadgeFor(string url)
    {
        var host = WebUrl.DefaultName(url);
        if (Badges.TryGetValue(host, out var cached)) return cached;

        var color = TagColors.ColorAt(LetterBadge.ColorIndex(host, TagColors.Count));
        var text = new FormattedText(LetterBadge.Letter(host), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            BadgeFont, 11, BadgeText, 1.0);
        var origin = new Point(10 - text.WidthIncludingTrailingWhitespace / 2, 10 - text.Height / 2);

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Frozen(new SolidColorBrush(color)), null, new EllipseGeometry(new Point(10, 10), 9, 9)));
        drawing.Children.Add(new GeometryDrawing(BadgeText, null, text.BuildGeometry(origin)));
        var image = new DrawingImage(drawing);
        image.Freeze();
        Badges[host] = image;
        return image;
    }

    /// <summary>
    /// The largest frame of an image file (PNG, ICO, JPG, BMP, GIF...), read fully so the file is not kept open.
    /// Null when it cannot be read as an image.
    /// </summary>
    public static BitmapSource? LoadImage(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.MaxBy(f => f.PixelWidth * f.PixelHeight);
            if (frame is null) return null;
            frame.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
                                       or ArgumentException or InvalidOperationException or FileFormatException)
        {
            return null;
        }
    }

    private static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
