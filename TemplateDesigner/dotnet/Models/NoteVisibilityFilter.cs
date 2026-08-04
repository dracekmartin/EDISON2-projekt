using System.Text.Json.Nodes;

namespace TemplateDesigner.Models;

/// <summary>
/// Render-time filtr viditelnosti poznámek zastávkového JŘ (AD). Caché emituje nadmnožinu
/// (všechny anotace u minut i řádky legendy, otagované <c>KATEGORIE</c> + <c>V_ROZSAHU_NEPOZNAMKOVAT</c>);
/// tady se podle nastavení šablony schová příslušná kategorie - ale jen u anotací, které spadají do
/// rozsahu „Spoje, nepoznámkovat" (<c>V_ROZSAHU_NEPOZNAMKOVAT=1</c>).
///
/// Legenda se prořezává podle kategorie + přítomnosti znaku: řádek legendy se odebere jen když jeho
/// kategorie patří mezi schovávané A jeho znak už není u žádné viditelné anotace (sdílené znaky, které
/// zůstaly mimo rozsah, se zachovají). Kategorie navíc chrání řádky bez anotace u minut (např.
/// „x - na znamení"), aby se neprořezaly omylem.
///
/// Filtr je agenda-agnostický: anotace bez těchto tagů (MHD) se nikdy neschovají.
/// </summary>
/// <summary>
/// Počitadlo skutečně skrytých poznámek pro jeden průchod filtrem. Slouží viditelnému signálu v UI -
/// bez něj nejde rozeznat „žádné poznámky nejsou" od „poznámky jsou, ale nastavení je schovalo".
/// Počítá se skutečnost, ne jen zapnutý parametr: rozsah „Spoje, nepoznámkovat" nemusí mít v datech
/// firmy vůbec záznamy, a signál z parametru samotného by pak lhal.
/// </summary>
public sealed class NoteSuppressionCounter
{
    /// <summary>Počet skrytých značek u minut (napříč všemi zpracovanými stránkami).</summary>
    public int HiddenAnnotations { get; private set; }

    /// <summary>Počet skrytých řádků legendy.</summary>
    public int HiddenLegendRows { get; private set; }

    public bool AnythingHidden => HiddenAnnotations > 0 || HiddenLegendRows > 0;

    internal void CountAnnotation() => HiddenAnnotations++;

    internal void CountLegendRow() => HiddenLegendRows++;
}

public static class NoteVisibilityFilter
{
    private const string OdjezdyKey  = "ODJEZDY";
    private const string PoznamkyKey = "POZNAMKY";

    /// <summary>Název parametru sestavy (GetParameters), který řídí filtr rozsahu „nepoznámkovat".</summary>
    public const string ParamName = "SCHOVAT_V_ROZSAHU";


    /// <summary>Kategorie legendy pro souhrn „všechny spoje bezbariérové" (Caché tag).</summary>
    private const string AggBezbarierKat = NoteCategories.BezbarierAgregat;

    /// <summary>Kategorie legendy pro „× na znamení" (Caché tag). Skryje se, když StopList značku netiskne (#7161 D5).</summary>
    private const string AtributZastavkyKat = NoteCategories.AtributZastavky;

    /// <summary>Kategorie legendy pro přestupy vlak/autobus (Caché tag). Skryje se, když je žádný
    /// StopList netiskne - piktogram bez vysvětlení je nečitelný, vysvětlení bez piktogramu matoucí.</summary>
    private const string PrestupKat = NoteCategories.Prestup;

    /// <summary>Kategorie atributů odjezdu (kola, místenka) - zapínají se v prvku poznámek a filtr
    /// podle nich prořezává značku u minuty i řádek legendy naráz.</summary>
    private const string KolaKat = NoteCategories.OdjezdKola;
    private const string MistenkyKat = NoteCategories.OdjezdMistenka;

    /// <summary>Převede hodnotu parametru sestavy na režim. Neznámé / „Ruční komentáře" = default (legacy).</summary>
    public static NoteRangeSuppression ParseMode(object? paramValue) => paramValue?.ToString() switch
    {
        "Automatické (rozdíly tras)" => NoteRangeSuppression.AutoRouteNote,
        "Obojí"                      => NoteRangeSuppression.Both,
        _                            => NoteRangeSuppression.ManualComment,
    };

