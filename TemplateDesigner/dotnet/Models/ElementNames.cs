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
        TextElement            => ForType(ElementTypes.Text),
        RectangleElement       => ForType(ElementTypes.Rectangle),
        LineElement            => ForType(ElementTypes.Line),
        ImageElement           => ForType(ElementTypes.Image),
        BarcodeElement         => ForType(ElementTypes.Barcode),
        DynamicTableElement    => ForType(ElementTypes.DynamicTable),
        TableElement           => ForType(ElementTypes.Table),
        StopListElement        => ForType(ElementTypes.StopList),
        DepartureGridElement   => ForType(ElementTypes.DepartureGrid),
        NotesLegendElement     => ForType(ElementTypes.NotesLegend),
        LineTableElement       => ForType(ElementTypes.LineTable),
        _                      => "prvek",
    };

    /// <summary>Totéž podle typu ze serializace - pro místa, kde je po ruce jen řetězec
    /// (katalog prvků pro asistenta). Neznámý typ se vrátí, jak přišel.</summary>
    public static string ForType(string type) => type switch
    {
        ElementTypes.Text          => "Text",
        ElementTypes.Rectangle     => "Obdélník",
        ElementTypes.Line          => "Čára",
        ElementTypes.Image         => "Obrázek",
        ElementTypes.Barcode       => "Kód (QR, čárový)",
        ElementTypes.DynamicTable  => "Tabulka (z dat)",
        ElementTypes.Table         => "Tabulka (pevná)",
        ElementTypes.StopList      => "Seznam zastávek",
        ElementTypes.DepartureGrid => "Minuty odjezdů",
        ElementTypes.NotesLegend   => "Poznámky",
        ElementTypes.LineTable     => "Tabulka spojů",
        _                          => type,
    };
}
