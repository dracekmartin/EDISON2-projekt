using SkiaSharp;
using System.Text.Json.Serialization;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum TableMode { Fixed, Dynamic }

public enum ColumnWidthMode { Mm, Percent }

// ─── TableColumn ────────────────────────────────────────────────────────────

public class TableColumn
{
    /// <summary>Text záhlaví sloupce.</summary>
    public string Header { get; set; } = "";

    /// <summary>Šířka sloupce - v mm nebo procentech (dle WidthMode).</summary>
    public float Width { get; set; } = 30f;

    public ColumnWidthMode WidthMode { get; set; } = ColumnWidthMode.Mm;

    /// <summary>
    /// Klíč datového pole ze schématu agendy (např. "CAS_ODJ").
    /// Dynamic: použije se jako zdroj dat řádku.
    /// Fixed: použití v {PLACEHOLDER} syntaxi je na uživateli.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>Formátovací řetězec - "dd.MM.yyyy", "N2", "HH:mm" apod. Null = výchozí.</summary>
    public string? Format { get; set; }

    /// <summary>
    /// Počítané pole - výraz nad daty řádku (aritmetika, např. "{CENA} * {POCET}"). Má-li hodnotu,
    /// buňka se počítá z výrazu místo <see cref="Field"/> (Field se ignoruje). Číselný výsledek lze
    /// naformátovat přes <see cref="Format"/> (např. "N2"). Null/prázdné = běžný datový sloupec.
    /// </summary>
    public string? Expression { get; set; }

    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Left;

    public bool Visible { get; set; } = true;

    /// <summary>Per-column styl záhlaví. Null = dědit z elementu.</summary>
    public TextStyle? HeaderStyle { get; set; }

    /// <summary>Per-column styl datových buněk. Null = dědit z elementu.</summary>
    public TextStyle? DataStyle { get; set; }

    public TableColumn Clone() => new()
    {
        Header      = Header,
        Width       = Width,
        WidthMode   = WidthMode,
        Field       = Field,
        Format      = Format,
        Expression  = Expression,
        Alignment   = Alignment,
        Visible     = Visible,
        HeaderStyle = HeaderStyle?.Clone(),
        DataStyle   = DataStyle?.Clone(),
    };

    /// <summary>Naškáluje šířku (jen v mm, ne %) a per-sloupcové styly (font/padding).</summary>
    public void ApplyScale(float scale)
    {
        if (WidthMode == ColumnWidthMode.Mm) Width *= scale;
        HeaderStyle?.ApplyScale(scale);
        DataStyle?.ApplyScale(scale);
    }
}

// ─── TableRow / TableCell (Fixed mód) ────────────────────────────────────────

public class TableRow
{
    public List<TableCell> Cells { get; set; } = [];

    public string? BackgroundColor { get; set; }

    /// <summary>Podmíněná viditelnost vyhodnocovaná print enginem. Null = vždy viditelný.</summary>
    public string? VisibilityExpression { get; set; }

    public TableRow Clone() => new()
    {
        Cells               = Cells.Select(c => c.Clone()).ToList(),
        BackgroundColor     = BackgroundColor,
        VisibilityExpression = VisibilityExpression
    };
}

public class TableCell
{
    /// <summary>Statický text nebo "{POLE}" placeholder.</summary>
    public string Content { get; set; } = "";

    public string? Format { get; set; }

    /// <summary>Per-cell styl. Null = výchozí. Jen nenullové properties přepisují default.</summary>
    public TextStyle? Style { get; set; }

    public TableCell Clone() => new()
    {
        Content = Content,
        Format  = Format,
        Style   = Style?.Clone(),
    };
}

// ─── TableElement ────────────────────────────────────────────────────────────

