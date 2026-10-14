using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using DesktopLauncher.Models;

namespace DesktopLauncher.Services;

/// <summary>
/// Saves items as JSON under the app folder so nothing is written outside the extracted folder.
/// </summary>
public sealed class ItemStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ItemStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data");
        FilePath = Path.Combine(DataDirectory, "items.json");
    }

    public string DataDirectory { get; }

    public string FilePath { get; }

    public List<LauncherItem> Load()
    {
        if (!File.Exists(FilePath)) return [];

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<LauncherItem>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            var brokenPath = Path.Combine(DataDirectory, $"items.broken-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(FilePath, brokenPath);
            throw new InvalidDataException($"保存データを読み込めませんでした。元のファイルは {brokenPath} に退避しました。");
        }
    }

    public void Save(IEnumerable<LauncherItem> items)
    {
        Directory.CreateDirectory(DataDirectory);
        var json = JsonSerializer.Serialize(items, JsonOptions);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
