using SkiaSharp;
using System.Text.Json.Serialization;
using TemplateDesigner.Models.Expressions;

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

    // ─── Header style defaults ────────────────────────────────────────

    private static readonly ResolvedTextStyle DefaultHeaderParent = new()
    {
        FontFamily = "Arial", FontSize = 10f,
        Bold = true, Italic = false, Underline = false,
        TextColor = "#FF333333", BackgroundColor = "#FFE8E8E8",
        HAlign = TextHorizontalAlignment.Left, VAlign = TextVerticalAlignment.Top,
        PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0, PaddingLeft = 0,
        LineSpacing = 1.2f,
    };

    private static readonly ResolvedTextStyle DefaultDataParent = new()
    {
        FontFamily = "Arial", FontSize = 10f,
        Bold = false, Italic = false, Underline = false,
        TextColor = "#FF222222", BackgroundColor = "#00FFFFFF",
        HAlign = TextHorizontalAlignment.Left, VAlign = TextVerticalAlignment.Top,
        PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0, PaddingLeft = 0,
        LineSpacing = 1.2f,
    };

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
        // Šířky sloupců předpočítáme jednou — reuse ve všech třech metodách render
        var colWidths = visibleColumns.Select(c =>
            c.WidthMode == ColumnWidthMode.Percent
                ? tableRect.Width * c.Width / 100f
                : ctx.Px(c.Width)).ToArray();
        float currentY = tableRect.Top;

        // Záhlaví
        if (ShowHeader)
            currentY = RenderHeader(ctx, visibleColumns, colWidths, tableRect, currentY, borderPaint, sepPaint);

        // Datové řádky
        IReadOnlyList<DataContext> dataRows = [];
        if (Mode == TableMode.Fixed)
            currentY = RenderFixedRows(ctx, visibleColumns, colWidths, tableRect, currentY, borderPaint, sepPaint);
        else
        {
            dataRows = ctx.Data?.GetRows(DataSource ?? "") ?? [];
            currentY = RenderDynamicRows(ctx, visibleColumns, colWidths, tableRect, currentY, sepPaint, dataRows);
        }

        // Souhrnné řádky (zápatí s agregacemi)
        if (SummaryRows is { Count: > 0 })
            RenderSummaryRows(ctx, visibleColumns, colWidths, tableRect, currentY, borderPaint, sepPaint, dataRows);

        // Vnější rámeček
        if (BorderStyle != TableBorderStyle.None)
            ctx.Canvas.DrawRect(tableRect, borderPaint);

    }

    private float RenderHeader(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float currentY,
        SKPaint borderPaint, SKPaint sepPaint)
    {
        float headerPx = ctx.Px(HeaderHeight);
        var rs = (HeaderStyle ?? new TextStyle()).Resolve(DefaultHeaderParent);

        using var hBg   = new SKPaint { Color = rs.ParsedBackgroundColor };
        using var hText = rs.CreateTextPaint();
        using var hFont = new SKFont(
            SKTypeface.FromFamilyName(rs.FontFamily, rs.ToSKFontStyle()) ?? SKTypeface.Default,
            headerPx * 0.55f);

        ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + headerPx), hBg);

        float colX = tableRect.Left;

        for (int i = 0; i < cols.Count; i++)
        {
            var   col  = cols[i];
            float colW = colWidths[i];

            // Per-column záhlaví styl (cascade: column → table → default)
            if (col.HeaderStyle is not null)
            {
                var colRs = col.HeaderStyle.Resolve(rs);
                using var colHBg = colRs.CreateBgPaint();
                if (colHBg is not null)
                    ctx.Canvas.DrawRect(new SKRect(colX, currentY, colX + colW, currentY + headerPx), colHBg);

                using var colHText = colRs.CreateTextPaint();
                ctx.Canvas.DrawText(col.Header, colX + 4, currentY + headerPx * 0.72f, hFont, colHText);
            }
            else
            {
                ctx.Canvas.DrawText(col.Header, colX + 4, currentY + headerPx * 0.72f, hFont, hText);
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + headerPx, sepPaint);

            colX += colW;
        }

        ctx.Canvas.DrawLine(tableRect.Left, currentY + headerPx, tableRect.Right, currentY + headerPx, borderPaint);
        return currentY + headerPx;
    }

    private float RenderFixedRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float startY,
        SKPaint borderPaint, SKPaint sepPaint)
    {
        if (Rows is null || Rows.Count == 0) return startY;

        float rowPx = ctx.Px(RowHeight);
        using var rFont = new SKFont(SKTypeface.Default, rowPx * 0.55f);
        using var rText = new SKPaint { Color = new SKColor(0xFF222222), IsAntialias = true };

        // Alternating color předparsovaná jednou — neopakovat Parse() pro každý řádek
        SKColor? altColor = AlternatingRowColor is not null ? SKColor.Parse(AlternatingRowColor) : null;
        using var altBgPaint = altColor.HasValue ? new SKPaint { Color = altColor.Value } : null;

        float currentY = startY;
        int   visibleIdx = 0;   // index viditelných řádků — pro střídání barev
        for (int rowIdx = 0; rowIdx < Rows.Count; rowIdx++)
        {
            var row = Rows[rowIdx];

            // Podmíněná viditelnost — řádky s nesplněnou podmínkou se přeskočí
            if (!ExpressionEvaluator.IsVisible(row.VisibilityExpression, ctx.Data)) continue;

            // Střídání barev — podle pořadí viditelných řádků, ne původního indexu
            if (altBgPaint is not null && visibleIdx % 2 == 1)
                ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + rowPx), altBgPaint);

            // Vlastní pozadí řádku
            if (row.BackgroundColor is not null)
            {
                using var rowBg = new SKPaint { Color = SKColor.Parse(row.BackgroundColor) };
                ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + rowPx), rowBg);
            }

            float colX = tableRect.Left;
            for (int colIdx = 0; colIdx < cols.Count; colIdx++)
            {
                var   col  = cols[colIdx];
                float colW = colWidths[colIdx];

                // Obsah buňky
                string content = "";
                if (colIdx < row.Cells.Count)
                {
                    var cell = row.Cells[colIdx];
                    content = ctx.Data?.ResolvePlaceholders(cell.Content) ?? cell.Content;
                    // Aplikovat formát buňky (má přednost) nebo sloupce
                    var cellFormat = cell.Format ?? col.Format;
                    if (cellFormat is not null)
                        content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                    // Per-cell styl
                    if (cell.Style is not null)
                    {
                        var cellRs = cell.Style.Resolve(DefaultDataParent);
                        using var cellBg = cellRs.CreateBgPaint();
                        if (cellBg is not null)
                            ctx.Canvas.DrawRect(new SKRect(colX, currentY, colX + colW, currentY + rowPx), cellBg);

                        rText.Color = cellRs.ParsedTextColor;
                    }
                    else
                    {
                        rText.Color = new SKColor(0xFF222222);
                    }
                }

                ctx.Canvas.DrawText(content, colX + 4, currentY + rowPx * 0.72f, rFont, rText);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                    ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + rowPx, sepPaint);

                colX += colW;
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
                ctx.Canvas.DrawLine(tableRect.Left, currentY + rowPx, tableRect.Right, currentY + rowPx, sepPaint);

            currentY += rowPx;
            visibleIdx++;
        }
        return currentY;
    }

    private float RenderDynamicRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float startY,
        SKPaint sepPaint, IReadOnlyList<DataContext> rows)
    {
        bool hasRows = rows.Count > 0;
        int renderCount = hasRows ? rows.Count : DesignerPreviewRows;

        float rowPx = ctx.Px(RowHeight);
        using var rFont = new SKFont(SKTypeface.Default, rowPx * 0.55f);
        using var rText = new SKPaint
        {
            Color = hasRows ? new SKColor(0xFF222222) : new SKColor(0xFF999999),
            IsAntialias = true
        };

        // Alternating color předparsovaná jednou
        SKColor? altColor = AlternatingRowColor is not null ? SKColor.Parse(AlternatingRowColor) : null;
        using var altBgPaint = altColor.HasValue ? new SKPaint { Color = altColor.Value } : null;

        float currentY = startY;
        for (int i = 0; i < renderCount && currentY + rowPx <= tableRect.Bottom; i++)
        {
            // Střídání barev
            if (altBgPaint is not null && i % 2 == 1)
                ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + rowPx), altBgPaint);

            float colX = tableRect.Left;
            for (int ci = 0; ci < cols.Count; ci++)
            {
                var   col  = cols[ci];
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
                ctx.Canvas.DrawText(sample, colX + 4, currentY + rowPx * 0.72f, rFont, rText);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                    ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + rowPx, sepPaint);

                colX += colW;
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
                ctx.Canvas.DrawLine(tableRect.Left, currentY + rowPx, tableRect.Right, currentY + rowPx, sepPaint);

            currentY += rowPx;
        }
        return currentY;
    }

    private void RenderSummaryRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float startY,
        SKPaint borderPaint, SKPaint sepPaint,
        IReadOnlyList<DataContext> dataRows)
    {
        if (SummaryRows is null || SummaryRows.Count == 0) return;

        // Oddělovací čára nad souhrnem
        ctx.Canvas.DrawLine(tableRect.Left, startY, tableRect.Right, startY, borderPaint);

        float rowPx = ctx.Px(RowHeight);
        using var sFont = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), rowPx * 0.55f);
        using var sText = new SKPaint { Color = new SKColor(0xFF222222), IsAntialias = true };
        using var sBg   = new SKPaint { Color = new SKColor(0xFFF0F0F0) };

        float currentY = startY;
        for (int rowIdx = 0; rowIdx < SummaryRows.Count; rowIdx++)
        {
            var row = SummaryRows[rowIdx];

            // Pozadí souhrnného řádku
            var bgColor = row.BackgroundColor is not null
                ? SKColor.Parse(row.BackgroundColor)
                : new SKColor(0xFFF0F0F0);
            sBg.Color = bgColor;
            ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + rowPx), sBg);

            float colX = tableRect.Left;
            for (int colIdx = 0; colIdx < cols.Count; colIdx++)
            {
                var   col  = cols[colIdx];
                float colW = colWidths[colIdx];

                string content = "";
                if (colIdx < row.Cells.Count)
                {
                    var cell = row.Cells[colIdx];
                    // 1. Vyhodnotit agregace
                    content = AggregationResolver.Resolve(cell.Content, dataRows);
                    // 2. Vyhodnotit zbývající placeholdery
                    content = ctx.Data?.ResolvePlaceholders(content) ?? content;
                    // 3. Aplikovat formát
                    var cellFormat = cell.Format ?? col.Format;
                    if (cellFormat is not null)
                        content = DataContext.FormatValue(content, cellFormat, ctx.Data?.Culture);

                    // Per-cell styl
                    sText.Color = cell.Style?.TextColor is not null
                        ? SKColor.Parse(cell.Style.TextColor)
                        : new SKColor(0xFF222222);
                }

                ctx.Canvas.DrawText(content, colX + 4, currentY + rowPx * 0.72f, sFont, sText);

                if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                    ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + rowPx, sepPaint);

                colX += colW;
            }

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.RowsOnly)
                ctx.Canvas.DrawLine(tableRect.Left, currentY + rowPx, tableRect.Right, currentY + rowPx, sepPaint);

            currentY += rowPx;
        }
    }

    // ─── PropertyDescriptors ─────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ButtonPropertyDescriptor
        {
            Label       = "Editor",
            Group       = "Tabulka",
            ButtonLabel = "Upravit sloupce / řádky...",
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
        new BoolPropertyDescriptor
        {
            Label = "Tučné záhlaví",
            Group = "Tabulka",
            Get   = () => HeaderStyle?.Bold ?? true,
            Set   = v => { HeaderStyle ??= new TextStyle(); HeaderStyle.Bold = v; }
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
            Label = "Pozadí záhlaví",
            Group = "Tabulka",
            Get   = () => HeaderStyle?.BackgroundColor ?? "#FFE8E8E8",
            Set   = v => { HeaderStyle ??= new TextStyle(); HeaderStyle.BackgroundColor = v; }
        },
        new ColorPropertyDescriptor
        {
            Label = "Střídání řádků",
            Group = "Tabulka",
            Get   = () => AlternatingRowColor ?? "#00FFFFFF",
            Set   = v => AlternatingRowColor = v == "#00FFFFFF" ? null : v
        },

        .. GetPositionDescriptors()
    ];

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
            BorderStyle         = BorderStyle,
            AlternatingRowColor = AlternatingRowColor,
        };
        CopyBaseTo(clone);
        return clone;
    }
}
