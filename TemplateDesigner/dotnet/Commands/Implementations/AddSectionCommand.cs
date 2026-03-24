using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Přidá sekci do šablony. Undo = odebrání sekce.
/// </summary>
public class AddSectionCommand : IDesignerCommand
{
    private readonly Template _template;
    private readonly Section  _section;

    public string Description => "Přidat sekci";

    public AddSectionCommand(Template template, Section section)
    {
        _template = template;
        _section  = section;
    }

    public void Execute() => _template.Sections.Add(_section);

    public void Undo() => _template.Sections.Remove(_section);
}
