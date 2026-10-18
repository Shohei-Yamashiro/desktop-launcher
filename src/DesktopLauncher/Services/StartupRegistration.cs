using System.IO;
using Microsoft.Win32;

namespace DesktopLauncher.Services;

/// <summary>
/// Opt-in sign-in startup via HKCU\...\Run. This is the only thing the app writes outside its folder.
/// </summary>
public static class StartupRegistration
{
    public const string BackgroundArg = "--background";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hashelf";

    /// <summary>The value name used before the app was renamed to Hashelf.</summary>
    private const string LegacyValueName = "DesktopLauncher";

    private static string Command => $"\"{Environment.ProcessPath}\" {BackgroundArg}";

    private static string AppFolder => Path.GetDirectoryName(Environment.ProcessPath) ?? "";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, Command);
        RemoveLegacy(key);
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;
        key.DeleteValue(ValueName, throwOnMissingValue: false);
        RemoveLegacy(key);
    }

    /// <summary>
    /// Re-registers under the new name when the old version in this same folder had startup turned on.
    /// Registrations made by copies in other folders are left alone.
    /// </summary>
    public static void MigrateLegacy()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(LegacyValueName) is not string legacy || !IsInFolder(legacy, AppFolder)) return;
        key.SetValue(ValueName, Command);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }

    /// <summary>Whether a Run command ("C:\dir\app.exe" --background) starts an exe directly in <paramref name="folder"/>.</summary>
    public static bool IsInFolder(string command, string folder)
    {
        command = command.Trim();
        var exe = command.StartsWith('"')
            ? command[1..].Split('"')[0]
            : command.Split(' ')[0];
        if (exe.Length == 0 || folder.Length == 0) return false;

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(exe));
            return string.Equals(
                directory?.TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static void RemoveLegacy(RegistryKey key)
    {
        if (key.GetValue(LegacyValueName) is string legacy && IsInFolder(legacy, AppFolder))
        {
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        }
    }
}