/// <summary>
/// Pevná (ručně vyplňovaná) tabulka - mřížka m × n, jejíž obsah zadává uživatel; buňka nese text
/// nebo {PLACEHOLDER} vyhodnocený z dat. Nahrazuje skládání hlaviček a bloků (platební údaje,
/// rekapitulace) z jednotlivých textových prvků: chová se jako **jeden** prvek.
///
/// Řádky se generují z dat u <see cref="DynamicTableElement"/>, ne tady. Mód
/// <see cref="TableMode.Dynamic"/> zůstal jen kvůli **starým šablonám** - z UI se nenastavuje
/// (přepínač režimu v panelu není), nové tabulky vznikají přes <see cref="CreateGrid"/> jako Fixed.
///
/// Výchozí nová tabulka je bez řádku záhlaví: záhlaví = první řádek, který si uživatel udělá tučný
/// (rozhodnutí Jan 12. 8.). <see cref="ShowHeader"/> zůstává kvůli šablonám, které ho už mají.
/// </summary>
public class TableElement : TemplateElement
{
    /// <summary>
    /// Tabulka dřív kreslila vlastní vnější rámec natvrdo; ten je nově rámeček elementu, takže si ho
    /// nová tabulka přednastaví na tentýž vzhled (0,7 px při 96 DPI = 0,19 mm). `B40`.
    /// </summary>
    public TableElement()
    {
        BorderColor = "#FF999999";
        BorderWidth = 0.19f;
    }

    /// <summary>
    /// Nová prázdná mřížka pro vložení z palety - viditelné čáry, bez řádku záhlaví, sloupce
    /// stejně široké (auto). Bez tohohle by se vložil prvek bez sloupců, který se nevykreslí.
    /// </summary>
    public static TableElement CreateGrid(int columns, int rows)
    {
        var t = new TableElement
        {
            Mode       = TableMode.Fixed,
            ShowHeader = false,
            Grid       = new GridStyle { ShowRowLines = true, ShowColumnLines = true, Color = "#FFCCCCCC" },
            Columns    = [.. Enumerable.Range(0, columns).Select(_ => new TableColumn { Header = "", Width = 0f })],
        };
        t.Rows = [.. Enumerable.Range(0, rows).Select(_ => new TableRow
        {
            Cells = [.. Enumerable.Range(0, columns).Select(_ => new TableCell())]
        })];
        return t;
    }

    public TableMode Mode { get; set; } = TableMode.Dynamic;

    /// <summary>
    /// Dynamic mód: klíč datové kolekce v DataContext._rows (= DataFieldCollection.CollectionKey).
    /// Fixed mód: null (nepoužívá se).
    /// </summary>
    public string? DataSource { get; set; }

    public List<TableColumn> Columns { get; set; } = [];

    /// <summary>
    /// Fixed mód: definice řádků tabulky.
    /// Dynamic mód: null - print engine generuje řádky za běhu.
    /// </summary>
    public List<TableRow>? Rows { get; set; }

    /// <summary>Výška jednoho datového řádku v mm.</summary>
    public float RowHeight { get; set; } = 6f;

    public bool ShowHeader { get; set; } = true;

    /// <summary>Výška řádku záhlaví v mm.</summary>
    public float HeaderHeight { get; set; } = 8f;

    /// <summary>Styl záhlaví tabulky. Null = výchozí (Bold, #E8E8E8 pozadí, #333333 text).</summary>
    public TextStyle? HeaderStyle { get; set; }

    /// <summary>Styl datových buněk tabulky. Null = výchozí (#222222 text, průhledné pozadí).</summary>
    public TextStyle? DataStyle { get; set; }

    /// <summary>Mřížka = vnitřní čáry. Vnější obrys kreslí rámeček elementu (viz <see cref="GridStyle"/>).</summary>
    public GridStyle Grid { get; set; } = new() { ShowRowLines = false, ShowColumnLines = false, Color = "#FFCCCCCC" };

    /// <summary>Barva střídajících se řádků (#AARRGGBB). Null = bez střídání.</summary>
    public string? AlternatingRowColor { get; set; }

