using System.Reflection;
using System.Windows.Input;
using System.Windows.Markup;

namespace TemplateDesigner.Designer;

/// <summary>Zkratka návrháře: co dělá, kam patří a čím se spouští.</summary>
public sealed class KeyboardShortcut
{
    private KeyboardShortcut(string category, string action, Key key, ModifierKeys modifiers, string gesture)
    {
        Category = category;
        Action = action;
        Key = key;
        Modifiers = modifiers;
        Gesture = gesture;
    }

    /// <summary>Zkratka jednou klávesou. Text se skládá z klávesy, takže nápis v menu a obsluha
    /// klávesy se nemůžou rozejít.</summary>
    internal static KeyboardShortcut FromKey(string category, string action, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        new(category, action, key, modifiers, Format(key, modifiers));

    /// <summary>Gesto, které jedna klávesa nevyjádří: myš, šipky libovolným směrem, dvě klávesy po sobě.</summary>
    internal static KeyboardShortcut FromText(string category, string action, string gesture) =>
        new(category, action, Key.None, ModifierKeys.None, gesture);

    public string Category { get; }
    public string Action { get; }
    public Key Key { get; }
    public ModifierKeys Modifiers { get; }

    /// <summary>Text pro uživatele: „Ctrl+Shift+P", „Alt+klik".</summary>
    public string Gesture { get; }

    /// <summary>Bublina ve tvaru „Akce - zkratka" (<c>ux-conventions §2</c>).</summary>
    public string Tip => $"{Action} - {Gesture}";

    /// <summary>Přesná shoda: Ctrl+Shift+Z není Ctrl+Z a AltGr (Ctrl+Alt) nespustí zkratku s Ctrl.</summary>
    public bool Matches(Key key, ModifierKeys modifiers) => Key != Key.None && key == Key && modifiers == Modifiers;

    /// <summary>S Altem hlásí WPF klávesu jako systémovou - skutečná je v <c>SystemKey</c>.</summary>
    public bool Matches(KeyEventArgs e) => Matches(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);

    private static string Format(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Left => "←",
            Key.Right => "→",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Escape => "Esc",
            Key.Enter => "Enter",
            Key.Back => "Backspace",
            _ => key.ToString(),
        });
        return string.Join("+", parts);
    }
}

/// <summary>
/// Všechny zkratky návrháře na jednom místě. Obsluha kláves je porovnává přes <see cref="KeyboardShortcut.Matches(KeyEventArgs)"/>,
/// menu a bubliny berou text přes <see cref="GestureExtension"/> a <see cref="TipExtension"/>, nápověda F1 a asistent
/// z nich skládají své seznamy. Do 30. 10. byly zkratky opsané na sedmi místech a rozcházely se (Del × Delete,
/// PgUp × PageUp, popis Ctrl+D + šipka). Zkratky v ostatních textech a v příručce hlídá <c>KlavesoveZkratkyTests</c>.
/// </summary>
public static class KeyboardShortcuts
{
    public const string Soubor = "Soubor a šablony";
    public const string Upravy = "Úpravy";
    public const string Vyber = "Výběr";
    public const string Sekce = "Sekce";
    public const string Zobrazeni = "Zobrazení";
    public const string Tabulka = "Uvnitř pevné tabulky";
    public const string PsaniTextu = "Psaní textu na plátně";
    public const string OknoNapovedy = "Okno nápovědy";

    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys CtrlShift = ModifierKeys.Control | ModifierKeys.Shift;

