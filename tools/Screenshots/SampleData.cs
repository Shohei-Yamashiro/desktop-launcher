using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Screenshots;

/// <summary>
/// Fictional items for the screenshots: a made-up company's folders (empty, created next to the tool),
/// apps that come with Windows, and example.com URLs.
/// </summary>
public static class SampleData
{
    public static string Write(string baseDirectory)
    {
        var dataDirectory = Path.Combine(baseDirectory, "data");
        var sampleDirectory = Path.Combine(baseDirectory, "サンプル");
        if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, recursive: true);
        if (Directory.Exists(sampleDirectory)) Directory.Delete(sampleDirectory, recursive: true);
        Directory.CreateDirectory(dataDirectory);

        string Folder(string name)
        {
            Directory.CreateDirectory(Path.Combine(sampleDirectory, name));
            return Path.Combine("サンプル", name);
        }

        string File(string name)
        {
            Directory.CreateDirectory(sampleDirectory);
            System.IO.File.WriteAllText(Path.Combine(sampleDirectory, name), "");
            return Path.Combine("サンプル", name);
        }

        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        var nodes = new[]
        {
            Group("仕事", ["仕事"],
                Item("見積書", Folder("見積書"), "経理"),
                Item("議事録", Folder("議事録"), "会議"),
                Group("営業資料", ["営業"],
                    Item("提案書メモ", File("提案書メモ.txt")),
                    Item("顧客リスト", File("顧客リスト.csv"), "毎日"),
                    Favorite(Item("社内ポータル", "https://portal.example.com/", "毎日")))),
            Group("ツール", ["ツール"],
                Favorite(Item("メモ帳", Path.Combine(system, "notepad.exe"), "毎日")),
                Item("電卓", Path.Combine(system, "calc.exe")),
                Item("エクスプローラー", Path.Combine(windows, "explorer.exe"))),
            Group("リンク", ["Web"],
                Favorite(Item("勤怠システム", "https://kintai.example.com/", "毎日")),
                Item("経費精算", "https://keihi.example.com/", "経理")),
            Item("週報テンプレート", File("週報テンプレート.txt"), "会議"),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        System.IO.File.WriteAllText(Path.Combine(dataDirectory, "items.json"), JsonSerializer.Serialize(nodes, options));
        return dataDirectory;
    }

    private static Node Group(string name, string[] tags, params Node[] children) =>
        new() { Name = name, Expanded = true, Tags = [.. tags], Children = [.. children] };

    private static Node Item(string name, string path, params string[] tags) =>
        new() { Name = name, Path = path, Tags = tags.Length > 0 ? [.. tags] : null };

    private static Node Favorite(Node item)
    {
        item.Favorite = true;
        return item;
    }

    /// <summary>Same shape as the app's items.json.</summary>
    private sealed class Node
    {
        public string? Name { get; set; }

        public string? Path { get; set; }

        public bool? Expanded { get; set; }

        public List<Node>? Children { get; set; }

        public List<string>? Tags { get; set; }

        public bool? Favorite { get; set; }
    }
}
