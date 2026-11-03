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
    /// Vrací nový <see cref="Image"/> při každém přístupu - WPF <c>MenuItem.Icon</c> přes
    /// <c>ItemContainerStyle</c> setter nesnáší sdílení jednoho UIElement instance napříč MenuItems
    /// (UIElement smí mít právě jednoho rodiče). Computed property obejde tím že každý MenuItem
    /// dostane vlastní <c>Image</c>.
    /// </summary>
    public Image? IconElement => Icon != null ? new Image { Source = Icon, Width = 16, Height = 16 } : null;
    public Key Key { get; } = key;
    public ModifierKeys Modifiers { get; } = modifiers;
    public string ShortcutText { get; } = key != Key.None ? BuildShortcutText(key, modifiers) : "";
    public string ToolTipText => ShortcutText.Length > 0 ? $"{Label} [{ShortcutText}]" : Label;

    /// <summary>Oddělovač skupiny akcí: v pruhu nad seznamem svislá čára, v nabídkách vodorovná. Nic nespouští.</summary>
    public static readonly EDAction Separator = new("", NoCommand.Instance);

    public bool IsSeparator => ReferenceEquals(this, Separator);

    /// <summary>Nabídka akcí (tlačítko Akce v <c>EDFooter</c>, pravé tlačítko na řádku <c>EDList</c>) s popiskem,
    /// zkratkou a ikonou jako ikony v pruhu. Položky v kódu dostanou themed styl MenuItem samy.</summary>
    internal static ContextMenu BuildContextMenu(IEnumerable<EDAction> actions)
    {
        var menu = new ContextMenu();
        foreach (var action in actions)
            menu.Items.Add(action.IsSeparator ? new Separator() : new MenuItem
            {
                Header = action.Label, Command = action.Command,
                InputGestureText = action.ShortcutText, Icon = action.IconElement,
            });
        return menu;
    }

    private sealed class NoCommand : ICommand
    {
        public static readonly NoCommand Instance = new();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) { }
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
