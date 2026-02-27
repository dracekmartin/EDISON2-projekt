namespace TemplateDesigner.Models.Sections;

/// <summary>Zápatí — standardně se opakuje na každé stránce.</summary>
public class SectionFooter : Section
{
    public SectionFooter()
    {
        Name = "Zápatí";
        PrintBehavior = SectionPrintBehavior.OnEveryPage;
        OverflowBehavior = SectionOverflowBehavior.KeepTogether;
        DesignerHeight = 15f;
    }
}
