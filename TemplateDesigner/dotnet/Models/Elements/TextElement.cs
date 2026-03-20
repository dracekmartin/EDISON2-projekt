using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

public class TextElement : TemplateElement
{
    /// <summary>
    /// Text pro zobrazení. Může obsahovat placeholdery ve tvaru {NAZEV_POLE}.
    /// Print engine je za běhu nahradí reálnými daty.
    /// </summary>
    public string Text { get; set; } = "";

    public string FontFamily { get; set; } = "Arial";
    public float FontSize { get; set; } = 10f;
    public TextFontStyle FontStyle { get; set; } = TextFontStyle.Normal;
    public TextFontWeight FontWeight { get; set; } = TextFontWeight.Normal;
    public TextHorizontalAlignment HorizontalAlignment { get; set; } = TextHorizontalAlignment.Left;
    public TextVerticalAlignment VerticalAlignment { get; set; } = TextVerticalAlignment.Top;

    /// <summary>Barva textu ve formátu #AARRGGBB, např. "#FF000000" = černá.</summary>
    public string TextColor { get; set; } = "#FF000000";

    /// <summary>Barva pozadí ve formátu #AARRGGBB, např. "#00FFFFFF" = průhledná, "#FFFFFF00" = žlutá.</summary>
    public string BackgroundColor { get; set; } = "#00FFFFFF";

    public bool WordWrap { get; set; } = true;

    /// <summary>Řádkování jako násobek velikosti fontu (1.2 = 20% navíc). Použije se při WordWrap = true.</summary>
    public float LineSpacing { get; set; } = 1.2f;

    [JsonIgnore]
    public SKColor SkTextColor => SKColor.Parse(TextColor);

    [JsonIgnore]
    public SKColor SkBackgroundColor => SKColor.Parse(BackgroundColor);

    /// <summary>
    /// Když Height = null a WordWrap = true, výška se dopočítá z textu.
    /// Print engine spočítá z reálných dat; designer z raw textu (s placeholdery).
    /// </summary>
    protected override float DesignerPlaceholderHeight =>
        WordWrap ? CalculateTextHeight(Text) : 8f;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;
        var resolvedText = ctx.Data?.ResolvePlaceholders(Text) ?? Text;
        if (string.IsNullOrEmpty(resolvedText)) return;

        // Dynamická výška: Height ?? dopočítaná z textu (pokud WordWrap)
        float heightMm = Height ?? (WordWrap ? CalculateTextHeight(resolvedText) : EffectiveDesignerHeight);
        var rect = ctx.ToCanvasRect(X, Y, Width, heightMm);

        // Pozadí (pokud není průhledné)
        if (SkBackgroundColor.Alpha > 0)
        {
            using var bgPaint = new SKPaint { Color = SkBackgroundColor, Style = SKPaintStyle.Fill };
            ctx.Canvas.DrawRect(rect, bgPaint);
        }

