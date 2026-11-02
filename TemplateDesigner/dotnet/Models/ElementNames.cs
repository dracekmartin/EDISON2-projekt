using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Models;

/// <summary>
/// Názvy prvků tak, jak je uživatel vidí v paletě a v nabídce vkládání - z <see cref="ElementCatalog"/>,
/// ze kterého se staví i paleta a nabídka. Hlášky je berou odtud: jiné jméno v hlášce než na tlačítku
/// znamená, že uživatel neví, o který prvek jde (1. 9. bylo jméno opsané na třech místech).
/// </summary>
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

    /// <summary>
    /// Titulek okna vlastností prvku: „Vlastnosti - {název z palety}", jako záložka „Vlastnosti"
    /// v levém panelu. Do 26. 10. měla okna tři tvary („Vlastnosti textu", „Odjezdy - nastavení",
    /// „Linkový JŘ - nastavení" podle agendy místo prvku), `B194` E2.
    /// </summary>
    public static string TitulekVlastnosti(TemplateElement el) => PredponaTitulkuVlastnosti + ForUser(el);

    public const string PredponaTitulkuVlastnosti = "Vlastnosti - ";

    /// <summary>Do kolika znaků se text prvku vypíše celý.</summary>
    private const int MaxDelkaPopisu = 30;

    /// <summary>Název prvku pro uživatele. Neznámý typ dostane obecné „prvek", aby hláška držela.</summary>
    public static string ForUser(TemplateElement el) => ElementCatalog.Find(el)?.Name ?? "prvek";

    /// <summary>Totéž podle typu ze serializace - pro místa, kde je po ruce jen řetězec
    /// (katalog prvků pro asistenta). Neznámý typ se vrátí, jak přišel.</summary>
    public static string ForType(string type) => ElementCatalog.Find(type)?.Name ?? type;
}
