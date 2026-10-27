using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Přidá element do sekce. Undo = odebrání elementu.
/// </summary>
public class AddElementCommand : IDesignerCommand
{
    private readonly Section         _section;
    private readonly TemplateElement _element;

    public string Description => "Přidat prvek";

    public AddElementCommand(Section section, TemplateElement element)
    {
        _section = section;
        _element = element;
    }

    public void Execute() => _section.Elements.Add(_element);

    public void Undo() => _section.Elements.Remove(_element);
}
