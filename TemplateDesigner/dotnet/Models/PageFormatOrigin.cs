namespace TemplateDesigner.Models;

/// <summary>
/// Výchozí tisková plocha šablony a to, jak je od ní obsah přeškálovaný a posunutý.
/// </summary>
/// <remarks>
/// Drží se kvůli tomu, aby změna formátu stránky byla <b>vratná</b>: každý přepočet se počítá od
/// téhle plochy, ne od aktuálního stavu, takže návrat na původní formát vyjde přesně na původní
/// hodnoty. Bez toho se dvě otočení nevyruší, ale vynásobí - viz <see cref="Template.FormatOrigin"/>.
/// </remarks>
/// <param name="PrintableWidth">Šířka tiskové plochy, ke které se vztahují souřadnice prvků (mm).</param>
/// <param name="PrintableHeight">Výška téže plochy (mm).</param>
/// <param name="Scale">Kolikrát je dnešní obsah zvětšený proti té ploše (1 = původní velikost).</param>
/// <param name="OffsetX">O kolik je dnešní obsah posunutý doprava kvůli vystředění (mm).</param>
public readonly record struct PageFormatOrigin(
    float PrintableWidth, float PrintableHeight, float Scale, float OffsetX)
{
    /// <summary>Výchozí stav pro šablonu, na jejíž formát se ještě nesáhlo.</summary>
    public static PageFormatOrigin From(PageSize ps) =>
        new(ps.PrintableWidth, ps.PrintableHeight, 1f, 0f);
}
