using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Orchestruje kreslení jednoho řádku sloupců - deleguje buňky na CellRenderer.
/// Tabulkový element jen popisuje: "jaké sloupce mám, jaký text v každém, jaká šířka, jaký styl".
/// </summary>
public static class ColumnRenderer
{
    /// <summary>Jedna buňka řádku = šířka + text + styl. ShrinkReserveMm = rezerva pro
    /// „Přizpůsobit buňce" (element ji zvětší, když do buňky kreslí vlastní dekoraci - šipka směru).
    /// TrailingWidthMm = šířka obsahu dokreslovaného elementem ZA text (značky), škáluje se s textem.
    /// SuppressLeadingLine = na levé hraně buňky se nekreslí svislá čára; pro buňku, která je
    /// jen rozdělením jednoho sloupce (značky + název zastávky), ne samostatným sloupcem.
    /// Mezera = buňka je <b>mezera mezi skupinami sloupců</b>, tedy volné místo, ne sloupec:
    /// nekreslí se přes ni vodorovná čára ani pozadí řádku (viz <see cref="RowPlanRenderer"/>).
    /// PresahNahoruMm = buňka sahá o tolik mm NAD svůj řádek, tedy je <b>sloučená přes řádky</b>;
    /// text se pak vycentruje přes celou sloučenou výšku.</summary>
    public readonly record struct CellSpec(
        float WidthMm, string Text, ResolvedTextStyle Style,
        float ShrinkReserveMm = CellRenderer.DefaultShrinkReserveMm,
        float TrailingWidthMm = 0f,
        bool SuppressLeadingLine = false,
        bool Mezera = false,
        float PresahNahoruMm = 0f);

    /// <summary>
    /// Vykreslí vodorovný řádek buněk. Volitelně mezi sloupce svislé mřížkové čáry.
    /// </summary>
    /// <param name="ctx">Render context.</param>
    /// <param name="xStartMm">Levý okraj řádku (mm, relativní k sekci).</param>
    /// <param name="yMm">Horní okraj řádku (mm).</param>
    /// <param name="heightMm">Výška řádku (mm).</param>
    /// <param name="cells">Buňky v pořadí zleva doprava.</param>
    /// <param name="gridPaint">Pero pro svislé čáry mezi sloupci. Null = bez mřížky.</param>
    /// <param name="fontScale">Škála fontu předávaná do CellRenderer.</param>
    public static void DrawRow(
        RenderContext ctx,
        float xStartMm, float yMm, float heightMm,
        IEnumerable<CellSpec> cells,
        SKPaint? gridPaint = null,
        float fontScale = 1f)
    {
        var bunky = cells as IReadOnlyList<CellSpec> ?? cells.ToList();
        float x = xStartMm;
        foreach (var cell in bunky)
        {
            var rect = new SKRect(x, yMm - cell.PresahNahoruMm, x + cell.WidthMm, yMm + heightMm);
            CellRenderer.DrawCell(ctx, rect, cell.Text, cell.Style, fontScale, cell.ShrinkReserveMm, cell.TrailingWidthMm);
            x += cell.WidthMm;
        }
        if (gridPaint is null) return;

        // ⚠️ Čáry až po všech buňkách: čára leží napůl v sousední buňce a její pozadí by jinak
        // zakrylo pravou půlku (v podbarvených lichých Tč vyšly svislé čáry poloviční, nález Jan 5. 10.).
        // Sloučená buňka si čáru kreslí přes celou svou výšku: řádek nad ní ji sice nakreslil,
        // ale pozadí sloučené buňky tu část přemaže.
        x = xStartMm;
        for (int i = 0; i < bunky.Count; i++)
        {
            var cell = bunky[i];
            if (i > 0 && !cell.SuppressLeadingLine)
                ctx.Canvas.DrawLine(ctx.ToCanvas(x, yMm - cell.PresahNahoruMm), ctx.ToCanvas(x, yMm + heightMm), gridPaint);
            x += cell.WidthMm;
        }
    }
}
