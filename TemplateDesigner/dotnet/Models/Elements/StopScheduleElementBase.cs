using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TemplateDesigner.Models.Elements;

/// <summary>
/// Základ pro <b>zastávkové</b> JŘ elementy (StopList, DepartureGrid, NotesLegend). Drží DataSource
/// klíče zastávkového formátu (ZASTAVKY/SKUPINY_DNU/ODJEZDY/POZNAMKY), typované cache a <c>Build*</c>
/// helpery, které parsují vnořená pole DataContextu do runtime POCO.
///
/// <para>Generická tabulková vrstva (styly, kaskáda, auto-scale, typeface cache, render helpery) je
/// v <see cref="ScheduleElementBase"/> a dědí ji i ne-zastávkové elementy (LineTable), aniž by tahaly
/// tento zastávkový balast.</para>
/// </summary>
public abstract class StopScheduleElementBase : ScheduleElementBase
{
    /// <summary>
    /// Symboly, které se dle konvence JŘ tisknou jako <b>prostý text</b>, ne jako piktogram
    /// ze symbolového Timetable fontu - „na znamení" (`x` a časové varianty `y`/`z`). Ostatní
    /// jednoznakové symboly (bezbariér `H`/`F`…) jdou přes symbolový font.
    /// Viz <c>.claude/docs/elementy-architektura.md</c> (render-mód „text" vs „font-glyf").
    /// </summary>
    internal static bool SymbolJeText(string znak) => !SymbolMap.JePiktogram(znak);

    /// <summary>Značka se kreslí symbolovým fontem jako piktogram (a jen tehdy se překládá přes
    /// <see cref="SymbolGlyph"/>). Víceznakové značky a „na znamení" jdou normálním fontem.</summary>
    internal static bool JePiktogram(string znak) => SymbolMap.JePiktogram(znak);

    /// <summary>
    /// Znak, který se pro daný datový atribut skutečně kreslí ze symbolového fontu. Pozice glyfů
    /// v Timetable fontu se **neshodují s písmeny číselníku**, takže bez překladu vyjede cizí
    /// piktogram. Přesně proto se „na znamení" kreslí textem: na `x` je vláček, ne křížek.
    /// Neznámé znaky se vrací nezměněné - u `H`/`F` (bezbariér) a kroužkových dnů to sedí a funguje.
    ///
    /// Pozice ověřené 2026-08-03 **vytažením obrysů glyfů z `Fonts/TimeTable2.ttf`** (jména glyfů
    /// v fontu jsou obecná, takže se musí kreslit a prohlédnout - postup v `elementy-architektura.md`):
    /// `w` 0x77 = železniční **vagon** · `x` 0x78 = lokomotiva · `z` 0x7A = **autobus** ·
    /// `L` 0x4C = **jízdní kolo** · `y` 0x79 = restaurace.
    /// ⚠️ Pozice pro clo, paragrafy, metro, letadlo a loď **ověřené nejsou** - až je někdo bude mít
    /// v datech, musí projít stejnou kontrolou, jinak vyjede cizí piktogram.
    /// </summary>
    internal static string SymbolGlyph(string znak) => SymbolMap.Glyf(znak);

    /// <summary>Znak k vykreslení pro značku dané kategorie - přeloží jen sémantické značky
    /// (<see cref="NoteCategories.Semanticke"/>), ostatní vrátí nezměněné. Agenda MHD posílá značku
    /// jako <c>$c(kód)</c>, tedy přímo pozici ve fontu, takže plošný překlad podle písmene by jí
    /// nakreslil cizí piktogram.</summary>
    internal static string GlyfDleKategorie(string znak, string kategorie) =>
        NoteCategories.Semanticke.Contains(kategorie) ? SymbolGlyph(znak) : znak;

    // ─── DataSource klíče (editovatelné v šabloně) ──────────────────────────

    public string ZastavkyDataSource { get; set; } = "ZASTAVKY";
    public string SkupinyDnuDataSource { get; set; } = "SKUPINY_DNU";
    public string OdjezdyDataSource { get; set; } = "ODJEZDY";
    public string PoznamkyDataSource { get; set; } = "POZNAMKY";

    // ─── Cache ─────────────────────────────────────────────────────────────
    // Build helpery parsují vnořené JsonArray při každém volání.
    // Cache zajistí, že se parsuje jednou per DataContext reference.

