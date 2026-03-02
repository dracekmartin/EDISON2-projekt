namespace TemplateDesigner.Models.Sections;

/// <summary>
/// Sekce s opakujícími se řádky tabulky generovanými z dat.
/// DesignerHeight je null — dynamická sekce, výška závisí na počtu řádků.
/// Standardně se rozděluje přes více stránek (SplitAcrossPages).
/// </summary>
public class SectionTable : Section
{
    public SectionTable()
    {
        Name = "Tabulka";
        PrintBehavior = SectionPrintBehavior.Once;
        OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages;
        // DesignerHeight = null — dynamická sekce
    }

    protected override float DefaultPlaceholderHeight => 50f;
}
