using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>Přesun jednoho elementu - undoable.</summary>
public class MoveElementCommand : IDesignerCommand
{
    private readonly TemplateElement _element;
    private readonly float _fromX, _fromY;
    private readonly float _toX,   _toY;

    public string Description => "Přesun elementu";

    /// <param name="element">Element jehož pozice se mění.</param>
    /// <param name="fromX">Původní X (mm).</param>
    /// <param name="fromY">Původní Y (mm).</param>
    /// <param name="toX">Nová X (mm) - element už je na nové pozici z live dragu.</param>
    /// <param name="toY">Nová Y (mm).</param>
    public MoveElementCommand(TemplateElement element, float fromX, float fromY, float toX, float toY)
    {
        _element = element;
        _fromX = fromX; _fromY = fromY;
        _toX   = toX;   _toY   = toY;
    }

    public void Execute() { _element.X = _toX;   _element.Y = _toY; }
    public void Undo()    { _element.X = _fromX; _element.Y = _fromY; }
}
