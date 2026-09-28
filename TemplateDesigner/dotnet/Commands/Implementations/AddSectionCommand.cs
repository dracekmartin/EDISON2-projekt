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

    private readonly int? _index;

    /// <param name="index">Kam sekci vložit; null = na konec šablony.</param>
    public AddSectionCommand(Template template, Section section, int? index = null)
    {
        _template = template;
        _section  = section;
        _index    = index;
    }

    public void Execute()
    {
        if (_index is int i && i >= 0 && i < _template.Sections.Count) _template.Sections.Insert(i, _section);
        else _template.Sections.Add(_section);
    }

    public void Undo() => _template.Sections.Remove(_section);
}
