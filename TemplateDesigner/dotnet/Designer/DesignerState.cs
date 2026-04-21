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
    public Template CurrentTemplate { get; set; } = new();

    /// <summary>Aktuálně aktivní sekce (do které se přidávají elementy). Null = žádná.</summary>
    public Section? ActiveSection { get; set; }

    public List<TemplateElement> SelectedElements { get; } = [];

    private float _zoom = 1.0f;
    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, 0.1f, 5.0f);
    }

    public bool ShowGrid { get; set; } = false;
    public bool SnapToGrid { get; set; } = false;
    public bool SnapToElements { get; set; } = true;
    public bool ShowSectionHeaders { get; set; } = true;
    public float GridSize { get; set; } = 5f; // mm
    public List<GapIndicator> ActiveGapIndicators { get; } = [];

    /// <summary>Elementy jejichž boundary se zvýrazní během snapu (cílové elementy).</summary>
    public List<TemplateElement> SnapTargetElements { get; } = [];

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

    /// <summary>Vrátí elementy sekce seřazené sestupně podle ZIndex (pro hit testing).</summary>
    public List<TemplateElement> GetElementsByZIndexDesc(Section section)
    {
        if (_sortedByZIndexDesc.TryGetValue(section.Id, out var cached))
            return cached;
        var sorted = section.Elements.OrderByDescending(e => e.ZIndex).ToList();
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

    /// <summary>Invaliduje cache seřazených elementů — volat po každé změně.</summary>
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
