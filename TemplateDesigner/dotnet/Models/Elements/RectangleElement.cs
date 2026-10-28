namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Obdélník = plocha s barvou pozadí a obrysem. Obrys je rámeček prvku (společný všem prvkům, i s rohy);
/// do 28. 10. měl obdélník navíc vlastní okraj se stejným tvarem - dvě nastavení pro jednu čáru,
/// převedeno migrací 1.18.0 (<see cref="Serialization.MigrationObdelnikRamecek"/>).
/// </summary>
public class RectangleElement : TemplateElement
{
    /// <summary>Nový obdélník má černý obrys 0,5 mm, jako měl dřív vlastním okrajem.</summary>
    public RectangleElement()
    {
        BorderColor = "#FF000000";
        BorderWidth = 0.5f;
    }

    // Výplň (BackgroundColor) kreslí RenderBackgroundPass a obrys RenderBorderPass, obojí s rohy.
    public override void Render(RenderContext ctx) { }

    public override IReadOnlyList<PropertyDescriptor> GetPropertyDescriptors() =>
    [
        new ColorPropertyDescriptor { Label = "Barva pozadí", Group = "Obdélník", Get = () => BackgroundColor, Set = v => BackgroundColor = v },
        // Obrys (Barva a Tloušťka rámečku) a rohy jsou společné pro všechny prvky → GetPositionDescriptors.

        .. GetPositionDescriptors()
    ];

    public override TemplateElement Clone()
    {
        var clone = new RectangleElement();
        CopyBaseTo(clone);   // vč. rámečku, rohů a pozadí
        return clone;
    }
}
