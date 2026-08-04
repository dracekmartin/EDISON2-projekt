using System.Globalization;
using SkiaSharp;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Models.Layout;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Dynamická tabulka - řádky generované za běhu z <see cref="DataSource"/> (klíč do DataContext._rows,
/// = collectionKey agendy). Sloupce mapují datová pole (<see cref="TableColumn.Field"/>).
///
/// Architektonicky konzistentní s ostatními tabulkovými elementy (StopList/DepartureGrid): sdílená
/// styl-kaskáda `column.Style → element.HeaderStyle/DataStyle → baked defaults → CellDefaults`,
/// render přes `Rendering/CellRenderer`. 2-tabový editor: „Obecné" (EDInputForm - DataSource + styl)
/// + „Sloupce" (Field-mapping grid).
///
/// Statická (Fixed) tabulka je dočasně mimo (třída <see cref="TableElement"/> zůstala dormant) -
/// doděláme ji jako samostatný element až podle poptávky.
/// </summary>
public class DynamicTableElement : TemplateElement, IRowHighlightable
{
    /// <summary>
    /// Tabulka dřív kreslila vlastní vnější rámec natvrdo; ten je nově rámeček elementu, takže si ho
    /// nová tabulka přednastaví na tentýž vzhled (0,7 px při 96 DPI = 0,19 mm). `B40`.
    /// </summary>
    public DynamicTableElement()
    {
        BorderColor = "#FF999999";
        BorderWidth = 0.19f;
    }

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

    /// <summary>Mřížka = vnitřní čáry. Vnější obrys kreslí rámeček elementu (viz <see cref="GridStyle"/>).</summary>
    public GridStyle Grid { get; set; } = new() { ShowRowLines = false, ShowColumnLines = false, Color = "#FFCCCCCC" };

    /// <summary>Barva střídajících se řádků (#AARRGGBB). Null = bez střídání.</summary>
    public string? AlternatingRowColor { get; set; }

    /// <summary>Souhrnné řádky (zápatí) - buňky mohou obsahovat agregace SUM/COUNT/AVG/MIN/MAX. Null = žádné.</summary>
    public List<TableRow>? SummaryRows { get; set; }

    /// <summary>
    /// Podmínka viditelnosti řádku (výraz nad daty řádku, např. {POCET} &gt; 0). Vyhodnocuje se
    /// pro každý datový řádek zvlášť; nesplněné řádky se nevykreslí a nezapočítají (výška, agregace).
    /// Null/prázdné = všechny řádky viditelné. Analogie element-level VisibilityExpression, jen per řádek.
    /// </summary>
    public string? RowVisibilityExpression { get; set; }

    /// <summary>
    /// Podmíněné formátování řádku: seznam pravidel (výraz → barva pozadí), první splněné vyhrává.
    /// Null/prázdné = bez zvýraznění. Vyhodnocuje <see cref="RowConditions.HighlightColor(IReadOnlyList{HighlightRule}, DataContext)"/>.
    /// </summary>
    public List<HighlightRule>? HighlightRules { get; set; }

    /// <summary>
    /// Minimální počet řádků - je-li reálných datových řádků méně, tabulka se doplní prázdnými řádky
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

        // Výška boxu pro clip: pevná Height, jinak (dynamická) skutečná výška z dat - NE placeholder
        // (EffectiveDesignerHeight ~40mm), který by ořízl většinu řádků. V návrháři (bez dat) placeholder.
        float tableHeight = Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) : EffectiveDesignerHeight);
        var tableRect = ctx.ToCanvasRect(X, Y, Width, tableHeight);

        using var bgPaint     = new SKPaint { Color = SKColors.White };
        // Jediný paint pro všechny vnitřní čáry; vnější obrys kreslí RenderBorderPass z rámečku elementu.
        // Tloušťka v mm (dřív natvrdo v px, takže čáry neškálovaly se zvětšením ani s tiskovým rozlišením).
        using var sepPaint = new SKPaint
        {
            Color = ColorHex.ParseOrBlack(Grid.Color),
            IsStroke = true,
            StrokeWidth = MathF.Max(0.5f, (Grid.Width ?? BorderWidth) * ctx.MmToPx),
        };

        ctx.Canvas.DrawRect(tableRect, bgPaint);

        var visibleColumns = Columns.Where(c => c.Visible).ToList();
        if (visibleColumns.Count == 0)
        {
            // Prázdná tabulka (žádné sloupce) - v designeru zobraz naváděcí placeholder,
            // ať je element vždy viditelný (dřív se tiše nevykreslilo nic).
            ctx.Canvas.DrawRect(tableRect, sepPaint);
            string hint = string.IsNullOrWhiteSpace(DataSource)
                ? "Tabulka - vyberte zdroj dat"
                : $"Tabulka [{DataSource}] - přidejte sloupce";
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
        float heightScale = (Height.HasValue && naturalH > Height.Value && naturalH > 0)
            ? Height.Value / naturalH : 1f;
        // Vodorovně totéž: součet pevných šířek nad šířku elementu se dřív oříznul (bez varování).
        // Zmenšuje se tou horší z obou stran - výšky se pak zmenší taky, takže svisle to jen pomůže.
        float scale = MathF.Min(heightScale, ComputeWidthScale(visibleColumns));
        float headerH  = HeaderHeight * scale;
        float rowH     = RowHeight * scale;

        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(tableRect);
        try
        {
            // Plán řádků (R1) - element tvaruje data, kreslí sdílená smyčka.
            var plan = new List<RowSpec>();
            if (ShowHeader)
                plan.Add(HeaderRow(visibleColumns, colWidths, headerStyles, sepPaint, headerH));
            plan.AddRange(DataRows(ctx, visibleColumns, colWidths, dataStyles, sepPaint, dataRows, rowH));

            float currentY = RowPlanRenderer.DrawRows(ctx, X, Y, Width, plan, scale);

            if (SummaryRows is { Count: > 0 })
            {
                // Oddělovač bloku souhrnů se kreslí PŘED pozadím prvního souhrnného řádku - opačné
                // pořadí než u řádkových čar, proto zůstává tady a nejde do plánu.
                ctx.Canvas.DrawLine(ctx.ToCanvas(X, currentY), ctx.ToCanvas(X + Width, currentY), sepPaint);

                RowPlanRenderer.DrawRows(ctx, X, currentY, Width,
                    SummaryRows.Select(r => SummaryRow(ctx, visibleColumns, colWidths, r, dataRows, rowH, sepPaint)),
                    scale);
            }

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
        TableLayout.ResolveWidths(WidthProjection(cols), Width);

    /// <summary>Škála fontů, aby se tabulka vešla do šířky elementu (1 = vejde se).</summary>
    private float ComputeWidthScale(List<TableColumn> cols) =>
        TableLayout.ComputeWidthScale(WidthProjection(cols), Width);

    private static List<TableLayout.ColWidth> WidthProjection(List<TableColumn> cols) =>
        cols.Select(c => new TableLayout.ColWidth(c.Width <= 0 ? null : c.Width, c.WidthMode)).ToList();

    /// <summary>
    /// Vytvoří sloupce z datových polí kolekce (Header = label, Field = název pole, auto šířka).
    /// Sdílené panelem (auto-seed při výběru zdroje) i editorem sloupců.
    /// </summary>
    public static List<TableColumn> BuildColumnsFromFields(IEnumerable<Schema.DataField> fields) =>
        fields.Select(f => new TableColumn
        {
            Header    = f.Label,
            Field     = f.FieldName,
            Width     = 0f,                       // auto - rozdělí šířku elementu
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
        RowConditions.VisibleRows(rows, RowVisibilityExpression);

    /// <summary>Barva pozadí zvýraznění řádku (sdílený <see cref="RowConditions"/>); null = bez zvýraznění.</summary>
    public string? ResolveRowHighlight(DataContext row) =>
        RowConditions.HighlightColor(HighlightRules, row);

    /// <summary>Přirozená (neškálovaná) výška obsahu v mm pro daný počet datových řádků.</summary>
    private float NaturalHeight(int dataRowCount)
    {
        float summaryH = (SummaryRows?.Count ?? 0) * RowHeight;
        return (ShowHeader ? HeaderHeight : 0f) + dataRowCount * RowHeight + summaryH;
    }

    /// <summary>
    /// Přirozená výška obsahu z dat (mm) - pro dynamickou (Height == null) sekci v print enginu.
    /// Při pevné Height se obsah škáluje (viz Render), takže tato hodnota se použije jen pro auto-výšku.
    /// </summary>
    public float CalculateHeightFromData(DataContext? data)
    {
        var rows = VisibleDataRows(data?.GetRows(DataSource ?? "") ?? []);
        // S DataContextem (layout/tisk) efektivní počet (Max ořez + Min padding); bez něj náhledové řádky.
        int rowCount = data is null ? DesignerPreviewRows : EffectiveRowCount(rows.Count);
        return NaturalHeight(rowCount);
    }

    /// <summary>Řádek záhlaví jako <see cref="RowSpec"/> - pozadí z kaskády, dolní hrana
    /// rámečkovým perem (datové řádky používají oddělovací, proto jsou pera per-řádek).</summary>
    private RowSpec HeaderRow(List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] headerStyles, SKPaint sepPaint, float headerH)
    {
        var specs = new ColumnRenderer.CellSpec[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            specs[i] = new(colWidths[i], Rendering.ColumnHeader.Resolve(cols[i].Header, ""), headerStyles[i]);

        return new(RowKind.Header, headerH, specs)
        {
            Background = ResolveColumnHeaderParent().ParsedBackgroundColor,
            VerticalLinePaint = ColumnGridPaint(sepPaint),
            LineBelowPaint = sepPaint,   // čára pod záhlavím je strukturální - kreslí se vždy
        };
    }

    /// <summary>Pero svislých mřížkových čar mezi sloupci - jen u All/ColumnsOnly, jinak null (bez čar).
    /// Poslední (vnější) čáru DrawRow nekreslí; překryje ji vnější rámeček (kreslen jako poslední).</summary>
    private SKPaint? ColumnGridPaint(SKPaint sepPaint) =>
        Grid.ShowColumnLines ? sepPaint : null;

    /// <summary>Datové řádky jako plán: zvýraznění přebíjí střídání řádků, dolní čára jen
    /// u režimů, které řádkové čáry kreslí.</summary>
    private IEnumerable<RowSpec> DataRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, SKPaint sepPaint, IReadOnlyList<DataContext> rows, float rowH)
    {
        bool isDesigner = ctx.Data is null;
        // Datové řádky po ořezu Max; celkem = + doplnění na Min (prázdné řádky). Placeholder ({POLE})
        // jen v návrháři (bez DataContextu); s daty prázdná/nadbytečná → prázdné řádky, ne fake data.
        int dataN       = isDesigner ? 0 : ClampMax(rows.Count);
        int renderCount = isDesigner ? DesignerPreviewRows : (MinRows > 0 ? Math.Max(dataN, MinRows) : dataN);

        SKColor? altColor = ColorHex.ParseOrNull(AlternatingRowColor);

        // Šedivý placeholder styl jen pro návrhové {POLE} řádky; datové i prázdné padding = normální styl.
        ResolvedTextStyle[] effectiveStyles = isDesigner
            ? dataStyles.Select(s => s with { TextColor = "#FF999999" }).ToArray()
            : dataStyles;

        var gridPaint = ColumnGridPaint(sepPaint);
        var rowLinePaint = Grid.ShowRowLines ? sepPaint : null;

        for (int i = 0; i < renderCount; i++)
        {
            bool isDataRow = i < dataN;   // false = prázdný padding řádek (doplnění na Min)

            // Podmíněné zvýraznění (jen reálná data) přebíjí střídání řádků; jinak zebra.
            string? hlColor = isDataRow ? ResolveRowHighlight(rows[i]) : null;
            SKColor? background = ColorHex.ParseOrNull(hlColor)
                ?? (altColor.HasValue && i % 2 == 1 ? altColor : null);

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

            yield return new(RowKind.Data, rowH, specs)
            {
                Background = background,
                VerticalLinePaint = gridPaint,
                LineBelowPaint = rowLinePaint,
            };
        }
    }

    /// <summary>Souhrnný řádek jako <see cref="RowSpec"/> - pozadí per řádek (jinak z kaskády),
    /// obsah přes agregace (SUM/COUNT/…) nad datovými řádky.</summary>
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
            Background = ColorHex.Parse(row.BackgroundColor, TableSummaryParent.ParsedBackgroundColor),
            VerticalLinePaint = ColumnGridPaint(sepPaint),
            LineBelowPaint = Grid.ShowRowLines ? sepPaint : null,
        };
    }

    /// <summary>Text buňky datového řádku: Expression přebíjí Field, volitelný Format. Sdílené render cesty.</summary>
    private static string DataCellText(TableColumn col, DataContext row, CultureInfo? culture)
    {
        string s = !string.IsNullOrWhiteSpace(col.Expression)
            ? ExpressionEvaluator.EvaluateToString(col.Expression, row)
            : row.GetString(col.Field ?? "");
        return col.Format is not null ? DataContext.FormatValue(s, col.Format, culture) : s;
    }

    // ─── Rendering helpers ───────────────────────────────────────────

    /// <summary>Vsadí obdélník rámečku dovnitř o půl tahu, aby ho těsný clip sekce neukrojil na hraně
    /// (tah je vystředěný na čáře → bez insetu půlka padne mimo clip = poloviční/neviditelná hrana).</summary>
    private static SKRect InsetForStroke(SKRect r, float strokeWidthPx)
    {
        float half = strokeWidthPx / 2f;
        return new SKRect(r.Left + half, r.Top + half, r.Right - half, r.Bottom - half);
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
            ButtonLabel = "Upravit sloupce…",
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
        .. GetGridDescriptors(Grid),
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

        RowConditionDescriptors.RowVisibility(() => RowVisibilityExpression, v => RowVisibilityExpression = v),

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
            Grid                = Grid.Clone(),
            AlternatingRowColor = AlternatingRowColor,
            RowVisibilityExpression = RowVisibilityExpression,
            HighlightRules          = HighlightRules?.Select(r => r.Clone()).ToList(),
            MinRows                 = MinRows,
            MaxRows                 = MaxRows,
        };
        CopyBaseTo(clone);
        return clone;
    }
}
