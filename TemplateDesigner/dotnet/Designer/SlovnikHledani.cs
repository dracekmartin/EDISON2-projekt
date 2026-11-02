using EDControls.EDInputForm;

namespace TemplateDesigner.Designer;

/// <summary>
/// Synonyma pro hledání ve formulářích návrháře: slovo, které uživatel napíše, a popisky, které najde navíc
/// („písmo" → Font, Velikost). Platí jen v oknech návrháře (dosazuje je `App.OnAnyWindowLoaded`), ostatní
/// formuláře EDISONu hledají jako dřív. Hodnoty míří na popisky a skupiny formulářů prvků - hlídá
/// `SlovnikHledaniTests`, aby přejmenovaný popisek nenechal synonymum bez cíle.
/// </summary>
public static class SlovnikHledani
{
    internal static readonly IReadOnlyDictionary<string, string[]> Slova = new Dictionary<string, string[]>
    {
        ["písmo"] = ["Font", "Velikost", "Tučné", "Kurzíva"],
        ["řez"] = ["Tučné", "Kurzíva"],
        ["podbarvení"] = ["Barva pozadí"],
        ["výplň"] = ["Barva pozadí"],
        ["čára"] = ["Rámeček", "Mřížka", "Tloušťka", "Čáry"],
        ["čáry"] = ["Rámeček", "Mřížka", "Tloušťka"],
        ["obrys"] = ["Rámeček"],
        ["ohraničení"] = ["Rámeček"],
        ["okraj"] = ["Rámeček"],
        ["odsazení"] = ["Vnitřní okraj"],
        ["skrýt"] = ["Podmínka viditelnosti"],
        ["schovat"] = ["Podmínka viditelnosti"],
        ["zámek"] = ["Uzamčeno"],
        ["zamknout"] = ["Uzamčeno"],
        ["překrytí"] = ["Vrstva"],
        ["dopředu"] = ["Vrstva"],
        ["dozadu"] = ["Vrstva"],
        ["otočení"] = ["Rotace"],
        ["rozměr"] = ["Šířka", "Výška"],
        ["poloha"] = ["Pozice"],
        ["hlavička"] = ["Záhlaví"],
        ["nadpis"] = ["Záhlaví"],
    };

    public static readonly SearchSynonyms Slovnik = new(Slova);
}
