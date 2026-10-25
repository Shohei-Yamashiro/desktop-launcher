using System.Windows.Input;
using DesktopLauncher.Models;

namespace DesktopLauncher.Tests;

public class HotKeyGestureTests
{
    [Fact]
    public void Default_IsCtrlAltSpace() =>
        Assert.Equal("Ctrl+Alt+Space", HotKeyGesture.Default.DisplayText);

    [Theory]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift, Key.Space, "Ctrl+Shift+Space")]
    [InlineData(ModifierKeys.Alt | ModifierKeys.Control, Key.L, "Ctrl+Alt+L")]
    [InlineData(ModifierKeys.Windows | ModifierKeys.Shift, Key.D1, "Shift+Win+1")]
    [InlineData(ModifierKeys.Control, Key.F12, "Ctrl+F12")]
    public void DisplayText_OrdersModifiers(ModifierKeys modifiers, Key key, string expected) =>
        Assert.Equal(expected, new HotKeyGesture(modifiers, key).DisplayText);

    [Theory]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("Shift+Win+1")]
    [InlineData("Ctrl+F12")]
    [InlineData("Alt+Oem3")]
    public void Parse_ReadsWhatDisplayTextWrites(string text) =>
        Assert.Equal(text, HotKeyGesture.Parse(text)?.DisplayText);

    [Fact]
    public void Parse_IgnoresCaseAndSpaces() =>
        Assert.Equal(HotKeyGesture.Default, HotKeyGesture.Parse(" ctrl + ALT + space "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Space")]
    [InlineData("Shift+A")]
    [InlineData("Ctrl+LeftShift")]
    [InlineData("Ctrl+Hyper+A")]
    [InlineData("Ctrl+NoSuchKey")]
    [InlineData("Ctrl+65")]
    public void Parse_RejectsInvalid(string? text) =>
        Assert.Null(HotKeyGesture.Parse(text));

    [Theory]
    [InlineData(ModifierKeys.None, Key.A, false)]
    [InlineData(ModifierKeys.Shift, Key.A, false)]
    [InlineData(ModifierKeys.Control, Key.LeftAlt, false)]
    [InlineData(ModifierKeys.Control, Key.A, true)]
    [InlineData(ModifierKeys.Alt | ModifierKeys.Shift, Key.F1, true)]
    [InlineData(ModifierKeys.Windows, Key.Space, true)]
    public void IsValid_NeedsCtrlAltOrWinAndARealKey(ModifierKeys modifiers, Key key, bool expected) =>
        Assert.Equal(expected, new HotKeyGesture(modifiers, key).IsValid);

    [Fact]
    public void PartialText_ShowsHeldModifiers() =>
        Assert.Equal("Ctrl+Alt+", HotKeyGesture.PartialText(ModifierKeys.Alt | ModifierKeys.Control));
}
