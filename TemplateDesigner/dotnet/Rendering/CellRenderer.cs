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

    /// <summary>Nejmenší podíl nastavené velikosti, na který smí zmenšování jít.</summary>
    /// <remarks>
    /// Bez meze se text v úzkém sloupci zmenšil, jak bylo potřeba, a z písmene zbyla tečka -
    /// doloženo na variantních sloupcích jízdních dob (6 mm), kde se nevykreslila právě ta nejširší
    /// písmena. Polovina drží autorův záměr a 4 body jsou dolní hranice pole Velikost, takže mez
    /// nevyrobí písmo, jaké by uživatel nesměl zadat ručně. Táž dvojice hodnot jako u textového
    /// prvku (<see cref="Models.Elements.TextElement.MinScale"/>) - je to totéž pravidlo.
    /// </remarks>
    public const float MinScale = 0.5f;

    /// <summary>Absolutní dno v bodech - shodné s <c>Min</c> pole Velikost.</summary>
    public const float MinFontPt = 4f;

    /// <summary>
    /// Dolní mez zmenšení pro danou velikost písma: nikdy pod polovinu <paramref name="fontScale"/>
    /// ani pod <see cref="MinFontPt"/>. Pod mezí se obsah buňky ořízne, místo aby se stal nečitelným.
    /// </summary>
    public static float MinShrinkScale(ResolvedTextStyle style, float fontScale)
    {
        if (style.FontSize <= 0f) return 0f;
        return MathF.Min(fontScale, MathF.Max(fontScale * MinScale, MinFontPt / style.FontSize));
    }

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
        return ComputeShrinkScale(probe, lines, contentWidthMm, mmToPx, fontScale, shrinkReserveMm,
            trailingWidthMm, MinShrinkScale(style, fontScale));
    }

    /// <summary>Jádro výpočtu zmenšení s existujícím fontem (bez alokace) - hot path DrawCell.
    /// <paramref name="probeAtFontScale"/> musí být vytvořený při <paramref name="fontScale"/>.</summary>
    /// <param name="minScale">Dolní mez (0 = bez meze). Volající ji spočítá
    /// <see cref="MinShrinkScale"/>; tahle přetížená verze font má, ale velikost v bodech ne.</param>
    public static float ComputeShrinkScale(
        SKFont probeAtFontScale, string[] lines, float contentWidthMm,
        float mmToPx, float fontScale, float shrinkReserveMm, float trailingWidthMm = 0f,
        float minScale = 0f)
    {
        float maxLineWidthPx = 0f;
        foreach (var l in lines)
            if (!string.IsNullOrEmpty(l)) maxLineWidthPx = Math.Max(maxLineWidthPx, probeAtFontScale.MeasureText(l));
        maxLineWidthPx += trailingWidthMm * mmToPx;

        float contentWidthPx = contentWidthMm * mmToPx;
        float targetWidthPx = (contentWidthMm - shrinkReserveMm) * mmToPx;
        if (maxLineWidthPx > contentWidthPx && targetWidthPx > 0f)
            return Math.Max(fontScale * (targetWidthPx / maxLineWidthPx), minScale);
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
        float effScale = ComputeShrinkScale(baseFont, lines, contentMm.Width, ctx.MmToPx, fontScale,
            shrinkReserveMm, trailingWidthMm, MinShrinkScale(style, fontScale));
        if (effScale != fontScale)
        {
            baseFont.Dispose();
            baseFont = style.CreateFont(effScale, ctx.MmToPx);
        }
        using var font = baseFont;
        using var textPaint = style.CreateTextPaint();

        // Na mezi zmenšování se text do buňky vejít nemusí. Ořízne se na její hranu, aby nelezl
        // do sousedního sloupce - půlka písmene je vidět, že je něco špatně, kdežto text přes
        // sousedku vypadá jako správný obsah jiné buňky.
        // ⚠️ Přetékající text začíná u levého vnitřního okraje (`CellLayout.TextAnchorPx`) a hrana
        // buňky ho usekne až za pravým vnitřním okrajem. Text, který jen zabere pravý okraj, je celý
        // vidět - dokud se srovnával s obsahem bez okrajů, hlásil se jako oříznutý („11,12" v úzkém
        // sloupci TPZ, nález Jan 15. 10.).
        bool oriznout = effScale <= MinShrinkScale(style, fontScale)
            && MeasureWidestPx(font, lines) + trailingWidthMm * ctx.MmToPx
               > (rectMm.Right - contentMm.Left) * ctx.MmToPx;
        if (oriznout)
        {
            ctx.ClippedCells?.Report(text);
            ctx.Canvas.Save();
            ctx.Canvas.ClipRect(ctx.ToCanvasRect(rectMm.Left, rectMm.Top, rectMm.Width, rectMm.Height));
        }
        try
        {
            DrawText(ctx, contentMm, text, lines, style, font, textPaint);
        }
        finally
        {
            if (oriznout) ctx.Canvas.Restore();
        }
    }

    /// <summary>Šířka nejširšího řádku v px při daném fontu.</summary>
    private static float MeasureWidestPx(SKFont font, string[] lines)
    {
        float max = 0f;
        foreach (var l in lines)
            if (!string.IsNullOrEmpty(l)) max = Math.Max(max, font.MeasureText(l));
        return max;
    }

    /// <summary>Text buňky zarovnaný dle HAlign+VAlign do už vypočteného content boxu.</summary>
    private static void DrawText(
        RenderContext ctx, SKRect contentMm, string text, string[] lines,
        ResolvedTextStyle style, SKFont font, SKPaint textPaint)
    {
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
