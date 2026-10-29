using System.Text.Json.Serialization;
using SkiaSharp;

namespace TemplateDesigner.Models.Elements;

public class ImageElement : TemplateElement
{
    /// <summary>Max velikost Base64 dat v bytech (~5 MB).</summary>
    public const int MaxImageBytes = 5 * 1024 * 1024;
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

    // ─── Cache dekódovaného obrázku ─────────────────────────────────────────
    // Base64 se dekóduje jednou per obsah, ne při každém framu (až 5 MB dat).
    // Klíč = reference stringu: ImageBase64 se vždy nahrazuje celý (string je immutable),
    // takže nové přiřazení i načtení šablony ze JSON dají novou referenci = invalidaci.

    [JsonIgnore] private readonly Rendering.CachedBase64Image _cache = new();

    /// <summary>Počet skutečných dekódování - testovací sonda, že cache drží.</summary>
    [JsonIgnore] internal int DecodeCount => _cache.DecodeCount;

    /// <summary>
    /// Dekódovaný obrázek z <see cref="ImageBase64"/>, nebo null (chybí / poškozená data).
    /// Neúspěch se cachuje taky - jinak by se neplatná data zkoušela dekódovat při každém framu.
    /// </summary>
    private SKImage? GetDecodedImage() => _cache.Get(ImageBase64);

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;
        var rect = ctx.ToCanvasRect(X, Y, Width, Height ?? EffectiveDesignerHeight);

        // Pozadí elementu (letterbox mezery u Fit mode) kreslí base RenderBackgroundPass před obsahem.

        var image = GetDecodedImage();
        if (image != null)
        {
            using (var paint = new SKPaint { IsAntialias = true })
            {
                // Linear filter + mipmaps = kvalitní škálování nahoru i dolů (default by byl nearest).
                var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                DrawImageWithScaleMode(ctx.Canvas, image, rect, sampling, paint, ScaleMode);
            }
        }
        else if (ctx.JeNavrh)
        {
            // Placeholder: rámeček s křížem - jen v návrháři, do tisku nepatří
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

    /// <summary>
    /// Vykreslí obrázek do rect podle zvoleného režimu škálování:
    /// Fit = vejde se celý (mezery), Fill = vyplní (ořízne), Stretch = roztáhne (deformuje), None = originální velikost.
    /// </summary>
    internal static void DrawImageWithScaleMode(
        SKCanvas canvas, SKImage image, SKRect dest,
        SKSamplingOptions sampling, SKPaint paint, ImageScaleMode mode)
    {
        float imgW = image.Width, imgH = image.Height;
        if (imgW <= 0 || imgH <= 0 || dest.Width <= 0 || dest.Height <= 0) return;

        float sx = dest.Width / imgW, sy = dest.Height / imgH;
        SKRect target = mode switch
        {
            ImageScaleMode.Fit     => Centered(dest, imgW, imgH, MathF.Min(sx, sy)),
            ImageScaleMode.Fill    => Centered(dest, imgW, imgH, MathF.Max(sx, sy)),
            ImageScaleMode.Stretch => dest,
            ImageScaleMode.None    => SKRect.Create(dest.Left, dest.Top, imgW, imgH),
            _ => dest,
        };

        // Fill a None můžou přesáhnout dest → clip na element rect, ať nevylezou ven.
        if (mode is ImageScaleMode.Fill or ImageScaleMode.None)
        {
            canvas.Save();
            canvas.ClipRect(dest);
            canvas.DrawImage(image, target, sampling, paint);
            canvas.Restore();
        }
        else
        {
            canvas.DrawImage(image, target, sampling, paint);
        }
    }

    private static SKRect Centered(SKRect dest, float imgW, float imgH, float scale)
    {
        float w = imgW * scale, h = imgH * scale;
        return new SKRect(dest.MidX - w / 2f, dest.MidY - h / 2f, dest.MidX + w / 2f, dest.MidY + h / 2f);
    }

    /// <summary>
    /// Vrátí poměr stran obrázku (W/H) pro „Přizpůsobit poměru" akci.
    /// Null = obrázek není načten nebo je poškozený.
    /// </summary>
    public float? GetImageAspectRatio()
    {
        var image = GetDecodedImage();
        if (image is null || image.Width <= 0 || image.Height <= 0) return null;
        return (float)image.Width / image.Height;
    }

    /// <summary>Nastaví obrázek z byte[] s automatickým downscale pokud přesáhne limit.</summary>
    public static string? EncodeAndResize(byte[] data)
    {
        if (data.Length <= MaxImageBytes)
            return Convert.ToBase64String(data);

        // Downscale: zmenšit rozlišení dokud se vejde do limitu
        using var original = SKBitmap.Decode(data);
        if (original is null) return null;

        float scale = 1f;
        for (int i = 0; i < 5; i++)
        {
            scale *= 0.7f;
            int w = Math.Max(1, (int)(original.Width * scale));
            int h = Math.Max(1, (int)(original.Height * scale));
            using var resized = original.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized is null) continue;
            using var image = SKImage.FromBitmap(resized);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
            var bytes = encoded.ToArray();
            if (bytes.Length <= MaxImageBytes)
                return Convert.ToBase64String(bytes);
        }
        return null; // nedá se zmenšit dost
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ButtonPropertyDescriptor { Label = "Obrázek", Group = "Obrázek", ButtonLabel = "Nahrát obrázek…", ActionId = ButtonPropertyDescriptor.ActionUploadImage },
        new ButtonPropertyDescriptor { Label = "Poměr stran", Group = "Obrázek", ButtonLabel = "Přizpůsobit poměru obrázku", ActionId = ButtonPropertyDescriptor.ActionMatchImageAspect },
        new StringPropertyDescriptor { Label = "Datové pole", Group = "Obrázek",
            Get = () => DataField ?? "",
            Set = v => DataField = string.IsNullOrWhiteSpace(v) ? null : v },
        new EnumPropertyDescriptor   { Label = "Přizpůsobení velikosti", Group = "Obrázek", Get = () => ScaleMode, Set = v => ScaleMode = (ImageScaleMode)v,
            Options = [
                ("Vejít celý",         (object)ImageScaleMode.Fit),
                ("Vyplnit",           (object)ImageScaleMode.Fill),
                ("Roztáhnout",         (object)ImageScaleMode.Stretch),
                ("Bez úpravy",         (object)ImageScaleMode.None),
            ] },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new ImageElement
        {
            ImageBase64 = ImageBase64,
            DataField = DataField,
            ScaleMode = ScaleMode
        };
        CopyBaseTo(clone);
        return clone;
    }
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