    public static readonly KeyboardShortcut New = KeyboardShortcut.FromKey(Soubor, "Nová šablona", Key.N, Ctrl);
    public static readonly KeyboardShortcut Open = KeyboardShortcut.FromKey(Soubor, "Otevřít z databáze", Key.O, Ctrl);
    public static readonly KeyboardShortcut Save = KeyboardShortcut.FromKey(Soubor, "Uložit do databáze", Key.S, Ctrl);
    public static readonly KeyboardShortcut Print = KeyboardShortcut.FromKey(Soubor, "Tisk", Key.P, Ctrl);
    public static readonly KeyboardShortcut PageProperties = KeyboardShortcut.FromKey(Soubor, "Vlastnosti stránky", Key.P, CtrlShift);
    public static readonly KeyboardShortcut ChangeAgenda = KeyboardShortcut.FromKey(Soubor, "Změnit agendu", Key.A, CtrlShift);
    public static readonly KeyboardShortcut SelectInstances = KeyboardShortcut.FromKey(Soubor, "Výběr linek", Key.I, CtrlShift);
    public static readonly KeyboardShortcut RefreshData = KeyboardShortcut.FromKey(Soubor, "Obnovit data z databáze", Key.F5);

    public static readonly KeyboardShortcut Undo = KeyboardShortcut.FromKey(Upravy, "Zpět", Key.Z, Ctrl);
    public static readonly KeyboardShortcut Redo = KeyboardShortcut.FromKey(Upravy, "Vpřed", Key.Y, Ctrl);
    public static readonly KeyboardShortcut Cut = KeyboardShortcut.FromKey(Upravy, "Vyjmout", Key.X, Ctrl);
    public static readonly KeyboardShortcut Copy = KeyboardShortcut.FromKey(Upravy, "Kopírovat", Key.C, Ctrl);
    public static readonly KeyboardShortcut Paste = KeyboardShortcut.FromKey(Upravy, "Vložit", Key.V, Ctrl);
    public static readonly KeyboardShortcut Duplicate = KeyboardShortcut.FromKey(Upravy, "Duplikovat", Key.D, Ctrl);
    public static readonly KeyboardShortcut DuplicateBeside = KeyboardShortcut.FromText(Upravy, "Duplikovat vedle s mezerou pro přichytávání", "Ctrl+D, pak šipka");
    public static readonly KeyboardShortcut CopyStyle = KeyboardShortcut.FromKey(Upravy, "Kopírovat styl", Key.C, CtrlShift);
    public static readonly KeyboardShortcut PasteStyle = KeyboardShortcut.FromKey(Upravy, "Vložit styl", Key.V, CtrlShift);
    public static readonly KeyboardShortcut Delete = KeyboardShortcut.FromKey(Upravy, "Smazat", Key.Delete);
    public static readonly KeyboardShortcut Group = KeyboardShortcut.FromKey(Upravy, "Seskupit", Key.G, Ctrl);
    public static readonly KeyboardShortcut Ungroup = KeyboardShortcut.FromKey(Upravy, "Rozeskupit", Key.G, CtrlShift);
    public static readonly KeyboardShortcut SplitHorizontally = KeyboardShortcut.FromKey(Upravy, "Rozdělit vodorovně", Key.H, CtrlShift);
    public static readonly KeyboardShortcut SplitVertically = KeyboardShortcut.FromKey(Upravy, "Rozdělit svisle", Key.J, CtrlShift);
    public static readonly KeyboardShortcut NormalizePositions = KeyboardShortcut.FromKey(Upravy, "Normalizovat pozice", Key.N, CtrlShift);
    public static readonly KeyboardShortcut BringForward = KeyboardShortcut.FromKey(Upravy, "Přenést dopředu", Key.PageUp, Ctrl);
    public static readonly KeyboardShortcut SendBackward = KeyboardShortcut.FromKey(Upravy, "Odeslat dozadu", Key.PageDown, Ctrl);
    public static readonly KeyboardShortcut BringToFront = KeyboardShortcut.FromKey(Upravy, "Přenést zcela dopředu", Key.PageUp, CtrlShift);
    public static readonly KeyboardShortcut SendToBack = KeyboardShortcut.FromKey(Upravy, "Odeslat zcela dozadu", Key.PageDown, CtrlShift);
    public static readonly KeyboardShortcut Move = KeyboardShortcut.FromText(Upravy, "Posunout o 1 mm", "šipky");
    public static readonly KeyboardShortcut MoveByGrid = KeyboardShortcut.FromText(Upravy, "Posunout o krok mřížky", "Ctrl+šipky");
    public static readonly KeyboardShortcut Resize = KeyboardShortcut.FromText(Upravy, "Změnit velikost o 1 mm (pravá a dolní hrana)", "Shift+šipky");

