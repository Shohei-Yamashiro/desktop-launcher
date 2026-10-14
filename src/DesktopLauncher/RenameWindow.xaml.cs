using System.Windows;

namespace DesktopLauncher;

public partial class RenameWindow : Window
{
    public RenameWindow(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string NewName => NameBox.Text.Trim();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (NewName.Length == 0)
        {
            MessageBox.Show(this, "名前を入力してください。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
