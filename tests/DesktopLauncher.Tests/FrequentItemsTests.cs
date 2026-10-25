using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class FrequentItemsTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0);

    private static LauncherItem Opened(string name, int count, double daysAgo)
    {
        var item = LauncherItem.CreateItem(name, $@"C:\{name}.exe");
        item.RestoreLaunches(count, Now.AddDays(-daysAgo));
        return item;
    }

    [Fact]
    public void Rank_PutsRecentUseAboveOldHeavyUse()
    {
        var oldFavorite = Opened("昔よく使った", 20, 200);
        var recent = Opened("最近", 5, 1);
        var neverOpened = LauncherItem.CreateItem("未使用", @"C:\x.exe");

        var ranked = FrequentItems.Rank([oldFavorite, neverOpened, recent], Now);

        Assert.Equal(["最近", "昔よく使った"], ranked.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void Rank_SameScoreGoesByLastLaunch()
    {
        var earlier = Opened("先", 2, 3);
        var later = Opened("後", 2, 1);

        Assert.Equal(["後", "先"], FrequentItems.Rank([earlier, later], Now).Select(n => n.Name).ToArray());
    }

    [Fact]
    public void Rank_SkipsGroupsAndStopsAtMax()
    {
        var items = Enumerable.Range(1, 30).Select(i => Opened($"item{i}", i, 1)).ToList();
        var group = LauncherItem.CreateGroup("g");

        var ranked = FrequentItems.Rank([group, .. items], Now);

        Assert.Equal(FrequentItems.MaxCount, ranked.Count);
        Assert.Equal("item30", ranked[0].Name);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(4, 1.0)]
    [InlineData(10, 0.7)]
    [InlineData(30, 0.5)]
    [InlineData(60, 0.3)]
    [InlineData(365, 0.1)]
    public void RecencyWeight_FallsWithTime(double daysAgo, double expected) =>
        Assert.Equal(expected, FrequentItems.RecencyWeight(Now.AddDays(-daysAgo), Now));

    [Fact]
    public void RecordLaunch_CountsAndRemembersTheTime()
    {
        var item = LauncherItem.CreateItem("メモ帳", @"C:\notepad.exe");

        item.RecordLaunch(Now.AddHours(-1));
        item.RecordLaunch(Now);

        Assert.Equal(2, item.LaunchCount);
        Assert.Equal(Now, item.LastLaunched);

        item.ClearLaunches();
        Assert.Equal(0, item.LaunchCount);
        Assert.Null(item.LastLaunched);
    }
}
