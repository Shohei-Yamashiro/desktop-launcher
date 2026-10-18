using System.Runtime.InteropServices;
using System.Windows;
using DesktopLauncher.Models;

namespace DesktopLauncher.Services;

/// <summary>Reads what can be registered from the clipboard. Another app may hold the clipboard open, so reads can fail.</summary>
public static class ClipboardContent
{
    /// <summary>Files and folders copied in Explorer.</summary>
    public static string[]? Files()
    {
        try
        {
            return Clipboard.ContainsFileDropList() ? [.. Clipboard.GetFileDropList().Cast<string>()] : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static string? Url()
    {
        try
        {
            return Clipboard.ContainsText() && WebUrl.TryNormalize(Clipboard.GetText(), out var url) ? url : null;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