    public static readonly KeyboardShortcut AddToSelection = KeyboardShortcut.FromText(Vyber, "Přidat prvek do výběru nebo ho z něj odebrat", "Ctrl+klik");
    public static readonly KeyboardShortcut SelectFromGroup = KeyboardShortcut.FromText(Vyber, "Vybrat jeden prvek ze skupiny", "Alt+klik");
    public static readonly KeyboardShortcut SelectAll = KeyboardShortcut.FromKey(Vyber, "Vybrat vše v aktivní sekci", Key.A, Ctrl);
    public static readonly KeyboardShortcut Properties = KeyboardShortcut.FromKey(Vyber, "Vlastnosti výběru", Key.Enter);
    public static readonly KeyboardShortcut EditContent = KeyboardShortcut.FromKey(Vyber, "Upravit text na plátně, vyplnit pevnou tabulku", Key.F2);
    public static readonly KeyboardShortcut Cancel = KeyboardShortcut.FromKey(Vyber, "Zrušit probíhající akci nebo výběr", Key.Escape);

    public static readonly KeyboardShortcut MoveSectionUp = KeyboardShortcut.FromKey(Sekce, "Přesunout aktivní sekci nahoru", Key.Up, CtrlShift);
    public static readonly KeyboardShortcut MoveSectionDown = KeyboardShortcut.FromKey(Sekce, "Přesunout aktivní sekci dolů", Key.Down, CtrlShift);

    public static readonly KeyboardShortcut FitToPage = KeyboardShortcut.FromKey(Zobrazeni, "Přizpůsobit stránce", Key.D0, Ctrl);
    public static readonly KeyboardShortcut ActualSize = KeyboardShortcut.FromKey(Zobrazeni, "Skutečná velikost 100 %", Key.D1, Ctrl);
    public static readonly KeyboardShortcut ZoomAtCursor = KeyboardShortcut.FromText(Zobrazeni, "Zoom ke kurzoru", "Ctrl+kolečko");
    public static readonly KeyboardShortcut ToggleLeftPanel = KeyboardShortcut.FromKey(Zobrazeni, "Sbalit nebo rozbalit levý panel", Key.B, Ctrl);
    public static readonly KeyboardShortcut Help = KeyboardShortcut.FromKey(Zobrazeni, "Nápověda", Key.F1);

