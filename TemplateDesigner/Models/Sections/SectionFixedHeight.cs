namespace TemplateDesigner.Models.Sections;

/// <summary>Sekce s pevnou výškou — nejběžnější typ pro statický obsah.</summary>
public class SectionFixedHeight : Section
{
    public SectionFixedHeight()
    {
        Name = "Sekce";
        DesignerHeight = 30f;
    }
}
