using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace DesktopLauncher.Models;

public sealed class LauncherItem : INotifyPropertyChanged
{
    private string _name = "";
    private ImageSource? _icon;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Path { get; set; } = "";

    [JsonIgnore]
    public ImageSource? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    [JsonIgnore]
    public bool IsUrl => Uri.TryCreate(Path, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
