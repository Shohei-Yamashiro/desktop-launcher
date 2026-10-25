using System.Globalization;

namespace DesktopLauncher.Models;

/// <summary>The letter and color of the round badge shown as a URL item's icon.</summary>
public static class LetterBadge
{
    /// <summary>"kintai.example.com" → "K"; a leading "www." is skipped.</summary>
    public static string Letter(string host)
    {
        var name = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        if (name.Length == 0) return "?";
        var first = StringInfo.GetNextTextElement(name);
        return first.ToUpperInvariant();
    }

    /// <summary>
    /// A palette index that stays the same for a host across runs (string.GetHashCode changes every run).
    /// </summary>
    public static int ColorIndex(string host, int paletteSize)
    {
        var hash = 2166136261u;
        foreach (var c in host.ToLowerInvariant())
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return (int)(hash % (uint)paletteSize);
    }
}
