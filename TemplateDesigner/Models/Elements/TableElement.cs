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
        // TODO: implementovat renderování tabulky
    }

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
