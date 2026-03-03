using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Přesune sekci v seznamu o jednu pozici nahoru (-1) nebo dolů (+1).
/// </summary>
public class MoveSectionCommand : IDesignerCommand
{
    private readonly Template _template;
    private readonly Section  _section;
    private readonly int      _direction; // -1 = nahoru, +1 = dolů

    public string Description => _direction < 0 ? "Přesunout sekci nahoru" : "Přesunout sekci dolů";

    public MoveSectionCommand(Template template, Section section, int direction)
    {
        _template  = template;
        _section   = section;
        _direction = direction;
    }

    public void Execute() => Move(_direction);
    public void Undo()    => Move(-_direction);

    private void Move(int dir)
    {
        int idx    = _template.Sections.IndexOf(_section);
        int newIdx = Math.Clamp(idx + dir, 0, _template.Sections.Count - 1);
        if (idx == newIdx) return;
        _template.Sections.RemoveAt(idx);
        _template.Sections.Insert(newIdx, _section);
    }
}