    public static readonly KeyboardShortcut EnterCell = KeyboardShortcut.FromText(Tabulka, "Vstoupit rovnou do buňky", "Alt+klik");
    public static readonly KeyboardShortcut EditCell = KeyboardShortcut.FromKey(Tabulka, "Upravit obsah buňky", Key.F2);
    public static readonly KeyboardShortcut CellProperties = KeyboardShortcut.FromKey(Tabulka, "Vlastnosti buňky", Key.Enter, ModifierKeys.Alt);
    public static readonly KeyboardShortcut CellBold = KeyboardShortcut.FromKey(Tabulka, "Tučné", Key.B, Ctrl);
    public static readonly KeyboardShortcut CellItalic = KeyboardShortcut.FromKey(Tabulka, "Kurzíva", Key.I, Ctrl);
    public static readonly KeyboardShortcut CellUnderline = KeyboardShortcut.FromKey(Tabulka, "Podtržení", Key.U, Ctrl);
    public static readonly KeyboardShortcut NextCell = KeyboardShortcut.FromKey(Tabulka, "Buňka vedle", Key.Tab);
    public static readonly KeyboardShortcut PreviousCell = KeyboardShortcut.FromKey(Tabulka, "Předchozí buňka", Key.Tab, ModifierKeys.Shift);
    public static readonly KeyboardShortcut CellBelow = KeyboardShortcut.FromKey(Tabulka, "Potvrdit a o buňku níž", Key.Enter);
    public static readonly KeyboardShortcut CellAbove = KeyboardShortcut.FromKey(Tabulka, "Potvrdit a o buňku výš", Key.Enter, ModifierKeys.Shift);
    public static readonly KeyboardShortcut ExtendCellSelection = KeyboardShortcut.FromText(Tabulka, "Rozšířit výběr buněk", "Shift+šipky");
    public static readonly KeyboardShortcut ExtendCellSelectionByClick = KeyboardShortcut.FromText(Tabulka, "Rozšířit výběr buněk od kotvy", "Shift+klik");
    public static readonly KeyboardShortcut SelectAllCells = KeyboardShortcut.FromKey(Tabulka, "Vybrat všechny buňky", Key.A, Ctrl);
    public static readonly KeyboardShortcut CopyCells = KeyboardShortcut.FromKey(Tabulka, "Kopírovat obsah buněk", Key.C, Ctrl);
    public static readonly KeyboardShortcut CutCells = KeyboardShortcut.FromKey(Tabulka, "Vyjmout obsah buněk", Key.X, Ctrl);
    public static readonly KeyboardShortcut PasteCells = KeyboardShortcut.FromKey(Tabulka, "Vložit do buněk", Key.V, Ctrl);
    public static readonly KeyboardShortcut ClearCells = KeyboardShortcut.FromKey(Tabulka, "Vymazat obsah buněk (ne prvek)", Key.Delete);
    public static readonly KeyboardShortcut LeaveCells = KeyboardShortcut.FromKey(Tabulka, "Ven z buňky na prvek", Key.Escape);

    public static readonly KeyboardShortcut ConfirmText = KeyboardShortcut.FromKey(PsaniTextu, "Potvrdit", Key.Enter);
    public static readonly KeyboardShortcut NewLine = KeyboardShortcut.FromKey(PsaniTextu, "Nový řádek", Key.Enter, ModifierKeys.Shift);
    public static readonly KeyboardShortcut DiscardText = KeyboardShortcut.FromKey(PsaniTextu, "Zahodit úpravu", Key.Escape);

    public static readonly KeyboardShortcut HelpSearch = KeyboardShortcut.FromKey(OknoNapovedy, "Hledat", Key.F, Ctrl);

    private static readonly FieldInfo[] Fields = typeof(KeyboardShortcuts)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(KeyboardShortcut))
        .OrderBy(f => f.MetadataToken)
        .ToArray();

    /// <summary>Všechny zkratky v pořadí, jak jsou zapsané výš.</summary>
    public static IReadOnlyList<KeyboardShortcut> All { get; } = [.. Fields.Select(f => (KeyboardShortcut)f.GetValue(null)!)];

    /// <summary>Zkratka podle jména pole (pro XAML); neznámé jméno je chyba v okně, ne tichý prázdný text.</summary>
    public static KeyboardShortcut Get(string name) =>
        Fields.FirstOrDefault(f => f.Name == name)?.GetValue(null) as KeyboardShortcut
        ?? throw new ArgumentException($"Zkratka „{name}\" v KeyboardShortcuts není.", nameof(name));

    /// <summary>Text více zkratek za sebou: „Ctrl+Z / Ctrl+Y".</summary>
    public static string Gestures(params KeyboardShortcut[] shortcuts) => string.Join(" / ", shortcuts.Select(s => s.Gesture));
}

/// <summary>Text zkratky do XAML: <c>InputGestureText="{td:Gesture Undo}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class GestureExtension(string name) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => KeyboardShortcuts.Get(name).Gesture;
}

/// <summary>Bublina „Akce - zkratka": <c>ToolTip="{td:Tip Undo}"</c>; <c>Text</c> nahradí popis akce,
/// když ho tlačítko říká jinak (šipky sekce v panelu).</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TipExtension(string name) : MarkupExtension
{
    public string? Text { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var shortcut = KeyboardShortcuts.Get(name);
        return $"{Text ?? shortcut.Action} - {shortcut.Gesture}";
    }
}
