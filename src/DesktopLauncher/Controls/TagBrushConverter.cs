using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopLauncher.Services;

namespace DesktopLauncher.Controls;

/// <summary>
/// Tag name to its color; a numeric ConverterParameter gives the opacity (e.g. 0.2 for a tinted fill).
/// <see cref="Colors"/> is set by the window that owns the palette.
/// </summary>
public sealed class TagBrushConverter : IValueConverter
{
    public TagColors? Colors { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var brush = value is string tag && Colors is not null ? Colors.BrushFor(tag) : Brushes.Gray;
        if (parameter is not string text || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity)
            || brush is not SolidColorBrush solid)
        {
            return brush;
        }

        var color = solid.Color;
        color.A = (byte)Math.Round(255 * Math.Clamp(opacity, 0, 1));
        var tinted = new SolidColorBrush(color);
        tinted.Freeze();
        return tinted;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
