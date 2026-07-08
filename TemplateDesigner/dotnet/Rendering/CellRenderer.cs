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

        // 2. Padding — zmenšit content box (mm). Čistá geometrie viz CellLayout.
        SKRect contentMm = CellLayout.ApplyPadding(rectMm, style);

        // 3. Font + paint
        using var font = style.CreateFont(fontScale, ctx.MmToPx);
        using var textPaint = style.CreateTextPaint();

        var metrics = font.Metrics;
        float ascent  = -metrics.Ascent;   // Ascent je záporný (směr nahoru)
        float descent = metrics.Descent;

        var tl = ctx.ToCanvas(contentMm.Left,  contentMm.Top);
        var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);

        // Víceřádkový text přes \n (např. dvojjazyčné záhlaví „Zastávka" / „Stop").
        // Jednořádkový = rychlá cesta (bez alokace pole).
        string[] lines = CellLayout.SplitLines(text);

        if (lines.Length == 1)
        {
            float textWidthPx = font.MeasureText(text);
            var (xPx, yBaselinePx) = CellLayout.TextAnchorPx(
                tl, br, textWidthPx, ascent, descent, style.HAlign, style.VAlign);
            ctx.Canvas.DrawText(text, xPx, yBaselinePx, font, textPaint);
            style.DrawTextDecorations(ctx.Canvas, xPx, yBaselinePx, textWidthPx, font, textPaint);
            return;
        }

        // Multi-line: blok N řádků. VAlign umístí celý blok, HAlign zarovná každý řádek zvlášť.
        float lineHeight  = ascent + descent;
        float blockHeight = lines.Length * lineHeight;
        float blockTopY = style.VAlign switch
        {
            TextVerticalAlignment.Middle => tl.Y + (br.Y - tl.Y - blockHeight) / 2f,
            TextVerticalAlignment.Bottom => br.Y - blockHeight,
            _                            => tl.Y,   // Top
        };
        if (blockTopY < tl.Y) blockTopY = tl.Y;   // nevytékat nahoru z content boxu

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;   // prázdný řádek jen posune baseline
            float lineWidthPx = font.MeasureText(line);
            float xPx = style.HAlign switch
            {
                TextHorizontalAlignment.Center => tl.X + (br.X - tl.X - lineWidthPx) / 2f,
                TextHorizontalAlignment.Right  => br.X - lineWidthPx,
                _                              => tl.X,   // Left
            };
            if (xPx < tl.X) xPx = tl.X;
            float baselineY = blockTopY + i * lineHeight + ascent;
            ctx.Canvas.DrawText(line, xPx, baselineY, font, textPaint);
            style.DrawTextDecorations(ctx.Canvas, xPx, baselineY, lineWidthPx, font, textPaint);
        }
    }
}
