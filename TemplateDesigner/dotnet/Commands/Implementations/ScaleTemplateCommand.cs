using TemplateDesigner.Models;
using static TemplateDesigner.Models.MmMath;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Proporcionálně přeškáluje celou šablonu — stránku, sekce, elementy.
/// Undo aplikuje inverzní scale (1/scale).
/// </summary>
public class ScaleTemplateCommand : IDesignerCommand
{
    private readonly Template _template;
    private readonly float _scale;

    public string Description { get; }

    public ScaleTemplateCommand(Template template, float scale)
    {
        _template = template;
        _scale = scale;
        int percent = (int)Math.Round(scale * 100);
        Description = $"Škálování šablony na {percent} %";
    }

    public void Execute() => Apply(_scale);
    public void Undo() => Apply(1f / _scale);

    private void Apply(float s)
    {
        // Stránka
        var ps = _template.PageSize;
        ps.Width  = Round(ps.Width  * s);
        ps.Height = Round(ps.Height * s);
        ps.MarginTop    = Round(ps.MarginTop    * s);
        ps.MarginBottom = Round(ps.MarginBottom * s);
        ps.MarginLeft   = Round(ps.MarginLeft   * s);
        ps.MarginRight  = Round(ps.MarginRight  * s);

        // Sekce + elementy
        foreach (var section in _template.Sections)
        {
            if (section.DesignerHeight is not null)
                section.DesignerHeight = Round(section.DesignerHeight.Value * s);
            if (section.RowHeight is not null)
                section.RowHeight = Round(section.RowHeight.Value * s);
            if (section.BorderWidth > 0)
                section.BorderWidth = Round(section.BorderWidth * s);

            foreach (var element in section.Elements)
                element.ApplyScale(s);
        }
    }
}
