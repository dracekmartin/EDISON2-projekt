using TemplateDesigner.Commands;
using TemplateDesigner.Models;

namespace TemplateDesigner.Designer;

public record StyleClipboard(
    TextStyle? TextStyle,
    string BorderColor,
    float BorderWidth,
    string BackgroundColor);

public class DesignerState
{
    private Template _currentTemplate = new();
    public Template CurrentTemplate
    {
        get => _currentTemplate;
        set
        {
            _currentTemplate = value;
            // Cache seřazených elementů je klíčovaná Guid sekce a invaliduje se jen při editaci.
            // Při swapu šablony (Open/New/Import) by přežila → nová šablona se stejným Guid sekce
            // (systémová šablona má pevné Guidy) by dostala stará data předchozí. Invalidovat na swapu.
            InvalidateElementCache();
            Predloha = 0;   // jiná šablona se otevírá na první stránce
            TemplateSwapped?.Invoke();
        }
    }

    /// <summary>Volá se při výměně šablony (Open / New / Import / generovaná). Jediné místo, kde
    /// se dá zachytit „teď je v návrháři něco jiného" - proto sem, ne do jednotlivých volajících,
    /// kterých je pět a šestý by se zapomněl.</summary>
    public Action? TemplateSwapped { get; set; }

    /// <summary>Aktuálně aktivní sekce (do které se přidávají elementy). Null = žádná.</summary>
    public Section? ActiveSection { get; set; }

    public List<TemplateElement> SelectedElements { get; } = [];

    /// <summary>Která předloha stránky je otevřená na plátně (0 = první). Jen u šablony o víc
    /// stránkách; plátno index mimo rozsah srovná na poslední (`B184`).</summary>
    public int Predloha { get; set; }

    /// <summary>Náhled ukazuje celý doklad; false = jen stránky z otevřené předlohy (výchozí u
    /// šablony o víc stránkách). Jednostránkové šablony se to netýká.</summary>
    public bool NahledCelyDoklad { get; set; }

    private float _zoom = 1.0f;
    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, 0.1f, 10.0f);
    }

    // Defaulty (pre-version průchod 2026-07-13): SnapToGrid ZAP + jemný 1mm krok - drží čisté
    // souřadnice, aniž by omezoval (5mm krok byl hlavní zdroj pocitu „nepřesného" snapu).
    // ShowGrid vypnuto (čistý canvas). Per-template EditorPreferences tohle přepíšou.
    public bool ShowGrid { get; set; } = false;
    public bool SnapToGrid { get; set; } = true;
    public bool SnapToElements { get; set; } = true;
    public bool ShowSectionHeaders { get; set; } = true;
    public bool ShowRuler { get; set; } = false;
    public float GridSize { get; set; } = 1f; // mm

    /// <summary>
    /// Výchozí mezera mezi objekty při smart snapu (element↔element, element↔okraj sekce po P1)
    /// a při směrovém duplikování (Ctrl+D+šipka). User-configurable v toolbaru.
    /// </summary>
    public float SnapGapMm { get; set; } = 2f;
    public List<GapIndicator> ActiveGapIndicators { get; } = [];

    // ── Drag/resize badge - real-time pozice/rozměr během interakce ──

    /// <summary>Text badge zobrazovaný během dragu/resize (např. "X: 20 mm, Y: 40 mm" nebo "42 × 8 mm"). Null = skrýt.</summary>
    public string? DragBadgeText { get; set; }

    /// <summary>Pozice badge v px na canvasu (obvykle u kurzoru). Platí jen pokud DragBadgeText != null.</summary>
    public float DragBadgeX { get; set; }

    /// <summary>Pozice badge v px na canvasu.</summary>
    public float DragBadgeY { get; set; }

    /// <summary>Elementy jejichž boundary se zvýrazní během snapu (cílové elementy).</summary>
    public List<TemplateElement> SnapTargetElements { get; } = [];

    /// <summary>X souřadnice (mm v section coords) section-center guide line. Null = neaktivní.</summary>
    public float? ActiveSectionCenterX { get; set; }

    /// <summary>Aktuální preview DataContext (nastavuje se při renderování preview).</summary>
    public DataContext? CurrentPreviewData { get; set; }

    public StyleClipboard? StyleClipboard { get; set; }

    public Stack<IDesignerCommand> UndoStack { get; } = new();
    public Stack<IDesignerCommand> RedoStack { get; } = new();

    public bool CanUndo => UndoStack.Count > 0;
    public bool CanRedo => RedoStack.Count > 0;

    // ── Cache seřazených elementů per sekce ─────────────────────────────────
    private readonly Dictionary<Guid, List<TemplateElement>> _sortedByZIndexDesc = new();
    private readonly Dictionary<Guid, List<TemplateElement>> _sortedByZIndexAsc = new();

    /// <summary>
    /// Vrátí elementy sekce v pořadí pro hit testing - tedy <b>přesně obráceně</b> než je kreslí
    /// <see cref="GetElementsByZIndexAsc"/>.
    /// </summary>
    /// <remarks>
    /// Musí to být obrácené kreslicí pořadí, ne `OrderByDescending`: řazení je stabilní, takže
    /// při stejném ZIndex (nové elementy mají všechny 0) by vrátilo ten nejstarší, kdežto navrch
    /// se kreslí ten nejnovější. Klik pak sáhl na element pod tím, který uživatel vidí.
    /// </remarks>
    public List<TemplateElement> GetElementsByZIndexDesc(Section section)
    {
        if (_sortedByZIndexDesc.TryGetValue(section.Id, out var cached))
            return cached;
        var sorted = Enumerable.Reverse(GetElementsByZIndexAsc(section)).ToList();
        _sortedByZIndexDesc[section.Id] = sorted;
        return sorted;
    }

    /// <summary>Vrátí elementy sekce seřazené vzestupně podle ZIndex (pro rendering).</summary>
    public List<TemplateElement> GetElementsByZIndexAsc(Section section)
    {
        if (_sortedByZIndexAsc.TryGetValue(section.Id, out var cached))
            return cached;
        var sorted = section.Elements.OrderBy(e => e.ZIndex).ToList();
        _sortedByZIndexAsc[section.Id] = sorted;
        return sorted;
    }

    /// <summary>Invaliduje cache seřazených elementů - volat po každé změně.</summary>
    public void InvalidateElementCache()
    {
        _sortedByZIndexDesc.Clear();
        _sortedByZIndexAsc.Clear();
    }

    public void Execute(IDesignerCommand command)
    {
        command.Execute();
        UndoStack.Push(command);
        RedoStack.Clear();
        InvalidateElementCache();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = UndoStack.Pop();
        cmd.Undo();
        RedoStack.Push(cmd);
        InvalidateElementCache();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = RedoStack.Pop();
        cmd.Execute();
        UndoStack.Push(cmd);
        InvalidateElementCache();
    }
}