    /// <summary>Parse bool parametru sestavy (hodnota „true"/„false").</summary>
    public static bool ParseBool(object? paramValue) =>
        paramValue?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// #7161 D5 - má se skrýt legenda „× na znamení"? Ano, když šablona MÁ StopList, ale žádný z nich
    /// netiskne značku na znamení (<c>ShowSignalStopIndicator</c>). Legenda následuje viditelnost značky.
    /// Bez StopListu nevíme záměr → neskrývat.
    /// </summary>
    public static bool ShouldHideNaZnameniLegend(Template template)
    {
        var stopLists = template.Sections
            .SelectMany(s => s.Elements)
            .OfType<Elements.StopListElement>()
            .ToList();
        if (stopLists.Count == 0) return false;
        return !stopLists.Any(e => e.ShowSignalStopIndicator);
    }

    /// <summary>
    /// Má se skrýt legenda přestupů („přestup na vlak / autobus")? Ano, když šablona MÁ StopList, ale
    /// žádný z nich přestupy netiskne (<c>ShowTransferAttributes</c>, default vyp). Legenda následuje
    /// viditelnost značky - jinak by u výchozího nastavení naskočily řádky k piktogramům, které nikde
    /// nejsou. Bez StopListu nevíme záměr → neskrývat (stejně jako u „na znamení").
    /// </summary>
    /// <summary>
    /// Kategorie obsahu, které se v této šabloně **netisknou vůbec** (ani značka, ani vysvětlivka).
    /// Server posílá nadmnožinu otagovanou kategorií a rozhodnutí drží šablona - proto tady, na jednom
    /// místě, a ne parametrem sestavy: je to čistě „co se zobrazí", žádný re-fetch.
    ///
    /// Značka a její vysvětlivka jsou svázané **jednou** volbou v bloku poznámek: filtr podle kategorie
    /// odebere obojí. Nemůže tak vzniknout značka, které nikdo nerozumí, ani vysvětlivka k něčemu,
    /// co na vývěsce není.
    /// </summary>
    public static HashSet<string> HiddenContentCategories(Template template)
    {
        var legends = template.Sections.SelectMany(s => s.Elements)
            .OfType<Elements.NotesLegendElement>().ToList();

        var hidden = new HashSet<string>();
        // „Netiskne se, dokud to někdo nezapne" - proto se neptáme, jestli blok poznámek existuje.
        // Šablona bez něj tedy značky kol ani místenek nemá; je to důsledek toho, že vypínání obsahu
        // drží jeden prvek (rozhodnutí Jan 2026-08-04).
        if (!legends.Any(l => l.ShowZnackaKola)) hidden.Add(KolaKat);
        if (!legends.Any(l => l.ShowZnackaMistenky)) hidden.Add(MistenkyKat);
        // Souhrn bezbariér je naopak výchozím stavem zapnutý; bez bloku poznámek není co zobrazit.
        if (legends.Count > 0 && !legends.Any(l => l.ShowSouhrnBezbarier)) hidden.Add(AggBezbarierKat);

        return hidden;
    }

    public static bool ShouldHidePrestupLegend(Template template)
    {
        var stopLists = template.Sections
            .SelectMany(s => s.Elements)
            .OfType<Elements.StopListElement>()
            .ToList();
        if (stopLists.Count == 0) return false;
        return !stopLists.Any(e => e.ShowTransferAttributes);
    }

    public static List<DataContext> Apply(IReadOnlyList<DataContext> pages, NoteRangeSuppression mode, bool hideAggregateBezbarier = false, bool hideNaZnameniLegend = false, NoteSuppressionCounter? counter = null, bool hidePrestupLegend = false, IReadOnlySet<string>? hiddenContent = null)
    {
        var hide = HideCategories(mode);
        return pages.Select(p => Apply(p, hide, hideAggregateBezbarier, hideNaZnameniLegend, counter, hidePrestupLegend, hiddenContent)).ToList();
    }

    public static DataContext Apply(DataContext page, NoteRangeSuppression mode, bool hideAggregateBezbarier = false, bool hideNaZnameniLegend = false, NoteSuppressionCounter? counter = null, bool hidePrestupLegend = false, IReadOnlySet<string>? hiddenContent = null) =>
        Apply(page, HideCategories(mode), hideAggregateBezbarier, hideNaZnameniLegend, counter, hidePrestupLegend, hiddenContent);

