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
        using var paint = new SKPaint
        {
            Color = SkColor,
            StrokeWidth = Math.Max(0.5f, ctx.Px(StrokeWidth)),
            IsAntialias = true
        };

        if (Style != LineStyle.Solid)
        {
            float dash = ctx.Px(Style == LineStyle.Dashed ? 3f : 1f);
            float gap  = ctx.Px(Style == LineStyle.Dashed ? 2f : 1.5f);
            paint.PathEffect = SKPathEffect.CreateDash([dash, gap], 0);
        }

        if (Orientation == LineOrientation.Horizontal)
        {
            float midY = Y + (Height ?? 0) / 2f;
            ctx.Canvas.DrawLine(ctx.ToCanvas(X, midY), ctx.ToCanvas(X + Width, midY), paint);
        }
        else
        {
            float lineH = Height ?? Width;
            float midX = X + Width / 2f;
            ctx.Canvas.DrawLine(ctx.ToCanvas(midX, Y), ctx.ToCanvas(midX, Y + lineH), paint);
        }
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new EnumPropertyDescriptor  { Label = "Orientace", Group = "Čára", Get = () => Orientation, Set = v => Orientation = (LineOrientation)v,
            Options = [("Vodorovná", (object)LineOrientation.Horizontal), ("Svislá", (object)LineOrientation.Vertical)] },
        new ColorPropertyDescriptor { Label = "Barva",     Group = "Čára", Get = () => Color,       Set = v => Color = v },
        new FloatPropertyDescriptor { Label = "Tloušťka",  Group = "Čára", Get = () => StrokeWidth, Set = v => StrokeWidth = v, Min = 0.1f, Max = 10, Unit = "mm" },
        new EnumPropertyDescriptor  { Label = "Styl",      Group = "Čára", Get = () => Style,       Set = v => Style = (LineStyle)v,
            Options = [("Plná", (object)LineStyle.Solid), ("Čárkovaná", (object)LineStyle.Dashed), ("Tečkovaná", (object)LineStyle.Dotted)] },

        .. GetPositionDescriptors()
    ];

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
