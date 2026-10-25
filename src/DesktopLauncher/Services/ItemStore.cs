using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopLauncher.Models;

namespace DesktopLauncher.Services;

/// <summary>
/// Saves items as JSON under the app folder so nothing is written outside the extracted folder.
/// A node with "Children" is a group; older flat files (Name/Path only) load as top-level items.
/// </summary>
public sealed class ItemStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ItemStore(string? dataDirectory = null)
    {
        DataDirectory = Path.GetFullPath(dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "data"));
        AppDirectory = Path.GetDirectoryName(DataDirectory) ?? DataDirectory;
        FilePath = Path.Combine(DataDirectory, "items.json");
        LinksDirectory = Path.Combine(DataDirectory, "links");
    }

    public string DataDirectory { get; }

    /// <summary>Paths under this folder are saved relative to it, so the app folder can be moved.</summary>
    public string AppDirectory { get; }

    /// <summary>Where registered shortcuts are copied to.</summary>
    public string LinksDirectory { get; }

    public string FilePath { get; }

    public bool IsInLinksDirectory(string path) =>
        Path.IsPathRooted(path)
        && Path.GetFullPath(path).StartsWith(LinksDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public void Load(LauncherTree tree)
    {
        if (!File.Exists(FilePath)) return;

        List<NodeDto> nodes;
        try
        {
            nodes = JsonSerializer.Deserialize<List<NodeDto>>(File.ReadAllText(FilePath), JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            var brokenPath = Path.Combine(DataDirectory, $"items.broken-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Move(FilePath, brokenPath);
            throw new InvalidDataException($"保存データを読み込めませんでした。元のファイルは {brokenPath} に退避しました。");
        }

        foreach (var dto in nodes) Add(tree, dto, null);
    }

    public void Save(LauncherTree tree)
    {
        Directory.CreateDirectory(DataDirectory);
        var json = JsonSerializer.Serialize(tree.Roots.Select(ToDto).ToList(), JsonOptions);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }

    private void Add(LauncherTree tree, NodeDto dto, LauncherItem? parent)
    {
        if (dto.Children is not null)
        {
            var group = LauncherItem.CreateGroup(dto.Name ?? "グループ");
            group.IsExpanded = dto.Expanded ?? false;
            if (dto.Tags is not null) group.SetTags(dto.Tags);
            tree.Insert(group, parent);
            foreach (var child in dto.Children) Add(tree, child, group);
        }
        else if (!string.IsNullOrWhiteSpace(dto.Path))
        {
            var item = LauncherItem.CreateItem(dto.Name ?? dto.Path, ToAbsolute(dto.Path));
            if (dto.Tags is not null) item.SetTags(dto.Tags);
            item.IsFavorite = dto.Favorite == true;
            item.RestoreLaunches(dto.LaunchCount ?? 0, dto.LastLaunched);
            tree.Insert(item, parent);
        }
    }

    private NodeDto ToDto(LauncherItem node)
    {
        var tags = node.Tags.Count > 0 ? node.Tags.ToList() : null;
        return node.IsGroup
            ? new NodeDto { Name = node.Name, Expanded = node.IsExpanded, Tags = tags, Children = node.Children.Select(ToDto).ToList() }
            : new NodeDto
            {
                Name = node.Name,
                Path = ToStored(node.Path),
                Tags = tags,
                Favorite = node.IsFavorite ? true : null,
                LaunchCount = node.LaunchCount > 0 ? node.LaunchCount : null,
                LastLaunched = node.LaunchCount > 0 ? node.LastLaunched : null,
            };
    }

    private string ToStored(string path)
    {
        if (!Path.IsPathRooted(path)) return path;
        var full = Path.GetFullPath(path);
        return full.StartsWith(AppDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(AppDirectory, full)
            : path;
    }

    private string ToAbsolute(string stored)
    {
        if (Path.IsPathRooted(stored) || Uri.TryCreate(stored, UriKind.Absolute, out _)) return stored;
        return Path.GetFullPath(Path.Combine(AppDirectory, stored));
    }

    private sealed class NodeDto
    {
        public string? Name { get; set; }

        public string? Path { get; set; }

        public bool? Expanded { get; set; }

        public List<NodeDto>? Children { get; set; }

        public List<string>? Tags { get; set; }

        public bool? Favorite { get; set; }

        public int? LaunchCount { get; set; }

        public DateTime? LastLaunched { get; set; }
    }
}