        var typeface = SKTypeface.FromFamilyName(
            FontFamily,
            FontWeight == TextFontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            FontStyle == TextFontStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        // FontSize je v bodech (pt): 1pt = 0.3528mm → mm → px
        // V designeru (bez dat) omezit velikost fontu, aby placeholdery typu {NAZEV_POLE}
        // nepřetékaly — zobrazit čitelně místo ve skutečné velikosti.
        float effectiveFontSize = ctx.Data is null ? Math.Min(FontSize, 10f) : FontSize;
        float fontSizePx = ctx.Px(effectiveFontSize * 0.3528f);
        using var font = new SKFont(typeface, fontSizePx);
        using var paint = new SKPaint { Color = SkTextColor, IsAntialias = true };

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(rect);

        if (WordWrap)
        {
            float widthPx = ctx.Px(Width);
            var lines = TextWrapHelper.WrapText(resolvedText, font, widthPx);
            float lineHeightPx = fontSizePx * LineSpacing;

            // Vertikální zarovnání bloku řádků
            float totalTextHeight = lines.Count * lineHeightPx;
            float yStart = VerticalAlignment switch
            {
                TextVerticalAlignment.Middle => rect.Top + (rect.Height - totalTextHeight) / 2 + fontSizePx,
                TextVerticalAlignment.Bottom => rect.Bottom - totalTextHeight + fontSizePx,
                _ => rect.Top + fontSizePx
            };

            float yPx = yStart;
            foreach (var line in lines)
            {
                float xPx = HorizontalAlignment switch
                {
                    TextHorizontalAlignment.Center => rect.Left + (widthPx - font.MeasureText(line)) / 2,
                    TextHorizontalAlignment.Right => rect.Right - font.MeasureText(line),
                    _ => rect.Left + 1f
                };
                ctx.Canvas.DrawText(line, xPx, yPx, font, paint);
                yPx += lineHeightPx;
            }
        }
        else
        {
            // Jednořádkový režim — původní chování
            ctx.Canvas.DrawText(resolvedText, rect.Left + 1f, rect.Top + fontSizePx, font, paint);
        }

        ctx.Canvas.Restore();
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Text",         Group = "Obsah",     Get = () => Text,                Set = v => Text = v,                                   MultiLine = true },

        new StringPropertyDescriptor { Label = "Rodina písma", Group = "Font",      Get = () => FontFamily,          Set = v => FontFamily = v },
        new FloatPropertyDescriptor  { Label = "Velikost",      Group = "Font",      Get = () => FontSize,            Set = v => FontSize = v,                               Min = 4, Max = 200, Unit = "pt" },
        new EnumPropertyDescriptor   { Label = "Tučnost",       Group = "Font",      Get = () => FontWeight,          Set = v => FontWeight = (TextFontWeight)v,
            Options = [("Normální", (object)TextFontWeight.Normal), ("Tučné", (object)TextFontWeight.Bold)] },
        new EnumPropertyDescriptor   { Label = "Styl",          Group = "Font",      Get = () => FontStyle,           Set = v => FontStyle = (TextFontStyle)v,
            Options = [("Normální", (object)TextFontStyle.Normal), ("Kurzíva", (object)TextFontStyle.Italic)] },
        new BoolPropertyDescriptor   { Label = "Zalamování",    Group = "Font",      Get = () => WordWrap,            Set = v => WordWrap = v },
        new FloatPropertyDescriptor  { Label = "Řádkování",    Group = "Font",      Get = () => LineSpacing,         Set = v => LineSpacing = v,                            Min = 0.8f, Max = 3f, Format = "0.0#" },

        new EnumPropertyDescriptor   { Label = "Vodorovně",     Group = "Zarovnání", Get = () => HorizontalAlignment, Set = v => HorizontalAlignment = (TextHorizontalAlignment)v,
            Options = [("Vlevo", (object)TextHorizontalAlignment.Left), ("Na střed", (object)TextHorizontalAlignment.Center),
                       ("Vpravo", (object)TextHorizontalAlignment.Right), ("Do bloku", (object)TextHorizontalAlignment.Justify)] },
        new EnumPropertyDescriptor   { Label = "Svisle",        Group = "Zarovnání", Get = () => VerticalAlignment,   Set = v => VerticalAlignment = (TextVerticalAlignment)v,
            Options = [("Nahoře", (object)TextVerticalAlignment.Top), ("Na střed", (object)TextVerticalAlignment.Middle), ("Dole", (object)TextVerticalAlignment.Bottom)] },

        new ColorPropertyDescriptor  { Label = "Barva textu",   Group = "Vzhled",    Get = () => TextColor,           Set = v => TextColor = v },
        new ColorPropertyDescriptor  { Label = "Barva pozadí", Group = "Vzhled",    Get = () => BackgroundColor,     Set = v => BackgroundColor = v },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone() => new TextElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        GroupId = GroupId,
        BorderColor = BorderColor, BorderWidth = BorderWidth,
        Text = Text,
        FontFamily = FontFamily,
        FontSize = FontSize,
        FontStyle = FontStyle,
        FontWeight = FontWeight,
        HorizontalAlignment = HorizontalAlignment,
        VerticalAlignment = VerticalAlignment,
        TextColor = TextColor,
        BackgroundColor = BackgroundColor,
        WordWrap = WordWrap,
        LineSpacing = LineSpacing
    };

    // ── Word wrap helpers ────────────────────────────────────────────────────

    /// <summary>Vypočítá výšku textu v mm při zalamování do dané šířky.</summary>
    internal float CalculateTextHeight(string text)
    {
        if (string.IsNullOrEmpty(text) || Width <= 0) return 8f;

        var typeface = SKTypeface.FromFamilyName(
            FontFamily,
            FontWeight == TextFontWeight.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            FontStyle == TextFontStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;

        float fontSizeMm = FontSize * 0.3528f;
        float refMmToPx = 96f / 25.4f;
        float fontSizePx = fontSizeMm * refMmToPx;
        float widthPx = Width * refMmToPx;

        using var font = new SKFont(typeface, fontSizePx);
        var lines = TextWrapHelper.WrapText(text, font, widthPx);
        float lineHeightMm = fontSizeMm * LineSpacing;

        return Math.Max(lineHeightMm * lines.Count + fontSizeMm * 0.3f, fontSizeMm);
    }
}

public enum TextFontStyle { Normal, Italic }
public enum TextFontWeight { Normal, Bold }
public enum TextHorizontalAlignment { Left, Center, Right, Justify }
public enum TextVerticalAlignment { Top, Middle, Bottom }
