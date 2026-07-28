using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Jediné místo v aplikaci, kde se pozicuje a vykresluje text buňky tabulky.
/// Vstup: obdélník v mm + text + ResolvedTextStyle (všechny hodnoty non-null).
/// Výstup: vykreslené pozadí, text zarovnaný dle HAlign+VAlign+Padding, decorations.
/// Neví nic o sloupcích ani elementech - dělá jednu buňku.
///
/// Vestavěné chování: text širší než buňka se proporcionálně ZMENŠÍ, aby se vešel
/// (à la Excel „přizpůsobit buňce") - přetečení pod sousední buňku (useknutá půlka
/// slova) není nikdy záměr. Bez konfigurace; případný opt-out doplnit až na poptávku.
/// </summary>
public static class CellRenderer
{
    /// <summary>Rezerva zmenšení (mm): cíl je šířka buňky minus rezerva, aby zmenšený text nekončil
    /// nalepený na mřížce. Element může předat větší (StopList kvůli šipce směru). Zmenšuje se
    /// AŽ při skutečném přetečení - přesně vešlý text (auto šířky sloupců) se nemění.</summary>
    public const float DefaultShrinkReserveMm = 0.6f;

    /// <summary>
    /// Efektivní škála fontu po přizpůsobení buňce: <paramref name="fontScale"/> zmenšené tak, aby se
    /// text (u víceřádkového nejširší řádek - jednotná velikost) + volitelný trailing obsah vešly.
    /// Sdíleno DrawCell a elementy, které si za text dokreslují vlastní obsah stejnou škálou
    /// (značky StopListu) - obě strany musí volat se STEJNÝMI vstupy, jinak se pozice rozjedou.
    /// </summary>
    public static float ComputeShrinkScale(
        ResolvedTextStyle style, string[] lines, float contentWidthMm,
        float mmToPx, float fontScale, float shrinkReserveMm, float trailingWidthMm = 0f)
    {
        using var probe = style.CreateFont(fontScale, mmToPx);
        return ComputeShrinkScale(probe, lines, contentWidthMm, mmToPx, fontScale, shrinkReserveMm, trailingWidthMm);
    }

    /// <summary>Jádro výpočtu zmenšení s existujícím fontem (bez alokace) - hot path DrawCell.
    /// <paramref name="probeAtFontScale"/> musí být vytvořený při <paramref name="fontScale"/>.</summary>
    public static float ComputeShrinkScale(
        SKFont probeAtFontScale, string[] lines, float contentWidthMm,
        float mmToPx, float fontScale, float shrinkReserveMm, float trailingWidthMm = 0f)
    {
        float maxLineWidthPx = 0f;
        foreach (var l in lines)
            if (!string.IsNullOrEmpty(l)) maxLineWidthPx = Math.Max(maxLineWidthPx, probeAtFontScale.MeasureText(l));
        maxLineWidthPx += trailingWidthMm * mmToPx;

        float contentWidthPx = contentWidthMm * mmToPx;
        float targetWidthPx = (contentWidthMm - shrinkReserveMm) * mmToPx;
        if (maxLineWidthPx > contentWidthPx && targetWidthPx > 0f)
            return fontScale * (targetWidthPx / maxLineWidthPx);
        return fontScale;
    }

    /// <summary>
    /// Vykreslí jednu buňku: pozadí → padding shrink → HAlign+VAlign text → Underline/Strikethrough.
    /// </summary>
    /// <param name="ctx">Render context (převod mm→px, canvas).</param>
    /// <param name="rectMm">Obdélník buňky v mm, relativní k sekci (ctx.SectionOriginX/Y).</param>
    /// <param name="text">Text buňky. Prázdný string = jen pozadí.</param>
    /// <param name="style">Vyřešený styl, všechny hodnoty non-null.</param>
    /// <param name="fontScale">Škála fontu (pro auto-scale při přetečení, 1 = přirozené).</param>
    /// <param name="shrinkReserveMm">Rezerva pro „Přizpůsobit buňce" (viz <see cref="DefaultShrinkReserveMm"/>).</param>
    /// <param name="trailingWidthMm">Šířka obsahu, který element dokresluje ZA text (značky StopListu)
    /// a škáluje se s ním - zmenšení počítá s tím, aby se vešel text i tento obsah.</param>
    public static void DrawCell(
        RenderContext ctx, SKRect rectMm, string text,
        ResolvedTextStyle style, float fontScale = 1f, float shrinkReserveMm = DefaultShrinkReserveMm,
        float trailingWidthMm = 0f)
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

        // 2. Padding - zmenšit content box (mm). Čistá geometrie viz CellLayout.
        SKRect contentMm = CellLayout.ApplyPadding(rectMm, style);

        // Víceřádkový text přes \n (např. dvojjazyčné záhlaví „Zastávka" / „Stop").
        // Jednořádkový = rychlá cesta (bez alokace pole).
        string[] lines = CellLayout.SplitLines(text);

        // 3. Font + paint. Přetékající text se zmenší na šířku buňky (vestavěné chování,
        // viz class doc); sedící buňky beze změny - font se pak nevytváří podruhé.
        var baseFont = style.CreateFont(fontScale, ctx.MmToPx);
        float effScale = ComputeShrinkScale(baseFont, lines, contentMm.Width, ctx.MmToPx, fontScale, shrinkReserveMm, trailingWidthMm);
        if (effScale != fontScale)
        {
            baseFont.Dispose();
            baseFont = style.CreateFont(effScale, ctx.MmToPx);
        }
        using var font = baseFont;
        using var textPaint = style.CreateTextPaint();

        var metrics = font.Metrics;
        float ascent  = -metrics.Ascent;   // Ascent je záporný (směr nahoru)
        float descent = metrics.Descent;

        var tl = ctx.ToCanvas(contentMm.Left,  contentMm.Top);
        var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);

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
