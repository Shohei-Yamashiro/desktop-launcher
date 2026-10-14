using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DesktopLauncher.Controls;

/// <summary>
/// Places children in a row. Children that do not fit are clipped away and "…" is drawn after the last one shown.
/// </summary>
public sealed class OverflowPanel : Panel
{
    private const string Ellipsis = "…";

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(OverflowPanel),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(OverflowPanel), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    private double? _ellipsisX;

    public OverflowPanel()
    {
        ClipToBounds = true;
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (width > 0) width += Spacing;
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(Math.Min(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var total = 0.0;
        foreach (UIElement child in InternalChildren) total += child.DesiredSize.Width;
        total += Math.Max(0, InternalChildren.Count - 1) * Spacing;
        var allFit = total <= finalSize.Width + 0.5;
        var limit = allFit ? finalSize.Width : finalSize.Width - EllipsisText().Width - Spacing;

        double x = 0;
        var overflowing = false;
        foreach (UIElement child in InternalChildren)
        {
            var w = child.DesiredSize.Width;
            if (!overflowing && x + w <= limit + 0.5)
            {
                child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, w, child.DesiredSize.Height));
                x += w + Spacing;
            }
            else
            {
                overflowing = true;
                child.Arrange(new Rect(finalSize.Width + 1000, 0, w, child.DesiredSize.Height));
            }
        }

        var ellipsisX = overflowing ? x : (double?)null;
        if (ellipsisX != _ellipsisX)
        {
            _ellipsisX = ellipsisX;
            InvalidateVisual();
        }
        return finalSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_ellipsisX is not { } x) return;
        var text = EllipsisText();
        dc.DrawText(text, new Point(x, (RenderSize.Height - text.Height) / 2));
    }

    private FormattedText EllipsisText() => new(
        Ellipsis, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(TextElement.GetFontFamily(this), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        TextElement.GetFontSize(this), Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
}
