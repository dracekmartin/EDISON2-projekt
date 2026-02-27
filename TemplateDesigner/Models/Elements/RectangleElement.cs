using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class RectangleElement : TemplateElement
{
    /// <summary>Barva výplně ve formátu #AARRGGBB. Průhledná = "#00FFFFFF".</summary>
    public string FillColor { get; set; } = "#00FFFFFF";

    /// <summary>Barva okraje ve formátu #AARRGGBB.</summary>
    public string StrokeColor { get; set; } = "#FF000000";

    /// <summary>Tloušťka okraje v mm. 0 = bez okraje.</summary>
    public float StrokeWidth { get; set; } = 0.5f;

    /// <summary>Zaoblení rohů v mm.</summary>
    public float CornerRadius { get; set; } = 0f;

    [JsonIgnore]
    public SKColor SkFillColor => SKColor.Parse(FillColor);

    [JsonIgnore]
    public SKColor SkStrokeColor => SKColor.Parse(StrokeColor);

    public override void Render(RenderContext ctx)
    {
        // TODO: implementovat renderování obdélníku
    }

    public override TemplateElement Clone() => new RectangleElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        FillColor = FillColor,
        StrokeColor = StrokeColor,
        StrokeWidth = StrokeWidth,
        CornerRadius = CornerRadius
    };
}