    /// <summary>
    /// Souhrnné řádky zobrazené pod datovými řádky (zápatí tabulky).
    /// Buňky mohou obsahovat agregační funkce: SUM({POLE}), COUNT(), AVG({POLE}), MIN({POLE}), MAX({POLE}).
    /// Null = žádné souhrnné řádky.
    /// </summary>
    public List<TableRow>? SummaryRows { get; set; }

    // ─── Designer helpers ────────────────────────────────────────────

    /// <summary>Počet ukázkových řádků zobrazených v designeru pro Dynamic mód.</summary>
    private int DesignerPreviewRows => 3;

    protected override float DesignerPlaceholderHeight => Mode == TableMode.Fixed
        // Aspoň jeden řádek - prázdná tabulka by jinak měla nulovou výšku a nešlo by ji chytit myší.
        ? NaturalHeight(Math.Max(Rows?.Count ?? 0, 1))
        : 40f + (SummaryRows?.Count ?? 0) * RowHeight;

    /// <summary>Přirozená (neškálovaná) výška obsahu v mm pro daný počet řádků.</summary>
    private float NaturalHeight(int rowCount) =>
        (ShowHeader ? HeaderHeight : 0f) + rowCount * RowHeight + (SummaryRows?.Count ?? 0) * RowHeight;

    /// <summary>Řádky po filtru podmíněné viditelnosti - **jediné místo**, kudy jdou render i výpočet
    /// výšky, takže se nemůžou rozejít v počtu.</summary>
    private List<TableRow> VisibleFixedRows(DataContext? data) =>
        Mode == TableMode.Fixed && Rows is { Count: > 0 }
            ? [.. Rows.Where(r => ExpressionEvaluator.IsVisible(r.VisibilityExpression, data))]
            : [];

    /// <summary>
    /// Dynamická výška v mm z dat - pro print engine (přes <c>SkiaHeightCalculator</c>, stejně jako
    /// ostatní tabulkové elementy). Fixed = počet viditelných řádků, Dynamic = řádky z <see cref="DataSource"/>
    /// (bez dat ukázkové řádky jako v designeru).
    /// </summary>
    public float CalculateHeightFromData(DataContext? data)
    {
        if (Mode == TableMode.Fixed) return NaturalHeight(VisibleFixedRows(data).Count);

        var rows = data?.GetRows(DataSource ?? "") ?? [];
        return NaturalHeight(rows.Count > 0 ? rows.Count : DesignerPreviewRows);
    }

    // ─── Style resolution (kaskáda column → element → table defaults → CellDefaults) ──

    // Gettery (ne static readonly) - aby font dědily z aktuálního CellDefaults.Default (ambient
    // = výchozí font šablony). Tyhle rodiče font nepřebíjejí, jen Bold/barvy.
    /// <summary>Výchozí parent styl pro záhlaví tabulky. Bold + šedé pozadí (#FFE8E8E8) + tmavě šedý text (#FF333333).</summary>
    private static ResolvedTextStyle TableHeaderParent => CellDefaults.Default with
    {
        Bold = true,
        TextColor = "#FF333333",
        BackgroundColor = "#FFE8E8E8",
    };

    /// <summary>Výchozí parent styl pro datové buňky. Tmavě šedý text (#FF222222) na průhledném pozadí.</summary>
    private static ResolvedTextStyle TableDataParent => CellDefaults.Default with
    {
        TextColor = "#FF222222",
    };

    /// <summary>Výchozí parent styl pro souhrnné řádky. Bold + světle šedé pozadí (#FFF0F0F0).</summary>
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

    // Kaskáda column → element → baked delegována do sdíleného jádra (TableLayout.ResolveCascade) -
    // stejně jako DynamicTable/StopList. Legacy col.Alignment = HAlign fallback jen když ho
    // nedefinuje sloupec ani element.
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

