using System.Windows;
using System.Windows.Controls;

namespace DesktopLauncher.Controls;

/// <summary>
/// Lays out a name (first child) followed by its tags (second child) on one line.
/// When both do not fit, the tags give way first, but the name keeps at most
/// <see cref="NameShare"/> of the width so that some tags stay visible.
/// </summary>
public sealed class NameTagsPanel : Panel
{
    private const double Gap = 8;
    private const double NameShare = 0.55;

    private double _nameWidth;
    private double _tagsWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count < 2) return default;
        var name = InternalChildren[0];
        var tags = InternalChildren[1];

        var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
        name.Measure(unbounded);
        tags.Measure(unbounded);
        var nameDesired = name.DesiredSize.Width;
        var tagsDesired = tags.DesiredSize.Width;
        var gap = tagsDesired > 0 ? Gap : 0;
        var width = availableSize.Width;

        if (double.IsInfinity(width) || nameDesired + gap + tagsDesired <= width)
        {
            _nameWidth = nameDesired;
            _tagsWidth = tagsDesired;
        }
        else
        {
            _nameWidth = Math.Min(nameDesired, Math.Max(width - gap - tagsDesired, width * NameShare));
            _tagsWidth = Math.Max(0, width - _nameWidth - gap);
        }

        name.Measure(new Size(_nameWidth, availableSize.Height));
        tags.Measure(new Size(_tagsWidth, availableSize.Height));
        var height = Math.Max(name.DesiredSize.Height, tags.DesiredSize.Height);
        return new Size(_nameWidth + (_tagsWidth > 0 ? gap + _tagsWidth : 0), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count < 2) return finalSize;
        InternalChildren[0].Arrange(new Rect(0, 0, _nameWidth, finalSize.Height));
        InternalChildren[1].Arrange(new Rect(_nameWidth + Gap, 0, _tagsWidth, finalSize.Height));
        return finalSize;
    }
}
