using SkiaSharp;
using TemplateDesigner.Models;

namespace TemplateDesigner.Rendering;

/// <summary>Druh řádku tabulky. Kreslení sám neovlivňuje - nese **význam** řádku, na který se
/// navazují řádkové featury (page-spanning, opakování mezisoučtu na další stránce, grouping).
/// Dokud takovou featuru nemáme, je to popis: díky němu je řádkový plán čitelný a rozšiřitelný.</summary>
public enum RowKind
{
    /// <summary>Záhlaví sloupců (názvy, čísla spojů).</summary>
    Header,

    /// <summary>Nadřazené záhlaví nad skupinou sloupců (pás skupin dnů) - typicky slučuje sloupce.</summary>
    GroupHeader,

    /// <summary>Doplňkový řádek záhlaví nesoucí znaky ke sloupcům (platnost, negativní značky).</summary>
    HeaderExtra,

    /// <summary>Datový řádek.</summary>
    Data,

    /// <summary>Podřádek patřící k předchozímu datovému řádku (přípoj „vyčká / navazuje").</summary>
    SubRow,

    /// <summary>Souhrnný řádek (mezisoučet, celkem).</summary>
    Summary,
}

/// <summary>
/// Jeden řádek tabulky: druh + výška + buňky + volitelné pozadí a vodorovné čáry.
/// Nahrazuje per-element svislou smyčku - element popíše, CO se má kreslit, a
/// <see cref="RowPlanRenderer"/> to nakreslí jednou pro všechny.
/// </summary>
public readonly record struct RowSpec(
    RowKind Kind,
    float HeightMm,
    IReadOnlyList<ColumnRenderer.CellSpec> Cells)
{
    /// <summary>Pozadí celé šířky řádku, kreslí se POD buňky. Null nebo nulová alfa = nekreslit.
    /// Pro podmíněné zvýraznění / střídání řádků; per-buňkové pozadí patří do stylu buňky.</summary>
    public SKColor? Background { get; init; }

    /// <summary>Pero svislých čar mezi buňkami. Null = bez čar (sloučené pásy, kde by čáry
    /// rozsekaly nadpis přes několik sloupců).</summary>
    public SKPaint? VerticalLinePaint { get; init; }

    /// <summary>Pero vodorovné čáry na horní hraně řádku. Null = bez čáry.</summary>
    public SKPaint? LineAbovePaint { get; init; }

    /// <summary>Pero vodorovné čáry na dolní hraně řádku. Null = bez čáry.
    /// Pera jsou per-řádek schválně - záhlaví a datové řádky používají různá (rámečkové × oddělovací).</summary>
    public SKPaint? LineBelowPaint { get; init; }

    /// <summary>
    /// Dokreslení vlastního obsahu do řádku - dostane Y horní hrany (mm). Volá se **po buňkách
    /// a před čarami**, aby se chovalo jako obsah, ne jako dekorace nad mřížkou.
    ///
    /// Pro to, co buňka principiálně neumí: značky zastávky se kreslí po úsecích symbolovým
    /// a normálním fontem, a v jedné buňce nejde míchat font. Ne pro věci, které jsou vyjádřitelné
    /// buňkou nebo pozadím - ty patří do <see cref="Cells"/> / <see cref="Background"/>.
    /// </summary>
    public Action<float>? Decorate { get; init; }
}

/// <summary>
/// Jediná kreslicí smyčka řádků nad <see cref="ColumnRenderer.DrawRow"/> (R1 - řádkové jádro).
/// Dřív měl každý tabulkový element svoji svislou smyčku, takže každá řádková featura se musela
/// napsat N×; teď element jen tvaruje data (<see cref="RowSpec"/>).
///
/// ⚠️ **Pořadí kreslení je součást kontraktu**: pozadí → buňky → vodorovné čáry. Čáry musí jít
/// NAD buňky, jinak je pozadí buňky překryje (rozdíl v pixelech proti dosavadnímu chování).
/// </summary>
public static class RowPlanRenderer
{
    /// <summary>Nakreslí řádky pod sebe od <paramref name="yStartMm"/>. Vrací Y pod posledním řádkem.</summary>
    /// <param name="totalWidthMm">Šířka pro pozadí řádku a vodorovné čáry (obvykle šířka elementu).</param>
    public static float DrawRows(
        RenderContext ctx,
        float xMm, float yStartMm, float totalWidthMm,
        IEnumerable<RowSpec> rows,
        float fontScale = 1f)
    {
        float y = yStartMm;

        foreach (var row in rows)
        {
            if (row.Background is { Alpha: > 0 } color)
            {
                using var bg = new SKPaint { Color = color };
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(xMm, y, totalWidthMm, row.HeightMm), bg);
            }

            ColumnRenderer.DrawRow(ctx, xMm, y, row.HeightMm, row.Cells, row.VerticalLinePaint, fontScale);

            row.Decorate?.Invoke(y);

            if (row.LineAbovePaint is { } above)
                DrawHorizontalLine(ctx, xMm, y, totalWidthMm, above);
            if (row.LineBelowPaint is { } below)
                DrawHorizontalLine(ctx, xMm, y + row.HeightMm, totalWidthMm, below);

            y += row.HeightMm;
        }

        return y;
    }

    private static void DrawHorizontalLine(RenderContext ctx, float xMm, float yMm, float widthMm, SKPaint paint) =>
        ctx.Canvas.DrawLine(ctx.ToCanvas(xMm, yMm), ctx.ToCanvas(xMm + widthMm, yMm), paint);
}
