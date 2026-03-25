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

    /// <summary>Styl textu — font, barvy, zarovnání, řádkování, padding.</summary>
    public TextStyle Style { get; set; } = TextStyle.CreateDefault();

    public bool WordWrap { get; set; } = true;

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

        var rs = Style.Resolve();

        // Dynamická výška: Height ?? dopočítaná z textu (pokud WordWrap)
        float heightMm = Height ?? (WordWrap ? CalculateTextHeight(resolvedText) : EffectiveDesignerHeight);
        var rect = ctx.ToCanvasRect(X, Y, Width, heightMm);

        // Pozadí (pokud není průhledné)
        using (var bgPaint = rs.CreateBgPaint())
        {
            if (bgPaint is not null)
                ctx.Canvas.DrawRect(rect, bgPaint);
        }

        // V designeru (bez dat) omezit velikost fontu, aby placeholdery typu {NAZEV_POLE}
        // nepřetékaly — zobrazit čitelně místo ve skutečné velikosti.
        float effectiveFontSize = ctx.Data is null ? Math.Min(rs.FontSize, 10f) : rs.FontSize;
        float fontSizePx = ctx.Px(effectiveFontSize * ResolvedTextStyle.PtToMm);

        var typeface = SKTypeface.FromFamilyName(rs.FontFamily, rs.ToSKFontStyle()) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, fontSizePx);
        using var paint = rs.CreateTextPaint();

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(rect);

        if (WordWrap)
        {
            float widthPx = ctx.Px(Width);
            var lines = TextWrapHelper.WrapText(resolvedText, font, widthPx);
            float lineHeightPx = fontSizePx * rs.LineSpacing;

            // Vertikální zarovnání bloku řádků
            float totalTextHeight = lines.Count * lineHeightPx;
            float yStart = rs.VAlign switch
            {
                TextVerticalAlignment.Middle => rect.Top + (rect.Height - totalTextHeight) / 2 + fontSizePx,
                TextVerticalAlignment.Bottom => rect.Bottom - totalTextHeight + fontSizePx,
                _ => rect.Top + fontSizePx
            };

            float yPx = yStart;
            foreach (var line in lines)
            {
                float xPx = rs.HAlign switch
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
            // Jednořádkový režim — s horizontálním a vertikálním zarovnáním
            float textWidth = font.MeasureText(resolvedText);
            float xPx = rs.HAlign switch
            {
                TextHorizontalAlignment.Center => rect.Left + (rect.Width - textWidth) / 2,
                TextHorizontalAlignment.Right => rect.Right - textWidth - 1f,
                _ => rect.Left + 1f
            };
            float yPx = rs.VAlign switch
            {
                TextVerticalAlignment.Middle => rect.Top + (rect.Height + fontSizePx) / 2 - fontSizePx * 0.15f,
                TextVerticalAlignment.Bottom => rect.Bottom - fontSizePx * 0.3f,
                _ => rect.Top + fontSizePx
            };
            ctx.Canvas.DrawText(resolvedText, xPx, yPx, font, paint);
        }

        ctx.Canvas.Restore();
    }

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        if (Style.FontSize is not null) Style.FontSize *= scale;
        // LineSpacing je bezrozměrný násobitel (1.2 = 120 %) — neškáluje se
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Text",         Group = "Obsah",     Get = () => Text,                Set = v => Text = v,                                   MultiLine = true },

        new StringPropertyDescriptor { Label = "Rodina písma", Group = "Font",      Get = () => Style.FontFamily ?? "Arial",  Set = v => Style.FontFamily = v },
        new FloatPropertyDescriptor  { Label = "Velikost",      Group = "Font",      Get = () => Style.FontSize ?? 10f,        Set = v => Style.FontSize = v,                 Min = 4, Max = 200, Unit = "pt" },
        new BoolPropertyDescriptor   { Label = "Tučné",        Group = "Font",      Get = () => Style.Bold ?? false,          Set = v => Style.Bold = v },
        new BoolPropertyDescriptor   { Label = "Kurzíva",      Group = "Font",      Get = () => Style.Italic ?? false,        Set = v => Style.Italic = v },
        new BoolPropertyDescriptor   { Label = "Podtržení",   Group = "Font",      Get = () => Style.Underline ?? false,     Set = v => Style.Underline = v },
        new BoolPropertyDescriptor   { Label = "Zalamování",    Group = "Font",      Get = () => WordWrap,                     Set = v => WordWrap = v },
        new FloatPropertyDescriptor  { Label = "Řádkování",    Group = "Font",      Get = () => Style.LineSpacing ?? 1.2f,    Set = v => Style.LineSpacing = v,               Min = 0.8f, Max = 3f, Format = "0.0#" },

        new EnumPropertyDescriptor   { Label = "Vodorovně",     Group = "Zarovnání", Get = () => Style.HAlign ?? TextHorizontalAlignment.Left, Set = v => Style.HAlign = (TextHorizontalAlignment)v,
            Options = [("Vlevo", (object)TextHorizontalAlignment.Left), ("Na střed", (object)TextHorizontalAlignment.Center),
                       ("Vpravo", (object)TextHorizontalAlignment.Right), ("Do bloku", (object)TextHorizontalAlignment.Justify)] },
        new EnumPropertyDescriptor   { Label = "Svisle",        Group = "Zarovnání", Get = () => Style.VAlign ?? TextVerticalAlignment.Top, Set = v => Style.VAlign = (TextVerticalAlignment)v,
            Options = [("Nahoře", (object)TextVerticalAlignment.Top), ("Na střed", (object)TextVerticalAlignment.Middle), ("Dole", (object)TextVerticalAlignment.Bottom)] },

        new ColorPropertyDescriptor  { Label = "Barva textu",   Group = "Vzhled",    Get = () => Style.TextColor ?? "#FF000000",     Set = v => Style.TextColor = v },
        new ColorPropertyDescriptor  { Label = "Barva pozadí", Group = "Vzhled",    Get = () => Style.BackgroundColor ?? "#00FFFFFF", Set = v => Style.BackgroundColor = v },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new TextElement
        {
            Text = Text,
            Style = Style.Clone(),
            WordWrap = WordWrap,
        };
        CopyBaseTo(clone);
        return clone;
    }

    // ── Word wrap helpers ────────────────────────────────────────────────────

    /// <summary>Vypočítá výšku textu v mm při zalamování do dané šířky.</summary>
    internal float CalculateTextHeight(string text)
    {
        if (string.IsNullOrEmpty(text) || Width <= 0) return 8f;

        var rs = Style.Resolve();

        var typeface = SKTypeface.FromFamilyName(rs.FontFamily, rs.ToSKFontStyle()) ?? SKTypeface.Default;

        float fontSizeMm = rs.FontSize * ResolvedTextStyle.PtToMm;
        float refMmToPx = 96f / 25.4f;
        float fontSizePx = fontSizeMm * refMmToPx;
        float widthPx = Width * refMmToPx;

        using var font = new SKFont(typeface, fontSizePx);
        var lines = TextWrapHelper.WrapText(text, font, widthPx);
        float lineHeightMm = fontSizeMm * rs.LineSpacing;

        return Math.Max(lineHeightMm * lines.Count + fontSizeMm * 0.3f, fontSizeMm);
    }
}

public enum TextHorizontalAlignment { Left, Center, Right, Justify }
public enum TextVerticalAlignment { Top, Middle, Bottom }
