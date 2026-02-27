namespace TemplateDesigner.Models.Sections;

/// <summary>Záhlaví — standardně se opakuje na každé stránce.</summary>
public class SectionHeader : Section
{
    public SectionHeader()
    {
        Name = "Záhlaví";
        PrintBehavior = SectionPrintBehavior.OnEveryPage;
        OverflowBehavior = SectionOverflowBehavior.KeepTogether;
        DesignerHeight = 25f;
    }
}