    [JsonIgnore] private DataContext? _cachedDataRef;
    [JsonIgnore] private List<ZastavkaData>? _cachedZastavky;
    [JsonIgnore] private List<SkupinaDnuData>? _cachedSkupinyDnu;
    [JsonIgnore] private Dictionary<string, Dictionary<int, List<OdjezdData>>>? _cachedOdjezdy;
    [JsonIgnore] private List<PoznamkaData>? _cachedPoznamky;

    /// <summary>Invaliduje cache - volat při změně DataSource klíčů.</summary>
    protected void InvalidateCache() => _cachedDataRef = null;

    private bool IsCacheValid(DataContext? data) =>
        data is not null && ReferenceEquals(data, _cachedDataRef);

    /// <summary>
    /// Nastaví _cachedDataRef a vyčistí všechny cached kolekce.
    /// Volat na začátku každého Build helperu při detekci nového DataContext.
    /// </summary>
    private void SetCacheRef(DataContext data)
    {
        if (!ReferenceEquals(data, _cachedDataRef))
        {
            _cachedDataRef = data;
            _cachedZastavky = null;
            _cachedSkupinyDnu = null;
            _cachedOdjezdy = null;
            _cachedPoznamky = null;
        }
    }

    // ─── Protected Build helpery pro podtřídy (s cache) ─────────────────────

    protected List<ZastavkaData> BuildZastavky(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedZastavky is not null)
            return _cachedZastavky;

        var rows = data.GetRows(ZastavkyDataSource);
        var result = new List<ZastavkaData>(rows.Count);

        foreach (var row in rows)
        {
            var z = new ZastavkaData
            {
                Id            = row.GetString(StopFields.Id),
                Popis         = row.GetString(StopFields.StopName),
                CasJizdy      = row.GetString(StopFields.TravelTime),
                Poradi        = ParseInt(row.GetString(StopFields.Poradi)),
                JeAktualni    = ParseBool(row.GetString(StopFields.JeAktualni)),
                Odsazeni      = ParseInt(row.GetString("ODSAZENI")),
                NaZnameni     = ParseBool(row.GetString("NA_ZNAMENI")),
                Nastupiste    = row.GetString(StopFields.Nastupiste),
                NastupisteSpoju = row.GetString(StopFields.NastupisteSpoju),
                Sloupek       = row.GetString(StopFields.Sloupek),
                SloupekSpoju  = row.GetString(StopFields.SloupekSpoju),
                SloupekPopis  = row.GetString(StopFields.SloupekPopis),
                SourceRow     = row,
            };

            // Vnořená pole - z JsonArray
            if (row.GetValue(StopFields.Zone) is JsonArray zonyArr)
                z.Zony = zonyArr.Select(n => n?.ToString() ?? "").Where(s => s.Length > 0).ToList();

            if (row.GetValue("ATRIBUTY") is JsonArray attrArr)
                z.Atributy = attrArr
                    .Where(n => n is not null)
                    .Select(n => new AtributData
                    {
                        Znak  = PoleZaznamu(n, "ZNAK"),
                        Popis = PoleZaznamu(n, "POPIS")
                    }).ToList();

            if (row.GetValue("POZNAMKY_ZASTAVKY") is JsonArray pozArr)
                z.PoznamkyZastavky = pozArr
                    .Where(n => n is not null)
                    .Select(n => new PoznamkaZastavkyData
                    {
                        Znak = PoleZaznamu(n, "ZNAK"),
                        Typ  = ParseTypPoznamky(NullIfEmpty(PoleZaznamu(n, "TYP"))),
                        Text = PoleZaznamu(n, "TEXT")
                    }).ToList();

            // časy per varianta trasy (dynamické TrasaMin) - číslo / „<" / "" jako string
            if (row.GetValue("TRASA_MIN") is JsonArray tmArr)
                z.TrasaMin = tmArr.Select(n => n?.ToString() ?? "").ToList();

            // označník každé varianty (zarovnané s TrasaMin) - pro režim „Řádek pro každý označník"
            if (row.GetValue("NASTUPISTE_VARIANT") is JsonArray nvArr)
                z.NastupisteVariant = nvArr.Select(n => n?.ToString() ?? "").ToList();

            if (row.GetValue("SLOUPEK_VARIANT") is JsonArray svArr)
                z.SloupekVariant = svArr.Select(n => n?.ToString() ?? "").ToList();

            result.Add(z);
        }

