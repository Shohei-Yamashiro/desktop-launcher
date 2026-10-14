using System.Windows.Media;

namespace DesktopLauncher.Services;

/// <summary>
/// Gives each tag a muted color that sits well on the gray theme. A new tag takes the least used color,
/// and the choice is kept in the settings so a tag keeps its color when other tags come and go.
/// </summary>
public sealed class TagColors
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(0x7F, 0xA7, 0xD9), // blue
        Color.FromRgb(0x8C, 0xC0, 0xA0), // green
        Color.FromRgb(0xD9, 0xB3, 0x6C), // amber
        Color.FromRgb(0xD9, 0x8C, 0x8C), // red
        Color.FromRgb(0xB0, 0x9A, 0xD9), // purple
        Color.FromRgb(0x6F, 0xC1, 0xC7), // teal
        Color.FromRgb(0xD9, 0x9C, 0xC4), // pink
        Color.FromRgb(0xA9, 0xB3, 0x6C), // olive
    ];

    private static readonly Brush[] Brushes = Palette.Select(c =>
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();

    private readonly Dictionary<string, int> _assigned;

    public TagColors(IDictionary<string, int>? assigned = null)
    {
        _assigned = new(StringComparer.CurrentCultureIgnoreCase);
        foreach (var (tag, index) in assigned ?? new Dictionary<string, int>())
        {
            if (index >= 0 && index < Palette.Length) _assigned[tag] = index;
        }
    }

    public static int Count => Palette.Length;

    /// <summary>Set when an assignment was added or removed since the last save.</summary>
    public bool Changed { get; set; }

    public IReadOnlyDictionary<string, int> Assigned => _assigned;

    public int IndexFor(string tag)
    {
        if (_assigned.TryGetValue(tag, out var index)) return index;

        var usage = new int[Palette.Length];
        foreach (var used in _assigned.Values) usage[used]++;
        index = Array.IndexOf(usage, usage.Min());
        _assigned[tag] = index;
        Changed = true;
        return index;
    }

    public Brush BrushFor(string tag) => Brushes[IndexFor(tag)];

    /// <summary>Forgets tags that no item uses any more, freeing their colors.</summary>
    public void KeepOnly(IEnumerable<string> tagsInUse)
    {
        var keep = new HashSet<string>(tagsInUse, StringComparer.CurrentCultureIgnoreCase);
        foreach (var tag in _assigned.Keys.Where(t => !keep.Contains(t)).ToList())
        {
            _assigned.Remove(tag);
            Changed = true;
        }
    }
}
