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

    /// <summary>Délka čárky/tečky v mm. Null = výchozí (Dashed=3, Dotted=1).</summary>
    public float? CustomDashMm { get; set; }

    /// <summary>Délka mezery v mm. Null = výchozí (Dashed=2, Dotted=1.5).</summary>
    public float? CustomGapMm { get; set; }

    [JsonIgnore]
    public SKColor SkColor => SKColor.Parse(Color);

    /// <summary>Výchozí vzor čárky/mezery podle stylu.</summary>
    public static (float dash, float gap) GetDefaultDashPattern(LineStyle style) => style switch
    {
        LineStyle.Dashed => (3f, 2f),
        LineStyle.Dotted => (1f, 1.5f),
        _ => (0f, 0f)
    };

    private (float dash, float gap) DefaultDashPattern => GetDefaultDashPattern(Style);

    /// <summary>Efektivní délka čárky v mm (custom nebo výchozí).</summary>
    [JsonIgnore] public float EffectiveDashMm => CustomDashMm ?? DefaultDashPattern.dash;

    /// <summary>Efektivní délka mezery v mm (custom nebo výchozí).</summary>
    [JsonIgnore] public float EffectiveGapMm => CustomGapMm ?? DefaultDashPattern.gap;

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
            paint.PathEffect = SKPathEffect.CreateDash([ctx.Px(EffectiveDashMm), ctx.Px(EffectiveGapMm)], 0);
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
        new FloatPropertyDescriptor { Label = "Délka čárky", Group = "Čára", Get = () => EffectiveDashMm, Set = v => CustomDashMm = v, Min = 0.1f, Max = 20, Unit = "mm" },
        new FloatPropertyDescriptor { Label = "Délka mezery", Group = "Čára", Get = () => EffectiveGapMm, Set = v => CustomGapMm = v, Min = 0.1f, Max = 20, Unit = "mm" },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone() => new LineElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        GroupId = GroupId,
        Orientation = Orientation,
        Color = Color,
        StrokeWidth = StrokeWidth,
        Style = Style,
        CustomDashMm = CustomDashMm,
        CustomGapMm = CustomGapMm
    };
}

public enum LineOrientation { Horizontal, Vertical }
public enum LineStyle { Solid, Dashed, Dotted }
