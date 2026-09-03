using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Názvy prvků tak, jak je uživatel vidí v paletě a v nabídce vkládání. Jediné místo, odkud si je
/// berou hlášky - jiné jméno v hlášce než na tlačítku znamená, že uživatel neví, o který prvek jde.
/// </summary>
/// <remarks>
/// ⚠️ Musí souhlasit s paletou (<c>MainWindow.xaml</c>) a s nabídkou vkládání
/// (<c>BuildAddElementMenu</c>); hlídá to <c>ElementNamesTests</c>. Dva prvky se 1. 9. přejmenovaly
/// („Mřížka odjezdů" → „Minuty odjezdů", „Legenda poznámek" → „Poznámky") a jméno bylo tehdy
/// opsané na třech místech - proto tahle třída.
/// </remarks>
public static class ElementNames
{
    /// <summary>Název prvku pro uživatele. Neznámý typ dostane obecné „prvek", aby hláška držela.</summary>
    public static string ForUser(TemplateElement el) => el switch
    {
        TextElement            => "Text",
        RectangleElement       => "Obdélník",
        LineElement            => "Čára",
        ImageElement           => "Obrázek",
        BarcodeElement         => "Kód",
        DynamicTableElement    => "Tabulka (z dat)",
        TableElement           => "Tabulka (pevná)",
        StopListElement        => "Seznam zastávek",
        DepartureGridElement   => "Minuty odjezdů",
        NotesLegendElement     => "Poznámky",
        LineTableElement       => "Linkový JŘ",
        _                      => "prvek",
    };
}
