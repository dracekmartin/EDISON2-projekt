namespace TemplateDesigner.Models.Sections;

/// <summary>Tenká sekce pro vizuální oddělení — typicky čára nebo prázdné místo.</summary>
public class SectionSeparator : Section
{
    /// <summary>Výška oddělovače v mm.</summary>
    public float Height { get; set; } = 5f;

    public SectionSeparator()
    {
        Name = "Oddělovač";
        DesignerHeight = 5f;
    }
}
