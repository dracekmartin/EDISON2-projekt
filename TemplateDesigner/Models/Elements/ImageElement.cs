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
        // TODO: implementovat renderování obrázku
    }

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
