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
    internal static bool SymbolJeText(string znak) => znak is "x" or "y" or "z";

    /// <summary>
    /// Znak, který se pro daný datový atribut skutečně kreslí ze symbolového fontu. Pozice glyfů
    /// v Timetable fontu se **neshodují s písmeny číselníku** - autobus (`A`) sedí na `w`, vlak (`L`)
    /// na `x` (lokomotiva; `z` je vagon). Ověřeno v Mapě znaků 2026-08-03 (#13048 bod 1) a přesně
    /// proto se „na znamení" kreslí textem: na `x` je vláček, ne křížek.
    /// Neznámé znaky se vrací nezměněné - u `H`/`F` (bezbariér) a kroužkových dnů to sedí a funguje.
    /// ⚠️ Pozice pro clo, paragrafy, metro, letadlo a loď **ověřené nejsou** - až je někdo bude mít
    /// v datech, musí projít stejnou kontrolou, jinak vyjede cizí piktogram (viz `elementy-architektura.md`).
    /// </summary>
    internal static string SymbolGlyph(string znak) => znak switch
    {
        "A" => "w",   // autobus (0x77)
        "L" => "x",   // vlak - lokomotiva (0x78)
        _   => znak,
    };

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
                CasJizdy      = ParseInt(row.GetString(StopFields.TravelTime)),
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
                        Znak  = n!["ZNAK"]?.ToString() ?? "",
                        Popis = n!["POPIS"]?.ToString() ?? ""
                    }).ToList();

            if (row.GetValue("POZNAMKY_ZASTAVKY") is JsonArray pozArr)
                z.PoznamkyZastavky = pozArr
                    .Where(n => n is not null)
                    .Select(n => new PoznamkaZastavkyData
                    {
                        Znak = n!["ZNAK"]?.ToString() ?? "",
                        Typ  = ParseTypPoznamky(n!["TYP"]?.ToString()),
                        Text = n!["TEXT"]?.ToString() ?? ""
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
                        Typ       = ParseTypAnotace(n!["TYP"]?.ToString()),
                        Znak      = n!["ZNAK"]?.ToString() ?? "",
                        ForeColor = NullIfEmpty(n!["FORE_COLOR"]?.ToString()),
                        BackColor = NullIfEmpty(n!["BACK_COLOR"]?.ToString())
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
            Text      = row.GetString("TEXT"),
            ForeColor = NullIfEmpty(row.GetString("FORE_COLOR")),
            BackColor = NullIfEmpty(row.GetString("BACK_COLOR"))
        }).ToList();

        _cachedPoznamky = result;
        return result;
    }

    // ─── Parse helpery (typy poznámek/anotací - zastávkové POCO) ────────────

    protected static TypPoznamky ParseTypPoznamky(string? s) => s?.ToLowerInvariant() switch
    {
        "nz" or "negativniznacka" => TypPoznamky.NegativniZnacka,
        "specialni" or "specialniznak" => TypPoznamky.SpecialniZnak,
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
