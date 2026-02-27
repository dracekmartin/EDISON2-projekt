using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class LineElement : TemplateElement
{
    public LineOrientation Orientation { get; set; } = LineOrientation.Horizontal;

    /// <summary>Barva čáry ve formátu #AARRGGBB.</summary>
    public string Color { get; set; } = "#FF000000";

    /// <summary>Tloušťka čáry v mm.</summary>
    public float StrokeWidth { get; set; } = 0.5f;

    public LineStyle Style { get; set; } = LineStyle.Solid;

    [JsonIgnore]
    public SKColor SkColor => SKColor.Parse(Color);

    public override void Render(RenderContext ctx)
    {
        // TODO: implementovat renderování čáry
    }

    public override TemplateElement Clone() => new LineElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        Orientation = Orientation,
        Color = Color,
        StrokeWidth = StrokeWidth,
        Style = Style
    };
}

public enum LineOrientation { Horizontal, Vertical }
public enum LineStyle { Solid, Dashed, Dotted }
