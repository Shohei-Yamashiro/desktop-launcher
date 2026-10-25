using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DesktopLauncher;
using DesktopLauncher.Models;
using DesktopLauncher.Services;

namespace Screenshots;

/// <summary>
/// Usage: Screenshots.exe [output folder]. Writes main.png, search.png, favorites.png, edit.png, settings.png and logo.png
/// (default: docs\portfolio\images).
/// The windows open on the screen for a few seconds while they are captured.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("docs", "portfolio", "images"));
        var dataDirectory = SampleData.Write(AppContext.BaseDirectory);

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Hashelf;component/Themes/Gray.xaml"),
        });

        var exitCode = 0;
        app.Startup += async (_, _) =>
        {
            try
            {
                await TakeAll(dataDirectory, output);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "error.txt"), ex.ToString());
                exitCode = 1;
            }
            app.Shutdown();
        };
        app.Run();
        return exitCode;
    }

    private static async Task TakeAll(string dataDirectory, string output)
    {
        var settingsStore = new SettingsStore(dataDirectory);
        var main = new MainWindow(settingsStore, settingsStore.Load())
        {
            Left = 80,
            Top = 60,
            Width = 480,
            Height = 640,
            ShowActivated = false,
        };
        ((ToggleButton)main.FindName("PinButton")).IsChecked = true;
        main.Show();
        await Settle();

        // A row whose tags are partly inherited from its groups, selected.
        var tree = (TreeView)main.FindName("Tree");
        var portal = Flatten(tree.ItemsSource.Cast<LauncherItem>()).First(n => n.Name == "社内ポータル");
        portal.IsSelected = true;
        await Settle();
        WindowCapture.Save(Path.Combine(output, "main.png"), WindowCapture.Capture(main));
        portal.IsSelected = false;

        // Typing "#毎" lists the matching items and suggests the existing tag.
        var search = (TextBox)main.FindName("SearchBox");
        search.Text = "#毎";
        search.CaretIndex = search.Text.Length;
        // The suggestions open only while the box has keyboard focus, which an unactivated window never gets.
        var suggest = (Popup)main.FindName("TagSuggest");
        suggest.IsOpen = true;
        await Settle();
        var layers = suggest.IsOpen && suggest.Child is FrameworkElement child ? [WindowCapture.Render(child)] : Array.Empty<(System.Windows.Media.Imaging.BitmapSource, Point)>();
        WindowCapture.Save(Path.Combine(output, "search.png"), WindowCapture.Capture(main), layers);
        search.Clear();
        suggest.IsOpen = false;

        // The お気に入り tab.
        ((RadioButton)main.FindName("FavoritesTab")).IsChecked = true;
        await Settle();
        WindowCapture.Save(Path.Combine(output, "favorites.png"), WindowCapture.Capture(main));
        ((RadioButton)main.FindName("AllTab")).IsChecked = true;

        // The edit dialog for an item with its own and inherited tags.
        var loaded = new LauncherTree();
        new ItemStore(dataDirectory).Load(loaded);
        var node = loaded.All().First(n => n.Name == "社内ポータル");
        var colors = new TagColors(settingsStore.Load().TagColors);
        var edit = new EditWindow("", null, loaded.AllTags(), [node], colors.BrushFor)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 620,
            Top = 60,
            ShowActivated = false,
        };
        edit.Show();
        await Settle();
        ((TextBox)edit.FindName("NameBox")).Select(0, 0);
        await Settle();
        WindowCapture.Save(Path.Combine(output, "edit.png"), WindowCapture.Capture(edit));
        edit.Close();

        var settingsWindow = new SettingsWindow(settingsStore, settingsStore.Load(), null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 620,
            Top = 60,
            ShowActivated = false,
        };
        settingsWindow.Show();
        await Settle();
        WindowCapture.Save(Path.Combine(output, "settings.png"), WindowCapture.Capture(settingsWindow));
        settingsWindow.Close();

        main.PrepareExit();
        main.Close();

        SaveLogo(Path.Combine(output, "logo.png"));
    }

    /// <summary>The largest frame of the app icon, for the guide's cover.</summary>
    private static void SaveLogo(string path)
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Hashelf;component/Assets/app.ico"));
        using var stream = resource.Stream;
        var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(stream,
            System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(decoder.Frames.MaxBy(f => f.PixelWidth)!);
        using var file = File.Create(path);
        encoder.Save(file);
    }

    /// <summary>Lets layout, icons and rendering finish before a capture.</summary>
    private static async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Task.Delay(600);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<LauncherItem> Flatten(IEnumerable<LauncherItem> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));
}
