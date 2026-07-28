using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Univerzální styl textu - znovupoužitelný kdekoliv kde se vyskytuje text.
/// Nullable properties = "zdědí od rodiče" (element default, column default, ...).
/// CSS-like cascade: TextStyle.Resolve(parent) → ResolvedTextStyle.
/// </summary>
public class TextStyle
{
    // ── Font ──────────────────────────────────────────────────────────────────

    public string? FontFamily { get; set; }
    public float? FontSize { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    /// <summary>Váha písma (řez) 100–900. Null = odvodit z <see cref="Bold"/> (400/700). Přebíjí Bold, když je zadaná.</summary>
    public int? FontWeight { get; set; }
    public bool? Underline { get; set; }
    public bool? Strikethrough { get; set; }

    // ── Barvy ─────────────────────────────────────────────────────────────────

    /// <summary>Barva textu (#AARRGGBB). Null = zdědí od rodiče.</summary>
    public string? TextColor { get; set; }

    /// <summary>Barva pozadí textu (#AARRGGBB). Null = zdědí od rodiče (typicky průhledná).</summary>
    public string? BackgroundColor { get; set; }

    // ── Zarovnání ─────────────────────────────────────────────────────────────

    public TextHorizontalAlignment? HAlign { get; set; }
    public TextVerticalAlignment? VAlign { get; set; }

    // ── Spacing ───────────────────────────────────────────────────────────────

    public float? PaddingTop { get; set; }
    public float? PaddingRight { get; set; }
    public float? PaddingBottom { get; set; }
    public float? PaddingLeft { get; set; }

    /// <summary>Řádkování jako násobek velikosti fontu (1.2 = 120%).</summary>
    public float? LineSpacing { get; set; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    [JsonIgnore]
    public bool IsEmpty =>
        FontFamily is null && FontSize is null && Bold is null
        && Italic is null && FontWeight is null && Underline is null && Strikethrough is null
        && TextColor is null && BackgroundColor is null
        && HAlign is null && VAlign is null
        && PaddingTop is null && PaddingRight is null
        && PaddingBottom is null && PaddingLeft is null
        && LineSpacing is null;

    /// <summary>
    /// CSS-like cascade: vyřeší nullable hodnoty proti rodiči.
    /// Vrátí ResolvedTextStyle se všemi hodnotami guaranteed non-null.
    /// </summary>
    public ResolvedTextStyle Resolve(ResolvedTextStyle parent = default)
    {
        if (parent.FontFamily is null) parent = ResolvedTextStyle.Default;

        return new ResolvedTextStyle
        {
            // Trim - uživatel může do free-text font pole napsat mezeru navíc / jiný case;
            // SKTypeface.FromFamilyName je case-insensitive, ale mezery na okrajích by tiše spadly
            // na default font. Ořez to udělá shovívavé.
            FontFamily = (FontFamily ?? parent.FontFamily).Trim(),
            FontSize = FontSize ?? parent.FontSize,
            Bold = Bold ?? parent.Bold,
            Italic = Italic ?? parent.Italic,
            FontWeight = FontWeight ?? parent.FontWeight,
            Underline = Underline ?? parent.Underline,
            Strikethrough = Strikethrough ?? parent.Strikethrough,
            TextColor = TextColor ?? parent.TextColor,
            BackgroundColor = BackgroundColor ?? parent.BackgroundColor,
            HAlign = HAlign ?? parent.HAlign,
            VAlign = VAlign ?? parent.VAlign,
            PaddingTop = PaddingTop ?? parent.PaddingTop,
            PaddingRight = PaddingRight ?? parent.PaddingRight,
            PaddingBottom = PaddingBottom ?? parent.PaddingBottom,
            PaddingLeft = PaddingLeft ?? parent.PaddingLeft,
            LineSpacing = LineSpacing ?? parent.LineSpacing,
        };
    }

    public TextStyle Clone() => (TextStyle)MemberwiseClone();

    /// <summary>Vytvoří TextStyle s výchozími hodnotami pro hlavní text elementu.
    /// FontFamily záměrně null → nový text dědí výchozí font šablony (DocumentDefaults), fallback Arial.</summary>
    public static TextStyle CreateDefault() => new()
    {
        FontSize = 10f,
        Bold = false,
        Italic = false,
        Underline = false,
        Strikethrough = false,
        TextColor = "#FF000000",
        BackgroundColor = "#00FFFFFF",
        HAlign = TextHorizontalAlignment.Center,
        VAlign = TextVerticalAlignment.Middle,
        LineSpacing = 1.2f,
    };
}

/// <summary>
/// Vyřešený styl textu - všechny hodnoty non-null, připravený pro SkiaSharp rendering.
/// Vytváří se přes TextStyle.Resolve(parent).
/// </summary>
public struct ResolvedTextStyle
{
    public string FontFamily { get; init; }
    public float FontSize { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    /// <summary>Váha písma (řez) 100–900. 0 = auto (odvodí se z <see cref="Bold"/>).</summary>
    public int FontWeight { get; init; }
    public bool Underline { get; init; }
    public bool Strikethrough { get; init; }
    public string TextColor { get; init; }
    public string BackgroundColor { get; init; }
    public TextHorizontalAlignment HAlign { get; init; }
    public TextVerticalAlignment VAlign { get; init; }
    public float PaddingTop { get; init; }
    public float PaddingRight { get; init; }
    public float PaddingBottom { get; init; }
    public float PaddingLeft { get; init; }
    public float LineSpacing { get; init; }

    /// <summary>Konverze bodů (pt) na milimetry: 1pt = 25.4/72 mm.</summary>
    public const float PtToMm = 0.3528f;

    // Kořen kaskády - přes DocumentDefaults.Apply se sem propíše výchozí styl šablony (font…).
    public static ResolvedTextStyle Default => DocumentDefaults.Apply(new()
    {
        FontFamily = "Arial",
        FontSize = 10f,
        Bold = false,
        Italic = false,
        Underline = false,
        Strikethrough = false,
        TextColor = "#FF000000",
        BackgroundColor = "#00FFFFFF",
        HAlign = TextHorizontalAlignment.Left,
        VAlign = TextVerticalAlignment.Top,
        PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0, PaddingLeft = 0,
        LineSpacing = 1.2f,
    });

    // ── SkiaSharp helpers ─────────────────────────────────────────────────────

    /// <summary>SKFontStyle z řezu: explicitní <see cref="FontWeight"/> (100–900) přebíjí Bold;
    /// 0 = odvodit z Bold (400/700). Sklon z Italic. Šířka normální (Condensed řešíme rodinou).</summary>
    public readonly SKFontStyle ToSKFontStyle()
    {
        int weight = FontWeight > 0 ? FontWeight
            : (Bold ? (int)SKFontStyleWeight.Bold : (int)SKFontStyleWeight.Normal);
        var slant = Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, slant);
    }

    /// <summary>Vytvoří SKFont. Volající musí uvolnit (dispose).</summary>
    public readonly SKFont CreateFont(float scale = 1f, float mmToPx = 1f)
    {
        float sizePx = FontSize * PtToMm * scale * mmToPx;
        var typeface = FontResolver.Resolve(FontFamily, ToSKFontStyle());
        return new SKFont(typeface, sizePx);
    }

    /// <summary>Vytvoří SKPaint pro text. Volající musí uvolnit (dispose).</summary>
    public readonly SKPaint CreateTextPaint()
    {
        return new SKPaint { Color = SKColor.Parse(TextColor), IsAntialias = true };
    }

    /// <summary>Vytvoří SKPaint pro pozadí. Vrátí null pokud je pozadí průhledné.</summary>
    public readonly SKPaint? CreateBgPaint()
    {
        var color = SKColor.Parse(BackgroundColor);
        if (color.Alpha == 0) return null;
        return new SKPaint { Color = color, Style = SKPaintStyle.Fill };
    }

    public readonly SKColor ParsedTextColor => SKColor.Parse(TextColor);
    public readonly SKColor ParsedBackgroundColor => SKColor.Parse(BackgroundColor);

    /// <summary>
    /// Nakreslí podtržení a/nebo přeškrtnutí pod/přes právě vykreslený text.
    /// Volat ihned po DrawText se stejnými xPx, yBaseline (= baseline textu).
    /// textWidth = font.MeasureText(text).
    /// </summary>
    public readonly void DrawTextDecorations(SKCanvas canvas, float xPx, float yBaseline, float textWidth, SKFont font, SKPaint textPaint)
    {
        if (!Underline && !Strikethrough) return;

        var metrics = font.Metrics;

        if (Underline)
        {
            // UnderlinePosition je kladné = pod účarím (v SkiaSharp y jde dolů)
            float lineY = yBaseline + (metrics.UnderlinePosition ?? (font.Size * 0.12f));
            float thickness = Math.Max(1f, metrics.UnderlineThickness ?? (font.Size * 0.07f));
            using var p = new SKPaint { Color = textPaint.Color, StrokeWidth = thickness, IsAntialias = false };
            canvas.DrawLine(xPx, lineY, xPx + textWidth, lineY, p);
        }

        if (Strikethrough)
        {
            // StrikeoutPosition je záporné = nad účarím
            float lineY = yBaseline + (metrics.StrikeoutPosition ?? -(font.Size * 0.35f));
            float thickness = Math.Max(1f, metrics.StrikeoutThickness ?? (font.Size * 0.07f));
            using var p = new SKPaint { Color = textPaint.Color, StrokeWidth = thickness, IsAntialias = false };
            canvas.DrawLine(xPx, lineY, xPx + textWidth, lineY, p);
        }
    }
}