    private static DataContext Apply(DataContext page, HashSet<string> hide, bool hideAggBezbarier, bool hideNaZnameni, NoteSuppressionCounter? counter, bool hidePrestup = false, IReadOnlySet<string>? hiddenContent = null)
    {
        bool maObsah = hiddenContent is { Count: > 0 };
        if (hide.Count == 0 && !hideAggBezbarier && !hideNaZnameni && !hidePrestup && !maObsah) return page;

        var surviving = new HashSet<string>(StringComparer.Ordinal);
        var newRows = new Dictionary<string, IReadOnlyList<DataContext>>(StringComparer.OrdinalIgnoreCase);

        // 1) ODJEZDY - odfiltruj schované anotace a posbírej znaky těch, které zůstaly viditelné.
        foreach (var (key, rows) in page.Rows)
            if (key.Equals(OdjezdyKey, StringComparison.OrdinalIgnoreCase))
                newRows[key] = rows.Select(r => FilterOdjezd(r, hide, surviving, counter, hiddenContent)).ToList();

        // 2) POZNAMKY - prořež legendu (potřebuje surviving z kroku 1).
        foreach (var (key, rows) in page.Rows)
            if (key.Equals(PoznamkyKey, StringComparison.OrdinalIgnoreCase))
                newRows[key] = rows.Where(r => KeepPoznamka(r, hide, surviving, hideAggBezbarier, hideNaZnameni, counter, hidePrestup, hiddenContent)).ToList();

        // 3) ostatní zdroje beze změny.
        foreach (var (key, rows) in page.Rows)
            if (!newRows.ContainsKey(key))
                newRows[key] = rows;

        return new DataContext(page.Values, newRows, page.Culture);
    }

    private static DataContext FilterOdjezd(DataContext odjezd, HashSet<string> hide, HashSet<string> surviving, NoteSuppressionCounter? counter, IReadOnlySet<string>? hiddenContent = null)
    {
        if (odjezd.GetValue("ANOTACE") is not JsonArray anotace) return odjezd;

        var kept = new JsonArray();
        foreach (var node in anotace)
        {
            if (node is null) continue;
            var kategorie = node["KATEGORIE"]?.ToString() ?? "";
            var vRozsahu  = node["V_ROZSAHU_NEPOZNAMKOVAT"]?.ToString() == "1";
            if (vRozsahu && hide.Contains(kategorie)) { counter?.CountAnnotation(); continue; }   // schovat
            // Kategorie vypnutá v šabloně se netiskne vůbec (ne jen v rozsahu „nepoznámkovat").
            if (hiddenContent?.Contains(kategorie) == true) { counter?.CountAnnotation(); continue; }

            kept.Add(node.DeepClone());
            var znak = node["ZNAK"]?.ToString() ?? "";
            if (znak != "") surviving.Add(znak);
        }

        return odjezd.WithExtraValues(new Dictionary<string, object?> { ["ANOTACE"] = kept });
    }

    private static bool KeepPoznamka(DataContext poznamka, HashSet<string> hide, HashSet<string> surviving, bool hideAggBezbarier, bool hideNaZnameni, NoteSuppressionCounter? counter, bool hidePrestup = false, IReadOnlySet<string>? hiddenContent = null)
    {
        var kategorie = poznamka.GetString("KATEGORIE");
        // Vypnutá kategorie obsahu - vysvětlivka odejde spolu se značkou, kterou už nikdo nekreslí.
        if (hiddenContent?.Contains(kategorie) == true) { counter?.CountLegendRow(); return false; }
        if (hideAggBezbarier && kategorie == AggBezbarierKat) { counter?.CountLegendRow(); return false; }   // #7161 D6 - skrýt agregát bezbariér
        // „× na znamení" se nepočítá do signálu - neschovává ho parametr, ale nastavení StopListu
        // (legenda jen následuje viditelnost značky), takže by signál svítil u běžných šablon.
        if (hideNaZnameni && kategorie == AtributZastavkyKat) return false;   // #7161 D5 - StopList netiskne značku „×"
        // Přestupy dtto - neschovává je parametr, ale nastavení StopListu, takže se nepočítají do signálu.
        if (hidePrestup && kategorie == PrestupKat) return false;
        if (!hide.Contains(kategorie)) return true;        // jiná kategorie - nech být
        var znak = poznamka.GetString("ZNAK");
        if (znak == "") return true;                        // text bez znaku (směrové) - nech být
        if (surviving.Contains(znak)) return true;          // znak ještě někde svítí → legendu nech
        counter?.CountLegendRow();
        return false;
    }

    private static HashSet<string> HideCategories(NoteRangeSuppression mode) => mode switch
    {
        NoteRangeSuppression.ManualComment => new(StringComparer.Ordinal) { "rucni-komentar" },
        NoteRangeSuppression.AutoRouteNote => new(StringComparer.Ordinal) { "rozdil-trasy" },
        NoteRangeSuppression.Both          => new(StringComparer.Ordinal) { "rucni-komentar", "rozdil-trasy" },
        _                                  => new(StringComparer.Ordinal),
    };
}
