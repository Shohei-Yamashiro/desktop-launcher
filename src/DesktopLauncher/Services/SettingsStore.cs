using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopLauncher.Services;

public sealed class AppSettings
{
    public double? MiniLeft { get; set; }

    public double? MiniTop { get; set; }

    /// <summary>Copy registered .lnk/.url files into the app folder and move the originals to the Recycle Bin.</summary>
    public bool MoveShortcutsToLauncher { get; set; } = true;

    public RowSpacing RowSpacing { get; set; } = RowSpacing.Normal;
}

public enum RowSpacing
{
    Compact,
    Normal,
    Relaxed,
}

public static class RowSpacingExtensions
{
    /// <summary>Space above and below each row's content, in DIPs.</summary>
    public static double VerticalPadding(this RowSpacing spacing) => spacing switch
    {
        RowSpacing.Compact => 1,
        RowSpacing.Relaxed => 6,
        _ => 3,
    };
}

/// <summary>
/// Window settings next to items.json. Unreadable settings fall back to defaults instead of failing.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        FilePath = Path.Combine(DataDirectory, "settings.json");
    }

    public string DataDirectory { get; }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(tempPath, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing the mini window position is not worth interrupting the user.
        }
    }
}
