namespace DesktopLauncher.Models;

/// <summary>
/// Search box text split on spaces. "#name" requires a tag; any other word must appear in the
/// item's name or in one of its tags. Tags include those inherited from enclosing groups, and
/// every part must match.
/// </summary>
public sealed class SearchQuery
{
    public const char TagPrefix = '#';

    /// <summary>"#", or "＃" as typed with the Japanese IME on.</summary>
    public static bool IsTagWord(string word) => word.Length > 0 && word[0] is TagPrefix or '＃';

    private readonly (string Name, bool Exact)[] _tags;

    private SearchQuery(string[] words, (string Name, bool Exact)[] tags)
    {
        Words = words;
        _tags = tags;
    }

    public IReadOnlyList<string> Words { get; }

    /// <summary>Tag names written with "#".</summary>
    public IEnumerable<string> Tags => _tags.Select(t => t.Name);

    public bool IsEmpty => Words.Count == 0 && _tags.Length == 0;

    /// <summary>
    /// Parses the text. A "#name" that equals a known tag matches only that tag;
    /// otherwise it matches tags starting with it, so results appear while typing.
    /// </summary>
    public static SearchQuery Parse(string text, IEnumerable<string> knownTags)
    {
        var known = knownTags.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var words = new List<string>();
        var tags = new List<(string, bool)>();
        foreach (var part in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!IsTagWord(part)) words.Add(part);
            else if (part.Length > 1) tags.Add((part[1..], known.Contains(part[1..])));
        }
        return new SearchQuery([.. words], [.. tags]);
    }

    public bool Matches(LauncherItem item)
    {
        var tags = item.EffectiveTags().ToArray();
        foreach (var (name, exact) in _tags)
        {
            var found = exact
                ? tags.Contains(name, StringComparer.CurrentCultureIgnoreCase)
                : tags.Any(t => t.StartsWith(name, StringComparison.CurrentCultureIgnoreCase));
            if (!found) return false;
        }
        foreach (var word in Words)
        {
            if (!item.Name.Contains(word, StringComparison.CurrentCultureIgnoreCase)
                && !tags.Any(t => t.Contains(word, StringComparison.CurrentCultureIgnoreCase)))
            {
                return false;
            }
        }
        return true;
    }
}
