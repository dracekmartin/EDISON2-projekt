using System.Globalization;
using SkiaSharp;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Dynamická tabulka — řádky generované za běhu z <see cref="DataSource"/> (klíč do DataContext._rows,
/// = collectionKey agendy). Sloupce mapují datová pole (<see cref="TableColumn.Field"/>).
///
/// Architektonicky konzistentní s ostatními tabulkovými elementy (StopList/DepartureGrid): sdílená
/// styl-kaskáda `column.Style → element.HeaderStyle/DataStyle → baked defaults → CellDefaults`,
/// render přes `Rendering/CellRenderer`. 2-tabový editor: „Obecné" (EDInputForm — DataSource + styl)
/// + „Sloupce" (Field-mapping grid).
///
/// Statická (Fixed) tabulka je dočasně mimo (třída <see cref="TableElement"/> zůstala dormant) —
/// doděláme ji jako samostatný element až podle poptávky.
/// </summary>
public class DynamicTableElement : TemplateElement, IRowPaginatedElement
{
    /// <summary>Klíč datové kolekce v DataContext._rows (= DataFieldCollection.CollectionKey).</summary>
    public string? DataSource { get; set; }

    public List<TableColumn> Columns { get; set; } = [];

    /// <summary>Výška jednoho datového řádku v mm.</summary>
    public float RowHeight { get; set; } = 6f;

    public bool ShowHeader { get; set; } = true;

    /// <summary>Výška řádku záhlaví v mm.</summary>
    public float HeaderHeight { get; set; } = 8f;

    /// <summary>Styl záhlaví tabulky. Null = výchozí (Bold, #E8E8E8 pozadí, #333333 text).</summary>
    public TextStyle? HeaderStyle { get; set; }

    /// <summary>Styl datových buněk tabulky. Null = výchozí (#222222 text, průhledné pozadí).</summary>
    public TextStyle? DataStyle { get; set; }

    public TableBorderStyle BorderStyle { get; set; } = TableBorderStyle.Outer;

    /// <summary>Barva střídajících se řádků (#AARRGGBB). Null = bez střídání.</summary>
    public string? AlternatingRowColor { get; set; }

    /// <summary>Souhrnné řádky (zápatí) — buňky mohou obsahovat agregace SUM/COUNT/AVG/MIN/MAX. Null = žádné.</summary>
    public List<TableRow>? SummaryRows { get; set; }

    /// <summary>
    /// Podmínka viditelnosti řádku (výraz nad daty řádku, např. {POCET} &gt; 0). Vyhodnocuje se
    /// pro každý datový řádek zvlášť; nesplněné řádky se nevykreslí a nezapočítají (výška, agregace).
    /// Null/prázdné = všechny řádky viditelné. Analogie element-level VisibilityExpression, jen per řádek.
    /// </summary>
    public string? RowVisibilityExpression { get; set; }

    /// <summary>
    /// Podmíněné formátování (ukázka, 1 pravidlo, jen pozadí): řádek splňující tento výraz dostane
    /// pozadí <see cref="RowHighlightColor"/>. Null/prázdné = bez zvýraznění. Do budoucna rozšířitelné
    /// na seznam pravidel + barvu textu / další zóny.
    /// </summary>
    public string? RowHighlightExpression { get; set; }

    /// <summary>Barva pozadí zvýrazněného řádku (#AARRGGBB). Null = bez zvýraznění.</summary>
    public string? RowHighlightColor { get; set; }

    /// <summary>
    /// Minimální počet řádků — je-li reálných datových řádků méně, tabulka se doplní prázdnými řádky
    /// do tohoto počtu (pevná výška boxu položek, např. faktura). 0 = bez minima.
    /// </summary>
    public int MinRows { get; set; }

    /// <summary>Maximální počet datových řádků (ořez přebytečných). 0 = bez omezení.</summary>
    public int MaxRows { get; set; }

    // ─── Designer helpers ────────────────────────────────────────────

    private int DesignerPreviewRows => 3;

    protected override float DesignerPlaceholderHeight
    {
        get
        {
            float summaryH = SummaryRows is { Count: > 0 } ? SummaryRows.Count * RowHeight : 0f;
            return 40f + summaryH;
        }
    }

    // ─── Style resolution (kaskáda column → element → table defaults → CellDefaults) ──

    private static ResolvedTextStyle TableHeaderParent => CellDefaults.Default with
    {
        Bold = true,
        TextColor = "#FF333333",
        BackgroundColor = "#FFE8E8E8",
    };

    private static ResolvedTextStyle TableDataParent => CellDefaults.Default with
    {
        TextColor = "#FF222222",
    };

    private static ResolvedTextStyle TableSummaryParent => CellDefaults.Default with
    {
        Bold = true,
        TextColor = "#FF222222",
        BackgroundColor = "#FFF0F0F0",
    };

    /// <summary>Parent kaskády pro element-level záhlaví CellStyleVM (= baked defaults).</summary>
    public ResolvedTextStyle ResolveElementHeaderParent() => TableHeaderParent;

    /// <summary>Parent kaskády pro element-level data CellStyleVM (= baked defaults).</summary>
    public ResolvedTextStyle ResolveElementDataParent() => TableDataParent;

    /// <summary>Parent kaskády pro per-column header override.</summary>
    public ResolvedTextStyle ResolveColumnHeaderParent() =>
        (HeaderStyle ?? new TextStyle()).Resolve(TableHeaderParent);

    /// <summary>Parent kaskády pro per-column data override.</summary>
    public ResolvedTextStyle ResolveColumnDataParent() =>
        (DataStyle ?? new TextStyle()).Resolve(TableDataParent);

    // Kaskáda column → element → baked delegována do sdíleného jádra (TableLayout.ResolveCascade).
    private ResolvedTextStyle ResolveColumnHeaderStyle(TableColumn col) =>
        TableLayout.ResolveCascade(col.HeaderStyle, ResolveColumnHeaderParent(), col.Alignment,
            elementDefinesHAlign: HeaderStyle?.HAlign is not null);

    private ResolvedTextStyle ResolveColumnDataStyle(TableColumn col) =>
        TableLayout.ResolveCascade(col.DataStyle, ResolveColumnDataParent(), col.Alignment,
            elementDefinesHAlign: DataStyle?.HAlign is not null);

    // ─── Render ──────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0) return;

        float tableHeight = EffectiveDesignerHeight;
        var tableRect = ctx.ToCanvasRect(X, Y, Width, tableHeight);

        using var bgPaint     = new SKPaint { Color = SKColors.White };
        using var borderPaint = new SKPaint { Color = new SKColor(0xFF999999), IsStroke = true, StrokeWidth = 0.7f };
        using var sepPaint    = new SKPaint { Color = new SKColor(0xFFCCCCCC), IsStroke = true, StrokeWidth = 0.5f };

        ctx.Canvas.DrawRect(tableRect, bgPaint);

        var visibleColumns = Columns.Where(c => c.Visible).ToList();
        if (visibleColumns.Count == 0)
        {
            // Prázdná tabulka (žádné sloupce) — v designeru zobraz naváděcí placeholder,
            // ať je element vždy viditelný (dřív se tiše nevykreslilo nic).
            ctx.Canvas.DrawRect(tableRect, borderPaint);
            string hint = string.IsNullOrWhiteSpace(DataSource)
                ? "Tabulka — vyberte zdroj dat"
                : $"Tabulka [{DataSource}] — přidejte sloupce";
            using var hintFont  = new SKFont { Size = 3.2f * ctx.MmToPx };
            using var hintPaint = new SKPaint { Color = new SKColor(0xFF999999), IsAntialias = true };
            ctx.Canvas.DrawText(hint, tableRect.MidX, tableRect.MidY + hintFont.Size * 0.35f,
                SKTextAlign.Center, hintFont, hintPaint);
            return;
        }

        float[] colWidths = ComputeColumnWidths(visibleColumns);

        var headerStyles = visibleColumns.Select(ResolveColumnHeaderStyle).ToArray();
        var dataStyles   = visibleColumns.Select(ResolveColumnDataStyle).ToArray();

        var dataRows = VisibleDataRows(ctx.Data?.GetRows(DataSource ?? "") ?? []);

        // Auto-scale: při pevné Height a přetékajícím obsahu se font + výšky řádků
        // proporcionálně zmenší (stejně jako StopList/DepartureGrid). Height == null → beze scale.
        int scaleRowCount = ctx.Data is null ? DesignerPreviewRows : EffectiveRowCount(dataRows.Count);
        float naturalH = NaturalHeight(scaleRowCount);
        float scale    = (Height.HasValue && naturalH > Height.Value && naturalH > 0)
            ? Height.Value / naturalH : 1f;
        float headerH  = HeaderHeight * scale;
        float rowH     = RowHeight * scale;

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(tableRect);
        try
        {
            float currentY = Y;

            if (ShowHeader)
                currentY = RenderHeader(ctx, visibleColumns, colWidths, headerStyles, currentY, borderPaint, sepPaint, headerH, scale);

            currentY = RenderDynamicRows(ctx, visibleColumns, colWidths, dataStyles, currentY, sepPaint, dataRows, rowH, scale);

            if (SummaryRows is { Count: > 0 })
                RenderSummaryRows(ctx, visibleColumns, colWidths, currentY, borderPaint, sepPaint, dataRows, rowH, scale);

            if (BorderStyle != TableBorderStyle.None)
                ctx.Canvas.DrawRect(tableRect, borderPaint);
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    /// <summary>
    /// Šířky sloupců v mm přes sdílené jádro (<see cref="TableLayout.ResolveWidths"/>). Sloupec
    /// s Width &lt;= 0 = „auto" (promítne se na null = rovnoměrné rozdělení zbytku), jinak pevná mm/%.
    /// </summary>
    private float[] ComputeColumnWidths(List<TableColumn> cols) =>
        TableLayout.ResolveWidths(
            cols.Select(c => new TableLayout.ColWidth(c.Width <= 0 ? null : c.Width, c.WidthMode)).ToList(),
            Width);

    /// <summary>
    /// Vytvoří sloupce z datových polí kolekce (Header = label, Field = název pole, auto šířka).
    /// Sdílené panelem (auto-seed při výběru zdroje) i editorem sloupců.
    /// </summary>
    public static List<TableColumn> BuildColumnsFromFields(IEnumerable<Schema.DataField> fields) =>
        fields.Select(f => new TableColumn
        {
            Header    = f.Label,
            Field     = f.FieldName,
            Width     = 0f,                       // auto — rozdělí šířku elementu
            WidthMode = ColumnWidthMode.Mm,
            Visible   = true
        }).ToList();

    /// <summary>
    /// Datové řádky po aplikaci <see cref="RowVisibilityExpression"/> (per řádek). Prázdný výraz =
    /// beze změny. Používá se jednotně pro render, výpočet výšky i agregace v souhrnných řádcích
    /// (skryté řádky se nesčítají).
    /// </summary>
    /// <summary>Počet reálných datových řádků po ořezu <see cref="MaxRows"/> (bez Min paddingu).</summary>
    private int ClampMax(int dataCount) => MaxRows > 0 ? Math.Min(dataCount, MaxRows) : dataCount;

    /// <summary>Celkový počet vykreslených řádků s daty = Max ořez + doplnění na <see cref="MinRows"/>.</summary>
    private int EffectiveRowCount(int dataCount)
    {
        int n = ClampMax(dataCount);
        return MinRows > 0 ? Math.Max(n, MinRows) : n;
    }

    private IReadOnlyList<DataContext> VisibleDataRows(IReadOnlyList<DataContext> rows) =>
        string.IsNullOrWhiteSpace(RowVisibilityExpression)
            ? rows
            : rows.Where(r => RowConditions.IsVisible(RowVisibilityExpression, r)).ToList();

    /// <summary>Barva pozadí zvýraznění řádku (sdílený <see cref="RowConditions"/>); null = bez zvýraznění.</summary>
    public string? ResolveRowHighlight(DataContext row) =>
        RowConditions.HighlightColor(RowHighlightExpression, RowHighlightColor, row);

    /// <summary>Přirozená (neškálovaná) výška obsahu v mm pro daný počet datových řádků.</summary>
    private float NaturalHeight(int dataRowCount)
    {
        float summaryH = (SummaryRows?.Count ?? 0) * RowHeight;
        return (ShowHeader ? HeaderHeight : 0f) + dataRowCount * RowHeight + summaryH;
    }

    /// <summary>
    /// Přirozená výška obsahu z dat (mm) — pro dynamickou (Height == null) sekci v print enginu.
    /// Při pevné Height se obsah škáluje (viz Render), takže tato hodnota se použije jen pro auto-výšku.
    /// </summary>
    public float CalculateHeightFromData(DataContext? data)
    {
        var rows = VisibleDataRows(data?.GetRows(DataSource ?? "") ?? []);
        // S DataContextem (layout/tisk) efektivní počet (Max ořez + Min padding); bez něj náhledové řádky.
        int rowCount = data is null ? DesignerPreviewRows : EffectiveRowCount(rows.Count);
        return NaturalHeight(rowCount);
    }

    private float RenderHeader(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] headerStyles, float startY, SKPaint borderPaint, SKPaint sepPaint,
        float headerH, float fontScale)
    {
        var tableHeaderStyle = ResolveColumnHeaderParent();
        using var headerBgPaint = tableHeaderStyle.CreateBgPaint();
        if (headerBgPaint is not null)
        {
            var headerBgRect = ctx.ToCanvasRect(X, startY, Width, headerH);
            ctx.Canvas.DrawRect(headerBgRect, headerBgPaint);
        }

        // Sdílená řádková orchestrace — svislé separátory přebírá DrawRow (gridPaint).
        var specs = new ColumnRenderer.CellSpec[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            specs[i] = new(colWidths[i], Rendering.ColumnHeader.Resolve(cols[i].Header, ""), headerStyles[i]);
        ColumnRenderer.DrawRow(ctx, X, startY, headerH, specs, ColumnGridPaint(sepPaint), fontScale);

        var lineStart = ctx.ToCanvas(X, startY + headerH);
        var lineEnd   = ctx.ToCanvas(X + Width, startY + headerH);
        ctx.Canvas.DrawLine(lineStart, lineEnd, borderPaint);

        return startY + headerH;
    }

    /// <summary>Pero svislých mřížkových čar mezi sloupci — jen u All/ColumnsOnly, jinak null (bez čar).
    /// Poslední (vnější) čáru DrawRow nekreslí; překryje ji vnější rámeček (kreslen jako poslední).</summary>
    private SKPaint? ColumnGridPaint(SKPaint sepPaint) =>
        BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly ? sepPaint : null;

    private float RenderDynamicRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, float startY, SKPaint sepPaint, IReadOnlyList<DataContext> rows,
        float rowH, float fontScale)
    {
        bool isDesigner = ctx.Data is null;
        // Datové řádky po ořezu Max; celkem = + doplnění na Min (prázdné řádky). Placeholder ({POLE})
        // jen v návrháři (bez DataContextu); s daty prázdná/nadbytečná → prázdné řádky, ne fake data.
        int dataN       = isDesigner ? 0 : ClampMax(rows.Count);
        int renderCount = isDesigner ? DesignerPreviewRows : (MinRows > 0 ? Math.Max(dataN, MinRows) : dataN);

        SKColor? altColor = AlternatingRowColor is not null ? SKColor.Parse(AlternatingRowColor) : null;
        using var altBgPaint = altColor.HasValue ? new SKPaint { Color = altColor.Value } : null;

        // Šedivý placeholder styl jen pro návrhové {POLE} řádky; datové i prázdné padding = normální styl.
        ResolvedTextStyle[] effectiveStyles = isDesigner
            ? dataStyles.Select(s => s with { TextColor = "#FF999999" }).ToArray()
            : dataStyles;

        var gridPaint = ColumnGridPaint(sepPaint);
        float currentY = startY;
        for (int i = 0; i < renderCount; i++)
        {
            bool isDataRow = i < dataN;   // false = prázdný padding řádek (doplnění na Min)

            // Podmíněné zvýraznění (jen reálná data) přebíjí střídání řádků; jinak zebra.
            string? hlColor = isDataRow ? ResolveRowHighlight(rows[i]) : null;
            if (hlColor is not null)
            {
                using var hlPaint = new SKPaint { Color = SKColor.Parse(hlColor) };
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, rowH), hlPaint);
            }
            else if (altBgPaint is not null && i % 2 == 1)
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, rowH), altBgPaint);

            var specs = new ColumnRenderer.CellSpec[cols.Count];
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var col = cols[ci];
                string sample;
                if (isDataRow)
                {
                    sample = DataCellText(col, rows[i], ctx.Data?.Culture);
                }
                else if (isDesigner)
                {
                    sample = !string.IsNullOrWhiteSpace(col.Expression)
                        ? col.Expression!
                        : $"{{{col.Field ?? col.Header}}}";
                }
                else
                {
                    sample = "";   // prázdný padding řádek (doplnění na MinRows)
                }
                specs[ci] = new(colWidths[ci], sample, effectiveStyles[ci]);
            }
            ColumnRenderer.DrawRow(ctx, X, currentY, rowH, specs, gridPaint, fontScale);

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
            {
                var a = ctx.ToCanvas(X, currentY + rowH);
                var b = ctx.ToCanvas(X + Width, currentY + rowH);
                ctx.Canvas.DrawLine(a, b, sepPaint);
            }

            currentY += rowH;
        }
        return currentY;
    }

    private void RenderSummaryRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        float startY, SKPaint borderPaint, SKPaint sepPaint, IReadOnlyList<DataContext> dataRows,
        float rowH, float fontScale)
    {
        if (SummaryRows is null || SummaryRows.Count == 0) return;

        var topStart = ctx.ToCanvas(X, startY);
        var topEnd   = ctx.ToCanvas(X + Width, startY);
        ctx.Canvas.DrawLine(topStart, topEnd, borderPaint);

        float currentY = startY;
        foreach (var row in SummaryRows)
        {
            DrawSummaryRowAt(ctx, cols, colWidths, row, dataRows, currentY, rowH, sepPaint, fontScale);
            currentY += rowH;
        }
    }

    /// <summary>Vykreslí jeden souhrnný řádek na dané Y (sdílené plným renderem i řádkovým oknem R2).</summary>
    private void DrawSummaryRowAt(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        TableRow row, IReadOnlyList<DataContext> dataRows, float y, float rowH, SKPaint sepPaint, float fontScale)
    {
        var rowBgColor = row.BackgroundColor is not null
            ? SKColor.Parse(row.BackgroundColor)
            : TableSummaryParent.ParsedBackgroundColor;
        if (rowBgColor.Alpha > 0)
        {
            using var sbg = new SKPaint { Color = rowBgColor };
            ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, y, Width, rowH), sbg);
        }

        var specs = new ColumnRenderer.CellSpec[cols.Count];
        for (int colIdx = 0; colIdx < cols.Count; colIdx++)
        {
            var col = cols[colIdx];
            var cellStyle = TableSummaryParent;
            string content = "";

            if (colIdx < row.Cells.Count)
            {
                var cell = row.Cells[colIdx];
                content = AggregationResolver.Resolve(cell.Content, dataRows);
                content = ctx.Data?.ResolvePlaceholders(content) ?? content;
                var cellFormat = cell.Format ?? col.Format;
                if (cellFormat is not null)
                    content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                if (cell.Style is { } cs) cellStyle = cs.Resolve(TableSummaryParent);
                if (cell.Style?.HAlign is null)
                    cellStyle = cellStyle with { HAlign = col.Alignment };
            }
            else
            {
                cellStyle = cellStyle with { HAlign = col.Alignment };
            }

            specs[colIdx] = new(colWidths[colIdx], content, cellStyle);
        }
        ColumnRenderer.DrawRow(ctx, X, y, rowH, specs, ColumnGridPaint(sepPaint), fontScale);

        if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
            ctx.Canvas.DrawLine(ctx.ToCanvas(X, y + rowH), ctx.ToCanvas(X + Width, y + rowH), sepPaint);
    }

    /// <summary>Text buňky datového řádku: Expression přebíjí Field, volitelný Format. Sdílené render cesty.</summary>
    private static string DataCellText(TableColumn col, DataContext row, CultureInfo? culture)
    {
        string s = !string.IsNullOrWhiteSpace(col.Expression)
            ? ExpressionEvaluator.EvaluateToString(col.Expression, row)
            : row.GetString(col.Field ?? "");
        return col.Format is not null ? DataContext.FormatValue(s, col.Format, culture) : s;
    }

    // ─── IRowPaginatedElement (R2 — svislé stránkování řádků) ────────────────────

    /// <summary>Datové řádky ke stránkování: viditelné (po filtru) + ořez Max. Bez Min paddingu
    /// — paginující tabulka nedoplňuje prázdné řádky (to je jen pro fixní-výškový box).</summary>
    private int PaginatedDataCount(DataContext? data) =>
        ClampMax(VisibleDataRows(data?.GetRows(DataSource ?? "") ?? []).Count);

    // Jednotky: datové řádky (0..D-1) + souhrnné řádky (D..D+S-1). Záhlaví = samostatný opakovaný band.
    public int RowUnitCount(DataContext? data) => PaginatedDataCount(data) + (SummaryRows?.Count ?? 0);
    public float HeaderBandHeightMm(DataContext? data) => ShowHeader ? HeaderHeight : 0f;
    public float RowUnitHeightMm(DataContext? data, int unitIndex) => RowHeight;

    public void RenderRowWindow(RenderContext ctx, int startUnit, int endUnitExclusive, bool drawHeader)
    {
        var cols = Columns.Where(c => c.Visible).ToList();
        if (cols.Count == 0 || Width <= 0) return;

        float[] colWidths = ComputeColumnWidths(cols);
        var headerStyles = cols.Select(ResolveColumnHeaderStyle).ToArray();
        var dataStyles   = cols.Select(ResolveColumnDataStyle).ToArray();
        var dataRows = VisibleDataRows(ctx.Data?.GetRows(DataSource ?? "") ?? []);
        int dataN = ClampMax(dataRows.Count);

        using var borderPaint = new SKPaint { Color = new SKColor(0xFF999999), IsStroke = true, StrokeWidth = 0.7f };
        using var sepPaint    = new SKPaint { Color = new SKColor(0xFFCCCCCC), IsStroke = true, StrokeWidth = 0.5f };
        var gridPaint = ColumnGridPaint(sepPaint);

        SKColor? altColor = AlternatingRowColor is not null ? SKColor.Parse(AlternatingRowColor) : null;
        using var altBgPaint = altColor.HasValue ? new SKPaint { Color = altColor.Value } : null;

        float startY  = Y;
        float currentY = Y;

        if (drawHeader && ShowHeader)
            currentY = RenderHeader(ctx, cols, colWidths, headerStyles, currentY, borderPaint, sepPaint, HeaderHeight, 1f);

        for (int u = startUnit; u < endUnitExclusive; u++)
        {
            if (u < dataN)
            {
                var row = dataRows[u];
                // Zvýraznění (dle absolutního indexu u) přebíjí zebru; jinak střídání řádků.
                string? hl = ResolveRowHighlight(row);
                if (hl is not null)
                {
                    using var p = new SKPaint { Color = SKColor.Parse(hl) };
                    ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, RowHeight), p);
                }
                else if (altBgPaint is not null && u % 2 == 1)
                    ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, RowHeight), altBgPaint);

                var specs = new ColumnRenderer.CellSpec[cols.Count];
                for (int ci = 0; ci < cols.Count; ci++)
                    specs[ci] = new(colWidths[ci], DataCellText(cols[ci], row, ctx.Data?.Culture), dataStyles[ci]);
                ColumnRenderer.DrawRow(ctx, X, currentY, RowHeight, specs, gridPaint, 1f);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
                    ctx.Canvas.DrawLine(ctx.ToCanvas(X, currentY + RowHeight), ctx.ToCanvas(X + Width, currentY + RowHeight), sepPaint);
            }
            else if (SummaryRows is not null && u - dataN < SummaryRows.Count)
            {
                DrawSummaryRowAt(ctx, cols, colWidths, SummaryRows[u - dataN], dataRows, currentY, RowHeight, sepPaint, 1f);
            }
            currentY += RowHeight;
        }

        if (BorderStyle != TableBorderStyle.None)
            ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, startY, Width, currentY - startY), borderPaint);
    }

    // ─── PropertyDescriptors ─────────────────────────────────────────

    private TextStyle Header() => HeaderStyle ??= new TextStyle();
    private TextStyle Data()   => DataStyle   ??= new TextStyle();

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ButtonPropertyDescriptor
        {
            Label       = "Sloupce",
            Group       = "Tabulka",
            ButtonLabel = "Upravit sloupce...",
            ActionId    = ButtonPropertyDescriptor.ActionOpenTableEditor,
        },
        new StringPropertyDescriptor
        {
            Label = "Zdroj dat",
            Group = "Tabulka",
            Get   = () => DataSource ?? "",
            Set   = v => DataSource = string.IsNullOrWhiteSpace(v) ? null : v
        },
        new FloatPropertyDescriptor
        {
            Label = "Výška řádku", Group = "Tabulka",
            Get   = () => RowHeight, Set = v => RowHeight = v,
            Min   = 2, Max = 50, Unit = "mm"
        },
        new BoolPropertyDescriptor { Label = "Záhlaví", Group = "Tabulka", Get = () => ShowHeader, Set = v => ShowHeader = v },
        new FloatPropertyDescriptor
        {
            Label = "Výška záhlaví", Group = "Tabulka",
            Get   = () => HeaderHeight, Set = v => HeaderHeight = v,
            Min   = 2, Max = 50, Unit = "mm"
        },
        new EnumPropertyDescriptor
        {
            Label   = "Rámeček", Group = "Tabulka",
            Get     = () => BorderStyle,
            Set     = v => BorderStyle = (TableBorderStyle)v,
            Options = [("Žádný",    (object)TableBorderStyle.None),
                       ("Vnější",   (object)TableBorderStyle.Outer),
                       ("Vše",      (object)TableBorderStyle.All),
                       ("Řádky",    (object)TableBorderStyle.RowsOnly),
                       ("Sloupce",  (object)TableBorderStyle.ColumnsOnly)]
        },
        new ColorPropertyDescriptor
        {
            Label = "Střídání řádků", Group = "Tabulka",
            Get   = () => AlternatingRowColor ?? "#00FFFFFF",
            Set   = v => AlternatingRowColor = v == "#00FFFFFF" ? null : v
        },
        new FloatPropertyDescriptor
        {
            Label = "Min. řádků (0 = ne)", Group = "Tabulka",
            Get   = () => MinRows, Set = v => MinRows = (int)Math.Max(0, v),
            Min   = 0, Max = 200, Format = "0"
        },
        new FloatPropertyDescriptor
        {
            Label = "Max. řádků (0 = bez limitu)", Group = "Tabulka",
            Get   = () => MaxRows, Set = v => MaxRows = (int)Math.Max(0, v),
            Min   = 0, Max = 500, Format = "0"
        },

        new BoolPropertyDescriptor  { Label = "Tučné záhlaví",       Group = "Záhlaví",      Get = () => HeaderStyle?.Bold ?? true,                    Set = v => Header().Bold = v },
        new ColorPropertyDescriptor { Label = "Pozadí záhlaví",      Group = "Záhlaví",      Get = () => HeaderStyle?.BackgroundColor ?? "#FFE8E8E8",  Set = v => Header().BackgroundColor = v },
        new ColorPropertyDescriptor { Label = "Barva textu záhlaví", Group = "Záhlaví",      Get = () => HeaderStyle?.TextColor ?? "#FF333333",        Set = v => Header().TextColor = v },
        new ColorPropertyDescriptor { Label = "Pozadí buněk",        Group = "Datové buňky", Get = () => DataStyle?.BackgroundColor ?? "#00FFFFFF",    Set = v => Data().BackgroundColor = v },
        new ColorPropertyDescriptor { Label = "Barva textu buněk",   Group = "Datové buňky", Get = () => DataStyle?.TextColor ?? "#FF222222",          Set = v => Data().TextColor = v },

        new StringPropertyDescriptor
        {
            Label = "Viditelnost řádku", Group = "Podmínky",
            // Podmínka nad daty řádku — nesplněné řádky se nevykreslí. Prázdné = všechny.
            Get = () => RowVisibilityExpression ?? "",
            Set = v  => RowVisibilityExpression = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
            Validate = ExpressionEvaluator.Validate
        },
        new StringPropertyDescriptor
        {
            Label = "Zvýraznit řádek (podmínka)", Group = "Podmínky",
            // Řádek splňující podmínku dostane pozadí „Barva zvýraznění". Prázdné = bez zvýraznění.
            Get = () => RowHighlightExpression ?? "",
            Set = v  => RowHighlightExpression = string.IsNullOrWhiteSpace(v) ? null : v.Trim(),
            Validate = ExpressionEvaluator.Validate
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva zvýraznění", Group = "Podmínky",
            Get   = () => RowHighlightColor ?? "#00FFFFFF",
            Set   = v => RowHighlightColor = v == "#00FFFFFF" ? null : v
        },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ───────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        RowHeight *= scale;
        HeaderHeight *= scale;
        foreach (var col in Columns)
            if (col.WidthMode == ColumnWidthMode.Mm) col.Width *= scale;
    }

    // ─── Clone ───────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new DynamicTableElement
        {
            DataSource          = DataSource,
            Columns             = Columns.Select(c => c.Clone()).ToList(),
            SummaryRows         = SummaryRows?.Select(r => r.Clone()).ToList(),
            RowHeight           = RowHeight,
            ShowHeader          = ShowHeader,
            HeaderHeight        = HeaderHeight,
            HeaderStyle         = HeaderStyle?.Clone(),
            DataStyle           = DataStyle?.Clone(),
            BorderStyle         = BorderStyle,
            AlternatingRowColor = AlternatingRowColor,
            RowVisibilityExpression = RowVisibilityExpression,
            RowHighlightExpression  = RowHighlightExpression,
            RowHighlightColor       = RowHighlightColor,
            MinRows                 = MinRows,
            MaxRows                 = MaxRows,
        };
        CopyBaseTo(clone);
        return clone;
    }
}
