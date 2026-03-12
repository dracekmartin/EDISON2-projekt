using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Undoable rozeskupení elementů — nastaví GroupId na null.
/// </summary>
public class UngroupCommand : IDesignerCommand
{
    private readonly List<TemplateElement> _elements;
    private readonly Dictionary<TemplateElement, string?> _previousGroupIds;

    public string Description => "Rozeskupit";

    public UngroupCommand(IEnumerable<TemplateElement> elements)
    {
        _elements = elements.ToList();
        _previousGroupIds = _elements.ToDictionary(e => e, e => e.GroupId);
    }

    public void Execute()
    {
        foreach (var el in _elements)
            el.GroupId = null;
    }

    public void Undo()
    {
        foreach (var el in _elements)
            el.GroupId = _previousGroupIds[el];
    }
}
