using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class RectangleElement : TemplateElement
{
    /// <summary>Barva okraje ve formátu #AARRGGBB.</summary>
    public string StrokeColor { get; set; } = "#FF000000";

    /// <summary>Tloušťka okraje v mm. 0 = bez okraje.</summary>
    public float StrokeWidth { get; set; } = 0.5f;

    [JsonIgnore]
    public SKColor SkStrokeColor => SKColor.Parse(StrokeColor);

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;
        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);

        // Výplň (BackgroundColor) kreslí base RenderBackgroundPass před obsahem - respektuje rohy.
        // Zde jen okraj tvaru; DrawElementShape aplikuje CornerStyle (ostré/zaoblené/zkosené).
        if (StrokeWidth > 0 && SkStrokeColor.Alpha > 0)
        {
            using var stroke = new SKPaint { Color = SkStrokeColor, IsStroke = true, StrokeWidth = Math.Max(0.5f, ctx.Px(StrokeWidth)), IsAntialias = true };
            DrawElementShape(ctx, rect, stroke);
        }
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);   // X/Y/Width/Height/BorderWidth + CornerSize
        StrokeWidth *= scale;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ColorPropertyDescriptor { Label = "Výplň",       Group = "Vzhled", Get = () => BackgroundColor, Set = v => BackgroundColor = v },
        new ColorPropertyDescriptor { Label = "Barva okraje", Group = "Vzhled", Get = () => StrokeColor,  Set = v => StrokeColor = v },
        new FloatPropertyDescriptor { Label = "Tloušťka okraje",  Group = "Vzhled", Get = () => StrokeWidth,  Set = v => StrokeWidth = v,  Min = 0, Max = 10, Unit = "mm" },
        // Rohy (Ostré/Zaoblené/Zkosené + velikost) jsou společné pro všechny elementy → GetPositionDescriptors.

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new RectangleElement
        {
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth
        };
        CopyBaseTo(clone);   // vč. CornerStyle/CornerSize
        return clone;
    }
}
