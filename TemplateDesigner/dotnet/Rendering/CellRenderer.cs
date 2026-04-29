using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Jediné místo v aplikaci, kde se pozicuje a vykresluje text buňky tabulky.
/// Vstup: obdélník v mm + text + ResolvedTextStyle (všechny hodnoty non-null).
/// Výstup: vykreslené pozadí, text zarovnaný dle HAlign+VAlign+Padding, decorations.
/// Neví nic o sloupcích ani elementech — dělá jednu buňku.
/// </summary>
public static class CellRenderer
{
    /// <summary>
    /// Vykreslí jednu buňku: pozadí → padding shrink → HAlign+VAlign text → Underline/Strikethrough.
    /// </summary>
    /// <param name="ctx">Render context (převod mm→px, canvas).</param>
    /// <param name="rectMm">Obdélník buňky v mm, relativní k sekci (ctx.SectionOriginX/Y).</param>
    /// <param name="text">Text buňky. Prázdný string = jen pozadí.</param>
    /// <param name="style">Vyřešený styl, všechny hodnoty non-null.</param>
    /// <param name="fontScale">Škála fontu (pro auto-scale při přetečení, 1 = přirozené).</param>
    public static void DrawCell(
        RenderContext ctx, SKRect rectMm, string text,
        ResolvedTextStyle style, float fontScale = 1f)
    {
        // 1. Pozadí (jen pokud není průhledné)
        using (var bgPaint = style.CreateBgPaint())
        {
            if (bgPaint is not null)
            {
                var bgRect = ctx.ToCanvasRect(rectMm.Left, rectMm.Top, rectMm.Width, rectMm.Height);
                ctx.Canvas.DrawRect(bgRect, bgPaint);
            }
        }

        if (string.IsNullOrEmpty(text)) return;

        // 2. Padding — zmenšit content box. Pokud padding sežral celou dimenzi, rozbalit zpět.
        float contentLeft   = rectMm.Left   + style.PaddingLeft;
        float contentTop    = rectMm.Top    + style.PaddingTop;
        float contentRight  = rectMm.Right  - style.PaddingRight;
        float contentBottom = rectMm.Bottom - style.PaddingBottom;
        if (contentRight  <= contentLeft) { contentLeft = rectMm.Left; contentRight  = rectMm.Right;  }
        if (contentBottom <= contentTop)  { contentTop  = rectMm.Top;  contentBottom = rectMm.Bottom; }

        // 3. Font + paint
        using var font = style.CreateFont(fontScale, ctx.MmToPx);
        using var textPaint = style.CreateTextPaint();

        // 4. Měření textu a převod boxu na px
        float textWidthPx = font.MeasureText(text);
        var metrics = font.Metrics;
        float ascent  = -metrics.Ascent;   // Ascent je záporný (směr nahoru)
        float descent = metrics.Descent;

        var tl = ctx.ToCanvas(contentLeft,  contentTop);
        var br = ctx.ToCanvas(contentRight, contentBottom);

        // 5. Horizontální zarovnání
        float xTextPx = style.HAlign switch
        {
            TextHorizontalAlignment.Center => tl.X + (br.X - tl.X - textWidthPx) / 2f,
            TextHorizontalAlignment.Right  => br.X - textWidthPx,
            _                              => tl.X,  // Left
        };
        if (xTextPx < tl.X) xTextPx = tl.X;  // chrání před ořezem u dlouhého textu

        // 6. Vertikální zarovnání — výpočet baseline Y v px
        float yBaselinePx = style.VAlign switch
        {
            TextVerticalAlignment.Middle => tl.Y + (br.Y - tl.Y + ascent - descent) / 2f,
            TextVerticalAlignment.Bottom => br.Y - descent,
            _                            => tl.Y + ascent,  // Top
        };

        // 7. Text
        ctx.Canvas.DrawText(text, xTextPx, yBaselinePx, font, textPaint);

        // 8. Decorations (Underline / Strikethrough)
        style.DrawTextDecorations(ctx.Canvas, xTextPx, yBaselinePx, textWidthPx, font, textPaint);
    }
}
