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
    /// <summary>
    /// Název prvku i s tím, podle čeho ho uživatel pozná na plátně - u textu jeho obsah (hlášky,
    /// řádek výběru). Jedno datové pole nebo krátký text celý, delší první tři slova
    /// (rozhodnutí Jan 24. 9.); textů je v šabloně obvykle víc a samotné „Text" je nerozliší.
    /// </summary>
    public static string SPopisem(TemplateElement el)
    {
        if (el is not TextElement { Text: { } obsah } || string.IsNullOrWhiteSpace(obsah)) return ForUser(el);

        string jednim = string.Join(" ", obsah.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (jednim.Length > MaxDelkaPopisu)
        {
            // Za slovo se počítá jen to, co nese písmeno nebo datové pole - samotná „:" nebo „-" ne.
            var slova = jednim.Split(' ');
            var vzate = new List<string>();
            int vyznamnych = 0;
            foreach (var slovo in slova)
            {
                if (vyznamnych == 3) break;
                vzate.Add(slovo);
                if (slovo.Any(char.IsLetter) || slovo.Contains('{')) vyznamnych++;
            }
            jednim = string.Join(" ", vzate) + (vzate.Count < slova.Length ? "…" : "");
        }
        return $"{ForUser(el)} „{jednim}“";
    }

    /// <summary>Do kolika znaků se text prvku vypíše celý.</summary>
    private const int MaxDelkaPopisu = 30;

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
