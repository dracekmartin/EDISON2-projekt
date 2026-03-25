using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class RectangleElement : TemplateElement
{
    /// <summary>Barva okraje ve formátu #AARRGGBB.</summary>
    public string StrokeColor { get; set; } = "#FF000000";

    /// <summary>Tloušťka okraje v mm. 0 = bez okraje.</summary>
    public float StrokeWidth { get; set; } = 0.5f;

    /// <summary>Zaoblení rohů v mm.</summary>
    public float CornerRadius { get; set; } = 0f;

    [JsonIgnore]
    public SKColor SkStrokeColor => SKColor.Parse(StrokeColor);

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;
        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);
        float rx = ctx.Px(CornerRadius), ry = rx;

        if (SkBackgroundColor.Alpha > 0)
        {
            using var fill = new SKPaint { Color = SkBackgroundColor, IsAntialias = CornerRadius > 0 };
            if (CornerRadius > 0) ctx.Canvas.DrawRoundRect(rect, rx, ry, fill);
            else ctx.Canvas.DrawRect(rect, fill);
        }

        if (StrokeWidth > 0 && SkStrokeColor.Alpha > 0)
        {
            using var stroke = new SKPaint { Color = SkStrokeColor, IsStroke = true, StrokeWidth = Math.Max(0.5f, ctx.Px(StrokeWidth)), IsAntialias = true };
            if (CornerRadius > 0) ctx.Canvas.DrawRoundRect(rect, rx, ry, stroke);
            else ctx.Canvas.DrawRect(rect, stroke);
        }

    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        StrokeWidth *= scale;
        CornerRadius *= scale;
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ColorPropertyDescriptor { Label = "Výplň",       Group = "Vzhled", Get = () => BackgroundColor, Set = v => BackgroundColor = v },
        new ColorPropertyDescriptor { Label = "Barva okraje", Group = "Vzhled", Get = () => StrokeColor,  Set = v => StrokeColor = v },
        new FloatPropertyDescriptor { Label = "Tl. okraje",  Group = "Vzhled", Get = () => StrokeWidth,  Set = v => StrokeWidth = v,  Min = 0, Max = 10, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Zaoblení",    Group = "Vzhled", Get = () => CornerRadius, Set = v => CornerRadius = v, Min = 0, Max = 50, Unit = "mm" },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new RectangleElement
        {
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            CornerRadius = CornerRadius
        };
        CopyBaseTo(clone);
        return clone;
    }
}
