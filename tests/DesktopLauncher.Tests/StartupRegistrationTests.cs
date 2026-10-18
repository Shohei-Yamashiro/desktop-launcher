using DesktopLauncher.Services;

namespace DesktopLauncher.Tests;

public class StartupRegistrationTests
{
    [Theory]
    [InlineData("\"C:\\Apps\\Launcher\\DesktopLauncher.exe\" --background", @"C:\Apps\Launcher", true)]
    [InlineData("\"C:\\Apps\\Launcher\\DesktopLauncher.exe\" --background", @"c:\apps\launcher\", true)]
    [InlineData("\"C:\\Program Files\\My Apps\\DesktopLauncher.exe\"", @"C:\Program Files\My Apps", true)]
    [InlineData(@"C:\Apps\Launcher\DesktopLauncher.exe --background", @"C:\Apps\Launcher", true)]
    [InlineData("\"C:\\Apps\\Other\\DesktopLauncher.exe\" --background", @"C:\Apps\Launcher", false)]
    [InlineData("\"C:\\Apps\\Launcher\\sub\\DesktopLauncher.exe\" --background", @"C:\Apps\Launcher", false)]
    [InlineData("", @"C:\Apps\Launcher", false)]
    [InlineData("\"\" --background", @"C:\Apps\Launcher", false)]
    public void IsInFolder_ComparesTheExeFolder(string command, string folder, bool expected) =>
        Assert.Equal(expected, StartupRegistration.IsInFolder(command, folder));
}