        _cachedZastavky = result;
        return result;
    }

    protected List<SkupinaDnuData> BuildSkupinyDnu(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedSkupinyDnu is not null)
            return _cachedSkupinyDnu;

        var rows = data.GetRows(SkupinyDnuDataSource);
        var result = rows.Select(row => new SkupinaDnuData
        {
            Id     = row.GetString("ID"),
            Nadpis = row.GetString("NADPIS"),
            Nadpis2 = row.GetString("NADPIS2"),
            Poradi = ParseInt(row.GetString("PORADI"))
        }).OrderBy(s => s.Poradi).ToList();

        _cachedSkupinyDnu = result;
        return result;
    }

    protected Dictionary<string, Dictionary<int, List<OdjezdData>>> BuildOdjezdyBySkupina(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedOdjezdy is not null)
            return _cachedOdjezdy;

        var rows = data.GetRows(OdjezdyDataSource);
        var result = new Dictionary<string, Dictionary<int, List<OdjezdData>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var odjezd = new OdjezdData
            {
                ZastavkaId              = row.GetString("ZASTAVKA_ID"),
                SkupinaDnuId            = row.GetString("SKUPINA_DNU_ID"),
                Hodina                  = ParseInt(row.GetString("HODINA")),
                Minuta                  = ParseInt(row.GetString("MINUTA")),
                ForeColor               = NullIfEmpty(row.GetString("FORE_COLOR")),
                BackColor               = NullIfEmpty(row.GetString("BACK_COLOR")),
            };

            // Anotace - vnořené pole
            if (row.GetValue("ANOTACE") is JsonArray anotArr)
                odjezd.Anotace = anotArr
                    .Where(n => n is not null)
                    .Select(n => new AnotaceData
                    {
                        Typ       = ParseTypAnotace(NullIfEmpty(PoleZaznamu(n, "TYP"))),
                        Znak      = PoleZaznamu(n, "ZNAK"),
                        Kategorie = PoleZaznamu(n, "KATEGORIE"),
                        ForeColor = NullIfEmpty(PoleZaznamu(n, "FORE_COLOR")),
                        BackColor = NullIfEmpty(PoleZaznamu(n, "BACK_COLOR"))
                    }).ToList();

            if (!result.TryGetValue(odjezd.SkupinaDnuId, out var byHour))
            {
                byHour = new Dictionary<int, List<OdjezdData>>();
                result[odjezd.SkupinaDnuId] = byHour;
            }

            if (!byHour.TryGetValue(odjezd.Hodina, out var list))
            {
                list = [];
                byHour[odjezd.Hodina] = list;
            }

            list.Add(odjezd);
        }

        // Seřadit minuty v každé hodině
        foreach (var byHour in result.Values)
            foreach (var list in byHour.Values)
                list.Sort((a, b) => a.Minuta.CompareTo(b.Minuta));

        _cachedOdjezdy = result;
        return result;
    }

    protected List<PoznamkaData> BuildPoznamky(DataContext? data)
    {
        if (data is null) return [];
        SetCacheRef(data);
        if (_cachedPoznamky is not null)
            return _cachedPoznamky;

        var rows = data.GetRows(PoznamkyDataSource);
        var result = rows.Select(row => new PoznamkaData
        {
            Typ       = ParseTypPoznamky(row.GetString("TYP")),
            Znak      = row.GetString("ZNAK"),
            Text      = OdstavcePoznamky(row.GetString("TEXT")),
            ForeColor = NullIfEmpty(row.GetString("FORE_COLOR")),
            BackColor = NullIfEmpty(row.GetString("BACK_COLOR")),
            Kategorie = row.GetString("KATEGORIE"),
            Druh      = row.GetString("DRUH"),
            Verze     = row.GetString("VERZE")
        }).ToList();

        _cachedPoznamky = result;
        return result;
    }

    /// <summary>
    /// Rozdělí text poznámky na odstavce. Agenda posílá <c>#</c> jako <b>oddělovač odstavců</b>
    /// (prázdný odstavec už normalizuje na mezeru, protože tak ho tiskne starý modul).
    /// </summary>
    /// <remarks>
    /// ⚠️ Do 26. 8. to klient neinterpretoval nikde a <c>#</c> se tisklo jako písmeno uprostřed
    /// věty („…u řidiče.#INTEGROVANÉHO SYSTÉMU…", nahlásil Jan).
    /// <para>Převod je <b>tady, na jednom místě</b>, ne u kreslení: výška poznámky se počítá
    /// jinou cestou než render (<c>CalculateHeightFromData</c> vs <c>DrawWrappedBody</c>) a obě
    /// jdou přes <c>TextWrapHelper</c>, který na <c>\n</c> lámat umí. Kdyby se odstavce dělily
    /// u kreslení, výška by o nich nevěděla a legenda by přetékala.</para>
    /// </remarks>
    private static string OdstavcePoznamky(string text) => text.Replace("#", "\n");

    // ─── Parse helpery (typy poznámek/anotací - zastávkové POCO) ────────────

    /// <summary>
    /// Slovník typů poznámek, jak je posílají agendy. Obě rodiny agend pojmenovávají značku
    /// <b>jinak</b> - vývěskové „specialni", linkové „znak" - a obojí musí projít.
    /// </summary>
    /// <remarks>
    /// ⚠️ Do 26. 8. tady „znak" nebyl, takže značky linkového JŘ padaly na <c>Text</c> a legenda je
    /// kreslila <b>surovým písmenem</b>: „vyčká" vyšlo jako <c>m</c> místo dutého trojúhelníku,
    /// pracovní dny jako <c>X</c> místo zkřížených kladiv. V tabulce se přitom kreslily správně
    /// (řádek platnosti jde přes <c>SymbolMap</c> napřímo), takže táž značka vypadala v tabulce
    /// jinak než ve své vlastní legendě. Celý aparát piktogramů linkového JŘ se nikdy nespustil.
    /// <para>Neznámé slovo padá na <c>Text</c> záměrně - starší agenda nesmí spadnout. Proto to
    /// nehlásí chybu za běhu a hlídá to <c>PoznamkaTypDriftTests</c>: nový typ v <c>.cls</c> musí
    /// být i tady, jinak test spadne.</para>
    /// </remarks>
    internal static readonly string[] ZnameTypyPoznamky =
        ["nz", "negativniznacka", "specialni", "specialniznak", "znak", "text"];

    /// <summary>Pole vnořeného záznamu (atribut, poznámka zastávky, značka u odjezdu).</summary>
    /// <remarks>Agendy posílají záznamy různě: zastávková jako objekt `{ZNAK, POPIS…}`, linková jen
    /// jako text značky - ten se bere jako značka, ostatní pole zůstanou prázdná. Bez toho prvek
    /// zastávek v šabloně linkového JŘ padal při každém překreslení náhledu (POVED 8. 10.).</remarks>
    private static string PoleZaznamu(JsonNode? n, string klic) => n switch
    {
        JsonObject o           => o[klic]?.ToString() ?? "",
        JsonValue v when klic == "ZNAK" => v.ToString(),
        _                      => "",
    };

    protected static TypPoznamky ParseTypPoznamky(string? s) => s?.ToLowerInvariant() switch
    {
        "nz" or "negativniznacka" => TypPoznamky.NegativniZnacka,
        "specialni" or "specialniznak" or "znak" => TypPoznamky.SpecialniZnak,
        _ => TypPoznamky.Text
    };

    protected static TypAnotace ParseTypAnotace(string? s) => s?.ToLowerInvariant() switch
    {
        "nz" or "negativniznacka" => TypAnotace.NegativniZnacka,
        "specialni" or "specialniznak" => TypAnotace.SpecialniZnak,
        _ => TypAnotace.Text
    };

    // ─── Kopírování base properties ─────────────────────────────────────────

    /// <summary>Kopie generických (styly, grid) + zastávkových (DataSource klíče) properties do klonu.</summary>
    protected void CopyStopScheduleProperties(StopScheduleElementBase target)
    {
        CopyScheduleBaseProperties(target);
        target.ZastavkyDataSource   = ZastavkyDataSource;
        target.SkupinyDnuDataSource = SkupinyDnuDataSource;
        target.OdjezdyDataSource    = OdjezdyDataSource;
        target.PoznamkyDataSource   = PoznamkyDataSource;
    }
}