        float tableHeight = Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) : EffectiveDesignerHeight);
        var tableRect = ctx.ToCanvasRect(X, Y, Width, tableHeight);

        using var bgPaint = new SKPaint { Color = SKColors.White };
        // Jediný paint pro vnitřní čáry; vnější obrys kreslí RenderBorderPass z rámečku elementu.
        using var sepPaint = Grid.CreatePaint(ctx, BorderWidth);

        ctx.Canvas.DrawRect(tableRect, bgPaint);

        var cols = Columns.Where(c => c.Visible).ToList();
        if (cols.Count == 0)
        {
            // Bez sloupců se dřív nevykreslilo vůbec nic - vložený prvek byl neviditelný a uživatel
            // nevěděl, kam kliknul. Stejný naváděcí placeholder jako u dynamické tabulky.
            ctx.Canvas.DrawRect(tableRect, sepPaint);
            using var hintFont  = new SKFont { Size = 3.2f * ctx.MmToPx };
            using var hintPaint = new SKPaint { Color = new SKColor(0xFF999999), IsAntialias = true };
            ctx.Canvas.DrawText("Tabulka - přidejte sloupce", tableRect.MidX,
                tableRect.MidY + hintFont.Size * 0.35f, SKTextAlign.Center, hintFont, hintPaint);
            return;
        }

        // Šířky sloupců v mm přes sdílené jádro (TableLayout.ResolveWidths) - sjednoceno s
        // DynamicTable/StopList. Width <= 0 = „auto" (rovnoměrné rozdělení zbytku), jinak pevná mm/%.
        float[] colWidths = TableLayout.ResolveWidths(WidthProjection(cols), Width);

        // Pre-resolve styly (kaskáda je deterministická)
        var headerStyles = cols.Select(ResolveColumnHeaderStyle).ToArray();
        var dataStyles   = cols.Select(ResolveColumnDataStyle).ToArray();

        var fixedRows = VisibleFixedRows(ctx.Data);
        IReadOnlyList<DataContext> dataRows =
            Mode == TableMode.Dynamic ? ctx.Data?.GetRows(DataSource ?? "") ?? [] : [];

        int rowCount = Mode == TableMode.Fixed
            ? fixedRows.Count
            : (dataRows.Count > 0 ? dataRows.Count : DesignerPreviewRows);
        float naturalH = NaturalHeight(rowCount);
        float heightRatio = Height.HasValue && naturalH > 0 ? Height.Value / naturalH : 1f;
        float widthScale  = TableLayout.ComputeWidthScale(WidthProjection(cols), Width);

        // Písmo se jen **zmenšuje**, aby se obsah vešel (jako StopList/DepartureGrid/DynamicTable);
        // nikdy se nezvětšuje - vyšší tabulka má mít vyšší řádky, ne větší text.
        float fontScale = MathF.Min(1f, MathF.Min(heightRatio, widthScale));

        // Řádky vyplní výšku prvku: při roztažení se proporcionálně natáhnou (jinak by dole zbylo
        // prázdné bílé místo), při zmenšení jdou dolů s písmem. U starých tabulek v režimu „z dat"
        // se řádky netahají - jejich počet plyne z dat, takže by se výška měnila pod rukama.
        float rowScale = Mode == TableMode.Fixed ? MathF.Max(fontScale, heightRatio) : fontScale;

        float headerH = HeaderHeight * rowScale;
        float rowH    = RowHeight * rowScale;

        // Clip na hranice elementu - chrání před přesahem, pokud součet šířek sloupců
        // přesahuje Width (nekonzistentní konfigurace). Konzistentní s ostatními tabulkovými elementy.
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(tableRect);
        try
        {
            // Plán řádků (R1) - element tvaruje data, kreslí sdílená smyčka.
            var plan = new List<RowSpec>();
            if (ShowHeader)
                plan.Add(HeaderRow(cols, colWidths, headerStyles, sepPaint, headerH));
            plan.AddRange(Mode == TableMode.Fixed
                ? FixedRowSpecs(ctx, cols, colWidths, dataStyles, sepPaint, fixedRows, rowH)
                : DynamicRowSpecs(ctx, cols, colWidths, dataStyles, sepPaint, dataRows, rowH));

            float currentY = RowPlanRenderer.DrawRows(ctx, X, Y, Width, plan, fontScale);

            if (SummaryRows is { Count: > 0 })
            {
                // Oddělovač bloku souhrnů se kreslí PŘED pozadím prvního souhrnného řádku - opačné
                // pořadí než u řádkových čar, proto zůstává tady a nejde do plánu.
                ctx.Canvas.DrawLine(ctx.ToCanvas(X, currentY), ctx.ToCanvas(X + Width, currentY), sepPaint);

                RowPlanRenderer.DrawRows(ctx, X, currentY, Width,
                    SummaryRows.Select(r => SummaryRow(ctx, cols, colWidths, r, dataRows, rowH, sepPaint)),
                    fontScale);
            }
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    private static List<TableLayout.ColWidth> WidthProjection(List<TableColumn> cols) =>
        [.. cols.Select(c => new TableLayout.ColWidth(c.Width <= 0 ? null : c.Width, c.WidthMode))];

    /// <summary>Pero svislých čar mezi sloupci; null = bez čar. Poslední (vnější) čáru DrawRow
    /// nekreslí - dřív ji tabulka kreslila a zdvojovala tím vnější rámeček elementu.</summary>
    private SKPaint? ColumnGridPaint(SKPaint sepPaint) => Grid.ShowColumnLines ? sepPaint : null;

    /// <summary>Řádek záhlaví - pozadí z kaskády, dolní hrana je strukturální (kreslí se vždy).</summary>
    private RowSpec HeaderRow(List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] headerStyles, SKPaint sepPaint, float headerH)
    {
        var specs = new ColumnRenderer.CellSpec[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            specs[i] = new(colWidths[i], Rendering.ColumnHeader.Resolve(cols[i].Header, ""), headerStyles[i]);

        return new(RowKind.Header, headerH, specs)
        {
            Background        = ResolveColumnHeaderParent().ParsedBackgroundColor,
            VerticalLinePaint = ColumnGridPaint(sepPaint),
            LineBelowPaint    = sepPaint,
        };
    }

    /// <summary>Ručně vyplněné řádky: text buňky = obsah s vyhodnocenými {POLE}, styl v kaskádě
    /// buňka → sloupec → element.</summary>
    private IEnumerable<RowSpec> FixedRowSpecs(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, SKPaint sepPaint, List<TableRow> rows, float rowH)
    {
        SKColor? altColor = ColorHex.ParseOrNull(AlternatingRowColor);
        var gridPaint    = ColumnGridPaint(sepPaint);
        var rowLinePaint = Grid.ShowRowLines ? sepPaint : null;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var specs = new ColumnRenderer.CellSpec[cols.Count];
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var cellStyle = dataStyles[ci];
                string content = "";
                if (ci < row.Cells.Count)
                {
                    var cell = row.Cells[ci];
                    content = ctx.Data?.ResolvePlaceholders(cell.Content) ?? cell.Content;
                    var cellFormat = cell.Format ?? cols[ci].Format;
                    if (cellFormat is not null)
                        content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                    if (cell.Style is { } cs) cellStyle = cs.Resolve(dataStyles[ci]);
                }
                specs[ci] = new(colWidths[ci], content, cellStyle);
            }

            // Vlastní pozadí řádku přebíjí střídání řádků - dřív se kreslila obě přes sebe, takže
            // průhledná vlastní barva propouštěla zebru pod sebou.
            yield return new(RowKind.Data, rowH, specs)
            {
                Background = ColorHex.ParseOrNull(row.BackgroundColor)
                             ?? (altColor.HasValue && i % 2 == 1 ? altColor : null),
                VerticalLinePaint = gridPaint,
                LineBelowPaint    = rowLinePaint,
            };
        }
    }

    /// <summary>Řádky z dat - jen pro staré šablony, které mají tenhle prvek v režimu Dynamic
    /// (nové dynamické tabulky dělá <see cref="DynamicTableElement"/>).</summary>
    private IEnumerable<RowSpec> DynamicRowSpecs(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, SKPaint sepPaint, IReadOnlyList<DataContext> rows, float rowH)
    {
        bool hasRows = rows.Count > 0;
        int renderCount = hasRows ? rows.Count : DesignerPreviewRows;

        SKColor? altColor = ColorHex.ParseOrNull(AlternatingRowColor);
        var gridPaint    = ColumnGridPaint(sepPaint);
        var rowLinePaint = Grid.ShowRowLines ? sepPaint : null;

        // Placeholder styl pro designer - šedivý text
        ResolvedTextStyle[] effectiveStyles = hasRows
            ? dataStyles
            : [.. dataStyles.Select(s => s with { TextColor = "#FF999999" })];

        for (int i = 0; i < renderCount; i++)
        {
            var specs = new ColumnRenderer.CellSpec[cols.Count];
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var col = cols[ci];
                string sample;
                if (hasRows)
                {
                    sample = rows[i].GetString(col.Field ?? "");
                    if (col.Format is not null)
                        sample = DataContext.FormatValue(sample, col.Format, ctx.Data?.Culture);
                }
                else
                {
                    sample = $"{{{col.Field ?? col.Header}}}";
                }
                specs[ci] = new(colWidths[ci], sample, effectiveStyles[ci]);
            }

            yield return new(RowKind.Data, rowH, specs)
            {
                Background = altColor.HasValue && i % 2 == 1 ? altColor : null,
                VerticalLinePaint = gridPaint,
                LineBelowPaint    = rowLinePaint,
            };
        }
    }

    /// <summary>Souhrnný řádek - obsah přes agregace (SUM/COUNT/…) nad datovými řádky.</summary>
    private RowSpec SummaryRow(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        TableRow row, IReadOnlyList<DataContext> dataRows, float rowH, SKPaint sepPaint)
    {
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

                // Legacy col.Alignment fallback, jen pokud cell.Style neurčil HAlign
                if (cell.Style?.HAlign is null)
                    cellStyle = cellStyle with { HAlign = col.Alignment };
            }
            else
            {
                cellStyle = cellStyle with { HAlign = col.Alignment };
            }

            specs[colIdx] = new(colWidths[colIdx], content, cellStyle);
        }

        return new(RowKind.Summary, rowH, specs)
        {
            Background        = ColorHex.Parse(row.BackgroundColor, TableSummaryParent.ParsedBackgroundColor),
            VerticalLinePaint = ColumnGridPaint(sepPaint),
            LineBelowPaint    = Grid.ShowRowLines ? sepPaint : null,
        };
    }

    // ─── PropertyDescriptors ─────────────────────────────────────────

    /// <summary>Lazy init helper - HeaderStyle/DataStyle jsou nullable, setter vždy potřebuje non-null instanci.</summary>
    private TextStyle Header() => HeaderStyle ??= new TextStyle();
    private TextStyle Data()   => DataStyle   ??= new TextStyle();

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ButtonPropertyDescriptor
        {
            Label       = "Sloupce",
            Group       = "Tabulka",
            ButtonLabel = "Upravit sloupce…",
            ActionId    = ButtonPropertyDescriptor.ActionOpenTableEditor,
        },
        // Přepínač režimu tu vědomě NENÍ: dynamickou tabulku dělá DynamicTableElement, a přepnutí
        // by uživatele dostalo do stavu, ze kterého se nedostane zpět. Zdroj dat zůstává viditelný
        // jen u starých šablon, které tenhle prvek v režimu Dynamic mají.
        .. Mode == TableMode.Dynamic
            ? (PropertyDescriptor[])[new StringPropertyDescriptor
            {
                Label = "Zdroj dat",
                Group = "Tabulka",
                Get   = () => DataSource ?? "",
                Set   = v => DataSource = string.IsNullOrWhiteSpace(v) ? null : v
            }]
            : [],
        new FloatPropertyDescriptor
        {
            Label = "Výška řádku",
            Group = "Tabulka",
            Get   = () => RowHeight,
            Set   = v => RowHeight = v,
            Min   = 2, Max = 50, Unit = "mm"
        },
        new BoolPropertyDescriptor
        {
            Label = "Záhlaví",
            Group = "Tabulka",
            Get   = () => ShowHeader,
            Set   = v => ShowHeader = v
        },
        new FloatPropertyDescriptor
        {
            Label = "Výška záhlaví",
            Group = "Tabulka",
            Get   = () => HeaderHeight,
            Set   = v => HeaderHeight = v,
            Min   = 2, Max = 50, Unit = "mm"
        },
        .. GetGridDescriptors(Grid),
        new ColorPropertyDescriptor
        {
            Label = "Střídání řádků",
            Group = "Tabulka",
            Get   = () => AlternatingRowColor ?? "#00FFFFFF",
            Set   = v => AlternatingRowColor = v == "#00FFFFFF" ? null : v
        },

        // ── Záhlaví / Datové buňky - element-level základní barvy.
        // Detailní cascade (font, alignment, padding) je v TableEditorDialog.
        new BoolPropertyDescriptor
        {
            Label = "Tučné záhlaví",
            Group = "Záhlaví",
            Get   = () => HeaderStyle?.Bold ?? true,
            Set   = v => Header().Bold = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Pozadí záhlaví",
            Group = "Záhlaví",
            Get   = () => HeaderStyle?.BackgroundColor ?? "#FFE8E8E8",
            Set   = v => Header().BackgroundColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva textu záhlaví",
            Group = "Záhlaví",
            Get   = () => HeaderStyle?.TextColor ?? "#FF333333",
            Set   = v => Header().TextColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Pozadí buněk",
            Group = "Datové buňky",
            Get   = () => DataStyle?.BackgroundColor ?? "#00FFFFFF",
            Set   = v => Data().BackgroundColor = v
        },
        new ColorPropertyDescriptor
        {
            Label = "Barva textu buněk",
            Group = "Datové buňky",
            Get   = () => DataStyle?.TextColor ?? "#FF222222",
            Set   = v => Data().TextColor = v
        },

        .. GetPositionDescriptors()
    ];

    // ─── Scale ───────────────────────────────────────────────────────

    public override void ApplyScale(float scale)
    {
        base.ApplyScale(scale);
        RowHeight *= scale;
        HeaderHeight *= scale;
        HeaderStyle?.ApplyScale(scale);   // element-level font/padding (dřív se neškálovalo)
        DataStyle?.ApplyScale(scale);
        foreach (var col in Columns) col.ApplyScale(scale);   // šířka mm + per-sloupcové styly
        // Fixed mód: per-buňkové styly v řádcích i souhrnech
        foreach (var cell in (Rows ?? []).Concat(SummaryRows ?? []).SelectMany(r => r.Cells))
            cell.Style?.ApplyScale(scale);
    }

    // ─── Clone ───────────────────────────────────────────────────────

    public override TemplateElement Clone()
    {
        var clone = new TableElement
        {
            Mode                = Mode,
            DataSource          = DataSource,
            Columns             = Columns.Select(c => c.Clone()).ToList(),
            Rows                = Rows?.Select(r => r.Clone()).ToList(),
            SummaryRows         = SummaryRows?.Select(r => r.Clone()).ToList(),
            RowHeight           = RowHeight,
            ShowHeader          = ShowHeader,
            HeaderHeight        = HeaderHeight,
            HeaderStyle         = HeaderStyle?.Clone(),
            DataStyle           = DataStyle?.Clone(),
            Grid                = Grid.Clone(),
            AlternatingRowColor = AlternatingRowColor,
        };
        CopyBaseTo(clone);
        return clone;
    }
}
