using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Kreslení buňky, ve které se <b>střídá symbolový a běžný font</b> - třeba „⚒ BEV", kde je
/// zkřížené kladivo piktogram a „BEV" zkratka.
/// </summary>
/// <remarks>
/// <para><b>Proč to nejde přes <c>CellRenderer</c>:</b> ten kreslí celou buňku jedním fontem.
/// Buňka se smíšeným obsahem se proto kreslí ručně přes <c>RowSpec.Decorate</c>, tedy až nad
/// hotovým řádkem.</para>
/// <para><b>Proč to nesmí být „buď všechno, nebo nic":</b> první verze tak vypadala a v jednom
/// řádku pak stálo u jednoho spoje kladivo a u vedlejšího písmeno „X", protože ten druhý měl
/// v buňce navíc zkratku. Táž informace dvěma způsoby vedle sebe (nález Jan 24. 8.).</para>
/// <para><c>StopListElement</c> má vlastní variantu téhož - řeší k tomu navíc odsazení za názvem
/// zastávky. Až přibude třetí konzument, patří to sloučit.</para>
/// </remarks>
public static class SymbolRunRenderer
{
    /// <summary>Jeden úsek textu se svým fontem.</summary>
    /// <param name="Text">Co se vypíše.</param>
    /// <param name="Symbol">true = symbolový Timetable font (piktogram).</param>
    /// <param name="Zmenseni">Podíl velikosti písma vůči zbytku buňky (1 = stejné). Slouží
    /// k sázení minut menším písmem než hodina; úseky sedí na společné účaří.</param>
    /// <param name="MezeraMm">Úsek je <b>mezera dané šířky</b> (mm) a nic se v něm nekreslí.
    /// Null = běžný text. Slouží k nastavitelným odstupům mezi značkami a časem, které se jinak
    /// řídí mezerou z písma a nejdou utáhnout.</param>
    public readonly record struct Run(string Text, bool Symbol, float Zmenseni = 1f,
        float? MezeraMm = null);

    /// <summary>Rozdělí značky na úseky podle fontu a mezerou je oddělí, jako by to byl jeden text.
    /// Vrací prázdný seznam, když není co kreslit.</summary>
    public static List<Run> BuildRuns(IEnumerable<string> znacky,
        Func<string, (SymbolRenderMode Mode, string Value)> resolve, float? mezeraMm = null)
    {
        var runs = new List<Run>();
        foreach (var znak in znacky)
        {
            if (string.IsNullOrEmpty(znak)) continue;
            if (runs.Count > 0) runs.Add(Mezera(mezeraMm));
            var (mode, value) = resolve(znak);
            runs.Add(new(value, mode == SymbolRenderMode.FontGlyf));
        }
        return runs;
    }

    /// <summary>Odstup mezi úseky: zadaný v mm, jinak mezera z písma.</summary>
    public static Run Mezera(float? mezeraMm, float zmenseni = 1f) =>
        mezeraMm is float mm ? new("", false, zmenseni, mm) : new(" ", false, zmenseni);

    /// <summary>Obsahuje sada aspoň jeden piktogram? Když ne, nemá smysl kreslit ručně - stačí
    /// běžná buňka.</summary>
    public static bool MaPiktogram(List<Run> runs) => runs.Exists(r => r.Symbol);

    /// <summary>Šířka úseků (mm) tak, jak je <see cref="Draw"/> sází za sebou - i s mezerou na konci.</summary>
    public static float SirkaMm(RenderContext ctx, List<Run> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        if (runs.Count == 0) return 0f;
        using var normalFont = style.CreateFont(scale, ctx.MmToPx);
        using var symbolFont = new SKFont(symbolFace, normalFont.Size);
        float px = 0f;
        foreach (var r in runs)
        {
            if (r.MezeraMm is float mm) { px += mm * ctx.MmToPx; continue; }
            var zaklad = r.Symbol ? symbolFont : normalFont;
            if (r.Zmenseni >= 0.999f) { px += zaklad.MeasureText(r.Text); continue; }
            using var f = new SKFont(zaklad.Typeface, normalFont.Size * r.Zmenseni);
            px += f.MeasureText(r.Text);
        }
        return px / ctx.MmToPx;
    }

    /// <summary>Nakreslí úseky do buňky (v mm) podle zarovnání stylu.</summary>
    /// <remarks>
    /// Vnitřní odsazení si bere sám, protože <b>ustupuje obsahu</b>: když se úseky do odsazeného
    /// boxu nevejdou, kreslí se do celé buňky. Odsazení je vzduch kolem obsahu, ne jeho hranice -
    /// bez toho se čas se značkou návaznosti vylil přes hranu sloupce, i když se do sloupce vešel
    /// (nález Jan 22. 9.). Užší než buňka to neudělá; co se nevejde ani tam, přeteče jako dřív.
    /// </remarks>
    public static void Draw(RenderContext ctx, SKRect cellMm, List<Run> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        if (runs.Count == 0) return;

        using var normalFont = style.CreateFont(scale, ctx.MmToPx);
        using var symbolFont = new SKFont(symbolFace, normalFont.Size);
        using var paint = style.CreateTextPaint();

        // Zmenšené úseky (minuty) dostanou vlastní font, ale účaří i výška řádku se berou
        // z toho plného - jinak by menší text „plaval" a buňky by v řádku nesedly.
        var zmensene = new Dictionary<float, SKFont>();
        SKFont Font(Run r)
        {
            if (r.Zmenseni >= 0.999f) return r.Symbol ? symbolFont : normalFont;
            if (!zmensene.TryGetValue(r.Zmenseni, out var f))
                zmensene[r.Zmenseni] = f = new SKFont(
                    r.Symbol ? symbolFace : normalFont.Typeface, normalFont.Size * r.Zmenseni);
            return f;
        }

        try
        {
            var m = normalFont.Metrics;
            var contentMm = CellLayout.ApplyPadding(cellMm, style);
            var tl = ctx.ToCanvas(contentMm.Left, contentMm.Top);
            var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);

            float totalPx = 0f;
            foreach (var r in runs)
                totalPx += r.MezeraMm is float mm ? mm * ctx.MmToPx : Font(r).MeasureText(r.Text);

            if (totalPx > br.X - tl.X)
            {
                tl = ctx.ToCanvas(cellMm.Left, contentMm.Top);
                br = ctx.ToCanvas(cellMm.Right, contentMm.Bottom);
            }

            var (xPx, baselineY) = CellLayout.TextAnchorPx(
                tl, br, totalPx, -m.Ascent, m.Descent, style.HAlign, style.VAlign);

            foreach (var r in runs)
            {
                if (r.MezeraMm is float mm) { xPx += mm * ctx.MmToPx; continue; }
                var f = Font(r);
                ctx.Canvas.DrawText(r.Text, xPx, baselineY, f, paint);
                xPx += f.MeasureText(r.Text);
            }
        }
        finally
        {
            foreach (var f in zmensene.Values) f.Dispose();
        }
    }
}
