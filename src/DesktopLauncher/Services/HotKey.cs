using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopLauncher.Models;

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
    private HotKeyGesture _gesture;
    private bool _registered;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public HotKey(Window window, HotKeyGesture gesture)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WndProc);
        _gesture = gesture;
    }

    public event Action? Pressed;

    /// <summary>Raised after <see cref="Change"/> switched to another combination.</summary>
    public event Action? Changed;

    public HotKeyGesture Gesture => _gesture;

    public string DisplayText => _gesture.DisplayText;

    /// <summary>Registers the current combination; also resumes after <see cref="Suspend"/>. False when another app holds it.</summary>
    public bool Register()
    {
        if (!_registered) _registered = TryRegister(_gesture);
        return _registered;
    }

    /// <summary>Releases the combination for a while, such as while a new one is being typed in the settings.</summary>
    public void Suspend()
    {
        if (_registered) UnregisterHotKey(_source.Handle, Id);
        _registered = false;
    }

    /// <summary>
    /// Switches to <paramref name="gesture"/>. When another app holds it, nothing is registered and the
    /// current combination stays, ready to be restored with <see cref="Register"/>.
    /// </summary>
    public bool Change(HotKeyGesture gesture)
    {
        Suspend();
        if (!TryRegister(gesture)) return false;

        _registered = true;
        if (gesture == _gesture) return true;
        _gesture = gesture;
        Changed?.Invoke();
        return true;
    }

    // ModifierKeys uses the same bit values as the MOD_* flags.
    private bool TryRegister(HotKeyGesture gesture) =>
        RegisterHotKey(_source.Handle, Id, (uint)gesture.Modifiers | MOD_NOREPEAT, (uint)KeyInterop.VirtualKeyFromKey(gesture.Key));

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
        Suspend();
        _source.RemoveHook(WndProc);
    }
}
