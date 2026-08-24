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
    public readonly record struct Run(string Text, bool Symbol);

    /// <summary>Rozdělí značky na úseky podle fontu a mezerou je oddělí, jako by to byl jeden text.
    /// Vrací prázdný seznam, když není co kreslit.</summary>
    public static List<Run> BuildRuns(IEnumerable<string> znacky,
        Func<string, (SymbolRenderMode Mode, string Value)> resolve)
    {
        var runs = new List<Run>();
        foreach (var znak in znacky)
        {
            if (string.IsNullOrEmpty(znak)) continue;
            if (runs.Count > 0) runs.Add(new(" ", false));
            var (mode, value) = resolve(znak);
            runs.Add(new(value, mode == SymbolRenderMode.FontGlyf));
        }
        return runs;
    }

    /// <summary>Obsahuje sada aspoň jeden piktogram? Když ne, nemá smysl kreslit ručně - stačí
    /// běžná buňka.</summary>
    public static bool MaPiktogram(List<Run> runs) => runs.Exists(r => r.Symbol);

    /// <summary>Nakreslí úseky do obdélníku (v mm) podle zarovnání stylu.</summary>
    public static void Draw(RenderContext ctx, SKRect contentMm, List<Run> runs,
        ResolvedTextStyle style, SKTypeface symbolFace, float scale)
    {
        if (runs.Count == 0) return;

        using var normalFont = style.CreateFont(scale, ctx.MmToPx);
        using var symbolFont = new SKFont(symbolFace, normalFont.Size);
        using var paint = style.CreateTextPaint();

        var m = normalFont.Metrics;
        var tl = ctx.ToCanvas(contentMm.Left, contentMm.Top);
        var br = ctx.ToCanvas(contentMm.Right, contentMm.Bottom);

        float totalPx = 0f;
        foreach (var r in runs) totalPx += (r.Symbol ? symbolFont : normalFont).MeasureText(r.Text);

        var (xPx, baselineY) = CellLayout.TextAnchorPx(
            tl, br, totalPx, -m.Ascent, m.Descent, style.HAlign, style.VAlign);

        foreach (var r in runs)
        {
            var f = r.Symbol ? symbolFont : normalFont;
            ctx.Canvas.DrawText(r.Text, xPx, baselineY, f, paint);
            xPx += f.MeasureText(r.Text);
        }
    }
}
