using TemplateDesigner.Models;

namespace TemplateDesigner.Commands.Implementations;

/// <summary>
/// Proporcionálně přeškáluje celou šablonu - stránku, sekce, elementy.
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
        TemplateScaling.ScalePage(_template.PageSize, s);
        TemplateScaling.ScaleContent(_template, s);
    }
}
