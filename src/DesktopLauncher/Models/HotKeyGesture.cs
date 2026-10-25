using System.Windows.Input;

namespace DesktopLauncher.Models;

/// <summary>
/// A key combination for the system-wide hotkey, saved as text such as "Ctrl+Alt+Space".
/// It must include Ctrl, Alt or Win, so that it does not take away a key used for typing.
/// </summary>
public sealed record HotKeyGesture(ModifierKeys Modifiers, Key Key)
{
    private const ModifierKeys Required = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows;

    public static HotKeyGesture Default { get; } = new(ModifierKeys.Control | ModifierKeys.Alt, Key.Space);

    public bool IsValid =>
        (Modifiers & Required) != 0
        && !IsModifierKey(Key)
        && Key is not (Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed);

    public static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    public string DisplayText => string.Join("+", ModifierNames(Modifiers).Append(KeyName(Key)));

    /// <summary>"Ctrl+Alt+" while only modifier keys are held.</summary>
    public static string PartialText(ModifierKeys modifiers) => string.Concat(ModifierNames(modifiers).Select(m => m + "+"));

    /// <summary>Reads text written by <see cref="DisplayText"/>; null when it is missing or not a valid combination.</summary>
    public static HotKeyGesture? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var modifiers = ModifierKeys.None;
        foreach (var part in parts[..^1])
        {
            if (ModifierFromName(part) is not { } modifier) return null;
            modifiers |= modifier;
        }

        if (KeyFromName(parts[^1]) is not { } key) return null;
        var gesture = new HotKeyGesture(modifiers, key);
        return gesture.IsValid ? gesture : null;
    }

    private static ModifierKeys? ModifierFromName(string name) => name.ToLowerInvariant() switch
    {
        "ctrl" => ModifierKeys.Control,
        "alt" => ModifierKeys.Alt,
        "shift" => ModifierKeys.Shift,
        "win" => ModifierKeys.Windows,
        _ => null,
    };

    /// <summary>"0"-"9" for the digit keys, otherwise a <see cref="Key"/> name (not its number).</summary>
    private static Key? KeyFromName(string name)
    {
        if (name.Length == 1 && char.IsAsciiDigit(name[0])) return Key.D0 + (name[0] - '0');
        if (int.TryParse(name, out _)) return null;
        return Enum.TryParse(name, ignoreCase: true, out Key key) ? key : null;
    }

    private static IEnumerable<string> ModifierNames(ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(ModifierKeys.Alt)) yield return "Alt";
        if (modifiers.HasFlag(ModifierKeys.Shift)) yield return "Shift";
        if (modifiers.HasFlag(ModifierKeys.Windows)) yield return "Win";
    }

    private static string KeyName(Key key) => key is >= Key.D0 and <= Key.D9 ? ((int)(key - Key.D0)).ToString() : key.ToString();
}
