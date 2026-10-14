using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DesktopLauncher.Services;

namespace DesktopLauncher.Controls;

/// <summary>Tag name to its color. <see cref="Colors"/> is set by the window that owns the palette.</summary>
public sealed class TagBrushConverter : IValueConverter
{
    public TagColors? Colors { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string tag && Colors is not null ? Colors.BrushFor(tag) : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
