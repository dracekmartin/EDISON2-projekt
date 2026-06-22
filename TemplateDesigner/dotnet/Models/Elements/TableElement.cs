using SkiaSharp;
using System.Text.Json.Serialization;
using TemplateDesigner.Models.Expressions;
using TemplateDesigner.Rendering;

namespace TemplateDesigner.Models.Elements;

// ─── Enums ──────────────────────────────────────────────────────────────────

public enum TableMode { Fixed, Dynamic }

public enum TableBorderStyle { None, Outer, All, RowsOnly, ColumnsOnly }

public enum ColumnWidthMode { Mm, Percent }

// ─── TableColumn ────────────────────────────────────────────────────────────

public class TableColumn
{
    /// <summary>Text záhlaví sloupce.</summary>
    public string Header { get; set; } = "";

    /// <summary>Šířka sloupce — v mm nebo procentech (dle WidthMode).</summary>
    public float Width { get; set; } = 30f;

    public ColumnWidthMode WidthMode { get; set; } = ColumnWidthMode.Mm;

    /// <summary>
    /// Klíč datového pole ze schématu agendy (např. "CAS_ODJ").
    /// Dynamic: použije se jako zdroj dat řádku.
    /// Fixed: použití v {PLACEHOLDER} syntaxi je na uživateli.
    /// </summary>
    public string? Field { get; set; }

    /// <summary>Formátovací řetězec — "dd.MM.yyyy", "N2", "HH:mm" apod. Null = výchozí.</summary>
    public string? Format { get; set; }

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
        Alignment   = Alignment,
        Visible     = Visible,
        HeaderStyle = HeaderStyle?.Clone(),
        DataStyle   = DataStyle?.Clone(),
    };
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
/// Tabulkový element podporující dva módy:
///   Fixed   — řádky definované v šabloně, buňky obsahují statický text nebo {PLACEHOLDER}.
///   Dynamic — řádky generované za běhu z DataSource (klíč do DataContext._rows).
///
/// Height je vždy null u Dynamic módu — výšku vypočítá print engine.
/// U Fixed módu je výška odvozena automaticky z počtu řádků.
/// </summary>
public class TableElement : TemplateElement
{
    public TableMode Mode { get; set; } = TableMode.Dynamic;

    /// <summary>
    /// Dynamic mód: klíč datové kolekce v DataContext._rows (= DataFieldCollection.CollectionKey).
    /// Fixed mód: null (nepoužívá se).
    /// </summary>
    public string? DataSource { get; set; }

    public List<TableColumn> Columns { get; set; } = [];

    /// <summary>
    /// Fixed mód: definice řádků tabulky.
    /// Dynamic mód: null — print engine generuje řádky za běhu.
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

    public TableBorderStyle BorderStyle { get; set; } = TableBorderStyle.Outer;

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

    protected override float DesignerPlaceholderHeight
    {
        get
        {
            float summaryH = SummaryRows is { Count: > 0 } ? SummaryRows.Count * RowHeight : 0f;
            if (Mode == TableMode.Fixed && Rows is { Count: > 0 })
                return (ShowHeader ? HeaderHeight : 0f) + Rows.Count * RowHeight + summaryH;
            return 40f + summaryH;
        }
    }

    // ─── Style resolution (kaskáda column → element → table defaults → CellDefaults) ──

    /// <summary>Výchozí parent styl pro záhlaví tabulky. Bold + šedé pozadí (#FFE8E8E8) + tmavě šedý text (#FF333333).</summary>
    private static readonly ResolvedTextStyle TableHeaderParent = CellDefaults.Default with
    {
        Bold = true,
        TextColor = "#FF333333",
        BackgroundColor = "#FFE8E8E8",
    };

    /// <summary>Výchozí parent styl pro datové buňky. Tmavě šedý text (#FF222222) na průhledném pozadí.</summary>
    private static readonly ResolvedTextStyle TableDataParent = CellDefaults.Default with
    {
        TextColor = "#FF222222",
    };

    /// <summary>Výchozí parent styl pro souhrnné řádky. Bold + světle šedé pozadí (#FFF0F0F0).</summary>
    private static readonly ResolvedTextStyle TableSummaryParent = CellDefaults.Default with
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

    /// <summary>Vyřeší styl záhlaví tabulky (element → defaults).</summary>
    private ResolvedTextStyle ResolveTableHeaderStyle() => ResolveColumnHeaderParent();

