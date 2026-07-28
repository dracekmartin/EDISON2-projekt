using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>
/// Orchestruje kreslení jednoho řádku sloupců — deleguje buňky na CellRenderer.
/// Tabulkový element jen popisuje: "jaké sloupce mám, jaký text v každém, jaká šířka, jaký styl".
/// </summary>
public static class ColumnRenderer
{
    /// <summary>Jedna buňka řádku = šířka + text + styl. ShrinkReserveMm = rezerva pro
    /// „Přizpůsobit buňce" (element ji zvětší, když do buňky kreslí vlastní dekoraci — šipka směru).
    /// TrailingWidthMm = šířka obsahu dokreslovaného elementem ZA text (značky), škáluje se s textem.</summary>
    public readonly record struct CellSpec(
        float WidthMm, string Text, ResolvedTextStyle Style,
        float ShrinkReserveMm = CellRenderer.DefaultShrinkReserveMm,
        float TrailingWidthMm = 0f);

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
        float x = xStartMm;
        bool first = true;

        foreach (var cell in cells)
        {
            if (!first && gridPaint is not null)
            {
                var top = ctx.ToCanvas(x, yMm);
                var bot = ctx.ToCanvas(x, yMm + heightMm);
                ctx.Canvas.DrawLine(top, bot, gridPaint);
            }
            first = false;

            var rect = new SKRect(x, yMm, x + cell.WidthMm, yMm + heightMm);
            CellRenderer.DrawCell(ctx, rect, cell.Text, cell.Style, fontScale, cell.ShrinkReserveMm, cell.TrailingWidthMm);

            x += cell.WidthMm;
        }
    }
}
