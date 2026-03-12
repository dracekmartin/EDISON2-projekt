using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Undoable seskupení elementů — přiřadí společný GroupId.
/// </summary>
public class GroupCommand : IDesignerCommand
{
    private readonly List<TemplateElement> _elements;
    private readonly string _newGroupId;
    private readonly Dictionary<TemplateElement, string?> _previousGroupIds;

    public string Description => "Seskupit elementy";

    public GroupCommand(IEnumerable<TemplateElement> elements, string groupId)
    {
        _elements = elements.ToList();
        _newGroupId = groupId;
        _previousGroupIds = _elements.ToDictionary(e => e, e => e.GroupId);
    }

    public void Execute()
    {
        foreach (var el in _elements)
            el.GroupId = _newGroupId;
    }

    public void Undo()
    {
        foreach (var el in _elements)
            el.GroupId = _previousGroupIds[el];
    }
}