    /// <summary>Vyřeší styl datových buněk tabulky (element → defaults).</summary>
    private ResolvedTextStyle ResolveTableDataStyle() => ResolveColumnDataParent();

    /// <summary>Vyřeší styl záhlaví sloupce. Legacy col.Alignment je fallback pokud HAlign není v kaskádě.</summary>
    private ResolvedTextStyle ResolveColumnHeaderStyle(TableColumn col)
    {
        var elementResolved = ResolveTableHeaderStyle();
        var resolved = col.HeaderStyle is { } h ? h.Resolve(elementResolved) : elementResolved;

        if (col.HeaderStyle?.HAlign is null && HeaderStyle?.HAlign is null)
            resolved = resolved with { HAlign = col.Alignment };

        return resolved;
    }

    /// <summary>Vyřeší styl datové buňky sloupce. Legacy col.Alignment fallback stejně jako pro header.</summary>
    private ResolvedTextStyle ResolveColumnDataStyle(TableColumn col)
    {
        var elementResolved = ResolveTableDataStyle();
        var resolved = col.DataStyle is { } d ? d.Resolve(elementResolved) : elementResolved;

        if (col.DataStyle?.HAlign is null && DataStyle?.HAlign is null)
            resolved = resolved with { HAlign = col.Alignment };

        return resolved;
    }

