using System.IO;
using System.Text.Json;
using DesktopLauncher.Models;

namespace DesktopLauncher.Services;

/// <summary>A browser profile's bookmark file.</summary>
public sealed record BookmarkSource(string Browser, string Profile, string FilePath)
{
    public string DisplayName => $"{Browser}（{Profile}）";
}

/// <summary>
/// Finds the bookmark files of Chromium browsers installed for this user and reads them. The files are only
/// read, never written, and may be read while the browser is running.
/// </summary>
public static class BrowserBookmarks
{
    private static readonly (string Name, string UserData)[] Browsers =
    [
        ("Microsoft Edge", @"Microsoft\Edge\User Data"),
        ("Google Chrome", @"Google\Chrome\User Data"),
        ("Brave", @"BraveSoftware\Brave-Browser\User Data"),
        ("Vivaldi", @"Vivaldi\User Data"),
    ];

    public static IReadOnlyList<BookmarkSource> Find()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var sources = new List<BookmarkSource>();
        foreach (var (browser, userData) in Browsers)
        {
            var root = Path.Combine(local, userData);
            if (!Directory.Exists(root)) continue;

            var names = ProfileNames(Path.Combine(root, "Local State"));
            foreach (var profile in Directory.EnumerateDirectories(root).OrderBy(d => d == Path.Combine(root, "Default") ? 0 : 1).ThenBy(d => d))
            {
                var file = Path.Combine(profile, "Bookmarks");
                if (!File.Exists(file)) continue;
                var folder = Path.GetFileName(profile);
                sources.Add(new BookmarkSource(browser, names.GetValueOrDefault(folder, folder), file));
            }
        }
        return sources;
    }

    public static IReadOnlyList<BookmarkNode> Read(BookmarkSource source) => BookmarkReader.ReadChromium(ReadShared(source.FilePath));

    public static IReadOnlyList<BookmarkNode> ReadHtmlFile(string path) => BookmarkReader.ReadHtml(ReadShared(path));

    /// <summary>The browser may keep the file open; share it rather than fail.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Profile folder → the name shown in the browser ("個人", "仕事"...), from "Local State".</summary>
    private static Dictionary<string, string> ProfileNames(string localState)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(ReadShared(localState));
            if (document.RootElement.TryGetProperty("profile", out var profile)
                && profile.TryGetProperty("info_cache", out var cache) && cache.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in cache.EnumerateObject())
                {
                    if (entry.Value.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } text) names[entry.Name] = text;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Without the names, the profile folders ("Default", "Profile 1") are shown instead.
        }
        return names;
    }
}
