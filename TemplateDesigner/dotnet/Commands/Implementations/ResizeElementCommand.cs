using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>Změna velikosti (a případně polohy) elementu - undoable.</summary>
public class ResizeElementCommand : IDesignerCommand
{
    private readonly TemplateElement _element;
    private readonly float  _fromX, _fromY, _fromW;
    private readonly float? _fromH;
    private readonly float  _toX,   _toY,   _toW;
    private readonly float? _toH;

    public string Description => "Změna velikosti prvku";

    public ResizeElementCommand(TemplateElement element,
        float fromX, float fromY, float fromW, float? fromH,
        float toX,   float toY,   float toW,  float? toH)
    {
        _element = element;
        _fromX = fromX; _fromY = fromY; _fromW = fromW; _fromH = fromH;
        _toX   = toX;   _toY   = toY;   _toW   = toW;   _toH   = toH;
    }

    public void Execute() { _element.X = _toX;   _element.Y = _toY;   _element.Width = _toW;   _element.Height = _toH; }
    public void Undo()    { _element.X = _fromX; _element.Y = _fromY; _element.Width = _fromW; _element.Height = _fromH; }
}
