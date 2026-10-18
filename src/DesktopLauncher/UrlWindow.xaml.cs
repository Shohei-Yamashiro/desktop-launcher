using System.Windows;
using System.Windows.Controls;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace DesktopLauncher;

public partial class UrlWindow : Window
{
    /// <summary>The name last filled in from the URL; while the name still equals it, it follows the URL.</summary>
    private string _suggestedName = "";

    public UrlWindow()
    {
        InitializeComponent();
        DarkFrame.Apply(this);
        UrlBox.Text = ClipboardContent.Url() ?? "";
        Loaded += (_, _) =>
        {
            var box = UrlBox.Text.Length > 0 ? NameBox : UrlBox;
            box.Focus();
            box.SelectAll();
        };
    }

    public string Url { get; private set; } = "";

    public string ItemName { get; private set; } = "";

    private void UrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (NameBox.Text != _suggestedName) return;
        _suggestedName = WebUrl.TryNormalize(UrlBox.Text, out var url) ? WebUrl.DefaultName(url) : "";
        NameBox.Text = _suggestedName;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!WebUrl.TryNormalize(UrlBox.Text, out var url))
        {
            MessageBox.Show(this, "http:// または https:// で始まる URL を入力してください。", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            UrlBox.Focus();
            UrlBox.SelectAll();
            return;
        }

        Url = url;
        ItemName = NameBox.Text.Trim() is { Length: > 0 } name ? name : WebUrl.DefaultName(url);
        DialogResult = true;
    }
}