    // ─── Render ──────────────────────────────────────────────────────

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0 || Columns.Count == 0) return;

        float tableHeight = EffectiveDesignerHeight;
        var tableRect = ctx.ToCanvasRect(X, Y, Width, tableHeight);

        using var bgPaint     = new SKPaint { Color = SKColors.White };
        using var borderPaint = new SKPaint { Color = new SKColor(0xFF999999), IsStroke = true, StrokeWidth = 0.7f };
        using var sepPaint    = new SKPaint { Color = new SKColor(0xFFCCCCCC), IsStroke = true, StrokeWidth = 0.5f };

        ctx.Canvas.DrawRect(tableRect, bgPaint);

        var visibleColumns = Columns.Where(c => c.Visible).ToList();
        if (visibleColumns.Count == 0)
        {
            if (BorderStyle != TableBorderStyle.None)
                ctx.Canvas.DrawRect(tableRect, borderPaint);
            return;
        }

        // Šířky sloupců v mm — CellRenderer pracuje v mm, přepočítá si sám na px
        float[] colWidths = visibleColumns.Select(c =>
            c.WidthMode == ColumnWidthMode.Percent ? Width * c.Width / 100f : c.Width).ToArray();

        // Pre-resolve styly (kaskáda je deterministická)
        var headerStyles = visibleColumns.Select(ResolveColumnHeaderStyle).ToArray();
        var dataStyles   = visibleColumns.Select(ResolveColumnDataStyle).ToArray();

        // Clip na hranice elementu — chrání před přesahem, pokud součet šířek sloupců
        // přesahuje Width (nekonzistentní konfigurace). Konzistentní s ostatními tabulkovými elementy.
        ctx.Canvas.Save();
        ctx.Canvas.ClipRect(tableRect);
        try
        {
            float currentY = Y;

            if (ShowHeader)
                currentY = RenderHeader(ctx, visibleColumns, colWidths, headerStyles, currentY, borderPaint, sepPaint);

            IReadOnlyList<DataContext> dataRows = [];
            if (Mode == TableMode.Fixed)
                currentY = RenderFixedRows(ctx, visibleColumns, colWidths, dataStyles, currentY, sepPaint);
            else
            {
                dataRows = ctx.Data?.GetRows(DataSource ?? "") ?? [];
                currentY = RenderDynamicRows(ctx, visibleColumns, colWidths, dataStyles, currentY, sepPaint, dataRows);
            }

            if (SummaryRows is { Count: > 0 })
                RenderSummaryRows(ctx, visibleColumns, colWidths, currentY, borderPaint, sepPaint, dataRows);

            if (BorderStyle != TableBorderStyle.None)
                ctx.Canvas.DrawRect(tableRect, borderPaint);
        }
        finally
        {
            ctx.Canvas.Restore();
        }
    }

    private float RenderHeader(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] headerStyles, float startY, SKPaint borderPaint, SKPaint sepPaint)
    {
        // Full-width pozadí z table-level stylu — pokryje případný přesah nebo mezery
        var tableHeaderStyle = ResolveTableHeaderStyle();
        using var headerBgPaint = tableHeaderStyle.CreateBgPaint();
        if (headerBgPaint is not null)
        {
            var headerBgRect = ctx.ToCanvasRect(X, startY, Width, HeaderHeight);
            ctx.Canvas.DrawRect(headerBgRect, headerBgPaint);
        }

        float colX = X;
        for (int i = 0; i < cols.Count; i++)
        {
            float colW = colWidths[i];
            var cellRect = new SKRect(colX, startY, colX + colW, startY + HeaderHeight);
            CellRenderer.DrawCell(ctx, cellRect, Rendering.ColumnHeader.Resolve(cols[i].Header, ""), headerStyles[i]);

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
            {
                var top = ctx.ToCanvas(colX + colW, startY);
                var bot = ctx.ToCanvas(colX + colW, startY + HeaderHeight);
                ctx.Canvas.DrawLine(top, bot, sepPaint);
            }
            colX += colW;
        }

        // Čára pod záhlavím
        var lineStart = ctx.ToCanvas(X, startY + HeaderHeight);
        var lineEnd   = ctx.ToCanvas(X + Width, startY + HeaderHeight);
        ctx.Canvas.DrawLine(lineStart, lineEnd, borderPaint);

        return startY + HeaderHeight;
    }

    private float RenderFixedRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, float startY, SKPaint sepPaint)
    {
        if (Rows is null || Rows.Count == 0) return startY;

        SKColor? altColor = AlternatingRowColor is not null ? SKColor.Parse(AlternatingRowColor) : null;
        using var altBgPaint = altColor.HasValue ? new SKPaint { Color = altColor.Value } : null;

        float currentY = startY;
        int visibleIdx = 0;
        foreach (var row in Rows)
        {
            if (!ExpressionEvaluator.IsVisible(row.VisibilityExpression, ctx.Data)) continue;

            // Zebra
            if (altBgPaint is not null && visibleIdx % 2 == 1)
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, RowHeight), altBgPaint);

            // Row-level bg override
            if (row.BackgroundColor is not null)
            {
                using var rowBg = new SKPaint { Color = SKColor.Parse(row.BackgroundColor) };
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, RowHeight), rowBg);
            }

            float colX = X;
            for (int colIdx = 0; colIdx < cols.Count; colIdx++)
            {
                var col = cols[colIdx];
                float colW = colWidths[colIdx];
                var cellStyle = dataStyles[colIdx];

                string content = "";
                if (colIdx < row.Cells.Count)
                {
                    var cell = row.Cells[colIdx];
                    content = ctx.Data?.ResolvePlaceholders(cell.Content) ?? cell.Content;
                    var cellFormat = cell.Format ?? col.Format;
                    if (cellFormat is not null)
                        content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                    // Per-cell styl v kaskádě od column data stylu
                    if (cell.Style is { } cs) cellStyle = cs.Resolve(dataStyles[colIdx]);
                }

                var cellRect = new SKRect(colX, currentY, colX + colW, currentY + RowHeight);
                CellRenderer.DrawCell(ctx, cellRect, content, cellStyle);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                {
                    var top = ctx.ToCanvas(colX + colW, currentY);
                    var bot = ctx.ToCanvas(colX + colW, currentY + RowHeight);
                    ctx.Canvas.DrawLine(top, bot, sepPaint);
                }
                colX += colW;
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
            {
                var a = ctx.ToCanvas(X, currentY + RowHeight);
                var b = ctx.ToCanvas(X + Width, currentY + RowHeight);
                ctx.Canvas.DrawLine(a, b, sepPaint);
            }

            currentY += RowHeight;
            visibleIdx++;
        }
        return currentY;
    }

    private float RenderDynamicRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        ResolvedTextStyle[] dataStyles, float startY, SKPaint sepPaint, IReadOnlyList<DataContext> rows)
    {
        bool hasRows = rows.Count > 0;
        int renderCount = hasRows ? rows.Count : DesignerPreviewRows;

        SKColor? altColor = AlternatingRowColor is not null ? SKColor.Parse(AlternatingRowColor) : null;
        using var altBgPaint = altColor.HasValue ? new SKPaint { Color = altColor.Value } : null;

        // Placeholder styl pro designer — šedivý text
        ResolvedTextStyle[] effectiveStyles = hasRows
            ? dataStyles
            : dataStyles.Select(s => s with { TextColor = "#FF999999" }).ToArray();

        float tableBottom = Y + EffectiveDesignerHeight;
        float currentY = startY;
        for (int i = 0; i < renderCount && currentY + RowHeight <= tableBottom; i++)
        {
            if (altBgPaint is not null && i % 2 == 1)
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, RowHeight), altBgPaint);

            float colX = X;
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var col = cols[ci];
                float colW = colWidths[ci];

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

                var cellRect = new SKRect(colX, currentY, colX + colW, currentY + RowHeight);
                CellRenderer.DrawCell(ctx, cellRect, sample, effectiveStyles[ci]);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                {
                    var top = ctx.ToCanvas(colX + colW, currentY);
                    var bot = ctx.ToCanvas(colX + colW, currentY + RowHeight);
                    ctx.Canvas.DrawLine(top, bot, sepPaint);
                }
                colX += colW;
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
            {
                var a = ctx.ToCanvas(X, currentY + RowHeight);
                var b = ctx.ToCanvas(X + Width, currentY + RowHeight);
                ctx.Canvas.DrawLine(a, b, sepPaint);
            }

            currentY += RowHeight;
        }
        return currentY;
    }

    private void RenderSummaryRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        float startY, SKPaint borderPaint, SKPaint sepPaint, IReadOnlyList<DataContext> dataRows)
    {
        if (SummaryRows is null || SummaryRows.Count == 0) return;

        // Oddělovací čára nad souhrnem
        var topStart = ctx.ToCanvas(X, startY);
        var topEnd   = ctx.ToCanvas(X + Width, startY);
        ctx.Canvas.DrawLine(topStart, topEnd, borderPaint);

        float currentY = startY;
        foreach (var row in SummaryRows)
        {
            // Pozadí řádku (buď custom, nebo default ze SummaryParent)
            var rowBgColor = row.BackgroundColor is not null
                ? SKColor.Parse(row.BackgroundColor)
                : TableSummaryParent.ParsedBackgroundColor;
            if (rowBgColor.Alpha > 0)
            {
                using var sbg = new SKPaint { Color = rowBgColor };
                ctx.Canvas.DrawRect(ctx.ToCanvasRect(X, currentY, Width, RowHeight), sbg);
            }

            float colX = X;
            for (int colIdx = 0; colIdx < cols.Count; colIdx++)
            {
                var col = cols[colIdx];
                float colW = colWidths[colIdx];

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

                var cellRect = new SKRect(colX, currentY, colX + colW, currentY + RowHeight);
                CellRenderer.DrawCell(ctx, cellRect, content, cellStyle);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                {
                    var top = ctx.ToCanvas(colX + colW, currentY);
                    var bot = ctx.ToCanvas(colX + colW, currentY + RowHeight);
                    ctx.Canvas.DrawLine(top, bot, sepPaint);
                }
                colX += colW;
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
            {
                var a = ctx.ToCanvas(X, currentY + RowHeight);
                var b = ctx.ToCanvas(X + Width, currentY + RowHeight);
                ctx.Canvas.DrawLine(a, b, sepPaint);
            }

            currentY += RowHeight;
        }
    }

    // ─── PropertyDescriptors ─────────────────────────────────────────

    /// <summary>Lazy init helper — HeaderStyle/DataStyle jsou nullable, setter vždy potřebuje non-null instanci.</summary>
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
        new EnumPropertyDescriptor
        {
            Label   = "Režim",
            Group   = "Tabulka",
            Get     = () => Mode,
            Set     = v => Mode = (TableMode)v,
            Options = [("Dynamická (z dat)", (object)TableMode.Dynamic),
                       ("Pevná (definované řádky)", (object)TableMode.Fixed)]
        },
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
        new EnumPropertyDescriptor
        {
            Label   = "Rámeček",
            Group   = "Tabulka",
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
            Label = "Střídání řádků",
            Group = "Tabulka",
            Get   = () => AlternatingRowColor ?? "#00FFFFFF",
            Set   = v => AlternatingRowColor = v == "#00FFFFFF" ? null : v
        },

        // ── Záhlaví / Datové buňky — element-level základní barvy.
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
        foreach (var col in Columns)
            if (col.WidthMode == ColumnWidthMode.Mm) col.Width *= scale;
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
            BorderStyle         = BorderStyle,
            AlternatingRowColor = AlternatingRowColor,
        };
        CopyBaseTo(clone);
        return clone;
    }
}
