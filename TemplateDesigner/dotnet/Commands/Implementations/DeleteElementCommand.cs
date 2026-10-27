using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Smaže element ze sekce. Undo = obnovení elementu na původní index.
/// Pokud je smazáno více elementů najednou, použij CompositeCommand.
/// </summary>
public class DeleteElementCommand : IDesignerCommand
{
    private readonly Section         _section;
    private readonly TemplateElement _element;
    private          int             _originalIndex;

    public string Description => "Smazat prvek";

    public DeleteElementCommand(Section section, TemplateElement element)
    {
        _section = section;
        _element = element;
    }

    public void Execute()
    {
        _originalIndex = _section.Elements.IndexOf(_element);
        _section.Elements.Remove(_element);
    }

    public void Undo() => _section.Elements.Insert(
        Math.Clamp(_originalIndex, 0, _section.Elements.Count), _element);
}
