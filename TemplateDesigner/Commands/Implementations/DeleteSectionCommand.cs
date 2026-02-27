using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Smaže sekci ze šablony. Undo = obnovení na původní index.
/// Poznámka: stav ActiveSection a SelectedElements spravuje MainWindow,
/// ne tento příkaz — command pouze mění datový model.
/// </summary>
public class DeleteSectionCommand : IDesignerCommand
{
    private readonly Template _template;
    private readonly Section  _section;
    private          int      _originalIndex;

    public string Description => "Smazat sekci";

    public DeleteSectionCommand(Template template, Section section)
    {
        _template = template;
        _section  = section;
    }

    public void Execute()
    {
        _originalIndex = _template.Sections.IndexOf(_section);
        _template.Sections.Remove(_section);
    }

    public void Undo() => _template.Sections.Insert(
        Math.Clamp(_originalIndex, 0, _template.Sections.Count), _section);
}
