using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls;

public class EDAction
{
    public string Label { get; }
    public ImageSource? Icon { get; }
    /// <summary>
    /// Returns a fresh <see cref="Image"/> element on every access.
    /// <para>
    /// WPF <c>MenuItem.Icon</c> can only be set through an <c>ItemContainerStyle</c> setter,
    /// which shares a single value instance across all generated items. A <see cref="System.Windows.UIElement"/>
    /// can only have one parent in the visual tree, so sharing one <c>Image</c> object causes a
    /// runtime visual-tree error as soon as a second item tries to claim it.
    /// </para>
    /// <para>
    /// The workaround: make the setter value a computed property that allocates a new <c>Image</c>
    /// each time it is read. WPF evaluates the binding (and therefore calls this getter) once per
    /// item, so every <c>MenuItem</c> ends up with its own independent <c>Image</c> instance even
    /// though the setter mechanism is shared.
    /// </para>
    /// </summary>
    public Image? IconElement => Icon != null ? new Image { Source = Icon, Width = 16, Height = 16 } : null;
    public Key Key { get; }
    public ModifierKeys Modifiers { get; }
    public ICommand Command { get; }
    public string ShortcutText { get; }
    public string ToolTipText => $"{Label} [{ShortcutText}]";

    public EDAction(string label, ImageSource? icon, Key key, ModifierKeys modifiers, ICommand command)
    {
        Label = label;
        Icon = icon;
        Key = key;
        Modifiers = modifiers;
        Command = command;
        ShortcutText = BuildShortcutText(key, modifiers);
    }

    private static string BuildShortcutText(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
        parts.Add(key switch
        {
            Key.Insert => "Ins",
            Key.Delete => "Del",
            Key.Return => "Enter",
            Key.Escape => "Esc",
            _          => key.ToString()
        });
        return string.Join("+", parts);
    }
}
