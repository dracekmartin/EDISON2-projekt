namespace TemplateDesigner.Models;

/// <summary>
/// Souhrn toho, co vlastní sloupce vývěsky nevzaly - odjezdy mimo všechny sloupce
/// (NEZARAZENE_SPOJE) a sloupce, jejichž období leží mimo platnost jízdního řádku
/// (OBDOBI_MIMO_PLATNOST). Obojí je neviditelná vada: v prvním případě chybí minuta,
/// ve druhém zůstane prázdný sloupec bez vysvětlení. Ukazuje se v návrháři (status bar)
/// i při tisku/exportu.
/// </summary>
public static class DepartureColumnWarnings
{
    /// <summary>Text upozornění, nebo null když je vše v pořádku.</summary>
    public static string? Summarize(IReadOnlyList<DataContext> pages) =>
        Seznam(pages) is { Count: > 0 } s ? string.Join("; ", s.Select(h => h.Text)) : null;

    /// <summary>
    /// Odjezdy mimo sloupce na vývěsce chybí - chyba; sloupec s obdobím mimo platnost zůstane
    /// prázdný, ale nic nechybí - upozornění (rozhodnutí Jan 24. 9.).
    /// </summary>
    public static List<Hlaseni> Seznam(IReadOnlyList<DataContext> pages)
    {
        var casti = new List<Hlaseni>();

        var times = Hodnoty(pages, "NEZARAZENE_SPOJE");
        if (times.Count > 0)
        {
            // Počítají se SPOJE, ne odjezdy: tentýž spoj vypadne na každé zastávce linky zvlášť,
            // takže počet časů udělá ze tří problémů jedenatřicet (nález Jan 10. 8. na KOVED 802192).
            // ⚠️ V závorce musí být totéž, co se počítá - časy odjezdů se s počtem spojů rozcházejí
            // (jeden spoj = tolik časů, na kolika zastávkách staví) a hláška pak vypadá rozbitě.
            // Bez čísel spojů (starší server) se počítají i vypisují časy.
            var trips = Hodnoty(pages, "NEZARAZENE_SPOJE_CISLA");
            var jePodleSpoju = trips.Count > 0;
            var pocet = jePodleSpoju ? trips.Count : times.Count;
            var podstatne = jePodleSpoju
                ? pocet == 1 ? "spoj" : pocet <= 4 ? "spoje" : "spojů"
                : pocet == 1 ? "odjezd" : pocet <= 4 ? "odjezdy" : "odjezdů";
            // sloveso se v češtině shoduje jen u 2-4 (dva spoje se nevejdou × pět spojů se nevejde)
            var slovesa = pocet is >= 2 and <= 4 ? "se nevejdou" : "se nevejde";
            var nebude = pocet is >= 2 and <= 4 ? "nebudou" : "nebude";
            // vzestupně, ne v pořadí, v jakém je potkaly stránky - jinak to vypadá jako jiná čísla
            var vzorek = jePodleSpoju
                ? "č. " + string.Join(", ", Serazene(trips).Take(6)) + (trips.Count > 6 ? ", …" : "")
                : string.Join(", ", times.Take(4)) + (times.Count > 4 ? ", …" : "");
            casti.Add(new Hlaseni(ZavaznostHlaseni.Chyba,
                $"{pocet} {podstatne} {slovesa} do žádného sloupce a na vývěsce {nebude} ({vzorek})"));
        }

        var sloupce = Hodnoty(pages, "OBDOBI_MIMO_PLATNOST");
        if (sloupce.Count > 0)
        {
            var vyjmenovani = string.Join(", ", sloupce.Select(s => $"„{s}“"));
            casti.Add(new Hlaseni(ZavaznostHlaseni.Upozorneni, sloupce.Count == 1
                ? $"období sloupce {vyjmenovani} leží mimo platnost jízdního řádku - sloupec zůstane prázdný"
                : $"období sloupců {vyjmenovani} leží mimo platnost jízdního řádku - zůstanou prázdné"));
        }

        return casti;
    }

    /// <summary>Čísla spojů vzestupně; nečíselné hodnoty (kdyby je server někdy poslal) až za nimi
    /// abecedně, aby řazení nespadlo.</summary>
    private static IEnumerable<string> Serazene(List<string> hodnoty) => hodnoty
        .OrderBy(h => int.TryParse(h, out var n) ? n : int.MaxValue)
        .ThenBy(h => h, StringComparer.Ordinal);

    private static List<string> Hodnoty(IReadOnlyList<DataContext> pages, string klic) => pages
        .Select(p => p.GetValue(klic)?.ToString())
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .SelectMany(v => v!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .Distinct()
        .ToList();
}
