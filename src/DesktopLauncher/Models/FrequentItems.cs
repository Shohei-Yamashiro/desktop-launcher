namespace DesktopLauncher.Models;

/// <summary>
/// Orders items for the よく使う tab: the number of times each was opened, weighted by how recently it was
/// last opened, so that something used a lot long ago gives way to what is used now.
/// </summary>
public static class FrequentItems
{
    public const int MaxCount = 20;

    public static IReadOnlyList<LauncherItem> Rank(IEnumerable<LauncherItem> items, DateTime now, int max = MaxCount) =>
        items
            .Where(n => !n.IsGroup && n.LaunchCount > 0)
            .OrderByDescending(n => Score(n, now))
            .ThenByDescending(n => n.LastLaunched)
            .Take(max)
            .ToList();

    public static double Score(LauncherItem item, DateTime now) => item.LaunchCount * RecencyWeight(item.LastLaunched, now);

    /// <summary>1 within a few days, then less for each longer gap since the last launch.</summary>
    public static double RecencyWeight(DateTime? last, DateTime now)
    {
        if (last is not { } when) return 0.1;
        var days = (now - when).TotalDays;
        return days switch
        {
            <= 4 => 1.0,
            <= 14 => 0.7,
            <= 31 => 0.5,
            <= 90 => 0.3,
            _ => 0.1,
        };
    }
}
