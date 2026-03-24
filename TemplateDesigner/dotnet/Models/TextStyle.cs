using System.Text.Json.Serialization;
using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Univerzální styl textu — znovupoužitelný kdekoliv kde se vyskytuje text.
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
    public bool? Underline { get; set; }

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
        && Italic is null && Underline is null
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
            FontFamily = FontFamily ?? parent.FontFamily,
            FontSize = FontSize ?? parent.FontSize,
            Bold = Bold ?? parent.Bold,
            Italic = Italic ?? parent.Italic,
            Underline = Underline ?? parent.Underline,
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

    /// <summary>Vytvoří TextStyle s výchozími hodnotami pro hlavní text elementu.</summary>
    public static TextStyle CreateDefault() => new()
    {
        FontFamily = "Arial",
        FontSize = 10f,
        Bold = false,
        Italic = false,
        Underline = false,
        TextColor = "#FF000000",
        BackgroundColor = "#00FFFFFF",
        HAlign = TextHorizontalAlignment.Left,
        VAlign = TextVerticalAlignment.Top,
        LineSpacing = 1.2f,
    };
}

/// <summary>
/// Vyřešený styl textu — všechny hodnoty non-null, připravený pro SkiaSharp rendering.
/// Vytváří se přes TextStyle.Resolve(parent).
/// </summary>
public struct ResolvedTextStyle
{
    public string FontFamily { get; init; }
    public float FontSize { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
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

    public static ResolvedTextStyle Default => new()
    {
        FontFamily = "Arial",
        FontSize = 10f,
        Bold = false,
        Italic = false,
        Underline = false,
        TextColor = "#FF000000",
        BackgroundColor = "#00FFFFFF",
        HAlign = TextHorizontalAlignment.Left,
        VAlign = TextVerticalAlignment.Top,
        PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0, PaddingLeft = 0,
        LineSpacing = 1.2f,
    };

    // ── SkiaSharp helpers ─────────────────────────────────────────────────────

    public readonly SKFontStyle ToSKFontStyle() => (Bold, Italic) switch
    {
        (true, true) => SKFontStyle.BoldItalic,
        (true, false) => SKFontStyle.Bold,
        (false, true) => SKFontStyle.Italic,
        _ => SKFontStyle.Normal,
    };

    /// <summary>Vytvoří SKFont. Volající musí uvolnit (dispose).</summary>
    public readonly SKFont CreateFont(float scale = 1f, float mmToPx = 1f)
    {
        float sizePx = FontSize * PtToMm * scale * mmToPx;
        var typeface = SKTypeface.FromFamilyName(FontFamily, ToSKFontStyle()) ?? SKTypeface.Default;
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
}
