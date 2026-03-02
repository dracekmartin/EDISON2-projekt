namespace TemplateDesigner.Models.Sections;

/// <summary>Tenká sekce pro vizuální oddělení — typicky čára nebo prázdné místo.</summary>
public class SectionSeparator : Section
{
    public SectionSeparator()
    {
        Name = "Oddělovač";
        DesignerHeight = 5f;
    }
}
