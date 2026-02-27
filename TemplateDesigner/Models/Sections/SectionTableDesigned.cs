namespace TemplateDesigner.Models.Sections;

/// <summary>
/// Tabulka kde uživatel navrhuje jak vypadá jeden řádek.
/// Elements definují layout jednoho datového řádku — print engine je opakuje pro každý záznam.
/// DesignerHeight je null — dynamická sekce.
/// </summary>
public class SectionTableDesigned : Section
{
    /// <summary>Klíč datového zdroje pro řádky, např. "JIZDNI_RAD".</summary>
    public string DataSource { get; set; } = "";

    /// <summary>Výška jednoho řádku v mm (= výška sekce v designeru = výška pro jeden průchod dat).</summary>
    public float RowHeight { get; set; } = 8f;

    public SectionTableDesigned()
    {
        Name = "Tabulka (vlastní design)";
        PrintBehavior = SectionPrintBehavior.Once;
        OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages;
    }

    protected override float DefaultPlaceholderHeight => 50f;
}
