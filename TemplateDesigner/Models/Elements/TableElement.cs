using SkiaSharp;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Tabulkový element s dynamickými řádky generovanými za běhu z dat.
/// Height je vždy null — výšku vypočítá print engine podle počtu řádků.
/// V designeru se zobrazuje s placeholder výškou a ukázkovými řádky.
/// </summary>
public class TableElement : TemplateElement
{
    /// <summary>Klíč datového zdroje pro řádky tabulky, např. "JIZDNI_RAD".</summary>
    public string DataSource { get; set; } = "";

    /// <summary>Výška jednoho datového řádku v mm.</summary>
    public float RowHeight { get; set; } = 6f;

    public List<TableColumn> Columns { get; set; } = [];

    public bool ShowHeader { get; set; } = true;

    /// <summary>Výška řádku záhlaví v mm.</summary>
    public float HeaderHeight { get; set; } = 8f;

    public string HeaderFontWeight { get; set; } = "Bold";

    /// <summary>Počet ukázkových řádků zobrazených v designeru.</summary>
    [JsonIgnore]
    public int DesignerPreviewRows => 3;

    protected override float DesignerPlaceholderHeight => 40f;

    public override void Render(RenderContext ctx)
    {
        if (Width <= 0 || Columns.Count == 0) return;
        var tableRect = ctx.ToCanvasRect(X, Y, Width, EffectiveDesignerHeight);

        using var bgPaint     = new SKPaint { Color = SKColors.White };
        using var borderPaint = new SKPaint { Color = new SKColor(0xFF999999), IsStroke = true, StrokeWidth = 0.5f };
        using var sepPaint    = new SKPaint { Color = new SKColor(0xFFDDDDDD), StrokeWidth = 0.5f };

        ctx.Canvas.DrawRect(tableRect, bgPaint);

        float currentY = tableRect.Top;

        // Záhlavní řádek
        if (ShowHeader)
        {
            float headerPx = ctx.Px(HeaderHeight);
            using var hBg   = new SKPaint { Color = new SKColor(0xFFE8E8E8) };
            using var hFont = new SKFont(SKTypeface.Default, headerPx * 0.55f);
            using var hText = new SKPaint { Color = new SKColor(0xFF333333), IsAntialias = true };
            ctx.Canvas.DrawRect(new SKRect(tableRect.Left, currentY, tableRect.Right, currentY + headerPx), hBg);

            float colX = tableRect.Left;
            foreach (var col in Columns)
            {
                float colW = ctx.Px(col.Width);
                ctx.Canvas.DrawText(col.Header, colX + 4, currentY + headerPx * 0.72f, hFont, hText);
                ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + headerPx, sepPaint);
                colX += colW;
            }
            ctx.Canvas.DrawLine(tableRect.Left, currentY + headerPx, tableRect.Right, currentY + headerPx, borderPaint);
            currentY += headerPx;
        }

        // Vzorové datové řádky
        float rowPx = ctx.Px(RowHeight);
        using var rFont = new SKFont(SKTypeface.Default, rowPx * 0.55f);
        using var rText = new SKPaint { Color = new SKColor(0xFF888888), IsAntialias = true };

        for (int i = 0; i < DesignerPreviewRows && currentY + rowPx <= tableRect.Bottom; i++)
        {
            float colX = tableRect.Left;
            foreach (var col in Columns)
            {
                float colW = ctx.Px(col.Width);
                string sample = ctx.Data?.GetString(col.Field) ?? $"({col.Field})";
                ctx.Canvas.DrawText(sample, colX + 4, currentY + rowPx * 0.72f, rFont, rText);
                ctx.Canvas.DrawLine(colX + colW, currentY, colX + colW, currentY + rowPx, sepPaint);
                colX += colW;
            }
            ctx.Canvas.DrawLine(tableRect.Left, currentY + rowPx, tableRect.Right, currentY + rowPx, sepPaint);
            currentY += rowPx;
        }

        ctx.Canvas.DrawRect(tableRect, borderPaint);
    }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new StringPropertyDescriptor { Label = "Datový zdroj",  Group = "Tabulka", Get = () => DataSource,   Set = v => DataSource = v },
        new FloatPropertyDescriptor  { Label = "Výška řádku",   Group = "Tabulka", Get = () => RowHeight,    Set = v => RowHeight = v,    Min = 2, Max = 50, Unit = "mm" },
        new BoolPropertyDescriptor   { Label = "Záhlaví",       Group = "Tabulka", Get = () => ShowHeader,   Set = v => ShowHeader = v },
        new FloatPropertyDescriptor  { Label = "Výška záhlaví", Group = "Tabulka", Get = () => HeaderHeight, Set = v => HeaderHeight = v, Min = 2, Max = 50, Unit = "mm" },

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone() => new TableElement
    {
        Id = Guid.NewGuid(),
        X = X, Y = Y, Width = Width, Height = Height,
        ZIndex = ZIndex, IsLocked = IsLocked,
        VisibilityExpression = VisibilityExpression,
        CustomProperties = CustomProperties is null ? null : new(CustomProperties),
        DataSource = DataSource,
        RowHeight = RowHeight,
        Columns = Columns.Select(c => c.Clone()).ToList(),
        ShowHeader = ShowHeader,
        HeaderHeight = HeaderHeight,
        HeaderFontWeight = HeaderFontWeight
    };
}

public class TableColumn
{
    public string Header { get; set; } = "";

    /// <summary>Šířka sloupce v mm.</summary>
    public float Width { get; set; } = 30f;

    /// <summary>Název datového pole, např. "CAS" nebo "LINKA".</summary>
    public string Field { get; set; } = "";

    public TextHorizontalAlignment Alignment { get; set; } = TextHorizontalAlignment.Left;

    public TableColumn Clone() => new()
    {
        Header = Header,
        Width = Width,
        Field = Field,
        Alignment = Alignment
    };
}
