using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Mřížka tabulkového prvku = **jen vnitřní čáry** (mezi řádky a mezi sloupci).
/// <para>
/// ⚠️ **Vnější obrys sem nepatří** - ten má na starost rámeček elementu (<c>BorderColor</c> /
/// <c>BorderWidth</c>), který má každý prvek. Do 4. 8. 2026 vedly k vnějšímu obrysu dvě cesty
/// (rámeček + vlastní rámec tabulky) a právě ta duplicita byla důvod sjednocení (`B40`).
/// </para>
/// Sdílená kompozicí, ne děděním: tabulky nejsou <c>ScheduleElementBase</c>, takže společný předek
/// by nepomohl. Editace je všude stejná - v Caché přes <c>BaseElementFields.AddGrid</c>.
/// </summary>
public class GridStyle
{
    /// <summary>Čáry mezi řádky.</summary>
    public bool ShowRowLines { get; set; } = true;

    /// <summary>Čáry mezi sloupci.</summary>
    public bool ShowColumnLines { get; set; }

    public string Color { get; set; } = "#FFD0D0D0";

    /// <summary>
    /// Tloušťka v mm. Null = převzít z tloušťky rámečku elementu (dosavadní chování JŘ prvků);
    /// v panelu se to zadává jako 0, protože číselné pole nemá jak vyjádřit „nenastaveno".
    /// </summary>
    public float? Width { get; set; }

    public LineStyle Style { get; set; } = LineStyle.Solid;

    public GridStyle Clone() => (GridStyle)MemberwiseClone();
}
