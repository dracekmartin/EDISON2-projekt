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

    public string? HeaderBackgroundColor { get; set; }
    public string? HeaderTextColor       { get; set; }

    public TableColumn Clone() => new()
    {
        Header               = Header,
        Width                = Width,
        WidthMode            = WidthMode,
        Field                = Field,
        Format               = Format,
        Alignment            = Alignment,
        Visible              = Visible,
        HeaderBackgroundColor = HeaderBackgroundColor,
        HeaderTextColor      = HeaderTextColor
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

    public string? Format      { get; set; }
    public TextHorizontalAlignment? Alignment  { get; set; }
    public string? FontWeight  { get; set; }
    public string? TextColor   { get; set; }
    public string? BackgroundColor { get; set; }

    public TableCell Clone() => new()
    {
        Content         = Content,
        Format          = Format,
        Alignment       = Alignment,
        FontWeight      = FontWeight,
        TextColor       = TextColor,
        BackgroundColor = BackgroundColor
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

    public string HeaderFontWeight { get; set; } = "Bold";

    public TableBorderStyle BorderStyle { get; set; } = TableBorderStyle.Outer;

    /// <summary>Barva střídajících se řádků (#AARRGGBB). Null = bez střídání.</summary>
    public string? AlternatingRowColor { get; set; }

    public string? HeaderBackgroundColor { get; set; }
    public string? HeaderTextColor       { get; set; }

    // ─── Designer helpers ────────────────────────────────────────────

    /// <summary>Počet ukázkových řádků zobrazených v designeru pro Dynamic mód.</summary>
    private int DesignerPreviewRows => 3;

    protected override float DesignerPlaceholderHeight =>
        Mode == TableMode.Fixed && Rows is { Count: > 0 }
            ? (ShowHeader ? HeaderHeight : 0f) + Rows.Count * RowHeight
            : 40f;

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
        if (Mode == TableMode.Fixed)
            RenderFixedRows(ctx, visibleColumns, colWidths, tableRect, currentY, borderPaint, sepPaint);
        else
            RenderDynamicPlaceholder(ctx, visibleColumns, colWidths, tableRect, currentY, sepPaint);

        // Vnější rámeček
        if (BorderStyle != TableBorderStyle.None)
            ctx.Canvas.DrawRect(tableRect, borderPaint);
    }

    private float RenderHeader(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float currentY,
        SKPaint borderPaint, SKPaint sepPaint)
    {
        float headerPx  = ctx.Px(HeaderHeight);
        var   hBgColor  = HeaderBackgroundColor is not null
            ? SKColor.Parse(HeaderBackgroundColor) : new SKColor(0xFFE8E8E8);
        var   hTxtColor = HeaderTextColor is not null
            ? SKColor.Parse(HeaderTextColor) : new SKColor(0xFF333333);

        using var hBg   = new SKPaint { Color = hBgColor };
        using var hText = new SKPaint { Color = hTxtColor, IsAntialias = true };
        using var hFont = new SKFont(SKTypeface.Default, headerPx * 0.55f);
        if (HeaderFontWeight == "Bold")
            hFont.Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold);

        ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + headerPx), hBg);

        float colX = tableRect.Left;

        for (int i = 0; i < cols.Count; i++)
        {
            var   col  = cols[i];
            float colW = colWidths[i];

            // Vlastní záhlaví pozadí sloupce
            if (col.HeaderBackgroundColor is not null)
            {
                using var colHBg = new SKPaint { Color = SKColor.Parse(col.HeaderBackgroundColor) };
                ctx.Canvas.DrawRect(new SKRect(colX, currentY, colX + colW, currentY + headerPx), colHBg);
            }

            ctx.Canvas.DrawText(col.Header, colX + 4, currentY + headerPx * 0.72f, hFont, hText);

            if (BorderStyle is TableBorderStyle.All or TableBorderStyle.ColumnsOnly)
                ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + headerPx, sepPaint);

            colX += colW;
        }

        ctx.Canvas.DrawLine(tableRect.Left, currentY + headerPx, tableRect.Right, currentY + headerPx, borderPaint);
        return currentY + headerPx;
    }

    private void RenderFixedRows(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float startY,
        SKPaint borderPaint, SKPaint sepPaint)
    {
        if (Rows is null || Rows.Count == 0) return;

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
                        content = DataContext.FormatValue(content, cellFormat);

                    // Vlastní barva buňky
                    if (cell.BackgroundColor is not null)
                    {
                        using var cellBg = new SKPaint { Color = SKColor.Parse(cell.BackgroundColor) };
                        ctx.Canvas.DrawRect(new SKRect(colX, currentY, colX + colW, currentY + rowPx), cellBg);
                    }

                    // Barva textu buňky
                    if (cell.TextColor is not null)
                        rText.Color = SKColor.Parse(cell.TextColor);
                    else
                        rText.Color = new SKColor(0xFF222222);
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
    }

    private void RenderDynamicPlaceholder(RenderContext ctx, List<TableColumn> cols, float[] colWidths,
        SKRect tableRect, float startY, SKPaint sepPaint)
    {
        // Při preview (mock data) použij reálné řádky, jinak zobraz placeholdery
        var rows = ctx.Data?.GetRows(DataSource ?? "") ?? [];
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
                        sample = DataContext.FormatValue(sample, col.Format);
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
    }

    // ─── PropertyDescriptors ─────────────────────────────────────────

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ButtonPropertyDescriptor
        {
            Label       = "Editor",
            Group       = "Tabulka",
            ButtonLabel = "Upravit sloupce / řádky...",
            // OnClick je nastaveno externě z ElementPropertiesWindow
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
            Get   = () => HeaderFontWeight == "Bold",
            Set   = v => HeaderFontWeight = v ? "Bold" : "Normal"
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
            Get   = () => HeaderBackgroundColor ?? "#FFE8E8E8",
            Set   = v => HeaderBackgroundColor = v
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

    public override TemplateElement Clone() => new TableElement
    {
        Id                   = Guid.NewGuid(),
        X                    = X, Y = Y, Width = Width, Height = Height,
        ZIndex               = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties     = CustomProperties is null ? null : new(CustomProperties),
        Mode                 = Mode,
        DataSource           = DataSource,
        Columns              = Columns.Select(c => c.Clone()).ToList(),
        Rows                 = Rows?.Select(r => r.Clone()).ToList(),
        RowHeight            = RowHeight,
        ShowHeader           = ShowHeader,
        HeaderHeight         = HeaderHeight,
        HeaderFontWeight     = HeaderFontWeight,
        BorderStyle          = BorderStyle,
        AlternatingRowColor  = AlternatingRowColor,
        HeaderBackgroundColor = HeaderBackgroundColor,
        HeaderTextColor      = HeaderTextColor
    };
}
