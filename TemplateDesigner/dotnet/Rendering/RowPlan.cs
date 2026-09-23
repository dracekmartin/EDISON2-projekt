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
        // ⚠️ Na jedné hranici jedna čára: čáru nad řádkem kreslí jen ten, pod kterým žádná není.
        // Dvě čáry přes sebe vyjdou o pixel silnější a v tabulce to vypadá jako vnější rám -
        // přesně tak vypadala čára úseku pod zastávkou proti tenké mřížce (nález Jan 22. 9.).
        bool caraNaHranici = false;

        foreach (var row in rows)
        {
            var mezery = Mezery(xMm, row.Cells);

            if (row.Background is { Alpha: > 0 } color)
            {
                using var bg = new SKPaint { Color = color };
                foreach (var (od, doX) in Useky(xMm, xMm + totalWidthMm, mezery))
                    ctx.Canvas.DrawRect(ctx.ToCanvasRect(od, y, doX - od, row.HeightMm), bg);
            }

            ColumnRenderer.DrawRow(ctx, xMm, y, row.HeightMm, row.Cells, row.VerticalLinePaint, fontScale);

            row.Decorate?.Invoke(y);

            if (row.LineAbovePaint is { } above && !caraNaHranici)
                DrawHorizontalLine(ctx, xMm, y, totalWidthMm, above, mezery);
            if (row.LineBelowPaint is { } below)
                DrawHorizontalLine(ctx, xMm, y + row.HeightMm, totalWidthMm, below, mezery);

            caraNaHranici = row.LineBelowPaint is not null;
            y += row.HeightMm;
        }

        return y;
    }

    /// <summary>Absolutní x-rozsahy buněk, které jsou mezerou mezi skupinami sloupců (mm).</summary>
    private static List<(float Od, float Do)> Mezery(float xMm, IReadOnlyList<ColumnRenderer.CellSpec> cells)
    {
        List<(float, float)>? mezery = null;
        float x = xMm;
        foreach (var cell in cells)
        {
            if (cell.Mezera) (mezery ??= []).Add((x, x + cell.WidthMm));
            x += cell.WidthMm;
        }
        return mezery ?? [];
    }

    /// <summary>
    /// Úseky, na kterých se smí kreslit vodorovná čára a pozadí řádku - tedy všechno kromě mezer.
    /// </summary>
    /// <remarks>
    /// ⚠️ Mezerou nesmí procházet nic, jinak se skupiny sloupců zpátky slijí: čára ji přemostí
    /// a u přípojných řádků, které mají čáru nad i pod sebou, mezera zmizí úplně (nález Jan 27. 8.
    /// na vzoru 940 - „zůstane tam spojující úsečka" a „u přípojek ta mezera udělána není vůbec").
    /// Pozadí řádku se odděluje z téhož důvodu: podbarvení buněk se v mezeře přeruší samo, takže
    /// průchozí podbarvení řádku by proti němu vypadalo jako chyba.
    /// </remarks>
    private static IEnumerable<(float Od, float Do)> Useky(
        float odMm, float doMm, List<(float Od, float Do)> mezery)
    {
        float x = odMm;
        foreach (var (mezeraOd, mezeraDo) in mezery)
        {
            if (mezeraOd > x) yield return (x, MathF.Min(mezeraOd, doMm));
            x = MathF.Max(x, mezeraDo);
            if (x >= doMm) yield break;
        }
        if (x < doMm) yield return (x, doMm);
    }

    private static void DrawHorizontalLine(RenderContext ctx, float xMm, float yMm, float widthMm,
        SKPaint paint, List<(float Od, float Do)> mezery)
    {
        foreach (var (od, doX) in Useky(xMm, xMm + widthMm, mezery))
        {
            var zacatek = ctx.ToCanvas(od, yMm);
            var konec = ctx.ToCanvas(doX, yMm);
            float y = NaPixel(ctx, zacatek.Y, paint.StrokeWidth);
            ctx.Canvas.DrawLine(new SKPoint(zacatek.X, y), new SKPoint(konec.X, y), paint);
        }
    }

    /// <summary>
    /// Srovná vodorovnou čáru na pixel, aby se nerozmazala do dvou řad.
    /// </summary>
    /// <remarks>
    /// ⚠️ Výšky řádků jsou v milimetrech, takže hranice padají mezi pixely: tenká čára se pak
    /// rozprostře do dvou řad po 40 % a vedle crisp čáry vypadá dvakrát silnější, i když má touž
    /// tloušťku (nález Jan 22. 9. na hranici nad přípojnými řádky). Tenký tah se proto posadí na
    /// půl pixelu, silný na celý - to je pravidlo, jak se kreslí ostré čáry.
    /// <para>Dělá se to <b>jen na rastru</b> (viz <see cref="RenderContext.SrovnatCaryNaPixel"/>).
    /// V PDF je jednotkou bod, takže by srovnání čáru posunulo o desetiny milimetru.</para>
    /// <para>⚠️ Musí jí projít <b>každá</b> vodorovná čára tabulky. Když si ji část kreslila sama
    /// (řádky mřížky u tabulky spojů), vyšly srovnané a nesrovnané čáry vedle sebe různě silně -
    /// a přesně to vypadalo jako „ta jedna čára je tlustší" (nález Jan 22. 9.).</para>
    /// </remarks>
    internal static float NaPixel(RenderContext ctx, float yPx, float strokePx)
    {
        if (!ctx.SrovnatCaryNaPixel || strokePx <= 0f) return yPx;
        // ⚠️ Tenčí tah než pixel a půl patří doprostřed pixelu, ne na jeho hranu - na hraně by se
        // rozdělil na dvě šedé řady, což je přesně ta „dvakrát silnější" čára. Nad tou hranicí
        // rozhoduje lichost: lichý počet pixelů sedí na půlce, sudý na celém.
        if (strokePx <= 1.5f) return MathF.Floor(yPx) + 0.5f;
        return (int)MathF.Round(strokePx) % 2 == 1 ? MathF.Floor(yPx) + 0.5f : MathF.Round(yPx);
    }
}
