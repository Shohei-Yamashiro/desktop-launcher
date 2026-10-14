using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopLauncher.Services;

/// <summary>
/// System-wide hotkey registered with RegisterHotKey on the window's message loop.
/// </summary>
public sealed class HotKey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int Id = 0x4C31;

    private readonly HwndSource _source;
    private readonly ModifierKeys _modifiers;
    private readonly Key _key;
    private bool _registered;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotKey(Window window, ModifierKeys modifiers, Key key)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);
        _modifiers = modifiers;
        _key = key;
    }

    public event Action? Pressed;

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (_modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (_modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (_modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (_modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(_key.ToString());
            return string.Join("+", parts);
        }
    }

    public bool Register()
    {
        // ModifierKeys uses the same bit values as the MOD_* flags.
        var vk = (uint)KeyInterop.VirtualKeyFromKey(_key);
        _registered = RegisterHotKey(_source.Handle, Id, (uint)_modifiers | MOD_NOREPEAT, vk);
        return _registered;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == Id)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(_source.Handle, Id);
        _registered = false;
        _source.RemoveHook(WndProc);
    }
}
