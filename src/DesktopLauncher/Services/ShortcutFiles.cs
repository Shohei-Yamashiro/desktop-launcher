using System.IO;
using System.Runtime.InteropServices;

namespace DesktopLauncher.Services;

public static class ShortcutFiles
{
    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOERRORUI = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    public static bool IsShortcut(string path) =>
        (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase))
        && File.Exists(path);

    /// <summary>Copies the file into the directory, adding " (2)", " (3)"... when the name is taken.</summary>
    public static string CopyInto(string path, string directory)
    {
        Directory.CreateDirectory(directory);
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var destination = Path.Combine(directory, name + extension);
        for (var i = 2; File.Exists(destination); i++)
        {
            destination = Path.Combine(directory, $"{name} ({i}){extension}");
        }
        File.Copy(path, destination);
        return destination;
    }

    public static bool MoveToRecycleBin(string path)
    {
        var operation = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };
        var result = SHFileOperation(ref operation);
        return result == 0 && !operation.fAnyOperationsAborted && !File.Exists(path);
    }

    /// <summary>Target path of a .lnk, or the URL of a .url file; null when it cannot be read.</summary>
    public static string? ResolveTarget(string path)
    {
        try
        {
            if (Path.GetExtension(path).Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                return File.ReadLines(path)
                    .FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))?[4..];
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                string target = shell.CreateShortcut(path).TargetPath;
                return string.IsNullOrEmpty(target) ? null : target;
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            return null;
        }
    }
}
