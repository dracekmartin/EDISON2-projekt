using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Přesune sekci na cílový index (pro drag & drop reorder).
/// Undo obnoví původní index zaznamenaný při Execute.
/// </summary>
public class ReorderSectionCommand : IDesignerCommand
{
    private readonly Template _template;
    private readonly Section  _section;
    private readonly int      _fromIndex;
    private readonly int      _toIndex;

    public string Description => "Přeřadit sekci";

    public ReorderSectionCommand(Template template, Section section, int fromIndex, int toIndex)
    {
        _template  = template;
        _section   = section;
        _fromIndex = fromIndex;
        _toIndex   = toIndex;
    }

    public void Execute() => MoveTo(_toIndex);
    public void Undo()    => MoveTo(_fromIndex);

    private void MoveTo(int targetIndex)
    {
        int current = _template.Sections.IndexOf(_section);
        if (current < 0) return;
        _template.Sections.RemoveAt(current);
        _template.Sections.Insert(Math.Clamp(targetIndex, 0, _template.Sections.Count), _section);
    }
}
