using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

public class ImageElement : TemplateElement
{
    /// <summary>
    /// Statický obrázek jako Base64 string (PNG/JPG).
    /// Null pokud je obrázek data-bound přes DataField.
    /// </summary>
    public string? ImageBase64 { get; set; }

    /// <summary>
    /// Název datového pole pro obrázek z dat, např. "LOGO".
    /// Null pokud je obrázek statický (ImageBase64).
    /// </summary>
    public string? DataField { get; set; }

    public ImageScaleMode ScaleMode { get; set; } = ImageScaleMode.Fit;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;
        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);

        // Pokus o dekódování obrázku
        SKBitmap? bitmap = null;
        if (!string.IsNullOrEmpty(ImageBase64))
        {
            try { bitmap = SKBitmap.Decode(Convert.FromBase64String(ImageBase64)); }
            catch { /* neplatná data */ }
        }

        if (bitmap != null)
        {
            using (bitmap)
            using (var paint = new SKPaint { IsAntialias = true })
                ctx.Canvas.DrawBitmap(bitmap, rect, paint);
        }
        else
        {
            // Placeholder: rámeček s křížem
            using var border = new SKPaint { Color = new SKColor(0xFFAAAAAA), IsStroke = true, StrokeWidth = 1f };
            using var cross  = new SKPaint { Color = new SKColor(0xFFCCCCCC), StrokeWidth = 1f };
            ctx.Canvas.DrawRect(rect, border);
            ctx.Canvas.DrawLine(rect.Left, rect.Top, rect.Right, rect.Bottom, cross);
            ctx.Canvas.DrawLine(rect.Right, rect.Top, rect.Left, rect.Bottom, cross);

            string label = DataField is not null ? $"[{DataField}]" : "IMG";
            using var font  = new SKFont(SKTypeface.Default, Math.Min(rect.Height * 0.3f, 12f));
            using var paint = new SKPaint { Color = new SKColor(0xFF999999), IsAntialias = true };
            ctx.Canvas.DrawText(label, rect.MidX - (label.Length * 3f), rect.MidY + font.Size / 3f, font, paint);
        }
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Datové pole", Group = "Obrázek",
            Get = () => DataField ?? "",
            Set = v => DataField = string.IsNullOrWhiteSpace(v) ? null : v },
        new EnumPropertyDescriptor   { Label = "Škálování",   Group = "Obrázek", Get = () => ScaleMode, Set = v => ScaleMode = (ImageScaleMode)v,
            Options = [("Fit", (object)ImageScaleMode.Fit), ("Fill", (object)ImageScaleMode.Fill),
                       ("Stretch", (object)ImageScaleMode.Stretch), ("None", (object)ImageScaleMode.None)] },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone() => new ImageElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        ImageBase64 = ImageBase64,
        DataField = DataField,
        ScaleMode = ScaleMode
    };
}

public enum ImageScaleMode
{
    /// <summary>Vyplní celou plochu, ořízne přesah.</summary>
    Fill,
    /// <summary>Zachová poměr stran, vejde se celý obrázek.</summary>
    Fit,
    /// <summary>Roztáhne obrázek na celou plochu (deformuje).</summary>
    Stretch,
    /// <summary>Obrázek se nezměněný umístí do levého horního rohu.</summary>
    None
}
