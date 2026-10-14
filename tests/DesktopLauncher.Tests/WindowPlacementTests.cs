using System.Windows;
using DesktopLauncher.Services;

namespace DesktopLauncher.Tests;

public class WindowPlacementTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);
    private static readonly Size WindowSize = new(480, 600);

    [Fact]
    public void BottomRightIcon_OpensUpAndLeft()
    {
        var anchor = new Rect(1832, 952, 64, 64);

        var placed = WindowPlacement.PlaceNear(anchor, WindowSize, WorkArea);

        Assert.Equal(anchor.Right - 480, placed.Left);
        Assert.Equal(anchor.Bottom - 600, placed.Top);
    }

    [Fact]
    public void TopLeftIcon_OpensDownAndRight()
    {
        var anchor = new Rect(40, 30, 64, 64);

        var placed = WindowPlacement.PlaceNear(anchor, WindowSize, WorkArea);

        Assert.Equal(40, placed.Left);
        Assert.Equal(30, placed.Top);
    }

    [Fact]
    public void IconHalfOffScreen_WindowStaysInsideWorkArea()
    {
        var anchor = new Rect(1900, 1020, 64, 64);

        var placed = WindowPlacement.PlaceNear(anchor, WindowSize, WorkArea);

        Assert.Equal(1920 - 480, placed.Left);
        Assert.Equal(1040 - 600, placed.Top);
    }

    [Fact]
    public void WindowLargerThanWorkArea_IsShrunkToFit()
    {
        var small = new Rect(0, 0, 400, 500);

        var placed = WindowPlacement.PlaceNear(new Rect(10, 10, 64, 64), WindowSize, small);

        Assert.Equal(small, placed);
    }

    [Fact]
    public void SecondMonitorWithNegativeCoordinates_IsRespected()
    {
        var left = new Rect(-1280, 0, 1280, 984);
        var anchor = new Rect(-1300, 900, 64, 64);

        var placed = WindowPlacement.PlaceNear(anchor, WindowSize, left);

        Assert.Equal(-1280, placed.Left);
        Assert.Equal(anchor.Bottom - 600, placed.Top);
    }
}
