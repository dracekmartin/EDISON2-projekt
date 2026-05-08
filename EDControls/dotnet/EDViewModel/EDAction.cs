using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EDControls.EDViewModel;

public class EDAction(string label, ICommand command, ImageSource? icon = null, Key key = Key.None, ModifierKeys modifiers = ModifierKeys.None)
{
    public string Label { get; } = label;
    public ICommand Command { get; } = command;
    public ImageSource? Icon { get; } = icon;
    /// <summary>
    /// Vrací nový <see cref="Image"/> při každém přístupu — WPF <c>MenuItem.Icon</c> přes
    /// <c>ItemContainerStyle</c> setter nesnáší sdílení jednoho UIElement instance napříč MenuItems
    /// (UIElement smí mít právě jednoho rodiče). Computed property obejde tím že každý MenuItem
    /// dostane vlastní <c>Image</c>.
    /// </summary>
    public Image? IconElement => Icon != null ? new Image { Source = Icon, Width = 16, Height = 16 } : null;
    public Key Key { get; } = key;
    public ModifierKeys Modifiers { get; } = modifiers;
    public string ShortcutText { get; } = key != Key.None ? BuildShortcutText(key, modifiers) : "";
    public string ToolTipText => ShortcutText.Length > 0 ? $"{Label} [{ShortcutText}]" : Label;

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
