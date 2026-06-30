using System.Windows.Input;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>Testy čisté logiky EDAction: text klávesové zkratky a tooltip.</summary>
public class EDActionTests
{
    private static EDAction Action(Key key = Key.None, ModifierKeys mods = ModifierKeys.None) =>
        new("Smazat", Substitute.For<ICommand>(), icon: null, key, mods);

    [Fact]
    public void ShortcutText_NoKey_IsEmpty()
    {
        Assert.Equal("", Action().ShortcutText);
    }

    [Theory]
    [InlineData(Key.Insert, ModifierKeys.None, "Ins")]
    [InlineData(Key.Delete, ModifierKeys.None, "Del")]
    [InlineData(Key.Return, ModifierKeys.None, "Enter")]
    [InlineData(Key.Escape, ModifierKeys.None, "Esc")]
    [InlineData(Key.F2, ModifierKeys.None, "F2")]
    [InlineData(Key.S, ModifierKeys.Control, "Ctrl+S")]
    [InlineData(Key.D, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+D")]
    [InlineData(Key.N, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, "Ctrl+Shift+Alt+N")]
    public void ShortcutText_FormatsKeyAndModifiers(Key key, ModifierKeys mods, string expected)
    {
        Assert.Equal(expected, Action(key, mods).ShortcutText);
    }

    [Fact]
    public void ToolTipText_WithShortcut_AppendsBracketed()
    {
        Assert.Equal("Smazat [Ctrl+Del]", Action(Key.Delete, ModifierKeys.Control).ToolTipText);
    }

    [Fact]
    public void ToolTipText_NoShortcut_IsLabelOnly()
    {
        Assert.Equal("Smazat", Action().ToolTipText);
    }
}
