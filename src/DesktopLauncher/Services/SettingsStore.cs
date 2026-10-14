using System.IO;
using System.Text.Json;

namespace DesktopLauncher.Services;

public sealed class AppSettings
{
    public double? MiniLeft { get; set; }

    public double? MiniTop { get; set; }
}

/// <summary>
/// Window settings next to items.json. Unreadable settings fall back to defaults instead of failing.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
