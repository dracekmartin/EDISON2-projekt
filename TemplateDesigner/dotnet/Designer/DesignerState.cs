using TemplateDesigner.Commands;
using TemplateDesigner.Models;

namespace TemplateDesigner.Designer;

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
    public float GridSize { get; set; } = 5f; // mm
    public List<GapIndicator> ActiveGapIndicators { get; } = [];

    /// <summary>Elementy jejichž boundary se zvýrazní během snapu (cílové elementy).</summary>
    public List<TemplateElement> SnapTargetElements { get; } = [];

    public Stack<IDesignerCommand> UndoStack { get; } = new();
    public Stack<IDesignerCommand> RedoStack { get; } = new();

    public bool CanUndo => UndoStack.Count > 0;
    public bool CanRedo => RedoStack.Count > 0;

    public void Execute(IDesignerCommand command)
    {
        command.Execute();
        UndoStack.Push(command);
        RedoStack.Clear();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = UndoStack.Pop();
        cmd.Undo();
        RedoStack.Push(cmd);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = RedoStack.Pop();
        cmd.Execute();
        UndoStack.Push(cmd);
    }
}
